// #Misfits Change - Pip-Boy tactical map cartridge system
using System.Collections.Generic;
using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Content.Shared._Misfits.WastelandMap;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Misfits.WastelandMap;

public sealed class WastelandMapCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly WastelandMapSystem _wastelandMap = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    // Track which (cartridge, loader) pairs have had their UIFragment set up by the client.
    // We must NOT send WastelandMapBoundUserInterfaceState until after CartridgeUiReadyEvent
    // fires, because UpdateCartridgeUiState uses the same PDA UI key as CartridgeLoaderUiState.
    // If both are sent in the same server tick, the BUI system only transmits the last one,
    // causing the client to never receive CartridgeLoaderUiState and never attach the fragment.
    private readonly HashSet<(EntityUid Cartridge, EntityUid Loader)> _readyPairs = new();

    private const float UpdateInterval = 1f;
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WastelandMapComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<WastelandMapComponent, CartridgeMessageEvent>(OnCartridgeMessage);
        SubscribeLocalEvent<WastelandMapComponent, CartridgeDeactivatedEvent>(OnDeactivated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator = 0f;

        foreach (var (cartridge, loader) in _readyPairs)
        {
            if (!TryComp<WastelandMapComponent>(cartridge, out var map))
                continue;

            if (!TryComp<CartridgeLoaderComponent>(loader, out var loaderComp) ||
                loaderComp.ActiveProgram != cartridge)
            {
                continue;
            }

            var feed = GetLoaderFeed(loader, map);
            var mapId = GetViewerMapId(loader);
            var state = _wastelandMap.BuildState(map, mapId, feed);
            _cartridgeLoader.UpdateCartridgeUiState(loader, state, loader: loaderComp);
        }
    }

    private void OnUiReady(EntityUid uid, WastelandMapComponent component, CartridgeUiReadyEvent args)
    {
        // Mark this pair as ready so the Update loop can safely push state from here on.
        _readyPairs.Add((uid, args.Loader));

        var feed = GetLoaderFeed(args.Loader, component);
        var state = _wastelandMap.BuildState(component, GetViewerMapId(args.Loader), feed);
        _cartridgeLoader.UpdateCartridgeUiState(args.Loader, state);
    }

    private void OnDeactivated(EntityUid uid, WastelandMapComponent component, CartridgeDeactivatedEvent args)
    {
        // Clean up so we don't update a no-longer-active program.
        _readyPairs.RemoveWhere(p => p.Cartridge == uid);
    }

    private void OnCartridgeMessage(EntityUid uid, WastelandMapComponent component, CartridgeMessageEvent args)
    {
        var loaderUid = GetEntity(args.LoaderUid);
        if (!TryComp<CartridgeLoaderComponent>(loaderUid, out var loader))
            return;

        var actor = args.Actor;
        var feed = GetLoaderFeed(loaderUid, component);
        var mapId = GetViewerMapId(loaderUid);
        var changed = args switch
        {
            WastelandMapCartridgeAddAnnotationMessageEvent add => _wastelandMap.TryAddAnnotation(actor, component, mapId, add.Annotation, feed),
            WastelandMapCartridgeRemoveAnnotationMessageEvent remove => _wastelandMap.TryRemoveAnnotation(actor, component, mapId, remove.Index, feed),
            WastelandMapCartridgeClearAnnotationsMessageEvent => _wastelandMap.TryClearAnnotations(actor, component, mapId, feed),
            _ => false,
        };

        if (!changed)
            return;

        var state = _wastelandMap.BuildState(component, mapId, feed);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state, loader: loader);
    }

    private MapId GetViewerMapId(EntityUid loaderUid)
    {
        if (TryComp<UserInterfaceComponent>(loaderUid, out var ui))
        {
            foreach (var actor in _ui.GetActors((loaderUid, ui), PdaUiKey.Key))
            {
                return Transform(actor).MapID;
            }
        }

        return Transform(loaderUid).MapID;
    }

    private WastelandMapTacticalFeedKind GetLoaderFeed(EntityUid loaderUid, WastelandMapComponent component)
    {
        if (!TryComp<PdaComponent>(loaderUid, out var pda) || pda.ContainedId is not { } containedId)
            return _wastelandMap.GetEffectiveFeed(component);

        if (_tag.HasTag(containedId, "IdCardBrotherhood"))
            return WastelandMapTacticalFeedKind.Brotherhood;

        if (_tag.HasTag(containedId, "IdCardVault"))
            return WastelandMapTacticalFeedKind.Vault;

        if (_tag.HasTag(containedId, "IdCardNCR"))
            return WastelandMapTacticalFeedKind.NCR;

        if (_tag.HasTag(containedId, "IdCardEnclave")) // #Misfits Change
            return WastelandMapTacticalFeedKind.Enclave;

        return _wastelandMap.GetEffectiveFeed(component);
    }
}