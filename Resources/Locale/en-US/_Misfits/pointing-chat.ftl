## Misfits Chat Action Broadcasting — #Misfits Add
# Emote chat text broadcast for player interactions that normally only show as sprite popups.
# All strings are the action portion only; the emote system wraps them as: "* <name> <message> *"

## PointingChatSystem
pointing-chat-point-at-self = points at themselves
pointing-chat-point-at-other = points at {$other}

## OfferItemSystem
# Broadcast when a player hands an item to another player after accepting an offer.
misfits-chat-offer-handoff = hands {$item} to {$target}

## CarryingSystem
# Broadcast when a player picks up or puts down another entity.
misfits-chat-carry-pickup = picks up {$carried}
misfits-chat-carry-drop = puts down {$carried}
misfits-chat-carry-throw = throws {$victim}
misfits-chat-double-grab-throw = hurls {$victim} across the room

## CuffingChatSystem
# Broadcast when a player successfully restrains another entity with handcuffs.
misfits-chat-cuff-applied = restrains {$target}
misfits-chat-cuff-self = restrains themselves

## FactionBankTerminalSystem
# Observable emote broadcast to bystanders when a player uses a terminal.
misfits-chat-terminal-use = uses the {$terminal} terminal

## PersistentCurrencySystem
# Private feedback (only to the player) for deposit/withdraw actions.
misfits-currency-no-currency = You are not holding any currency to deposit.
misfits-currency-deposited = Deposited {$amount} {$type}. Total: {$total}
misfits-currency-insufficient = Not enough currency!
misfits-currency-withdrew = Withdrew {$amount} {$type}.

## SpearBlockSystem
# Emote sent from the defender describing the block — "* Jane deflects John's spear... *"
spear-block-embedded-emote = deflects {$thrower}'s {$spear}, embedding it in their {$shield}
spear-block-deflected-emote = deflects {$thrower}'s {$spear}, sending it to the ground

## GrabChatSystem
# Emote broadcast from the puller when they start dragging another mob.
misfits-chat-grab-start = grabs {$grabbed}

## BlockingChatSystem
# Emote broadcast from the blocker when they raise or lower a shield.
misfits-chat-blocking-start = raises {$shield}
misfits-chat-blocking-stop = lowers {$shield}

## PowerArmorChatSystem
# Emote broadcast when a power armor suit fully deploys or retracts its attached pieces.
misfits-chat-power-armor-close = locks into {$armor}
misfits-chat-power-armor-open = disengages {$armor}

## EnsnareableSystem
# Emotes for thrown bolas and similar throw-triggered ensnares.
misfits-chat-ensnare-hit = is hit by {$ensnare} and snared
misfits-chat-ensnare-free-start-self = starts working free from {$ensnare}
misfits-chat-ensnare-free-start-other = starts removing {$ensnare} from {$target}
misfits-chat-ensnare-free-fail-self = fails to get free of {$ensnare}
misfits-chat-ensnare-free-fail-other = fails to remove {$ensnare} from {$target}
misfits-chat-ensnare-free-complete-self = breaks free of {$ensnare}
misfits-chat-ensnare-free-complete-other = removes {$ensnare} from {$target}

## DoubleGrabSystem
# Carrier locks the victim into an active carry hold.
misfits-chat-double-grab-cinch = pins {$victim} in a firm grip
# Emote from the victim when they are picked up.
misfits-chat-double-grab-victim = is forcibly picked up by {$carrier}
# Victim breaks free during the pending-grab phase.
misfits-chat-double-grab-resist = breaks free from {$carrier}'s grip
# Victim gasps while being choked during an active carry.
misfits-chat-double-grab-gasp = gasps for air

## PersistentCurrencySystem — new keys
misfits-currency-unsupported-type = Only Bottlecaps can be deposited.

