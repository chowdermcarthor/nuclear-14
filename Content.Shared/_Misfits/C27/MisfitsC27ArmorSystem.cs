using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Misfits.C27;

// #Misfits Add - Shared system that gates equipping of C-27 armor / helmet items: only entities
// whose HumanoidAppearanceComponent.Species == "C27" can put them on. Mirrors the pattern used
// by PowerArmorProficiencySystem so prediction cancels the equip animation immediately on the
// client side.
public sealed class MisfitsC27ArmorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MisfitsC27ArmorComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
        // #Misfits Add - Present C-27 chassis as synthetic for identity purposes. Mirrors
        // SharedBorgSystem.OnTryGetIdentityShortInfo: when a C-27 is seen without ID, use the
        // entity name (e.g. "c-27 humanoid robot") instead of the default "old person" fallback.
        SubscribeLocalEvent<TryGetIdentityShortInfoEvent>(OnTryGetIdentityShortInfo);
    }

    private void OnTryGetIdentityShortInfo(TryGetIdentityShortInfoEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(args.ForActor, out var humanoid))
            return;

        if (humanoid.Species != "C27" && humanoid.Species != "C27NCR" && humanoid.Species != "C27BoS")
            return;

        args.Title = Name(args.ForActor).Trim();
        args.Handled = true;
    }

    private void OnEquipAttempt(Entity<MisfitsC27ArmorComponent> item, ref BeingEquippedAttemptEvent args)
    {
        // Trophy / admin-spawned variants: skip the gate entirely.
        if (!item.Comp.RequiresC27Species)
            return;

        // Non-humanoids (e.g. animal spawns, dummies) trivially can't wear C-27 armor.
        if (!TryComp<HumanoidAppearanceComponent>(args.EquipTarget, out var humanoid))
        {
            args.Reason = "c27-armor-species-required";
            args.Cancel();
            return;
        }

        // Species ProtoId compares case-sensitively as a string.
        // #Misfits Tweak - accept all three C-27 chassis variants (Generic / NCR / BoS).
        // Previously only the Generic 'C27' species was permitted, which blocked NCR and
        // BoS C-27 jobs from equipping their own faction armor kits.
        if (humanoid.Species == "C27" || humanoid.Species == "C27NCR" || humanoid.Species == "C27BoS")
            return;

        args.Reason = "c27-armor-species-required";
        args.Cancel();
    }
}
