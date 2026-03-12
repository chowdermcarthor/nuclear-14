// #Misfits Add: Broadcasts local emote chat when power armor deploys or retracts via the suit toggle action.
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared._Misfits.PowerArmor;

namespace Content.Server._Misfits.PowerArmor;

/// <summary>
/// Sends nearby emote chat when a power armor suit fully deploys or retracts its attached pieces.
/// Fires only on the actual suit toggle path, not on equip or unequip.
/// </summary>
public sealed class PowerArmorChatSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<N14PowerArmorComponent, ToggleableClothingToggledEvent>(OnPowerArmorToggled);
    }

    private void OnPowerArmorToggled(EntityUid uid, N14PowerArmorComponent component, ref ToggleableClothingToggledEvent args)
    {
        if (TerminatingOrDeleted(args.User))
            return;

        var armorName = Exists(uid)
            ? Name(uid)
            : "power armor";

        var message = Loc.GetString(
            args.Activated ? "misfits-chat-power-armor-close" : "misfits-chat-power-armor-open",
            ("armor", armorName));

        _chat.TrySendInGameICMessage(args.User, message, InGameICChatType.Emote,
            ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }
}