// #Misfits Change - Client-side EUI for the Whitelist Viewer admin panel
using Content.Client.Eui;
using Content.Shared._Misfits.Administration;
using Content.Shared.Eui;

namespace Content.Client._Misfits.Administration.UI;

public sealed class WhitelistViewEui : BaseEui
{
    private WhitelistViewWindow _window;

    public WhitelistViewEui()
    {
        _window = new WhitelistViewWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnRefresh += () => SendMessage(new RequestWhitelistViewMessage());
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not WhitelistViewEuiState cast)
            return;

        _window.HandleState(cast);
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
        _window.Dispose();
    }
}
