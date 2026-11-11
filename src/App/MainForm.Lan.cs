// Hauptfenster: LAN-Dienst, Server-Finder, Abgleich und Sync - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BF2Coop
{
    public partial class MainForm
    {
        LanListener _lanListen; LanFileServer _lanFiles; DateTime _lastBeacon = DateTime.MinValue;
        List<LanHost> _fakeHosts;
        LanHost _syncHost; SyncPlan _syncPlan; volatile bool _syncCancel; bool _syncBusy;

        bool LanHostOn { get { return _cfg.B("LanHost", !_setupMode); } }

        void StartLan()
        {
            if (_shotDir != null) return;
            _lanListen = new LanListener(); _lanListen.OwnId = InstanceId;
            _lanListen.Start();
            ApplyLanHost();
            RefreshLanInfo();
        }

        void StopLan()
        {
            if (_lanListen != null) { _lanListen.Dispose(); _lanListen = null; }
            if (_lanFiles != null) { _lanFiles.Dispose(); _lanFiles = null; }
        }

        void ApplyLanHost()
        {
            bool on = LanHostOn && GameLocator.IsGameDir(GameDir) && _shotDir == null;
            if (on && _lanFiles == null) { _lanFiles = new LanFileServer(GameDir); _lanFiles.Start(); }
            else if (!on && _lanFiles != null) { _lanFiles.Dispose(); _lanFiles = null; }
        }

        // Alle 2 s aus RefreshLive: Bekanntmachung senden und Serverliste aktualisieren
        string InstanceId
        {
            get
            {
                string id = _cfg.S("InstanceId", null);
                if (id == null) { id = Guid.NewGuid().ToString("N").Substring(0, 12); _cfg.Set("InstanceId", id); _cfg.Save(); }
                return id;
            }
        }

        Dictionary<string, string> _lastLive = new Dictionary<string, string>();

        // Der eigene Server, wie ihn andere PCs über die Bekanntmachung sehen
        LanHost OwnHost(Dictionary<string, string> live)
        {
            string map, hu, hm, bu, bm; live.TryGetValue("map", out map);
            LanHost h = new LanHost();
            h.Name = _vals.ContainsKey("sv.serverName") ? _vals["sv.serverName"] : "BF2";
            h.GamePort = Util.ToInt(_vals["sv.serverPort"], 16567); h.Mod = _running && _runMod != null ? _runMod : (_mod.Length > 0 ? _mod : PlayMod); h.Map = map ?? "";
            h.Humans = live.TryGetValue("humansUS", out hu) && live.TryGetValue("humansMEC", out hm) ? Util.ToInt(hu, 0) + Util.ToInt(hm, 0) : 0;
            h.Bots = live.TryGetValue("botsUS", out bu) && live.TryGetValue("botsMEC", out bm) ? Util.ToInt(bu, 0) + Util.ToInt(bm, 0) : 0;
            h.Max = Util.ToInt(_vals["sv.maxPlayers"], 64); h.Mode = _coop ? "coop" : "cq"; h.Running = _running;
            foreach (MapEntry me in _st.Maps) if (string.Equals(me.Name, h.Map, StringComparison.OrdinalIgnoreCase)) { h.Size = me.Size; break; }
            h.Id = InstanceId; h.HttpPort = Lan.HttpPort; h.Version = AppInfo.Version; h.ModVersion = ModInfo.Read(GameDir, h.Mod).Version ?? "";
            h.Fp = _lanFiles != null && _lanFiles.Error == null ? ModPrints.Get(GameDir, h.Mod) ?? "" : "";
            List<string> ips = Srv.LanAddresses(); h.Ip = ips.Count > 0 ? ips[0] : "127.0.0.1"; h.Self = true; h.Seen = DateTime.Now;
            return h;
        }

        // Alle 2 s aus RefreshLive: Bekanntmachung senden und Serverliste aktualisieren
        void LanTick(Dictionary<string, string> live)
        {
            _lastLive = live;
            if ((DateTime.Now - _lastBeacon).TotalSeconds >= 3)
            {
                if (_lanFiles != null && _lanFiles.Error == null) SendBeacon(live);
                else if (_lanListen != null && _lanListen.Error == null) SendProbe();
            }
            if (_page == PgPlayer && !_syncBusy) RefreshServers(false);
            HostPrintTick();
            AutoJoinTick();
            try { AnnounceTick(); FlowTick(); HearTick(); FollowTick(); } catch (Exception ex) { Util.Log(AppPaths.Dir, ex.ToString()); }
            // Ändert sich, ob die eigene Bekanntmachung ankommt, den PC-Check neu laufen lassen
            bool? e = EchoState;
            if (e != null && e != _echoShown) RefreshChecks(true);
        }

        DateTime _firstBeacon = DateTime.MinValue; bool? _echoShown;

        // null = (noch) nicht prüfbar; true = eigene Bekanntmachung kommt zurück; false = kommt seit über 10 s nicht an
        bool? EchoState
        {
            get
            {
                if (_lanListen == null || _lanListen.Error != null || _firstBeacon == DateTime.MinValue || Srv.LanAddresses().Count == 0) return null;
                if ((DateTime.Now - _lanListen.SelfSeen).TotalSeconds < 15) return true;
                return (DateTime.Now - _firstBeacon).TotalSeconds > 10 ? (bool?)false : null;
            }
        }

        void SendProbe()
        {
            _lastBeacon = DateTime.Now;
            if (_firstBeacon == DateTime.MinValue) _firstBeacon = DateTime.Now;
            string payload = Lan.ProbeMagic + InstanceId;
            ThreadPool.QueueUserWorkItem(delegate { try { Lan.Announce(payload); } catch { } });
        }

        void SendBeacon(Dictionary<string, string> live)
        {
            _lastBeacon = DateTime.Now;
            if (_firstBeacon == DateTime.MinValue) _firstBeacon = DateTime.Now;
            LanHost h = OwnHost(live);
            string payload = Lan.Payload(h.Name, h.GamePort, h.Mod, h.Map, h.Humans, h.Max, h.Mode, h.Running)
                + "|" + h.Id + "|" + h.Size + "|" + h.Bots + "|" + Lan.Esc(h.ModVersion) + "|" + Lan.Esc(h.Fp)
                + "|" + Lan.Esc(_annId) + "|" + Lan.Esc(_annText) + "|" + Lan.Esc(_nextModAnn);
            ThreadPool.QueueUserWorkItem(delegate { try { Lan.Announce(payload); } catch { } });
        }

        // „Aktualisieren“: Liste leeren, sofort neu bekanntmachen; Antworten kommen innerhalb weniger Sekunden
        void RescanServers()
        {
            if (_lanListen != null) _lanListen.Clear();
            if (_lanFiles != null && _lanFiles.Error == null) SendBeacon(_lastLive);
            RefreshServers(true);
            Toast(_lanListen != null && _lanListen.Error != null
                ? T("Serversuche nicht möglich: ", "Server search not possible: ") + _lanListen.Error
                : T("Suche läuft – laufende Server erscheinen in wenigen Sekunden.", "Searching – running servers appear within a few seconds."), ToastKind.Info);
        }

        void SetSync(string text) { _syncText.Text = text; if (_page == PgPlayer) _plHost.Masonry(_plCards, 2); }

        // Lokale Mod-Dateien mit dem Server-PC vergleichen
        void CompareWith(LanHost h)
        {
            if (_syncBusy) return;
            _syncBusy = true; _syncCancel = false; _syncHost = h; _syncPlan = null;
            string mod = h.Mod, root = Path.Combine(GameDir, "mods", h.Mod);
            SetSync(T("Hole Dateiliste vom Server-PC … (beim ersten Mal kann das dauern, weil der Server-PC alle Dateien prüft)", "Fetching file list from the server PC … (the first time can take a while because the server PC checks all files)"));
            ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null; SyncPlan plan = null;
                try
                {
                    List<FileEntry> remote = Lan.ParseManifest(ModSync.GetManifest(h, mod, "", delegate (string s) { try { BeginInvoke((MethodInvoker)delegate { SetSync(s); }); } catch { } }, delegate { return _syncCancel; }));
                    int last = -1;
                    List<FileEntry> local = Lan.BuildManifest(root, delegate (int i, int n)
                    {
                        int pct = n > 0 ? i * 100 / n : 0;
                        if (pct / 5 != last / 5) { last = pct; try { BeginInvoke((MethodInvoker)delegate { SetSync(T("Prüfe eigene Dateien … ", "Checking own files … ") + pct + " %"); }); } catch { } }
                    }, delegate { return _syncCancel; });
                    plan = Lan.Diff(local, remote);
                }
                catch (OperationCanceledException) { err = T("Abgebrochen.", "Cancelled."); }
                catch (Exception ex) { err = ex.Message; }
                try { BeginInvoke((MethodInvoker)delegate { CompareDone(h, plan, err); }); } catch { }
            });
            if (_page == PgPlayer) _plHost.Masonry(_plCards, 2);
        }

        void CompareDone(LanHost h, SyncPlan plan, string err)
        {
            _syncBusy = false; _syncPlan = plan;
            if (err != null) { SetSync("⚠ " + T("Abgleich nicht möglich: ", "Compare failed: ") + err + T("\nFirewall auf dem Server-PC prüfen (PC-Check).", "\nCheck the firewall on the server PC (PC check).")); return; }
            if (plan.Count == 0)
            {
                SetSync("✓ " + T("Alle Dateien von „", "All files of “") + h.Mod + T("“ stimmen mit dem Server-PC überein.", "” match the server PC.") +
                    (plan.Extra.Count > 0 ? "\n" + plan.Extra.Count + T(" zusätzliche Dateien nur auf diesem PC (stören nicht).", " extra files only on this PC (harmless).") : ""));
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("⚠ ").Append(plan.Count).Append(plan.Count == 1 ? T(" Datei weicht ab oder fehlt (", " file differs or is missing (") : T(" Dateien weichen ab oder fehlen (", " files differ or are missing (")).Append(SizeText(plan.Bytes)).Append(")");
            if (plan.Missing.Count > 0) sb.Append(T(" · fehlend: ", " · missing: ")).Append(plan.Missing.Count);
            if (plan.Different.Count > 0) sb.Append(T(" · anders: ", " · different: ")).Append(plan.Different.Count);
            List<FileEntry> all = new List<FileEntry>(plan.Missing); all.AddRange(plan.Different);
            for (int i = 0; i < all.Count && i < 6; i++) sb.Append("\n   ").Append(all[i].Rel);
            if (all.Count > 6) sb.Append("\n   …");
            if (plan.Extra.Count > 0) sb.Append("\n").Append(plan.Extra.Count).Append(T(" zusätzliche Dateien nur auf diesem PC bleiben unverändert.", " extra files only on this PC stay unchanged."));
            SetSync(sb.ToString());
        }

        static string SizeText(long b) { return ModSync.SizeText(b); }

        // Mod, den dieser PC im LAN anbietet
        string HostedMod { get { return _mod.Length > 0 ? _mod : PlayMod ?? ""; } }

        // ---- Host: Fortschritt der einmaligen Prüfsummen-Berechnung (bei großen Mods einige Minuten)
        bool _hostPrintShown; string _hostPrintMod;
        void HostPrintTick()
        {
            if (_lanFiles == null || _lanFiles.Error != null || _shotDir != null) return;
            string mod = HostedMod; if (mod.Length == 0) return;
            ModPrints.Get(GameDir, mod);   // stößt die Berechnung an, falls nötig
            int pct; double secs;
            bool busy = ModPrints.Busy(GameDir, mod, out pct, out secs);
            if (busy && secs > 4 && (!_hostPrintShown || _hostPrintMod != mod))
            {
                _hostPrintShown = true; _hostPrintMod = mod;
                Toast(T("Bereite den Abgleich für Mitspieler vor: prüfe einmalig alle Dateien von „", "Preparing sync for players: checking all files of “") + mod + T("“ … Der Fortschritt steht unter Optionen › Server-Verhalten. Beitreten geht schon.", "” once … Progress is shown under Options › Server behaviour. Joining already works."), ToastKind.Info);
            }
            else if (!busy && _hostPrintShown && _hostPrintMod == mod)
            {
                _hostPrintShown = false;
                Toast(T("Abgleich bereit: Mitspieler sehen jetzt, ob ihr „", "Sync ready: players now see whether their “") + mod + T("“ dem Server entspricht.", "” matches the server."), ToastKind.Ok);
            }
            if (busy || _lanInfoBusy) { _lanInfoBusy = busy; RefreshLanInfo(); }
        }
        bool _lanInfoBusy;

        // ---- Spieler: automatisch beitreten, sobald genau ein Server im LAN startet
        readonly HashSet<string> _autoSeen = new HashSet<string>();
        bool AutoJoinOn { get { return _cfg.B("AutoJoin", false); } }

        void AutoJoinTick()
        {
            if (_lanListen == null || _fakeHosts != null) return;
            List<LanHost> running = new List<LanHost>();
            foreach (LanHost h in _lanListen.Hosts()) if (h.Running && !h.Self) running.Add(h);
            // Server, die nicht mehr laufen, vergessen – startet er neu, wird wieder beigetreten
            HashSet<string> now = new HashSet<string>(); foreach (LanHost h in running) now.Add(h.Key);
            _autoSeen.RemoveWhere(delegate (string k) { return !now.Contains(k); });
            if (!AutoJoinOn || _host || _running || _syncBusy || running.Count != 1 || Srv.GameRunning() || !GameLocator.IsGameDir(GameDir)) { foreach (string k in now) if (!AutoJoinOn) _autoSeen.Add(k); return; }
            LanHost s = running[0];
            if (_autoSeen.Contains(s.Key)) return;
            _autoSeen.Add(s.Key);
            Toast(T("Server „", "Server “") + s.Name + T("“ gestartet – trete automatisch bei …", "” started – joining automatically …"), ToastKind.Ok);
            FlushPlayer();
            JoinHost(s, true);
        }

        // „Beitreten“: passt der Mod, startet das Spiel sofort; sonst erst fehlende oder abweichende Dateien holen, dann starten
        void JoinHost(LanHost h) { JoinHost(h, false); }

        // auto = automatisches Beitreten: ohne Rückfrage abgleichen (der Spieler hat es so eingestellt)
        void JoinHost(LanHost h, bool auto)
        {
            if (Srv.GameRunning()) { Toast(T("Battlefield 2 läuft bereits.", "Battlefield 2 is already running."), ToastKind.Info); return; }
            bool ok; Compat(h, out ok);
            if (ok || h.Self) { StartGame(h.Ip, h.GamePort, h.Mod); return; }
            if (_syncBusy) { Toast(T("Ein Abgleich läuft schon.", "A sync is already running."), ToastKind.Info); return; }
            bool missing = !Directory.Exists(Path.Combine(GameDir, "mods", h.Mod));
            if (_running && string.Equals(_mod, h.Mod, StringComparison.OrdinalIgnoreCase)) { Toast(T("Bitte zuerst den eigenen Server beenden.", "Please stop your own server first."), ToastKind.Warn); return; }
            if (!Installer.CanWrite(Path.Combine(GameDir, "mods"))) { Toast(T("Keine Schreibrechte im Ordner mods – PC-Check › Schreibrechte.", "No write access to the mods folder – PC check › write access."), ToastKind.Error); return; }
            string msg = (missing ? T("Der Mod „", "The mod “") + h.Mod + T("“ fehlt auf diesem PC.", "” is missing on this PC.")
                                  : T("Der Mod „", "The mod “") + h.Mod + T("“ unterscheidet sich vom Server-PC.", "” differs from the server PC."))
                + T("\n\nDer Manager holt jetzt nur die fehlenden oder abweichenden Dateien vom Server-PC „", "\n\nThe manager now fetches only the missing or different files from the server PC “") + h.Name
                + T("“ und startet danach das Spiel. Beim ersten Mal kann das ein paar Minuten dauern.\nAbweichende Dateien werden ersetzt, zusätzliche eigene Dateien bleiben.", "” and then starts the game. The first time this can take a few minutes.\nDifferent files are replaced, extra files of your own stay.");
            if (!auto && !DarkDialog.Confirm(this, T("Abgleichen und beitreten", "Sync and join"), msg, T("Abgleichen und beitreten", "Sync and join"), false)) return;
            _syncBusy = true; _syncCancel = false; _syncPlan = null; _syncHost = h;
            SetSync(T("Verbinde mit dem Server-PC …", "Connecting to the server PC …"));
            ThreadPool.QueueUserWorkItem(delegate
            {
                ModSync.Result res = null; string err = null;
                try
                {
                    res = ModSync.Run(h, GameDir, delegate (string s) { try { BeginInvoke((MethodInvoker)delegate { SetSync(s); }); } catch { } }, delegate { return _syncCancel; });
                }
                catch (OperationCanceledException) { err = T("Abgebrochen.", "Cancelled."); }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _syncBusy = false;
                        if (err != null) { SetSync("⚠ " + T("Abgleich nicht möglich: ", "Sync failed: ") + err + T("\nFirewall auf dem Server-PC prüfen (PC-Check) und erneut „Beitreten“.", "\nCheck the firewall on the server PC (PC check) and press “Join” again.")); return; }
                        SetSync("✓ " + (res.Files == 0 ? T("Alles war schon gleich.", "Everything already matched.") : res.Files + T(" Dateien geholt (", " files fetched (") + SizeText(res.Bytes) + ")")
                            + (res.Links > 0 ? T(" · Spaß-Aufsatz eingerichtet", " · fun pack set up") : "") + T(" – Spiel startet …", " – starting the game …"));
                        RefreshStatus(); RefreshServers(true);
                        StartGame(h.Ip, h.GamePort, h.Mod);
                    });
                }
                catch { }
            });
            if (_page == PgPlayer) _plHost.Masonry(_plCards, 2);
        }

        // Abweichende und fehlende Dateien vom Server-PC holen (jede Datei geprüft, erst danach ersetzt)
        void SyncFromHost()
        {
            if (_syncBusy || _syncPlan == null || _syncHost == null || _syncPlan.Count == 0) return;
            if (Srv.GameRunning()) { Toast(T("Bitte zuerst Battlefield 2 beenden.", "Please close Battlefield 2 first."), ToastKind.Warn); return; }
            if (_running && string.Equals(_mod, _syncHost.Mod, StringComparison.OrdinalIgnoreCase)) { Toast(T("Bitte zuerst den eigenen Server beenden.", "Please stop your own server first."), ToastKind.Warn); return; }
            if (!DarkDialog.Confirm(this, T("Dateien holen", "Fetch files"), _syncPlan.Count + T(" Dateien (", " files (") + SizeText(_syncPlan.Bytes) + T(") vom Server-PC „", ") from the server PC “") + _syncHost.Name + T("“ in den Mod „", "” into the mod “") + _syncHost.Mod + T("“ übernehmen?\nVorhandene Dateien werden ersetzt.", "”?\nExisting files are replaced."), T("Holen", "Fetch"), false)) return;
            string root = Path.Combine(GameDir, "mods", _syncHost.Mod);
            if (!Installer.CanWrite(GameLocator.IsGameDir(GameDir) ? Path.Combine(GameDir, "mods") : root)) { Toast(T("Keine Schreibrechte – PC-Check › Schreibrechte.", "No write access – PC check › write access."), ToastKind.Error); return; }
            _syncBusy = true; _syncCancel = false;
            LanHost h = _syncHost; SyncPlan plan = _syncPlan; string mod = h.Mod;
            List<FileEntry> todo = new List<FileEntry>(plan.Missing); todo.AddRange(plan.Different);
            long total = plan.Bytes;
            SetSync(T("Übertrage …", "Transferring …"));
            ThreadPool.QueueUserWorkItem(delegate
            {
                long done = 0; int files = 0, last = -1; string err = null;
                try
                {
                    foreach (FileEntry e in todo)
                    {
                        if (_syncCancel) throw new OperationCanceledException();
                        Lan.Fetch(h, mod, e, root, delegate (long n)
                        {
                            done += n;
                            int pct = total > 0 ? (int)(done * 100 / total) : 100;
                            if (pct != last) { last = pct; try { BeginInvoke((MethodInvoker)delegate { SetSync(T("Übertrage … ", "Transferring … ") + pct + " %  (" + SizeText(done) + " / " + SizeText(total) + ")"); }); } catch { } }
                        }, delegate { return _syncCancel; });
                        files++;
                    }
                }
                catch (OperationCanceledException) { err = T("Abgebrochen.", "Cancelled."); }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _syncBusy = false; _syncPlan = null;
                        if (err != null) { SetSync("⚠ " + files + T(" Dateien übertragen, dann gestoppt: ", " files transferred, then stopped: ") + err); return; }
                        SetSync("✓ " + files + T(" Dateien übernommen. Der Mod entspricht jetzt dem Server-PC.", " files taken over. The mod now matches the server PC."));
                        Toast(T("Abgleich fertig.", "Sync finished."), ToastKind.Ok);
                        RefreshStatus();
                    });
                }
                catch { }
            });
            if (_page == PgPlayer) _plHost.Masonry(_plCards, 2);
        }
    }
}
