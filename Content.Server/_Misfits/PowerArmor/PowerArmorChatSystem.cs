// #Misfits Add: Broadcasts local emote chat when power armor deploys or retracts via the suit toggle action.
// #Misfits Add: Also handles emote chat for the brace stance hotkey (PowerArmorBraceSystem).
// Chat messages are throttled via MisfitsEmoteThrottleSystem to prevent spam from rapid toggling.
using Content.Server._Misfits.Chat.Systems;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared._Misfits.PowerArmor;

namespace Content.Server._Misfits.PowerArmor;

/// <summary>
/// Sends nearby emote chat when a power armor suit fully deploys or retracts its attached pieces.
/// Also sends emote chat when the wearer activates or deactivates the brace stance via hotkey.
/// Fires only on the actual action paths, not on equip or unequip lifecycle events.
/// </summary>
public sealed class PowerArmorChatSystem : EntitySystem
{
    [Dependency] private readonly MisfitsEmoteThrottleSystem _throttle = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Suit fold/unfold toggle.
        SubscribeLocalEvent<N14PowerArmorComponent, ToggleableClothingToggledEvent>(OnPowerArmorToggled);

        // Brace stance activate/deactivate.
        SubscribeLocalEvent<PowerArmorBraceComponent, PowerArmorBraceActivatedEvent>(OnBraceActivated);
        SubscribeLocalEvent<PowerArmorBraceComponent, PowerArmorBraceDeactivatedEvent>(OnBraceDeactivated);
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

        _throttle.SendThrottledEmote(args.User, "powerarmor", message);
    }

    private void OnBraceActivated(EntityUid uid, PowerArmorBraceComponent component, PowerArmorBraceActivatedEvent args)
    {
        if (TerminatingOrDeleted(args.User))
            return;

        var armorName = Exists(args.Armor) ? Name(args.Armor) : "power armor";
        var message = Loc.GetString("misfits-chat-power-armor-brace-activate", ("armor", armorName));

        _throttle.SendThrottledEmote(args.User, "brace", message);
    }

    private void OnBraceDeactivated(EntityUid uid, PowerArmorBraceComponent component, PowerArmorBraceDeactivatedEvent args)
    {
        if (TerminatingOrDeleted(args.User))
            return;

        var armorName = Exists(args.Armor) ? Name(args.Armor) : "power armor";
        var message = Loc.GetString("misfits-chat-power-armor-brace-deactivate", ("armor", armorName));

        _throttle.SendThrottledEmote(args.User, "brace", message);
    }
}