# User guide

Switch between **Play** and **Host** at the top left. Every setting has an **ⓘ** with an explanation, the default and when a change applies. Everything is saved automatically.

## Where the manager lives

The manager finds the game folder by itself when it sits **next to it** (`Battlefield2\BF2Manager.exe` and `Battlefield2\Battlefield 2\BF2.exe`), inside it, or in a subfolder such as `_LAN`.

Started from Downloads or a ZIP? The manager offers once to **copy itself next to the game folder**, adds the firewall rule for that place, puts a *BF2 Manager* shortcut on the desktop and restarts from there. The old copy can be deleted afterwards. Any time later: *Settings › Folders › Copy manager next to the game folder*.

## First start

A welcome window asks for two things: the **game folder** (found automatically; otherwise *Choose folder*) and your **player name**. The manager creates your Battlefield 2 profile if there is none. *Later* skips it – the **Your player** card on *Play* asks again. Run it again any time: *Profile › Run first-time setup again*.

## Play

**Your player** – your name, **Change name** (applies right away), profile switcher when you have several, and the *Profile* menu: **New profile**, **Repair profile** (fresh files, same name – helps when BF2 hangs at start or when joining), **Delete profile**, open the profile folder. Every change is backed up to `Documents\BF2_Backups`. While BF2 runs, the card offers **Game stuck? Close it** with tips.

**Play** – the main view. Running servers on the network appear automatically with map, size, mode, players, bots and mod. The list updates itself every few seconds; **Refresh** clears it and searches right away. A server running on this PC is always listed and marked *this PC*.
- **✓ Compatible** → click **Join**. The game starts in the server's mod, logs in and joins.
- **⚠ Mod missing / files differ** → just click **Join**: the manager compares your mod with the server PC by file content, copies only what differs (each file is verified) and then starts the game. Fun packs (mods with a `fun.recipe`) need only a few KB; their base mod is synced first if needed. The first time a server PC offers a mod it checks all files once (a notice shows when it starts and when it is ready); until then players are compared by version number.
- **Manager x.y – update** appears when the server PC has a newer manager.
- No server? *Start game without server* opens the main menu. *Server not found?* lets you enter an IP (only needed for VPN LANs).
- When something on this PC needs attention, a yellow bar with **Fix** appears at the top; when all is well the card just shows *✓ This PC is ready*.

