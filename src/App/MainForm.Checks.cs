// Hauptfenster: PC-Check, LAN-Dienst, Wartung und Versionen (Seite Extras) - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BF2Coop
{
    public partial class MainForm
    {
        Card _cCheck, _cLanHost, _cMaint, _cVersions;
        readonly List<Control> _checkItems = new List<Control>(); List<CheckItem> _checks = new List<CheckItem>();
        FlatButton _bFixAll; int _checkBad; bool _checking; bool? _checkOkShown; DateTime _checkedAt = DateTime.MinValue;
        ToggleSwitch _lanHostToggle; FormRow _lanHostRow; Para _diagErrors; Para _lanInfo, _versionsText, _maintLog;

        // Ordnet Elemente untereinander an. Nur die gelieferten Elemente sind sichtbar (Kopf-Elemente ausgenommen).
        Func<int, int> Stack(Card c, Func<List<Control>> items)
        {
            return delegate (int w)
            {
                List<Control> list = items();
                HashSet<Control> act = new HashSet<Control>(list);
                foreach (Control ch in c.Controls) if (!c.HeaderControls.Contains(ch)) ch.Visible = act.Contains(ch);
                int y = c.HeaderH - Dpi.D(4);
                foreach (Control x in list)
                {
                    if (x is FormRow) { x.SetBounds(Dpi.D(6), y, w - Dpi.D(12), x.Height); y += x.Height; }
                    else if (x is Para) { int h = ((Para)x).HeightFor(w - Dpi.D(44)); x.SetBounds(Dpi.D(22), y + Dpi.D(2), w - Dpi.D(44), h); y += h + Dpi.D(8); }
                    else if (x is ButtonRow) { int h = ((ButtonRow)x).LayoutFor(w - Dpi.D(40)); x.SetBounds(Dpi.D(20), y + Dpi.D(4), w - Dpi.D(40), h); y += h + Dpi.D(12); }
                    else { x.SetBounds(Dpi.D(20), y, w - Dpi.D(40), x.Height); y += x.Height + Dpi.D(8); }
                }
                return y + Dpi.D(10);
            };
        }

        static ButtonRow Buttons(params Control[] b) { ButtonRow r = new ButtonRow(); foreach (Control c in b) r.Add(c); return r; }

        void BuildCheckCards()
        {
            // ---- PC-Check
            _cCheck = new Card(T("PC-Check", "PC check"), G.Check);
            _bFixAll = new FlatButton(T("Alles beheben", "Fix all"), G.Shield, BtnKind.Primary); _bFixAll.Height = Dpi.D(30); _bFixAll.Fit();
            Theme.Tip.SetToolTip(_bFixAll, T("Behebt alle markierten Punkte mit einer einzigen Windows-Abfrage.", "Fixes all marked items with a single Windows prompt."));
            _bFixAll.Click += delegate { FixChecks(null); };
            FlatButton bRecheck = new FlatButton(null, G.Refresh, BtnKind.Ghost);
            Theme.Tip.SetToolTip(bRecheck, T("Erneut prüfen", "Check again"));
            bRecheck.Click += delegate { RefreshChecks(true); };
            _cCheck.Controls.Add(_bFixAll); _cCheck.Controls.Add(bRecheck);
            _cCheck.HeaderControls.Add(_bFixAll); _cCheck.HeaderControls.Add(bRecheck);
            _cCheck.Collapsible = true; _cCheck.Collapsed = true;
            _cCheck.Toggled += delegate { _exHost.Masonry(_exCards, 2); if (!_cCheck.Collapsed) _exHost.EnsureVisible(_cCheck); };
            Func<int, int> stack = Stack(_cCheck, delegate { return new List<Control>(_checkItems); });
            _cCheck.Layouter = delegate (int w)
            {
                bRecheck.Location = new Point(w - Dpi.D(12) - bRecheck.Width, Dpi.D(8));
                _bFixAll.Location = new Point(bRecheck.Left - Dpi.D(6) - _bFixAll.Width, Dpi.D(9));
                _bFixAll.Visible = _checkBad > 0 && !_checking;
                _cCheck.HeaderReserve = w - (_bFixAll.Visible ? _bFixAll.Left : bRecheck.Left) + Dpi.D(4);
                return stack(w);
            };

            // ---- LAN-Dienst
            _cLanHost = new Card(T("LAN-Dienst", "LAN service"), G.Globe);
            _lanHostToggle = new ToggleSwitch(); _lanHostToggle.SetSilent(LanHostOn);
            FormRow rHost = _lanHostRow = new FormRow(T("Diesen PC als Server-PC anbieten", "Offer this PC as server PC"),
                T("Server-PC im LAN\nAndere Manager sehen diesen PC in ihrer Serverliste, können direkt beitreten, ihre Mod-Dateien mit diesem PC abgleichen und fehlende Dateien sowie den Manager von hier holen.\n\nNur lesend, nur der Ordner mods und die Manager-exe. Ports: UDP 29960, TCP 29961.",
                  "Server PC on LAN\nOther managers see this PC in their server list, can join directly, compare their mod files with this PC and fetch missing files and the manager from here.\n\nRead-only, only the mods folder and the manager exe. Ports: UDP 29960, TCP 29961."), _lanHostToggle, _lanHostToggle.Width, false);
            _lanHostToggle.CheckedChanged += delegate { _cfg.Set("LanHost", _lanHostToggle.Checked); _cfg.Save(); ApplyLanHost(); RefreshLanInfo(); };
            _lanInfo = new Para(""); _lanInfo.Color = Theme.Dim;
            _cLanHost.Controls.Add(rHost); _cLanHost.Controls.Add(_lanInfo);
            _cLanHost.Layouter = Stack(_cLanHost, delegate { return new List<Control> { rHost, _lanInfo }; });

            // ---- Wartung
            _cMaint = new Card(T("Diagnose", "Diagnostics"), G.Wrench);
            Para mInfo = _diagErrors = new Para("");
            FlatButton bLog = new FlatButton(T("Server-Log", "Server log"), G.Console, BtnKind.Secondary);
            bLog.Click += delegate { ShowServerLog(); };
            FlatButton bRepair = new FlatButton(T("Reparieren", "Repair"), G.Wrench, BtnKind.Secondary);
            FlatButton bDiag = new FlatButton(T("Diagnose exportieren", "Export diagnostics"), G.Save, BtnKind.Secondary);
            FlatButton bCache = new FlatButton(T("Kartenvorschau-Cache leeren", "Clear map preview cache"), G.Delete, BtnKind.Ghost);
            bRepair.Click += delegate { Repair(); };
            bDiag.Click += delegate { ExportDiagnostics(); };
            bCache.Click += delegate
            {
                try { if (Directory.Exists(AppPaths.Cache)) Directory.Delete(AppPaths.Cache, true); Toast(T("Cache geleert – Maps werden neu eingelesen.", "Cache cleared – maps are scanned again."), ToastKind.Ok); if (!_setupMode) StartScan(); }
                catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
            };
            ButtonRow mb = Buttons(bLog, bDiag, bRepair, bCache);
            Theme.Tip.SetToolTip(bDiag, T("ZIP mit Versionen, Windows-Version, installierten Mods, Konfiguration, Logs und erkannten Fehlern – ohne Passwörter, Benutzernamen und öffentliche IP-Adressen.", "ZIP with versions, Windows version, installed mods, configuration, logs and detected problems – without passwords, user names and public IP addresses."));
            Theme.Tip.SetToolTip(bRepair, T("Installiert das Bot-Modul neu, erneuert Schreibrechte und wendet alle Korrekturen an.", "Reinstalls the bot module, renews write access and applies all fixes."));
            _maintLog = new Para(""); _maintLog.Color = Theme.Dim;
            foreach (Control c in new Control[] { mInfo, mb, _maintLog }) _cMaint.Controls.Add(c);
            _cMaint.Layouter = Stack(_cMaint, delegate { List<Control> l = new List<Control> { mInfo, mb }; if (_maintLog.Text.Length > 0) l.Add(_maintLog); return l; });

            // ---- Versionen und Info
            _cVersions = new Card(T("Versionen", "Versions"), G.Info);
            _versionsText = new Para(""); _versionsText.Color = Theme.Text;
            Para links = new Para(AppInfo.RepoUrl + "\n" + T("Tastenkürzel: Strg+S speichern · F5 Server starten/stoppen · Strg+1 bis 6 Seiten", "Shortcuts: Ctrl+S save · F5 start/stop server · Ctrl+1 to 6 pages") + "\n" + T("Einstellungen und Presets: ", "Settings and presets: ") + AppPaths.Dir);
            links.Color = Theme.Dim;
            FlatButton bGit = new FlatButton(T("Projektseite öffnen", "Open project page"), G.Globe, BtnKind.Ghost);
            bGit.Click += delegate { try { Process.Start(AppInfo.RepoUrl); } catch { } };
            ButtonRow vb = Buttons(bGit);
            foreach (Control c in new Control[] { _versionsText, links, vb }) _cVersions.Controls.Add(c);
            _cVersions.Layouter = Stack(_cVersions, delegate { return new List<Control> { _versionsText, links, vb }; });
        }

        void RefreshVersions()
        {
            if (_versionsText == null) return;
            StringBuilder sb = new StringBuilder();
            // Bot-Modul-Versionen interessieren nur auf dem Server-PC
            sb.Append(AppInfo.Name).Append(" ").Append(AppInfo.Version);
            if (_host) sb.Append(T("  ·  Bot-Modul ", "  ·  bot module ")).Append(AppInfo.ModuleVersion);
            sb.Append('\n');
            if (GameLocator.IsGameDir(GameDir))
            {
                sb.Append("BF2.exe ").Append(GameLocator.VersionText(Path.Combine(GameDir, "BF2.exe")));
                sb.Append("  ·  Server ").Append(GameLocator.VersionText(Path.Combine(GameDir, "bf2_w32ded.exe")));
                if (GameLocator.IsBf2Hub(GameDir)) sb.Append("  ·  BF2Hub");
                sb.Append('\n');
                foreach (string m in GameLocator.AllMods(GameDir))
                {
                    ModInfo mi = ModInfo.Read(GameDir, m);
                    sb.Append("• ").Append(m);
                    if (!string.IsNullOrEmpty(mi.Title) && mi.Title != m) sb.Append(" – ").Append(mi.Title);
                    if (!string.IsNullOrEmpty(mi.Version)) sb.Append(" ").Append(mi.Version);
                    ModStatus s;
                    if (_host && _status.TryGetValue(m, out s) && s.Installed) sb.Append(T("  (Bot-Modul ", "  (bot module ")).Append(s.Version).Append(")");
                    sb.Append('\n');
                }
            }
            _versionsText.Text = sb.ToString().TrimEnd();
        }

        void RefreshLanInfo()
        {
            if (_lanInfo == null) return;
            StringBuilder sb = new StringBuilder();
            List<string> ips = Srv.LanAddresses();
            sb.Append(T("Adressen dieses PCs: ", "Addresses of this PC: ")).Append(ips.Count > 0 ? string.Join(", ", ips.ToArray()) : "–").Append('\n');
            if (_lanListen != null && _lanListen.Error != null) sb.Append(T("⚠ Serverliste nicht verfügbar: ", "⚠ Server list unavailable: ")).Append(_lanListen.Error).Append('\n');
            if (LanHostOn)
            {
                if (_lanFiles != null && _lanFiles.Error != null) sb.Append(T("⚠ Datei-Dienst nicht gestartet: ", "⚠ File service not started: ")).Append(_lanFiles.Error);
                else
                {
                    sb.Append(T("✓ Aktiv – andere PCs sehen diesen Server und können Dateien abgleichen.", "✓ Active – other PCs see this server and can compare files."));
                    string hm = HostedMod; int pct; double secs;
                    if (hm.Length > 0 && ModPrints.Busy(GameDir, hm, out pct, out secs))
                        sb.Append('\n').Append(T("⏳ Prüfe Dateien von „", "⏳ Checking files of “")).Append(hm).Append(T("“ für den Abgleich: ", "” for syncing: ")).Append(pct).Append(" %");
                }
            }
            else sb.Append(T("Aus – dieser PC sucht nur nach Servern.", "Off – this PC only looks for servers."));
            _lanInfo.Text = sb.ToString().TrimEnd();
        }

        // PC-Check im Hintergrund (netsh-Abfragen dauern etwas); force = auch kurz nach der letzten Prüfung
        void RefreshChecks(bool force)
        {
            if (_checking || _cCheck == null) return;
            if (!force && (DateTime.Now - _checkedAt).TotalSeconds < 20) return;
            _checking = true;
            string game = GameDir, docs = DocsDir; bool? echo = EchoState; _echoShown = echo;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<CheckItem> r;
                try { r = PcCheck.Run(game, docs, echo); } catch (Exception ex) { r = new List<CheckItem>(); Util.Log(AppPaths.Dir, ex.ToString()); }
                try { BeginInvoke((MethodInvoker)delegate { ShowChecks(r); }); } catch { }
            });
        }

        void ShowChecks(List<CheckItem> items)
        {
            _checking = false; _checkedAt = DateTime.Now; _checks = items;
            foreach (Control c in _checkItems) { _cCheck.Controls.Remove(c); c.Dispose(); }
            _checkItems.Clear();
            _checkBad = 0;
            foreach (CheckItem it in items)
            {
                CheckItem ci = it;
                if (ci.NeedsFix) _checkBad++;
                string mark = ci.Status == CheckStatus.Ok ? "✓  " : (ci.Status == CheckStatus.Info ? "ℹ  " : (ci.Status == CheckStatus.Warn ? "⚠  " : "✗  "));
                Control ed;
                if (ci.NeedsFix && ci.CanFix)
                {
                    FlatButton b = new FlatButton(ci.FixLabel ?? T("Beheben", "Fix"), null, BtnKind.Secondary); b.Height = Dpi.D(30); b.Fit();
                    b.Click += delegate { FixChecks(ci); };
                    ed = b;
                }
                else ed = new TextLabel(ci.Status == CheckStatus.Ok ? "OK" : "", Theme.SmallB);
                FormRow row = new FormRow(mark + ci.Title, null, ed, ed.Width, false);
                Para p = new Para(ci.Detail ?? ""); p.Color = ci.Status == CheckStatus.Error ? Theme.Red : (ci.Status == CheckStatus.Warn ? Theme.Amber : Theme.Dim); p.Font = Theme.Small;
                _checkItems.Add(row); _checkItems.Add(p);
                _cCheck.Controls.Add(row); _cCheck.Controls.Add(p);
            }
            _cCheck.SetNote(_checkBad == 0 ? T("alles in Ordnung", "all good") : _checkBad + T(" offen", " open"));
            // Alles grün: Details eingeklappt; Probleme: aufgeklappt. Nur beim Wechsel des Zustands, sonst bleibt die Wahl des Nutzers.
            bool okNow = _checkBad == 0;
            if (_checkOkShown != okNow) { _cCheck.Collapsed = okNow; _checkOkShown = okNow; }
            List<string> errs = new List<string>();
            foreach (CheckItem c in items) if (c.NeedsFix) errs.Add("⚠ " + c.Title + ": " + c.Detail);
            foreach (ModStatus s2 in _status.Values) if (_mods.Contains(s2.Mod) && !s2.Ok) errs.Add("⚠ " + s2.Mod + T(": Bot-Modul nicht bereit", ": bot module not ready"));
            if (_crashed) errs.Add(T("⚠ Server wurde unerwartet beendet", "⚠ Server stopped unexpectedly"));
            _diagErrors.Text = errs.Count == 0 ? T("✓ Keine Fehler erkannt.", "✓ No problems detected.") : string.Join("\n", errs.ToArray());
            _diagErrors.Color = errs.Count == 0 ? Theme.Green : Theme.Amber;
            _cCheck.NoteColor = _checkBad == 0 ? Theme.Green : Theme.Amber;
            int bad = 0; foreach (ModStatus s in _status.Values) if (_mods.Contains(s.Mod) && !s.Ok) bad++;
            _nav[PgExtras].Badge = bad > 0 || _checkBad > 0 ? "!" : null; _nav[PgExtras].BadgeColor = Theme.Red; _nav[PgExtras].Invalidate();
            if (_exHost != null) _exHost.Masonry(_exCards, 2);
            RefreshPlayHealth();
        }

        // Einen Punkt (oder alle offenen, wenn only == null) beheben – Adminaufträge gesammelt mit einer Abfrage
        void FixChecks(CheckItem only)
        {
            List<Dictionary<string, object>> tasks = new List<Dictionary<string, object>>();
            foreach (CheckItem c in _checks)
            {
                if (only != null && c != only) continue;
                if (!c.NeedsFix) continue;
                if (c.Id == "profile") { FixProfile(); continue; }
                if (c.Id == "game") { ChooseGameDir(); continue; }
                if (c.Id == "arm") { FixArm(); continue; }
                if (c.Url != null) { if (only != null) try { Process.Start(c.Url); } catch { } continue; }
                tasks.AddRange(c.Tasks);
            }
            if (tasks.Count == 0) return;
            if (Srv.GameRunning()) { Toast(T("Bitte zuerst Battlefield 2 beenden.", "Please close Battlefield 2 first."), ToastKind.Warn); return; }
            _bFixAll.Enabled = false;
            RunTasks(tasks, delegate (bool ok, string result)
            {
                _bFixAll.Enabled = true;
                string log = FormatLog(result);
                Toast(ok ? T("Behoben.", "Fixed.") : (log.Length > 0 ? log : T("Nicht alles konnte behoben werden.", "Not everything could be fixed.")), ok ? ToastKind.Ok : ToastKind.Warn);
                RefreshStatus(); RefreshChecks(true);
            });
        }

        // Windows on ARM: Emulationsschalter (HKCU, für diesen Benutzer) und CPU-Patch – ohne Adminrechte
        void FixArm()
        {
            if (Srv.GameRunning() || _running) { Toast(T("Bitte zuerst Battlefield 2 und den Server beenden.", "Please close Battlefield 2 and stop the server first."), ToastKind.Warn); return; }
            try
            {
                List<string> log = ArmSupport.Apply(GameDir);
                Util.Log(AppPaths.Dir, "Windows on ARM setup: " + string.Join("; ", log.ToArray()));
                Toast(T("Für Windows on ARM eingerichtet.", "Set up for Windows on ARM."), ToastKind.Ok);
            }
            catch (UnauthorizedAccessException) { Toast(T("Keine Schreibrechte im Spielordner – zuerst „Schreibrechte vergeben“.", "No write access to the game folder – use “Grant write access” first."), ToastKind.Error); }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
            RefreshChecks(true);
        }

        // Profil fehlt oder heißt noch „defaultPlayer“: auf der Seite Spielen direkt den Namen abfragen
        void FixProfile()
        {
            if (!_host) SelectPage(PgPlayer);
            if (CurrentMeState == MeState.NoProfile && !_host) { _meField.Box.Focus(); return; }
            Bf2Profile p = PlayProfile;
            if (p == null) CreateProfileFlow(); else RenameFlow();
        }

        void Repair()
        {
            List<Dictionary<string, object>> tasks = new List<Dictionary<string, object>>();
            if (GameLocator.IsGameDir(GameDir) && !Installer.CanWrite(Path.Combine(GameDir, "mods"))) tasks.Add(Tasks.Grant(GameDir));
            if (_mods.Count > 0) tasks.Add(Tasks.Setup(GameDir, DocsDir, _mods));
            foreach (CheckItem c in _checks) if (c.NeedsFix) tasks.AddRange(c.Tasks);
            if (tasks.Count == 0) { Toast(T("Nichts zu reparieren – alles in Ordnung.", "Nothing to repair – all good."), ToastKind.Ok); return; }
            if (_running) { Toast(T("Bitte zuerst den Server beenden.", "Please stop the server first."), ToastKind.Warn); return; }
            _maintLog.Text = T("Reparatur läuft … bitte die Windows-Abfrage bestätigen.", "Repairing … please confirm the Windows prompt."); _exHost.Masonry(_exCards, 2);
            RunTasks(tasks, delegate (bool ok, string result)
            {
                _maintLog.Text = FormatLog(result);
                Toast(ok ? T("Repariert.", "Repaired.") : T("Reparatur mit Warnungen beendet.", "Repair finished with warnings."), ok ? ToastKind.Ok : ToastKind.Warn);
                RefreshStatus(); RefreshChecks(true);
            });
        }

        void ExportDiagnostics()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "ZIP|*.zip";
                d.FileName = "BF2Manager-Diagnose-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip";
                d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    List<string> mods = GameLocator.IsGameDir(GameDir) ? GameLocator.AllMods(GameDir) : new List<string>();
                    Diagnostics.Export(d.FileName, GameDir, mods);
                    Toast(T("Diagnose gespeichert: ", "Diagnostics saved: ") + Path.GetFileName(d.FileName), ToastKind.Ok);
                }
                catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
            }
        }
    }
}
