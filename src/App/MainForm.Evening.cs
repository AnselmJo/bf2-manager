// Hauptfenster: Abend-Funktionen (Bestenliste, geplante Nachrichten, Wächter) - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BF2Coop
{
    public partial class MainForm
    {
        Card _cBoard, _cMsg; LeaderTable _board;
        TextField _msgText; NumberBox _msgMin; ToggleSwitch _msgRepeat; readonly List<FormRow> _msgRows = new List<FormRow>();
        bool _userStopped; readonly List<DateTime> _wdRestarts = new List<DateTime>();
        Process _srvProc; string _srvExit; // zuletzt gestarteter Server und der verständliche Grund für sein Ende
        DateTime _boardStamp = DateTime.MinValue; readonly Dictionary<string, DateTime> _msgDue = new Dictionary<string, DateTime>();

        void BuildEvening()
        {
            // ---- Bestenliste
            _cBoard = new Card(T("Bestenliste des Abends", "Leaderboard of the evening"), G.Star);
            _board = new LeaderTable();
            FlatButton bReset = new FlatButton(T("Neu beginnen", "Start over"), G.Undo, BtnKind.Ghost); bReset.Height = Dpi.D(30); bReset.Fit();
            Theme.Tip.SetToolTip(bReset, T("Zählt ab jetzt neu. Die Rundendaten bleiben in coop_rounds.txt erhalten.", "Starts counting from now. Round data stays in coop_rounds.txt."));
            bReset.Click += delegate
            {
                if (!DarkDialog.Confirm(this, T("Bestenliste", "Leaderboard"), T("Die Bestenliste ab jetzt neu zählen?", "Restart the leaderboard from now?"), T("Neu beginnen", "Start over"), false)) return;
                _cfg.Set("BoardSince", (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds); _cfg.Save();
                _boardStamp = DateTime.MinValue; RefreshLeaderboard();
            };
            _cBoard.Controls.Add(_board); _cBoard.Controls.Add(bReset); _cBoard.HeaderControls.Add(bReset);
            _cBoard.Layouter = delegate (int w)
            {
                bReset.Location = new Point(w - Dpi.D(16) - bReset.Width, Dpi.D(9));
                _cBoard.HeaderReserve = bReset.Width + Dpi.D(8);
                _board.SetBounds(Dpi.D(20), _cBoard.HeaderH - Dpi.D(4), w - Dpi.D(40), _board.Height);
                return _cBoard.HeaderH + _board.Height + Dpi.D(10);
            };

            // ---- Geplante Nachrichten
            _cMsg = new Card(T("Nachrichten planen", "Scheduled messages"), G.Chat);
            _cMsg.Key = "live.msg"; _cMsg.Collapsible = true; _cMsg.Collapsed = !_collapsed.Contains("live.msg.open");
            _cMsg.Toggled += delegate { if (_cMsg.Collapsed) _collapsed.Remove("live.msg.open"); else _collapsed.Add("live.msg.open"); _cfg.Set("Collapsed", new List<string>(_collapsed)); _cfg.Save(); LayoutLive(); };
            _msgText = new TextField(G.Chat, T("z. B. Pizza ist da!", "e.g. Pizza has arrived!"));
            _msgMin = new NumberBox(1, 240, 10, "min");
            _msgRepeat = new ToggleSwitch();
            FlatButton bAdd = new FlatButton(T("Planen", "Schedule"), G.Add, BtnKind.Secondary);
            Theme.Tip.SetToolTip(_msgRepeat, T("Wiederholen\nAn: alle X Minuten. Aus: einmal in X Minuten.", "Repeat\nOn: every X minutes. Off: once in X minutes."));
            bAdd.Click += delegate
            {
                string t = (_msgText.Value ?? "").Trim().Replace("|", "/").Replace("\"", "'");
                if (t.Length == 0) return;
                List<string> l = _cfg.List("Messages");
                l.Add(_msgMin.Value + "|" + (_msgRepeat.Checked ? "1" : "0") + "|" + t);
                _cfg.Set("Messages", l); _cfg.Save();
                _msgDue[l[l.Count - 1]] = DateTime.Now.AddMinutes(_msgMin.Value);
                _msgText.Value = ""; RebuildMessages(); LayoutLive();
            };
            FormRow rMin = new FormRow(T("In / alle", "In / every"), null, _msgMin, _msgMin.Width, false);
            FormRow rRep = new FormRow(T("Wiederholen", "Repeat"), null, _msgRepeat, _msgRepeat.Width, false);
            foreach (Control c in new Control[] { _msgText, rMin, rRep, bAdd }) _cMsg.Controls.Add(c);
            _cMsg.Layouter = delegate (int w)
            {
                int y = _cMsg.HeaderH - Dpi.D(4);
                _msgText.SetBounds(Dpi.D(20), y, w - Dpi.D(40), Dpi.D(32)); y += Dpi.D(38);
                rMin.SetBounds(Dpi.D(6), y, w - Dpi.D(12), rMin.Height); y += rMin.Height;
                rRep.SetBounds(Dpi.D(6), y, w - Dpi.D(12), rRep.Height); y += rRep.Height + Dpi.D(4);
                bAdd.Location = new Point(Dpi.D(20), y); y += bAdd.Height + Dpi.D(8);
                foreach (FormRow r in _msgRows) { r.SetBounds(Dpi.D(6), y, w - Dpi.D(12), r.Height); y += r.Height; }
                return y + Dpi.D(10);
            };
            RebuildMessages();
        }

        void RebuildMessages()
        {
            foreach (FormRow r in _msgRows) { _cMsg.Controls.Remove(r); r.Dispose(); }
            _msgRows.Clear();
            foreach (string entry in _cfg.List("Messages"))
            {
                string e = entry;
                string[] p = e.Split(new char[] { '|' }, 3);
                if (p.Length < 3) continue;
                FlatButton del = new FlatButton(null, G.Delete, BtnKind.Ghost);
                Theme.Tip.SetToolTip(del, T("Entfernen", "Remove"));
                del.Click += delegate
                {
                    List<string> l = _cfg.List("Messages"); l.Remove(e); _cfg.Set("Messages", l); _cfg.Save();
                    _msgDue.Remove(e); RebuildMessages(); LayoutLive();
                };
                string when = p[1] == "1" ? T("alle ", "every ") + p[0] + " min" : T("einmal in ", "once in ") + p[0] + " min";
                FormRow row = new FormRow("„" + p[2] + "“  ·  " + when, null, del, del.Width, false);
                _msgRows.Add(row); _cMsg.Controls.Add(row);
                if (!_msgDue.ContainsKey(e)) _msgDue[e] = DateTime.Now.AddMinutes(Util.ToInt(p[0], 10));
            }
        }

        // Fällige Nachrichten senden (läuft mit der Live-Aktualisierung alle 2 s)
        void MessagesTick()
        {
            if (!_running || !_module) return;
            List<string> l = _cfg.List("Messages");
            bool changed = false;
            foreach (string e in l.ToArray())
            {
                DateTime due;
                if (!_msgDue.TryGetValue(e, out due) || DateTime.Now < due) continue;
                string[] p = e.Split(new char[] { '|' }, 3);
                if (p.Length < 3) continue;
                try { CmdChannel.Send(ModSettingsDir, "game.sayAll \"" + p[2] + "\""); } catch { }
                if (p[1] == "1") _msgDue[e] = DateTime.Now.AddMinutes(Util.ToInt(p[0], 10));
                else { l.Remove(e); _msgDue.Remove(e); changed = true; }
            }
            if (changed) { _cfg.Set("Messages", l); _cfg.Save(); RebuildMessages(); if (_page == PgLive) LayoutLive(); }
        }

        void RefreshLeaderboard()
        {
            if (_mod.Length == 0) return;
            string f = Path.Combine(ModSettingsDir, "coop_rounds.txt");
            DateTime stamp = File.Exists(f) ? File.GetLastWriteTimeUtc(f) : DateTime.MinValue.AddDays(1);
            if (stamp == _boardStamp) return;
            _boardStamp = stamp;
            long since = 0; long.TryParse(_cfg.S("BoardSince", "0"), out since);
            int rounds;
            List<LeaderRow> rows = Leaderboard.Read(f, since, out rounds);
            int oldH = _board.Height;
            _board.SetRows(rows);
            _cBoard.SetNote(rounds == 1 ? T("1 Runde", "1 round") : rounds + T(" Runden", " rounds"));
            if (_board.Height != oldH && _page == PgLive) LayoutLive();
        }

        // Exit-Code und Laufzeit des zuletzt gestarteten Servers. false = unbekannt (z. B. von außen gestartet).
        bool ServerExit(out int code, out double seconds)
        {
            code = 0; seconds = -1;
            try
            {
                if (_srvProc == null || !_srvProc.HasExited) return false;
                code = _srvProc.ExitCode;
                seconds = (_srvProc.ExitTime - _srvStart).TotalSeconds;
                return true;
            }
            catch { return false; }
        }

        // Server ist ohne Zutun beendet worden: neu starten (höchstens 3-mal in 10 Minuten).
        // Hat er sich nur selbst beendet, weil er nichts zu tun fand, hilft ein Neustart nicht – dann den Grund zeigen.
        void Watchdog()
        {
            int code; double secs;
            bool known = ServerExit(out code, out secs);
            _srvExit = known ? Srv.ExitReason(code, secs) : null;
            if (known) Util.Log(AppPaths.Dir, "Server ended: exit code " + code + " after " + (int)secs + " s");
            if (known && Srv.ExitIsConfigProblem(code, secs))
            {
                _crashed = true; _srvStart = DateTime.MinValue; UpdateServerBanners();
                DarkDialog.Info(this, T("Server hat sich beendet", "Server stopped"), _srvExit);
                return;
            }
            if (!_cfg.B("Watchdog", true) || _setupMode) { _crashed = true; UpdateServerBanners(); Toast(_srvExit ?? T("Server wurde unerwartet beendet.", "The server stopped unexpectedly."), ToastKind.Error); return; }
            _wdRestarts.RemoveAll(delegate (DateTime d) { return (DateTime.Now - d).TotalMinutes > 10; });
            if (_wdRestarts.Count >= 3)
            {
                Toast(T("Server mehrfach abgestürzt – Wächter pausiert. Log prüfen.", "Server crashed repeatedly – watchdog paused. Check the log."), ToastKind.Error);
                _srvStart = DateTime.MinValue;
                return;
            }
            _wdRestarts.Add(DateTime.Now);
            Util.Log(AppPaths.Dir, "Watchdog: server stopped unexpectedly, restarting (" + _wdRestarts.Count + ")");
            Toast((known ? _srvExit + " " : "") + T("Wächter startet ihn neu …", "Watchdog is restarting it …"), ToastKind.Warn);
            RestartCurrent();
        }
    }
}
