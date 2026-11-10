# Preparing a LAN party

Goal: **one folder** that you copy to every PC. Nobody installs anything, nobody edits files.

> Every player needs their own legal copy of Battlefield 2. The manager does not include or download game files.

## 1. Build the folder (host, once)

```
Battlefield2\                    ← LAN folder, e.g. C:\Games\Battlefield2
  BF2Manager.exe                ← the manager (from the project
                                   – finds the game folder next to it by itself
  Battlefield 2\                ← your game folder
    BF2.exe
    bf2_w32ded.exe
    mods\                       ← all mods you want to play (bf2all64, xpack, …)
    _LAN\
      DirectX\                  ← optional but recommended (see below)
      ReShade\                  ← optional
```

Tips:
- Do **not** place the folder in `C:\Program Files` on the other PCs – `C:\Games\Battlefield 2` avoids permission problems.
- Start the manager on your PC first and click **Fix** in the health bar (Settings › This PC). The 4 GB patch and the unlock patch are written into `BF2.exe`, so copies of your folder already contain them.
- Set up the server once: **Host › one-time setup**.

### DirectX (for PCs without the old DirectX 9 files)

Fresh Windows installations often miss `d3dx9_xx.dll`. Microsoft allows passing on the official package:

1. Get the *DirectX End-User Runtimes (June 2010)* from Microsoft
2. Extract it into the folder: `directx_Jun2010_redist.exe /Q /T:"C:\Games\Battlefield 2\_LAN\DirectX"`
3. The health bar (**Fix**) now installs DirectX on PCs that need it (silent, one Windows prompt).

### ReShade (optional)

Install ReShade once into your game folder (from the official ReShade website; select `BF2.exe`, Direct3D 9) – *Settings › Graphics › ReShade › Set up* shows the steps. To hand it on, put the working setup (the `d3d9.dll`, `ReShade.ini`, preset and shader folders) into `_LAN\ReShade`. Players can switch it on and off under **Settings › Graphics › ReShade**. It only affects their own screen. If BF2 freezes, switch ReShade off first.

## 2. Copy the folder to every PC

USB stick, network share – whatever is fastest. Changed something later? Players just click **Join** – the manager fetches the differences from your PC first.

## 3. On every player PC

1. Start `BF2Manager.exe` (SmartScreen: *More info › Run anyway*).
2. The welcome window asks for the game folder and your player name – the manager creates the profile. Click **Fix** if the bar at the top is yellow (one Windows prompt).
3. When the host starts the server it appears on the **Play** page → **Join**.

## 4. On the server PC

1. Switch to **Host** (top left) and complete the one-time setup.
2. Choose mod and mode at the top, build the rotation on **Server**, adjust **Bots**, click **Start server**.
3. The live view opens: players, upcoming maps, messages, leaderboard. Crashes and hangs restart automatically.
4. Want to play on the same PC? Click **Join** in the top bar.

## Checklist before the party

- [ ] Every PC runs the same manager version (bottom left)
- [ ] Health bar is green on every PC
- [ ] Every player has a unique player name
- [ ] Test round with two PCs: server visible, join works, unlock weapons available
- [ ] Mods compared with the server PC (no red differences)
- [ ] Wired network if possible; server PC with a strong CPU (bots need one fast core – about 64 bots is the realistic limit)

## Troubleshooting

| Problem | Solution |
|---|---|
| Server not listed | **Fix** on **both** PCs (firewall) – *Settings › This PC › Server search on LAN* shows whether announcements get through. Moved the exe? Let the manager copy itself into the game folder. Same network? VPN LAN: *Server not found?* and enter the IP. |
| "Modified content" or kicked after joining | Play › Join (syncs the files first) |
| Join stops at the login screen | Settings › Game start › Log in automatically on; offline profile selected |
| Game freezes when joining | Play › *Game stuck? Close it*, then Play › Profile › **Repair profile**; ReShade off; check that the server still runs |
| Game does not start / missing d3dx9 file | **Fix** (DirectX) |
| Crash when loading big maps | **Fix** (4 GB memory patch) |
| No unlock weapons | Host: Options › Server behaviour › all unlocks. Players: **Fix** (client unlock) |
| Bots do not change per map | Settings › Mods: mod must be *ready*; restart the server |
| Server crashed | The watchdog restarts it automatically; check the log on the Live page |

Still stuck? **Settings › Diagnostics › Export diagnostics** and open an issue on GitHub with the ZIP attached.

## Network ports

| Port | Used by |
|---|---|
| UDP 16567 | game server (default, adjustable) |
| UDP 29900 | GameSpy query (server list in game) |
| UDP 29960 | manager: server finder |
| TCP 29961 | manager: file compare/sync and manager updates (read-only) |
