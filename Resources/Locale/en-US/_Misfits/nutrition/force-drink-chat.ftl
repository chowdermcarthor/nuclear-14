# #Misfits Add: emote messages sent when a force-drink attempt begins (mirrors throw-impact-chat / buckle-chat pattern)

# Shown to the person doing the force-drinking
misfits-chat-force-drink-start = tries to force { $target } to drink { $item }!

# #Misfits Fix: victim emote removed — emote system prepends entity name causing broken
# formatting, and "you" is wrong for a message visible to everyone.
# misfits-chat-force-drink-victim = { $user } is trying to force you to drink { $item }!

# Shown to the person doing the force-feeding
misfits-chat-force-feed-start = tries to force { $target } to eat { $item }!

# #Misfits Fix: victim emote removed — same reason as force-drink-victim above.
# misfits-chat-force-feed-victim = { $user } is trying to force you to eat { $item }!
