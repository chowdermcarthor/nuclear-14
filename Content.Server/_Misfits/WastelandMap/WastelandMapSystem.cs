// #Misfits Change - Wasteland Map server system
using System;
using System.Collections.Generic;
using Content.Server.Access.Components;
using Content.Server._Misfits.Group; // #Misfits Add - group blip injection
using Content.Shared.Access.Components;
using Content.Shared.Tag;
using Content.Shared._Misfits.WastelandMap;
using Content.Shared._Misfits.TribalHunt;
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
    [Dependency] private readonly GroupSystem _groupSystem = default!; // #Misfits Add - group member map blips

    private const int MaxSharedAnnotations = 128;
    private const int MaxStrokePoints = 512; // 256 UV points × 2 floats each
    // #Misfits Fix: Slowed from 0.5 s — the map is informational, not real-time.
    // GetIdCardBlips does a global PresetIdCard world-scan every update; 2.5 s is imperceptible to players.
    private const float UpdateInterval = 2.5f;
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
            // #Misfits Fix: Skip the expensive BUI rebuild when nobody has this map open.
            // GetActors() is O(1) with the early-out; the rebuild + GetIdCardBlips world-scan is O(all id cards).
            var viewerMap = xform.MapID;
            EntityUid? firstActor = null;
            foreach (var actor in _uiSystem.GetActors((uid, ui), WastelandMapUiKey.Key))
            {
                viewerMap = Transform(actor).MapID;
                firstActor = actor; // #Misfits Add - pass actor so group blips are relative to who holds the map
                break;
            }
            if (firstActor == null)
                continue;

            _uiSystem.SetUiState((uid, ui), WastelandMapUiKey.Key, BuildState(map, viewerMap, actor: firstActor));
        }
    }

    private void OnAfterOpen(EntityUid uid, WastelandMapComponent comp, AfterActivatableUIOpenEvent args)
    {
        var userMap = Transform(args.User).MapID;
        // #Misfits Add - pass the user so group member blips are seeded correctly on open
        _uiSystem.SetUiState(uid, WastelandMapUiKey.Key, BuildState(comp, userMap, actor: args.User));
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

    // #Misfits Add - optional actor param so group-member blips can be injected per-viewer
    public WastelandMapBoundUserInterfaceState BuildState(WastelandMapComponent comp, MapId mapId, WastelandMapTacticalFeedKind? feedOverride = null, EntityUid? actor = null)
    {
        var feed = feedOverride ?? GetEffectiveFeed(comp);
        var trackedBlips = GetTrackedBlips(feed, mapId, comp.WorldBounds, actor);
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

    // #Misfits Add - actor param enables group-member blip injection
    private WastelandMapTrackedBlip[] GetTrackedBlips(WastelandMapTacticalFeedKind feed, MapId mapId, Box2 bounds, EntityUid? actor = null)
    {
        var blips = new List<WastelandMapTrackedBlip>();

        var factionBlips = feed switch
        {
            WastelandMapTacticalFeedKind.Brotherhood => GetIdCardBlips(mapId, bounds, "IdCardBrotherhood"),
            WastelandMapTacticalFeedKind.Vault => GetIdCardBlips(mapId, bounds, "IdCardVault"),
            WastelandMapTacticalFeedKind.NCR => GetIdCardBlips(mapId, bounds, "IdCardNCR"),
            WastelandMapTacticalFeedKind.Enclave => GetIdCardBlips(mapId, bounds, "IdCardEnclave"), // #Misfits Change
            WastelandMapTacticalFeedKind.Legion => GetIdCardBlips(mapId, bounds, "IdCardLegion"), // #Misfits Add - Legion tactical feed
            _ => [],
        };

        blips.AddRange(factionBlips);
        blips.AddRange(GetTribalHuntTargetBlips(mapId, bounds));

        // #Misfits Add - inject group member blips if the map carrier is in a group
        if (actor.HasValue)
            blips.AddRange(GetGroupMemberBlips(actor.Value, mapId, bounds));

        return blips.ToArray();
    }

    /// <summary>Returns a blip for each group member on the same map as the actor, excluding the actor themselves.</summary>
    private WastelandMapTrackedBlip[] GetGroupMemberBlips(EntityUid actor, MapId mapId, Box2 bounds)
    {
        var members = _groupSystem.GetGroupMemberEntities(actor);
        if (members == null || members.Count == 0)
            return [];

        var blips = new List<WastelandMapTrackedBlip>(members.Count);
        foreach (var member in members)
        {
            if (member == actor)
                continue; // don't show the holder as a blip

            var mapCoords = _transform.GetMapCoordinates(member);
            if (mapCoords.MapId != mapId)
                continue;

            var pos = mapCoords.Position;
            if (!bounds.Contains(pos))
                continue;

            var label = Name(member);
            blips.Add(new WastelandMapTrackedBlip(pos.X, pos.Y, label, WastelandMapTrackedBlipKind.PipBoyGroupMember));
        }

        return blips.ToArray();
    }

    private WastelandMapTrackedBlip[] GetTribalHuntTargetBlips(MapId mapId, Box2 bounds)
    {
        var blips = new List<WastelandMapTrackedBlip>();
        var query = EntityQueryEnumerator<LegendaryCreatureComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var legendary, out var xform))
        {
            if (!legendary.RevealLocation)
                continue;

            var mapCoordinates = _transform.GetMapCoordinates(uid, xform);
            if (mapCoordinates.MapId != mapId)
                continue;

            var pos = mapCoordinates.Position;
            if (!bounds.Contains(pos))
                continue;

            var label = string.IsNullOrWhiteSpace(legendary.CreatureName)
                ? "Legendary Target"
                : $"Legendary {legendary.CreatureName}";

            blips.Add(new WastelandMapTrackedBlip(
                pos.X,
                pos.Y,
                label,
                WastelandMapTrackedBlipKind.TribalHuntTarget));
        }

        return blips.ToArray();
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

        // #Misfits Add - Legion rank detection for the Centurion tactical computer
        if (source.Contains("legate", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("centurion", StringComparison.OrdinalIgnoreCase))
        {
            return WastelandMapTrackedBlipKind.LegionCenturion;
        }

        if (source.Contains("decanus", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("dean", StringComparison.OrdinalIgnoreCase)) // CaesarLegionDean = Decanus in-game
        {
            return WastelandMapTrackedBlipKind.LegionDecanus;
        }

        if (source.Contains("legionnaire", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("vexillarius", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("houndmaster", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("frumentarii", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("orator", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("explorer", StringComparison.OrdinalIgnoreCase))
        {
            return WastelandMapTrackedBlipKind.LegionWarrior;
        }

        if (source.Contains("auxilia", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("recruit", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("slave", StringComparison.OrdinalIgnoreCase))
        {
            return WastelandMapTrackedBlipKind.LegionRecruit;
        }
        // End Misfits Add

        return WastelandMapTrackedBlipKind.Unknown;
    }
}
