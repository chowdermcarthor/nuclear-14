// #Misfits Change - Persistent currency system (database-backed, Bottle Caps only)
using System.IO;
using System.Text.Json;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.Mind;
using Content.Shared._Misfits.Currency;
using Content.Shared._Misfits.Currency.Components;
using Content.Shared.Chat;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Currency.Systems;

/// <summary>
/// Handles consuming Bottle Cap items and adding them to a player's persistent balance
/// stored in the PostgreSQL / SQLite database.
/// </summary>
public sealed class PersistentCurrencySystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private ISawmill _log = default!;

    // The only persistent currency prototype — Bottle Caps.
    private const string BottlecapPrototype = "N14CurrencyCap";

    public override void Initialize()
    {
        base.Initialize();

        _log = Logger.GetSawmill("persistent_currency");

        SubscribeLocalEvent<ConsumableCurrencyComponent, UseInHandEvent>(OnUseCurrency);
        SubscribeLocalEvent<PersistentCurrencyComponent, ComponentStartup>(OnCurrencyStartup);
        SubscribeLocalEvent<PersistentCurrencyComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PersistentCurrencyComponent, ComponentShutdown>(OnCurrencyShutdown);
        SubscribeLocalEvent<PersistentCurrencyComponent, OpenCurrencyWalletEvent>(OnOpenWallet);
        SubscribeNetworkEvent<WithdrawCurrencyRequest>(OnWithdrawRequest);
        SubscribeNetworkEvent<OpenWalletHudMessage>(OnHudOpenWallet);
        SubscribeNetworkEvent<DepositHeldCurrencyRequest>(OnDepositHeldRequest);

        // One-time migration: import Bottle Cap balances from the old JSON file into the database.
        MigrateJsonToDatabase();
    }

    // ── Z-key deposit ─────────────────────────────────────────────────────────

    private void OnOpenWallet(Entity<PersistentCurrencyComponent> ent, ref OpenCurrencyWalletEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var uid = ent.Owner;
        var comp = ent.Comp;
        var session = actor.PlayerSession;

        // Find a Bottle Cap ConsumableCurrencyComponent item in any held hand
        EntityUid? heldItem = null;
        ConsumableCurrencyComponent? heldCurrency = null;
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (TryComp<ConsumableCurrencyComponent>(held, out var cc) && cc.CurrencyType == CurrencyType.Bottlecaps)
            {
                heldItem = held;
                heldCurrency = cc;
                break;
            }
        }

        if (heldItem == null || heldCurrency == null)
        {
            var nothingMsg = "You are not holding any Bottle Caps to deposit.";
            _chatManager.ChatMessageToOne(ChatChannel.Server, nothingMsg, nothingMsg, EntityUid.Invalid, false, session.Channel);
            return;
        }

        // Calculate amount (stack-aware)
        var amount = heldCurrency.ValuePerUnit;
        if (TryComp<StackComponent>(heldItem.Value, out var stackComp))
            amount *= stackComp.Count;

        comp.Bottlecaps += amount;
        var total = comp.Bottlecaps;

        Dirty(uid, comp);
        SaveCurrency(comp);

        QueueDel(heldItem.Value);

        var depositMsg = $"You have deposited {amount} Bottlecaps into your bank account. You now have {total} Bottlecaps.";
        _chatManager.ChatMessageToOne(ChatChannel.Server, depositMsg, depositMsg, EntityUid.Invalid, false, session.Channel);
    }

    // ── HUD wallet open ───────────────────────────────────────────────────────

    private void OnHudOpenWallet(OpenWalletHudMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (player.AttachedEntity is not { } uid)
            return;

        if (!TryComp<PersistentCurrencyComponent>(uid, out var comp))
            return;

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        RaiseNetworkEvent(new CurrencyWalletStateMessage
        {
            Bottlecaps = comp.Bottlecaps,
        }, actor.PlayerSession.Channel);
    }

    // ── Deposit In Hand button ────────────────────────────────────────────────

    private void OnDepositHeldRequest(DepositHeldCurrencyRequest msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (player.AttachedEntity is not { } uid)
            return;

        if (!TryComp<PersistentCurrencyComponent>(uid, out var comp))
            return;

        // Find a Bottle Cap ConsumableCurrencyComponent item in any held hand
        EntityUid? heldItem = null;
        ConsumableCurrencyComponent? heldCurrency = null;
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (TryComp<ConsumableCurrencyComponent>(held, out var cc) && cc.CurrencyType == CurrencyType.Bottlecaps)
            {
                heldItem = held;
                heldCurrency = cc;
                break;
            }
        }

        if (heldItem == null || heldCurrency == null)
        {
            _popup.PopupEntity("You're not holding any Bottle Caps!", uid, uid);
            return;
        }

        var amount = heldCurrency.ValuePerUnit;
        if (TryComp<StackComponent>(heldItem.Value, out var stack))
            amount *= stack.Count;

        comp.Bottlecaps += amount;
        var total = comp.Bottlecaps;

        _popup.PopupEntity($"Deposited {amount} bottlecaps. Total: {total}", uid, uid);

        Dirty(uid, comp);
        SaveCurrency(comp);
        QueueDel(heldItem.Value);

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        RaiseNetworkEvent(new CurrencyWalletStateMessage
        {
            Bottlecaps = comp.Bottlecaps,
        }, actor.PlayerSession.Channel);
    }

    // ── Withdraw ──────────────────────────────────────────────────────────────

    private void OnWithdrawRequest(WithdrawCurrencyRequest msg, EntitySessionEventArgs args)
    {
        // Only Bottle Caps can be withdrawn from persistent storage.
        if (msg.CurrencyType != CurrencyType.Bottlecaps)
            return;

        var player = args.SenderSession;
        if (player.AttachedEntity is not { } uid)
            return;

        if (!TryComp<PersistentCurrencyComponent>(uid, out var comp))
            return;

        if (msg.Amount <= 0)
            return;

        if (comp.Bottlecaps < msg.Amount)
        {
            _popup.PopupEntity("Not enough bottlecaps!", uid, uid);
            return;
        }

        comp.Bottlecaps -= msg.Amount;
        Dirty(uid, comp);
        SaveCurrency(comp);

        var spawned = Spawn(BottlecapPrototype, Transform(uid).Coordinates);

        if (TryComp<StackComponent>(spawned, out var stackComp) && msg.Amount > 1)
            _stack.SetCount(spawned, msg.Amount);

        _hands.TryPickupAnyHand(uid, spawned);
        _popup.PopupEntity($"Withdrew {msg.Amount} bottlecaps.", uid, uid);

        RaiseNetworkEvent(new CurrencyWalletStateMessage
        {
            Bottlecaps = comp.Bottlecaps,
        }, player.Channel);
    }

    // ── Use-in-hand deposit ───────────────────────────────────────────────────

    private void OnUseCurrency(Entity<ConsumableCurrencyComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        // Only Bottle Caps are depositable.
        if (ent.Comp.CurrencyType != CurrencyType.Bottlecaps)
            return;

        var user = args.User;
        var currencyComp = EnsureComp<PersistentCurrencyComponent>(user);

        int amount = ent.Comp.ValuePerUnit;
        if (TryComp<StackComponent>(ent, out var stack))
            amount *= stack.Count;

        currencyComp.Bottlecaps += amount;
        var total = currencyComp.Bottlecaps;

        _popup.PopupEntity($"Deposited {amount} bottlecaps. Total: {total}", user, user);

        Dirty(user, currencyComp);
        SaveCurrency(currencyComp);
        QueueDel(ent);

        if (TryComp<ActorComponent>(user, out var actor))
        {
            RaiseNetworkEvent(new CurrencyWalletStateMessage
            {
                Bottlecaps = currencyComp.Bottlecaps,
            }, actor.PlayerSession.Channel);
        }

        args.Handled = true;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnCurrencyStartup(Entity<PersistentCurrencyComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<ActorComponent>(ent, out var actor))
            LoadCurrency(ent, ent.Comp, actor.PlayerSession);
    }

    private void OnCurrencyShutdown(Entity<PersistentCurrencyComponent> ent, ref ComponentShutdown args)
    {
        // Nothing to clean up; wallet is accessed via the AlertsUI HUD button.
    }

    private void OnPlayerAttached(Entity<PersistentCurrencyComponent> ent, ref PlayerAttachedEvent args)
    {
        LoadCurrency(ent, ent.Comp, args.Player);
    }

    // ── Database load / save ──────────────────────────────────────────────────

    private async void LoadCurrency(EntityUid uid, PersistentCurrencyComponent comp, ICommonSession session)
    {
        if (comp.Loaded)
            return;

        if (!_mind.TryGetMind(uid, out _, out var mind))
            return;

        var characterName = mind.CharacterName;
        if (string.IsNullOrEmpty(characterName))
            return;

        var userId = session.UserId;
        comp.UserId = userId.ToString();
        comp.CharacterName = characterName;

        try
        {
            var bottlecaps = await _db.GetCharacterCurrencyAsync(userId.UserId, characterName);

            // Re-validate entity after async; it may have been deleted.
            if (!Exists(uid) || !TryComp<PersistentCurrencyComponent>(uid, out var freshComp))
                return;

            freshComp.Bottlecaps = bottlecaps;
            freshComp.Loaded = true;
            Dirty(uid, freshComp);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load currency for {userId}:{characterName}: {ex}");
        }
    }

    private async void SaveCurrency(PersistentCurrencyComponent comp)
    {
        if (comp.UserId == null || comp.CharacterName == null)
            return;

        if (!Guid.TryParse(comp.UserId, out var userId))
            return;

        try
        {
            await _db.UpsertCharacterCurrencyAsync(userId, comp.CharacterName, comp.Bottlecaps);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to save currency for {comp.UserId}:{comp.CharacterName}: {ex}");
        }
    }

    // ── One-time JSON migration ───────────────────────────────────────────────

    /// <summary>
    /// If the old currency_data.json file exists, imports all Bottle Cap balances into the database
    /// and renames the file to prevent re-import on subsequent starts.
    /// </summary>
    private async void MigrateJsonToDatabase()
    {
        try
        {
            var userDataPath = _resourceManager.UserData.RootDir ?? ".";
            var jsonPath = Path.Combine(userDataPath, "currency_data.json");

            if (!File.Exists(jsonPath))
                return;

            _log.Info("Found currency_data.json — starting one-time migration to database...");

            var json = File.ReadAllText(jsonPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, LegacyCharacterCurrency>>(json);

            if (data == null || data.Count == 0)
            {
                File.Move(jsonPath, jsonPath + ".migrated");
                return;
            }

            var count = 0;
            foreach (var entry in data.Values)
            {
                if (entry.Bottlecaps <= 0)
                    continue;

                if (!Guid.TryParse(entry.UserId, out var userId))
                    continue;

                if (string.IsNullOrEmpty(entry.CharacterName))
                    continue;

                await _db.UpsertCharacterCurrencyAsync(userId, entry.CharacterName, entry.Bottlecaps);
                count++;
            }

            // Rename to prevent re-import.
            File.Move(jsonPath, jsonPath + ".migrated");

            _log.Info($"Migrated {count} Bottle Cap balance(s) from currency_data.json to database.");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to migrate currency_data.json to database: {ex}");
        }
    }

    /// <summary>
    /// Legacy JSON data structure for reading the old currency_data.json file during migration.
    /// </summary>
    private sealed class LegacyCharacterCurrency
    {
        public string UserId { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public int Bottlecaps { get; set; }
        public int NCRDollars { get; set; }
        public int LegionDenarii { get; set; }
        public int PrewarMoney { get; set; }
    }
}
