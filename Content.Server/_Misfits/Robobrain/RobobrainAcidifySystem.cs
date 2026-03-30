// Handles the "Acidify Brain" instant action for player-controlled Robobrain chassis.
// When triggered the player receives a private farewell message, nearby players see
// an IC emote, and the player's mind is ejected to ghost form. The brain (and its
// MMI container) is destroyed to prevent re-use after the acid sequence completes.
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Mind;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Mind;
using Content.Shared._Misfits.Robobrain;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Robobrain;

/// <summary>
/// Grants the Acidify Brain action to <see cref="RobobrainChassisComponent"/> entities on
/// initialisation, and handles the action event to ghost the player and destroy their brain.
/// </summary>
public sealed class RobobrainAcidifySystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Grant the intrinsic acidify action when the chassis entity is initialised.
        SubscribeLocalEvent<RobobrainChassisComponent, ComponentInit>(OnChassisInit);

        // Filter to RobobrainChassisComponent so only Robobrain entities handle this event.
        SubscribeLocalEvent<RobobrainChassisComponent, RobobrainAcidifyBrainEvent>(OnAcidify);
    }

    private void OnChassisInit(EntityUid uid, RobobrainChassisComponent comp, ComponentInit args)
    {
        // Spawn the action entity and store its UID in the component for later reference.
        _actions.AddAction(uid, ref comp.AcidifyActionEntity, "ActionRobobrainAcidifyBrain");
    }

    private void OnAcidify(EntityUid uid, RobobrainChassisComponent comp, RobobrainAcidifyBrainEvent args)
    {
        // Only proceed if an actual player mind is controlling this chassis.
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        // --- Private message to the departing player ---
        // Must happen before the mind transfer so the message reaches the right session.
        if (TryComp<ActorComponent>(uid, out var actor))
        {
            var selfMsg = Loc.GetString("robobrain-acidify-self");
            _chatManager.ChatMessageToOne(
                ChatChannel.Local,
                selfMsg,
                selfMsg,
                EntityUid.Invalid,
                hideChat: false,
                actor.PlayerSession.Channel);
        }

        // --- Emote visible to nearby players ---
        // TrySendInGameICMessage prefixes the Robobrain's name automatically.
        _chat.TrySendInGameICMessage(
            uid,
            Loc.GetString("robobrain-acidify-warning"),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);

        // --- Eject mind to ghost ---
        // BorgSystem.OnMindRemoved fires automatically after this and calls BorgDeactivate(),
        // which toggles the chassis off and updates the HasPlayer appearance flag.
        _mind.TransferTo(mindId, null, createGhost: true, mind: mind);

        // --- Destroy inserted brain / MMI ---
        // The borg_brain container holds the MMI; the biological brain is inside the MMI.
        // Deleting the MMI entity cascades to delete the brain within it.
        if (_container.TryGetContainer(uid, "borg_brain", out var brainContainer))
        {
            // Iterate in reverse so removals do not invalidate upcoming indexes.
            for (var i = brainContainer.ContainedEntities.Count - 1; i >= 0; i--)
            {
                var brainEntity = brainContainer.ContainedEntities[i];
                _container.Remove(brainEntity, brainContainer);
                QueueDel(brainEntity);
            }
        }

        args.Handled = true;
    }
}
