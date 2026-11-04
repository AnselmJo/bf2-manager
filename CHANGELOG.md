# Changelog

## [1.3.0]

Bots that do what you set, coop that starts in every mod, Windows on ARM (Mac with Parallels) and readable update notes.

### Fixed
- **Bot count ignored in some mods:** mods like `bf2sf64` and `bfp2` ship an `ai\aiDefault.ai` with `overrideMenuSettings 1` – the game then always used the mod's own bots (31) and ignored count, distribution and difficulty from the manager. Before the server starts the manager now offers to switch this off (backup created); *Setup* does it for all mods.
- **At most 32 bots:** the coop server never spawns more than 32 bots, no matter what `sv.maxPlayers` or `setMaxNBots` say (measured: 40 requested → 32). Planning, preview and the bot count field now respect this, and the default for 64-player maps is 32 instead of 40.
- **Server stopped after 30 seconds:** the server was started with `@HOME@/ServerConfigs/…`, which BF2 resolves to the Windows *Documents* folder. If the manager's documents folder was elsewhere (Parallels shared folders, OneDrive, a changed folder), the server found no settings or map list and quit silently. The server now gets the real path, and the manager follows a changed *Documents* folder (existing server configs are copied over, nothing is overwritten).
- **Coop failed with "failed to initialize gamemode":** some mods (e.g. `bfp2`) lack `python\game\gamemodes\gpm_coop.py`. The manager now creates it from the mod's own `gpm_cq.py` – in the base game both files are identical except for comments. Asked once before the server starts; *Setup* does it for all mods.
- **Unplayable coop maps were offered:** a map only runs in coop if its `info\<map>.desc` lists `gpm_coop` (otherwise the server quits after about 30 s) and it has bot data (`AIPathFinding` and `StrategicAreas.ai` for the size). Maps without these are no longer offered for coop and are removed from the rotation (with *Undo*). Example: `bfp2` has three official coop maps; `wake_island_1707` has no bot paths at all.
- **Update notes showed raw Markdown** (`### Fixed`, `*text*`, `**text**`) and were cut after 1500 characters. They are now formatted (headings, lists, bold, italic, code), scrollable and complete, without the GitHub download hint.
- **Patching was blocked by any running server:** the 4 GB and unlock patches refused to work as soon as any BF2 game or server ran – even one from another folder. The manager now checks whether the file being patched is actually in use.

### New
- **Windows on ARM** (*Settings › This PC*): on ARM PCs (e.g. a Mac with Parallels) BF2 runs in Windows' x86 emulation and crashes at start or after the intro, and the server stops after a short time. The check shows what is missing and **Set up** fixes it without administrator rights: the emulation setting `ARM64CHPEDISABLED` for `BF2.exe` and `bf2_w32ded.exe` (Windows' hybrid system libraries compute `cos()` wrongly, so the menu shader `swiffmenu.fx` fails with "unexpected input register type"), and a 2-byte patch for `RendDX9.dll` / `RendDX9x2.dll`, which only know Intel and AMD processors ("Unknown processor family"). Backups are created.
- **What's new?** in *Extras › Updates* shows the notes of a new version before you install it.
- **Bots set vs. actual** on the *Live* page (e.g. *Bots 10/10*). If they differ for a while, the manager says why once per map – for example when the mod forces its own bots.
- **Clear reason when the server stops:** the manager now knows the exit code and run time. If the server quit by itself right after starting (it found nothing to do), it says so and does not restart it three times in vain; crashes show their code.
- **Bot module 6:** distributes bots for the 32 that actually spawn and reports the bot count of the current map.

## [1.2.3]

Bug fixes and an easier download.

### Fixed
- **Console results were invisible:** commands (e.g. `admin.listPlayers`, `sv.coopBotCount`) ran, but the answer only landed in a log that is no longer shown. The answer now appears under the console field within a few seconds, with a hint when the ID is wrong or the server does not reply.
- **Kick from the console:** `kick <name>`, `ban <name>` and `admin.kickPlayer <name>` now resolve the player name to the ID (IDs still work). Unknown or ambiguous names get a clear message.
- **Bot module 5:** after *Next map now* the module recomputed the bot values a second later at the end-of-round event and so threw away one-time values. Fixed. New command `@reload` reloads the **current** map with new bot values.
- **Keep vehicles off in coop:** `sv.noVehicles` is ignored by the game in coop mode (tested with `bf2` and `bf2all64`: identical objects with 0 and 1; in Conquest the vehicles disappear). The setting is now marked *Conquest only* with an explanation.
- **Advanced server settings:** switching *Advanced* on or off showed "Unexpected error: object reference not set" and the advanced cards never appeared.
- **Own server missing on Play:** a server running on this PC is now always listed (marked *this PC*), even if the PC does not receive its own network announcement (firewall, VPN adapters).
- **Rotation toolbar:** *Undo* overlapped *Shuffle* / *Clear*. All rotation actions are now compact icons with tooltips (up, down, remove, shuffle, undo, clear).
- **Mods / Versions:** "BF2.exe ?" and "Server ?" – patched game files carry no version info; the manager now shows *present* / *missing* and detects BF2Hub. Mods whose `mod.desc` only contains a translation key (e.g. `MODDESC_XP_TITLE`) show the folder name instead.

- **Firewall check was too optimistic:** a rule only counted by its name, so after moving the exe (e.g. from a ZIP or Downloads) the check said *OK* while Windows blocked the manager at the new place. Rules now must point to the current path. *Add rules* also replaces the rules Windows creates at its first prompt, which often allow only *public* networks.

