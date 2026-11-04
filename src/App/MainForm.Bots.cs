// Hauptfenster: Seite Bots - BF2 Manager. C# 5 (.NET Framework 4.x).
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
        Card _cAdapt, _cRadio; ToggleSwitch _adaptToggle, _radioToggle; NumberBox _adaptStep, _adaptMax; FormRow _rowRadio;
        // ================================================================== Seite Bots
        void BuildBots()
        {
            PageHeader ph = NewPage(1, "Bots", T("Richtwerte je Mapgröße – gelten ab dem nächsten Mapwechsel", "Values per map size – applied from the next map change"));
            ph.SetTitleInfo(T("So funktioniert es\nDie Werte gelten pro Mapgröße. Beim Mapwechsel berechnet der Server daraus Botzahl, Verteilung und Schwierigkeit.\nEinzelne Maps bekommen eigene Werte auf der Seite Maps (Stern), einmalige Werte für die nächste Map gibt es auf der Seite Live.\n\nBalken: volle Farbe = Menschen, schraffiert = Bots.",
                "How it works\nValues apply per map size. At every map change the server calculates bot count, distribution and difficulty from them.\nSingle maps get their own values on the Maps page (star), one-time values for the next map are on the Live page.\n\nBar: solid = humans, striped = bots."));
            _hUS = new NumberBox(0, 64, 8, null); _hMEC = new NumberBox(0, 64, 0, null);
            _hUS.Width = _hMEC.Width = Dpi.D(104);
            string tip = T("\nWie viele von euch voraussichtlich in diesem Team spielen. Für die Vorschau und als Ersatzwert.\n\nBeim Mapwechsel zählt der Server die tatsächlich verbundenen Menschen pro Team.", "\nHow many of you will probably play on this team. Used for the preview and as a fallback.\n\nAt every map change the server counts the humans actually connected per team.");
            Theme.Tip.SetToolTip(_hUS, T("Menschen im Team US", "Humans on team US") + tip);
            Theme.Tip.SetToolTip(_hMEC, T("Menschen im Team MEC", "Humans on team MEC") + tip);
            TextLabel lUS = new TextLabel("US", Theme.BodyB); lUS.Color = Theme.Us;
            TextLabel lMEC = new TextLabel("MEC", Theme.BodyB); lMEC.Color = Theme.Mec;
            TextLabel lH = new TextLabel(T("Menschen", "Humans"), Theme.Body);
            _botAdvToggle = new ToggleSwitch(); _botAdvToggle.SetSilent(_cfg.B("BotAdv", false));
            Theme.Tip.SetToolTip(_botAdvToggle, T("Erweitert\nKI-Datei des Mods (aiDefault.ai) und Botnamen (botnames.ai).", "Advanced\nThe mod's AI file (aiDefault.ai) and bot names (botnames.ai)."));
            ph.AddRight(_botAdvToggle); ph.AddRight(new TextLabel(T("Erweitert", "Advanced"), Theme.BodyB));
            ph.AddRight(new TextLabel("   ", Theme.Body));
            ph.AddRight(_hMEC); ph.AddRight(lMEC); ph.AddRight(_hUS); ph.AddRight(lUS); ph.AddRight(lH);
            EventHandler hc = delegate { UpdateHumans(); MarkDirty(); };
            _hUS.ValueChanged += hc; _hMEC.ValueChanged += hc;

            _botHost = new ScrollHost(); _botHost.Dock = DockStyle.Fill;
            Dock3(_pages[1], ph, null, _botHost);

            foreach (int size in new int[] { 16, 32, 64 })
            {
                Card c = MakeCard("bot." + size, size + T("er Maps", "-player maps"), G.Robot, false);
                c.Note = T("bis ", "up to ") + size + T(" Spieler", " players");
                BotEditor ed = new BotEditor();
                c.Controls.Add(ed);
                c.Layouter = delegate (int w) { int h = ed.LayoutFor(w - Dpi.D(40)); ed.SetBounds(Dpi.D(20), c.HeaderH, w - Dpi.D(40), h); return c.HeaderH + h + Dpi.D(18); };
                ed.Changed += delegate { MarkDirty(); RefreshRot(); };
                _botEd[size] = ed; _botCards.Add(c);
            }

            _cAi = MakeCard("bot.ai", T("KI-Datei des Mods", "Mod AI file"), G.Wrench, true);
            _cAi.Note = "aiDefault.ai";
            _aiOverride = new ToggleSwitch(); _aiInclude = new ToggleSwitch(); _aiMax = new NumberBox(0, 255, 64, null);
            _aiInfo = new Para(""); _aiInfo.Color = Theme.Amber;
            FormRow rO = new FormRow(T("Menü-Werte überschreiben", "Override menu settings"), T("overrideMenuSettings\nBei 1 können die Werte dieser Datei die Bot-Einstellungen des Managers überschreiben. Empfohlen: aus.", "overrideMenuSettings\nWhen on, this file's values may override the manager's bot settings. Recommended: off."), _aiOverride, _aiOverride.Width, false);
            FormRow rM = new FormRow(T("Bot-Obergrenze", "Bot limit"), T("setMaxNBots\nBotzahl, die der Mod bei aktivem Überschreiben fest vorgibt. Ist das Überschreiben aus, hat der Wert auf dem Server keine Wirkung; dort gelten die Werte des Managers (höchstens 32 Bots).", "setMaxNBots\nBot count the mod forces while override is on. With override off it has no effect on the server; the manager's values apply (at most 32 bots)."), _aiMax, _aiMax.Width, false);
            FormRow rI = new FormRow(T("Menschen zählen mit", "Humans count toward limit"), T("maxBotsIncludeHumans\nAn: Menschen werden auf die Bot-Obergrenze angerechnet.", "maxBotsIncludeHumans\nOn: humans count toward the bot limit."), _aiInclude, _aiInclude.Width, false);
            foreach (Control c in new Control[] { rO, rM, rI, _aiInfo }) _cAi.Controls.Add(c);
            EventHandler ac = delegate { if (!_applying) { _aiDirty = true; MarkDirty(); UpdateAiInfo(); } };
            _aiOverride.CheckedChanged += ac; _aiInclude.CheckedChanged += ac; _aiMax.ValueChanged += ac;
            _cAi.Layouter = delegate (int w)
            {
                int y = _cAi.HeaderH - Dpi.D(4);
                foreach (FormRow r in new FormRow[] { rO, rM, rI }) { r.SetBounds(Dpi.D(6), y, w - Dpi.D(12), r.Height); y += r.Height; }
                int ph2 = _aiInfo.Text.Length > 0 ? _aiInfo.HeightFor(w - Dpi.D(44)) : 0;
                _aiInfo.SetBounds(Dpi.D(22), y + Dpi.D(4), w - Dpi.D(44), ph2);
                return y + ph2 + Dpi.D(16);
            };

            _cNames = MakeCard("bot.names", T("Botnamen", "Bot names"), G.Contact, true);
            _names = new TextBox();
            _names.Multiline = true; _names.ScrollBars = ScrollBars.Vertical; _names.BorderStyle = BorderStyle.None; _names.AcceptsReturn = true;
            _names.BackColor = Theme.Input; _names.ForeColor = Theme.Text; _names.Font = Theme.Body; _names.WordWrap = false;
            _names.HandleCreated += delegate { Native.DarkScroll(_names); };
            _names.TextChanged += delegate { if (_applying) return; _namesDirty = true; MarkDirty(); UpdateNamesNote(); };
            Para namesTip = new Para(T("Ein Name pro Zeile. Am besten zwei Wörter (z. B. „Pvt. Miller“), nur Standardzeichen ohne Umlaute. Gibt es weniger Namen als Bots, hängt das Spiel Nummern an.", "One name per line. Two words work best (e.g. “Pvt. Miller”), plain characters only. With fewer names than bots, the game appends numbers."));
            _cNames.Controls.Add(namesTip); _cNames.Controls.Add(_names);
            _cNames.Layouter = delegate (int w)
            {
                int h = namesTip.HeightFor(w - Dpi.D(44));
                namesTip.SetBounds(Dpi.D(22), _cNames.HeaderH - Dpi.D(4), w - Dpi.D(44), h);
                _names.SetBounds(Dpi.D(22), _cNames.HeaderH + h + Dpi.D(4), w - Dpi.D(44), Dpi.D(260));
                return _cNames.HeaderH + h + Dpi.D(280);
            };
            // ---- Anpassende Schwierigkeit
            _cAdapt = MakeCard("bot.adapt", T("Anpassende Schwierigkeit", "Adaptive difficulty"), G.Bolt, true);
            _adaptToggle = new ToggleSwitch(); _adaptStep = new NumberBox(1, 25, 5, null); _adaptMax = new NumberBox(5, 60, 30, null);
            FormRow rA = new FormRow(T("Aktiv", "Enabled"), T("Gewinnt ihr eine Runde, spielen die Bots auf der nächsten Map um „Schritt“ stärker, verliert ihr, um „Schritt“ schwächer – höchstens bis „Grenze“ vom eingestellten Wert. Startet bei jedem Serverstart neu bei 0.", "If you win a round, bots play stronger by “step” on the next map; if you lose, weaker by “step” – at most “limit” away from the configured value. Resets to 0 at every server start."), _adaptToggle, _adaptToggle.Width, false);
            FormRow rS = new FormRow(T("Schritt", "Step"), null, _adaptStep, _adaptStep.Width, false);
            FormRow rLim = new FormRow(T("Grenze", "Limit"), null, _adaptMax, _adaptMax.Width, false);
            foreach (Control c in new Control[] { rA, rS, rLim }) _cAdapt.Controls.Add(c);
            _cAdapt.Layouter = delegate (int w) { int y = _cAdapt.HeaderH - Dpi.D(4); foreach (FormRow r in new FormRow[] { rA, rS, rLim }) { r.SetBounds(Dpi.D(6), y, w - Dpi.D(12), r.Height); y += r.Height; } return y + Dpi.D(12); };
            EventHandler adc = delegate { if (!_applying) MarkDirty(); };
            _adaptToggle.CheckedChanged += adc; _adaptStep.ValueChanged += adc; _adaptMax.ValueChanged += adc;
            // ---- Funk der Bots
            _cRadio = MakeCard("bot.radio", T("Funk", "Radio"), G.Mic, false);
            _radioToggle = new ToggleSwitch();
            _rowRadio = new FormRow(T("Bot-Funksprüche", "Bot radio messages"), T("Bot-Funksprüche\nAus = Bots rufen nicht mehr „Medic!“, „Roger that“ usw. über Funk. Sie spielen genauso weiter, nur leiser.\nÄndert ai\\AIRadioMessages.ai des Mods (Sicherung wird angelegt).\n\nWirkt beim nächsten Serverstart. Gilt für alle Spieler auf diesem Server.", "Bot radio messages\nOff = bots no longer call “Medic!”, “Roger that” etc. over the radio. They play just the same, only quieter.\nChanges the mod's ai\\AIRadioMessages.ai (a backup is created).\n\nApplies at the next server start. Affects everyone on this server."), _radioToggle, _radioToggle.Width, false);
            _radioToggle.CheckedChanged += delegate { if (!_applying) SetBotRadio(_radioToggle.Checked); };
            _cRadio.Controls.Add(_rowRadio);
            _cRadio.Layouter = delegate (int w) { int y = _cRadio.HeaderH - Dpi.D(4); _rowRadio.SetBounds(Dpi.D(6), y, w - Dpi.D(12), _rowRadio.Height); return y + _rowRadio.Height + Dpi.D(12); };
            _botCards.Add(_cAdapt); _botCards.Add(_cRadio);
            _botCards.Add(_cAi); _botCards.Add(_cNames);
            _cAi.Hidden = _cNames.Hidden = !_botAdvToggle.Checked;
            foreach (Card c in _botCards) { _botHost.Canvas.Controls.Add(c); if (c.Collapsible) HookCollapse(c, _botHost, _botCards, 3); }
            _botHost.Resize += delegate { _botHost.Masonry(_botCards, 3); };
            _botAdvToggle.CheckedChanged += delegate
            {
                _cfg.Set("BotAdv", _botAdvToggle.Checked); _cfg.Save();
                _cAi.Hidden = _cNames.Hidden = !_botAdvToggle.Checked;
                _botHost.Masonry(_botCards, 3);
                if (_botAdvToggle.Checked) _botHost.EnsureVisible(_cAi);
            };
        }

        void UpdateHumans()
        {
            foreach (KeyValuePair<int, BotEditor> kv in _botEd) kv.Value.SetContext(kv.Key, _hUS.Value, _hMEC.Value);
            if (_curRot != null) _ovEd.SetContext(_curRot.Size, _hUS.Value, _hMEC.Value);
            _nextEd.SetContext(0, _hUS.Value, _hMEC.Value);
            if (!_applying) RefreshRot();
        }

        void LoadAiAndNames()
        {
            _ai = AiFile.Load(GameDir, _mod);
            bool has = _ai.Path != null;
            _aiOverride.Enabled = has && _ai.Override >= 0; _aiInclude.Enabled = has && _ai.IncludeHumans >= 0; _aiMax.Enabled = has && _ai.MaxBots >= 0;
            _aiOverride.SetSilent(_ai.Override == 1); _aiInclude.SetSilent(_ai.IncludeHumans == 1); _aiMax.SetSilent(Math.Max(0, _ai.MaxBots));
            UpdateAiInfo();
            _namesPath = BotNames.Find(GameDir, _mod);
            List<string> names = _namesPath != null ? BotNames.Read(_namesPath, out _namesQuoted) : new List<string>();
            _names.Text = string.Join("\r\n", names.ToArray());
            _names.Enabled = _namesPath != null;
            _aiDirty = _namesDirty = false;
            UpdateNamesNote();
            LoadBotRadio();
        }

        void LoadBotRadio()
        {
            bool old = _applying; _applying = true;
            bool? st = BotRadio.State(BotRadio.Find(GameDir, _mod));
            _radioToggle.SetSilent(st != false);
            _radioToggle.Enabled = st != null;
            _cRadio.SetNote(st == null ? T("AIRadioMessages.ai fehlt", "AIRadioMessages.ai missing") : (st == true ? "" : T("Bots funken nicht", "bots stay silent")));
            _applying = old;
        }

        void SetBotRadio(bool on)
        {
            string path = BotRadio.Find(GameDir, _mod);
            try
            {
                if (path == null) throw new FileNotFoundException(T("AIRadioMessages.ai fehlt in diesem Mod.", "AIRadioMessages.ai is missing in this mod."));
                BotRadio.Set(path, on, DocsDir);
                Toast((on ? T("Bot-Funk an.", "Bot radio on.") : T("Bot-Funk aus.", "Bot radio off.")) + (_running ? T(" Wirkt nach einem Server-Neustart.", " Applies after a server restart.") : T(" Wirkt beim nächsten Serverstart.", " Applies at the next server start.")), ToastKind.Ok);
            }
            catch (UnauthorizedAccessException) { Toast(T("Keine Schreibrechte im Spielordner – Einstellungen › Dieser PC › Schreibrechte.", "No write access to the game folder – Settings › This PC › Write access."), ToastKind.Error); }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
            LoadBotRadio();
        }

        void UpdateAiInfo()
        {
            string t = "";
            if (_ai == null || _ai.Path == null) t = T("aiDefault.ai wurde in diesem Mod nicht gefunden.", "aiDefault.ai was not found in this mod.");
            else if (_aiOverride.Checked) t = T("Achtung: Bei aktivem Überschreiben gibt die KI-Datei die Bots fest vor – Botzahl, Verteilung und Schwierigkeit des Managers wirken dann nicht. Empfohlen: aus.", "Note: with override on, the AI file forces its own bots – the manager's bot count, distribution and difficulty have no effect. Recommended: off.");
            int wanted = 0; foreach (BotEditor ed in _botEd.Values) wanted = Math.Max(wanted, Planner.Plan(ed.Opt, _hUS.Value, _hMEC.Value).Requested);
            if (wanted > Planner.EngineMaxBots) t += (t.Length > 0 ? "\n" : "") + T("BF2 setzt höchstens 32 Bots ein – eingestellt sind bis zu ", "BF2 uses at most 32 bots – you set up to ") + wanted + ".";
            if (_aiInfo.Text != t) { _aiInfo.Text = t; _botHost.Masonry(_botCards, 3); }
            _cAi.NoteColor = _aiOverride.Checked ? Theme.Amber : Theme.Dim; _cAi.Invalidate();
        }

        List<string> NamesFromBox()
        {
            List<string> r = new List<string>();
            foreach (string l in _names.Text.Replace("\r", "").Split('\n')) { string n = BotNames.Clean(l); if (n.Length > 0) r.Add(n); }
            return r;
        }

        void UpdateNamesNote()
        {
            if (_namesPath == null) { _cNames.SetNote(T("botnames.ai fehlt", "botnames.ai missing")); return; }
            List<string> n = NamesFromBox(); int single = 0;
            foreach (string s in n) if (s.IndexOf(' ') < 0) single++;
            _cNames.NoteColor = single > 0 ? Theme.Amber : Theme.Dim;
            _cNames.SetNote(n.Count + T(" Namen", " names") + (single > 0 ? " · " + single + T(" einteilig", " single-word") : ""));
        }

        void ResetBotDefaults()
        {
            if (!DarkDialog.Confirm(this, T("Bot-Standardwerte", "Bot defaults"), T("Bot-Werte für 16er, 32er und 64er Maps auf Standard zurücksetzen?\nEigene Werte einzelner Maps bleiben erhalten.", "Reset bot values for 16, 32 and 64 player maps?\nPer-map values are kept."), T("Zurücksetzen", "Reset"), false)) return;
            CoopState fresh = new CoopState();
            foreach (int s in new int[] { 16, 32, 64 }) _botEd[s].SetOpt(fresh.Defaults[s]);
            MarkDirty(); RefreshRot();
            Toast(T("Bot-Standardwerte gesetzt – zum Übernehmen speichern.", "Bot defaults set – save to apply."), ToastKind.Info);
        }
    }
}
