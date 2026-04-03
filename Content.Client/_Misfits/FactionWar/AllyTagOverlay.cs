// #Misfits Add - Screen-space overlay that draws [ALLY] and [ENEMY] tags above in-world entities
// when the local player's faction is engaged in an active war.
// Uses a server-provided participant dictionary (NetEntity → faction side) because
// NpcFactionMemberComponent.Factions is NOT synced to clients.
// Pattern mirrors AdminNameOverlay (Content.Client/Administration/AdminNameOverlay.cs).

using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client._Misfits.FactionWar;

/// <summary>
/// Draws green <c>[ALLY]</c> or red <c>[ENEMY]</c> tags above entities whose faction is relevant
/// to the current war state. Active only while the local player is involved in at least one war.
/// All ally/enemy classification comes from the server-broadcast participant dict.
/// </summary>
internal sealed class AllyTagOverlay : Overlay
{
    private readonly FactionWarClientSystem _warSystem;
    private readonly IEntityManager         _entityManager;
    private readonly IPlayerManager         _playerManager;
    private readonly IEyeManager            _eyeManager;
    private readonly EntityLookupSystem     _entityLookup;
    private readonly Font                   _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public AllyTagOverlay(
        FactionWarClientSystem warSystem,
        IEntityManager         entityManager,
        IPlayerManager         playerManager,
        IEyeManager            eyeManager,
        IResourceCache         resourceCache,
        EntityLookupSystem     entityLookup)
    {
        _warSystem     = warSystem;
        _entityManager = entityManager;
        _playerManager = playerManager;
        _eyeManager    = eyeManager;
        _entityLookup  = entityLookup;

        ZIndex = 195; // just below AdminNameOverlay (200) so admin tags render on top
        _font = new VectorFont(
            resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localEntity = _playerManager.LocalSession?.AttachedEntity;
        if (localEntity == null)
            return;

        var activeWars = _warSystem.ActiveWars;
        if (activeWars.Count == 0)
            return;

        var participants = _warSystem.WarParticipants;
        if (participants.Count == 0)
            return;

        // Determine the local player's side from the participant dict itself.
        // This stays accurate across respawns/faction-swaps since the server rebuilds it every 2s.
        var localNet = _entityManager.GetNetEntity(localEntity.Value);
        if (!participants.TryGetValue(localNet, out var effectiveFaction))
        {
            // Fallback: /warjoin side if the local entity isn't in the dict yet.
            effectiveFaction = _warSystem.LocalWarJoinSide;
        }

        if (effectiveFaction == null)
            return;

        var viewport = args.WorldAABB;

        // Iterate the server-provided dict of war-relevant entities and their side.
        foreach (var (netEntity, side) in participants)
        {
            var uid = _entityManager.GetEntity(netEntity);

            // Skip self, non-existent, and entities without sprites.
            if (uid == localEntity.Value || !_entityManager.EntityExists(uid))
                continue;

            if (!_entityManager.HasComponent<SpriteComponent>(uid))
                continue;

            if (_entityManager.GetComponent<TransformComponent>(uid).MapID != _eyeManager.CurrentMap)
                continue;

            var aabb = _entityLookup.GetWorldAABB(uid);
            if (!aabb.Intersects(viewport))
                continue;

            // Determine ally or enemy relative to the local player's side.
            string tag;
            Color tagColor;

            if (side == effectiveFaction)
            {
                tag      = "[ALLY]";
                tagColor = Color.LimeGreen;
            }
            else
            {
                tag      = "[ENEMY]";
                tagColor = new Color(1f, 0.3f, 0.3f);
            }

            // Position the tag at the top-right of the entity's AABB, same as AdminNameOverlay.
            var screenCoords = _eyeManager.WorldToScreen(
                aabb.Center + new Angle(-_eyeManager.CurrentEye.Rotation)
                    .RotateVec(aabb.TopRight - aabb.Center)) + new Vector2(1f, 7f);

            args.ScreenHandle.DrawString(_font, screenCoords, tag, tagColor);
        }
    }
}
