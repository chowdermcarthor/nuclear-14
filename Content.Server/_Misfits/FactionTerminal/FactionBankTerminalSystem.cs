// #Misfits Add - Server system for faction ATM / bank terminals.
//
// PERSISTENT PLACEMENT — admin places once, respawns every round automatically.
//
// Admin workflow:
//   1. Open Entity Spawn Panel → type ID → click to place on floor
//   2. Position saved to database immediately
//   3. Every round start re-spawns all recorded terminals at their saved tile
//   4. To remove: admin deletes the entity in-game → record purged from database
//
// Prototype IDs:
//   MisfitsATMNCR | MisfitsATMLegion | MisfitsATMBoS | MisfitsATMVault | MisfitsATMTown | MisfitsATMWasteland
using System.IO;
using System.Text.Json;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Database;
using Content.Shared._Misfits.Currency;
using Content.Shared._Misfits.Currency.Components;
using Content.Shared._Misfits.FactionTerminal.Components;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._Misfits.FactionTerminal;

public sealed class FactionBankTerminalSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ActorSystem _actor = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;

    private ISawmill _log = default!;

    // All recorded placements keyed by "MapName:TileX:TileY".
    private readonly Dictionary<string, AtmPlacement> _placements = new();

    // Prevents re-recording terminals that we just spawned during round init.
    private bool _isSelfSpawning;

    // Set during RoundRestartCleanupEvent so ComponentShutdown doesn't wipe records.
    private bool _isRoundRestarting;

    // Per-round dedup: prevents double-spawning if multiple grids fire for the same map.
    private readonly HashSet<string> _spawnedThisRound = new();

    public override void Initialize()
    {
        base.Initialize();

        _log = Logger.GetSawmill("faction_atm");

        // Interaction
        SubscribeLocalEvent<FactionBankTerminalComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FactionBankTerminalComponent, ActivateInWorldEvent>(OnActivate);

        // Record placement when a terminal is added to the world.
        SubscribeLocalEvent<FactionBankTerminalComponent, ComponentStartup>(OnTerminalStartup);
        // Remove placement record when a terminal is explicitly deleted by an admin.
        SubscribeLocalEvent<FactionBankTerminalComponent, ComponentShutdown>(OnTerminalShutdown);

        // Re-spawn saved terminals when the map grid initialises each round.
        SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnGridMapInit);

        // Flag round restart so shutdown events don't purge database records.
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        LoadPlacementsAsync();
        MigrateJsonToDatabase();
    }

    // ── Placement recording ───────────────────────────────────────────────────

    private void OnTerminalStartup(Entity<FactionBankTerminalComponent> ent, ref ComponentStartup args)
    {
        // Skip terminals that we ourselves just spawned from JSON (round init path).
        if (_isSelfSpawning)
            return;

        var coords = Transform(ent).Coordinates;
        var gridUid = coords.GetGridUid(EntityManager);
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return;

        var tile = _maps.GetTileRef(gridUid.Value, grid, coords);
        var ix = tile.GridIndices.X;
        var iy = tile.GridIndices.Y;

        var mapUid = Transform(gridUid.Value).MapUid;
        var mapName = mapUid != null && TryComp<MetaDataComponent>(mapUid.Value, out var meta)
            ? meta.EntityName
            : "Unknown";

        var key = BuildKey(mapName, ix, iy);

        // Store key on component so shutdown can look it up without hitting the transform.
        ent.Comp.PlacementKey = key;
        Dirty(ent);

        if (_placements.ContainsKey(key))
            return; // already saved (e.g. same tile re-used)

        var protoId = MetaData(ent).EntityPrototype?.ID ?? string.Empty;
        if (string.IsNullOrEmpty(protoId))
            return;

        _placements[key] = new AtmPlacement
        {
            PlacementKey = key,
            PrototypeId = protoId,
            MapName = mapName,
            TileX = ix,
            TileY = iy,
        };

        _db.UpsertAtmPlacementAsync(_placements[key]);
        _log.Debug($"Recorded ATM '{protoId}' at {key}");
    }

    private void OnTerminalShutdown(Entity<FactionBankTerminalComponent> ent, ref ComponentShutdown args)
    {
        // Round cleanup deletes every entity — keep the JSON intact.
        if (_isRoundRestarting)
            return;

        var key = ent.Comp.PlacementKey;
        if (string.IsNullOrEmpty(key))
            return;

        if (_placements.Remove(key))
        {
            _db.RemoveAtmPlacementAsync(key);
            _log.Debug($"Removed ATM placement at {key}");
        }
    }

    // ── Round-start re-spawn ──────────────────────────────────────────────────

    private void OnGridMapInit(Entity<MapGridComponent> grid, ref MapInitEvent args)
    {
        // A new round is beginning — reset flags.
        _isRoundRestarting = false;

        var mapUid = Transform(grid).MapUid;
        if (mapUid == null || !TryComp<MetaDataComponent>(mapUid.Value, out var meta))
            return;

        var mapName = meta.EntityName;

        _isSelfSpawning = true;
        try
        {
            foreach (var record in _placements.Values)
            {
                if (record.MapName != mapName)
                    continue;

                var key = BuildKey(mapName, record.TileX, record.TileY);

                // Dedup: only spawn each placement once even if multiple grids init.
                if (!_spawnedThisRound.Add(key))
                    continue;

                var tileLocal = _maps.GridTileToLocal(grid, grid.Comp,
                    new Vector2i(record.TileX, record.TileY));

                Spawn(record.PrototypeId, tileLocal);
                _log.Debug($"Respawned ATM '{record.PrototypeId}' at {key}");
            }
        }
        finally
        {
            _isSelfSpawning = false;
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _isRoundRestarting = true;
        _spawnedThisRound.Clear();
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    private void OnGetVerbs(Entity<FactionBankTerminalComponent> terminal,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("faction-terminal-verb-access"),
            Priority = 10,
            Act = () => OpenWalletForPlayer(user, terminal),
        });
    }

    private void OnActivate(Entity<FactionBankTerminalComponent> terminal,
        ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        OpenWalletForPlayer(args.User, terminal);
        args.Handled = true;
    }

    // ── Wallet open helper ────────────────────────────────────────────────────

    private void OpenWalletForPlayer(EntityUid user, Entity<FactionBankTerminalComponent> terminal)
    {
        // Require an active player session — needed for all private messages.
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        var session = actor.PlayerSession;

        // The player must have a persistent currency component (loaded on spawn).
        if (!TryComp<PersistentCurrencyComponent>(user, out var wallet))
        {
            // Private feedback — the user has no bank account.
            var noAccountMsg = Loc.GetString("faction-terminal-no-account");
            _chatManager.ChatMessageToOne(ChatChannel.Local, noAccountMsg, noAccountMsg,
                EntityUid.Invalid, false, session.Channel);
            return;
        }

        var factionName = terminal.Comp.Faction switch
        {
            BankFaction.NCR               => Loc.GetString("faction-terminal-ncr"),
            BankFaction.Legion            => Loc.GetString("faction-terminal-legion"),
            BankFaction.BrotherhoodOfSteel => Loc.GetString("faction-terminal-bos"),
            BankFaction.VaultDwellers     => Loc.GetString("faction-terminal-vault"),
            BankFaction.Townsfolk         => Loc.GetString("faction-terminal-town"),
            BankFaction.Wasteland         => Loc.GetString("faction-terminal-wasteland"), // #Misfits Add
            _                             => "Unknown",
        };

        // Private greeting to the user only.
        var greetingMsg = Loc.GetString("faction-terminal-greeting", ("faction", factionName));
        _chatManager.ChatMessageToOne(ChatChannel.Local, greetingMsg, greetingMsg,
            EntityUid.Invalid, false, session.Channel);

        // Bystander emote — nearby players see the user interacting with the terminal.
        var terminalName = Name(terminal);
        _chat.TrySendInGameICMessage(user,
            Loc.GetString("misfits-chat-terminal-use", ("terminal", terminalName)),
            InGameICChatType.Emote, ChatTransmitRange.Normal, ignoreActionBlocker: true);

        // Reuse the existing wallet state message — opens the same wallet window the HUD button does.
        var msg = new CurrencyWalletStateMessage
        {
            Bottlecaps = wallet.Bottlecaps,
            OpenWindow = true,
        };

        RaiseNetworkEvent(msg, session.Channel);
    }

    // ── Database persistence ─────────────────────────────────────────────────────

    private static string BuildKey(string mapName, int x, int y) => $"{mapName}:{x}:{y}";

    private async void LoadPlacementsAsync()
    {
        try
        {
            var all = await _db.GetAllAtmPlacementsAsync();
            foreach (var placement in all)
                _placements[placement.PlacementKey] = placement;

            _log.Debug($"Loaded {_placements.Count} ATM placements from database.");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load ATM placements: {ex}");
        }
    }

    // ── One-time JSON → database migration ─────────────────────────────────────

    private async void MigrateJsonToDatabase()
    {
        var userDataPath = _resourceManager.UserData.RootDir ?? ".";
        var jsonPath = Path.Combine(userDataPath, "atm_placements.json");

        if (!File.Exists(jsonPath))
            return;

        try
        {
            var json = File.ReadAllText(jsonPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, LegacyAtmPlacementRecord>>(json);

            if (data != null && data.Count > 0)
            {
                _log.Info($"Migrating {data.Count} ATM placement records from JSON to database...");

                foreach (var (key, record) in data)
                {
                    var dbPlacement = new AtmPlacement
                    {
                        PlacementKey = key,
                        PrototypeId = record.PrototypeId,
                        MapName = record.MapName,
                        TileX = record.TileX,
                        TileY = record.TileY,
                    };

                    await _db.UpsertAtmPlacementAsync(dbPlacement);
                    _placements[key] = dbPlacement;
                }
            }

            File.Move(jsonPath, jsonPath + ".migrated");
            _log.Info("ATM placement JSON migration complete.");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to migrate atm_placements.json to database: {ex}");
        }
    }
}

// ── Legacy data model for one-time JSON migration ──────────────────────────────

internal sealed class LegacyAtmPlacementRecord
{
    public string PrototypeId { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public int TileX { get; set; }
    public int TileY { get; set; }
}
