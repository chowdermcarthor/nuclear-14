# #Misfits Add: emote messages for ensnare hit and self/other freeing actions
# Mirrors the pattern used by throw-impact-chat.ftl and buckle-chat.ftl.

# Thrown/applied ensnare lands on a target — shown to the attacker
misfits-chat-ensnare-hit = ensnares { $target } with { $ensnare }!

# Victim starts trying to free themselves
misfits-chat-ensnare-free-start-self = struggles to free themselves from { $ensnare }...

# Third party begins freeing the victim (shown to the helper)
misfits-chat-ensnare-free-start-other = begins freeing { $target } from { $ensnare }...

# Victim fails to free themselves
misfits-chat-ensnare-free-fail-self = fails to free themselves from { $ensnare }.

# Third party fails to free the victim
misfits-chat-ensnare-free-fail-other = fails to free { $target } from { $ensnare }.

# Victim successfully frees themselves
misfits-chat-ensnare-free-complete-self = breaks free from { $ensnare }!

# Third party successfully frees the victim
misfits-chat-ensnare-free-complete-other = frees { $target } from { $ensnare }!
