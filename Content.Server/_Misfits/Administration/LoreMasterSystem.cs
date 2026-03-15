// #Misfits Change /Add/ - Server-side Loremaster admin system.
// Handles faction info queries and objective issuance for the Loremaster admin tab.
// Sends styled faction-flavored notifications to the recipient so the orders feel in-world.
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared._Misfits.Administration;
using Content.Shared._Misfits.Objectives; // #Misfits Add - CustomObjectiveComponent
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems; // #Misfits Change /Fix/ - RA0002
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects; // #Misfits Add - MetaDataSystem
using Robust.Shared.Player;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Administration;

/// <summary>
/// Server system backing the LoreMaster admin tab.
/// Responds to faction info queries and issues objectives to the highest-ranking online member
/// of a faction, then delivers a styled in-world notification styled after how objectives
/// are presented in the character (C) menu.
/// </summary>
public sealed class LoreMasterSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!; // #Misfits Change /Fix/ - RA0002
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!; // #Misfits Add - set custom objective title/description

    // Faction display names and chat-markup colours used when formatting notifications.
    private static readonly Dictionary<string, (string Display, string Color)> FactionConfig = new()
    {
        ["NCR"]                = ("NCR",                  "#cc2f2f"),
        ["BrotherhoodOfSteel"] = ("Brotherhood of Steel", "#4f81bd"),
        ["CaesarLegion"]       = ("Caesar's Legion",      "#8B0000"),
        ["Vault"]              = ("Vault",                "#FFD700"),  // #Misfits Add - gold for vault-tec
        ["Townsfolk"]          = ("Town",                 "#8FBC8F"),  // #Misfits Add - muted green for townsfolk
    };

    // #Misfits Add - maps faction ID → objective-issuer locale key for the custom objective prototype.
    // This ensures the custom order groups under the correct coloured faction header in the C menu.
    private static readonly Dictionary<string, string> FactionIssuer = new()
    {
        ["NCR"]                = "ncr",
        ["BrotherhoodOfSteel"] = "brotherhoodofsteel",
        ["CaesarLegion"]       = "caesarlegion",
        ["Vault"]              = "vault",       // #Misfits Add
        ["Townsfolk"]          = "townsfolk",   // #Misfits Add
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestLoreMasterFactionInfoEvent>(OnRequestFactionInfo);
        SubscribeNetworkEvent<IssueLoreMasterObjectiveEvent>(OnIssueObjective);
        SubscribeNetworkEvent<IssueCustomLoreMasterObjectiveEvent>(OnIssueCustomObjective); // #Misfits Add
        SubscribeNetworkEvent<RemoveLoreMasterObjectiveEvent>(OnRemoveObjective); // #Misfits Add - objective removal
    }

    // ── Query ─────────────────────────────────────────────────────────────

    private void OnRequestFactionInfo(RequestLoreMasterFactionInfoEvent msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.IsAdmin(args.SenderSession))
            return;

        var members = BuildMemberList(msg.FactionId);
        RaiseNetworkEvent(new LoreMasterFactionInfoEvent
        {
            FactionId = msg.FactionId,
            Members = members,
        }, args.SenderSession);
    }

    // ── Issue ─────────────────────────────────────────────────────────────

    private void OnIssueObjective(IssueLoreMasterObjectiveEvent msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.IsAdmin(args.SenderSession))
            return;

        var members = BuildMemberList(msg.FactionId);
        if (members.Count == 0)
        {
            Respond(args.SenderSession, false, $"No online {msg.FactionId} members found.");
            return;
        }

        // #Misfits Tweak - use the admin-selected target; fall back to highest-ranked if none specified.
        var targetName = string.IsNullOrEmpty(msg.TargetPlayerName) ? members[0].PlayerName : msg.TargetPlayerName;
        if (!members.Any(m => m.PlayerName == targetName))
        {
            Respond(args.SenderSession, false, $"'{targetName}' is not an online member of {msg.FactionId}.");
            return;
        }

        ICommonSession? targetSession = null;
        var actorQuery = EntityQueryEnumerator<ActorComponent>();
        while (actorQuery.MoveNext(out _, out var actor))
        {
            if (actor.PlayerSession.Name == targetName)
            {
                targetSession = actor.PlayerSession;
                break;
            }
        }

        if (targetSession?.AttachedEntity == null)
        {
            Respond(args.SenderSession, false, "Could not locate target session.");
            return;
        }

        if (!_minds.TryGetMind(targetSession.AttachedEntity.Value, out var mindId, out var mind))
        {
            Respond(args.SenderSession, false, "Target has no mind.");
            return;
        }

        // Create the objective — FactionRequirementComponent will gate this correctly
        var objective = _objectives.TryCreateObjective(mindId, mind, msg.ObjectivePrototype);
        if (objective == null)
        {
            // #Misfits Change /Fix/ - distinguish the three failure modes:
            // 1. Prototype doesn't exist at all.
            // 2. Unique constraint — target already holds this exact objective.
            // 3. Runtime cancellation — no valid target (enemy offline) or steal limit hit.
            if (!_protoManager.HasIndex<EntityPrototype>(msg.ObjectivePrototype))
            {
                Respond(args.SenderSession, false,
                    $"Unknown objective prototype '{msg.ObjectivePrototype}'.");
            }
            else if (mind.AllObjectives.Any(o =>
                MetaData(o).EntityPrototype?.ID == msg.ObjectivePrototype))
            {
                // unique: true blocked the assignment — the member already has this objective
                Respond(args.SenderSession, false,
                    $"Could not assign '{msg.ObjectivePrototype}'.\n{targetName} already has this objective assigned.");
            }
            else
            {
                Respond(args.SenderSession, false,
                    $"Could not assign '{msg.ObjectivePrototype}'.\nKill objectives require at least one enemy player to be online. Steal objectives may already be at their limit.");
            }
            return;
        }

        _minds.AddObjective(mindId, mind, objective.Value);

        // Deliver an in-world, faction-flavoured notification styled like the C menu objective display
        var info = _objectives.GetInfo(objective.Value, mindId, mind);
        if (info != null)
            SendFactionOrderNotification(targetSession, msg.FactionId, info.Value.Title, info.Value.Description);

        // #Misfits Add - broadcast to admin chat so all admins see who issued what to whom
        var objTitle = info?.Title ?? msg.ObjectivePrototype;
        var objDesc  = info?.Description ?? string.Empty;
        var adminNotice = string.IsNullOrEmpty(objDesc)
            ? $"{args.SenderSession.Name} issued '{objTitle}' to {targetName} ({msg.FactionId})"
            : $"{args.SenderSession.Name} issued '{objTitle}' to {targetName} ({msg.FactionId}): {objDesc}";
        _chat.SendAdminAnnouncement(adminNotice);

        Respond(args.SenderSession, true,
            $"Issued '{msg.ObjectivePrototype}' to {targetName}.");
    }

    // ── Custom objective issuance ──────────────────────────────────────────

    // #Misfits Add - issue a fully admin-typed (freeform) objective to the top-ranked faction member.
    private void OnIssueCustomObjective(IssueCustomLoreMasterObjectiveEvent msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.IsAdmin(args.SenderSession))
            return;

        // Basic server-side sanity — client validates too, but never trust client-only checks.
        var title = msg.CustomTitle.Trim();
        if (string.IsNullOrEmpty(title))
        {
            Respond(args.SenderSession, false, "Custom order title cannot be empty.");
            return;
        }

        var members = BuildMemberList(msg.FactionId);
        if (members.Count == 0)
        {
            Respond(args.SenderSession, false, $"No online {msg.FactionId} members found.");
            return;
        }

        // #Misfits Tweak - use the admin-selected target; fall back to highest-ranked if none specified.
        var targetName = string.IsNullOrEmpty(msg.TargetPlayerName) ? members[0].PlayerName : msg.TargetPlayerName;
        if (!members.Any(m => m.PlayerName == targetName))
        {
            Respond(args.SenderSession, false, $"'{targetName}' is not an online member of {msg.FactionId}.");
            return;
        }

        ICommonSession? targetSession = null;
        var actorQuery = EntityQueryEnumerator<ActorComponent>();
        while (actorQuery.MoveNext(out _, out var actor))
        {
            if (actor.PlayerSession.Name == targetName)
            {
                targetSession = actor.PlayerSession;
                break;
            }
        }

        if (targetSession?.AttachedEntity == null)
        {
            Respond(args.SenderSession, false, "Could not locate target session.");
            return;
        }

        if (!_minds.TryGetMind(targetSession.AttachedEntity.Value, out var mindId, out var mind))
        {
            Respond(args.SenderSession, false, "Target has no mind.");
            return;
        }

        // Spawn the placeholder prototype and override its metadata before adding to the mind.
        // We bypass TryCreateObjective here because there is no target-selection event to fire.
        var uid = Spawn("LoreMasterCustomObjective");
        _metaData.SetEntityName(uid, title);
        _metaData.SetEntityDescription(uid, msg.CustomDescription.Trim());

        // Set the issuer to match the faction so it groups correctly in the C menu.
        var issuerKey = FactionIssuer.GetValueOrDefault(msg.FactionId, "unknown");
        var objComp = Comp<ObjectiveComponent>(uid);
        objComp.Issuer = issuerKey;
        Dirty(uid, objComp);

        _minds.AddObjective(mindId, mind, uid);

        // Deliver in-world faction notification (same style as preset objectives).
        SendFactionOrderNotification(targetSession, msg.FactionId, title, msg.CustomDescription.Trim());

        // #Misfits Add - broadcast to admin chat so all admins see who issued what to whom
        var customDesc = msg.CustomDescription.Trim();
        var customAdminNotice = string.IsNullOrEmpty(customDesc)
            ? $"{args.SenderSession.Name} issued order to {targetName} ({msg.FactionId}): {title}"
            : $"{args.SenderSession.Name} issued order to {targetName} ({msg.FactionId}): {title} — {customDesc}";
        _chat.SendAdminAnnouncement(customAdminNotice);

        Respond(args.SenderSession, true,
            $"Custom order issued to {targetName}: {title}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // ── Objective removal ──────────────────────────────────────────────────

    // #Misfits Add - remove a specific objective from a faction member by title match.
    private void OnRemoveObjective(RemoveLoreMasterObjectiveEvent msg, EntitySessionEventArgs args)
    {
        if (!_adminManager.IsAdmin(args.SenderSession))
            return;

        var targetName = msg.TargetPlayerName.Trim();
        if (string.IsNullOrEmpty(targetName))
        {
            Respond(args.SenderSession, false, "No target specified.");
            return;
        }

        // Find the target's session
        ICommonSession? targetSession = null;
        var actorQuery = EntityQueryEnumerator<ActorComponent>();
        while (actorQuery.MoveNext(out _, out var actor))
        {
            if (actor.PlayerSession.Name == targetName)
            {
                targetSession = actor.PlayerSession;
                break;
            }
        }

        if (targetSession?.AttachedEntity == null)
        {
            Respond(args.SenderSession, false, $"Could not locate session for '{targetName}'.");
            return;
        }

        if (!_minds.TryGetMind(targetSession.AttachedEntity.Value, out var mindId, out var mind))
        {
            Respond(args.SenderSession, false, "Target has no mind.");
            return;
        }

        // Find the objective by title match and remove it
        for (var i = 0; i < mind.Objectives.Count; i++)
        {
            var objUid = mind.Objectives[i];
            if (Name(objUid) == msg.ObjectiveTitle)
            {
                _minds.TryRemoveObjective(mindId, mind, i);

                // Notify the player that the order was revoked
                SendFactionOrderRevocationNotification(targetSession, msg.FactionId, msg.ObjectiveTitle);

                // Admin chat announcement
                _chat.SendAdminAnnouncement(
                    $"{args.SenderSession.Name} revoked order from {targetName} ({msg.FactionId}): {msg.ObjectiveTitle}");

                Respond(args.SenderSession, true,
                    $"Removed '{msg.ObjectiveTitle}' from {targetName}.");
                return;
            }
        }

        Respond(args.SenderSession, false,
            $"No objective titled '{msg.ObjectiveTitle}' found on {targetName}.");
    }

    /// <summary>
    /// Sends an in-world notification to a player when an order is revoked.
    /// </summary>
    private void SendFactionOrderRevocationNotification(ICommonSession session, string factionId, string title)
    {
        if (session.AttachedEntity == null)
            return;

        var (displayName, color) = FactionConfig.TryGetValue(factionId, out var cfg)
            ? cfg
            : ("Command", "#ffffff");

        var plain = $"[{displayName}] Order Revoked: {title}";
        var wrapped = $"[color={color}][bold][{displayName}] — Order Revoked[/bold][/color]\n" +
                      $"[italic]{title}[/italic]";

        _chat.ChatMessageToOne(
            ChatChannel.Server,
            plain,
            wrapped,
            EntityUid.Invalid,
            hideChat: false,
            session.Channel);
    }

    /// <summary>
    /// Builds a list of online members in the given faction, sorted by job weight descending (most senior first).
    /// </summary>
    private List<LoreMasterMemberInfo> BuildMemberList(string factionId)
    {
        var result = new List<LoreMasterMemberInfo>();

        // Iterate all entities that are both in the faction and controlled by a player
        var query = EntityQueryEnumerator<NpcFactionMemberComponent, ActorComponent>();
        while (query.MoveNext(out var entity, out var faction, out var actor))
        {
            if (actor.PlayerSession.Status != SessionStatus.InGame)
                continue;
            // #Misfits Change /Fix/ - use IsMember instead of .Factions.Contains() to avoid RA0002
            if (!_npcFaction.IsMember(entity, factionId))
                continue;
            if (!_minds.TryGetMind(entity, out var mindId, out var mind))
                continue;

            var jobName = "Unknown";
            var jobWeight = 0;
            if (_jobs.MindTryGetJob(mindId, out _, out var jobProto))
            {
                jobName = Loc.GetString(jobProto.Name);
                jobWeight = jobProto.Weight;
            }

            var objectives = new List<LoreMasterObjectiveSnapshot>();
            foreach (var objUid in mind.AllObjectives)
            {
                var info = _objectives.GetInfo(objUid, mindId, mind);
                if (info == null)
                    continue;
                objectives.Add(new LoreMasterObjectiveSnapshot
                {
                    Title = info.Value.Title,
                    Description = info.Value.Description,
                    Progress = info.Value.Progress,
                });
            }

            result.Add(new LoreMasterMemberInfo
            {
                PlayerName = actor.PlayerSession.Name,
                JobName = jobName,
                JobWeight = jobWeight,
                Objectives = objectives,
            });
        }

        // Highest weight (most senior rank) first
        result.Sort((a, b) => b.JobWeight.CompareTo(a.JobWeight));
        return result;
    }

    /// <summary>
    /// Sends an in-world, faction-flavoured notification to a player when new orders are issued.
    /// The chat message follows the same colour and layout as objectives shown in the C character menu,
    /// so it does not look out of place as a prefab-style alert.
    /// </summary>
    private void SendFactionOrderNotification(ICommonSession session, string factionId, string title, string description)
    {
        if (session.AttachedEntity == null)
            return;

        var (displayName, color) = FactionConfig.TryGetValue(factionId, out var cfg)
            ? cfg
            : ("Command", "#ffffff");

        // Chat message is formatted to match how the C menu shows objectives:
        // coloured faction issuer header, bold title, italic description body.
        var plain = $"[{displayName}] New Orders: {title} — {description}";
        var wrapped = $"[color={color}][bold][{displayName}] — New Orders Received[/bold][/color]\n" +
                      $"[bold]{title}[/bold]\n" +
                      $"[italic]{description}[/italic]";

        _chat.ChatMessageToOne(
            ChatChannel.Server,
            plain,
            wrapped,
            EntityUid.Invalid,
            hideChat: false,
            session.Channel);
    }

    private void Respond(ICommonSession admin, bool success, string message)
    {
        RaiseNetworkEvent(new LoreMasterObjectiveResultEvent
        {
            Success = success,
            Message = message,
        }, admin);
    }
}
