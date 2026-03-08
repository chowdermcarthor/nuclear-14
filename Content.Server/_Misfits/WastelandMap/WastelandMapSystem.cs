// #Misfits Change - Wasteland Map server system
using Content.Shared._Misfits.WastelandMap;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Misfits.WastelandMap;

/// <summary>
/// Sends the WastelandMap state (including world bounds) to the client BUI
/// when the UI is opened. Box2 is not NetSerializable, so we unpack it into
/// 4 floats inside the BUI state.
/// </summary>
public sealed class WastelandMapSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WastelandMapComponent, AfterActivatableUIOpenEvent>(OnAfterOpen);
    }

    private void OnAfterOpen(EntityUid uid, WastelandMapComponent comp, AfterActivatableUIOpenEvent args)
    {
        var state = new WastelandMapBoundUserInterfaceState(
            comp.MapTitle,
            comp.MapTexturePath.ToString(),
            comp.WorldBounds.Left,
            comp.WorldBounds.Bottom,
            comp.WorldBounds.Right,
            comp.WorldBounds.Top);

        _uiSystem.SetUiState(uid, WastelandMapUiKey.Key, state);
    }
}
