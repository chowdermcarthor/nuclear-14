using Content.Shared._Misfits.Warcry;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._Misfits.Warcry;

/// <summary>
/// Draws persistent world-space circles around active warcry sources.
/// </summary>
public sealed class WarcryOverlay(IEntityManager entityManager) : Overlay
{
    private const float FillAlpha = 0.05f;
    private const float OutlineAlpha = 0.85f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;
        var xformSystem = entityManager.System<SharedTransformSystem>();

        foreach (var (active, xform) in entityManager.EntityQuery<ActiveWarcryComponent, TransformComponent>(true))
        {
            if (xform.MapID != args.MapId)
            {
                continue;
            }

            var position = xformSystem.GetWorldPosition(xform);
            if (!args.WorldAABB.Contains(position))
            {
                continue;
            }

            worldHandle.DrawCircle(position, active.Radius, active.Color.WithAlpha(FillAlpha));
            worldHandle.DrawCircle(position, active.Radius, active.Color.WithAlpha(OutlineAlpha), filled: false);
        }
    }
}