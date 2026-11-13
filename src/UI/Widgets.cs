// Widgets.cs - zusammengesetzte Bausteine (Bot-Editor, Kopfzeilen, Hinweisleiste). C# 5.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BF2Coop
{
    public class BotEditor : Panel, IAutoHeight
    {
        public readonly Segmented SegMode = new Segmented(I18n.T("Feste Anzahl", "Fixed count"), I18n.T("Auffüllen bis", "Fill up to"));
        public readonly Segmented SegDist = new Segmented(I18n.T("Gegner-Vorteil", "Enemy advantage"), "Ratio");
        public readonly NumberBox NumCount = new NumberBox(0, 128, 8, "Bots");
        public readonly NumberBox NumDist = new NumberBox(0, 300, 20, "%");
        public readonly Slider Diff = new Slider(0, 100, 30);
        public readonly TeamBar Bar = new TeamBar();
        readonly InfoIcon _iMode, _iCount, _iDist, _iDistVal, _iDiff;
        readonly Rectangle[] _lr = new Rectangle[5];
        readonly string[] _lt = new string[5];
        Rectangle _diffValR;
        int _fixed = 8, _target = 16, _enemy = 20, _ratio = 50, _size = 32, _hUS = 8, _hMEC;
        bool _silent;
        public event EventHandler Changed;

        public BotEditor()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            _lt[0] = I18n.T("Botzahl", "Bot count"); _lt[2] = I18n.T("Verteilung", "Distribution"); _lt[4] = I18n.T("Schwierigkeit", "Difficulty");
            _iMode = new InfoIcon(I18n.T(
                "Botzahl\nFeste Anzahl: Es spielen immer genau so viele Bots.\nAuffüllen bis: Bots füllen bis zur Zielzahl auf. Kommen mehr Menschen, gibt es entsprechend weniger Bots.\n\nDer Server zählt die Menschen bei jedem Mapwechsel.",
                "Bot count\nFixed count: always exactly this many bots.\nFill up to: bots fill up to the target. The more humans join, the fewer bots.\n\nThe server counts humans at every map change."));
            _iCount = new InfoIcon("");
            _iDist = new InfoIcon(I18n.T(
                "Verteilung der Bots\nGegner-Vorteil: Team MEC bekommt insgesamt X % mehr Spieler als Team US. Die Bots werden passend verteilt, auch wenn Menschen in beiden Teams spielen.\nRatio: fester Anteil der Bots im Team MEC, unabhängig von den Menschen.\n\nsv.coopBotRatio",
                "Bot distribution\nEnemy advantage: team MEC gets X % more players in total than team US. Bots are split accordingly, even if humans play on both teams.\nRatio: fixed share of bots on team MEC, regardless of humans.\n\nsv.coopBotRatio"));
            _iDistVal = new InfoIcon("");
            _iDiff = new InfoIcon(I18n.T(
                "Schwierigkeit\nZielgenauigkeit und Reaktion der Bots.\n0 = sehr leicht, 100 = sehr schwer.\n\nsv.coopBotDifficulty · Standard 30",
                "Difficulty\nAim and reaction of the bots.\n0 = very easy, 100 = very hard.\n\nsv.coopBotDifficulty · default 30"));
            Control[] all = { SegMode, SegDist, NumCount, NumDist, Diff, Bar, _iMode, _iCount, _iDist, _iDistVal, _iDiff };
            foreach (Control c in all) Controls.Add(c);
            SegMode.SelectedIndexChanged += delegate { Sync(); Fire(); };
            SegDist.SelectedIndexChanged += delegate { Sync(); Fire(); };
            NumCount.ValueChanged += delegate { if (SegMode.SelectedIndex == 1) _target = NumCount.Value; else _fixed = NumCount.Value; UpdateBar(); Fire(); };
            NumDist.ValueChanged += delegate { if (SegDist.SelectedIndex == 1) _ratio = NumDist.Value; else _enemy = NumDist.Value; UpdateBar(); Fire(); };
            Diff.ValueChanged += delegate { Invalidate(_diffValR); Fire(); };
            Sync();
        }

        public BotOpt Opt
        {
            get
            {
                BotOpt o = new BotOpt();
                o.Count = _fixed; o.Target = _target; o.Mode = SegMode.SelectedIndex == 1 ? "fill" : "fixed";
                o.DistKind = SegDist.SelectedIndex == 1 ? "ratio" : "enemy"; o.DistVal = o.DistKind == "ratio" ? _ratio : _enemy;
                o.Diff = Diff.Value;
                return o;
            }
        }

        public void SetOpt(BotOpt o)
        {
            _silent = true;
            _fixed = o.Count; _target = o.Target;
            if (o.DistKind == "ratio") _ratio = o.DistVal; else _enemy = o.DistVal;
            SegMode.SetSilent(o.Mode == "fill" ? 1 : 0);
            SegDist.SetSilent(o.DistKind == "ratio" ? 1 : 0);
            Diff.SetSilent(o.Diff);
            Sync();
            _silent = false;
        }

        public void SetContext(int size, int hUS, int hMEC) { _size = size; _hUS = hUS; _hMEC = hMEC; UpdateBar(); }
        void Fire() { if (!_silent && Changed != null) Changed(this, EventArgs.Empty); }

        void Sync()
        {
            bool fill = SegMode.SelectedIndex == 1, ratio = SegDist.SelectedIndex == 1;
            NumCount.SetRange(fill ? 1 : 0, fill ? 128 : Planner.EngineMaxBots); NumCount.SetSilent(fill ? _target : Math.Min(_fixed, Planner.EngineMaxBots)); NumCount.Suffix = fill ? I18n.T("Spieler", "players") : "Bots";
            NumDist.SetRange(0, ratio ? 100 : 300); NumDist.SetSilent(ratio ? _ratio : _enemy);
            _lt[1] = fill ? I18n.T("Spieler gesamt", "Total players") : I18n.T("Anzahl Bots", "Number of bots");
            _lt[3] = ratio ? I18n.T("Anteil Bots im Team MEC", "Bots on team MEC") : I18n.T("Team MEC größer um", "Team MEC larger by");
            _iCount.SetTip(fill
                ? I18n.T("Spieler gesamt\nZiel für Menschen und Bots zusammen. Beispiel: 32 bei 8 Menschen ergibt 24 Bots.\nBF2 setzt höchstens 32 Bots ein.\n\nsv.coopBotCount = Ziel − Menschen", "Total players\nTarget for humans and bots together. Example: 32 with 8 humans gives 24 bots.\nBF2 uses at most 32 bots.\n\nsv.coopBotCount = target − humans")
                : I18n.T("Anzahl Bots\nSo viele Bots spielen auf dieser Mapgröße, egal wie viele Menschen da sind.\nBF2 setzt höchstens 32 Bots ein, auch auf 64er Maps.\n\nsv.coopBotCount", "Number of bots\nThis many bots play on this map size, no matter how many humans join.\nBF2 uses at most 32 bots, even on 64-player maps.\n\nsv.coopBotCount"));
            _iDistVal.SetTip(ratio
                ? I18n.T("Anteil Bots im Team MEC\n100 % = alle Bots gegen euch, 50 % = Bots gleichmäßig auf beide Teams.", "Bots on team MEC\n100 % = all bots against you, 50 % = bots split evenly.")
                : I18n.T("Team MEC größer um\n0 % = gleich große Teams, 50 % = MEC hat anderthalbmal so viele Spieler, 100 % = doppelt so viele.", "Team MEC larger by\n0 % = equal teams, 50 % = MEC has one and a half times as many players, 100 % = twice as many."));
            UpdateBar();
            PerformLayout(); Invalidate();
        }

        void UpdateBar() { Bar.Set(Planner.Plan(Opt, _hUS, _hMEC), _size); }

        public static string DiffWord(int v)
        {
            if (v < 25) return I18n.T("sehr leicht", "very easy");
            if (v < 45) return I18n.T("leicht", "easy");
            if (v < 65) return I18n.T("mittel", "medium");
            if (v < 85) return I18n.T("schwer", "hard");
            return I18n.T("sehr schwer", "very hard");
        }

        public int LayoutFor(int width)
        {
            int rh = Dpi.D(40), y = 0, lx = Dpi.D(6);
            Control[] eds = { SegMode, NumCount, SegDist, NumDist };
            InfoIcon[] infos = { _iMode, _iCount, _iDist, _iDistVal, _iDiff };
            bool stackAll = false;
            foreach (int k in new int[] { 0, 2 }) if (lx + Theme.TextW(_lt[k], Theme.Body) + Dpi.D(30) + ((Segmented)eds[k]).Natural > width) stackAll = true;
            for (int i = 0; i < 5; i++)
            {
                int tw = Theme.TextW(_lt[i], Theme.Body);
                Segmented seg = i < 4 ? eds[i] as Segmented : null;
                if (seg != null) seg.Width = seg.Natural;
                bool stack = seg != null && stackAll;
                int maxLw = i < 4 && !stack ? Math.Max(Dpi.D(40), width - eds[i].Width - Dpi.D(40)) : width - Dpi.D(30);
                int lw = Math.Min(tw, maxLw);
                _lr[i] = new Rectangle(lx, y, lw + 2, rh);
                infos[i].Location = new Point(lx + lw + Dpi.D(6), y + (rh - infos[i].Height) / 2);
                if (stack)
                {
                    seg.SetBounds(0, y + rh - Dpi.D(4), width, seg.Height);
                    y += rh + seg.Height + Dpi.D(4);
                    continue;
                }
                if (i < 4) eds[i].Location = new Point(width - eds[i].Width, y + (rh - eds[i].Height) / 2);
                y += rh;
            }
            _diffValR = new Rectangle(width - Dpi.D(160), y - rh, Dpi.D(160), rh);
            Diff.SetBounds(0, y - Dpi.D(4), width, Dpi.D(26)); y += Dpi.D(30);
            Bar.SetBounds(0, y, width, Bar.Height); y += Bar.Height;
            return y;
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(BackColor);
            Color c = Enabled ? Theme.Text : Theme.Mute;
            for (int i = 0; i < 5; i++) Theme.Txt(g, _lt[i], Theme.Body, _lr[i], c, 0);
            Theme.Txt(g, Diff.Value + "  ·  " + DiffWord(Diff.Value), Theme.BodyB, _diffValR, Enabled ? Theme.AccentHi : Theme.Mute, TextFormatFlags.Right);
        }
    }

    public class PageHeader : Panel
    {
        public string Title, Sub;
        public InfoIcon TitleInfo;
        public readonly List<Control> RightItems = new List<Control>();
        public PageHeader(string title, string sub)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Title = title; Sub = sub; Dock = DockStyle.Top; Height = Dpi.D(80); BackColor = Theme.Bg;
        }
        public void AddRight(Control c) { RightItems.Add(c); Controls.Add(c); }
        public void SetTitleInfo(string tip) { TitleInfo = new InfoIcon(tip); Controls.Add(TitleInfo); }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int x = Width - Dpi.D(24);
            foreach (Control c in RightItems) { x -= c.Width; c.Location = new Point(x, Dpi.D(26) + (Dpi.D(34) - c.Height) / 2); x -= Dpi.D(10); }
            if (TitleInfo != null) TitleInfo.Location = new Point(Dpi.D(24) + Theme.TextW(Title, Theme.Title) + Dpi.D(8), Dpi.D(24));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(BackColor);
            Theme.Txt(g, Title, Theme.Title, new Rectangle(Dpi.D(24), Dpi.D(16), Theme.TextW(Title, Theme.Title) + 4, Dpi.D(32)), Theme.Text, 0);
            int sw = RightItems.Count > 0 ? RightItems[RightItems.Count - 1].Left - Dpi.D(40) : Width - Dpi.D(40);
            Theme.Txt(g, Sub, Theme.Small, new Rectangle(Dpi.D(25), Dpi.D(48), Math.Max(10, sw), Dpi.D(20)), Theme.Dim, 0);
        }
    }

    public class TextLabel : Ctl
    {
        public Color Color = Theme.Dim; int _max;
        public int MaxWidth { get { return _max; } set { if (_max != value) { _max = value; Fit(); } } }
        public TextLabel(string text, Font f) { Font = f; Height = Dpi.D(30); Text = text; Fit(); }
        void Fit() { int w = Theme.TextW(Text, Font) + 4; Width = _max > 0 ? Math.Min(w, _max) : w; }
        protected override void OnTextChanged(EventArgs e) { Fit(); Invalidate(); base.OnTextChanged(e); }
        protected override void OnPaint(PaintEventArgs e) { Graphics g = Begin(e); Theme.Txt(g, Text, Font, ClientRectangle, Color, 0); }
    }

    // Mehrzeiliger Text in einer Karte
    public class Para : Ctl
    {
        public Color Color = Theme.Dim;
        public Para(string text) { Text = text; Font = Theme.Body; }
        public int HeightFor(int w) { return Theme.Measure(Text, Font, w).Height + Dpi.D(4); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Color, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    public class HeaderBar : BufferedPanel
    {
        public int RailW;
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (LinearGradientBrush b = new LinearGradientBrush(ClientRectangle, Theme.Mix(Theme.Side, Color.White, 0.02f), Theme.Bg, 90f)) g.FillRectangle(b, ClientRectangle);
            using (HatchBrush hb = new HatchBrush(HatchStyle.WideUpwardDiagonal, Theme.A(Theme.Accent, 12), Color.Transparent)) g.FillRectangle(hb, 0, 0, RailW, Height);
            using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, 0, Height - Dpi.D(2), RailW, Dpi.D(2));
            Theme.DrawLogo(g, new Rectangle((RailW - Dpi.D(30)) / 2, Dpi.D(14), Dpi.D(30), Dpi.D(36)), Theme.Accent);
            Theme.Txt(g, "BF2", Theme.Title, new Rectangle(RailW + Dpi.D(14), Dpi.D(9), Dpi.D(160), Dpi.D(30)), Theme.Text, 0);
            Theme.Txt(g, "MANAGER", Theme.SmallB, new Rectangle(RailW + Dpi.D(16), Dpi.D(37), Dpi.D(140), Dpi.D(16)), Theme.Accent, 0);
        }
    }

    public class SideBar : BufferedPanel
    {
        public string Footer = "";
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Theme.Side);
            using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
            Theme.Txt(g, Footer, Theme.SmallB, new Rectangle(0, Height - Dpi.D(30), Width, Dpi.D(20)), Theme.Mute, TextFormatFlags.HorizontalCenter);
        }
    }

    // Hinweisleiste oberhalb einer Seite (z. B. "Server läuft")
    public class Banner : Panel
    {
        public string Text2 = ""; public string Glyph = G.Pulse; public Color Tint = Theme.Green;
        public readonly List<Control> RightItems = new List<Control>();
        public Banner()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top; Height = Dpi.D(62); BackColor = Theme.Bg; Visible = false;
        }
        public Color Surface { get { return Theme.Mix(Theme.Card, Tint, 0.10f); } }
        public void AddRight(Control c) { RightItems.Add(c); Controls.Add(c); }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int x = Width - Dpi.D(36);
            foreach (Control c in RightItems) { x -= c.Width; c.Location = new Point(x, Dpi.D(4) + (Height - Dpi.D(12) - c.Height) / 2); x -= Dpi.D(8); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(BackColor); Theme.Hq(g);
            RectangleF r = new RectangleF(Dpi.D(24), Dpi.D(4), Width - Dpi.D(48), Height - Dpi.D(12));
            Theme.Fill(g, r, Dpi.D(9), Surface);
            Theme.Stroke(g, r, Dpi.D(9), Theme.A(Tint, 110), 1);
            int x = Dpi.D(40);
            if (Theme.Icon != null) { Theme.Glyph(g, Glyph, Theme.Icon, new Rectangle(x, (int)r.Y, Dpi.D(20), (int)r.Height), Tint); x += Dpi.D(30); }
            int right = RightItems.Count > 0 ? RightItems[RightItems.Count - 1].Left - Dpi.D(12) : (int)r.Right - Dpi.D(12);
            Theme.Txt(g, Text2, Theme.Body, new Rectangle(x, (int)r.Y, Math.Max(10, right - x), (int)r.Height), Theme.Text, 0);
        }
    }

    // Knöpfe nebeneinander mit Zeilenumbruch
    public class ButtonRow : Panel
    {
        public ButtonRow() { BackColor = Theme.Card; }
        public int LayoutFor(int width)
        {
            int x = 0, y = 0, rowH = 0;
            foreach (Control c in Controls)
            {
                if (x > 0 && x + c.Width > width) { x = 0; y += rowH + Dpi.D(8); rowH = 0; }
                c.Location = new Point(x, y); x += c.Width + Dpi.D(8); rowH = Math.Max(rowH, c.Height);
            }
            return y + rowH;
        }
        public void Add(Control c) { Controls.Add(c); }
    }

    // Abend-Bestenliste
    public class LeaderTable : Ctl
    {
        List<LeaderRow> _rows = new List<LeaderRow>();
        public LeaderTable() { Font = Theme.Body; Height = Dpi.D(40); }
        public void SetRows(List<LeaderRow> rows) { _rows = rows ?? new List<LeaderRow>(); Height = Dpi.D(30) + Math.Max(1, Math.Min(_rows.Count, 12)) * Dpi.D(28); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            int[] cx = { 0, Dpi.D(34), Width - Dpi.D(250), Width - Dpi.D(180), Width - Dpi.D(120), Width - Dpi.D(56) };
            string[] hd = { "#", I18n.T("Spieler", "Player"), I18n.T("Punkte", "Score"), "K / D", I18n.T("Siege", "Wins"), I18n.T("Runden", "Rounds") };
            for (int i = 0; i < hd.Length; i++) Theme.Txt(g, hd[i], Theme.SmallB, new Rectangle(cx[i], 0, (i + 1 < cx.Length ? cx[i + 1] : Width) - cx[i] - 4, Dpi.D(26)), Theme.Dim, 0);
            using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, 0, Dpi.D(27), Width, Dpi.D(27));
            if (_rows.Count == 0) { Theme.Txt(g, I18n.T("Noch keine Runde beendet.", "No round finished yet."), Theme.Body, new Rectangle(0, Dpi.D(30), Width, Dpi.D(26)), Theme.Mute, 0); return; }
            for (int r = 0; r < _rows.Count && r < 12; r++)
            {
                LeaderRow x = _rows[r];
                int y = Dpi.D(30) + r * Dpi.D(28);
                Color c = r == 0 ? Theme.AccentHi : Theme.Text;
                string[] v = { (r + 1).ToString(), x.Name, x.Score.ToString(), x.Kills + " / " + x.Deaths, x.Wins.ToString(), x.Rounds.ToString() };
                for (int i = 0; i < v.Length; i++) Theme.Txt(g, v[i], i == 1 || r == 0 ? Theme.BodyB : Theme.Body, new Rectangle(cx[i], y, (i + 1 < cx.Length ? cx[i + 1] : Width) - cx[i] - 4, Dpi.D(26)), i == 0 ? Theme.Dim : c, 0);
            }
        }
    }

    // Bildschirmauflösungen über EnumDisplaySettings (nur Windows; sonst die aktuelle Bildschirmgröße)
    public static class DisplayModes
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency, dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }
        [DllImport("user32.dll", CharSet = CharSet.Ansi)] static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        // Format wie in Video.con: 1920x1080@60Hz, größte zuerst
        public static List<string> List()
        {
            List<string> r = new List<string>();
            List<int[]> modes = new List<int[]>();
            try
            {
                DEVMODE dm = new DEVMODE(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                for (int i = 0; EnumDisplaySettings(null, i, ref dm) && i < 2000; i++)
                    if (dm.dmBitsPerPel >= 32 && dm.dmPelsWidth >= 800 && dm.dmDisplayFrequency >= 50) modes.Add(new int[] { dm.dmPelsWidth, dm.dmPelsHeight, dm.dmDisplayFrequency });
            }
            catch { }
            if (modes.Count == 0) { Rectangle b = Screen.PrimaryScreen.Bounds; modes.Add(new int[] { b.Width, b.Height, 60 }); }
            modes.Sort(delegate (int[] a, int[] b) { int c = (b[0] * b[1]).CompareTo(a[0] * a[1]); return c != 0 ? c : b[2].CompareTo(a[2]); });
            foreach (int[] m in modes) { string s = m[0] + "x" + m[1] + "@" + m[2] + "Hz"; if (!r.Contains(s)) r.Add(s); }
            return r;
        }

        // Aktuelle Bildschirmauflösung im Video.con-Format (höchste passende Bildwiederholrate)
        public static string Current()
        {
            Rectangle b = Screen.PrimaryScreen.Bounds;
            string pre = b.Width + "x" + b.Height + "@";
            foreach (string s in List()) if (s.StartsWith(pre)) return s;
            return pre + "60Hz";
        }
    }
}
