// Hauptfenster: Server und Spiel - BF2 Manager. C# 5 (.NET Framework 4.x).
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using WinTimer = System.Windows.Forms.Timer;
using Timer = System.Windows.Forms.Timer;

namespace BF2Coop
{
    public partial class MainForm
    {
        // ================================================================== Server und Spiel
        void ToggleServer()
        {
            if (_setupMode) { SelectPage(PgExtras); Toast(T("Zuerst unter Extras › Einrichtung den Server einrichten.", "Set up the server under Extras › Setup first."), ToastKind.Info); return; }
            if (_running)
            {
                if (!DarkDialog.Confirm(this, T("Server beenden", "Stop server"), T("Den laufenden Server jetzt beenden? Alle Spieler werden getrennt.", "Stop the running server now? All players will be disconnected."), T("Beenden", "Stop"), true)) return;
                _userStopped = true;
                Srv.Kill(); _srvStart = DateTime.MinValue; RefreshLive(); Toast(T("Server beendet.", "Server stopped."), ToastKind.Info);
                return;
            }
            AutoSaveNow();
            if (!ValidateStart() || !SaveAll(true)) return;
            if (StartServerProcess()) { SaveRecent(); _crashed = false; _forceConfig = false; RefreshLive(); SelectPage(PgLive); }
        }

