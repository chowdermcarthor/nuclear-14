// #Misfits Add — BUI glue for the Spirit Board ghost response window.
// Opened by the server when a ghost communes with an active spirit board.

using Content.Shared._Misfits.SpiritBoard;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.SpiritBoard;

/// <summary>
/// Bound user interface for the ghost response selection window.
/// Sends <see cref="SpiritBoardSelectResponseMessage"/> to the server on selection.
/// </summary>
[UsedImplicitly]
public sealed class SpiritBoardBoundUserInterface : BoundUserInterface
{
    private SpiritBoardResponseWindow? _window;

    public SpiritBoardBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SpiritBoardResponseWindow>();

        // Each response fires a BUI message to the server
        _window.OnResponse += response =>
        {
            SendMessage(new SpiritBoardSelectResponseMessage(response));
        };
    }
}
