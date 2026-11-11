// Hauptfenster: Seite Server - BF2 Manager. C# 5 (.NET Framework 4.x).
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
        // ================================================================== Seite Server
        void BuildServer()
        {
            PageHeader ph = NewPage(0, T("Server-Optionen", "Server options"), T("Alle Einstellungen des Servers – ⓘ zeigt Erklärung, Standard und ab wann eine Änderung wirkt", "All server settings – ⓘ shows explanation, default and when a change applies"));
            FlatButton bDef = new FlatButton(T("Standardwerte", "Defaults"), G.Undo, BtnKind.Ghost);
            Theme.Tip.SetToolTip(bDef, T("Alle Servereinstellungen auf Standard zurücksetzen\nWird erst mit Speichern übernommen.", "Reset all server settings to default\nApplied when you save."));
            bDef.Click += delegate { ResetToStandard(false); };
            _advToggle = new ToggleSwitch(); _advToggle.SetSilent(_cfg.B("SrvAdv", false));
            Theme.Tip.SetToolTip(_advToggle, T("Erweiterte Einstellungen\nWartezeiten, Abstimmungen, Ränge, Netzwerk, Sprachchat, Demos und Funk. Selten nötig.", "Advanced settings\nTimers, voting, ranks, network, voice chat, demos and radio. Rarely needed."));
            ph.AddRight(_advToggle); ph.AddRight(new TextLabel(T("Erweitert", "Advanced"), Theme.BodyB));

            _srvBanner = new Banner(); _srvBanner.Glyph = G.Bolt; _srvBanner.Tint = Theme.Green;
            _bLiveSend = new FlatButton(T("Live senden", "Send live"), G.Send, BtnKind.Primary);
            Theme.Tip.SetToolTip(_bLiveSend, T("Geänderte Werte mit ⚡ sofort an den laufenden Server senden (ungetestet). Zusätzlich speichern, damit sie nach einem Neustart bleiben.", "Send changed ⚡ values to the running server now (untested). Also save so they persist after a restart."));
            _bLiveSend.Click += delegate { if (DarkDialog.Confirm(this, T("Server neu starten", "Restart server"), T("Alle Spieler werden kurz getrennt und können danach wieder beitreten.", "All players are disconnected briefly and can join again afterwards."), T("Neu starten", "Restart"), false)) RestartServer(); };
            _srvBanner.AddRight(_bLiveSend);

            _srvHost = new ScrollHost(); _srvHost.Dock = DockStyle.Fill;
            Dock3(_pages[0], ph, _srvBanner, _srvHost);
            foreach (GroupDef grp in Defs.Groups)
            {
                SettingsCard c = new SettingsCard(grp.Name, grp.Glyph);
                c.Key = "srv." + grp.Id; c.Collapsible = true; c.Collapsed = _collapsed.Contains(c.Key);
                c.Tag = grp;
                foreach (SettingDef d in Defs.All) if (d.Group == grp.Id) c.Add(MakeRow(d));
                c.Hidden = grp.Adv && !_advToggle.Checked;
                _srvCards.Add(c); _srvHost.Canvas.Controls.Add(c);
                HookCollapse(c, _srvHost, _srvCards, 2);
            }
            _srvHost.Resize += delegate { _srvHost.Masonry(_srvCards, 2); };
            _advToggle.CheckedChanged += delegate
            {
                _cfg.Set("SrvAdv", _advToggle.Checked); _cfg.Save();
                Card first = null;
                // Karten ohne Gruppe (Server-Verhalten, Standard) bleiben immer sichtbar
                foreach (Card c in _srvCards) { GroupDef g = c.Tag as GroupDef; if (g != null && g.Adv) { c.Hidden = !_advToggle.Checked; if (first == null) first = c; } }
                _srvHost.Masonry(_srvCards, 2);
                if (_advToggle.Checked && first != null) _srvHost.EnsureVisible(first);
            };
        }

        FormRow MakeRow(SettingDef d)
        {
            Control ed; int ew;
            if (d.Type == SetType.Bool || d.Type == SetType.FF)
            {
                ToggleSwitch t = new ToggleSwitch(); t.CheckedChanged += delegate { RowChanged(d); }; ed = t; ew = t.Width;
            }
            else if (d.Type == SetType.Int || d.Type == SetType.Minutes)
            {
                NumberBox n = new NumberBox(d.Min, d.Max, Util.ToInt(d.Def, 0), d.Unit); n.ValueChanged += delegate { RowChanged(d); }; ed = n; ew = n.Width;
            }
            else
            {
                TextField f = new TextField(null, d.Key == "sv.password" ? T("kein Passwort", "no password") : ""); f.ValueChanged += delegate { RowChanged(d); }; ed = f; ew = Dpi.D(220);
            }
            FormRow row = new FormRow(d.Label, TipFor(d), ed, ew, true);
            row.ResetBtn.Click += delegate { SetEditor(d, d.Def); RowChanged(d); };
            _rows[d.Key] = row;
            return row;
        }

        static string TipFor(SettingDef d)
        {
            string meta = T("Standard: ", "Default: ") + Defs.DefaultText(d) + "\n" +
                (d.Live ? T("⚡ Wirkt sofort, auch bei laufendem Server", "⚡ Applies immediately, also while the server runs") : T("Wirkt beim nächsten Serverstart", "Applies at the next server start"));
            if (!d.IsSpecial) meta += "\n" + T("Schlüssel: ", "Key: ") + d.Key;
            return d.Label + "\n" + (string.IsNullOrEmpty(d.Tip) ? "" : d.Tip) + "\n\n" + meta;
        }

        string GetEditor(SettingDef d)
        {
            Control ed = _rows[d.Key].Editor;
            if (ed is ToggleSwitch) return ((ToggleSwitch)ed).Checked ? "1" : "0";
            if (ed is NumberBox) return ((NumberBox)ed).Value.ToString();
            return ((TextField)ed).Value;
        }

        void SetEditor(SettingDef d, string v)
        {
            bool old = _applying; _applying = true;
            FormRow r = _rows[d.Key];
            if (r.Editor is ToggleSwitch) ((ToggleSwitch)r.Editor).SetSilent(v == "1");
            else if (r.Editor is NumberBox) ((NumberBox)r.Editor).SetSilent(Util.ToInt(v, Util.ToInt(d.Def, 0)));
            else ((TextField)r.Editor).Value = v ?? "";
            r.SetChanged(!Defs.Same(d, v, d.Def));
            _applying = old;
        }

        void RowChanged(SettingDef d)
        {
            if (_applying) return;
            string v = GetEditor(d);
            _vals[d.Key] = v;
            _rows[d.Key].SetChanged(!Defs.Same(d, v, d.Def));
            MarkDirty();
        }

        void ApplyVals() { foreach (SettingDef d in Defs.All) { string v; if (!_vals.TryGetValue(d.Key, out v)) v = d.Def; SetEditor(d, v); } UpdateCoopHints(); }

        // Einstellungen, die der Coop-Modus ignoriert, im Titel kennzeichnen
        void UpdateCoopHints()
        {
            FormRow r;
            if (_rows.TryGetValue("sv.noVehicles", out r))
            {
                string lab = Defs.Get("sv.noVehicles").Label + (_coop ? T("   ·   nur Conquest", "   ·   Conquest only") : "");
                if (r.Label != lab) { r.Label = lab; r.PerformLayout(); r.Invalidate(); }
            }
        }

        int PendingLive()
        {
            int n = 0;
            foreach (SettingDef d in Defs.All)
            {
                if (!d.Live) continue;
                string s; _saved.TryGetValue(d.Key, out s);
                if (!Defs.Same(d, _vals[d.Key], s)) n++;
            }
            return n;
        }

        void UpdateLiveBanner()
        {
            if (_srvBanner == null) return;
            bool show = _running && !_setupMode;
            _srvBanner.Visible = show;
            foreach (KeyValuePair<string, FormRow> kv in _rows)
            {
                bool lm = show && Defs.Get(kv.Key).Live;
                if (kv.Value.LiveMark != lm) { kv.Value.LiveMark = lm; kv.Value.Invalidate(); }
            }
            if (!show) return;
            int n = PendingRestart();
            _srvBanner.Text2 = n > 0
                ? n + T(" Änderung(en) wirken erst nach einem Neustart. Werte mit ⚡ gelten schon.", " change(s) apply only after a restart. Values marked ⚡ already apply.")
                : T("Server läuft: Werte mit ⚡ wirken sofort, alle anderen nach einem Neustart.", "Server is running: values marked ⚡ apply immediately, all others after a restart.");
            _bLiveSend.Text = T("Jetzt neu starten", "Restart now"); _bLiveSend.Visible = n > 0; _bLiveSend.Fit();
            _srvBanner.PerformLayout(); _srvBanner.Invalidate();
        }

        void ResetServerDefaults()
        {
            if (!DarkDialog.Confirm(this, T("Standardwerte", "Defaults"), T("Alle Servereinstellungen (auch Servername und erweiterte Werte) auf Standard zurücksetzen?\nÜbernommen wird erst beim Speichern.", "Reset all server settings (including server name and advanced values) to default?\nApplied when you save."), T("Zurücksetzen", "Reset"), false)) return;
            _vals = Defs.Defaults();
            ApplyVals(); MarkDirty();
            Toast(T("Standardwerte gesetzt – zum Übernehmen speichern.", "Defaults set – save to apply."), ToastKind.Info);
        }
    }
}
