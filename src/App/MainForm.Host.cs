// Hauptfenster: Host-Logik (Rollen, Standard, Zuletzt gestartet, Autospeichern, Startprüfung, laufender Server) - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace BF2Coop
{
    public partial class MainForm
    {
        bool _setupForce;
        WinTimer _autoSave; Dictionary<string, string> _startVals; string _rotSig = "";
        bool _moduleSeen, _hung, _crashed; DateTime _hungSince = DateTime.MinValue;
        Card _cBehavior, _cStandard; ToggleSwitch _autoRestart;
        Banner _cfgBanner, _liveBanner; FlatButton _bCfgAction, _bLiveAction;
        static string StandardFile { get { return Path.Combine(AppPaths.Dir, "standard.json"); } }
        static string RecentDir { get { return Path.Combine(AppPaths.Dir, "recent"); } }

        // ================================================================== Rollen
        void ApplyRoleCards()
        {
            RefreshVersions();
            if (_exHost != null) _exHost.Masonry(_exCards, 2);
            if (_cSetup != null) { _cSetup.Title = _host ? T("Mods und Server-Einrichtung", "Mods and server setup") : "Mods"; _cSetup.Invalidate(); }
        }

        // ================================================================== Optionen-Seite: Verhalten und Standard
        void BuildServerExtras()
        {
            _cBehavior = MakeCard("srv.behavior", T("Server-Verhalten", "Server behaviour"), G.Shield, true);
            _autoRestart = new ToggleSwitch(); _autoRestart.SetSilent(_cfg.B("Watchdog", true));
            _autoRestart.CheckedChanged += delegate { _cfg.Set("Watchdog", _autoRestart.Checked); _cfg.Save(); };
            FormRow rAuto = new FormRow(T("Bei Absturz automatisch neu starten", "Restart automatically after a crash"),
                T("Startet den Server neu, wenn er abstürzt oder nicht mehr reagiert (höchstens 3-mal in 10 Minuten).", "Restarts the server if it crashes or stops responding (at most 3 times in 10 minutes)."), _autoRestart, _autoRestart.Width, false);
            _cUnlock.Controls.Remove(_rowUnlockServer);
            _cLanHost.Controls.Remove(_lanHostRow);
            foreach (Control c in new Control[] { rAuto, _rowUnlockServer, _lanHostRow, _lanInfo }) _cBehavior.Controls.Add(c);
            _cBehavior.Layouter = Stack(_cBehavior, delegate { return new List<Control> { rAuto, _rowUnlockServer, _lanHostRow, _lanInfo }; });

            _cStandard = MakeCard("srv.standard", T("Standard und Vorlagen", "Default and presets"), G.Star, true);
            Para sInfo = new Para(T("Dein Standard gilt für alle Mods: Servereinstellungen, Bot-Werte pro Mapgröße und anpassende Schwierigkeit. Neue Mods starten damit, „Auf Standard zurücksetzen“ kehrt dorthin zurück. Die Rotation bleibt pro Mod.",
                "Your default applies to all mods: server settings, bot values per map size and adaptive difficulty. New mods start with it, “Reset to default” returns to it. The rotation stays per mod."));
            sInfo.Color = Theme.Dim;
            FlatButton bSet = new FlatButton(T("Aktuelle Werte als Standard", "Use current values as default"), G.Star, BtnKind.Secondary);
            bSet.Click += delegate { SaveStandard(); };
            FlatButton bReset = new FlatButton(T("Auf Standard zurücksetzen", "Reset to default"), G.Undo, BtnKind.Secondary);
            bReset.Click += delegate { ResetToStandard(false); };
            FlatButton bFactory = new FlatButton(T("Werkseinstellungen", "Factory settings"), null, BtnKind.Ghost);
            bFactory.Click += delegate { ResetToStandard(true); };
            ButtonRow sb = Buttons(bSet, bReset, bFactory);
            Para pInfo = new Para(T("VORLAGEN – beliebig viele gespeicherte Stände zum Vorbereiten, z. B. je eine pro Abendprogramm. Einstellungen und Bots sind immer enthalten, Rotation (mit Mod und Modus) und Spaßrunde wahlweise.",
                "PRESETS – any number of saved states to prepare ahead, e.g. one per evening programme. Settings and bots are always included, rotation (with mod and mode) and fun round optionally."));
            pInfo.Color = Theme.Dim;
            _presetEmpty = new Para(""); _presetEmpty.Color = Theme.Mute;
            FlatButton bNew = new FlatButton(T("Neue Vorlage …", "New preset …"), G.Save, BtnKind.Primary);
            Theme.Tip.SetToolTip(bNew, T("Speichert den aktuellen Stand unter einem Namen. Danach fragt der Manager, ob Rotation und Spaßrunde dazugehören.", "Saves the current state under a name. Then the manager asks whether rotation and fun round belong to it."));
            bNew.Click += delegate { SavePresetAs(); };
            ButtonRow nb = Buttons(bNew);
            foreach (Control c in new Control[] { sInfo, sb, pInfo, _presetEmpty, nb }) _cStandard.Controls.Add(c);
            _cStandard.Layouter = Stack(_cStandard, delegate
            {
                List<Control> l = new List<Control> { sInfo, sb, pInfo };
                if (_presetEmpty.Text.Length > 0) l.Add(_presetEmpty);
                l.AddRange(_presetRows); l.Add(nb);
                return l;
            });

            _srvCards.Insert(0, _cStandard); _srvCards.Insert(0, _cBehavior);
            _srvHost.Canvas.Controls.Add(_cBehavior); _srvHost.Canvas.Controls.Add(_cStandard);
            HookCollapse(_cBehavior, _srvHost, _srvCards, 2); HookCollapse(_cStandard, _srvHost, _srvCards, 2);
            RefreshPresets();
            BuildFunCard();

            // Hinweisleisten: Konfiguration (Server läuft / Absturz) und Live (reagiert nicht)
            _cfgBanner = new Banner();
            _bCfgAction = new FlatButton("", null, BtnKind.Primary);
            _bCfgAction.Click += delegate { if (_crashed) { _crashed = false; UpdateServerBanners(); if (StartServerProcess()) SelectPage(PgLive); } else { _forceConfig = false; SelectPage(PgLive); } };
            FlatButton bCfgDetails = new FlatButton(T("Details", "Details"), null, BtnKind.Ghost);
            bCfgDetails.Click += delegate { ShowServerLog(); };
            _cfgBanner.AddRight(_bCfgAction); _cfgBanner.AddRight(bCfgDetails);
            _pages[PgMaps].Controls.Add(_cfgBanner);
            _pages[PgMaps].Controls.SetChildIndex(_cfgBanner, _pages[PgMaps].Controls.Count - 2);

            _liveBanner = new Banner(); _liveBanner.Tint = Theme.Red; _liveBanner.Glyph = G.Warn;
            _bLiveAction = new FlatButton(T("Neu starten", "Restart"), G.Refresh, BtnKind.Primary);
            _bLiveAction.Click += delegate { RestartServer(); };
            _liveBanner.AddRight(_bLiveAction);
            _pages[PgLive].Controls.Add(_liveBanner);
            _pages[PgLive].Controls.SetChildIndex(_liveBanner, _pages[PgLive].Controls.Count - 2);
        }

        void UpdateServerBanners()
        {
            if (_cfgBanner == null) return;
            if (_crashed)
            {
                _cfgBanner.Visible = true; _cfgBanner.Tint = Theme.Red; _cfgBanner.Glyph = G.Warn;
                _cfgBanner.Text2 = _srvExit ?? T("Server wurde unerwartet beendet.", "The server stopped unexpectedly.");
                _bCfgAction.Text = T("Neu starten", "Restart");
            }
            else if (_running)
            {
                _cfgBanner.Visible = true; _cfgBanner.Tint = Theme.Green; _cfgBanner.Glyph = G.Pulse;
                _cfgBanner.Text2 = T("Server läuft – Rotation und Bots wirken ab der nächsten Map.", "Server is running – rotation and bots apply from the next map.");
                _bCfgAction.Text = T("Zur Live-Ansicht", "Back to live view");
            }
            else _cfgBanner.Visible = false;
            _bCfgAction.Fit(); _cfgBanner.PerformLayout(); _cfgBanner.Invalidate();
            _liveBanner.Visible = _running && _hung;
            _liveBanner.Text2 = T("Server reagiert nicht (Fehlerfenster offen?).", "Server is not responding (error window open?).");
            _liveBanner.PerformLayout(); _liveBanner.Invalidate();
        }

        void RestartServer()
        {
            _userStopped = true;
            Srv.Kill();
            System.Threading.Thread.Sleep(800);
            _hung = false; _crashed = false;
            if (StartServerProcess()) Toast(T("Server wird neu gestartet …", "Server is restarting …"), ToastKind.Info);
            UpdateServerBanners();
        }

        // Hänger: Prozess läuft, aber das Bot-Modul meldet sich nicht mehr (z. B. „Debug assertion failed“)
        void CheckHang(bool run)
        {
            if (_module) _moduleSeen = true;
            bool hung = run && _moduleSeen && !_module;
            if (hung && !_hung) { _hungSince = DateTime.Now; Util.Log(AppPaths.Dir, "Server not responding"); }
            _hung = hung;
            if (!run) _moduleSeen = false;
            if (_hung && _cfg.B("Watchdog", true) && (DateTime.Now - _hungSince).TotalSeconds > 60)
            {
                _hungSince = DateTime.Now.AddMinutes(5);
                Toast(T("Server reagiert nicht – wird neu gestartet …", "Server not responding – restarting …"), ToastKind.Warn);
                RestartServer();
            }
            UpdateServerBanners();
        }

        // ================================================================== Standard
        void SaveStandard()
        {
            try
            {
                Dictionary<string, object> d = Snapshot(false);
                d.Remove("Mod"); d.Remove("Type");
                Directory.CreateDirectory(AppPaths.Dir);
                File.WriteAllText(StandardFile, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
                Toast(T("Als Standard gespeichert.", "Saved as default."), ToastKind.Ok);
            }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
        }

        Dictionary<string, object> LoadStandard()
        {
            try { if (File.Exists(StandardFile)) return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(StandardFile, Encoding.UTF8)); }
            catch { }
            return null;
        }

        void ResetToStandard(bool factory)
        {
            Dictionary<string, object> std = factory ? null : LoadStandard();
            string what = factory ? T("Werkseinstellungen", "factory settings") : (std != null ? T("deinen Standard", "your default") : T("Werkseinstellungen (noch kein eigener Standard)", "factory settings (no own default yet)"));
            if (!DarkDialog.Confirm(this, T("Zurücksetzen", "Reset"), T("Servereinstellungen und Bot-Werte auf ", "Reset server settings and bot values to ") + what + T(" setzen? Die Rotation bleibt.", "? The rotation stays."), T("Zurücksetzen", "Reset"), false)) return;
            if (std == null)
            {
                _applying = true;
                _vals = Defs.Defaults(); ApplyVals();
                CoopState fresh = new CoopState();
                foreach (int s in new int[] { 16, 32, 64 }) _botEd[s].SetOpt(fresh.Defaults[s]);
                _adaptToggle.SetSilent(false); _adaptStep.SetSilent(5); _adaptMax.SetSilent(30);
                _applying = false;
                UpdateHumans(); MarkDirty();
            }
            else ApplySnapshot(std, false);
            Toast(T("Zurückgesetzt.", "Reset."), ToastKind.Ok);
        }

        // ================================================================== Zuletzt gestartet
        void SaveRecent()
        {
            try
            {
                Directory.CreateDirectory(RecentDir);
                Dictionary<string, object> d = Snapshot(true);
                d["Label"] = _mod + " · " + (_coop ? "Coop" : "Conquest") + " · " + _st.Maps.Count + " Maps · " + DateTime.Now.ToString("dd.MM. HH:mm");
                string sig = _mod + _coop + string.Join(",", ((List<string>)d["Maps"]).ToArray());
                foreach (string f in Directory.GetFiles(RecentDir, "*.json"))
                    try { if (File.ReadAllText(f).Contains("\"Sig\":\"" + Util.SafeName(sig.GetHashCode().ToString()) + "\"")) File.Delete(f); } catch { }
                d["Sig"] = Util.SafeName(sig.GetHashCode().ToString());
                File.WriteAllText(Path.Combine(RecentDir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"), new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
                string[] all = Directory.GetFiles(RecentDir, "*.json");
                Array.Sort(all, StringComparer.Ordinal);
                for (int i = 0; i < all.Length - 5; i++) File.Delete(all[i]);
            }
            catch { }
        }

        void BuildConfigMenu(ContextMenuStrip m)
        {
            m.Items.Clear();
            ToolStripMenuItem head = new ToolStripMenuItem(T("Zuletzt gestartet", "Recently started")); head.Enabled = false;
            m.Items.Add(head);
            string[] rec = Directory.Exists(RecentDir) ? Directory.GetFiles(RecentDir, "*.json") : new string[0];
            Array.Sort(rec, StringComparer.Ordinal); Array.Reverse(rec);
            foreach (string f in rec)
            {
                string file = f, label = Path.GetFileNameWithoutExtension(f);
                try { Dictionary<string, object> d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(f, Encoding.UTF8)); object o; if (d.TryGetValue("Label", out o)) label = Convert.ToString(o); } catch { }
                ToolStripMenuItem it = new ToolStripMenuItem("   " + label); it.Click += delegate { LoadPreset(file); };
                m.Items.Add(it);
            }
            if (rec.Length == 0) { ToolStripMenuItem none = new ToolStripMenuItem(T("   noch nichts", "   nothing yet")); none.Enabled = false; m.Items.Add(none); }
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem ph = new ToolStripMenuItem(T("Vorlagen", "Presets")); ph.Enabled = false; m.Items.Add(ph);
            foreach (string f in PresetFile.All())
            {
                string file = f, name = Path.GetFileNameWithoutExtension(f);
                ToolStripMenuItem it = new ToolStripMenuItem("   " + name); it.Click += delegate { LoadPreset(file); };
                m.Items.Add(it);
            }
            ToolStripMenuItem save = new ToolStripMenuItem(T("   Aktuellen Stand als Vorlage speichern …", "   Save current state as preset …"));
            save.Click += delegate { SavePresetAs(); };
            ToolStripMenuItem manage = new ToolStripMenuItem(T("   Vorlagen verwalten …", "   Manage presets …"));
            manage.Click += delegate { SelectPage(PgServer); if (_cStandard.Collapsed) _cStandard.Collapsed = false; _srvHost.Masonry(_srvCards, 2); _srvHost.EnsureVisible(_cStandard); };
            m.Items.Add(save); m.Items.Add(manage);
        }

        // ================================================================== Automatisch speichern
        void ScheduleSave()
        {
            if (_autoSave == null) { _autoSave = new WinTimer(); _autoSave.Interval = 800; _autoSave.Tick += delegate { _autoSave.Stop(); AutoSaveNow(); }; }
            _autoSave.Stop(); _autoSave.Start();
        }

        void AutoSaveNow()
        {
            if (_setupMode || _mod.Length == 0) return;
            if (_autoSave != null) _autoSave.Stop();
            if (!_dirty) return;
            if (SaveAll(true) && _running) ApplyToRunningServer();
        }

        // Laufender Server: ⚡-Werte sofort senden, geänderte Rotation übergeben (nächste Map bleibt die nach der aktuellen)
        void ApplyToRunningServer()
        {
            if (!_running || !_module) return;
            if (_startVals != null)
                foreach (SettingDef d in Defs.All)
                {
                    if (!d.Live) continue;
                    string v = _vals[d.Key], s; _sentVals.TryGetValue(d.Key, out s);
                    if (s == null) _startVals.TryGetValue(d.Key, out s);
                    if (Defs.Same(d, v, s)) continue;
                    try { CmdChannel.Send(ModSettingsDir, d.IsSpecial ? "@commander " + v : d.Key + " " + Defs.ToCon(d, v)); _sentVals[d.Key] = v; } catch { }
                }
            string sig = RotationSig();
            if (sig != _rotSig && _st.Maps.Count > 0)
            {
                _rotSig = sig;
                try
                {
                    CmdChannel.Send(ModSettingsDir, "mapList.clear");
                    foreach (MapEntry m in _st.Maps) CmdChannel.Send(ModSettingsDir, "mapList.append " + m.Name + " " + m.Mode + " " + m.Size);
                    int cur = CurrentMapIndex();
                    CmdChannel.Send(ModSettingsDir, "admin.nextLevel " + ((cur + 1) % _st.Maps.Count));
                    Toast(T("Rotation an den laufenden Server übergeben – wirkt ab der nächsten Map.", "Rotation sent to the running server – applies from the next map."), ToastKind.Info);
                }
                catch { }
            }
            UpdateLiveBanner();
        }

        readonly Dictionary<string, string> _sentVals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string _liveMap = "";

        string RotationSig() { StringBuilder sb = new StringBuilder(); foreach (MapEntry m in _st.Maps) sb.Append(m.Key).Append(';'); return sb.ToString(); }

        int CurrentMapIndex()
        {
            for (int i = 0; i < _st.Maps.Count; i++) if (string.Equals(_st.Maps[i].Name, _liveMap, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        // Anzahl Änderungen, die erst nach einem Neustart wirken
        int PendingRestart()
        {
            if (_startVals == null) return 0;
            int n = 0;
            foreach (SettingDef d in Defs.All)
            {
                if (d.Live) continue;
                string s; _startVals.TryGetValue(d.Key, out s);
                if (!Defs.Same(d, _vals[d.Key], s)) n++;
            }
            return n;
        }

        // ================================================================== Prüfung vor dem Serverstart
        bool ValidateStart()
        {
            List<string> err = new List<string>(), warn = new List<string>();
            if (!File.Exists(Path.Combine(GameDir, "bf2_w32ded.exe"))) err.Add(T("bf2_w32ded.exe fehlt im Spielordner.", "bf2_w32ded.exe is missing in the game folder."));
            if (_st.Maps.Count == 0) err.Add(T("Die Rotation ist leer – mindestens eine Map hinzufügen.", "The rotation is empty – add at least one map."));
            ModStatus ms;
            if (_coop && _status.TryGetValue(_mod, out ms) && (!ms.Installed || !ms.Hooked)) err.Add(T("Das Bot-Modul ist in „", "The bot module is not set up in “") + _mod + T("“ nicht eingerichtet (Einstellungen › Mods).", "” (Settings › Mods)."));
            if (!PortFree(Util.ToInt(_vals["sv.serverPort"], 16567))) err.Add(T("Der Server-Port ist belegt – läuft schon ein anderer Server?", "The server port is in use – is another server already running?"));
            if (_coop)
                foreach (MapEntry m in _st.Maps)
                {
                    BotPlan p = Planner.Plan(OptFor(m), _hUS.Value, _hMEC.Value);
                    if (p.Total > m.Size) { warn.Add(Util.Pretty(m.Name) + " " + m.Size + ": " + p.Total + T(" Spieler", " players")); }
                }
            if (err.Count > 0) { DarkDialog.Info(this, T("Server kann nicht starten", "Server cannot start"), "• " + string.Join("\n• ", err.ToArray())); return false; }
            if (_coop && !CheckCoopScript()) return false;
            if (_coop && !CheckAiOverride()) return false;
            if (warn.Count > 0) Toast(T("Mehr Spieler als Plätze: ", "More players than slots: ") + string.Join(", ", warn.GetRange(0, Math.Min(3, warn.Count)).ToArray()), ToastKind.Warn);
            return true;
        }

        // Mods ohne gpm_coop.py (z. B. bfp2) scheitern bei jedem Coop-Start mit "failed to initialize gamemode". false = Start abbrechen.
        bool CheckCoopScript()
        {
            if (!CoopScript.Missing(GameDir, _mod)) return true;
            string msg = T("Dem Mod „", "The mod “") + _mod + T("“ fehlt das Coop-Spielmodus-Skript (gpm_coop.py). Ohne es bricht der Server mit „failed to initialize gamemode“ ab.\n\nJetzt anlegen? Der Manager erzeugt es aus dem Conquest-Skript des Mods (gpm_cq.py) – so ist es auch im Basisspiel gelöst. Vorhandene Dateien werden nicht verändert.",
                "” is missing its co-op game mode script (gpm_coop.py). Without it the server stops with “failed to initialize gamemode”.\n\nCreate it now? The manager builds it from the mod's conquest script (gpm_cq.py) – the same as in the base game. Existing files are not changed.");
            if (!DarkDialog.Confirm(this, T("Coop-Spielmodus fehlt", "Co-op game mode missing"), msg, T("Anlegen und starten", "Create and start"), false)) return false;
            try { CoopScript.Create(GameDir, _mod); Toast(T("Coop-Spielmodus angelegt.", "Co-op game mode created."), ToastKind.Ok); return true; }
            catch (UnauthorizedAccessException) { Toast(T("Keine Schreibrechte im Spielordner – Einstellungen › Dieser PC › Schreibrechte.", "No write access to the game folder – Settings › This PC › Write access."), ToastKind.Error); return false; }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); return false; }
        }

        // Mods wie bf2sf64 und bfp2 geben die Bots in aiDefault.ai fest vor (overrideMenuSettings 1).
        // Vor dem Start anbieten, das abzuschalten. false = Start abbrechen.
        bool CheckAiOverride()
        {
            AiFile a = AiFile.Load(GameDir, _mod);
            if (!a.OverridesManager) return true;
            string fixedBots = a.MaxBots >= 0 ? a.MaxBots.ToString() : "?";
            string msg = T("Der Mod „", "The mod “") + _mod + T("“ gibt die Bots fest vor (", "” forces its own bots (") + fixedBots +
                T(" Bots, aiDefault.ai: overrideMenuSettings 1). Deine Einstellungen für Botzahl, Verteilung und Schwierigkeit hätten keine Wirkung.\n\nJetzt beheben? Der Wert wird auf 0 gesetzt, eine Sicherung wird angelegt.",
                  " bots, aiDefault.ai: overrideMenuSettings 1). Your bot count, distribution and difficulty would have no effect.\n\nFix it now? The value is set to 0 and a backup is created.");
            if (!DarkDialog.Confirm(this, T("Mod überschreibt die Bots", "Mod overrides the bots"), msg, T("Beheben und starten", "Fix and start"), false))
            {
                Toast(T("Start mit den festen Bots des Mods.", "Starting with the mod's fixed bots."), ToastKind.Warn);
                return true;
            }
            try { AiFile.DisableOverride(GameDir, _mod, DocsDir); LoadAiAndNames(); Toast(T("Bot-Einstellungen des Managers gelten jetzt.", "The manager's bot settings now apply."), ToastKind.Ok); return true; }
            catch (UnauthorizedAccessException) { Toast(T("Keine Schreibrechte im Spielordner – Einstellungen › Dieser PC › Schreibrechte.", "No write access to the game folder – Settings › This PC › Write access."), ToastKind.Error); return false; }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); return false; }
        }

        static bool PortFree(int port)
        {
            try { using (UdpClient u = new UdpClient(port)) { } return true; }
            catch { return false; }
        }

        // ================================================================== Diagnose: Server-Log
        void ShowServerLog()
        {
            string f = _mod.Length > 0 ? Path.Combine(ModSettingsDir, "coop_status.txt") : null;
            string[] lines = f != null ? Util.ReadLines(f) : new string[0];
            int from = Math.Max(0, lines.Length - 200);
            StringBuilder sb = new StringBuilder();
            for (int i = from; i < lines.Length; i++) sb.Append(lines[i]).Append("\r\n");
            using (Form d = new Form())
            {
                d.Text = T("Server-Log", "Server log") + " – " + _mod; d.StartPosition = FormStartPosition.CenterParent; d.Size = new Size(Dpi.D(900), Dpi.D(560));
                d.BackColor = Theme.Bg; d.ShowInTaskbar = false; d.Icon = Icon;
                RichTextBox rt = new RichTextBox(); rt.Dock = DockStyle.Fill; rt.ReadOnly = true; rt.BorderStyle = BorderStyle.None;
                rt.BackColor = Theme.Input; rt.ForeColor = Theme.Text; rt.Font = Theme.Mono; rt.Text = sb.Length > 0 ? sb.ToString() : T("Noch keine Einträge.", "No entries yet.");
                d.Controls.Add(rt);
                d.HandleCreated += delegate { Native.DarkTitle(d); Native.DarkScroll(rt); };
                rt.SelectionStart = rt.TextLength; rt.ScrollToCaret();
                d.ShowDialog(this);
            }
        }
    }
}
