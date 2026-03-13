// #Misfits Add - EntityEffect that installs DrugSpecialBoostComponent on a player each
// metabolism tick while a SPECIAL-boosting drug is being metabolised.
// Follows the same pattern as MovespeedModifier — each tick refreshes a timer so the
// effect persists for as long as the reagent remains in the bloodstream.
// Referenced from chems.yml as !type:SpecialStatBoostEffect.

using Content.Shared._Misfits.SpecialStats;
using Content.Shared.EntityEffects;
using Content.Shared.Movement.Systems;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.EntityEffects.Effects;

/// <summary>
/// Applies a temporary SPECIAL stat boost to the target entity for each tick of reagent
/// metabolism. Non-zero boost fields produce in-game effects via <see cref="DrugSpecialBoostSystem"/>:
/// <list type="bullet">
///   <item><see cref="StrengthBoost"/>   — melee damage bonus (+4 % per point)</item>
///   <item><see cref="PerceptionBoost"/> — gun spread / recoil reduction (+0.5 % per point)</item>
///   <item><see cref="AgilityBoost"/>    — walk/sprint speed bonus (+1.5 % per point)</item>
///   <item><see cref="IntelligenceBoost"/>, <see cref="EnduranceBoost"/>, etc. — stored, reserved for future systems</item>
/// </list>
/// <para>
/// Set <see cref="StatusLifetime"/> to control how long the boost lingers after the last
/// metabolism tick (i.e. the "grace window" once the reagent amount hits zero).
/// </para>
/// </summary>
[UsedImplicitly]
public sealed partial class SpecialStatBoostEffect : EntityEffect
{
    [DataField] public int StrengthBoost;
    [DataField] public int PerceptionBoost;
    [DataField] public int EnduranceBoost;
    [DataField] public int CharismaBoost;
    [DataField] public int AgilityBoost;
    [DataField] public int IntelligenceBoost;
    [DataField] public int LuckBoost;

    /// <summary>
    /// Seconds the boost persists after the last metabolism tick.
    /// Combined with RefreshTimer's "push forward" logic this keeps the effect active
    /// for the full duration the reagent is in the bloodstream, expiring shortly after.
    /// Default: 4 s gives enough headroom for all standard metabolism rates.
    /// </summary>
    [DataField]
    public float StatusLifetime = 4f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var parts = new List<string>();
        if (StrengthBoost     != 0) parts.Add($"STR +{StrengthBoost}");
        if (PerceptionBoost   != 0) parts.Add($"PER +{PerceptionBoost}");
        if (EnduranceBoost    != 0) parts.Add($"END +{EnduranceBoost}");
        if (CharismaBoost     != 0) parts.Add($"CHA +{CharismaBoost}");
        if (AgilityBoost      != 0) parts.Add($"AGI +{AgilityBoost}");
        if (IntelligenceBoost != 0) parts.Add($"INT +{IntelligenceBoost}");
        if (LuckBoost         != 0) parts.Add($"LCK +{LuckBoost}");
        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        var uid  = args.TargetEntity;
        var comp = args.EntityManager.EnsureComponent<DrugSpecialBoostComponent>(uid);

        // Write each non-zero boost value. Using direct assignment means the last
        // drug to tick (within the same game frame) wins per stat. Since all current
        // drugs use the same boost magnitude and run simultaneously, this is correct.
        // Overlapping drugs with DIFFERENT magnitudes for the same stat will resolve
        // to whichever effect fires last in that tick — acceptable given current content.
        if (StrengthBoost     != 0) comp.StrengthBoost     = StrengthBoost;
        if (PerceptionBoost   != 0) comp.PerceptionBoost   = PerceptionBoost;
        if (EnduranceBoost    != 0) comp.EnduranceBoost    = EnduranceBoost;
        if (CharismaBoost     != 0) comp.CharismaBoost     = CharismaBoost;
        if (AgilityBoost      != 0) comp.AgilityBoost      = AgilityBoost;
        if (IntelligenceBoost != 0) comp.IntelligenceBoost = IntelligenceBoost;
        if (LuckBoost         != 0) comp.LuckBoost         = LuckBoost;

        // Push the expiry window forward — keeps the boost alive while metabolising.
        // StatusLifetime is NOT scaled by reagent amount: the boost is binary
        // (active while drug is present) rather than dose-proportional.
        var boostSys = args.EntityManager.System<DrugSpecialBoostSystem>();
        boostSys.RefreshTimer(uid, comp, StatusLifetime);

        // If an agility boost is active, trigger a movement speed recalc so the
        // player feels the change immediately (same pattern as MovespeedModifier.cs).
        if (AgilityBoost != 0)
            args.EntityManager.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(uid);

        args.EntityManager.Dirty(uid, comp);
    }
}
