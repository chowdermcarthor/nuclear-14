# #Misfits Add - Disease system locale strings.
# All disease names, descriptions, popups, and machine messages.

# --- Disease Names ---
disease-rad-flu-name = Rad Flu
disease-ghoul-rot-name = Ghoul Rot
disease-mole-rat-name = Mole Rat Disease
disease-brahmin-pox-name = Brahmin Pox
disease-wasteland-fever-name = Wasteland Fever
disease-spore-sickness-name = Spore Sickness
disease-new-plague-name = New Plague
disease-swamp-itch-name = Swamp Itch

# --- Disease Symptom Popups ---
disease-rad-flu-popup-nausea = You feel nauseous...
disease-ghoul-rot-popup-itch = Your skin is rotting and itches painfully.
disease-mole-rat-popup-fever = You feel feverish and weak.
disease-brahmin-pox-popup-spots = Itchy spots are forming on your skin.
disease-wasteland-fever-popup-hot = You're burning up with fever!
disease-spore-sickness-popup-cough = Your lungs burn and you can't stop coughing.
disease-new-plague-popup-chills = Violent chills wrack your body.
disease-swamp-itch-popup-scratch = You can't stop scratching yourself.

# --- Swab Messages ---
disease-swab-already-used = This swab has already been used.
disease-swab-no-disease = The target doesn't appear to be diseased.
disease-swab-collecting = You carefully collect a sample...
disease-swab-collected = Sample collected successfully.

# --- Diagnoser Messages ---
disease-diagnoser-no-sample = Insert a used disease swab to begin analysis.
disease-diagnoser-already-running = The diagnoser is already processing a sample.
disease-diagnoser-started = The diagnoser hums to life and begins analyzing the sample.
disease-diagnoser-finished = The diagnoser prints a diagnosis report.

# --- Diagnosis Report ---
disease-diagnosis-report = DISEASE ANALYSIS REPORT
    Disease Identified: {$disease}
    Prototype Reference: {$diseaseId}
    Status: CONFIRMED PATHOGEN
    Recommendation: Present this report to a vaccinator unit.

# --- Vaccinator Messages ---
disease-vaccinator-no-diagnosis = Insert a diagnosis report to begin vaccine production.
disease-vaccinator-already-running = The vaccinator is already producing a vaccine.
disease-vaccinator-started = The vaccinator begins synthesizing a vaccine.
disease-vaccinator-finished = The vaccinator ejects a freshly produced vaccine.

# --- Vaccine Messages ---
disease-vaccine-named = {$disease} vaccine
disease-vaccine-empty = This vaccine has already been used.
disease-vaccine-injecting = You begin injecting the vaccine...
disease-vaccine-injected = Vaccine administered successfully. Immunity granted.
