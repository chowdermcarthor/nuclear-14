// #Misfits Add - Disease swab interaction system.
// Handles using a sterile swab on a sick entity to collect a disease sample.

using Content.Server._Misfits.Disease;
using Content.Shared._Misfits.Disease.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server._Misfits.Disease.Machines;

/// <summary>
/// Handles swab → target interaction to collect a disease sample.
/// The swab stores the disease prototype ID for use in diagnosers and vaccinators.
/// </summary>
public sealed class DiseaseSwabSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseSwabComponent, AfterInteractEvent>(OnSwabAfterInteract);
        SubscribeLocalEvent<DiseaseSwabComponent, DiseaseSwabDoAfterEvent>(OnSwabDoAfter);
    }

    /// <summary>
    /// When a swab is used on a target entity, start a DoAfter to collect a sample.
    /// </summary>
    private void OnSwabAfterInteract(EntityUid uid, DiseaseSwabComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        // Already used
        if (comp.Used)
        {
            _popup.PopupEntity(Loc.GetString("disease-swab-already-used"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Target must be a disease carrier with at least one active disease
        if (!TryComp<DiseaseCarrierComponent>(args.Target, out var carrier) || carrier.Diseases.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("disease-swab-no-disease"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Start the DoAfter to collect the sample
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(comp.SwabDelay),
            new DiseaseSwabDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(Loc.GetString("disease-swab-collecting"), uid, args.User);
        args.Handled = true;
    }

    /// <summary>
    /// On DoAfter completion, collect a random disease from the target.
    /// </summary>
    private void OnSwabDoAfter(EntityUid uid, DiseaseSwabComponent comp, DiseaseSwabDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null)
            return;

        if (!TryComp<DiseaseCarrierComponent>(args.Target, out var carrier) || carrier.Diseases.Count == 0)
            return;

        // Pick a random disease from the target
        var diseases = carrier.Diseases.Keys.ToList();
        comp.Disease = _random.Pick(diseases);
        comp.Used = true;
        Dirty(uid, comp);

        _popup.PopupEntity(Loc.GetString("disease-swab-collected"), uid, args.User);
        args.Handled = true;
    }
}
