// #Misfits Change /Tweak/: Route thrown-bola feedback through chat emotes instead of screen popups.
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Ensnaring;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Server.Ensnaring;

public sealed partial class EnsnareableSystem : SharedEnsnareableSystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    // #Misfits Add: Track which targets have been hit by each thrown ensnaring item to prevent double-hits
    private Dictionary<EntityUid, HashSet<EntityUid>> _thrownEnsnareTargets = new();
    
    public override void Initialize()
    {
        base.Initialize();

        InitializeEnsnaring();

        SubscribeLocalEvent<EnsnareableComponent, ComponentInit>(OnEnsnareableInit);
        SubscribeLocalEvent<EnsnareableComponent, EnsnareableDoAfterEvent>(OnDoAfter);
        // #Misfits Add: Clean up tracking when thrown ensnaring items are deleted
        SubscribeLocalEvent<ThrownItemComponent, ComponentRemove>(OnThrownItemRemove);
    }

    private void OnEnsnareableInit(EntityUid uid, EnsnareableComponent component, ComponentInit args)
    {
        component.Container = _container.EnsureContainer<Container>(uid, "ensnare");
    }

    // #Misfits Add: Clean up tracking data when a thrown item is removed
    private void OnThrownItemRemove(EntityUid uid, ThrownItemComponent component, ComponentRemove args)
    {
        if (_thrownEnsnareTargets.ContainsKey(uid))
            _thrownEnsnareTargets.Remove(uid);
    }

    // #Misfits Add: Perform swept collision detection for thrown ensnaring items
    // This catches moving targets that tunnel through discrete collision detection
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Query for all thrown items with ensnaring components
        var query = EntityQueryEnumerator<ThrownItemComponent, EnsnaringComponent>();
        while (query.MoveNext(out var thrownUid, out var thrown, out var ensnaring))
        {
            // Only process items that can trigger ensnaring when thrown
            if (!ensnaring.CanThrowTrigger || ensnaring.Ensnared != null)
                continue;

            // Get the bola's current position for proximity checks
            var thrownXform = Transform(thrownUid);
            var thrownPos = _transform.GetWorldPosition(thrownXform);
            var mapId = thrownXform.MapID;

            // Create a search radius slightly larger than the item to catch nearby targets
            // This helps catch targets that are moving and would otherwise tunnel through
            const float SearchRadius = 0.5f; // Adjust based on desired hit detection range
            var boundingBox = Box2.CenteredAround(thrownPos, new Vector2(SearchRadius * 2, SearchRadius * 2));

            // Track this bola's attempted targets if not already tracked
            if (!_thrownEnsnareTargets.ContainsKey(thrownUid))
                _thrownEnsnareTargets[thrownUid] = new HashSet<EntityUid>();

            var attemptedTargets = _thrownEnsnareTargets[thrownUid];

            // Query for nearby entities using entity lookup
            foreach (var entity in _entityLookup.GetEntitiesIntersecting(mapId, boundingBox))
            {
                // Skip the thrower
                if (entity == thrown.Thrower)
                    continue;

                // Check if this entity has an ensnareable component and hasn't been hit yet
                if (TryComp<EnsnareableComponent>(entity, out var ensnareable) &&
                    !attemptedTargets.Contains(entity))
                {
                    // Mark this target as attempted
                    attemptedTargets.Add(entity);

                    // Try to ensnare this target
                    TryEnsnare(entity, thrownUid, ensnaring);

                    // If ensnaring succeeded, send feedback
                    if (ensnaring.Ensnared == entity)
                    {
                        var ensnareName = Identity.Entity(thrownUid, EntityManager);
                        var targetName = Identity.Entity(entity, EntityManager);
                        _chat.TrySendInGameICMessage(entity,
                            Loc.GetString("misfits-chat-ensnare-hit", ("target", targetName), ("ensnare", ensnareName)),
                            InGameICChatType.Emote,
                            ChatTransmitRange.Normal,
                            ignoreActionBlocker: true);
                    }
                }
            }
        }
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
