// #Misfits Add - Core disease tick/spread/cure system.
// Handles disease progression, stage-based effects, airborne/contact transmission,
// equipment protection, natural immunity, and cure condition checking.

using Content.Server._Misfits.Disease.Effects;
using Content.Server.Chat.Systems;
using Content.Server.Medical;
using Content.Shared._Misfits.Disease;
using Content.Shared._Misfits.Disease.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Disease;

/// <summary>
/// Core disease system. Every tick it advances each carried disease, rolls effects
/// based on the current stage, checks cure conditions, and handles airborne/contact
/// spread to nearby entities.
/// </summary>
public sealed class DiseaseSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    /// <summary>Range in tiles for airborne disease spread.</summary>
    private const float AirborneRange = 3f;

    public override void Initialize()
    {
        base.Initialize();

        // When a mask/gas-mask with DiseaseProtection is equipped, mark it active
        SubscribeLocalEvent<DiseaseProtectionComponent, GotEquippedEvent>(OnProtectionEquipped);
        SubscribeLocalEvent<DiseaseProtectionComponent, GotUnequippedEvent>(OnProtectionUnequipped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DiseaseCarrierComponent>();
        while (query.MoveNext(out var uid, out var carrier))
        {
            // Skip dead entities
            if (TryComp<MobStateComponent>(uid, out var mobState) && _mobState.IsDead(uid, mobState))
                continue;

            if (carrier.Diseases.Count == 0)
            {
                // No active diseases — remove the marker if present
                RemCompDeferred<DiseasedComponent>(uid);
                continue;
            }

            // Ensure marker component is present while sick
            EnsureComp<DiseasedComponent>(uid);

            // Iterate over a snapshot to allow removal during loop
            foreach (var (diseaseId, accumulatedTime) in new Dictionary<ProtoId<DiseasePrototype>, float>(carrier.Diseases))
            {
                if (!_proto.TryIndex(diseaseId, out var disease))
                    continue;

                // Advance the per-disease tick accumulator
                if (!carrier.Accumulators.TryGetValue(diseaseId, out var tickAcc))
                    tickAcc = 0f;

                tickAcc += frameTime;

                if (tickAcc < disease.TickTime)
                {
                    carrier.Accumulators[diseaseId] = tickAcc;
                    // Still advance total accumulated time for JustWaitCure
                    carrier.Diseases[diseaseId] = accumulatedTime + frameTime;
                    Dirty(uid, carrier);
                    continue;
                }

                // Reset tick accumulator and advance total time
                carrier.Accumulators[diseaseId] = 0f;
                var newTotal = accumulatedTime + tickAcc;
                carrier.Diseases[diseaseId] = newTotal;

                // Determine current stage (0-indexed)
                var currentStage = GetStage(disease, newTotal);

                // Check cure conditions — if ALL cures for this stage pass, cure the disease
                if (CheckCures(uid, disease, currentStage))
                {
                    CureDisease(uid, carrier, diseaseId);
                    continue;
                }

                // Roll effects for current stage
                var effectArgs = new DiseaseEffectArgs(uid, disease, EntityManager);
                foreach (var effect in disease.Effects)
                {
                    // Effect only applies to certain stages
                    if (effect.Stages.Count > 0 && !effect.Stages.Contains(currentStage))
                        continue;

                    // Probability check
                    if (!_random.Prob(effect.Probability))
                        continue;

                    // Special handling for sneeze/cough — needs injected dependencies
                    if (effect is DiseaseSnough snough)
                    {
                        HandleSnough(uid, carrier, disease, snough);
                        continue;
                    }

                    effect.Effect(effectArgs);
                }

                Dirty(uid, carrier);
            }
        }
    }

    /// <summary>
    /// Calculate the current stage index from accumulated time and stage thresholds.
    /// Stages list contains ascending time thresholds; stage 0 is below first threshold.
    /// </summary>
    private static int GetStage(DiseasePrototype disease, float accumulatedTime)
    {
        for (var i = disease.Stages.Count - 1; i >= 0; i--)
        {
            if (accumulatedTime >= disease.Stages[i])
                return i + 1;
        }
        return 0;
    }

    /// <summary>
    /// Check if all cure conditions for the current stage are met.
    /// Returns true only if the disease has at least one cure and all cures pass.
    /// </summary>
    private bool CheckCures(EntityUid uid, DiseasePrototype disease, int stage)
    {
        var relevantCures = new List<DiseaseCure>();
        foreach (var cure in disease.Cures)
        {
            if (cure.Stages.Count > 0 && !cure.Stages.Contains(stage))
                continue;
            relevantCures.Add(cure);
        }

        if (relevantCures.Count == 0)
            return false;

        var args = new DiseaseEffectArgs(uid, disease, EntityManager);
        foreach (var cure in relevantCures)
        {
            if (!cure.Cure(args))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Removes a disease from a carrier and grants immunity.
    /// </summary>
    public void CureDisease(EntityUid uid, DiseaseCarrierComponent carrier, ProtoId<DiseasePrototype> diseaseId)
    {
        carrier.Diseases.Remove(diseaseId);
        carrier.Accumulators.Remove(diseaseId);
        carrier.PastDiseases.Add(diseaseId); // immunity
        Dirty(uid, carrier);

        if (carrier.Diseases.Count == 0)
            RemCompDeferred<DiseasedComponent>(uid);
    }

    /// <summary>
    /// Attempt to infect an entity with a disease. Respects immunity, resistance, and equipment protection.
    /// </summary>
    public bool TryInfect(EntityUid target, ProtoId<DiseasePrototype> diseaseId, float spreadChance = 0.5f)
    {
        if (!TryComp<DiseaseCarrierComponent>(target, out var carrier))
            return false;

        // Already infected or immune
        if (carrier.Diseases.ContainsKey(diseaseId))
            return false;
        if (carrier.PastDiseases.Contains(diseaseId))
            return false;
        if (carrier.NaturalImmunities.Contains(diseaseId))
            return false;

        // Equipment protection reduces chance
        var protection = GetEquipmentProtection(target);
        var finalChance = spreadChance * (1f - carrier.DiseaseResist) * (1f - protection);

        if (!_random.Prob(Math.Clamp(finalChance, 0f, 1f)))
            return false;

        carrier.Diseases[diseaseId] = 0f;
        carrier.Accumulators[diseaseId] = 0f;
        EnsureComp<DiseasedComponent>(target);
        Dirty(target, carrier);
        return true;
    }

    /// <summary>
    /// Add a disease directly without resistance checks (admin, vaccine failure, etc.).
    /// </summary>
    public void AddDisease(EntityUid target, ProtoId<DiseasePrototype> diseaseId)
    {
        var carrier = EnsureComp<DiseaseCarrierComponent>(target);

        if (carrier.Diseases.ContainsKey(diseaseId))
            return;

        carrier.Diseases[diseaseId] = 0f;
        carrier.Accumulators[diseaseId] = 0f;
        EnsureComp<DiseasedComponent>(target);
        Dirty(target, carrier);
    }

    /// <summary>
    /// Handle sneeze/cough: play emote, then attempt airborne spread if configured.
    /// </summary>
    private void HandleSnough(EntityUid uid, DiseaseCarrierComponent carrier,
        DiseasePrototype disease, DiseaseSnough snough)
    {
        // Play the emote (Sneeze or Cough)
        _chat.TryEmoteWithChat(uid, snough.EmoteId, forceEmote: true);

        if (!snough.AirTransmit || !disease.Airborne)
            return;

        // Spread to nearby entities
        var nearby = _lookup.GetEntitiesInRange(uid, AirborneRange);
        foreach (var target in nearby)
        {
            if (target == uid)
                continue;

            TryInfect(target, disease.ID, disease.ContactSpread);
        }
    }

    /// <summary>
    /// Calculate total equipment disease protection for an entity.
    /// Checks all equipped items with DiseaseProtectionComponent.
    /// </summary>
    private float GetEquipmentProtection(EntityUid uid)
    {
        var totalProtection = 0f;

        // Check common protective slots: mask, head, outerClothing
        string[] slots = { "mask", "head", "outerClothing" };
        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(uid, slot, out var item))
                continue;
            if (!TryComp<DiseaseProtectionComponent>(item, out var prot))
                continue;
            if (!prot.IsActive)
                continue;

            totalProtection += prot.Protection;
        }

        // Clamp to [0, 1]
        return Math.Clamp(totalProtection, 0f, 1f);
    }

    // -- Equipment protection event handlers --

    private void OnProtectionEquipped(EntityUid uid, DiseaseProtectionComponent comp, GotEquippedEvent args)
    {
        comp.IsActive = true;
        Dirty(uid, comp);
    }

    private void OnProtectionUnequipped(EntityUid uid, DiseaseProtectionComponent comp, GotUnequippedEvent args)
    {
        comp.IsActive = false;
        Dirty(uid, comp);
    }
}
