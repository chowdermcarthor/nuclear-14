// #Misfits Change
using Content.Server._Misfits.GhoulReversal;
using Content.Server.Humanoid;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Server.Ghoul;

namespace Content.Server._Misfits.EntityEffects.Effects.GhoulReversal;

/// <summary>
/// Reagent effect that reverses a ghoul back into a human, but only if they were
/// ghoulified within the configured time window (default 12 real hours).
/// Round-start ghouls (no GhoulificationTimeComponent) are permanently blocked.
/// </summary>
[UsedImplicitly]
public sealed partial class GhoulReversalEffect : EntityEffect
{
    /// <summary>
    /// Species IDs this effect can reverse.
    /// </summary>
    [DataField]
    public List<string> GhoulSpecies = new() { "Ghoul", "GhoulGlowing" };

    /// <summary>
    /// Species to revert to.
    /// </summary>
    [DataField]
    public string TargetSpecies = "Human";

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-ghoul-reversal", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        var entityManager = args.EntityManager;
        var uid = args.TargetEntity;

        if (!entityManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var appearance))
            return;

        if (!GhoulSpecies.Contains(appearance.Species))
            return;

        var popupSys = args.EntityManager.EntitySysManager.GetEntitySystem<SharedPopupSystem>();

        // Round-start ghouls have no timer — permanently blocked
        if (!entityManager.TryGetComponent<GhoulificationTimeComponent>(uid, out var timeComp))
        {
            popupSys.PopupEntity(
                Loc.GetString("ghoul-reversal-reagent-too-old"),
                uid, uid, PopupType.MediumCaution);
            return;
        }

        var elapsed = DateTime.UtcNow - timeComp.GhoulifiedAtUtc;
        if (elapsed.TotalHours > timeComp.ReversibleWindowHours)
        {
            popupSys.PopupEntity(
                Loc.GetString("ghoul-reversal-reagent-too-old"),
                uid, uid, PopupType.MediumCaution);
            return;
        }

        // Validate target species
        if (!IoCManager.Resolve<IPrototypeManager>().TryIndex<SpeciesPrototype>(TargetSpecies, out _))
            return;

        var humanoidSys = entityManager.EntitySysManager.GetEntitySystem<HumanoidAppearanceSystem>();
        humanoidSys.SetSpecies(uid, TargetSpecies);

        // Remove the feral tracker so they don't go feral again
        entityManager.RemoveComponentDeferred<FeralGhoulifyComponent>(uid);
        entityManager.RemoveComponent<GhoulificationTimeComponent>(uid);

        popupSys.PopupEntity(
            Loc.GetString("ghoul-reversal-reagent-self"),
            uid, uid, PopupType.LargeCaution);
        popupSys.PopupEntity(
            Loc.GetString("ghoul-reversal-reagent-others", ("target", uid)),
            uid, Filter.PvsExcept(uid), true, PopupType.MediumCaution);
    }
}
