// #Misfits Change - Custom map viewer control with zoom, pan, and player position marker
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.WastelandMap;

/// <summary>
/// A control that displays a texture with mouse-wheel zoom and click-drag pan,
/// and overlays a dot at the local player's current position on the map.
/// </summary>
public sealed class MapViewerControl : Control
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public MapViewerControl()
    {
        IoCManager.InjectDependencies(this);
        // Must be Stop to receive mouse wheel, click, and move events.
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
    }

    private Texture? _texture;
    private Box2 _worldBounds;

    // Zoom: 1.0 = fit to window. Values > 1 are zoomed in.
    private float _zoom = 1f;

    // Pan offset in UI pixels from the centered position.
    private Vector2 _pan = Vector2.Zero;

    private bool _dragging;
    private Vector2 _dragStart;
    private Vector2 _panAtDragStart;

    private const float ZoomStep = 1.15f;
    private const float ZoomMin = 0.5f;
    private const float ZoomMax = 16f;

    public void SetTexture(Texture? texture, Box2 worldBounds)
    {
        _texture = texture;
        _worldBounds = worldBounds;
        _zoom = 1f;
        _pan = Vector2.Zero;
    }

    // Compute fit-to-window scale (1.0 in _zoom space = fills the control).
    private float FitScale
    {
        get
        {
            if (_texture == null || Size.X <= 0 || Size.Y <= 0)
                return 1f;
            return Math.Min(Size.X / _texture.Width, Size.Y / _texture.Height);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_texture == null)
            return;

        var scale = FitScale * _zoom;
        var drawW = _texture.Width * scale;
        var drawH = _texture.Height * scale;

        // Center in control, then apply pan offset.
        var x = (Size.X - drawW) / 2f + _pan.X;
        var y = (Size.Y - drawH) / 2f + _pan.Y;

        handle.DrawTextureRect(_texture, UIBox2.FromDimensions(x, y, drawW, drawH));

        if (TryGetPlayerUv(out var playerUv))
        {
            var markerX = x + playerUv.X * drawW;
            var markerY = y + playerUv.Y * drawH;
            DrawMarker(handle, new Vector2(markerX, markerY));
        }
    }

    private bool TryGetPlayerUv(out Vector2 uv)
    {
        uv = default;

        if (_worldBounds.Width <= 0 || _worldBounds.Height <= 0)
            return false;

        var localEntity = _playerManager.LocalEntity;
        if (!localEntity.HasValue ||
            !_entityManager.TryGetComponent<TransformComponent>(localEntity.Value, out var xform))
        {
            return false;
        }

        var xformSystem = _entityManager.System<SharedTransformSystem>();

        // The renderer/image bounds are derived from grid-local tile coordinates,
        // but depending on the player's parent chain the live entity position may
        // be exposed in either world-space or map-space. Try both and use the one
        // that actually lands inside the rendered bounds.
        var worldPos = xformSystem.GetWorldPosition(xform);
        if (TryGetUv(worldPos, out uv))
            return true;

        var mapPos = xformSystem.GetMapCoordinates(xform).Position;
        return TryGetUv(mapPos, out uv);
    }

    private bool TryGetUv(Vector2 position, out Vector2 uv)
    {
        var u = (position.X - _worldBounds.Left) / _worldBounds.Width;
        var v = 1f - (position.Y - _worldBounds.Bottom) / _worldBounds.Height;
        uv = new Vector2(u, v);
        return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
    }

    private void DrawMarker(DrawingHandleScreen handle, Vector2 markerPos)
    {
        var pulse = (float) (0.5 + 0.5 * Math.Sin(_gameTiming.RealTime.TotalSeconds * 4.0));
        var outerRadius = 24f + 10f * pulse;

        handle.DrawCircle(markerPos, outerRadius, new Color(1f, 0.2f, 0.2f, 0.35f * pulse));
        handle.DrawCircle(markerPos, 18f, new Color(0f, 0f, 0f, 0.55f));
        handle.DrawCircle(markerPos, 14f, new Color(1f, 0.15f, 0.15f, 1f));
        handle.DrawCircle(markerPos, 6f, Color.White);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        if (_texture == null)
            return;

        var oldZoom = _zoom;
        if (args.Delta.Y > 0)
            _zoom = Math.Min(_zoom * ZoomStep, ZoomMax);
        else if (args.Delta.Y < 0)
            _zoom = Math.Max(_zoom / ZoomStep, ZoomMin);

        // Zoom toward mouse cursor position.
        var mouseInControl = args.RelativePosition;
        var centerOffset = mouseInControl - Size / 2f;
        _pan = ((_pan - centerOffset) * (_zoom / oldZoom)) + centerOffset;

        ClampPan();
        args.Handle();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _dragging = true;
            _dragStart = args.RelativePosition;
            _panAtDragStart = _pan;
            args.Handle();
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            _dragging = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_dragging)
            return;

        _pan = _panAtDragStart + (args.RelativePosition - _dragStart);
        ClampPan();
    }

    private void ClampPan()
    {
        if (_texture == null)
            return;

        var scale = FitScale * _zoom;
        var drawW = _texture.Width * scale;
        var drawH = _texture.Height * scale;

        // Allow panning up to half the image beyond the control edge.
        var maxPanX = drawW / 2f;
        var maxPanY = drawH / 2f;
        _pan.X = Math.Clamp(_pan.X, -maxPanX, maxPanX);
        _pan.Y = Math.Clamp(_pan.Y, -maxPanY, maxPanY);
    }
}
