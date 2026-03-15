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

## EscapeInventorySystem / ResistLockerSystem
# Broadcast when a carried or locked-in entity struggles.
misfits-chat-struggle-carried = struggles against {$carrier}'s grip
misfits-chat-locker-struggle = struggles inside {$container}
# Broadcast when a trapped entity successfully forces their way out.
misfits-chat-locker-breakout = breaks out of {$container}

## InjectorSystem / HypospraySystem
# Broadcast when a player injects another entity.
misfits-chat-inject-other = injects {$target} with {$item}

## CuffingChatSystem
# Broadcast when a player successfully restrains another entity with handcuffs.
misfits-chat-cuff-applied = restrains {$target}
misfits-chat-cuff-self = restrains themselves

## StrippableSystem
# Emotes broadcast when a player visibly removes worn gear from another player.
misfits-chat-strip-remove = removes {$item} from {$target}
misfits-chat-strip-victim-remove = has their {$item} removed by {$user}
# Emotes broadcast when a player applies an ingestion-blocking item to another player.
misfits-chat-gag-apply = gags {$target} with {$item}
misfits-chat-gag-victim = is gagged with {$item} by {$user}

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
# Emote broadcast when the wearer enters or exits the brace stance via hotkey.
misfits-chat-power-armor-brace-activate = locks {$armor}'s servos and braces for fire
misfits-chat-power-armor-brace-deactivate = releases {$armor}'s servos and resumes movement

## PowerArmorBraceSystem
# Client-side popup when the wearer tries to brace but is not standing on a valid grid tile.
power-armor-brace-cant-here = You can't brace here.

## BodySystem
# Emote broadcast when a mob is fully gibbed.
misfits-chat-gib-body-1 = explodes into giblets.
misfits-chat-gib-body-2 = bursts apart in a shower of gore.
misfits-chat-gib-body-3 = comes apart in a wet spray of viscera.
# Emote broadcast when a specific body part is gibbed.
misfits-chat-gib-part-1 = has their {$part} violently blown apart.
misfits-chat-gib-part-2 = loses their {$part} in a spray of gore.
misfits-chat-gib-part-3 = has their {$part} ripped into bloody chunks.

## DoubleGrabSystem
# Carrier locks the victim into an active carry hold.
misfits-chat-double-grab-cinch = pins {$victim} in a firm grip
# #Misfits Fix: victim emote removed — redundant with performer's "pins" emote above.
# misfits-chat-double-grab-victim = is forcibly picked up by {$carrier}
# Victim breaks free during the pending-grab phase.
misfits-chat-double-grab-resist = breaks free from {$carrier}'s grip
# Victim gasps while being choked during an active carry.
misfits-chat-double-grab-gasp = gasps for air

## PersistentCurrencySystem — new keys
misfits-currency-unsupported-type = Only Bottlecaps can be deposited.

## MisfitsEmoteThrottleSystem
misfits-emote-clump = {$message} (x{$count})

