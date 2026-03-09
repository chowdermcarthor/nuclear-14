// #Misfits Change - Ported from Delta-V chronic pain system
using Content.Client._Misfits.ChronicPain.Overlays;
using Content.Shared._Misfits.ChronicPain.Components;
using Content.Shared._Misfits.ChronicPain.EntitySystems;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._Misfits.ChronicPain.EntitySystems;

/// <summary>
///     Client-side system for chronic pain. Manages the visual overlay.
/// </summary>
public sealed class ChronicPainSystem : SharedChronicPainSystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    private ChronicPainOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ChronicPainOverlay();
    }

    protected override void OnChronicPainInit(Entity<ChronicPainComponent> entity, ref ComponentInit args)
    {
        base.OnChronicPainInit(entity, ref args);

        if (entity.Owner != _playerManager.LocalEntity)
            return;

        // Only show overlay if pain isn't suppressed
        if (!IsChronicPainSuppressed((entity.Owner, entity.Comp)))
            _overlayMan.AddOverlay(_overlay);
    }

    protected override void OnChronicPainShutdown(Entity<ChronicPainComponent> entity, ref ComponentShutdown args)
    {
        if (entity.Owner != _playerManager.LocalEntity)
            return;

        _overlayMan.RemoveOverlay(_overlay);
    }

    protected override void OnPlayerAttached(Entity<ChronicPainComponent> entity, ref LocalPlayerAttachedEvent args)
    {
        if (!IsChronicPainSuppressed((entity.Owner, entity.Comp)))
            _overlayMan.AddOverlay(_overlay);
    }

    protected override void OnPlayerDetached(Entity<ChronicPainComponent> entity, ref LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Sync overlay visibility with suppression state each tick
        if (_playerManager.LocalEntity is not { } player)
            return;

        if (!TryComp<ChronicPainComponent>(player, out var comp))
            return;

        var isSuppressed = IsChronicPainSuppressed((player, comp));

        if (isSuppressed && _overlayMan.HasOverlay<ChronicPainOverlay>())
            _overlayMan.RemoveOverlay(_overlay);
        else if (!isSuppressed && !_overlayMan.HasOverlay<ChronicPainOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }
}
