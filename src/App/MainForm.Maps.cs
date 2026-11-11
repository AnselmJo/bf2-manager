// Hauptfenster: Seite Maps - BF2 Manager. C# 5 (.NET Framework 4.x).
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
        // ================================================================== Seite Maps
        void BuildMaps()
        {
            PageHeader ph = NewPage(2, "Server", T("Rotation zusammenstellen, Bots und Spaßregeln pro Map einstellen, oben „Server starten“", "Build the rotation, set bots and fun rules per map, then “Start server” at the top"));
            ContextMenuStrip cfgMenu = Theme.Menu();
            cfgMenu.Opening += delegate { BuildConfigMenu(cfgMenu); };
            FlatButton bRecent = new FlatButton(T("Zuletzt & Vorlagen", "Recent & presets"), G.Refresh, BtnKind.Secondary); bRecent.DropMenu = cfgMenu; bRecent.MenuOnly = true; bRecent.Fit();
            Theme.Tip.SetToolTip(bRecent, T("Zuletzt gestartete Konfigurationen wieder laden, Vorlagen laden oder speichern.", "Reload recently started configurations, load or save presets."));
            ph.AddRight(bRecent);
            _mapsBody = new BufferedPanel(); _mapsBody.Dock = DockStyle.Fill; _mapsBody.BackColor = Theme.Bg;
            BuildFlowBar();
            Dock3(_pages[2], ph, _flowBar, _mapsBody);

            _cAvail = new Card(T("Verfügbar", "Available"), G.Globe);
            _filter = new TextField(G.Search, T("Map suchen", "Search map"));
            _filter.ValueChanged += delegate { RefreshAvail(); };
            _lbAvail = new MapListBox(); _lbAvail.SelectionMode = SelectionMode.MultiExtended;
            _lbAvail.DoubleClick += delegate { AddSelected(); };
            _lbAvail.SelectedIndexChanged += delegate { AvailMap a = SelAvail(); if (a != null) ShowPreview(a, null); };
            _lbAvail.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { AddSelected(); e.Handled = true; } };
            _bAdd = new FlatButton(T("Zur Rotation hinzufügen", "Add to rotation"), G.Add, BtnKind.Secondary);
            _bAdd.Click += delegate { AddSelected(); };
            _availEmpty = new TextLabel(T("Lade Maps …", "Loading maps …"), Theme.Body); _availEmpty.Color = Theme.Mute;
            foreach (Control c in new Control[] { _filter, _lbAvail, _bAdd, _availEmpty }) _cAvail.Controls.Add(c);
            _availEmpty.BringToFront();

            _cRot = new Card("Rotation", G.Refresh);
            _sizeFilter = new Segmented(T("Alle", "All"), "16", "32", "64");
            _sizeFilter.SelectedIndexChanged += delegate { RefreshAvail(); };
            _cAvail.Controls.Add(_sizeFilter);
            _bShuffle = new FlatButton(null, G.Sync, BtnKind.Ghost); Theme.Tip.SetToolTip(_bShuffle, T("Reihenfolge mischen", "Shuffle order"));
            _bShuffle.Click += delegate { if (_st.Maps.Count < 2) return; PushUndo(); Random rnd = new Random(); for (int i = _st.Maps.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); MapEntry t = _st.Maps[i]; _st.Maps[i] = _st.Maps[j]; _st.Maps[j] = t; } RefreshRot(0); MarkDirty(); };
            _bUndo = new FlatButton(null, G.Undo, BtnKind.Secondary); _bUndo.Visible = false;
            Theme.Tip.SetToolTip(_bUndo, T("Rückgängig\nMacht die letzte Änderung an der Rotation rückgängig.", "Undo\nReverts the last change to the rotation."));
            _bUndo.Click += delegate { Undo(); };
            _rotMenu = Theme.Menu();
            _rotMenu.Opening += delegate { BuildRotMenu(); };
            _lbRot = new MapListBox(); _lbRot.AllowReorder = true;
            _lbRot.Reordered += delegate (int from, int to) { MapEntry m = _st.Maps[from]; _st.Maps.RemoveAt(from); _st.Maps.Insert(to, m); RefreshRot(to); MarkDirty(); };
            _lbRot.SelectedIndexChanged += delegate { OnRotSelect(); };
            _lbRot.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Delete) { RemoveRot(); e.Handled = true; } };
            _bUp = new FlatButton(null, G.Up, BtnKind.Ghost); Theme.Tip.SetToolTip(_bUp, T("Nach oben", "Move up"));
            _bDown = new FlatButton(null, G.Down, BtnKind.Ghost); Theme.Tip.SetToolTip(_bDown, T("Nach unten", "Move down"));
            _bDel = new FlatButton(null, G.Remove, BtnKind.Ghost); Theme.Tip.SetToolTip(_bDel, T("Entfernen (Entf)", "Remove (Del)"));
            _bClear = new FlatButton(null, G.Delete, BtnKind.Ghost); Theme.Tip.SetToolTip(_bClear, T("Leeren\nEntfernt alle Maps aus der Rotation. Rückgängig holt sie zurück.", "Clear\nRemoves all maps from the rotation. Undo brings them back."));
            // Kompakte Symbolleiste, damit alles auch in schmalen Fenstern in eine Zeile passt
            foreach (FlatButton b in new FlatButton[] { _bUp, _bDown, _bDel, _bShuffle, _bUndo, _bClear }) { b.Height = Dpi.D(30); b.Fit(); }
            _bUp.Click += delegate { MoveRot(-1); };
            _bDown.Click += delegate { MoveRot(1); };
            _bDel.Click += delegate { RemoveRot(); };
            _bClear.Click += delegate
            {
                if (_st.Maps.Count == 0) return;
                PushUndo(); _st.Maps.Clear(); RefreshRot(-1); RefreshAvail(); MarkDirty();
                Toast(T("Rotation geleert – der Rückgängig-Knopf holt sie zurück.", "Rotation cleared – the undo button brings it back."), ToastKind.Info);
            };
            _rotEmpty = new TextLabel(T("Noch keine Maps – links doppelklicken", "No maps yet – double-click on the left"), Theme.Body); _rotEmpty.Color = Theme.Mute;
            foreach (Control c in new Control[] { _lbRot, _bUp, _bDown, _bDel, _bShuffle, _bUndo, _bClear, _rotEmpty }) _cRot.Controls.Add(c);
            _lbRot.MouseDown += delegate (object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right) return;
                int i = _lbRot.IndexFromPoint(e.Location);
                if (i < 0) return;
                _lbRot.SelectedIndex = i;
                _rotMenu.Show(_lbRot, e.Location);
            };
            _rotEmpty.BringToFront();

            _mapRight = new ScrollHost();
            _preview = new MapPreview();
            _cOv = new Card(T("Bot-Werte", "Bot values"), G.Robot);
            _ovToggle = new ToggleSwitch();
            _ovLabel = new TextLabel(T("Eigene Werte", "Custom values"), Theme.BodyB);
            Theme.Tip.SetToolTip(_ovToggle, T("Eigene Bot-Werte\nAus: Diese Map nutzt die Werte ihrer Mapgröße von der Seite Bots.\nAn: Diese Map bekommt eigene Werte und ist in der Rotation mit einem Stern markiert.", "Custom bot values\nOff: this map uses the values of its map size from the Bots page.\nOn: this map gets its own values and is marked with a star in the rotation."));
            _ovEd = new BotEditor();
            _ovInfo = new Para("");
            foreach (Control c in new Control[] { _ovToggle, _ovLabel, _ovEd, _ovInfo }) _cOv.Controls.Add(c);
            _cOv.HeaderControls.Add(_ovToggle); _cOv.HeaderControls.Add(_ovLabel);
            _cOv.Layouter = delegate (int w)
            {
                _ovToggle.Location = new Point(w - Dpi.D(20) - _ovToggle.Width, Dpi.D(13));
                _ovLabel.Location = new Point(_ovToggle.Left - Dpi.D(8) - _ovLabel.Width, Dpi.D(9));
                _cOv.HeaderReserve = w - _ovLabel.Left - Dpi.D(8);
                bool custom = _curRot != null && _curRot.Opt != null;
                _ovEd.Visible = custom; _ovInfo.Visible = !custom;
                if (custom)
                {
                    int h = _ovEd.LayoutFor(w - Dpi.D(40));
                    _ovEd.SetBounds(Dpi.D(20), _cOv.HeaderH, w - Dpi.D(40), h);
                    return _cOv.HeaderH + h + Dpi.D(18);
                }
                int ph2 = _ovInfo.HeightFor(w - Dpi.D(44));
                _ovInfo.SetBounds(Dpi.D(22), _cOv.HeaderH - Dpi.D(4), w - Dpi.D(44), ph2);
                return _cOv.HeaderH + ph2 + Dpi.D(12);
            };
            _ovToggle.CheckedChanged += delegate { OnOverrideToggle(); };
            _ovEd.Changed += delegate { if (_curRot != null && _curRot.Opt != null) { _curRot.Opt = _ovEd.Opt; RefreshRot(); MarkDirty(); } };
            BuildFunMapCard();
            _mapRight.Canvas.Controls.Add(_preview); _mapRight.Canvas.Controls.Add(_cOv); _mapRight.Canvas.Controls.Add(_cFunMap);

            _mapsBody.Controls.Add(_cAvail); _mapsBody.Controls.Add(_cRot); _mapsBody.Controls.Add(_mapRight);
            _mapsBody.Resize += delegate { LayoutMaps(); };
            _mapRight.Resize += delegate { LayoutMapRight(); };
        }

        void LayoutMaps()
        {
            if (_mapsBody.Width < 10) return;
            int W = _mapsBody.ClientSize.Width, H = _mapsBody.ClientSize.Height, pad = Dpi.D(24), gap = Dpi.D(16), top = Dpi.D(4);
            int rightW = Util.Clamp((int)(W * 0.36), Dpi.D(300), Dpi.D(520));
            int listW = Math.Max(Dpi.D(210), (W - 2 * pad - 2 * gap - rightW) / 2);
            int h = Math.Max(Dpi.D(300), H - top - pad);
            _cAvail.SetBounds(pad, top, listW, h);
            _cRot.SetBounds(pad + listW + gap, top, listW, h);
            _mapRight.SetBounds(_cRot.Right + gap, top, Math.Max(Dpi.D(200), W - _cRot.Right - gap), H - top);
            int cw = listW, hh = _cAvail.HeaderH;
            _filter.SetBounds(Dpi.D(16), hh - Dpi.D(4), cw - Dpi.D(32), Dpi.D(32));
            _sizeFilter.Location = new Point(Dpi.D(16), hh + Dpi.D(34));
            _lbAvail.SetBounds(Dpi.D(8), hh + Dpi.D(72), cw - Dpi.D(16), h - hh - Dpi.D(72) - Dpi.D(58));
            _bAdd.SetBounds(Dpi.D(16), h - Dpi.D(50), cw - Dpi.D(32), Dpi.D(34));
            _availEmpty.Location = new Point((cw - _availEmpty.Width) / 2, hh + Dpi.D(60));
            _lbRot.SetBounds(Dpi.D(8), hh - Dpi.D(4), cw - Dpi.D(16), h - hh + Dpi.D(4) - Dpi.D(58));
            int bx = Dpi.D(12);
            // Symbolknöpfe links (Rückgängig direkt neben Mischen), „Leeren“ rechts – nichts überlappt
            foreach (FlatButton b in new FlatButton[] { _bUp, _bDown, _bDel, _bShuffle, _bUndo }) { b.Location = new Point(bx, h - Dpi.D(48)); bx += b.Width + Dpi.D(2); }
            _bClear.Location = new Point(Math.Max(bx + Dpi.D(2), cw - Dpi.D(12) - _bClear.Width), h - Dpi.D(48));
            _rotEmpty.Location = new Point((cw - _rotEmpty.Width) / 2, hh + Dpi.D(40));
            LayoutMapRight();
        }

        void LayoutMapRight()
        {
            int w = _mapRight.ContentWidth - Dpi.D(20);
            if (w < 50) return;
            _preview.SetBounds(0, 0, w, w);
            int y = w + Dpi.D(16);
            bool showOv = _coop && _curRot != null;
            _cOv.Visible = showOv;
            if (showOv) { int h = _cOv.LayoutFor(w); _cOv.SetBounds(0, y, w, h); y += h + Dpi.D(16); }
            bool showFun = _curRot != null;
            _cFunMap.Visible = showFun;
            if (showFun) { int h = _cFunMap.LayoutFor(w); _cFunMap.SetBounds(0, y, w, h); y += h + Dpi.D(24); }
            _mapRight.SetContentHeight(y);
        }

        void RefreshAvail()
        {
            string f = (_filter.Value ?? "").Trim().ToLowerInvariant();
            HashSet<string> inRot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MapEntry m in _st.Maps) inRot.Add(m.Key);
            _availView = new List<AvailMap>();
            _lbAvail.BeginUpdate();
            _lbAvail.Items.Clear();
            int n = 0;
            foreach (AvailMap a in _avail)
            {
                if (a.Mode != ModeName) continue;
                if (_sizeFilter.SelectedIndex > 0 && a.Size.ToString() != SizeFilters[_sizeFilter.SelectedIndex]) continue;
                n++;
                if (f.Length > 0 && a.Display.ToLowerInvariant().IndexOf(f) < 0 && a.Name.ToLowerInvariant().IndexOf(f) < 0 && a.Size.ToString() != f) continue;
                RowView v = new RowView();
                v.Title = a.Display; v.Badge = a.Size.ToString(); v.BadgeColor = Theme.SizeColor(a.Size);
                v.Sub = a.Size + T(" Spieler", " players") + (inRot.Contains(a.Key) ? T("  ·  in der Rotation", "  ·  in rotation") : "");
                _lbAvail.Items.Add(v); _availView.Add(a);
            }
            _lbAvail.EndUpdate();
            _cAvail.SetNote(_scanning ? T("lädt …", "loading …") : n + T(" Varianten", " variants"));
            _availEmpty.Text = _scanning ? T("Lade Maps …", "Loading maps …") : (n == 0 ? T("Keine Maps für diesen Modus", "No maps for this mode") : T("Keine Treffer", "No matches"));
            _availEmpty.Visible = _lbAvail.Items.Count == 0;
            _availEmpty.Location = new Point((_cAvail.Width - _availEmpty.Width) / 2, _availEmpty.Top);
        }

        AvailMap SelAvail() { int i = _lbAvail.SelectedIndex; return i >= 0 && i < _availView.Count ? _availView[i] : null; }

        void AddSelected()
        {
            List<int> idx = new List<int>();
            foreach (int i in _lbAvail.SelectedIndices) idx.Add(i);
            if (idx.Count == 0) { Toast(T("Links eine Map auswählen.", "Select a map on the left."), ToastKind.Info); return; }
            foreach (int i in idx) { AvailMap a = _availView[i]; _st.Maps.Add(MapEntry.Make(a.Name, a.Mode, a.Size)); }
            RefreshRot(_st.Maps.Count - 1); RefreshAvail(); MarkDirty();
        }

        void MoveRot(int d)
        {
            int i = _lbRot.SelectedIndex, j = i + d;
            if (i < 0 || j < 0 || j >= _st.Maps.Count) return;
            MapEntry m = _st.Maps[i]; _st.Maps.RemoveAt(i); _st.Maps.Insert(j, m); RefreshRot(j); MarkDirty();
        }

        void RemoveRot()
        {
            int i = _lbRot.SelectedIndex;
            if (i < 0 || i >= _st.Maps.Count) return;
            PushUndo(); _st.Maps.RemoveAt(i); RefreshRot(Math.Min(i, _st.Maps.Count - 1)); RefreshAvail(); MarkDirty();
        }

        BotOpt DefaultOpt(int size) { BotEditor ed; return _botEd.TryGetValue(size, out ed) ? ed.Opt : BotOpt.Make(8, 30); }
        BotOpt OptFor(MapEntry m) { return m.Opt ?? DefaultOpt(m.Size); }

        string PlanText(BotOpt o)
        {
            BotPlan p = Planner.Plan(o, _hUS.Value, _hMEC.Value);
            return p.Count + " Bots · US " + p.TotalUS + " / MEC " + p.TotalMEC;
        }

        void RefreshRot() { RefreshRot(_lbRot.SelectedIndex); }
        void RefreshRot(int sel)
        {
            _lbRot.BeginUpdate();
            _lbRot.Items.Clear();
            for (int i = 0; i < _st.Maps.Count; i++)
            {
                MapEntry m = _st.Maps[i];
                RowView v = new RowView();
                v.Index = (i + 1).ToString("00"); v.Title = Util.Pretty(m.Name); v.Badge = m.Size.ToString(); v.BadgeColor = Theme.SizeColor(m.Size);
                v.Star = m.Opt != null;
                v.Warn = _avail.Count > 0 && !_availIx.ContainsKey(m.Key);
                v.Sub = v.Warn ? T("nicht im Mod gefunden", "not found in mod") : (_coop ? RowText(m, false) : "Conquest");
                string fs = FunShort(m); if (fs != null) v.Sub += "  ·  ⚡ " + fs;
                _lbRot.Items.Add(v);
            }
            if (sel >= 0 && sel < _lbRot.Items.Count) _lbRot.SelectedIndex = sel;
            _lbRot.EndUpdate();
            _rotEmpty.Visible = _st.Maps.Count == 0;
            _cRot.SetNote(_st.Maps.Count == 1 ? "1 Map" : _st.Maps.Count + " Maps");
            _nav[2].Badge = _st.Maps.Count > 0 ? _st.Maps.Count.ToString() : null; _nav[2].Invalidate();
            if (_st.Maps.Count == 0 && _curRot != null) { _curRot = null; UpdateOverrideUi(); }
            else if (_curRot != null) UpdateOvInfo();
            RefreshFlowBar();
        }

        void OnRotSelect()
        {
            int i = _lbRot.SelectedIndex;
            MapEntry m = i >= 0 && i < _st.Maps.Count ? _st.Maps[i] : null;
            if (m == _curRot) return;
            _curRot = m;
            UpdateFunMapUi();
            UpdateOverrideUi();
            if (m != null) { AvailMap a; _availIx.TryGetValue(m.Key, out a); ShowPreview(a, m); }
        }

        void UpdateOvInfo()
        {
            if (_curRot == null) return;
            _ovInfo.Text = T("Nutzt die Werte der ", "Uses the values of ") + _curRot.Size + T("er Maps (Seite Bots): ", "-player maps (Bots page): ") + PlanText(DefaultOpt(_curRot.Size)) + ".";
        }

        void UpdateOverrideUi()
        {
            bool old = _applying; _applying = true;
            if (_curRot != null)
            {
                _ovToggle.SetSilent(_curRot.Opt != null);
                _ovEd.SetOpt(OptFor(_curRot));
                _ovEd.SetContext(_curRot.Size, _hUS.Value, _hMEC.Value);
                UpdateOvInfo();
            }
            _applying = old;
            LayoutMapRight();
        }

        void OnOverrideToggle()
        {
            if (_applying || _curRot == null) return;
            if (_ovToggle.Checked) { _curRot.Opt = DefaultOpt(_curRot.Size).Clone(); _ovEd.SetOpt(_curRot.Opt); }
            else _curRot.Opt = null;
            RefreshRot(); MarkDirty(); LayoutMapRight();
        }

        void ShowPreview(AvailMap a, MapEntry m)
        {
            string name = a != null ? a.Display : (m != null ? Util.Pretty(m.Name) : "");
            int size = a != null ? a.Size : (m != null ? m.Size : 0);
            Image img = null;
            if (a != null)
            {
                string key = _mod + "|" + a.Key;
                if (!_img.TryGetValue(key, out img)) { img = MapImages.Load(AppPaths.Cache, _mod, a); _img[key] = img; }
            }
            string sub = size + T("er Map · ", "-player map · ") + (_coop ? "Coop" : "Conquest");
            if (_coop && size > 0) sub += " · " + PlanText(m != null ? OptFor(m) : DefaultOpt(size));
            _preview.Set(img, name, sub);
        }
    }
}
