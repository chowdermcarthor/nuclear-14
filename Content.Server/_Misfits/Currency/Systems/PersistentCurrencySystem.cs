// #Misfits Change - Persistent currency system
using System.IO;
using System.Text.Json;
using Content.Server.Mind;
using Content.Shared._Misfits.Currency;
using Content.Shared._Misfits.Currency.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Currency.Systems;

/// <summary>
/// Handles consuming currency items and adding them to a player's persistent balance.
/// </summary>
public sealed class PersistentCurrencySystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    // #Misfits Change - Sawmill for wallet logging
    private ISawmill _log = default!;

    private const string CurrencyDataPath = "/currency_data.json";
    private readonly Dictionary<string, CharacterCurrency> _currencyData = new();
    private string _saveFilePath = string.Empty;

    /// <summary>
    /// Maps CurrencyType to the entity prototype ID to spawn when withdrawing.
    /// </summary>
    private static readonly Dictionary<CurrencyType, string> CurrencyPrototypes = new()
    {
        { CurrencyType.Bottlecaps, "N14CurrencyCap" },
        { CurrencyType.NCRDollars, "N14CurrencyNCRDollar" },
        { CurrencyType.LegionDenarii, "N14CurrencyLegionDenarius" },
        { CurrencyType.PrewarMoney, "N14CurrencyPrewar" },
    };

    public override void Initialize()
    {
        base.Initialize();

        // #Misfits Change - initialise the sawmill before any other setup
        _log = Logger.GetSawmill("persistent_currency");

        SubscribeLocalEvent<ConsumableCurrencyComponent, UseInHandEvent>(OnUseCurrency);
        SubscribeLocalEvent<PersistentCurrencyComponent, ComponentStartup>(OnCurrencyStartup);
        SubscribeLocalEvent<PersistentCurrencyComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PersistentCurrencyComponent, ComponentShutdown>(OnCurrencyShutdown);
        SubscribeLocalEvent<PersistentCurrencyComponent, OpenCurrencyWalletEvent>(OnOpenWallet);
        SubscribeNetworkEvent<WithdrawCurrencyRequest>(OnWithdrawRequest);
        SubscribeNetworkEvent<OpenWalletHudMessage>(OnHudOpenWallet); // #Misfits Change - HUD button support
        SubscribeNetworkEvent<DepositHeldCurrencyRequest>(OnDepositHeldRequest); // #Misfits Change - wallet Deposit In Hand button

        // Set up save file path
        var userDataPath = _resourceManager.UserData.RootDir ?? ".";
        _saveFilePath = Path.Combine(userDataPath, "currency_data.json");

        // Load existing data
        LoadCurrencyData();
    }

    private void OnOpenWallet(Entity<PersistentCurrencyComponent> ent, ref OpenCurrencyWalletEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var comp = ent.Comp;
        var msg = new CurrencyWalletStateMessage
        {
            Bottlecaps = comp.Bottlecaps,
            NCRDollars = comp.NCRDollars,
            LegionDenarii = comp.LegionDenarii,
            PrewarMoney = comp.PrewarMoney,
        };

        RaiseNetworkEvent(msg, actor.PlayerSession.Channel);
    }

    // #Misfits Change - handles wallet open request from the dedicated HUD button
    private void OnHudOpenWallet(OpenWalletHudMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (player.AttachedEntity is not { } uid)
            return;

        if (!TryComp<PersistentCurrencyComponent>(uid, out var comp))
            return;

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        var stateMsg = new CurrencyWalletStateMessage
        {
            Bottlecaps = comp.Bottlecaps,
            NCRDollars = comp.NCRDollars,
            LegionDenarii = comp.LegionDenarii,
            PrewarMoney = comp.PrewarMoney,
        };

        RaiseNetworkEvent(stateMsg, actor.PlayerSession.Channel);
    }

    // #Misfits Change - Deposit In Hand button: deposit whatever ConsumableCurrency item the player is holding
    private void OnDepositHeldRequest(DepositHeldCurrencyRequest msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (player.AttachedEntity is not { } uid)
            return;

        if (!TryComp<PersistentCurrencyComponent>(uid, out var comp))
            return;

        // Find a ConsumableCurrencyComponent item in any held hand
        EntityUid? heldItem = null;
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (HasComp<ConsumableCurrencyComponent>(held))
            {
                heldItem = held;
                break;
            }
        }

        if (heldItem == null)
        {
            _popup.PopupEntity("You're not holding any currency!", uid, uid);
            return;
        }

        if (!TryComp<ConsumableCurrencyComponent>(heldItem.Value, out var currency))
            return;

        // Determine deposit amount (stack-aware)
        var amount = currency.ValuePerUnit;
        if (TryComp<StackComponent>(heldItem.Value, out var stack))
            amount *= stack.Count;

        // Credit the balance
        var typeName = currency.CurrencyType switch
        {
            CurrencyType.Bottlecaps => "bottlecaps",
            CurrencyType.NCRDollars => "NCR dollars",
            CurrencyType.LegionDenarii => "denarii",
            CurrencyType.PrewarMoney => "pre-war money",
            _ => "currency"
        };

        switch (currency.CurrencyType)
        {
            case CurrencyType.Bottlecaps:    comp.Bottlecaps    += amount; break;
            case CurrencyType.NCRDollars:    comp.NCRDollars    += amount; break;
            case CurrencyType.LegionDenarii: comp.LegionDenarii += amount; break;
            case CurrencyType.PrewarMoney:   comp.PrewarMoney   += amount; break;
        }

        var total = GetBalance(comp, currency.CurrencyType);
        _popup.PopupEntity($"Deposited {amount} {typeName}. Total: {total}", uid, uid);

        Dirty(uid, comp);

        if (comp.UserId != null && comp.CharacterName != null)
            SaveCurrency(comp.UserId, comp.CharacterName, comp);

        // Delete the held currency item
        QueueDel(heldItem.Value);

        // Send refreshed state back so the window updates immediately
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        RaiseNetworkEvent(new CurrencyWalletStateMessage
        {
            Bottlecaps    = comp.Bottlecaps,
            NCRDollars    = comp.NCRDollars,
            LegionDenarii = comp.LegionDenarii,
            PrewarMoney   = comp.PrewarMoney,
        }, actor.PlayerSession.Channel);
    }

    private void OnWithdrawRequest(WithdrawCurrencyRequest msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (player.AttachedEntity is not { } uid)
            return;

        if (!TryComp<PersistentCurrencyComponent>(uid, out var comp))
            return;

        if (msg.Amount <= 0)
            return;

        // Check balance
        var balance = GetBalance(comp, msg.CurrencyType);
        if (balance < msg.Amount)
        {
            _popup.PopupEntity("Not enough currency!", uid, uid);
            return;
        }

        // Deduct
        SetBalance(comp, msg.CurrencyType, balance - msg.Amount);
        Dirty(uid, comp);

        // Save
        if (comp.UserId != null && comp.CharacterName != null)
            SaveCurrency(comp.UserId, comp.CharacterName, comp);

        // Spawn the currency items
        if (!CurrencyPrototypes.TryGetValue(msg.CurrencyType, out var protoId))
            return;

        var spawned = Spawn(protoId, Transform(uid).Coordinates);

        // Set stack count if applicable
        if (TryComp<StackComponent>(spawned, out var stackComp) && msg.Amount > 1)
            _stack.SetCount(spawned, msg.Amount);

        // Try to put in hand
        _hands.TryPickupAnyHand(uid, spawned);

        var typeName = msg.CurrencyType switch
        {
            CurrencyType.Bottlecaps => "bottlecaps",
            CurrencyType.NCRDollars => "NCR dollars",
            CurrencyType.LegionDenarii => "denarii",
            CurrencyType.PrewarMoney => "pre-war money",
            _ => "currency"
        };

        _popup.PopupEntity($"Withdrew {msg.Amount} {typeName}.", uid, uid);

        // Send updated state to client
        var stateMsg = new CurrencyWalletStateMessage
        {
            Bottlecaps = comp.Bottlecaps,
            NCRDollars = comp.NCRDollars,
            LegionDenarii = comp.LegionDenarii,
            PrewarMoney = comp.PrewarMoney,
        };

        RaiseNetworkEvent(stateMsg, player.Channel);
    }

    private int GetBalance(PersistentCurrencyComponent comp, CurrencyType type)
    {
        return type switch
        {
            CurrencyType.Bottlecaps => comp.Bottlecaps,
            CurrencyType.NCRDollars => comp.NCRDollars,
            CurrencyType.LegionDenarii => comp.LegionDenarii,
            CurrencyType.PrewarMoney => comp.PrewarMoney,
            _ => 0
        };
    }

    private void SetBalance(PersistentCurrencyComponent comp, CurrencyType type, int value)
    {
        switch (type)
        {
            case CurrencyType.Bottlecaps:
                comp.Bottlecaps = value;
                break;
            case CurrencyType.NCRDollars:
                comp.NCRDollars = value;
                break;
            case CurrencyType.LegionDenarii:
                comp.LegionDenarii = value;
                break;
            case CurrencyType.PrewarMoney:
                comp.PrewarMoney = value;
                break;
        }
    }

    private void OnUseCurrency(Entity<ConsumableCurrencyComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;

        // Ensure the user has the persistent currency component
        var currencyComp = EnsureComp<PersistentCurrencyComponent>(user);

        // Get the amount to add (from stack or single item)
        int amount = ent.Comp.ValuePerUnit;
        if (TryComp<StackComponent>(ent, out var stack))
        {
            amount *= stack.Count;
        }

        // Add to the appropriate currency type
        var typeName = "";
        switch (ent.Comp.CurrencyType)
        {
            case CurrencyType.Bottlecaps:
                currencyComp.Bottlecaps += amount;
                typeName = "bottlecaps";
                break;
            case CurrencyType.NCRDollars:
                currencyComp.NCRDollars += amount;
                typeName = "NCR dollars";
                break;
            case CurrencyType.LegionDenarii:
                currencyComp.LegionDenarii += amount;
                typeName = "denarii";
                break;
            case CurrencyType.PrewarMoney:
                currencyComp.PrewarMoney += amount;
                typeName = "pre-war money";
                break;
        }

        var total = GetBalance(currencyComp, ent.Comp.CurrencyType);
        _popup.PopupEntity($"Deposited {amount} {typeName}. Total: {total}", user, user);

        Dirty(user, currencyComp);

        // Save to file
        if (currencyComp.UserId != null && currencyComp.CharacterName != null)
        {
            SaveCurrency(currencyComp.UserId, currencyComp.CharacterName, currencyComp);
        }

        // Delete the currency item
        QueueDel(ent);

        // #Misfits Change - refresh the wallet window if the player has it open
        if (TryComp<ActorComponent>(user, out var actor))
        {
            RaiseNetworkEvent(new CurrencyWalletStateMessage
            {
                Bottlecaps    = currencyComp.Bottlecaps,
                NCRDollars    = currencyComp.NCRDollars,
                LegionDenarii = currencyComp.LegionDenarii,
                PrewarMoney   = currencyComp.PrewarMoney,
            }, actor.PlayerSession.Channel);
        }

        args.Handled = true;
    }

    private void OnCurrencyStartup(Entity<PersistentCurrencyComponent> ent, ref ComponentStartup args)
    {
        // #Misfits Change - action intentionally not granted; wallet is accessed via the AlertsUI HUD button instead

        // Load currency from file when component starts up
        if (TryComp<ActorComponent>(ent, out var actor))
        {
            LoadCurrency(ent, ent.Comp, actor.PlayerSession);
        }
    }

    private void OnCurrencyShutdown(Entity<PersistentCurrencyComponent> ent, ref ComponentShutdown args)
    {
        // #Misfits Change - nothing to remove; action is no longer granted
    }

    private void OnPlayerAttached(Entity<PersistentCurrencyComponent> ent, ref PlayerAttachedEvent args)
    {
        // Load currency when a player is attached to their character
        LoadCurrency(ent, ent.Comp, args.Player);
    }

    private void LoadCurrency(EntityUid uid, PersistentCurrencyComponent comp, ICommonSession session)
    {
        if (comp.Loaded)
            return;

        // Get character name from mind
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        var characterName = mind.CharacterName;
        if (string.IsNullOrEmpty(characterName))
            return;

        comp.UserId = session.UserId.ToString();
        comp.CharacterName = characterName;

        // Load from saved data
        var key = GetCurrencyKey(comp.UserId, characterName);
        if (_currencyData.TryGetValue(key, out var currency))
        {
            comp.Bottlecaps = currency.Bottlecaps;
            comp.NCRDollars = currency.NCRDollars;
            comp.LegionDenarii = currency.LegionDenarii;
            comp.PrewarMoney = currency.PrewarMoney;
        }

        comp.Loaded = true;
        Dirty(uid, comp);
    }

    private void SaveCurrency(string userId, string characterName, PersistentCurrencyComponent comp)
    {
        var key = GetCurrencyKey(userId, characterName);
        _currencyData[key] = new CharacterCurrency
        {
            UserId = userId,
            CharacterName = characterName,
            Bottlecaps = comp.Bottlecaps,
            NCRDollars = comp.NCRDollars,
            LegionDenarii = comp.LegionDenarii,
            PrewarMoney = comp.PrewarMoney,
        };

        SaveCurrencyData();
    }

    private string GetCurrencyKey(string userId, string characterName)
    {
        return $"{userId}:{characterName}";
    }

    private void LoadCurrencyData()
    {
        try
        {
            if (!File.Exists(_saveFilePath))
                return;

            var json = File.ReadAllText(_saveFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, CharacterCurrency>>(json);

            if (data != null)
            {
                foreach (var kvp in data)
                {
                    _currencyData[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load currency data: {ex}");
        }
    }

    private void SaveCurrencyData()
    {
        try
        {
            var json = JsonSerializer.Serialize(_currencyData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_saveFilePath, json);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to save currency data: {ex}");
        }
    }
}

/// <summary>
/// Data structure for storing character currency
/// </summary>
public sealed class CharacterCurrency
{
    public string UserId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public int Bottlecaps { get; set; }
    public int NCRDollars { get; set; }
    public int LegionDenarii { get; set; }
    public int PrewarMoney { get; set; }
}
