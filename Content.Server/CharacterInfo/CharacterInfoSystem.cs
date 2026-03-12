using System.Linq;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared._Misfits.Currency.Components;
using Content.Shared._Misfits.PlayerData.Components;
using Content.Shared.CharacterInfo;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;

namespace Content.Server.CharacterInfo;

public sealed class CharacterInfoSystem : EntitySystem
{
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestCharacterInfoEvent>(OnRequestCharacterInfoEvent);
    }

    private void OnRequestCharacterInfoEvent(RequestCharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue
            || args.SenderSession.AttachedEntity != GetEntity(msg.NetEntity))
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        var objectives = new Dictionary<string, List<ObjectiveInfo>>();
        var jobTitle = "No Profession";
        string? briefing = null;
        var specials = new List<string>(); //Nuclear14 SPECIAL
        if (_minds.TryGetMind(entity, out var mindId, out var mind))
        {
            // Get objectives
            foreach (var objective in mind.AllObjectives)
            {
                var info = _objectives.GetInfo(objective, mindId, mind);
                if (info == null)
                    continue;

                // group objectives by their issuer
                var issuer = Comp<ObjectiveComponent>(objective).Issuer;
                if (!objectives.ContainsKey(issuer))
                    objectives[issuer] = new List<ObjectiveInfo>();
                objectives[issuer].Add(info.Value);
            }

            if (_jobs.MindTryGetJobName(mindId, out var jobName))
                jobTitle = jobName;

            // Get briefing
            briefing = _roles.MindGetBriefing(mindId);

            // Nuclear14 Get specials
            specials = mind.AllSpecials.ToList();
        }

        var evnt = new CharacterInfoEvent(GetNetEntity(entity), jobTitle, objectives, briefing, specials);

        // #Misfits Add - Attach persistent SPECIAL / stats / history if available
        if (TryComp<PersistentPlayerDataComponent>(entity, out var playerData))
        {
            evnt.PersistentStats = new CharacterPersistentStats
            {
                Strength     = playerData.Strength,
                Perception   = playerData.Perception,
                Endurance    = playerData.Endurance,
                Charisma     = playerData.Charisma,
                Agility      = playerData.Agility,
                Intelligence = playerData.Intelligence,
                Luck         = playerData.Luck,

                MobKills     = playerData.MobKills,
                Deaths       = playerData.Deaths,
                RoundsPlayed = playerData.RoundsPlayed,

                HistoryLog      = new List<string>(playerData.HistoryLog),
                StatsConfirmed  = playerData.StatsConfirmed,
            };
        }

        // #Misfits Add - Attach currency balance from PersistentCurrencyComponent
        if (TryComp<PersistentCurrencyComponent>(entity, out var currency))
        {
            evnt.PersistentStats ??= new CharacterPersistentStats();
            evnt.PersistentStats.Bottlecaps = currency.Bottlecaps;
        }

        RaiseNetworkEvent(evnt, args.SenderSession);
    }
}
