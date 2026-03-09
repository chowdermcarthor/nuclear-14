# #Misfits Change
# Ghoul Reversal (De-Ghoulification) Syringe Localization

ghoul-reversal-self = You feel a strange warmth spreading through your veins as the compound begins to work. The radiation damage starts to reverse... you're becoming human again!
ghoul-reversal-others = {THE($target)}'s ghoulish features slowly fade away as their skin regains its original tone and texture, restoring them to a human form!
ghoul-reversal-not-ghoul = They don't appear to be a ghoul. This serum has no effect on them.

# Reagent (Promethine) popup strings
ghoul-reversal-reagent-self = The Promethine floods your cells — the radiation markers begin to dissolve! You can feel yourself returning to normal...
ghoul-reversal-reagent-others = {THE($target)} shudders as their ghoulish appearance slowly recedes, skin and eyes returning to their human state!
ghoul-reversal-reagent-too-old = The Promethine has no effect. The ghoulification markers are too deeply set to be reversed by chemistry.

# Radiation death ghoulification
ghoul-on-death-self = The fatal dose of radiation tears through your body — but instead of killing you, it transforms you. You are a ghoul now.
ghoul-on-death-others = {THE($target)} collapses from the radiation... but rises again, skin twisted and eyes hollow. They've become a ghoul!

# Reagent guidebook
reagent-effect-guidebook-ghoul-reversal = reverse ghoulification if administered within 12 hours of exposure ({ $chance ->
  [1] always
  *[other] { $chance } chance
})

# Promethine reagent strings
reagent-name-promethine = Promethine
reagent-desc-promethine = An extraordinarily rare compound synthesized from RadAway, RadX, and cellular catalysts. Clinical studies suggest it can suppress and reverse the FEV radiation cascade responsible for ghoulification — but only within a narrow window after initial exposure. After 12 hours, the cellular mutation becomes permanent.
reagent-physical-desc-luminous = luminous golden
