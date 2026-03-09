// #Misfits Change - Client-side EUI for the Whitelist Search admin panel
using Content.Client.Eui;
using Content.Shared._Misfits.Administration;
using Content.Shared.Eui;
using Robust.Shared.Log;

namespace Content.Client._Misfits.Administration.UI;

public sealed class WhitelistSearchEui : BaseEui
{
    private readonly ISawmill _sawmill;
    private WhitelistSearchWindow _window;

    public WhitelistSearchEui()
    {
        _sawmill = Logger.GetSawmill("admin.whitelist_search_eui");
        _window = new WhitelistSearchWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnSearch += query => SendMessage(new SearchPlayersMessage(query));
        _window.OnSelectPlayer += playerId => SendMessage(new SelectPlayerMessage(playerId));
        _window.OnSetJob += (job, whitelisting) => SendMessage(new SetWhitelistSearchJobMessage(job, whitelisting));
        _window.OnAddRoleTime += (job, timeString) => SendMessage(new AddWhitelistSearchRoleTimeMessage(job, timeString));
        _window.OnSetRoleTime += (job, timeString) => SendMessage(new SetWhitelistSearchRoleTimeMessage(job, timeString));
        _window.OnAddDeptTime += (deptId, timeString) => SendMessage(new AddWhitelistSearchDeptTimeMessage(deptId, timeString));
        _window.OnSetDeptTime += (deptId, timeString) => SendMessage(new SetWhitelistSearchDeptTimeMessage(deptId, timeString));
        _window.OnAdjustJobSlots += (job, delta) => SendMessage(new AdjustWhitelistSearchJobSlotsMessage(job, delta));
    }

    public override void HandleState(EuiStateBase state)
    {
        _sawmill.Debug($"HandleState called with type: {state.GetType().Name}");

        if (state is not WhitelistSearchEuiState cast)
        {
            _sawmill.Warning($"State is NOT WhitelistSearchEuiState, actual type: {state.GetType().FullName}");
            return;
        }

        _sawmill.Debug($"State received: {cast.SearchResults.Count} results, selected={cast.SelectedPlayerName}");
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
