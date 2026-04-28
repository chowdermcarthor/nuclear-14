// #Misfits Change - Server-side EUI for the Whitelist Viewer admin panel
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared._Misfits.Administration;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Log;

namespace Content.Server._Misfits.Administration;

public sealed class WhitelistViewEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IServerDbManager _db = default!;

    private readonly ISawmill _sawmill;
    private List<string> _whitelistedCkeys = new();

    public WhitelistViewEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _log.GetSawmill("admin.whitelist_view_eui");
    }

    public override EuiStateBase GetNewState()
    {
        return new WhitelistViewEuiState(_whitelistedCkeys);
    }

    public override async void Opened()
    {
        base.Opened();
        await LoadWhitelistedPlayers();
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to use whitelist viewer without permission");
            return;
        }

        if (msg is RequestWhitelistViewMessage)
            await LoadWhitelistedPlayers();
    }

    private async Task LoadWhitelistedPlayers()
    {
        try
        {
            var records = await _db.GetAllWhitelistedPlayersAsync();
            _whitelistedCkeys = records.Select(r => r.LastSeenUserName).ToList();
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error loading whitelisted players: {e}");
            _whitelistedCkeys = new List<string>();
        }

        StateDirty();
    }
}
