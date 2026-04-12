// #Misfits Add - Disease diagnoser machine system.
// Accepts a used swab, processes the sample, and ejects a diagnosis paper.

using Content.Server.Paper;
using Content.Shared._Misfits.Disease;
using Content.Shared._Misfits.Disease.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Disease.Machines;

/// <summary>
/// Handles the disease diagnoser machine: insert a used swab → wait for processing
/// → ejects a diagnosis paper listing the disease. The paper is used for vaccines.
/// </summary>
public sealed class DiseaseDiagnosisSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PaperSystem _paper = default!;

    public override void Initialize()
    {
        base.Initialize();

        // When a swab is inserted into the diagnoser
        SubscribeLocalEvent<DiseaseDiagnoserComponent, InteractUsingEvent>(OnDiagnoserInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DiseaseDiagnoserComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Running)
                continue;

            comp.Accumulator += frameTime;
            if (comp.Accumulator < comp.ProcessDelay)
                continue;

            // Processing complete — spawn the diagnosis report
            comp.Running = false;
            comp.Accumulator = 0f;

            if (comp.QueuedDisease == null)
                continue;

            var diseaseId = comp.QueuedDisease.Value;
            comp.QueuedDisease = null;

            SpawnDiagnosisPaper(uid, diseaseId);
        }
    }

    /// <summary>
    /// Insert a used swab into the diagnoser to begin processing.
    /// </summary>
    private void OnDiagnoserInteractUsing(EntityUid uid, DiseaseDiagnoserComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Must be a used disease swab with a sample
        if (!TryComp<DiseaseSwabComponent>(args.Used, out var swab))
            return;

        if (!swab.Used || swab.Disease == null)
        {
            _popup.PopupEntity(Loc.GetString("disease-diagnoser-no-sample"), uid, args.User);
            args.Handled = true;
            return;
        }

        if (comp.Running)
        {
            _popup.PopupEntity(Loc.GetString("disease-diagnoser-already-running"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Consume the swab and start processing
        comp.QueuedDisease = swab.Disease;
        comp.Running = true;
        comp.Accumulator = 0f;

        // Delete the used swab
        QueueDel(args.Used);

        _popup.PopupEntity(Loc.GetString("disease-diagnoser-started"), uid, args.User);
        args.Handled = true;
    }

    /// <summary>
    /// Spawn a diagnosis paper at the diagnoser's location with the disease info.
    /// </summary>
    private void SpawnDiagnosisPaper(EntityUid diagnoser, ProtoId<DiseasePrototype> diseaseId)
    {
        if (!_proto.TryIndex(diseaseId, out var disease))
            return;

        var paper = Spawn("DiagnosisReportPaper", Transform(diagnoser).Coordinates);

        // Tag the paper with the disease ID so the vaccinator can read it
        var diagComp = EnsureComp<DiseaseDiagnosisComponent>(paper);
        diagComp.Disease = diseaseId;

        // Write disease information to the paper for player readability
        var diseaseName = Loc.GetString(disease.Name);
        var content = Loc.GetString("disease-diagnosis-report",
            ("disease", diseaseName),
            ("diseaseId", diseaseId.Id));
        _paper.SetContent(paper, content);

        _popup.PopupEntity(Loc.GetString("disease-diagnoser-finished"), diagnoser);
    }
}