**Settings** – This PC (check and fixes, click to expand; *Server search on LAN* tests whether the firewall lets the manager's announcements through and offers *Allow in firewall*), Game start (**join automatically** – joins as soon as a server starts on the LAN and fetches missing files first; windowed, skip intro, log in automatically), Graphics (incl. optional ReShade), Audio, Mods (versions), Updates, Diagnostics, Language, Folders (incl. *Change game folder*).

**ReShade (optional)** – image filters for a sharper, more vivid look. The manager does not ship ReShade; *Set up* explains how to install it from the official ReShade website (Direct3D 9, select BF2.exe). Afterwards a switch turns it on and off. If BF2 freezes, switch it off first.

## Host

On the first switch to *Host* you set up the server once (pick mods, one Windows prompt). The top bar shows mod, mode, server status, **Join** (your own server) and **Start/Stop server**.

**Server** – while the server is stopped: build the rotation.
- Double-click a map on the left (filter by size) to add it. Drag the **≡** handle to sort, **right-click** for more (custom bot values, duplicate, remove). The icons below the rotation: up, down, remove, shuffle, undo, clear (hover for a tooltip).
- Select a map to see its preview and its **bot values**. *Custom values* gives this map its own bots (★).
- **Recent & presets**: reload a configuration you started before, or save/load named presets.

**Console and results:** type a command under *Actions* (or pick one via *Commands*); the server answers within a few seconds and the answer appears below the field. `kick <name>` and `ban <name>` work with the player name. **Change bots …** sets the bot count for the running map (the map reloads, players stay connected) – kicking single bots does not help, the AI refills them.

**Keep vehicles off:** `sv.noVehicles` only works in *Conquest*. In *Coop* the game ignores it.

While the server runs, **Server** shows the **live view**: players (kick), current and upcoming maps, next map now, restart round, message to all, console with **Commands** assistant, scheduled messages, evening leaderboard. *Edit rotation & bots* switches to the configuration – changes apply from the next map and are sent to the server automatically.

**Bots** – default bot values per map size (16/32/64), humans per team, adaptive difficulty, **bot radio messages** on/off (applies at the next server start), bot names and the mod's AI file (*Advanced*).

**Options** – every server setting. Cards *Server behaviour* (automatic restart after a crash or hang, server unlocks, LAN service) and *Default* (use current values as your default for all mods, reset to default, factory settings). While the server runs: ⚡ values apply immediately, the bar shows how many changes need a restart and offers **Restart now**.

**Fun round** (Options › *Advanced*) – for the odd party round. **Create and use** builds a small mod `<mod>_fun` next to the selected mod (its folders are links to the base mod, which stays unchanged) and selects it; then (re)start the server. Rules: C4 per player and force (C4 trampoline), blast damage to soldiers, no fall damage, mega mine and jump claymore for Spec Ops (key 5, press again to switch), rocket jump, rocket speed and count, instant rocket reload, 99 magazines for all hand weapons, sprint without stamina, gravity (60 % = high jumps, applies to everything). Players get the pack automatically when they press **Join** (a few KB). **Back to base mod** returns to the normal mod, **Remove** deletes the pack.

## When do changes apply?

| | |
|---|---|
| ⚡ immediately | message, kick, respawn time, friendly fire and other ⚡ settings |
| next map | rotation, bots, difficulty, custom map values |
| after a restart | mod, mode, max. players, port and other settings without ⚡ |

## Diagnostics

*Settings › Diagnostics*: detected problems, **Server log**, **Export diagnostics** (ZIP with versions, Windows version, mods, configuration, logs and problems – without passwords, user names and public IP addresses), **Repair**.

## Bots and coop maps

- The coop server uses **at most 32 bots** – on every map size and independent of `sv.maxPlayers`. The manager plans with this limit.
- Some mods force their own bot count in `ai\aiDefault.ai` (`overrideMenuSettings 1`, e.g. `bf2sf64`, `bfp2`). Then count, distribution and difficulty from the manager have no effect. Before the server starts the manager offers to switch this off; *Setup* does it for all mods. The *Live* page shows *Bots set/actual*.
- A map is offered for coop only if it can run there: its `info\<map>.desc` lists `gpm_coop` and it has bot data (`AIPathFinding` and `StrategicAreas.ai` for the size). If a mod lacks the coop game mode script `gpm_coop.py`, the manager creates it from the mod's `gpm_cq.py`.

## Windows on ARM (Mac with Parallels)

BF2 runs on ARM PCs in Windows' x86 emulation, but needs two adjustments. *Settings › This PC* shows a **Windows on ARM** check; **Set up** applies both (no administrator rights; close the game and the server first):

1. **Emulation setting `ARM64CHPEDISABLED`** for `BF2.exe` and `bf2_w32ded.exe` (stored per user under *Compatibility* of the exe). Without it the menu shader fails ("swiffmenu.fx … unexpected input register type"), the game crashes after the intro and the server stops after about 25 seconds.
2. **CPU patch** for `RendDX9.dll` and `RendDX9x2.dll`. They only know Intel and AMD processors; the emulated processor causes "Unknown processor family" and BF2 quits. The patch treats it like a known Intel processor (2 bytes, backup `*.vor_cpu.bak`).

## Keyboard

F5 start/stop server · Ctrl+1 … pages in sidebar order

## Where things are stored

Settings, presets, recent configurations and your default: `%APPDATA%\BF2Manager` · backups of changed game files: `Documents\BF2_Backups` · server configs: `Documents\Battlefield 2\ServerConfigs` (the current Windows *Documents* folder; if it changes, the manager copies the server configs over)
