// Hauptfenster: Seite Spieler (Profil, Spielen, Server im LAN, Grafik, Audio) - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace BF2Coop
{
    public partial class MainForm
    {
        ScrollHost _plHost; readonly List<Card> _plCards = new List<Card>();
        readonly List<Card> _profileCards = new List<Card>(); Banner _plHealth; FlatButton _bHealth;
        Card _cPlay, _cLan, _cGfx, _cAudio;
        SelectBox _plMod, _plRes; ToggleSwitch _plWindowed, _plIntro, _plAutoLogin, _plReShade, _plEax;
        Segmented _plQuality, _plSound; Slider _plView, _plMusic, _plFx;
        readonly List<Control> _lanRows = new List<Control>(); string _lanSig = "";
        Para _lanEmpty, _syncText; ButtonRow _syncButtons; FlatButton _bSyncGo, _bSyncCancel;
        WinTimer _plSave; bool _plLoading, _plQualityTouched, _plQualityCustom; List<Bf2Profile> _profiles = new List<Bf2Profile>(); bool _profileBackedUp;
        static readonly string[] QualityKeys = { "setTerrainQuality", "setGeometryQuality", "setLightingQuality", "setDynamicLightingQuality", "setDynamicShadowsQuality", "setEffectsQuality", "setTextureQuality", "setTextureFilteringQuality" };

        string PlayMod
        {
            get
            {
                string m = _cfg.S("PlayMod", "");
                if (m.Length == 0 || !Directory.Exists(Path.Combine(GameDir, "mods", m))) m = _mod.Length > 0 ? _mod : "bf2";
                return m;
            }
        }

        Bf2Profile PlayProfile
        {
            get
            {
                string id = _cfg.S("PlayProfile", "");
                foreach (Bf2Profile p in _profiles) if (p.Id == id) return p;
                Bf2Profile d = Profiles.Default();
                return d;
            }
        }

        string ProfileFile(string name) { Bf2Profile p = PlayProfile; return p != null ? Path.Combine(p.Dir, name) : ""; }
        string VideoCon { get { return ProfileFile("Video.con"); } }

        void BuildPlayer()
        {
            PageHeader ph = new PageHeader(T("Spielen", "Play"), T("Server im Netzwerk werden automatisch gefunden – einfach beitreten", "Servers on the network are found automatically – just join"));
            _pages[PgPlayer].Controls.Add(ph);
            _plHost = new ScrollHost(); _plHost.Dock = DockStyle.Fill;
            _plHealth = new Banner(); _plHealth.Glyph = G.Check;
            _bHealth = new FlatButton(T("Beheben", "Fix"), G.Shield, BtnKind.Primary);
            _bHealth.Click += delegate { FixChecks(null); };
            FlatButton bDetails = new FlatButton(T("Details", "Details"), null, BtnKind.Ghost);
            bDetails.Click += delegate { _cCheck.Collapsed = false; SelectPage(PgExtras); _exHost.Masonry(_exCards, 2); _exHost.ScrollTo(0, true); };
            _plHealth.AddRight(_bHealth); _plHealth.AddRight(bDetails);
            Dock3(_pages[PgPlayer], ph, _plHealth, _plHost);
            _plSave = new WinTimer(); _plSave.Interval = 700; _plSave.Tick += delegate { _plSave.Stop(); SavePlayer(); };

            BuildMe();

            // ---- Spielstart
            _cPlay = new Card(T("Spielstart", "Game start"), G.Play);
            _plMod = new SelectBox(null, Dpi.D(220));
            _plMod.SelectionChanged += delegate { if (_plLoading) return; _cfg.Set("PlayMod", _plMod.SelectedItem); _cfg.Save(); };
            _plWindowed = new ToggleSwitch(); _plWindowed.SetSilent(_cfg.B("PlayWindowed", false));
            _plWindowed.CheckedChanged += delegate { _cfg.Set("PlayWindowed", _plWindowed.Checked); _cfg.Save(); };
            _plIntro = new ToggleSwitch(); _plIntro.SetSilent(_cfg.B("SkipIntro", true));
            _plIntro.CheckedChanged += delegate { _cfg.Set("SkipIntro", _plIntro.Checked); _cfg.Save(); if (!_plLoading) { _plSave.Stop(); _plSave.Start(); } };
            _plAutoLogin = new ToggleSwitch(); _plAutoLogin.SetSilent(_cfg.B("AutoLogin", true));
            _plAutoLogin.CheckedChanged += delegate { _cfg.Set("AutoLogin", _plAutoLogin.Checked); _cfg.Save(); };
            FormRow rWin = new FormRow(T("Fenstermodus", "Windowed"), T("Startet BF2 im Fenster mit der Auflösung aus der Grafik-Karte.", "Starts BF2 in a window with the resolution from the graphics card."), _plWindowed, _plWindowed.Width, false);
            FormRow rIntro = new FormRow(T("Intro überspringen", "Skip intro"), T("Überspringt die Intro-Videos – beim Start über den Manager und dauerhaft im Profil.", "Skips the intro videos – when started from the manager and permanently in the profile."), _plIntro, _plIntro.Width, false);
            FormRow rLogin = new FormRow(T("Automatisch anmelden", "Log in automatically"), T("Übergibt das Profil beim Start (+playerName), damit man beim direkten Beitreten nicht im Anmeldemenü hängen bleibt. Nur für Offline-Profile.", "Passes the profile at start (+playerName) so joining directly does not stop at the login menu. Offline profiles only."), _plAutoLogin, _plAutoLogin.Width, false);
            ToggleSwitch tAuto = new ToggleSwitch(); tAuto.SetSilent(_cfg.B("AutoJoin", false));
            tAuto.CheckedChanged += delegate
            {
                _cfg.Set("AutoJoin", tAuto.Checked); _cfg.Save();
                if (tAuto.Checked) Toast(T("Sobald ein Server im LAN startet, tritt der Manager bei – fehlende Dateien holt er vorher.", "As soon as a server starts on the LAN, the manager joins – fetching missing files first."), ToastKind.Info);
            };
            FormRow rAuto = new FormRow(T("Automatisch beitreten", "Join automatically"), T("Startet ein Server im LAN, tritt der Manager von selbst bei: erst fehlende oder abweichende Mod-Dateien holen (ohne Rückfrage), dann Battlefield 2 starten.\nNur wenn genau ein Server läuft und dieser PC nicht selbst hostet. Pro Serverstart einmal – wer das Spiel beendet, wird nicht erneut hineingeholt.", "When a server starts on the LAN, the manager joins by itself: first fetch missing or different mod files (without asking), then start Battlefield 2.\nOnly when exactly one server runs and this PC does not host. Once per server start – if you quit the game you are not pulled back in."), tAuto, tAuto.Width, false);
            foreach (Control c in new Control[] { rAuto, rWin, rIntro, rLogin }) _cPlay.Controls.Add(c);
            _cPlay.Layouter = Stack(_cPlay, delegate { return new List<Control> { rAuto, rWin, rIntro, rLogin }; });

            // ---- Server im Netzwerk (Hauptansicht für Spieler)
            _cLan = new Card(T("Server im Netzwerk", "Servers on the network"), G.Globe);
            _lanEmpty = new Para(T("Suche nach Servern im Netzwerk …\nSobald ein Server läuft, erscheint er hier automatisch.", "Looking for servers on the network …\nAs soon as a server runs, it appears here automatically."));
            _lanEmpty.Color = Theme.Dim;
            FlatButton bSolo = new FlatButton(T("Spiel ohne Server starten", "Start game without server"), G.Play, BtnKind.Secondary);
            Theme.Tip.SetToolTip(bSolo, T("Startet Battlefield 2 im gewählten Mod und landet im Hauptmenü.", "Starts Battlefield 2 in the selected mod and opens the main menu."));
            bSolo.Click += delegate { FlushPlayer(); StartGame(null, 0, PlayMod); };
            FlatButton bManual = new FlatButton(T("Server nicht gefunden?", "Server not found?"), null, BtnKind.Ghost);
            bManual.Click += delegate { JoinManual(); };
            _plMod.Height = bSolo.Height;
            ButtonRow emptyRow = Buttons(bSolo, _plMod, bManual);
            _syncText = new Para(""); _syncText.Color = Theme.Text;
            _bSyncGo = new FlatButton(T("Dateien vom Server-PC holen", "Fetch files from server PC"), G.Down, BtnKind.Primary);
            _bSyncGo.Click += delegate { SyncFromHost(); };
            _bSyncCancel = new FlatButton(T("Abbrechen", "Cancel"), G.Close, BtnKind.Ghost);
            _bSyncCancel.Click += delegate { _syncCancel = true; };
            _syncButtons = Buttons(_bSyncGo, _bSyncCancel);
            foreach (Control c in new Control[] { _lanEmpty, emptyRow, _syncText, _syncButtons }) _cLan.Controls.Add(c);
            FlatButton bScan = new FlatButton(T("Aktualisieren", "Refresh"), G.Refresh, BtnKind.Ghost); bScan.Height = Dpi.D(30); bScan.Fit();
            Theme.Tip.SetToolTip(bScan, T("Serverliste aktualisieren\nDie Liste aktualisiert sich ohnehin alle paar Sekunden von selbst. Der Knopf leert sie und sucht sofort neu.", "Refresh server list\nThe list updates itself every few seconds anyway. This button clears it and searches right away."));
            bScan.Click += delegate { RescanServers(); };
            _cLan.Controls.Add(bScan); _cLan.HeaderControls.Add(bScan);
            Func<int, int> lanStack = Stack(_cLan, delegate
            {
                List<Control> l = new List<Control>();
                if (_lanRows.Count == 0) { l.Add(_lanEmpty); l.Add(emptyRow); }
                l.AddRange(_lanRows);
                if (_syncText.Text.Length > 0) l.Add(_syncText);
                bool showGo = !_syncBusy && _syncPlan != null && _syncPlan.Count > 0;
                _bSyncGo.Visible = showGo; _bSyncCancel.Visible = _syncBusy;
                foreach (Control b in new Control[] { _bSyncGo, _bSyncCancel }) if (!b.Visible) _syncButtons.Controls.Remove(b); else if (!_syncButtons.Controls.Contains(b)) _syncButtons.Controls.Add(b);
                if (showGo || _syncBusy) l.Add(_syncButtons);
                return l;
            });
            _cLan.Layouter = delegate (int w)
            {
                bScan.Location = new Point(w - Dpi.D(16) - bScan.Width, Dpi.D(9));
                _cLan.HeaderReserve = bScan.Width + Dpi.D(8);
                return lanStack(w);
            };

            // ---- Grafik
            _cGfx = new Card(T("Grafik", "Graphics"), G.Video);
            _plRes = new SelectBox(null, Dpi.D(200));
            _plRes.SelectionChanged += delegate { if (!_plLoading) { _plSave.Stop(); _plSave.Start(); } };
            _plQuality = new Segmented(T("Niedrig", "Low"), T("Mittel", "Medium"), T("Hoch", "High"));
            _plQuality.SelectedIndexChanged += delegate { if (!_plLoading) { _plQualityTouched = true; _plSave.Stop(); _plSave.Start(); } };
            _plView = new Slider(50, 100, 75);
            _plView.ValueChanged += delegate { _cGfx.Invalidate(); if (!_plLoading) { _plSave.Stop(); _plSave.Start(); } };
            _plReShade = new ToggleSwitch();
            _plReShade.CheckedChanged += delegate { if (!_plLoading) ToggleReShade(_plReShade.Checked); };
            FormRow rRes = new FormRow(T("Auflösung", "Resolution"), T("Auflösungen, die dein Bildschirm unterstützt.", "Resolutions your monitor supports."), _plRes, _plRes.Width, false);
            FormRow rQ = new FormRow(T("Qualität", "Quality"), T("Setzt Gelände, Modelle, Licht, Schatten, Effekte und Texturen gemeinsam. Leer = eigene Mischung aus dem Spielmenü.", "Sets terrain, models, lighting, shadows, effects and textures together. Empty = custom mix from the game menu."), _plQuality, _plQuality.Width, false);
            FormRow rView = new FormRow(T("Sichtweite", "View distance"), T("Anteil der Sichtweite (50–100 %). Mehr kostet Leistung.", "Share of the view distance (50–100 %). More costs performance."), _plView, Dpi.D(200), false);
            FormRow rRs = new FormRow("ReShade", T("ReShade an/aus\nBildfilter (Schärfe, Farben, Kantenglättung) nur auf diesem PC. Schaltet die d3d9.dll im Spielordner an oder aus.\n\nFriert BF2 ein oder stürzt ab: zuerst hier ausschalten.", "ReShade on/off\nImage filters (sharpness, colours, anti-aliasing) on this PC only. Switches the d3d9.dll in the game folder on or off.\n\nIf BF2 freezes or crashes: switch it off here first."), _plReShade, _plReShade.Width, false);
            FlatButton bRsHelp = new FlatButton(T("Einrichten …", "Set up …"), G.Info, BtnKind.Secondary); bRsHelp.Height = Dpi.D(30); bRsHelp.Fit();
            bRsHelp.Click += delegate { ReShadeHelp(); };
            FormRow rRsSetup = new FormRow(T("ReShade (optional)", "ReShade (optional)"), T("ReShade\nOptionale Bildfilter für einen schärferen, kräftigeren Look. Nicht nötig zum Spielen. „Einrichten“ zeigt, wie es geht.", "ReShade\nOptional image filters for a sharper, more vivid look. Not needed to play. “Set up” shows how."), bRsHelp, bRsHelp.Width, false);
            FlatButton bCache = new FlatButton(T("Shader-Cache leeren", "Clear shader cache"), G.Delete, BtnKind.Ghost);
            Theme.Tip.SetToolTip(bCache, T("Hilft bei Grafikfehlern nach Mod- oder Treiberwechsel. BF2 baut den Cache beim nächsten Start neu.", "Helps with graphics glitches after changing mods or drivers. BF2 rebuilds the cache at the next start."));
            bCache.Click += delegate { ClearShaderCache(); };
            ButtonRow gb = Buttons(bCache);
            foreach (Control c in new Control[] { rRes, rQ, rView, rRs, rRsSetup, gb }) _cGfx.Controls.Add(c);
            _cGfx.Layouter = Stack(_cGfx, delegate { return new List<Control> { rRes, rQ, rView, ReShadeAvailable ? (Control)rRs : rRsSetup, gb }; });

            // ---- Audio
            _cAudio = new Card("Audio", G.Mic);
            _plFx = new Slider(0, 100, 100); _plMusic = new Slider(0, 100, 33);
            _plSound = new Segmented(T("Niedrig", "Low"), T("Mittel", "Medium"), T("Hoch", "High"));
            _plEax = new ToggleSwitch();
            EventHandler aud = delegate { if (!_plLoading) { _plSave.Stop(); _plSave.Start(); } };
            _plFx.ValueChanged += aud; _plMusic.ValueChanged += aud; _plSound.SelectedIndexChanged += aud; _plEax.CheckedChanged += aud;
            FormRow rFx = new FormRow(T("Effekte", "Effects"), null, _plFx, Dpi.D(200), false);
            FormRow rMu = new FormRow(T("Musik", "Music"), null, _plMusic, Dpi.D(200), false);
            FormRow rSq = new FormRow(T("Soundqualität", "Sound quality"), null, _plSound, _plSound.Width, false);
            FormRow rEax = new FormRow("EAX", T("Raumklang-Effekte der Soundkarte (falls unterstützt).", "Sound card room effects (if supported)."), _plEax, _plEax.Width, false);
            foreach (Control c in new Control[] { rFx, rMu, rSq, rEax }) _cAudio.Controls.Add(c);
            _cAudio.Layouter = Stack(_cAudio, delegate { return new List<Control> { rFx, rMu, rSq, rEax }; });

            _plCards.Add(_cMe); _plCards.Add(_cLan);
            _profileCards.AddRange(new Card[] { _cPlay, _cGfx, _cAudio });
            foreach (Card c in _plCards) _plHost.Canvas.Controls.Add(c);
            _plHost.Resize += delegate { _plHost.Masonry(_plCards, 1); };
        }

        void RefreshPlayer()
        {
            _plLoading = true;
            try
            {
                List<string> mods = GameLocator.IsGameDir(GameDir) ? GameLocator.AllMods(GameDir) : new List<string>();
                _plMod.SetItems(mods, PlayMod);
                _profiles = Profiles.All();
                RefreshMe();
                LoadPlayerValues();
            }
            finally { _plLoading = false; }
            RefreshServers(true);
        }

        void LoadPlayerValues()
        {
            bool old = _plLoading; _plLoading = true;
            Bf2Profile p = PlayProfile;
            string vc = VideoCon;
            string res = Profiles.Get(vc, "VideoSettings", "setResolution") ?? "";
            List<string> modes = DisplayModes.List();
            if (res.Length > 0 && !modes.Contains(res)) modes.Insert(0, res);
            // Noch keine Grafikdatei (BF2 nie gestartet): aktuelle Bildschirmauflösung vorschlagen – gespeichert wird bei Änderung
            _plRes.SetItems(modes, res.Length > 0 ? res : DisplayModes.Current());
            int q = -1;
            for (int lvl = 0; lvl <= 2 && q < 0; lvl++)
            {
                bool all = true;
                foreach (string k in QualityKeys) if (Profiles.Get(vc, "VideoSettings", k) != lvl.ToString()) { all = false; break; }
                if (all) q = lvl;
            }
            _plQuality.SetSilent(q < 0 ? 1 : q);
            _plQualityTouched = false; _plQualityCustom = q < 0;
            _plView.SetSilent((int)Math.Round(ParseF(Profiles.Get(vc, "VideoSettings", "setViewDistanceScale"), 0.75) * 100));
            string ac = ProfileFile("Audio.con");
            _plFx.SetSilent((int)Math.Round(ParseF(Profiles.Get(ac, "AudioSettings", "setEffectsVolume"), 1) * 100));
            _plMusic.SetSilent((int)Math.Round(ParseF(Profiles.Get(ac, "AudioSettings", "setMusicVolume"), 0.33) * 100));
            string sq = (Profiles.Get(ac, "AudioSettings", "setSoundQuality") ?? "Medium").ToLowerInvariant();
            _plSound.SetSilent(sq == "low" ? 0 : (sq == "high" ? 2 : 1));
            _plEax.SetSilent(Profiles.Get(ac, "AudioSettings", "setEnableEAX") == "1");
            _plReShade.SetSilent(File.Exists(Path.Combine(GameDir, "d3d9.dll")));
            _plReShade.Enabled = File.Exists(Path.Combine(GameDir, "d3d9.dll")) || File.Exists(Path.Combine(GameDir, "d3d9.dll.off")) || Directory.Exists(Path.Combine(AppPaths.LanDir(GameDir), "ReShade"));
            bool any = p != null;
            foreach (Control c in new Control[] { _plRes, _plQuality, _plView, _plFx, _plMusic, _plSound, _plEax }) c.Enabled = any;
            if (_cGfx.Width > 0 && _page == PgExtras) _exHost.Masonry(_exCards, 2);
            _cGfx.SetNote(p == null ? T("erst ein Profil anlegen (Seite Spielen)", "create a profile first (Play page)") : "");
            if (p != null) _cGfx.SetNote(_plQualityCustom ? T("eigene Mischung", "custom mix") : "");
            _plLoading = old;
        }

        static double ParseF(string s, double def) { double d; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : def; }
        static string F(double d) { return d.ToString("0.##", CultureInfo.InvariantCulture); }

        void FlushPlayer() { if (_plSave.Enabled) { _plSave.Stop(); SavePlayer(); } }

        // Schreibt nur die Werte, die der Manager verwaltet; der Rest der Profildateien bleibt unverändert
        void SavePlayer()
        {
            Bf2Profile p = PlayProfile;
            if (p == null) return;
            if (Srv.GameRunning()) { Toast(T("BF2 läuft – Profil wird beim Beenden vom Spiel überschrieben. Bitte zuerst das Spiel schließen.", "BF2 is running – it overwrites the profile on exit. Please close the game first."), ToastKind.Warn); return; }
            try
            {
                if (!_profileBackedUp)
                {
                    List<string> files = new List<string>();
                    foreach (string f in new string[] { "Profile.con", "Video.con", "Audio.con", "General.con" }) files.Add(Path.Combine(p.Dir, f));
                    Util.Backup(files, DocsDir); _profileBackedUp = true;
                }
                Dictionary<string, string> g = new Dictionary<string, string>();
                g["setViewIntroMovie"] = _plIntro.Checked ? "0" : "1";
                Profiles.Set(Path.Combine(p.Dir, "General.con"), "GeneralSettings", g);
                Dictionary<string, string> vid = new Dictionary<string, string>();
                if (_plRes.SelectedItem != null) vid["setResolution"] = _plRes.SelectedItem;
                if (_plQualityTouched || !_plQualityCustom) foreach (string k in QualityKeys) vid[k] = _plQuality.SelectedIndex.ToString();
                vid["setViewDistanceScale"] = F(_plView.Value / 100.0);
                Profiles.Set(VideoCon, "VideoSettings", vid);
                Dictionary<string, string> au = new Dictionary<string, string>();
                au["setEffectsVolume"] = F(_plFx.Value / 100.0); au["setMusicVolume"] = F(_plMusic.Value / 100.0);
                au["setSoundQuality"] = "\"" + new string[] { "Low", "Medium", "High" }[Math.Max(0, _plSound.SelectedIndex)] + "\"";
                au["setEnableEAX"] = _plEax.Checked ? "1" : "0";
                Profiles.Set(ProfileFile("Audio.con"), "AudioSettings", au);
            }
            catch (Exception ex) { Toast(T("Profil nicht gespeichert: ", "Profile not saved: ") + ex.Message, ToastKind.Error); }
        }

        // ReShade: d3d9.dll an/aus (umbenennen); beim ersten Mal aus _LAN\ReShade installieren
        void ToggleReShade(bool on)
        {
            string dll = Path.Combine(GameDir, "d3d9.dll"), off = dll + ".off", src = Path.Combine(AppPaths.LanDir(GameDir), "ReShade");
            try
            {
                if (Srv.GameRunning()) throw new Exception(T("Bitte zuerst Battlefield 2 beenden.", "Please close Battlefield 2 first."));
                if (on)
                {
                    if (File.Exists(off)) File.Move(off, dll);
                    else if (!File.Exists(dll) && Directory.Exists(src))
                        foreach (string f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                        {
                            string t = Path.Combine(GameDir, f.Substring(src.Length).TrimStart('\\', '/'));
                            Directory.CreateDirectory(Path.GetDirectoryName(t));
                            File.Copy(f, t, true);
                        }
                    Toast(File.Exists(dll) ? T("ReShade aktiv. Im Spiel mit Pos1 öffnen.", "ReShade on. Open it in game with Home.") : T("Kein ReShade gefunden (_LAN\\ReShade).", "No ReShade found (_LAN\\ReShade)."), File.Exists(dll) ? ToastKind.Ok : ToastKind.Warn);
                }
                else if (File.Exists(dll)) { if (File.Exists(off)) File.Delete(off); File.Move(dll, off); Toast(T("ReShade aus.", "ReShade off."), ToastKind.Info); }
            }
            catch (UnauthorizedAccessException) { Toast(T("Keine Schreibrechte im Spielordner – PC-Check › Schreibrechte.", "No write access to the game folder – PC check › write access."), ToastKind.Error); }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
            bool old = _plLoading; _plLoading = true; _plReShade.SetSilent(File.Exists(dll)); _plLoading = old;
        }

        // Schalter nur, wenn es etwas zu schalten gibt: installiertes ReShade (auch ausgeschaltet) oder Paket im LAN-Ordner
        bool ReShadeAvailable
        {
            get { return File.Exists(Path.Combine(GameDir, "d3d9.dll")) || File.Exists(Path.Combine(GameDir, "d3d9.dll.off")) || Directory.Exists(Path.Combine(AppPaths.LanDir(GameDir), "ReShade")); }
        }

        void ReShadeHelp()
        {
            DarkDialog.Ask(this, T("ReShade einrichten", "Set up ReShade"),
                T("ReShade legt Filter über das fertige Bild: schärfer, kräftigere Farben, bessere Kantenglättung. Texturen und Modelle bleiben gleich – es ist ein aufpolierter Look, keine neue Grafik. Es kostet etwas Leistung und ist zum Spielen nicht nötig.\n\n" +
                  "So geht's:\n1. ReShade von der offiziellen ReShade-Website herunterladen und starten.\n2. BF2.exe im Spielordner auswählen.\n3. Als Schnittstelle „Direct3D 9“ wählen.\n4. Effekte wählen, z. B. SMAA, LumaSharpen, Vibrance.\n5. Manager neu öffnen – hier erscheint dann der Schalter an/aus.\nIm Spiel öffnet die Pos1-Taste das ReShade-Menü.\n\n" +
                  "⚠ Friert BF2 ein oder stürzt ab, ReShade hier zuerst ausschalten. Manche Online-Server mit Anti-Cheat beanstanden ReShade; im LAN ist das kein Thema.",
                  "ReShade puts filters over the finished image: sharper, more vivid colours, better anti-aliasing. Textures and models stay the same – it is a polished look, not new graphics. It costs some performance and is not needed to play.\n\n" +
                  "How to:\n1. Download ReShade from the official ReShade website and run it.\n2. Select BF2.exe in the game folder.\n3. Choose “Direct3D 9” as the rendering API.\n4. Pick effects, e.g. SMAA, LumaSharpen, Vibrance.\n5. Reopen the manager – the on/off switch then appears here.\nIn game the Home key opens the ReShade menu.\n\n" +
                  "⚠ If BF2 freezes or crashes, switch ReShade off here first. Some online servers with anti-cheat object to ReShade; on LAN this does not matter."),
                0, false, T("Schließen", "Close"));
        }

        void ClearShaderCache()
        {
            if (Srv.GameRunning()) { Toast(T("Bitte zuerst Battlefield 2 beenden.", "Please close Battlefield 2 first."), ToastKind.Warn); return; }
            int n = 0;
            try
            {
                string root = Path.Combine(AppPaths.Docs, "Battlefield 2", "mods");
                if (Directory.Exists(root))
                    foreach (string d in Directory.GetDirectories(root))
                    {
                        string c = Path.Combine(d, "cache");
                        if (Directory.Exists(c)) { Directory.Delete(c, true); n++; }
                    }
                Toast(n > 0 ? T("Shader-Cache geleert.", "Shader cache cleared.") : T("Kein Cache vorhanden.", "No cache present."), ToastKind.Ok);
            }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
        }

        // Kompatibilität eines Servers mit diesem PC: Mod vorhanden und gleiche Mod-Version
        string Compat(LanHost h, out bool ok)
        {
            ok = false;
            if (!GameLocator.IsGameDir(GameDir)) return T("Spielordner fehlt", "Game folder missing");
            if (!Directory.Exists(Path.Combine(GameDir, "mods", h.Mod))) return T("Mod „", "Mod “") + h.Mod + T("“ fehlt – „Beitreten“ holt ihn vom Server-PC", "” missing – “Join” fetches it from the server PC");
            // Neuere Server-PCs melden eine Prüfsumme über alle Mod-Dateien: genauer als die Versionsnummer
            if (!string.IsNullOrEmpty(h.Fp) && !h.Self)
            {
                string mine = ModPrints.Get(GameDir, h.Mod);
                if (mine == null) return T("Prüfe Mod-Dateien …", "Checking mod files …");
                if (mine != h.Fp) return T("Dateien weichen vom Server-PC ab – „Beitreten“ gleicht ab", "Files differ from the server PC – “Join” syncs them");
                ok = true; return T("Kompatibel (gleiche Dateien)", "Compatible (same files)");
            }
            string lv = ModInfo.Read(GameDir, h.Mod).Version;
            if (!string.IsNullOrEmpty(h.ModVersion) && !string.IsNullOrEmpty(lv) && lv != h.ModVersion) return T("Andere Mod-Version (hier ", "Different mod version (here ") + lv + T(", Server ", ", server ") + h.ModVersion + ")";
            ok = true; return T("Kompatibel", "Compatible");
        }

        void JoinManual()
        {
            string ip = DarkDialog.Input(this, T("Server nicht gefunden?", "Server not found?"),
                T("Normalerweise erscheinen Server automatisch. Wenn nicht: auf beiden PCs Einstellungen › Dieser PC › „Alles beheben“ (Firewall). Bei VPN-LANs (Radmin, ZeroTier, Hamachi) hier die IP-Adresse des Server-PCs eingeben:", "Servers normally appear automatically. If not: on both PCs Settings › This PC › “Fix all” (firewall). For VPN LANs (Radmin, ZeroTier, Hamachi) enter the server PC's IP address here:"), _cfg.S("LastManualIp", ""));
            if (string.IsNullOrEmpty(ip) || ip.Trim().Length == 0) return;
            ip = ip.Trim(); _cfg.Set("LastManualIp", ip); _cfg.Save();
            int port = 16567, c = ip.LastIndexOf(':');
            if (c > 0) { port = Util.ToInt(ip.Substring(c + 1), 16567); ip = ip.Substring(0, c); }
            FlushPlayer(); StartGame(ip, port, PlayMod);
        }

        // Zustand dieses PCs als Leiste über der Serverliste (grün, oder gelb mit „Beheben“)
        void RefreshPlayHealth()
        {
            if (_plHealth == null) return;
            // Alles in Ordnung: keine Leiste, nur ein kleiner Hinweis in der Spielerkarte. Probleme: gelbe Leiste mit „Beheben“.
            bool ok = _checks.Count > 0 && _checkBad == 0;
            _plHealth.Visible = _checks.Count > 0 && !ok;
            _plHealth.Tint = Theme.Amber; _plHealth.Glyph = G.Warn;
            if (!ok)
            {
                List<string> t = new List<string>();
                foreach (CheckItem c in _checks) if (c.NeedsFix) t.Add(c.Title);
                _plHealth.Text2 = (t.Count == 1 ? T("1 Punkt offen: ", "1 open item: ") : t.Count + T(" Punkte offen: ", " open items: ")) + string.Join(", ", t.ToArray());
            }
            _plHealth.PerformLayout(); _plHealth.Invalidate();
            if (_cMe != null) { _cMe.NoteColor = Theme.Green; _cMe.SetNote(ok ? T("✓ Dieser PC ist bereit", "✓ This PC is ready") : ""); }
        }

        // Liste der laufenden Server neu aufbauen, wenn sich etwas geändert hat
        void RefreshServers(bool force)
        {
            if (_cLan == null) return;
            List<LanHost> all = _fakeHosts ?? (_lanListen != null ? _lanListen.Hosts() : new List<LanHost>());
            List<LanHost> hosts = new List<LanHost>();
            foreach (LanHost h in all) if (h.Running) hosts.Add(h);
            // Eigener Server immer anzeigen – auch wenn die eigene Bekanntmachung nicht zurückkommt (Firewall, VPN-Adapter)
            if (_fakeHosts == null && _running)
            {
                string id = InstanceId; bool have = false;
                foreach (LanHost h in hosts) if (h.Id == id) { h.Self = true; have = true; }
                if (!have) hosts.Insert(0, OwnHost(_lastLive));
            }
            StringBuilder sig = new StringBuilder();
            foreach (LanHost h in hosts) sig.Append(h.Key).Append(h.Map).Append(h.Humans).Append(h.Bots).Append(h.Mod).Append(h.ModVersion).Append(h.Version).Append(h.Fp)
                .Append(string.IsNullOrEmpty(h.Fp) || h.Self ? "" : ModPrints.Get(GameDir, h.Mod)).Append(';');
            if (!force && sig.ToString() == _lanSig) return;
            _lanSig = sig.ToString();
            foreach (Control c in _lanRows) { _cLan.Controls.Remove(c); c.Dispose(); }
            _lanRows.Clear();
            foreach (LanHost h in hosts)
            {
                LanHost host = h;
                bool ok; string compat = Compat(host, out ok);
                Para head = new Para(host.Name + (host.Self ? T("  ·  dieser PC", "  ·  this PC") : "")); head.Font = Theme.Head; head.Color = Theme.Text;
                string size = host.Size > 0 ? "  ·  " + host.Size + T("er Map", "-player map") : "";
                string mode = host.Mode == "coop" ? "Coop" : (host.Mode == "cq" ? "Conquest" : host.Mode);
                Para info = new Para(Util.Pretty(host.Map) + size + "  ·  " + mode + "\n" +
                    T("Spieler ", "Players ") + host.Humans + " / " + host.Max + (host.Bots > 0 ? "  ·  Bots " + host.Bots : "") + "  ·  Mod " + host.Mod + (string.IsNullOrEmpty(host.ModVersion) ? "" : " " + host.ModVersion));
                info.Color = Theme.Dim;
                Para st = new Para((ok ? "✓ " : "⚠ ") + compat); st.Color = ok ? Theme.Green : Theme.Amber; st.Font = Theme.SmallB;
                ButtonRow btns = new ButtonRow();
                FlatButton bj = new FlatButton(T("Beitreten", "Join"), G.Play, BtnKind.Primary);
                bj.Enabled = GameLocator.IsGameDir(GameDir);
                Theme.Tip.SetToolTip(bj, T("Beitreten\nPasst der Mod, startet Battlefield 2 direkt auf diesem Server. Sonst holt der Manager vorher nur die fehlenden oder abweichenden Dateien vom Server-PC.", "Join\nIf the mod matches, Battlefield 2 starts straight on this server. Otherwise the manager first fetches only the missing or different files from the server PC."));
                bj.Click += delegate { FlushPlayer(); JoinHost(host); };
                btns.Add(bj);
                if (_cfg.B("ShowCompare", false))
                {
                    FlatButton bc = new FlatButton(T("Dateien vom Server holen", "Get files from server"), G.Down, ok ? BtnKind.Ghost : BtnKind.Secondary);
                    Theme.Tip.SetToolTip(bc, T("Vergleicht die Mod-Dateien mit dem Server-PC und holt nur, was fehlt oder anders ist.", "Compares the mod files with the server PC and fetches only what is missing or different."));
                    bc.Click += delegate { CompareWith(host); };
                    btns.Add(bc);
                }
                if (Updater.Compare(host.Version, AppInfo.Version) > 0)
                {
                    FlatButton bu = new FlatButton(T("Manager ", "Manager ") + host.Version + T(" übernehmen", " – update"), G.Down, BtnKind.Ghost);
                    bu.Click += delegate { InstallUpdate(Lan.Url(host, "/manager"), Lan.Url(host, "/manager.sha256"), host.Version, T("Vom Server-PC im LAN übernommen.", "Taken over from the server PC on LAN.")); };
                    btns.Add(bu);
                }
                foreach (Control c in new Control[] { head, info, st, btns }) { _lanRows.Add(c); _cLan.Controls.Add(c); }
            }
            _cLan.SetNote(hosts.Count == 0 ? T("sucht laufend …", "searching …") : (hosts.Count == 1 ? T("1 Server", "1 server") : hosts.Count + T(" Server", " servers")));
            if (_page == PgPlayer) _plHost.Masonry(_plCards, 1);
        }
    }
}
