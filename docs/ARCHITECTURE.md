# Architecture

## Overview

```
BF2Manager.exe  ──writes──▶  coop_server.txt, serverSettings.con, maplist.con
        │                                   │
        │ starts                            │ read at every map change
        ▼                                   ▼
 bf2_w32ded.exe  ──loads──▶  python/game/scoringCommon.py ──imports──▶ coopbots.py
                                                                     │
        ◀──reads── coop_live.txt, coop_status.txt, coop_rounds.txt ──┘
        ──writes──▶ coop_cmd.txt (live commands)  ──polled every 2 s──▶
```

The manager never talks to the game server over the network. Everything goes through small text files in `mods/<mod>/settings/`, which the server module `coopbots.py` reads and writes. No open admin port, no password.

| File | Written by | Purpose |
|---|---|---|
| `coop_server.txt` | manager | bot values per size and map, humans per team, commander, adaptive difficulty, one-time values |
| `coop_cmd.txt` | manager | live commands with increasing IDs; only `sv.*`, `mapList.*`, `admin.*`, `game.say*`, `@nextmap`, `@commander` are executed |
| `coop_live.txt` | module | heartbeat, map, team sizes, human players, module version, bot count the map was loaded with (`botsWanted`) |
| `coop_status.txt` | module | log shown on the Live page |
| `coop_rounds.txt` | module | one block per finished round for the leaderboard |

`coopbots.py` must stay **Python 2.3 compatible** (BF2's embedded Python): no `sorted()`, no generator expressions, no `with`, no conditional expressions, no `except X as e`.

## Source layout

```
src/
  App/        MainForm (partial class, one file per page), Program, Config
  UI/         Theme, controls (cards, switches, lists, dialogs), widgets, command dialog
  Model/      settings table (bilingual), bot planning and coop_server.txt, leaderboard, command catalog
  Game/       game folder and versions, mods and profiles, .con files, map scanner, patches (unlock, 4 GB, DirectX), Windows on ARM
  Server/     server process, exit reasons and live channel, coopbots.py (embedded)
  Services/   installer (incl. gpm_coop.py), admin tasks (UAC), firewall, PC check, updater and release notes, LAN service, diagnostics
tests/        console test runner
build/        icon, manifest, resource script, Linux build script
```

## Key design decisions

- **Single exe, no dependencies.** Personal data in `%APPDATA%\BF2Manager`; shared LAN extras in `<game>\_LAN`.
- **Portable mode.** If `BF2.exe` is next to the manager, that folder is the game folder.
- **Admin rights only when needed.** Tasks that need them (setup, patches in Program Files, firewall, DirectX, update swap) are collected into one job and run by a second instance of the exe via `runas` (`--task-job`). One Windows prompt for several fixes.
- **Bilingual.** Every UI string is `T("Deutsch", "English")`; the settings table has both languages per row.
- **Own scrolling.** `ScrollHost` replaces WinForms AutoScroll; the offset is clamped to the content height on every change.
- **Never trust `Control.Visible` as state.** It returns false while the page is hidden. Layout code decides visibility from its own state.
- **Absolute config paths for the server.** `+config`/`+mapList` get the real path; `@HOME@` is resolved by BF2 itself and can point to another *Documents* folder than the manager's.
- **Patches that survive relocation.** `RendDX9.dll` is loaded at a different base address; patched bytes must not overlap a relocation entry. The CPU patch therefore only changes a relative jump (`jnz` → `jmp`).

## Updater

1. `GET api.github.com/repos/AnselmJo/bf2-manager/releases` (TLS 1.2, 5 s timeout, silent without internet).
2. Newest non-draft release newer than `AppInfo.Version` (pre-releases optional) with assets `BF2Manager.exe` and `SHA256SUMS.txt`.
3. Download next to the exe as `.new`, verify SHA256, rename the running exe to `.old`, move the new file in place (elevated if the folder is not writable), start the new exe with `--updated <old version>` and exit. The new instance waits for the single-instance mutex, shows the release notes and deletes leftovers. *Previous version* swaps `.old` back.
4. After an update an outdated bot module is replaced silently if the mod folder is writable.

## LAN service

- **Beacon** (server PC): UDP broadcast to port 29960 every 3 s: `BF2SM1|name|gamePort|mod|map|humans|max|httpPort|version|mode|running`.
- **Listener** (every PC): collects beacons, entries expire after 12 s.
- **File service** (server PC, TCP 29961, minimal HTTP/1.0, read-only): `/info`, `/manifest?mod=`, `/file?mod=&p=`, `/manager`, `/manager.sha256`. Only files inside `mods/<mod>`; server-only folders (`settings`, `python`, `ai`) and logs are excluded; path traversal is rejected.
- **Manifest:** relative path, size, SHA1. Hashes are cached per file (size + modification time) in `%APPDATA%\BF2Manager\cache`.
- **Sync:** each file is downloaded to `.part`, verified, then replaces the local file. Extra local files are never deleted.
- **Fingerprint:** the beacon carries a 16-hex checksum over the manifest (`ModPrints`, computed in the background; for a fun pack plus its base mod). *Join* starts directly when it matches; otherwise `ModSync.Run` syncs (base mod first for fun packs, then `FunMods.Link` creates the junctions) and starts the game. Linked folders are skipped when building a manifest.
- **Fun pack** (`Services/FunPack.cs`, UI `MainForm.Fun.cs`): reads the base mod's `serverarchives.con`, takes each file from the first mounted archive that has it (that is what the game sees), patches values per template (`SetInTemplate`) and the material table, writes `fun_objects.zip`/`fun_common.zip`, mounts them first in both archive lists and writes `fun.recipe`. Gravity goes into `GameLogicInit.con` so server and players simulate the same. Kit changes go through a key plan (`ItemSlots`, `BestKey`): two items with the same `itemIndex` in one kit crash the server, so extras only move to keys free in every affected kit. Presets (`MainForm.Presets.cs`, `%APPDATA%\BF2Manager\presets\*.json`) are snapshots with optional `Maps`/`Mod`/`Type` and `Fun`. New weapon templates are not used: BF2 does not resolve templates from new files at kit load (assert in `kit.cpp`), so extra explosives reuse existing ones.