        // Startet bf2_w32ded.exe (auch für den Wächter)
        bool StartServerProcess()
        {
            string exe = Path.Combine(GameDir, "bf2_w32ded.exe");
            if (!File.Exists(exe)) { DarkDialog.Info(this, T("Server nicht gefunden", "Server not found"), T("bf2_w32ded.exe fehlt in:\n", "bf2_w32ded.exe missing in:\n") + GameDir); return false; }
            try { foreach (string f in new string[] { "coop_cmd.txt", "coop_live.txt" }) { string p = Path.Combine(ModSettingsDir, f); if (File.Exists(p)) File.Delete(p); } } catch { }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, Srv.Args(_mod, _coop, CfgDir));
                psi.WorkingDirectory = GameDir; psi.UseShellExecute = false;
                Process p = Process.Start(psi);
                if (_srvProc != null) _srvProc.Dispose();
                _srvProc = p; _srvExit = null;
                _srvStart = DateTime.Now; _userStopped = false; _moduleSeen = false; _hung = false;
                _startVals = new Dictionary<string, string>(_vals, StringComparer.OrdinalIgnoreCase); _sentVals.Clear(); _rotSig = RotationSig();
                Toast(T("Server startet …", "Server starting …"), ToastKind.Ok);
                return true;
            }
            catch (Exception ex) { Toast(T("Start fehlgeschlagen: ", "Start failed: ") + ex.Message, ToastKind.Error); return false; }
        }

        void StartGame(bool join)
        {
            if (!join) { StartGame(null, 0, null); return; }
            if (!_running) Toast(T("Hinweis: Der Server läuft noch nicht.", "Note: the server is not running yet."), ToastKind.Warn);
            List<string> ips = Srv.LanAddresses();
            StartGame(ips.Count > 0 ? ips[0] : "127.0.0.1", Util.ToInt(_vals["sv.serverPort"], 16567), _mod.Length > 0 ? _mod : null);
        }

        // Startet BF2; mit ip wird direkt beigetreten. Der Profilname wird als Auto-Login übergeben.
        void StartGame(string ip, int port, string mod)
        {
            string exe = Path.Combine(GameDir, "BF2.exe");
            if (!File.Exists(exe)) { DarkDialog.Info(this, T("Spiel nicht gefunden", "Game not found"), T("BF2.exe fehlt in:\n", "BF2.exe missing in:\n") + GameDir); return; }
            if (Srv.GameRunning()) { Toast(T("Battlefield 2 läuft bereits.", "Battlefield 2 is already running."), ToastKind.Info); return; }
            if (mod == null) mod = PlayMod;
            if (!Directory.Exists(Path.Combine(GameDir, "mods", mod)))
            {
                DarkDialog.Info(this, T("Mod fehlt", "Mod missing"), T("Der Mod „", "The mod “") + mod + T("“ ist auf diesem PC nicht vorhanden. Auf der Seite Spieler mit dem Server-PC abgleichen.", "” is not installed on this PC. Sync with the server PC on the Player page."));
                return;
            }
            StringBuilder a = new StringBuilder("+menu 1");
            bool windowed = _cfg.B("PlayWindowed", false);
            a.Append(" +fullscreen ").Append(windowed ? "0" : "1");
            if (windowed)
            {
                int w, h;
                if (ParseRes(Profiles.Get(VideoCon, "VideoSettings", "setResolution"), out w, out h)) a.Append(" +szx ").Append(w).Append(" +szy ").Append(h);
            }
            if (_cfg.B("SkipIntro", true)) a.Append(" +restart 1");
            a.Append(" +modPath mods/").Append(mod);
            Bf2Profile prof = PlayProfile;
            if (prof != null && !prof.Online && _cfg.B("AutoLogin", true))
            {
                // BF2 meldet über den Kontonamen (setName) an; Manager-Profile haben Konto- und Spielername gleich
                Profiles.SetDefault(prof.Id);
                a.Append(" +playerName \"").Append(prof.Name ?? prof.Nick).Append("\" +playerPassword \"\"");
            }
            if (ip != null) a.Append(" +joinServer ").Append(ip).Append(" +port ").Append(port);
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, a.ToString());
                psi.WorkingDirectory = GameDir; psi.UseShellExecute = false;
                Run(psi);
                Toast(T("Battlefield 2 startet (", "Battlefield 2 starting (") + mod + (ip != null ? " · " + ip : "") + ") …", ToastKind.Ok);
            }
            catch (Exception ex) { Toast(T("Start fehlgeschlagen: ", "Start failed: ") + ex.Message, ToastKind.Error); }
        }

        static bool ParseRes(string s, out int w, out int h)
        {
            w = h = 0;
            Match m = Regex.Match(s ?? "", @"(\d+)x(\d+)");
            if (!m.Success) return false;
            w = Util.ToInt(m.Groups[1].Value, 0); h = Util.ToInt(m.Groups[2].Value, 0);
            return w > 0 && h > 0;
        }

        void CopyIp()
        {
            List<string> ips = Srv.LanAddresses();
            if (ips.Count == 0) { Toast(T("Keine LAN-Adresse gefunden.", "No LAN address found."), ToastKind.Warn); return; }
            string s = ips[0] + ":" + Util.ToInt(_vals["sv.serverPort"], 16567);
            try { Clipboard.SetText(s); Toast(T("Adresse kopiert: ", "Address copied: ") + s, ToastKind.Ok); } catch { Toast(s, ToastKind.Info); }
        }

        void RefreshLive()
        {
            bool run = Srv.IsRunning();
            Dictionary<string, string> live = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string[]> players = new List<string[]>();
            if (_mod.Length > 0)
                foreach (string l in Util.ReadLines(Path.Combine(ModSettingsDir, "coop_live.txt")))
                {
                    int i = l.IndexOf('='); if (i <= 0) continue;
                    string k = l.Substring(0, i).Trim(), v = l.Substring(i + 1).Trim();
                    if (k == "player") { string[] p = v.Split('|'); if (p.Length >= 7) players.Add(p); }
                    else live[k] = v;
                }
            long upd = 0; string us; if (live.TryGetValue("updated", out us)) long.TryParse(us, out upd);
            double age = upd > 0 ? (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds - upd : 9999;
            _module = run && age < 25;
            SrvState st = !run ? SrvState.Offline : (_module ? SrvState.Running : ((DateTime.Now - _srvStart).TotalSeconds < 75 ? SrvState.Starting : SrvState.NoModule));
            string txt = st == SrvState.Offline ? "Offline" : (st == SrvState.Running ? "Online" : (st == SrvState.Starting ? T("Startet …", "Starting …") : T("Online · ohne Modul", "Online · no module")));
            _pill.Set(st, txt);
            List<string> ips = Srv.LanAddresses();
            string port = Util.ToInt(_vals["sv.serverPort"], 16567).ToString();
            StringBuilder tip = new StringBuilder(txt + "\n");
            if (ips.Count == 0) tip.Append(T("Keine LAN-Adresse gefunden.", "No LAN address found."));
            else { tip.Append(T("Beitreten über Join Local oder per IP:\n", "Join via Join Local or by IP:\n")); foreach (string ip in ips) tip.Append(ip + ":" + port + "\n"); }
            tip.Append(T("\nKlicken kopiert die Adresse.", "\nClick to copy the address."));
            if (st == SrvState.NoModule) tip.Append(T("\nBot-Modul meldet sich nicht: Extras › Einrichtung prüfen und Server neu starten.", "\nBot module not responding: check Extras › Setup and restart the server."));
            Theme.Tip.SetToolTip(_pill, tip.ToString());
            bool crashed = _running && !run && !_userStopped && _srvStart != DateTime.MinValue;
            if (run != _running)
            {
                _running = run;
                _bServer.Text = run ? T("Server stoppen", "Stop server") : T("Server starten", "Start server");
                _bServer.Glyph = run ? G.Stop : G.Play; _bServer.Kind = run ? BtnKind.Danger : BtnKind.Primary;
                LayoutHeader();
                UpdateLiveBanner();
                if (_page == PgLive) LayoutLive();
                if (!run && _page == PgLive) SelectPage(PgMaps);
                if (!run) _startVals = null;
            }
            string lm; if (live.TryGetValue("map", out lm) && _module) _liveMap = lm;
            if (crashed) Watchdog();
            CheckHang(run);
            LanTick(live);
            MessagesTick();
            string nu;
            if (_nextToggle.Checked && _nextId.Length > 0 && live.TryGetValue("nextUsed", out nu) && nu == _nextId)
            {
                bool old = _applying; _applying = true; _nextToggle.SetSilent(false); _applying = old;
                WriteNext(); LayoutLive();
                Toast(T("Einmalige Botwerte wurden angewendet.", "One-time bot values were applied."), ToastKind.Info);
            }
            string nu2; if (live.TryGetValue("nextUsed", out nu2)) BotsNowDone(nu2);
            if (_page != PgLive) return;
            RefreshLeaderboard();
            string map; live.TryGetValue("map", out map);
            _tiles[0].Set(st == SrvState.Running || st == SrvState.NoModule ? "Online" : (st == SrvState.Starting ? T("Startet", "Starting") : "Offline"),
                st == SrvState.Running ? T("Bot-Modul aktiv", "bot module active") : (st == SrvState.NoModule ? T("Bot-Modul meldet sich nicht", "bot module not responding") : (run ? T("wird geladen", "loading") : "")), _pill.DotColor);
            string hu, hm, bu, bm, bw, mapSub = _module ? T("aktuelle Map", "current map") : "";
            if (_module && live.TryGetValue("humansUS", out hu) && live.TryGetValue("humansMEC", out hm) && live.TryGetValue("botsUS", out bu) && live.TryGetValue("botsMEC", out bm))
            {
                _tiles[2].Set((Util.ToInt(hu, 0) + Util.ToInt(bu, 0)).ToString(), hu + T(" Menschen · ", " humans · ") + bu + " Bots", Theme.Us);
                _tiles[3].Set((Util.ToInt(hm, 0) + Util.ToInt(bm, 0)).ToString(), hm + T(" Menschen · ", " humans · ") + bm + " Bots", Theme.Mec);
                int want = live.TryGetValue("botsWanted", out bw) ? Util.ToInt(bw, -1) : -1;
                if (want >= 0)
                {
                    int have = Util.ToInt(bu, 0) + Util.ToInt(bm, 0);
                    want = Math.Min(want, Planner.EngineMaxBots);
                    mapSub += " · Bots " + have + "/" + want;
                    CheckBotMismatch(map, have, want);
                }
            }
            else { _tiles[2].Set("–", "", Theme.Us); _tiles[3].Set("–", "", Theme.Mec); }
            _tiles[1].Set(_module && !string.IsNullOrEmpty(map) ? Util.Pretty(map) : "–", mapSub, Theme.Accent);
            if (_running) { UpdatePlayers(players); RefreshUpcoming(); }
            if (_mod.Length > 0)
            {
                string[] lines = _tail.ReadIfChanged(Path.Combine(ModSettingsDir, "coop_status.txt"), 250);
                if (lines != null) { SetLog(lines); CheckResults(lines); }
            }
        }

        // Spielen deutlich mehr oder weniger Bots als eingestellt? Nach ~20 s (Bots erscheinen nach und nach) einmal pro Map erklären.
        int _botMismatch; string _botWarnMap;
        void CheckBotMismatch(string map, int have, int want)
        {
            if (Math.Abs(have - want) <= 1) { _botMismatch = 0; return; }
            if (++_botMismatch < 4 || _botWarnMap == map) return;
            _botWarnMap = map;
            string msg = T("Es spielen ", "There are ") + have + T(" Bots statt ", " bots instead of ") + want + ". ";
            if (AiFile.Load(GameDir, _mod).OverridesManager)
                msg += T("Der Mod gibt die Bots in aiDefault.ai fest vor – beim nächsten Serverstart bietet der Manager an, das zu beheben.", "The mod forces its own bots in aiDefault.ai – at the next server start the manager offers to fix it.");
            else
                msg += T("Möglicherweise lädt die Map nicht für alle Bots Wegpunkte, oder der Mod begrenzt die Bots.", "The map may not provide paths for all bots, or the mod limits them.");
            Util.Log(AppPaths.Dir, "Bot mismatch on " + map + ": " + have + " instead of " + want);
            Toast(msg, ToastKind.Warn);
        }

        void SetLog(string[] lines)
        {
            StringBuilder sb = new StringBuilder(@"{\rtf1\ansi\deff0{\fonttbl{\f0 " + Theme.Mono.Name + @";}}{\colortbl ;");
            foreach (Color c in new Color[] { Theme.Text, Theme.Dim, Theme.Red, Theme.Accent, Theme.Us, Theme.Green })
                sb.Append(@"\red" + c.R + @"\green" + c.G + @"\blue" + c.B + ";");
            sb.Append(@"}\f0\fs" + (int)(Theme.Mono.SizeInPoints * 2) + " ");
            if (lines.Length == 0) sb.Append(@"\cf2 " + Esc(T("Noch keine Einträge. Das Log füllt sich, sobald der Server mit Bot-Modul läuft.", "No entries yet. The log fills once the server runs with the bot module.")) + @"\par ");
            foreach (string l in lines)
            {
                string low = l.ToLowerInvariant();
                int c = 1;
                if (low.Contains("error") || low.Contains("rejected") || low.Contains("fehler") || low.Contains("abgelehnt")) c = 3;
                else if (low.Contains("command") || low.Contains("kommando")) c = 5;
                else if (low.Contains("next map") || low.Contains("naechste map") || low.Contains("commander")) c = 4;
                else if (low.Contains("started") || low.Contains("gestartet")) c = 6;
                string ts = l.Length > 20 && l[4] == '-' ? l.Substring(11, 8) : null;
                if (ts != null) sb.Append(@"\cf2 " + ts + "  ");
                sb.Append(@"\cf" + c + " " + Esc(ts != null ? l.Substring(20) : l) + @"\par ");
            }
            sb.Append("}");
            _log.Rtf = sb.ToString();
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        static string Esc(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (ch == '\\' || ch == '{' || ch == '}') sb.Append('\\').Append(ch);
                else if (ch > 127) sb.Append(@"\u" + (int)ch + "?");
                else sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
