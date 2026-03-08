// #Misfits Change - Wasteland Map Viewer BUI
using Content.Shared._Misfits.WastelandMap;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.WastelandMap;

[UsedImplicitly]
public sealed class WastelandMapBoundUserInterface : BoundUserInterface
{
    private WastelandMapWindow? _window;

    public WastelandMapBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<WastelandMapWindow>();

        if (EntMan.TryGetComponent<WastelandMapComponent>(Owner, out var comp))
        {
            _window.SetMap(comp.MapTitle, comp.MapTexturePath);
        }
    }
}
