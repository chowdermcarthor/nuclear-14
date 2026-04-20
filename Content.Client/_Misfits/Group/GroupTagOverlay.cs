// #Misfits Add - Screen-space overlay that draws teal [ALLY] tags above group members.
// Mirrors the AllyTagOverlay (FactionWar) pattern but uses GroupClientSystem's participant dict.
// Teal color distinguishes group allies from faction war allies (lime green).

using System.Numerics;
using Content.Shared.Examine;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.Group;

/// <summary>
/// Draws teal <c>[ALLY]</c> tags above group members when the overlay is active.
/// Active only while the server reports group members in the participant dict.
/// </summary>
internal sealed class GroupTagOverlay : Overlay
{
    private readonly GroupClientSystem    _groupSystem;
    private readonly IEntityManager      _entityManager;
    private readonly IPlayerManager      _playerManager;
    private readonly IEyeManager         _eyeManager;
    private readonly IGameTiming         _timing;
    private readonly EntityLookupSystem  _entityLookup;
    private readonly ExamineSystemShared _examine;
    private readonly SharedTransformSystem _transform;
    private readonly Font                _font;

    // Visibility cache to avoid per-frame LOS traces.
    private readonly Dictionary<NetEntity, VisibilityCacheEntry> _visibilityCache = new();
    private TimeSpan _nextCleanup;

    private static readonly TimeSpan VisibilityCacheLifetime  = TimeSpan.FromSeconds(0.15);
    private static readonly TimeSpan CacheCleanupInterval     = TimeSpan.FromSeconds(2);
    private const float MaxTagDistance        = 50f;
    private const float MaxTagDistanceSq      = MaxTagDistance * MaxTagDistance;
    private const float PosRefreshThresholdSq = 1f;
    private const int   MaxLosRefreshPerFrame = 12;

    // Teal group ally color — distinguishable from war system's lime green.
    private static readonly Color GroupAllyColor = new(0.1f, 0.85f, 0.85f);

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public GroupTagOverlay(
        GroupClientSystem    groupSystem,
        IEntityManager       entityManager,
        IPlayerManager       playerManager,
        IEyeManager          eyeManager,
        IGameTiming          timing,
        IResourceCache       resourceCache,
        EntityLookupSystem   entityLookup,
        ExamineSystemShared  examine,
        SharedTransformSystem transform)
    {
        _groupSystem   = groupSystem;
        _entityManager = entityManager;
        _playerManager = playerManager;
        _eyeManager    = eyeManager;
        _timing        = timing;
        _entityLookup  = entityLookup;
        _examine       = examine;
        _transform     = transform;

        // Render just below the war overlay (AllyTagOverlay ZIndex = 195).
        ZIndex = 194;
        _font  = new VectorFont(
            resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
    }

    private sealed class VisibilityCacheEntry
    {
        public bool     Visible;
        public MapId    MapId;
        public Vector2  Position;
        public TimeSpan NextRefresh;
    }

    private bool NeedsRefresh(VisibilityCacheEntry entry, MapCoordinates coords, TimeSpan now)
    {
        if (now >= entry.NextRefresh)
            return true;
        if (entry.MapId != coords.MapId)
            return true;
        return (coords.Position - entry.Position).LengthSquared() >= PosRefreshThresholdSq;
    }

    private void CleanupCache(IReadOnlyDictionary<NetEntity, string> participants, TimeSpan now)
    {
        if (now < _nextCleanup)
            return;
        _nextCleanup = now + CacheCleanupInterval;

        var toRemove = new List<NetEntity>();
        foreach (var key in _visibilityCache.Keys)
        {
            if (!participants.ContainsKey(key))
                toRemove.Add(key);
        }
        foreach (var k in toRemove)
            _visibilityCache.Remove(k);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localEntity = _playerManager.LocalSession?.AttachedEntity;
        if (localEntity == null)
            return;

        var participants = _groupSystem.GroupParticipants;
        if (participants.Count == 0)
            return;

        var localPos   = _transform.GetMapCoordinates(localEntity.Value);
        var now        = _timing.CurTime;
        var viewport   = args.WorldAABB;
        var losRefresh = MaxLosRefreshPerFrame;

        CleanupCache(participants, now);

        foreach (var (netEntity, _) in participants)
        {
            var uid = _entityManager.GetEntity(netEntity);

            if (uid == localEntity.Value || !_entityManager.EntityExists(uid))
                continue;

            if (!_entityManager.HasComponent<SpriteComponent>(uid))
                continue;

            var otherPos = _transform.GetMapCoordinates(uid);
            if (otherPos.MapId != localPos.MapId)
                continue;

            if ((otherPos.Position - localPos.Position).LengthSquared() > MaxTagDistanceSq)
                continue;

            var aabb = _entityLookup.GetWorldAABB(uid);
            if (!aabb.Intersects(viewport))
                continue;

            // LOS cache check.
            _visibilityCache.TryGetValue(netEntity, out var cached);

            if (cached == null || NeedsRefresh(cached, otherPos, now))
            {
                if (losRefresh > 0)
                {
                    losRefresh--;
                    var visible = _examine.InRangeUnOccluded(localPos, otherPos, MaxTagDistance,
                        e => e == localEntity.Value || e == uid);

                    cached = new VisibilityCacheEntry
                    {
                        Visible     = visible,
                        MapId       = otherPos.MapId,
                        Position    = otherPos.Position,
                        NextRefresh = now + VisibilityCacheLifetime,
                    };
                    _visibilityCache[netEntity] = cached;
                }
                else if (cached == null)
                {
                    continue;
                }
            }

            if (!cached.Visible)
                continue;

            // Draw [ALLY] above the entity.
            var screenCoords = _eyeManager.WorldToScreen(
                aabb.Center + new Angle(-_eyeManager.CurrentEye.Rotation)
                    .RotateVec(aabb.TopRight - aabb.Center)) + new Vector2(1f, 7f);

            args.ScreenHandle.DrawString(_font, screenCoords, "[ALLY]", GroupAllyColor);
        }
    }
}
