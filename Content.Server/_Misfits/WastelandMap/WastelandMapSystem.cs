// #Misfits Change - Wasteland Map server system
using System;
using System.Collections.Generic;
using Content.Server.Access.Components;
using Content.Shared.Access.Components;
using Content.Shared.Tag;
using Content.Shared._Misfits.WastelandMap;
using Content.Shared.Nyanotrasen.NPC.Components.Faction;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Misfits.WastelandMap;

/// <summary>
/// Sends the WastelandMap state (including world bounds) to the client BUI
/// when the UI is opened. Box2 is not NetSerializable, so we unpack it into
/// 4 floats inside the BUI state.
/// </summary>
public sealed class WastelandMapSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const int MaxSharedAnnotations = 128;
    private const int MaxStrokePoints = 512; // 256 UV points × 2 floats each
    private const float UpdateInterval = 0.5f;
    private float _updateAccumulator;
    private readonly Dictionary<(MapId MapId, WastelandMapTacticalFeedKind Feed), List<WastelandMapAnnotation>> _sharedFeedAnnotations = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WastelandMapComponent, AfterActivatableUIOpenEvent>(OnAfterOpen);
        SubscribeLocalEvent<WastelandMapComponent, WastelandMapAddAnnotationMessage>(OnAddAnnotationMessage);
        SubscribeLocalEvent<WastelandMapComponent, WastelandMapRemoveAnnotationMessage>(OnRemoveAnnotationMessage);
        SubscribeLocalEvent<WastelandMapComponent, WastelandMapClearAnnotationsMessage>(OnClearAnnotationsMessage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateInterval)
            return;

        _updateAccumulator = 0f;

        var query = EntityQueryEnumerator<WastelandMapComponent, UserInterfaceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var map, out var ui, out var xform))
        {
            var viewerMap = xform.MapID;
            foreach (var actor in _uiSystem.GetActors((uid, ui), WastelandMapUiKey.Key))
            {
                viewerMap = Transform(actor).MapID;
                break;
            }

            _uiSystem.SetUiState((uid, ui), WastelandMapUiKey.Key, BuildState(map, viewerMap));
        }
    }

    private void OnAfterOpen(EntityUid uid, WastelandMapComponent comp, AfterActivatableUIOpenEvent args)
    {
        var userMap = Transform(args.User).MapID;
        _uiSystem.SetUiState(uid, WastelandMapUiKey.Key, BuildState(comp, userMap));
    }

    private void OnAddAnnotationMessage(EntityUid uid, WastelandMapComponent comp, WastelandMapAddAnnotationMessage args)
    {
        if (!TryAddAnnotation(args.Actor, comp, Transform(args.Actor).MapID, args.Annotation))
            return;

        UpdateMapUi(uid, comp, Transform(args.Actor).MapID);
    }

    private void OnRemoveAnnotationMessage(EntityUid uid, WastelandMapComponent comp, WastelandMapRemoveAnnotationMessage args)
    {
        if (!TryRemoveAnnotation(args.Actor, comp, Transform(args.Actor).MapID, args.Index))
            return;

        UpdateMapUi(uid, comp, Transform(args.Actor).MapID);
    }

    private void OnClearAnnotationsMessage(EntityUid uid, WastelandMapComponent comp, WastelandMapClearAnnotationsMessage args)
    {
        if (!TryClearAnnotations(args.Actor, comp, Transform(args.Actor).MapID))
            return;

        UpdateMapUi(uid, comp, Transform(args.Actor).MapID);
    }

    public WastelandMapBoundUserInterfaceState BuildState(WastelandMapComponent comp, MapId mapId, WastelandMapTacticalFeedKind? feedOverride = null)
    {
        var feed = feedOverride ?? GetEffectiveFeed(comp);
        var trackedBlips = GetTrackedBlips(feed, mapId, comp.WorldBounds);
        var sharedAnnotations = GetSharedAnnotations(comp, mapId, feed).ToArray();

        return new WastelandMapBoundUserInterfaceState(
            comp.MapTitle,
            comp.MapTexturePath.ToString(),
            comp.CompactHud,
            comp.WorldBounds.Left,
            comp.WorldBounds.Bottom,
            comp.WorldBounds.Right,
            comp.WorldBounds.Top,
            trackedBlips,
            sharedAnnotations);
    }

    public WastelandMapTacticalFeedKind GetEffectiveFeed(WastelandMapComponent comp)
    {
        if (comp.TacticalFeed != WastelandMapTacticalFeedKind.None)
            return comp.TacticalFeed;

        return comp.TrackBrotherhoodHolotags
            ? WastelandMapTacticalFeedKind.Brotherhood
            : WastelandMapTacticalFeedKind.None;
    }

    public bool TryAddAnnotation(EntityUid actor, WastelandMapComponent comp, MapId mapId, WastelandMapAnnotation annotation, WastelandMapTacticalFeedKind? feedOverride = null)
    {
        var sanitized = SanitizeAnnotation(annotation);
        if (sanitized == null)
            return false;

        var annotations = GetSharedAnnotations(comp, mapId, feedOverride ?? GetEffectiveFeed(comp));
        annotations.Add(sanitized.Value);
        if (annotations.Count > MaxSharedAnnotations)
            annotations.RemoveAt(0);

        return true;
    }

    public bool TryRemoveAnnotation(EntityUid actor, WastelandMapComponent comp, MapId mapId, int index, WastelandMapTacticalFeedKind? feedOverride = null)
    {
        var annotations = GetSharedAnnotations(comp, mapId, feedOverride ?? GetEffectiveFeed(comp));
        if (index < 0 || index >= annotations.Count)
            return false;

        annotations.RemoveAt(index);
        return true;
    }

    public bool TryClearAnnotations(EntityUid actor, WastelandMapComponent comp, MapId mapId, WastelandMapTacticalFeedKind? feedOverride = null)
    {
        var annotations = GetSharedAnnotations(comp, mapId, feedOverride ?? GetEffectiveFeed(comp));
        if (annotations.Count == 0)
            return false;

        annotations.Clear();
        return true;
    }

    private void UpdateMapUi(EntityUid uid, WastelandMapComponent comp, MapId? mapId = null)
    {
        if (!TryComp<UserInterfaceComponent>(uid, out var ui))
            return;

        _uiSystem.SetUiState((uid, ui), WastelandMapUiKey.Key, BuildState(comp, mapId ?? Transform(uid).MapID));
    }

    private static WastelandMapAnnotation? SanitizeAnnotation(WastelandMapAnnotation annotation)
    {
        if (annotation.Type is not (WastelandMapAnnotationType.Marker
            or WastelandMapAnnotationType.Box
            or WastelandMapAnnotationType.Draw))
            return null;

        var label = annotation.Label.Trim();
        if (label.Length > 64)
            label = label[..64].TrimEnd();

        // Draw type: sanitize stroke points
        if (annotation.Type == WastelandMapAnnotationType.Draw)
        {
            var pts = annotation.StrokePoints;
            if (pts == null || pts.Length < 4)
                return null;
            var count = Math.Min(pts.Length & ~1, MaxStrokePoints); // ensure even, cap to max
            var sanitizedPts = new float[count];
            for (var i = 0; i < count; i++)
                sanitizedPts[i] = Math.Clamp(pts[i], 0f, 1f);
            if (string.IsNullOrWhiteSpace(label))
                label = "Drawing";
            return new WastelandMapAnnotation(WastelandMapAnnotationType.Draw, 0f, 0f, 0f, 0f, label, annotation.PackedColor, Math.Clamp(annotation.StrokeWidth, 1f, 12f), sanitizedPts);
        }

        // Marker / Box
        var startX = Math.Clamp(annotation.StartX, 0f, 1f);
        var startY = Math.Clamp(annotation.StartY, 0f, 1f);
        var endX = Math.Clamp(annotation.EndX, 0f, 1f);
        var endY = Math.Clamp(annotation.EndY, 0f, 1f);

        if (string.IsNullOrWhiteSpace(label))
            label = annotation.Type == WastelandMapAnnotationType.Marker ? "Marker" : "Box";

        return new WastelandMapAnnotation(annotation.Type, startX, startY, endX, endY, label, annotation.PackedColor, Math.Clamp(annotation.StrokeWidth, 1f, 12f), null);
    }

    private List<WastelandMapAnnotation> GetSharedAnnotations(WastelandMapComponent comp, MapId mapId, WastelandMapTacticalFeedKind feed)
    {
        if (feed == WastelandMapTacticalFeedKind.None)
            return comp.SharedAnnotations;

        var key = (mapId, feed);
        if (_sharedFeedAnnotations.TryGetValue(key, out var annotations))
            return annotations;

        annotations = new List<WastelandMapAnnotation>(comp.SharedAnnotations);
        _sharedFeedAnnotations[key] = annotations;
        return annotations;
    }

    private WastelandMapTrackedBlip[] GetTrackedBlips(WastelandMapTacticalFeedKind feed, MapId mapId, Box2 bounds)
    {
        return feed switch
        {
            WastelandMapTacticalFeedKind.Brotherhood => GetIdCardBlips(mapId, bounds, "IdCardBrotherhood"),
            WastelandMapTacticalFeedKind.Vault => GetIdCardBlips(mapId, bounds, "IdCardVault"),
            WastelandMapTacticalFeedKind.NCR => GetIdCardBlips(mapId, bounds, "IdCardNCR"),
            WastelandMapTacticalFeedKind.Enclave => GetIdCardBlips(mapId, bounds, "IdCardEnclave"), // #Misfits Change
            _ => [],
        };
    }

    private WastelandMapTrackedBlip[] GetIdCardBlips(MapId mapId, Box2 bounds, string requiredTag)
    {
        var blips = new List<WastelandMapTrackedBlip>();
        var query = EntityQueryEnumerator<PresetIdCardComponent, IdCardComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var presetId, out var idCard, out var xform))
        {
            if (!_tag.HasTag(uid, requiredTag))
                continue;

            var meta = MetaData(uid);

            var mapCoordinates = _transform.GetMapCoordinates(uid, xform);
            if (mapCoordinates.MapId != mapId)
                continue;

            var pos = mapCoordinates.Position;
            if (!bounds.Contains(pos))
                continue;

            var label = GetHolotagLabel(idCard, presetId);
            var kind = GetHolotagKind(idCard, presetId, meta);
            blips.Add(new WastelandMapTrackedBlip(pos.X, pos.Y, label, kind));
        }

        return blips.ToArray();
    }

    private static string GetHolotagLabel(IdCardComponent idCard, PresetIdCardComponent presetId)
    {
        var fullName = idCard.FullName?.Trim();
        var rank = idCard.LocalizedJobTitle?.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
            return "Unknown Holotag";

        if (string.IsNullOrWhiteSpace(rank))
            rank = presetId.JobName?.ToString()?.Trim();

        if (string.IsNullOrWhiteSpace(rank))
            return fullName;

        return $"{fullName} ({rank})";
    }

    private static WastelandMapTrackedBlipKind GetHolotagKind(IdCardComponent idCard, PresetIdCardComponent presetId, MetaDataComponent meta)
    {
        var rank = idCard.LocalizedJobTitle?.Trim();
        if (string.IsNullOrWhiteSpace(rank))
            rank = presetId.JobName?.ToString()?.Trim();

        var protoId = meta.EntityPrototype?.ID ?? string.Empty;
        var source = string.IsNullOrWhiteSpace(rank) ? protoId : rank;

        if (source.Contains("elder", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("commander", StringComparison.OrdinalIgnoreCase))
        {
            return WastelandMapTrackedBlipKind.Elder;
        }

        if (source.Contains("paladin", StringComparison.OrdinalIgnoreCase))
            return WastelandMapTrackedBlipKind.Paladin;

        if (source.Contains("knight", StringComparison.OrdinalIgnoreCase))
            return WastelandMapTrackedBlipKind.Knight;

        if (source.Contains("scribe", StringComparison.OrdinalIgnoreCase))
            return WastelandMapTrackedBlipKind.Scribe;

        if (source.Contains("squire", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("initiate", StringComparison.OrdinalIgnoreCase))
        {
            return WastelandMapTrackedBlipKind.Squire;
        }

        return WastelandMapTrackedBlipKind.Unknown;
    }
}
