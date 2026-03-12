// #Misfits Change /Tweak/: Route thrown-bola feedback through chat emotes instead of screen popups.
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Ensnaring;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server.Ensnaring;

public sealed partial class EnsnareableSystem : SharedEnsnareableSystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    
    public override void Initialize()
    {
        base.Initialize();

        InitializeEnsnaring();

        SubscribeLocalEvent<EnsnareableComponent, ComponentInit>(OnEnsnareableInit);
        SubscribeLocalEvent<EnsnareableComponent, EnsnareableDoAfterEvent>(OnDoAfter);
    }

    private void OnEnsnareableInit(EntityUid uid, EnsnareableComponent component, ComponentInit args)
    {
        component.Container = _container.EnsureContainer<Container>(uid, "ensnare");
    }

    private void OnDoAfter(EntityUid uid, EnsnareableComponent component, DoAfterEvent args)
    {
        if (args.Args.Target == null || args.Handled)
            return;

        if (!TryComp<EnsnaringComponent>(args.Args.Used, out var usedSnareComponent))
            return;

        var usedSnare = args.Args.Used.Value;

        if (args.Cancelled || !_container.Remove(usedSnare, component.Container))
        {
            if (usedSnareComponent.CanThrowTrigger)
            {
                var ensnareName = Identity.Entity(usedSnare, EntityManager);

                if (args.Args.User == uid)
                {
                    _chat.TrySendInGameICMessage(uid,
                        Loc.GetString("misfits-chat-ensnare-free-fail-self", ("ensnare", ensnareName)),
                        InGameICChatType.Emote,
                        ChatTransmitRange.Normal,
                        ignoreActionBlocker: true);
                }
                else
                {
                    var targetName = Identity.Entity(uid, EntityManager);
                    _chat.TrySendInGameICMessage(args.Args.User,
                        Loc.GetString("misfits-chat-ensnare-free-fail-other", ("ensnare", ensnareName), ("target", targetName)),
                        InGameICChatType.Emote,
                        ChatTransmitRange.Normal,
                        ignoreActionBlocker: true);
                }
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("ensnare-component-try-free-fail", ("ensnare", args.Args.Used)), uid, uid, PopupType.MediumCaution);
            }

            return;
        }

        component.IsEnsnared = component.Container.ContainedEntities.Count > 0;
        Dirty(uid, component);
        usedSnareComponent.Ensnared = null;

        if (usedSnareComponent.DestroyOnRemove)
            QueueDel(usedSnare);
        else
            _hands.PickupOrDrop(args.Args.User, usedSnare);

        if (usedSnareComponent.CanThrowTrigger)
        {
            var ensnareName = Identity.Entity(usedSnare, EntityManager);

            if (args.Args.User == uid)
            {
                _chat.TrySendInGameICMessage(uid,
                    Loc.GetString("misfits-chat-ensnare-free-complete-self", ("ensnare", ensnareName)),
                    InGameICChatType.Emote,
                    ChatTransmitRange.Normal,
                    ignoreActionBlocker: true);
            }
            else
            {
                var targetName = Identity.Entity(uid, EntityManager);
                _chat.TrySendInGameICMessage(args.Args.User,
                    Loc.GetString("misfits-chat-ensnare-free-complete-other", ("ensnare", ensnareName), ("target", targetName)),
                    InGameICChatType.Emote,
                    ChatTransmitRange.Normal,
                    ignoreActionBlocker: true);
            }
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("ensnare-component-try-free-complete", ("ensnare", args.Args.Used)), uid, uid, PopupType.Medium);
        }

        UpdateAlert(args.Args.Target.Value, component);
        var ev = new EnsnareRemoveEvent(usedSnareComponent.WalkSpeed, usedSnareComponent.SprintSpeed);
        RaiseLocalEvent(uid, ev);

        args.Handled = true;
    }
}
