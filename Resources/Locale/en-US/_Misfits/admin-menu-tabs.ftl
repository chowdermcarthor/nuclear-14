# Misfits Change - Locale strings for admin menu tab descriptions and Staff tab

# Tab names
misfits-admin-menu-whitelisting-tab = Whitelisting
misfits-admin-menu-staff-tab = Staff
# #Misfits Change - LoreMaster tab for faction objective management
misfits-admin-menu-loremaster-tab = Loremaster

## LoreMaster tab UI strings
loremaster-tab-faction-label = Faction:
loremaster-tab-refresh = Refresh
loremaster-tab-members-header = Online Members & Objectives
loremaster-tab-issue-header = Issue New Orders
loremaster-tab-objective-label = Objective:
loremaster-tab-issue-button = Issue Orders
loremaster-tab-refreshing = Refreshing...
loremaster-tab-no-members = No online members found.
loremaster-tab-no-objectives = (no objectives assigned)
loremaster-tab-target-none = Target: (none online)
loremaster-tab-target-label = Will issue to: {$name} ({$job})
loremaster-tab-member-count = Online members: {$count}
loremaster-tab-issuing = Issuing...

## Per-faction objective selector labels
# NCR
loremaster-obj-ncr-kill = Eliminate High-Value Target
loremaster-obj-ncr-steal-enclave = Steal Enclave Intel Holotape
loremaster-obj-ncr-steal-bosw = Steal Brotherhood Plans Holotape
loremaster-obj-ncr-steal-legion = Steal Legion Orders Holotape
# Brotherhood of Steel
loremaster-obj-bosw-kill = Purge Enemy Officer
loremaster-obj-bosw-steal-geck = Seize GECK Location Holotape
loremaster-obj-bosw-steal-ncr = Seize NCR Plans Holotape
loremaster-obj-bosw-steal-legion = Seize Legion Battle Orders Holotape
# Caesar's Legion
loremaster-obj-legion-kill = Execute Enemy for Caesar
loremaster-obj-legion-steal-ncr = Steal NCR Intel Holotape
loremaster-obj-legion-steal-bosw = Steal Brotherhood Orders Holotape
loremaster-obj-legion-steal-enclave = Steal Enclave Tech Data Holotape

# Staff tab button + description
misfits-staff-tab-permissions-btn = Admin Permissions
misfits-staff-tab-permissions-desc = Search staff by name and manage their ranks and permission flags.

# Admin tab button descriptions
misfits-admin-tab-kick-desc = Open the player actions panel (kick, warn, etc.)
misfits-admin-tab-ban-desc = Open the banning panel to ban or unban players.
misfits-admin-tab-aghost-desc = Toggle admin ghost mode for invisible observation.
misfits-admin-tab-teleport-desc = Teleport to a location, player, or entity.
misfits-admin-tab-announce-desc = Send a server-wide announcement to all players.
misfits-admin-tab-shuttle-desc = Call or recall the evacuation train.
misfits-admin-tab-logs-desc = View admin logs and filter by player or action.
misfits-admin-tab-fax-desc = Send an admin fax in-game.
misfits-admin-tab-spawn-dungeons-desc = Open the dungeon spawn panel to place procedural dungeons on the map.
misfits-admin-tab-time-transfer-desc = Transfer playtime between players.

# Adminbus tab button descriptions
misfits-adminbus-tab-spawn-entities-desc = Open the entity spawn panel to place objects in the world.
misfits-adminbus-tab-spawn-dungeons-desc = Open the dungeon spawn panel to place procedural dungeons on the map.
misfits-adminbus-tab-spawn-tiles-desc = Open the tile spawn panel to paint floor/wall tiles.
misfits-adminbus-tab-spawn-decals-desc = Open the decal placer to paint decals on tiles.
misfits-adminbus-tab-load-prototype-desc = Load a YAML prototype file from disk into the game.
misfits-adminbus-tab-load-blueprints-desc = Load a saved map blueprint (.yml) into the world.
misfits-adminbus-tab-delete-singulos-desc = Delete all singularity entities on the map.

