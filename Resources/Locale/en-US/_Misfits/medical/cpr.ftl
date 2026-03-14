# CPR locale strings
# #Misfits Add - CPR system localisation

cpr-start-performer = You begin performing CPR on { $target }.
cpr-start-target = { $user } begins performing CPR on you.

# Keys used by CPRSystem.cs TrySendInGameICMessage calls (emote channel)
misfits-chat-cpr-start = begins performing CPR on { $target }.
# #Misfits Fix: victim emote removed — emote system prepends entity name causing broken
# formatting, and "you" is wrong for a message visible to everyone.
# misfits-chat-cpr-victim = { $user } begins performing CPR on you.
cpr-success-performer = You successfully perform CPR on { $target }!
cpr-success-target = { $user } performs CPR on you — your heart pounds back to life!
cpr-on-cooldown = You're too exhausted to perform CPR again so soon.
cpr-target-no-longer-critical = { $target } no longer needs CPR.
