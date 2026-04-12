// #Misfits Add - Disease vaccinator machine system.
// Accepts a diagnosis paper, processes it, and ejects a vaccine syringe.
// Also handles using a vaccine on a target to grant disease immunity.

using Content.Shared._Misfits.Disease;
using Content.Shared._Misfits.Disease.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Disease.Machines;

/// <summary>
/// Vaccinator machine: insert a diagnosis paper → wait → ejects a vaccine.
/// Also handles vaccine → target injection to grant immunity.
/// </summary>
public sealed class DiseaseVaccineSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Vaccinator machine: insert diagnosis paper
        SubscribeLocalEvent<DiseaseVaccinatorComponent, InteractUsingEvent>(OnVaccinatorInteractUsing);

        // Vaccine syringe: use on a target to grant immunity
        SubscribeLocalEvent<DiseaseVaccineComponent, AfterInteractEvent>(OnVaccineAfterInteract);
        SubscribeLocalEvent<DiseaseVaccineComponent, DiseaseVaccineDoAfterEvent>(OnVaccineDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DiseaseVaccinatorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Running)
                continue;

            comp.Accumulator += frameTime;
            if (comp.Accumulator < comp.ProcessDelay)
                continue;

            // Processing complete — spawn the vaccine
            comp.Running = false;
            comp.Accumulator = 0f;

            if (comp.QueuedDisease == null)
                continue;

            var diseaseId = comp.QueuedDisease.Value;
            comp.QueuedDisease = null;

            SpawnVaccine(uid, diseaseId);
        }
    }

    // -- Vaccinator machine --

    /// <summary>
    /// Insert a diagnosis paper into the vaccinator to begin vaccine production.
    /// </summary>
    private void OnVaccinatorInteractUsing(EntityUid uid, DiseaseVaccinatorComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Must be a diagnosis paper with a disease
        if (!TryComp<DiseaseDiagnosisComponent>(args.Used, out var diag) || diag.Disease == null)
        {
            _popup.PopupEntity(Loc.GetString("disease-vaccinator-no-diagnosis"), uid, args.User);
            args.Handled = true;
            return;
        }

        if (comp.Running)
        {
            _popup.PopupEntity(Loc.GetString("disease-vaccinator-already-running"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Consume the paper and start processing
        comp.QueuedDisease = diag.Disease;
        comp.Running = true;
        comp.Accumulator = 0f;

        QueueDel(args.Used);

        _popup.PopupEntity(Loc.GetString("disease-vaccinator-started"), uid, args.User);
        args.Handled = true;
    }

    /// <summary>
    /// Spawn a vaccine entity at the vaccinator's position.
    /// </summary>
    private void SpawnVaccine(EntityUid vaccinator, ProtoId<DiseasePrototype> diseaseId)
    {
        if (!_proto.TryIndex(diseaseId, out var disease))
            return;

        var vaccine = Spawn("Vaccine", Transform(vaccinator).Coordinates);

        // Set the vaccine's disease target
        var vaccComp = EnsureComp<DiseaseVaccineComponent>(vaccine);
        vaccComp.Disease = diseaseId;
        vaccComp.Used = false;
        Dirty(vaccine, vaccComp);

        // Update the entity name to reflect the disease
        var diseaseName = Loc.GetString(disease.Name);
        _meta.SetEntityName(vaccine, Loc.GetString("disease-vaccine-named", ("disease", diseaseName)));

        _popup.PopupEntity(Loc.GetString("disease-vaccinator-finished"), vaccinator);
    }

    // -- Vaccine injection --

    /// <summary>
    /// Use vaccine on a target to inject and grant immunity.
    /// </summary>
    private void OnVaccineAfterInteract(EntityUid uid, DiseaseVaccineComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        if (comp.Used || comp.Disease == null)
        {
            _popup.PopupEntity(Loc.GetString("disease-vaccine-empty"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Start a DoAfter for injection
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(comp.InjectDelay),
            new DiseaseVaccineDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(Loc.GetString("disease-vaccine-injecting"), uid, args.User);
        args.Handled = true;
    }

    /// <summary>
    /// Vaccine injection completed — grant immunity to the target.
    /// </summary>
    private void OnVaccineDoAfter(EntityUid uid, DiseaseVaccineComponent comp, DiseaseVaccineDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null)
            return;

        if (comp.Disease == null)
            return;

        var target = args.Target.Value;
        var carrier = EnsureComp<DiseaseCarrierComponent>(target);

        // Grant immunity (add to past diseases)
        carrier.PastDiseases.Add(comp.Disease.Value);

        // If already infected with this disease, cure it
        if (carrier.Diseases.ContainsKey(comp.Disease.Value))
            _disease.CureDisease(target, carrier, comp.Disease.Value);

        Dirty(target, carrier);

        // Mark vaccine as used
        comp.Used = true;
        Dirty(uid, comp);

        _popup.PopupEntity(Loc.GetString("disease-vaccine-injected"), uid, args.User);
        args.Handled = true;
    }
}