# Atmos tab button descriptions
misfits-atmos-tab-add-atmos-desc = Initialize atmosphere on a grid that has none.
misfits-atmos-tab-add-gas-desc = Add a specific gas to an area.
misfits-atmos-tab-fill-gas-desc = Fill an entire grid with a chosen gas mixture.
misfits-atmos-tab-set-temp-desc = Set the temperature of gas in an area.

# Round tab button descriptions
misfits-round-tab-start-round = Start Round
misfits-round-tab-start-round-desc = Begin the round if it hasn't started yet.
misfits-round-tab-end-round = End Round
misfits-round-tab-end-round-desc = End the current round gracefully with summary screen.
misfits-round-tab-restart-round = Restart Round
misfits-round-tab-restart-round-desc = End the current round and start a new one.
misfits-round-tab-restart-now = Restart NOW
misfits-round-tab-restart-now-desc = Immediately restart the round without delay.
misfits-round-tab-extend-round = Extend Round
misfits-round-tab-extend-round-desc = Stop the incoming train and add more time before round end can trigger again.

# Server tab button descriptions
misfits-server-tab-shutdown-desc = Shut down the game server.
misfits-server-restart = Restart Server
misfits-server-tab-restart-desc = Cleanly restart the server process (watchdog/systemd will relaunch).
misfits-server-tab-ooc-desc = Toggle OOC (out-of-character) chat on or off.
misfits-server-tab-looc-desc = Toggle LOOC (local out-of-character) chat on or off.

# Whitelisting tab button descriptions
misfits-whitelisting-tab-role-whitelists-btn = Role Whitelists Menu
misfits-whitelisting-tab-role-whitelists-desc = Search for players and manage their job/role whitelists.
misfits-whitelisting-tab-job-slots-btn = Job Slots Menu
misfits-whitelisting-tab-job-slots-desc = Search for players and manage station job slot configuration.
misfits-whitelist-search-station = Station: {$station}
misfits-whitelist-search-station-none = Station: none
misfits-whitelist-search-role-time = Time: {$time}
misfits-whitelist-search-add-time = Add
misfits-whitelist-search-add-time-placeholder = 1h
misfits-whitelist-search-set-time = Set
misfits-whitelist-search-set-time-tooltip = Overwrite this role's playtime to an exact value
misfits-whitelist-search-dept-add-all = Add All
misfits-whitelist-search-dept-add-all-tooltip = Add this amount of time to every role in this faction
misfits-whitelist-search-dept-set-all = Set All
misfits-whitelist-search-dept-set-all-tooltip = Overwrite every role in this faction to this exact time
misfits-whitelist-search-slot-count = Slots: {$count}
misfits-whitelist-search-slot-unlimited = Slots: Unlimited
misfits-whitelist-search-slot-no-station = Slots: no station

# Job Slots EUI (standalone panel, no player search)
misfits-job-slots-station = Station: {$station}
misfits-job-slots-station-none = Station: none
misfits-job-slots-slot-count = Slots: {$count}
misfits-job-slots-slot-unlimited = Slots: Unlimited
misfits-job-slots-slot-no-station = Slots: no station
misfits-job-slots-slot-not-configured = Not configured

# Server restart command messages
misfits-server-restart-announcement = The server is restarting. Please reconnect in a moment.
# Used when the watchdog has a staged build ready — more informative for players
misfits-server-restart-announcement-update = The server is restarting to deploy a pending update. Please reconnect shortly.
misfits-server-restart-shutdown-reason = Server restarting by admin request.
# Admin-only chat notification when the watchdog signals a new build is staged and ready
misfits-server-update-pending-admin = [UPDATE] A new server build is staged and ready. Use "Restart Server" in the F7 Server tab to deploy it.
