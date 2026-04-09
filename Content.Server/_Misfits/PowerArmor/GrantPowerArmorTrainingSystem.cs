// #Misfits Add - Server system that grants PowerArmorProficiency when a training manual is used in hand.
using Content.Shared._Misfits.PowerArmor;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Misfits.PowerArmor;

/// <summary>
///     Handles the <see cref="GrantPowerArmorTrainingComponent"/> UseInHand path.
///     Reading a power armor training manual grants <see cref="PowerArmorProficiencyComponent"/>
///     to the wielder and consumes the book.
/// </summary>
public sealed class GrantPowerArmorTrainingSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrantPowerArmorTrainingComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(EntityUid uid, GrantPowerArmorTrainingComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;

        // Already trained — inform the player and do nothing.
        if (HasComp<PowerArmorProficiencyComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("power-armor-training-already-trained"), uid, user, PopupType.Small);
            return;
        }

        // Grant proficiency.
        AddComp<PowerArmorProficiencyComponent>(user);

        _popup.PopupEntity(Loc.GetString("power-armor-training-learned"), uid, user, PopupType.Large);

        if (comp.SoundOnUse != null)
            _audio.PlayEntity(comp.SoundOnUse, user, user);

        args.Handled = true;

        // Manual is single-use — consume it.
        QueueDel(uid);
    }
}
