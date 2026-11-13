// Hauptfenster: Ansagen im Spiel-Chat und Ablauf (Regeln pro Map, Mod-Reihenfolge) - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace BF2Coop
{
    public partial class MainForm
    {
        // ================================================================== Ansagen
        // Der Server-PC schreibt Ansagen in den Chat; die Bekanntmachung trägt den Mod der nächsten Playlist-Runde.
        string _annId = "", _annText = "", _nextModAnn = "", _annMapSeen = null;
        readonly Dictionary<string, string> _heard = new Dictionary<string, string>();
        readonly Dictionary<string, string> _preSynced = new Dictionary<string, string>();

        bool AnnounceOn { get { return _cfg.B("AutoAnnounce", true); } }

        void Announce(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = text.Replace("\"", "'").Replace("|", "/").Replace("\n", " ");
            if (_running && _module) { try { CmdChannel.Send(ModSettingsDir, "game.sayAll \"" + text + "\""); } catch { } }
            _annId = DateTime.Now.Ticks.ToString("x"); _annText = text;
            if (_lanFiles != null && _lanFiles.Error == null) SendBeacon(_lastLive);
        }

        // Neue Map auf dem eigenen Server: Map und ggf. Spaßregeln ansagen
        void AnnounceTick()
        {
            if (!_running || !_module) { _annMapSeen = null; return; }
            if (string.IsNullOrEmpty(_liveMap) || _liveMap == _annMapSeen) return;
            _annMapSeen = _liveMap;
            if (!AnnounceOn) return;
            string text = T("Neue Runde: ", "New round: ") + Util.Pretty(_liveMap) + ".";
            string fun = FunRulesOf(_runMod ?? _mod);
            if (fun.Length > 0) text += T(" Spaßrunde! ", " Fun round! ") + fun + ".";
            Announce(text);
        }

        string FunRulesOf(string mod)
        {
            try
            {
                string f = Path.Combine(Path.Combine(Path.Combine(GameDir, "mods"), mod), FunMods.RecipeFile);
                return File.Exists(f) ? FunPack.Describe(FunOptions.FromDict(FunMods.ParseRecipe(File.ReadAllText(f)))) : "";
            }
            catch { return ""; }
        }

        // Spieler-PC: kündigt der Server einen Mod-Wechsel an, den Mod schon vorher abgleichen
        void HearTick()
        {
            if (_lanListen == null || _fakeHosts != null) return;
            foreach (LanHost h in _lanListen.Hosts())
            {
                if (h.Self) continue;
                string last;
                bool known = _heard.TryGetValue(h.Key, out last);
                if (!string.IsNullOrEmpty(h.AnnId) && h.AnnId != last)
                {
                    _heard[h.Key] = h.AnnId;
                    }
                else if (!known) _heard[h.Key] = h.AnnId;
                string done;
                if (!string.IsNullOrEmpty(h.NextMod) && (!_preSynced.TryGetValue(h.Key, out done) || done != h.NextMod) && AutoJoinOn && !_syncBusy && !_running)
                {
                    _preSynced[h.Key] = h.NextMod;
                    PreSync(h, h.NextMod);
                }
            }
        }

        // Im Hintergrund abgleichen, ohne das Spiel zu starten (läuft BF2, werden nur fehlende Dateien geholt – die laufende Runde stört das nicht)
        void PreSync(LanHost h, string mod)
        {
            if (!FunMods.ValidModName(mod)) return;
            LanHost t = new LanHost();
            t.Ip = h.Ip; t.HttpPort = h.HttpPort; t.Name = h.Name; t.Mod = mod; t.GamePort = h.GamePort; t.Id = h.Id;
            _syncBusy = true; _syncCancel = false;
            SetSync(T("Nächste Runde mit „", "Next round with “") + mod + T("“ – gleiche im Hintergrund ab …", "” – syncing in the background …"));
            ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null; ModSync.Result res = null;
                try { res = ModSync.Run(t, GameDir, delegate (string s) { try { BeginInvoke((MethodInvoker)delegate { SetSync(s); }); } catch { } }, delegate { return _syncCancel; }); }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _syncBusy = false;
                        SetSync(err != null ? "⚠ " + T("Vorab-Abgleich nicht möglich: ", "Pre-sync failed: ") + err + T(" – beim Beitreten wird es erneut versucht.", " – joining tries again.")
                                            : "✓ " + T("Bereit für die nächste Runde („", "Ready for the next round (“") + mod + T("“) – ", "”) – ") + res.Files + T(" Dateien geholt.", " files fetched."));
                        RefreshStatus();
                    });
                }
                catch { }
            });
        }

        // ================================================================== Ablauf
        // Die Rotation eines Mods läuft wie immer. Braucht die nächste Map andere Spaßregeln (oder ist die Rotation zu Ende
        // und in der Mod-Leiste folgt ein anderer Mod), startet der Manager den Server mit dem passenden Aufsatz neu –
        // die Maplist enthält dabei nur die folgenden Maps mit denselben Regeln.
        string _runMod;                                   // Mod-Pfad, mit dem der Server gerade läuft (Basis-Mod oder Aufsatz)
        List<int> _runList = new List<int>(); int _runPos, _runRounds = -1;
        DateTime _restartAt = DateTime.MinValue; int _restartIdx; string _restartModTo;
        Panel _flowBar; ButtonRow _flowRow; TextLabel _flowLabel;

        List<string> ModOrder
        {
            get
            {
                List<string> r = new List<string>();
                foreach (string m in _cfg.List("ModOrder")) if (_mods.Contains(m) && !r.Contains(m)) r.Add(m);
                return r;
            }
        }

        void SaveModOrder(List<string> l) { _cfg.Set("ModOrder", l); _cfg.Save(); RefreshFlowBar(); }

        bool HasMapRules(List<MapEntry> maps)
        {
            foreach (MapEntry m in maps) { Dictionary<string, string> r = FunSets.RulesOf(m); if (r != null && !FunOptions.FromDict(r).IsNeutral()) return true; }
            return false;
        }

        bool FlowActive { get { List<string> o = ModOrder; return HasMapRules(_st.Maps) || (o.Count > 1 && o.Contains(_mod)); } }

        string PackFor(MapEntry m, string baseMod)
        {
            Dictionary<string, string> r = FunSets.RulesOf(m);
            if (r == null) return baseMod;
            FunOptions o = FunOptions.FromDict(r);
            return o.IsNeutral() ? baseMod : FunPack.PackName(baseMod, o);
        }

        // Aufsatz für diese Map bereitstellen (baut ihn bei Bedarf; gleiche Regeln = gleicher Ordner)
        string EnsurePack(MapEntry m)
        {
            string name = PackFor(m, _mod);
            if (name == _mod) return name;
            List<string> maps = MapsOfPack(name, _mod, _st.Maps);
            if (!PackReady(GameDir, name, maps)) FunPack.Build(GameDir, _mod, FunOptions.FromDict(FunSets.RulesOf(m)), name, maps);
            return name;
        }

        // Maps der Rotation, die mit diesem Aufsatz laufen (fÃ¼r â€žOhne Fahrzeugeâ€œ braucht jede eine eigene server.zip)
        List<string> MapsOfPack(string pack, string baseMod, List<MapEntry> rot)
        {
            List<string> r = new List<string>();
            foreach (MapEntry e in rot) if (PackFor(e, baseMod) == pack && !r.Contains(e.Name.ToLowerInvariant())) r.Add(e.Name.ToLowerInvariant());
            return r;
        }

        static bool PackReady(string gameDir, string pack, List<string> maps)
        {
            string f = Path.Combine(Path.Combine(Path.Combine(gameDir, "mods"), pack), FunMods.RecipeFile);
            if (!File.Exists(f)) return false;
            Dictionary<string, string> rc = FunMods.ParseRecipe(File.ReadAllText(f));
            if (!FunOptions.FromDict(rc).NoVehicles) return true;
            string have; rc.TryGetValue("VehicleFreeMaps", out have);
            List<string> h = new List<string>((have ?? "").ToLowerInvariant().Split(new char[] {   }, StringSplitOptions.RemoveEmptyEntries));
            foreach (string mm in maps) if (!h.Contains(mm)) return false;
            return true;
        }

        // Ab Position k die folgenden Maps mit demselben Aufsatz (ohne Regeln und ohne weitere Mods: die ganze Rotation)
        List<int> SegmentFrom(int k)
        {
            List<int> r = new List<int>(); int n = _st.Maps.Count;
            if (n == 0) return r;
            if (!FlowActive) { for (int i = 0; i < n; i++) r.Add((k + i) % n); return r; }
            string pack = PackFor(_st.Maps[k], _mod);
            for (int i = k; i < n && PackFor(_st.Maps[i], _mod) == pack; i++) r.Add(i);
            return r;
        }

        // Startet den Server mit der Map an Position k der Rotation (vorher speichern)
        bool StartFlow(int k)
        {
            if (_st.Maps.Count == 0) return false;
            k = Math.Max(0, Math.Min(k, _st.Maps.Count - 1));
            string pack;
            try { pack = EnsurePack(_st.Maps[k]); }
            catch (Exception ex) { Toast(T("Spaßregeln konnten nicht vorbereitet werden: ", "Fun rules could not be prepared: ") + ex.Message, ToastKind.Error); Util.Log(AppPaths.Dir, ex.ToString()); return false; }
            List<int> seg = SegmentFrom(k);
            List<MapEntry> maps = new List<MapEntry>(); foreach (int i in seg) maps.Add(_st.Maps[i]);
            try { Maplist.Write(Path.Combine(CfgDir, "maplist.con"), maps); } catch (Exception ex) { Toast(ex.Message, ToastKind.Error); return false; }
            _runList = seg; _runPos = 0; _runRounds = -1; _nextModAnn = "";
            if (!StartServerProcess(pack)) return false;
            if (FlowActive) PreparePacksAsync();
            return true;
        }

        // Alle weiteren Aufsätze der Rotation im Hintergrund bauen (schnellere Wechsel, Mitspieler können vorab abgleichen); Unbenutztes aufräumen
        void PreparePacksAsync()
        {
            string game = GameDir, baseMod = _mod;
            List<KeyValuePair<string, FunOptions>> todo = new List<KeyValuePair<string, FunOptions>>(); HashSet<string> keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<string>> mapsOf = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (MapEntry m in _st.Maps)
            {
                string p = PackFor(m, baseMod); if (p == baseMod || !keep.Add(p)) continue;
                todo.Add(new KeyValuePair<string, FunOptions>(p, FunOptions.FromDict(FunSets.RulesOf(m))));
                mapsOf[p] = MapsOfPack(p, baseMod, _st.Maps);
            }
            string running = _runMod;
            ThreadPool.QueueUserWorkItem(delegate
            {
                foreach (KeyValuePair<string, FunOptions> kv in todo)
                    try { if (kv.Key != running && !PackReady(game, kv.Key, mapsOf[kv.Key])) FunPack.Build(game, baseMod, kv.Value, kv.Key, mapsOf[kv.Key]); } catch { }
                try { FunPack.Cleanup(game, baseMod, keep); } catch { }
            });
        }

        // Alle 2 s: Rundenende erkennen und ggf. Neustart mit anderen Regeln / anderem Mod auslösen
        void FlowTick()
        {
            if (_restartAt != DateTime.MinValue && DateTime.Now >= _restartAt) { _restartAt = DateTime.MinValue; DoFlowRestart(); return; }
            if (!_running || !_module || _restartAt != DateTime.MinValue) return;
            int rounds = CountRounds();
            if (_runRounds < 0) { _runRounds = rounds; return; }
            if (rounds <= _runRounds) return;
            _runRounds = rounds;
            OnRoundEnd();
        }

        void OnRoundEnd()
        {
            int n = _st.Maps.Count;
            if (n == 0 || _runList.Count == 0) return;
            if (!FlowActive) { _runPos = (_runPos + 1) % _runList.Count; RefreshFlowBar(); return; }
            int cur = _runPos < _runList.Count ? _runList[_runPos] : 0, next = cur + 1;
            if (next >= n)
            {
                string nm = NextModInOrder();
                if (nm != null) { ScheduleRestart(0, nm); return; }
                next = 0;
            }
            string want = PackFor(_st.Maps[next], _mod);
            int bf2Next = (_runPos + 1) % _runList.Count;
            if (want == _runMod && _runList[bf2Next] == next) { _runPos = bf2Next; RefreshFlowBar(); return; }
            ScheduleRestart(next, null);
        }

        string NextModInOrder()
        {
            List<string> o = ModOrder;
            int i = o.IndexOf(_mod);
            if (o.Count < 2 || i < 0) return null;
            return o[(i + 1) % o.Count];
        }

        int CountRounds()
        {
            try
            {
                string f = Path.Combine(ModSettingsDir, "coop_rounds.txt"); int n = 0;
                if (File.Exists(f)) foreach (string l in File.ReadAllLines(f)) if (l.StartsWith("round|")) n++;
                return n;
            }
            catch { return 0; }
        }

        void ScheduleRestart(int idx, string mod)
        {
            _restartIdx = idx; _restartModTo = mod; _restartAt = DateTime.Now.AddSeconds(8);
            string text;
            if (mod != null) { text = T("Weiter mit ", "Next up: ") + mod + T(" – der Server startet gleich neu, danach einfach wieder beitreten.", " – the server restarts in a moment, then just join again."); _nextModAnn = mod; }
            else
            {
                MapEntry m = _st.Maps[idx];
                Dictionary<string, string> mr = FunSets.RulesOf(m); string set = FunSets.SetNameOf(m);
                string fun = mr != null ? (set != null ? set + ": " : "") + FunPack.Describe(FunOptions.FromDict(mr)) : "";
                _nextModAnn = PackFor(m, _mod);
                text = T("Nächste Runde: ", "Next round: ") + Util.Pretty(m.Name) + (fun.Length > 0 ? T(" – Spaßregeln: ", " – fun rules: ") + fun : T(" – normale Regeln", " – normal rules"))
                     + T(". Kurzer Server-Neustart, danach einfach wieder beitreten.", ". Short server restart, then just join again.");
                // Aufsatz jetzt schon bauen, damit der Neustart schnell geht und Mitspieler abgleichen können
                try { EnsurePack(m); } catch { }
            }
            Announce(text);
            Toast(T("Ablauf: ", "Schedule: ") + text, ToastKind.Info);
        }

        void DoFlowRestart()
        {
            _userStopped = true; Srv.Kill(); Thread.Sleep(1000);
            if (_restartModTo != null && _restartModTo != _mod)
            {
                _selMod.SetSilent(_restartModTo); SwitchContext(_restartModTo, _coop);
                if (_st.Maps.Count == 0) { Toast(T("„", "“") + _restartModTo + T("“ hat keine Rotation – bitte Maps hinzufügen.", "” has no rotation – please add maps."), ToastKind.Warn); RefreshLive(); return; }
            }
            AutoSaveNow(); SaveAll(true);
            if (StartFlow(_restartIdx)) { _crashed = false; RefreshLive(); }
            RefreshFlowBar();
        }

        // ---- Spiel folgt dem Server: läuft der Server, dem wir beigetreten sind, jetzt mit einem anderen Mod (Spaßregeln,
        // nächster Mod), kann das laufende BF2 nicht mit – Spiel schließen, abgleichen, neu beitreten. Nur mit „Automatisch beitreten“.
        string _joinedIp, _joinedMod; DateTime _joinedAt = DateTime.MinValue;

        void FollowTick()
        {
            if (_joinedIp == null || _joinedMod == null || !AutoJoinOn || _syncBusy || _fakeHosts != null) return;
            if ((DateTime.Now - _joinedAt).TotalSeconds < 20 || !Srv.GameRunning()) return;
            LanHost target = null;
            if (_running && Srv.LanAddresses().Contains(_joinedIp) || _joinedIp == "127.0.0.1")
            {
                // Eigener Server (Host spielt mit): erst folgen, wenn die neue Map geladen ist
                if (_running && _module && _runMod != null && !string.Equals(_runMod, _joinedMod, StringComparison.OrdinalIgnoreCase) && _restartAt == DateTime.MinValue)
                    target = OwnHost(_lastLive);
            }
            else if (_lanListen != null)
                foreach (LanHost h in _lanListen.Hosts())
                    if (h.Ip == _joinedIp && h.Running && !string.IsNullOrEmpty(h.Map) && !string.Equals(h.Mod, _joinedMod, StringComparison.OrdinalIgnoreCase)) { target = h; break; }
            if (target == null) return;
            Toast(T("Der Server spielt jetzt „", "The server now runs “") + target.Mod + T("“ – Spiel wird neu gestartet …", "” – restarting the game …"), ToastKind.Info);
            _joinedIp = null;
            Srv.KillGame();
            if (target.Self) StartGame(target.Ip, target.GamePort, target.Mod);
            else JoinHost(target, true);
        }

        // ---- Leiste über der Rotation: Mods in Spielreihenfolge
        void BuildFlowBar()
        {
            _flowBar = new Panel(); _flowBar.BackColor = Theme.Bg; _flowBar.Height = Dpi.D(52); _flowBar.Dock = DockStyle.Top;
            _flowLabel = new TextLabel(T("Reihenfolge", "Order"), Theme.BodyB); _flowLabel.Color = Theme.Dim;
            Theme.Tip.SetToolTip(_flowLabel, T("Mods in Spielreihenfolge\nNach der Rotation eines Mods geht es mit dem nächsten weiter (der Server startet dafür kurz neu). Klick auf einen Mod zeigt seine Rotation zum Bearbeiten. Rechtsklick: verschieben oder entfernen.", "Mods in play order\nAfter one mod's rotation the next one follows (the server restarts briefly). Click a mod to edit its rotation. Right-click: move or remove."));
            _flowRow = new ButtonRow(); _flowRow.BackColor = Theme.Bg;
            _flowBar.Controls.Add(_flowLabel); _flowBar.Controls.Add(_flowRow);
            _flowBar.Resize += delegate { LayoutFlowBar(); };
            RefreshFlowBar();
        }

        void LayoutFlowBar()
        {
            if (_flowBar == null) return;
            int pad = Dpi.D(24);
            _flowLabel.Location = new Point(pad, Dpi.D(10));
            int x = _flowLabel.Right + Dpi.D(12), w = Math.Max(Dpi.D(100), _flowBar.Width - x - pad);
            int h = _flowRow.LayoutFor(w); _flowRow.SetBounds(x, Dpi.D(6), w, h);
            int need = h + Dpi.D(12);
            if (_flowBar.Height != need) _flowBar.Height = need;
        }

        void RefreshFlowBar()
        {
            if (_flowRow == null) return;
            foreach (Control c in _flowRow.Controls) c.Dispose();
            _flowRow.Controls.Clear();
            List<string> order = ModOrder;
            if (!order.Contains(_mod) && _mod.Length > 0) order.Insert(0, _mod);
            for (int i = 0; i < order.Count; i++)
            {
                string m = order[i]; int ix = i;
                int n = m == _mod ? _st.Maps.Count : MapCountOf(m);
                bool run = _running && _runMod != null && (m == _runMod || FunMods.BaseOf(GameDir, _runMod) == m);
                FlatButton b = new FlatButton((order.Count > 1 ? (i + 1) + ". " : "") + (run ? "▶ " : "") + m + "  ·  " + n + " Maps", null, m == _mod ? BtnKind.Primary : BtnKind.Secondary);
                b.Height = Dpi.D(32); b.Fit();
                Theme.Tip.SetToolTip(b, m == _mod ? T("Wird gerade bearbeitet. Rechtsklick: verschieben oder entfernen.", "Being edited. Right-click: move or remove.") : T("Klick: Rotation dieses Mods bearbeiten. Rechtsklick: verschieben oder entfernen.", "Click: edit this mod's rotation. Right-click: move or remove."));
                b.Click += delegate { if (m != _mod) { _selMod.SetSilent(m); SwitchContext(m, _coop); } };
                ContextMenuStrip cm = Theme.Menu();
                ToolStripMenuItem left = new ToolStripMenuItem(T("Früher spielen", "Play earlier")), right = new ToolStripMenuItem(T("Später spielen", "Play later")), rem = new ToolStripMenuItem(T("Aus der Reihenfolge nehmen", "Remove from order"));
                left.Enabled = ix > 0; right.Enabled = ix < order.Count - 1; rem.Enabled = order.Count > 1;
                left.Click += delegate { List<string> l = CurrentOrder(); string x = l[ix]; l.RemoveAt(ix); l.Insert(ix - 1, x); SaveModOrder(l); };
                right.Click += delegate { List<string> l = CurrentOrder(); string x = l[ix]; l.RemoveAt(ix); l.Insert(ix + 1, x); SaveModOrder(l); };
                rem.Click += delegate { List<string> l = CurrentOrder(); l.RemoveAt(ix); SaveModOrder(l.Count > 1 ? l : new List<string>()); };
                cm.Items.AddRange(new ToolStripItem[] { left, right, rem });
                b.ContextMenuStrip = cm;
                _flowRow.Add(b);
            }
            ContextMenuStrip add = Theme.Menu();
            foreach (string m in _mods)
            {
                if (order.Contains(m)) continue;
                string mm = m; ToolStripMenuItem it = new ToolStripMenuItem(m);
                it.Click += delegate { List<string> l = CurrentOrder(); l.Add(mm); SaveModOrder(l); };
                add.Items.Add(it);
            }
            if (add.Items.Count > 0)
            {
                FlatButton bAdd = new FlatButton(T("Danach anderen Mod spielen", "Then play another mod"), G.Add, BtnKind.Ghost);
                bAdd.Height = Dpi.D(32); bAdd.Fit(); bAdd.DropMenu = add; bAdd.MenuOnly = true;
                Theme.Tip.SetToolTip(bAdd, T("Hängt einen weiteren Mod an. Nach der Rotation dieses Mods startet der Server mit dem nächsten Mod neu.", "Appends another mod. After this mod's rotation the server restarts with the next mod."));
                _flowRow.Add(bAdd);
            }
            LayoutFlowBar();
        }

        List<string> CurrentOrder() { List<string> o = ModOrder; if (!o.Contains(_mod) && _mod.Length > 0) o.Insert(0, _mod); return o; }

        int MapCountOf(string mod)
        {
            try { CoopState s = CoopTxt.Read(Path.Combine(Path.Combine(Path.Combine(Path.Combine(GameDir, "mods"), mod), "settings"), "coop_server.txt")); return s == null ? 0 : s.Maps.Count; }
            catch { return 0; }
        }
    }
}