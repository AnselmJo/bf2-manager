// Hauptfenster: Seite Live - BF2 Manager. C# 5 (.NET Framework 4.x).
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
        FlatButton _bHelp, _bBotsNow; Para _cmdHint, _cmdResult;
        class PendingCmd { public string Cmd; public DateTime At; }
        readonly List<PendingCmd> _pending = new List<PendingCmd>();
        List<string[]> _curPlayers = new List<string[]>(); string _botsNowId = "";
        // ================================================================== Seite Live
        void BuildLive()
        {
            PageHeader ph = NewPage(3, T("Server live", "Server live"), T("Spieler, Map und Aktionen – Änderungen an Rotation und Bots wirken ab der nächsten Map", "Players, map and actions – changes to rotation and bots apply from the next map"));
            FlatButton bCfg = new FlatButton(T("Rotation & Bots bearbeiten", "Edit rotation & bots"), G.Edit, BtnKind.Secondary);
            bCfg.Click += delegate { _forceConfig = true; SelectPage(PgMaps); };
            ph.AddRight(bCfg);
            _liveHost = new ScrollHost(); _liveHost.Dock = DockStyle.Fill;
            Dock3(_pages[3], ph, null, _liveHost);

            _cOffline = new Card(null, null);
            Para po = new Para(T("Der Server ist offline. Nach dem Start siehst du hier Status, Spieler, Aktionen und das Log.", "The server is offline. After starting it you will see status, players, actions and the log here."));
            po.Color = Theme.Dim;
            _bStartBig = new FlatButton(T("Server starten", "Start server"), G.Play, BtnKind.Primary);
            _bStartBig.Click += delegate { ToggleServer(); };
            _cOffline.Controls.Add(po); _cOffline.Controls.Add(_bStartBig);
            _cOffline.Layouter = delegate (int w)
            {
                int h = po.HeightFor(w - Dpi.D(260));
                po.SetBounds(Dpi.D(24), Dpi.D(20), w - Dpi.D(260), Math.Max(h, Dpi.D(36)));
                _bStartBig.Location = new Point(w - Dpi.D(24) - _bStartBig.Width, Dpi.D(20));
                return Math.Max(h, Dpi.D(36)) + Dpi.D(40);
            };

            _tiles[0] = new StatTile("Server", G.Power);
            _tiles[1] = new StatTile("Map", G.Map);
            _tiles[2] = new StatTile("Team US", G.People);
            _tiles[3] = new StatTile("Team MEC", G.People);
            foreach (StatTile t in _tiles) _liveHost.Canvas.Controls.Add(t);

            _cAct = new Card(T("Aktionen", "Actions"), G.Bolt);
            _bNextMap = new FlatButton(T("Nächste Map jetzt", "Next map now"), G.Next, BtnKind.Secondary);
            Theme.Tip.SetToolTip(_bNextMap, T("Nächste Map jetzt\nSetzt die Bot-Werte der nächsten Map und wechselt sofort.", "Next map now\nApplies the bot values of the next map and switches immediately."));
            _bNextMap.Click += delegate { SendCmd("@nextmap", T("Mapwechsel angefordert.", "Map change requested.")); };
            _bRestart = new FlatButton(T("Runde neu starten", "Restart round"), G.Refresh, BtnKind.Secondary);
            Theme.Tip.SetToolTip(_bRestart, T("Runde neu starten\nStartet die aktuelle Map neu. Die Botzahl bleibt dabei unverändert.", "Restart round\nRestarts the current map. The bot count stays the same."));
            _bRestart.Click += delegate { if (DarkDialog.Confirm(this, T("Runde neu starten", "Restart round"), T("Die aktuelle Runde wird sofort neu gestartet.", "The current round restarts immediately."), T("Neu starten", "Restart"), false)) SendCmd("admin.restartMap", T("Neustart angefordert.", "Restart requested.")); };
            _bMapsLive = new FlatButton(T("Rotation übernehmen", "Apply rotation"), G.Sync, BtnKind.Secondary);
            Theme.Tip.SetToolTip(_bMapsLive, T("Rotation übernehmen\nSpeichert und ersetzt die Maprotation im laufenden Server. Greift ab der nächsten Map.", "Apply rotation\nSaves and replaces the map rotation in the running server. Takes effect from the next map."));
            _bMapsLive.Click += delegate { MapsLive(); };
            _say = new TextField(G.Chat, T("Nachricht an alle Spieler", "Message to all players"));
            _say.Box.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { SendSay(); e.Handled = true; e.SuppressKeyPress = true; } };
            _bSay = new FlatButton(T("Senden", "Send"), G.Send, BtnKind.Primary);
            _bSay.Click += delegate { SendSay(); };
            _cmd = new TextField(G.Console, T("Konsolenbefehl, z. B. sv.spawnTime 10", "Console command, e.g. sv.spawnTime 10"));
            _cmd.Box.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { SendFreeCmd(); e.Handled = true; e.SuppressKeyPress = true; } };
            _bSend = new FlatButton(T("Ausführen", "Run"), G.Console, BtnKind.Secondary);
            Theme.Tip.SetToolTip(_bSend, T("Befehl ausführen\nErlaubt: sv.*, mapList.*, admin.*, game.say* sowie @nextmap und @commander 0/1.", "Run command\nAllowed: sv.*, mapList.*, admin.*, game.say* plus @nextmap and @commander 0/1."));
            _bSend.Click += delegate { SendFreeCmd(); };
            _bHelp = new FlatButton(T("Befehle", "Commands"), G.Info, BtnKind.Secondary);
            Theme.Tip.SetToolTip(_bHelp, T("Alle Befehle mit Erklärung – auswählen, Wert eintragen, ausführen.", "All commands with explanations – pick, enter a value, run."));
            _bHelp.Click += delegate { OpenCommands(); };
            _cmdHint = new Para(T("Tipp: „Befehle“ zeigt alle Möglichkeiten. Beim Tippen werden Befehle vorgeschlagen.", "Tip: “Commands” shows everything possible. Commands are suggested while typing."));
            _cmdHint.Color = Theme.Mute; _cmdHint.Font = Theme.Small;
            _cmdResult = new Para(""); _cmdResult.Color = Theme.Text; _cmdResult.Font = Theme.Mono;
            _bBotsNow = new FlatButton(T("Bots ändern …", "Change bots …"), G.Robot, BtnKind.Secondary);
            Theme.Tip.SetToolTip(_bBotsNow, T("Bots ändern\nSetzt die Botzahl für die laufende Map neu. Die Map wird dafür neu geladen (ca. 30 Sekunden), alle Spieler bleiben verbunden.\n\nBots einzeln kicken bringt nichts: Die KI füllt sie sofort wieder auf.", "Change bots\nSets the bot count for the running map. The map is reloaded for this (about 30 seconds); all players stay connected.\n\nKicking single bots does not help: the AI refills them right away."));
            _bBotsNow.Click += delegate { BotsNow(); };
            try
            {
                _cmd.Box.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                _cmd.Box.AutoCompleteSource = AutoCompleteSource.CustomSource;
                AutoCompleteStringCollection ac = new AutoCompleteStringCollection(); ac.AddRange(Commands.Names(null));
                _cmd.Box.AutoCompleteCustomSource = ac;
            }
            catch { }
            foreach (Control c in new Control[] { _bNextMap, _bRestart, _bBotsNow, _say, _bSay, _cmd, _bSend, _bHelp, _cmdHint, _cmdResult }) _cAct.Controls.Add(c);

            _cAct.Layouter = delegate (int w)
            {
                int x = Dpi.D(20), y = _cAct.HeaderH - Dpi.D(4);
                foreach (FlatButton b in new FlatButton[] { _bNextMap, _bRestart, _bBotsNow })
                {
                    if (x + b.Width > w - Dpi.D(20)) { x = Dpi.D(20); y += Dpi.D(42); }
                    b.Location = new Point(x, y); x += b.Width + Dpi.D(10);
                }
                y += Dpi.D(46);
                int bw = Math.Max(_bSay.Width, _bSend.Width);
                _bSay.SetBounds(w - Dpi.D(20) - bw, y, bw, _bSay.Height);
                _say.SetBounds(Dpi.D(20), y + (_bSay.Height - Dpi.D(32)) / 2, _bSay.Left - Dpi.D(30), Dpi.D(32));
                y += Dpi.D(42);
                _bSend.SetBounds(w - Dpi.D(20) - bw, y, bw, _bSend.Height);
                _bHelp.Location = new Point(_bSend.Left - Dpi.D(8) - _bHelp.Width, y);
                _cmd.SetBounds(Dpi.D(20), y + (_bSend.Height - Dpi.D(32)) / 2, _bHelp.Left - Dpi.D(30), Dpi.D(32));
                y += _bSend.Height + Dpi.D(6);
                int hh = _cmdHint.HeightFor(w - Dpi.D(40));
                _cmdHint.SetBounds(Dpi.D(20), y, w - Dpi.D(40), hh);
                y += hh;
                bool res = _cmdResult.Text.Length > 0; _cmdResult.Visible = res;
                if (res) { int rh = Math.Min(_cmdResult.HeightFor(w - Dpi.D(40)), Dpi.D(260)); _cmdResult.SetBounds(Dpi.D(20), y + Dpi.D(6), w - Dpi.D(40), rh); y += rh + Dpi.D(8); }
                return y + Dpi.D(14);
            };

            _cPlayers = new Card(T("Spieler", "Players"), G.People);
            _playersEmpty = new Para(T("Noch keine Menschen verbunden.", "No humans connected yet.")); _playersEmpty.Color = Theme.Mute;
            _cPlayers.Controls.Add(_playersEmpty);
            _cPlayers.Layouter = delegate (int w)
            {
                int y = _cPlayers.HeaderH - Dpi.D(4);
                _playersEmpty.Visible = _playerRows.Count == 0;
                if (_playerRows.Count == 0) { _playersEmpty.SetBounds(Dpi.D(22), y, w - Dpi.D(44), Dpi.D(24)); y += Dpi.D(28); }
                foreach (FormRow r in _playerRows) { r.SetBounds(Dpi.D(6), y, w - Dpi.D(12), r.Height); y += r.Height; }
                return y + Dpi.D(12);
            };

            _cNext = new Card(T("Nur nächste Map", "Next map only"), G.Next);
            _nextToggle = new ToggleSwitch();
            Theme.Tip.SetToolTip(_nextToggle, T("Einmalige Bot-Werte\nAn: Die Werte gelten nur für den nächsten Mapwechsel, danach wieder die normalen Werte. Schaltet sich danach selbst aus.", "One-time bot values\nOn: the values apply only to the next map change, then the normal values return. Turns itself off afterwards."));
            _nextEd = new BotEditor();
            Para nextInfo = new Para(T("Einschalten, um die Botwerte nur für den nächsten Mapwechsel zu ändern.", "Turn on to change the bot values for the next map change only."));
            _cNext.Controls.Add(_nextToggle); _cNext.Controls.Add(_nextEd); _cNext.Controls.Add(nextInfo);
            _cNext.HeaderControls.Add(_nextToggle);
            _cNext.Layouter = delegate (int w)
            {
                _nextToggle.Location = new Point(w - Dpi.D(20) - _nextToggle.Width, Dpi.D(13));
                _cNext.HeaderReserve = _nextToggle.Width + Dpi.D(8);
                _nextEd.Visible = _nextToggle.Checked; nextInfo.Visible = !_nextToggle.Checked;
                if (!_nextToggle.Checked)
                {
                    int ph2 = nextInfo.HeightFor(w - Dpi.D(44));
                    nextInfo.SetBounds(Dpi.D(22), _cNext.HeaderH - Dpi.D(4), w - Dpi.D(44), ph2);
                    return _cNext.HeaderH + ph2 + Dpi.D(12);
                }
                int h = _nextEd.LayoutFor(w - Dpi.D(40));
                _nextEd.SetBounds(Dpi.D(20), _cNext.HeaderH, w - Dpi.D(40), h);
                return _cNext.HeaderH + h + Dpi.D(18);
            };
            _nextToggle.CheckedChanged += delegate { if (_applying) return; WriteNext(); LayoutLive(); };
            _nextEd.Changed += delegate { if (_nextToggle.Checked) { _nextTimer.Stop(); _nextTimer.Start(); } };

            _cLog = new Card("Log", G.Console);
            _log = new RichTextBox();
            _log.BorderStyle = BorderStyle.None; _log.BackColor = Theme.Input; _log.ForeColor = Theme.Text; _log.Font = Theme.Mono;
            _log.ReadOnly = true; _log.DetectUrls = false; _log.WordWrap = true; _log.ScrollBars = RichTextBoxScrollBars.Vertical;
            _log.HandleCreated += delegate { Native.DarkScroll(_log); };
            _cLog.Controls.Add(_log); _cLog.Note = "coop_status.txt";

            BuildEvening(); BuildUpcoming();
            foreach (Card c in new Card[] { _cOffline, _cAct, _cPlayers, _cNext, _cLog, _cMsg, _cBoard, _cUpcoming }) _liveHost.Canvas.Controls.Add(c);
            _liveHost.Resize += delegate { LayoutLive(); };
        }

        // Kacheln oben; darunter zwei Spalten: links Aktionen und Log, rechts Spieler, nächste Map, Nachrichten, Bestenliste
        void LayoutLive()
        {
            if (_liveHost.Width < 10) return;
            int w = _liveHost.ContentWidth, pad = Dpi.D(24), gap = Dpi.D(16), y = Dpi.D(4), inner = w - 2 * pad;
            bool on = _running;
            _cOffline.Visible = !on;
            foreach (StatTile t in _tiles) t.Visible = on;
            _cAct.Visible = _cPlayers.Visible = _cMsg.Visible = on;
            _cNext.Visible = on && _coop;
            _cBoard.Visible = _coop;
            if (!on)
            {
                int oh = _cOffline.LayoutFor(inner); _cOffline.SetBounds(pad, y, inner, oh); y += oh + gap;
            }
            else
            {
                int per = inner >= Dpi.D(820) ? 4 : 2, tw = (inner - gap * (per - 1)) / per;
                for (int i = 0; i < 4; i++) _tiles[i].SetBounds(pad + (i % per) * (tw + gap), y + (i / per) * (_tiles[i].Height + gap), tw, _tiles[i].Height);
                y += (4 / per) * (_tiles[0].Height + gap);
            }
            bool two = inner >= Dpi.D(860);
            int lw = two ? (inner - gap) / 2 : inner;
            _cNext.Visible = false; _cLog.Visible = false;
            _cUpcoming.Visible = on;
            List<Card> left = new List<Card>(), right = new List<Card>();
            if (on) { left.Add(_cAct); left.Add(_cUpcoming); right.Add(_cPlayers); right.Add(_cMsg); }
            if (_coop) right.Add(_cBoard);
            int ly = y, ry = y;
            foreach (Card c in left) { int h = c.LayoutFor(lw); c.SetBounds(pad, ly, lw, h); ly += h + gap; }
            if (!two) ry = ly;
            int rx = two ? pad + lw + gap : pad;
            foreach (Card c in right) { int h = c.LayoutFor(lw); c.SetBounds(rx, ry, lw, h); ry += h + gap; }
            _liveHost.SetContentHeight(Math.Max(ly, ry) - gap + pad);
        }

        void OpenCommands()
        {
            using (CommandDialog d = new CommandDialog(Commands.All(_vals), _running))
            {
                d.ShowDialog(this);
                if (d.Result == 1) { _cmd.Value = d.Command; _cmd.Box.Focus(); _cmd.Box.SelectionStart = _cmd.Value.Length; }
                else if (d.Result == 2) SendCmdTracked(d.Command);
            }
        }

        bool RequireServer()
        {
            if (_running) return true;
            Toast(T("Der Server läuft nicht.", "The server is not running."), ToastKind.Warn);
            return false;
        }

        void SendCmd(string cmd, string okMsg)
        {
            if (!RequireServer()) return;
            try { CmdChannel.Send(ModSettingsDir, cmd); if (okMsg != null) Toast(okMsg, ToastKind.Ok); }
            catch (UnauthorizedAccessException) { NoRights(); }
            catch (Exception ex) { Toast(T("Senden fehlgeschlagen: ", "Sending failed: ") + ex.Message, ToastKind.Error); }
        }

        void SendSay()
        {
            string m = (_say.Value ?? "").Trim().Replace("\"", "'");
            if (m.Length == 0) return;
            SendCmd("game.sayAll \"" + m + "\"", T("Nachricht gesendet.", "Message sent."));
            if (_running) _say.Value = "";
        }

        void SendFreeCmd()
        {
            string c = (_cmd.Value ?? "").Trim();
            if (c.Length == 0) return;
            string err; c = ResolveCommand(c, out err);
            if (err != null) { Toast(err, ToastKind.Warn); return; }
            if (!RequireServer()) return;
            SendCmdTracked(c);
            _cmd.Value = "";
        }

        // Kurzformen: „kick Name“, „ban Name“, admin.kickPlayer Name – der Name wird zur Spieler-ID aufgelöst
        string ResolveCommand(string c, out string error)
        {
            error = null;
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(c, @"^(?:admin\.)?(kick|ban)(?:Player)?\s+(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return c;
            string verb = m.Groups[1].Value.ToLowerInvariant() == "ban" ? "admin.banPlayer" : "admin.kickPlayer", arg = m.Groups[2].Value.Trim().Trim((char)34);
            int id; if (int.TryParse(arg, out id)) return verb + " " + id;
            List<string[]> exact = new List<string[]>(), part = new List<string[]>();
            foreach (string[] p in _curPlayers)
            {
                if (p[2].Equals(arg, StringComparison.OrdinalIgnoreCase)) exact.Add(p);
                else if (p[2].IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0) part.Add(p);
            }
            List<string[]> hit = exact.Count > 0 ? exact : part;
            if (hit.Count == 1) return verb + " " + hit[0][0];
            error = hit.Count == 0 ? T("Kein Spieler „", "No player “") + arg + T("“ verbunden – Namen stehen in der Liste „Spieler“.", "” connected – names are in the “Players” list.")
                                   : T("„", "“") + arg + T("“ passt auf mehrere Spieler – bitte genauer eingeben.", "” matches several players – please be more specific.");
            return c;
        }

        // Befehl senden und die Antwort des Servers in der Karte anzeigen (steht nach wenigen Sekunden im Log)
        void SendCmdTracked(string cmd)
        {
            if (!RequireServer()) return;
            SendCmd(cmd, null);
            PendingCmd p = new PendingCmd(); p.Cmd = cmd; p.At = DateTime.Now; _pending.Add(p);
            _cmdResult.Text = "› " + cmd + "\n" + T("Warte auf Antwort des Servers … (bis zu 5 Sekunden)", "Waiting for the server" + (char)39 + "s reply … (up to 5 seconds)");
            LayoutLive();
        }

        // Antworten aus dem Log holen: Zeile „… command <Befehl> -> <Ergebnis>“
        void CheckResults(string[] lines)
        {
            if (_pending.Count == 0) return;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingCmd p = _pending[i]; string key = "command " + p.Cmd + " -> ", found = null;
                for (int k = lines.Length - 1; k >= 0 && found == null; k--)
                {
                    int at = lines[k].IndexOf(key, StringComparison.Ordinal);
                    if (at < 0) continue;
                    DateTime ts;
                    if (lines[k].Length >= 19 && DateTime.TryParse(lines[k].Substring(0, 19), out ts) && ts < p.At.AddSeconds(-3)) continue;
                    found = lines[k].Substring(at + key.Length);
                }
                if (found != null) { ShowResult(p.Cmd, found); _pending.RemoveAt(i); }
                else if ((DateTime.Now - p.At).TotalSeconds > 40)
                {
                    _cmdResult.Text = "› " + p.Cmd + "\n⚠ " + T("Keine Antwort. Läuft der Server mit Bot-Modul? (Einstellungen › Diagnose › Server-Log)", "No reply. Is the server running with the bot module? (Settings › Diagnostics › Server log)");
                    _pending.RemoveAt(i); LayoutLive();
                }
            }
        }

        void ShowResult(string cmd, string res)
        {
            string[] parts = (res ?? "").Replace("\r", "").Split(new string[] { " | " }, StringSplitOptions.RemoveEmptyEntries);
            List<string> rows = new List<string>();
            foreach (string s in parts) { string x = s.Trim(); if (x.Length > 0) rows.Add(x); }
            StringBuilder sb = new StringBuilder("› " + cmd + "\n");
            if (rows.Count == 0) sb.Append("✓ ").Append(T("ausgeführt", "done"));
            else
            {
                int max = 14;
                for (int i = 0; i < rows.Count && i < max; i++) sb.Append(rows[i]).Append(i + 1 < rows.Count && i + 1 < max ? "\n" : "");
                if (rows.Count > max) sb.Append("\n… +").Append(rows.Count - max);
                if (cmd.StartsWith("admin.kickPlayer") && rows[0].IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) sb.Append("\n").Append(T("Die ID stimmt nicht – Spieler stehen rechts in der Liste „Spieler“.", "The ID is wrong – players are in the “Players” list on the right."));
            }
            _cmdResult.Text = sb.ToString();
            LayoutLive();
        }

        // Botzahl für die laufende Map ändern: einmalige Werte schreiben, Map neu laden (@reload im Bot-Modul)
        void BotsNow()
        {
            if (!RequireServer()) return;
            if (!_coop) { Toast(T("Bots gibt es nur im Coop-Modus.", "Bots exist in coop mode only."), ToastKind.Info); return; }
            if (!_module) { Toast(T("Das Bot-Modul meldet sich noch nicht – bitte kurz warten oder Einstellungen › Einrichtung prüfen.", "The bot module is not responding yet – please wait a moment or check Settings › Setup."), ToastKind.Warn); return; }
            string map; _lastLive.TryGetValue("map", out map);
            MapEntry cur = null;
            foreach (MapEntry me in _st.Maps) if (string.Equals(me.Name, map, StringComparison.OrdinalIgnoreCase)) { cur = me; break; }
            int size = cur != null ? cur.Size : 32;
            BotOpt start = (cur != null ? OptFor(cur) : _st.Defaults[32]).Clone();
            using (BotNowDialog d = new BotNowDialog(Util.Pretty(map ?? ""), start, size, _hUS.Value, _hMEC.Value))
            {
                d.ShowDialog(this);
                if (d.Result == null) return;
                try
                {
                    CoopState st = CoopTxt.Read(CoopTxtPath);
                    if (st == null) { Toast(T("coop_server.txt fehlt – zuerst speichern.", "coop_server.txt missing – save first."), ToastKind.Warn); return; }
                    BotOpt o = d.Result; o.Id = DateTime.Now.ToString("HHmmssff"); _botsNowId = o.Id; st.Next = o;
                    CoopTxt.Write(CoopTxtPath, st);
                    SendCmd("@reload", null);
                    Toast(T("Map wird neu geladen – die neue Botzahl gilt in ca. 30 Sekunden.", "Reloading the map – the new bot count applies in about 30 seconds."), ToastKind.Ok);
                }
                catch (UnauthorizedAccessException) { NoRights(); }
                catch (Exception ex) { Toast(T("Schreiben fehlgeschlagen: ", "Writing failed: ") + ex.Message, ToastKind.Error); }
            }
        }

        // Einmalige Werte nach dem Neuladen wieder aus der Datei nehmen, damit sie beim nächsten Serverstart nicht noch einmal greifen
        void BotsNowDone(string usedId)
        {
            if (_botsNowId.Length == 0 || usedId != _botsNowId) return;
            _botsNowId = "";
            try { CoopState st = CoopTxt.Read(CoopTxtPath); if (st != null && st.Next != null) { st.Next = null; CoopTxt.Write(CoopTxtPath, st); } } catch { }
        }

        void SendLiveSettings()
        {
            if (!RequireServer()) return;
            int n = 0;
            foreach (SettingDef d in Defs.All)
            {
                if (!d.Live) continue;
                string v = _vals[d.Key], s; _saved.TryGetValue(d.Key, out s);
                if (Defs.Same(d, v, s)) continue;
                SendCmd(d.IsSpecial ? "@commander " + v : d.Key + " " + Defs.ToCon(d, v), null);
                n++;
            }
            if (n > 0 && SaveAll(true)) Toast(n + T(" Werte live gesendet und gespeichert.", " values sent live and saved."), ToastKind.Ok);
        }

        void MapsLive()
        {
            if (!RequireServer()) return;
            if (!SaveAll(false)) return;
            SendCmd("mapList.clear", null);
            foreach (MapEntry m in _st.Maps) SendCmd("mapList.append " + m.Name + " " + m.Mode + " " + m.Size, null);
            Toast(T("Rotation mit ", "Rotation with ") + _st.Maps.Count + T(" Maps gesendet.", " maps sent."), ToastKind.Ok);
        }

        void WriteNext()
        {
            try
            {
                CoopState st = CoopTxt.Read(CoopTxtPath);
                if (st == null) { Toast(T("coop_server.txt fehlt – zuerst speichern.", "coop_server.txt missing – save first."), ToastKind.Warn); return; }
                if (_nextToggle.Checked) { BotOpt o = _nextEd.Opt; o.Id = DateTime.Now.ToString("HHmmssff"); _nextId = o.Id; st.Next = o; }
                else { st.Next = null; _nextId = ""; }
                CoopTxt.Write(CoopTxtPath, st);
            }
            catch (UnauthorizedAccessException) { NoRights(); }
            catch (Exception ex) { Toast(T("Schreiben fehlgeschlagen: ", "Writing failed: ") + ex.Message, ToastKind.Error); }
        }

        void UpdatePlayers(List<string[]> ps)
        {
            _curPlayers = ps;
            StringBuilder ids = new StringBuilder();
            foreach (string[] p in ps) ids.Append(p[0]).Append(',');
            if (ids.ToString() != _playerSig)
            {
                _playerSig = ids.ToString();
                foreach (FormRow r in _playerRows) { _cPlayers.Controls.Remove(r); r.Dispose(); }
                _playerRows.Clear();
                foreach (string[] p in ps)
                {
                    string id = p[0], name = p[2];
                    FlatButton kick = new FlatButton("Kick", G.Close, BtnKind.Ghost);
                    kick.Height = Dpi.D(28); kick.Fit();
                    kick.Click += delegate
                    {
                        if (DarkDialog.Confirm(this, "Kick", T("„", "“") + name + T("“ vom Server werfen?", "” – kick from the server?"), "Kick", true))
                            SendCmd("admin.kickPlayer " + id, T("Kick gesendet.", "Kick sent."));
                    };
                    FlatButton team = new FlatButton("Team", G.Refresh, BtnKind.Ghost);
                    team.Height = Dpi.D(28); team.Fit();
                    Theme.Tip.SetToolTip(team, T("Teamwechsel\nSchickt den Spieler ins andere Team. Wer gerade lebt, stirbt kurz und spawnt dort neu. Braucht das Bot-Modul ab Version 7 (Einstellungen › Mods › Bot-Modul aktualisieren).", "Switch team\nMoves the player to the other team. If alive, they die briefly and respawn there. Needs bot module version 7 or later (Settings › Mods › update bot module)."));
                    team.Click += delegate
                    {
                        ModStatus ms;
                        if (_status.TryGetValue(_mod, out ms) && ms.Installed && ms.Version < 7) { Toast(T("Das Bot-Modul auf dem Server ist Version ", "The bot module on the server is version ") + ms.Version + T(" – Teamwechsel braucht 7. Server beenden und neu starten, dann wird es automatisch ersetzt.", " – team switch needs 7. Stop and start the server, it is replaced automatically."), ToastKind.Warn); return; }
                        SendCmd("@team " + id, T("Teamwechsel gesendet: ", "Team switch sent: ") + name);
                    };
                    ButtonRow acts = Buttons(team, kick);
                    int aw = team.Width + Dpi.D(8) + kick.Width; acts.Size = new Size(aw, Dpi.D(28)); acts.LayoutFor(aw);
                    FormRow row = new FormRow("", null, acts, aw, false);
                    row.Tag = p;
                    _playerRows.Add(row); _cPlayers.Controls.Add(row);
                }
                LayoutLive();
            }
            for (int i = 0; i < ps.Count && i < _playerRows.Count; i++)
            {
                string[] p = ps[i];
                string team = p[1] == "2" ? "US" : (p[1] == "1" ? "MEC" : "–");
                string label = p[2] + "   " + team + " · " + p[3] + T(" Pkt", " pts") + " · " + p[4] + "/" + p[5] + " · " + p[6] + " ms";
                if (_playerRows[i].Label != label) { _playerRows[i].Label = label; _playerRows[i].Invalidate(); }
            }
            _cPlayers.SetNote(ps.Count == 1 ? T("1 Mensch", "1 human") : ps.Count + T(" Menschen", " humans"));
        }
    }
}