### New
- **Change bots while the game runs** (*Live › Actions › Change bots …*): set count, distribution and difficulty for the running map; the map reloads (about 30 s), all players stay connected. Kicking single bots does not work – the AI refills them at once. The values apply to this round only.
- **Game folder is found automatically** when the manager sits next to it – the usual layout `Battlefield2\BF2Manager.exe` + `Battlefield2\Battlefield 2\BF2.exe` – or inside it, or in a subfolder like `_LAN`. The copy offer (*Settings › Folders*) now targets that layout.
- **Registration check** in *Settings › This PC*: shows whether the Battlefield 2 registration entry exists in the Windows registry (display only; the serial number belongs to your own copy).
- **Server search check** in *Settings › This PC*: the manager tests whether its own LAN announcement comes back. If not, it says so and offers **Allow in firewall** (one Windows prompt). Player PCs without a server send a small test packet for this; older managers ignore it.
- **Copy the manager into the game folder:** if it runs from somewhere else (e.g. Downloads), the manager offers once to copy itself next to `BF2.exe` – with a firewall rule for the new place and a *BF2 Manager* desktop shortcut – restarts from there and offers to delete the old copy. Also under *Settings › Folders*.
- **Refresh** button on *Servers on the network*. The list still updates by itself every few seconds; the note *searching …* shows that it is looking.
- **Link to the latest release** at the top of the README (the Releases page; the file itself is listed under *Assets*).

## [1.2.2]

Easier start for new players, profiles in the manager, bot radio switch.

### New
- **Welcome window on first start:** game folder (auto-detected, otherwise *Choose folder*) and player name in one step. If there is no BF2 profile yet, the manager creates it. Can be run again from the *Profile* menu.
- **"Your player" card on Play:** name, **Change name** (applies right away – no waiting, no restart), profile switcher, and the *Profile* menu with **New profile**, **Repair profile** (fresh files, same name – the fix for BF2 hanging at start or when joining), **Delete profile** and *Open profile folder*. Every change is backed up to `Documents\BF2_Backups`.
- **"Game stuck? Close it"** while BF2 runs, with tips (repair profile, ReShade off, windowed mode, check the server).
- **Game folder:** *Choose folder* directly on Play when BF2 is not found (also accepts the parent folder), *Settings › Folders › Change game folder* for later changes.
- **Bot radio messages on/off** (*Bots › Radio*): silences "Medic!", "Roger that" etc. by switching the mod's `ai\AIRadioMessages.ai` (backup created, applies at the next server start).
- **ReShade (optional) with setup guide:** what it does, how to install it from the official ReShade website for BF2 (Direct3D 9), freeze warning. The on/off switch appears once ReShade is installed.

### Changed
- The green "This PC is ready" bar is gone; the player card shows a small *✓ This PC is ready*. The yellow bar with **Fix** only appears when something needs attention.
- *Settings › Profile* is now *Game start* (windowed, skip intro, log in automatically); name and profile live on the Play page.
- "Log in automatically" passes the profile's account name, so login and profile always match.
- Bot module versions are only shown on the server PC (Host).
- Console commands window: close with **✕** at the top right (or Esc).

### Fixed
- *Settings › This PC* could not be expanded when everything was fine.
- *Fix* for the player name jumped to a page without a name field; it now asks for the name directly or creates the profile.
- Buttons in the status bar were square at rest and rounded on hover.
- No profile or only BF2's *Default* template: the name could not be set.

## [1.2.1]

First public release as **BF2 Manager** (previously BF2 Server Manager).

### New
- **Two modes: Play and Host**, switched at the top left. Play shows only what players need; Host contains everything including a Join button for the own server.
- **Play view like a server browser:** running servers with map, size, mode, players, bots and mod; **join compatibility** (mod present, same version) with one-click file fetch from the server PC; servers that are off are no longer listed; one entry per PC even with several network adapters.
- **Health bar:** green when the PC is ready, otherwise one *Fix* button; details are collapsed when everything is fine.
- **Server view follows the server state:** configuration while stopped, live view while running (players, upcoming maps, actions). Rotation and bot changes during a game are sent automatically and apply from the next map; the next map stays the one after the current.
- **Clear timing of changes:** ⚡ immediately, next map, or after a restart (with a *Restart now* button and a counter).
- **Rotation:** drag handles, right-click menu (custom bot values, duplicate, remove), shuffle, filter by map size, undo; rows show bots and difficulty, ★ marks custom values.
- **Your own default** for all mods (*Options › Default*), **recently started** configurations and presets in one menu.
- **Automatic saving** – no save button and no "unsaved changes" dialogs.
- **Crash and hang detection:** "Server stopped unexpectedly" / "Server not responding" with restart; automatic restart in *Options › Server behaviour* (default on).
- **Start check** before launching the server (rotation, bot module, port, server exe).
- **Diagnostics:** detected problems, server log, export without passwords, user names and public IP addresses.
- First start asks for the player name.

### Removed
- Getting-started card, pre-release updates, manual IP field on the main view (still available under *Server not found?*), the separate log card on the live view, one-time "next map only" values.

## [1.2.0]

Internal test version: player page, PC check, LAN discovery and file sync, updater, command assistant, leaderboard, scheduled messages, adaptive difficulty, portable mode.

## [1.1.1]

- Fixed: running-server bar after stop, mouse wheel in bot names, header overlap in small windows, silent removal of invalid rotation entries, Mono-only API crash at start.

## [1.1] / [1.0]

- Single exe with in-app setup, German/English, collapsible cards, live player list, bot names and AI file editors, unlock patches. First version as BF2 Coop Manager.
