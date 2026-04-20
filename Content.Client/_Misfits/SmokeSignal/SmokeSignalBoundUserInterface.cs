// #Misfits Add — BUI glue for the Smoke Signal text input window.
// Opens the window on BUI open, sends the typed message to the server, and closes cleanly.

using Content.Shared._Misfits.SmokeSignal;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.SmokeSignal;

/// <summary>
/// Bound user interface that bridges the Smoke Signal text input to the server system.
/// </summary>
[UsedImplicitly]
public sealed class SmokeSignalBoundUserInterface : BoundUserInterface
{
    private SmokeSignalWindow? _window;

    public SmokeSignalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SmokeSignalWindow>();

        // Wire the send callback: forward message to server, then close
        _window.OnSend += message =>
        {
            SendMessage(new SmokeSignalSendMessage(message));
        };
    }
}
