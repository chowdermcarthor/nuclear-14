// #Misfits Change - Ported from Delta-V chronic pain system
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Misfits.ChronicPain.Overlays;

/// <summary>
///     Visual overlay for chronic pain — renders a subtle drunk/wavering effect on screen
///     when the player's chronic pain is active and unsuppressed.
/// </summary>
public sealed partial class ChronicPainOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _painShader;
    private static readonly ProtoId<ShaderPrototype> ShaderProto = "Drunk";

    public ChronicPainOverlay()
    {
        IoCManager.InjectDependencies(this);
        _painShader = _prototype.Index(ShaderProto).InstanceUnique();
        _painShader.SetParameter("boozePower", 0.3f); // Subtle pain distortion — less intense than actual drunkenness
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { Valid: true })
            return false;

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null)
            return;

        _painShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_painShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}
