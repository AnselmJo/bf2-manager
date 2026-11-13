// Hauptfenster: Rotation – Rückgängig, Kontextmenü, Zeilentext - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace BF2Coop
{
    public partial class MainForm
    {
        static readonly string[] SizeFilters = { "", "16", "32", "64" };
        Segmented _sizeFilter; FlatButton _bShuffle, _bUndo; ContextMenuStrip _rotMenu;
        List<MapEntry> _undo;

        void PushUndo()
        {
            _undo = new List<MapEntry>();
            foreach (MapEntry m in _st.Maps) { MapEntry c = MapEntry.Make(m.Name, m.Mode, m.Size); c.Opt = m.Opt != null ? m.Opt.Clone() : null; _undo.Add(c); }
            _bUndo.Visible = true;
        }

        void Undo()
        {
            if (_undo == null) return;
            _st.Maps = _undo; _undo = null; _bUndo.Visible = false;
            _curRot = null; RefreshRot(_st.Maps.Count > 0 ? 0 : -1); OnRotSelect(); RefreshAvail(); MarkDirty();
        }

        // Zeilentext in der Rotation: Bots, Schwierigkeit, Stern bei eigenen Werten
        string RowText(MapEntry m) { return RowText(m, true); }
        string RowText(MapEntry m, bool star)
        {
            BotOpt o = OptFor(m);
            BotPlan p = Planner.Plan(o, _hUS.Value, _hMEC.Value);
            return p.Count + " Bots · " + BotEditor.DiffWord(o.Diff) + (star && m.Opt != null ? "  ·  ★ " + T("eigene Werte", "custom values") : "");
        }

        void BuildRotMenu()
        {
            _rotMenu.Items.Clear();
            int i = _lbRot.SelectedIndex;
            if (i < 0 || i >= _st.Maps.Count) return;
            MapEntry m = _st.Maps[i];
            ToolStripMenuItem own = new ToolStripMenuItem(T("★ Eigene Bot-Werte", "★ Custom bot values")); own.Checked = m.Opt != null; own.Enabled = _coop;
            own.Click += delegate { _ovToggle.Checked = m.Opt == null; };
            ToolStripMenuItem up = new ToolStripMenuItem(T("Nach oben", "Move up")); up.Enabled = i > 0; up.Click += delegate { MoveRot(-1); };
            ToolStripMenuItem down = new ToolStripMenuItem(T("Nach unten", "Move down")); down.Enabled = i < _st.Maps.Count - 1; down.Click += delegate { MoveRot(1); };
            ToolStripMenuItem dup = new ToolStripMenuItem(T("Duplizieren", "Duplicate"));
            dup.Click += delegate { MapEntry c = m.Clone(); _st.Maps.Insert(i + 1, c); RefreshRot(i + 1); MarkDirty(); };
            ToolStripMenuItem del = new ToolStripMenuItem(T("Entfernen (Entf)", "Remove (Del)")); del.Click += delegate { RemoveRot(); };
            // Spaßregeln: Regel-Set direkt zuweisen
            ToolStripMenuItem fun = new ToolStripMenuItem(T("⚡ Spaßregeln", "⚡ Fun rules"));
            string cur = FunSets.SetNameOf(m);
            ToolStripMenuItem none = new ToolStripMenuItem(T("Normal", "Normal")); none.Checked = cur == null; none.Click += delegate { AssignFunSet(m, null); };
            fun.DropDownItems.Add(none);
            foreach (string n in FunSets.Names())
            {
                string nn = n; ToolStripMenuItem it = new ToolStripMenuItem(n); it.Checked = string.Equals(cur, n, StringComparison.OrdinalIgnoreCase);
                it.Click += delegate { AssignFunSet(m, nn); };
                fun.DropDownItems.Add(it);
            }
            _rotMenu.Items.AddRange(new ToolStripItem[] { own, fun, new ToolStripSeparator(), up, down, dup, new ToolStripSeparator(), del });
        }

        // ---- Live: kommende Maps
        Card _cUpcoming; Para _upText; string _upSig = "";

        void BuildUpcoming()
        {
            _cUpcoming = new Card(T("Kommende Maps", "Upcoming maps"), G.Next);
            _upText = new Para(""); _upText.Color = Theme.Text;
            Para hint = new Para(T("Änderungen an Rotation und Bots wirken ab der nächsten Map.", "Changes to rotation and bots apply from the next map.")); hint.Color = Theme.Mute; hint.Font = Theme.Small;
            FlatButton bEdit = new FlatButton(T("Bearbeiten", "Edit"), G.Edit, BtnKind.Ghost);
            bEdit.Click += delegate { _forceConfig = true; SelectPage(PgMaps); };
            ButtonRow br = Buttons(bEdit);
            foreach (Control c in new Control[] { _upText, hint, br }) _cUpcoming.Controls.Add(c);
            _cUpcoming.Layouter = Stack(_cUpcoming, delegate { return new List<Control> { _upText, hint, br }; });
        }

        void RefreshUpcoming()
        {
            if (_upText == null) return;
            int cur = CurrentMapIndex(), n = _st.Maps.Count;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int k = 0; k < Math.Min(4, n); k++)
            {
                int i = cur < 0 ? k : (cur + k) % n;
                MapEntry m = _st.Maps[i];
                sb.Append(k == 0 && cur >= 0 ? T("▶ jetzt   ", "▶ now     ") : (k == 1 && cur >= 0 ? T("→ nächste  ", "→ next     ") : "     ")).Append(Util.Pretty(m.Name)).Append("  ").Append(m.Size);
                if (_coop) sb.Append("  ·  ").Append(RowText(m));
                sb.Append('\n');
            }
            string t = n == 0 ? T("Rotation ist leer.", "Rotation is empty.") : sb.ToString().TrimEnd();
            if (t == _upSig) return;
            _upSig = t; _upText.Text = t;
            if (_page == PgLive) LayoutLive();
        }
    }
}
