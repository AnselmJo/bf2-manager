// Ui.cs - Design und eigene Steuerelemente (dunkles, modernes BF2-Design). C# 5.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BF2Coop
{
    // Segoe MDL2 Assets (Windows 10/11)
    public static class G
    {
        public const string Home = "\uE80F", Clock = "\uE916", Flag = "\uE7C1", Warn = "\uE7BA", Check = "\uE73E", Star = "\uE734",
            StarFill = "\uE735", Wrench = "\uE90F", Settings = "\uE713", People = "\uE716", Map = "\uE707", Pulse = "\uE9D9",
            Play = "\uE768", Stop = "\uE71A", Save = "\uE74E", Info = "\uE946", Refresh = "\uE72C", Undo = "\uE7A7", Add = "\uE710",
            Remove = "\uE738", Up = "\uE74A", Down = "\uE74B", Search = "\uE721", Game = "\uE7FC", Close = "\uE711", More = "\uE712",
            Chevron = "\uE70D", Next = "\uE893", Sync = "\uE895", Send = "\uE724", Folder = "\uE8B7", Globe = "\uE774", Power = "\uE7E8",
            Bolt = "\uE945", Delete = "\uE74D", Copy = "\uE8C8", Console = "\uE756", Robot = "\uE99A", Mic = "\uE720",
            Video = "\uE714", ChevronUp = "\uE70E", Shield = "\uEA18", Chat = "\uE8BD", Contact = "\uE77B", Edit = "\uE70F";
        public static string ByName(string n)
        {
            switch (n)
            {
                case "Home": return Home; case "Clock": return Clock; case "Flag": return Flag; case "Warn": return Warn;
                case "Check": return Check; case "Star": return Star; case "Globe": return Globe; case "Mic": return Mic;
                case "Video": return Video; case "Wrench": return Wrench; default: return Settings;
            }
        }
    }

    public static class Dpi
    {
        public static float S = 1f;
        public static int D(float v) { return (int)Math.Round(v * S); }
        public static void Init()
        {
            try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) S = g.DpiX / 96f; } catch { S = 1f; }
            if (S < 1f || S > 4f) S = 1f;
        }
    }

    public static class Native
    {
        public static readonly bool IsWin = Environment.OSVersion.Platform == PlatformID.Win32NT;
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hWnd, string app, string idList);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        public static void DpiAware() { if (IsWin) try { SetProcessDPIAware(); } catch { } }
        public static void DarkScroll(Control c) { if (IsWin) try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }
        public static void DarkTitle(Form f)
        {
            if (!IsWin) return;
            try { int v = 1; if (DwmSetWindowAttribute(f.Handle, 20, ref v, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref v, 4); } catch { }
        }
        public static void Cue(TextBox tb, string text) { if (IsWin && !string.IsNullOrEmpty(text)) try { SendMessage(tb.Handle, 0x1501, (IntPtr)1, text); } catch { } }
    }

    public static class Theme
    {
        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
        public static readonly Color Bg = C(12, 15, 18), Side = C(16, 20, 24), Card = C(23, 28, 34), CardHi = C(32, 39, 46), RowHi = C(28, 34, 40),
            Input = C(15, 19, 23), Border = C(42, 50, 58), BorderHi = C(64, 74, 84), Text = C(232, 234, 236), Dim = C(150, 160, 168),
            Mute = C(96, 106, 115), Accent = C(242, 162, 27), AccentHi = C(255, 188, 72), AccentLo = C(62, 45, 15), OnAccent = C(24, 18, 8),
            Green = C(63, 185, 80), Red = C(229, 83, 75), Amber = C(214, 158, 46), Us = C(74, 144, 226), Mec = C(217, 91, 67), Teal = C(62, 180, 160);

        public static string UiFam, IconFam;
        public static Font Body, BodyB, Small, SmallB, Head, Title, Big, Mid, Mono, Icon, IconSmall, IconBig, IconNav;
        public static DarkToolTip Tip;

        public static void Init()
        {
            HashSet<string> fams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { using (InstalledFontCollection ic = new InstalledFontCollection()) foreach (FontFamily f in ic.Families) fams.Add(f.Name); } catch { }
            UiFam = fams.Contains("Segoe UI") ? "Segoe UI" : FontFamily.GenericSansSerif.Name;
            string semi = fams.Contains("Segoe UI Semibold") ? "Segoe UI Semibold" : null;
            Body = new Font(UiFam, 9.5f);
            BodyB = semi != null ? new Font(semi, 9.5f) : new Font(UiFam, 9.5f, FontStyle.Bold);
            Small = new Font(UiFam, 8.5f);
            SmallB = semi != null ? new Font(semi, 8.5f) : new Font(UiFam, 8.5f, FontStyle.Bold);
            string hf = fams.Contains("Bahnschrift SemiBold") ? "Bahnschrift SemiBold" : (fams.Contains("Bahnschrift") ? "Bahnschrift" : UiFam);
            FontStyle hs = hf == "Bahnschrift SemiBold" ? FontStyle.Regular : FontStyle.Bold;
            Head = new Font(hf, 10f, hs); Title = new Font(hf, 16f, hs); Big = new Font(hf, 17f, hs); Mid = new Font(hf, 12.5f, hs);
            Mono = new Font(fams.Contains("Consolas") ? "Consolas" : FontFamily.GenericMonospace.Name, 9f);
            IconFam = fams.Contains("Segoe MDL2 Assets") ? "Segoe MDL2 Assets" : (fams.Contains("Segoe Fluent Icons") ? "Segoe Fluent Icons" : null);
            if (IconFam != null) { Icon = new Font(IconFam, 11f); IconSmall = new Font(IconFam, 8.5f); IconBig = new Font(IconFam, 20f); IconNav = new Font(IconFam, 14f); }
            Tip = new DarkToolTip();
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
        public static Color A(Color c, int alpha) { return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c); }
        public static Color ParentBg(Control c)
        {
            if (c.Parent is Banner) return ((Banner)c.Parent).Surface; // Knöpfe liegen auf der getönten Fläche, nicht auf dem Seitenhintergrund
            return c.Parent != null ? c.Parent.BackColor : Bg;
        }
        public static void Hq(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            using (GraphicsPath p = Round(r, rad)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }
        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            RectangleF q = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
            using (GraphicsPath p = Round(q, rad)) using (Pen pen = new Pen(c, w)) g.DrawPath(pen, p);
        }

        const TextFormatFlags Base = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        public static void Txt(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags extra)
        {
            TextRenderer.DrawText(g, s ?? "", f, r, c, Base | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | extra);
        }
        public static void Glyph(Graphics g, string glyph, Font f, Rectangle r, Color c)
        {
            if (f == null || string.IsNullOrEmpty(glyph)) return;
            TextRenderer.DrawText(g, glyph, f, r, c, Base | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        public static int TextW(string s, Font f) { return string.IsNullOrEmpty(s) ? 0 : TextRenderer.MeasureText(s, f, new Size(int.MaxValue, int.MaxValue), Base | TextFormatFlags.SingleLine).Width; }
        public static Size Measure(string s, Font f, int maxW)
        {
            return TextRenderer.MeasureText(s ?? "", f, new Size(maxW, int.MaxValue), Base | TextFormatFlags.WordBreak);
        }

        public static Color SizeColor(int size) { return size <= 16 ? Teal : (size <= 32 ? Accent : Mec); }

        // BF2-Rangabzeichen als Logo
        public static void DrawLogo(Graphics g, Rectangle r, Color c)
        {
            Hq(g);
            float w = r.Width, h = r.Height, x = r.X, y = r.Y;
            using (SolidBrush b = new SolidBrush(c))
            {
                for (int i = 0; i < 3; i++)
                {
                    float top = y + h * (0.12f + i * 0.24f);
                    PointF[] pts = {
                        new PointF(x + w * 0.08f, top + h * 0.10f), new PointF(x + w * 0.5f, top - h * 0.06f), new PointF(x + w * 0.92f, top + h * 0.10f),
                        new PointF(x + w * 0.92f, top + h * 0.26f), new PointF(x + w * 0.5f, top + h * 0.10f), new PointF(x + w * 0.08f, top + h * 0.26f) };
                    g.FillPolygon(b, pts);
                }
            }
        }

        public static ContextMenuStrip Menu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new DarkRenderer();
            m.BackColor = Card; m.ForeColor = Text; m.Font = Body;
            m.ShowImageMargin = false; m.ShowCheckMargin = true;
            return m;
        }
    }

    // ------------------------------------------------------------------ Menues
    public class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.CardHi; } }
        public override Color MenuItemBorder { get { return Theme.CardHi; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Card; } }
        public override Color CheckBackground { get { return Theme.AccentLo; } }
        public override Color CheckSelectedBackground { get { return Theme.AccentLo; } }
        public override Color CheckPressedBackground { get { return Theme.AccentLo; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.CardHi; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.CardHi; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.CardHi; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.CardHi; } }
    }

    public class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Mute; base.OnRenderItemText(e); }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Theme.Dim; base.OnRenderArrow(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Graphics g = e.Graphics; Theme.Hq(g);
            Rectangle r = e.ImageRectangle;
            using (Pen p = new Pen(Theme.Accent, Dpi.D(2)))
            {
                g.DrawLines(p, new Point[] { new Point(r.X + r.Width / 5, r.Y + r.Height / 2), new Point(r.X + r.Width * 2 / 5, r.Bottom - r.Height / 4), new Point(r.Right - r.Width / 6, r.Y + r.Height / 4) });
            }
        }
    }

    // ------------------------------------------------------------------ Tooltip (dunkel, mehrzeilig)
    // Format: "Titel\nBeschreibung\n\nMeta-Zeilen"
    public class DarkToolTip : ToolTip
    {
        public DarkToolTip()
        {
            OwnerDraw = true; InitialDelay = 250; AutoPopDelay = 30000; ReshowDelay = 80; UseAnimation = false; UseFading = false;
            BackColor = Theme.CardHi; ForeColor = Theme.Text;
            Popup += delegate (object s, PopupEventArgs e) { e.ToolTipSize = Lay(GetToolTip(e.AssociatedControl), null, Rectangle.Empty); };
            Draw += delegate (object s, DrawToolTipEventArgs e)
            {
                e.Graphics.Clear(Theme.CardHi);
                using (Pen p = new Pen(Theme.BorderHi)) e.Graphics.DrawRectangle(p, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                using (SolidBrush b = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(b, 0, 0, Dpi.D(3), e.Bounds.Height);
                Lay(e.ToolTipText, e.Graphics, e.Bounds);
            };
        }

        Size Lay(string text, Graphics g, Rectangle bounds)
        {
            text = (text ?? "").Replace("\r", "");
            int pad = Dpi.D(12), maxW = Dpi.D(340);
            string[] parts = text.Split(new string[] { "\n\n" }, 2, StringSplitOptions.None);
            string head = parts[0], meta = parts.Length > 1 ? parts[1] : null;
            int nl = head.IndexOf('\n');
            string title = nl >= 0 ? head.Substring(0, nl) : head, desc = nl >= 0 ? head.Substring(nl + 1) : null;
            bool single = desc == null && meta == null;
            Font tf = single ? Theme.Body : Theme.BodyB;
            List<object[]> blocks = new List<object[]>();
            blocks.Add(new object[] { title, tf, single ? Theme.Text : Theme.AccentHi });
            if (!string.IsNullOrEmpty(desc)) blocks.Add(new object[] { desc, Theme.Body, Theme.Text });
            if (!string.IsNullOrEmpty(meta)) blocks.Add(new object[] { meta, Theme.Small, Theme.Dim });
            int y = single ? Dpi.D(7) : pad, w = 0;
            for (int i = 0; i < blocks.Count; i++)
            {
                string s = (string)blocks[i][0]; Font f = (Font)blocks[i][1];
                Size sz = Theme.Measure(s, f, maxW);
                if (g != null)
                    TextRenderer.DrawText(g, s, f, new Rectangle(pad, y, maxW, sz.Height), (Color)blocks[i][2], TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                w = Math.Max(w, sz.Width);
                y += sz.Height + (i < blocks.Count - 1 ? Dpi.D(i == 0 ? 4 : 8) : 0);
            }
            return new Size(w + pad * 2, y + (single ? Dpi.D(7) : pad));
        }
    }

    // ------------------------------------------------------------------ Basisklasse
    public class Ctl : Control
    {
        protected bool Hover, Pressed;
        public Ctl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        }
        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected Graphics Begin(PaintEventArgs e) { Graphics g = e.Graphics; g.Clear(Theme.ParentBg(this)); Theme.Hq(g); return g; }
    }

    public interface IAutoHeight { int LayoutFor(int width); }

    // ------------------------------------------------------------------ Schaltflaeche
    public enum BtnKind { Primary, Secondary, Ghost, Danger }

    public class FlatButton : Ctl
    {
        string _glyph; BtnKind _kind; bool _dot; int _downX;
        public ContextMenuStrip DropMenu; public bool MenuOnly;

        public FlatButton(string text, string glyph, BtnKind kind)
        {
            Text = text; _glyph = glyph; _kind = kind; Font = Theme.BodyB; Cursor = Cursors.Hand; TabStop = true; Height = Dpi.D(34); Fit();
        }
        public string Glyph { get { return _glyph; } set { _glyph = value; Fit(); Invalidate(); } }
        public BtnKind Kind { get { return _kind; } set { _kind = value; Invalidate(); } }
        public bool Dot { get { return _dot; } set { if (_dot != value) { _dot = value; Invalidate(); } } }
        bool HasIcon { get { return _glyph != null && Theme.Icon != null; } }
        bool HasText { get { return !string.IsNullOrEmpty(Text); } }
        int DropW { get { return DropMenu != null && !MenuOnly ? Dpi.D(26) : 0; } }

        public void Fit()
        {
            if (!HasText) { Width = Height + DropW; return; }
            int w = Dpi.D(14) + (HasIcon ? Dpi.D(24) : 0) + Theme.TextW(Text, Font) + Dpi.D(14) + DropW;
            Width = w;
        }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Fit(); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { _downX = e.X; base.OnMouseDown(e); }
        protected override void OnClick(EventArgs e)
        {
            if (DropMenu != null && (MenuOnly || _downX >= Width - DropW)) { DropMenu.Show(this, new Point(MenuOnly ? Width - DropMenu.PreferredSize.Width : 0, Height + Dpi.D(4))); return; }
            base.OnClick(e);
        }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { _downX = 0; OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            Color bg = Color.Empty, fg, border = Color.Empty;
            switch (_kind)
            {
                case BtnKind.Primary: bg = Pressed ? Theme.Mix(Theme.Accent, Color.Black, 0.18f) : Hover ? Theme.AccentHi : Theme.Accent; fg = Theme.OnAccent; break;
                case BtnKind.Danger: bg = Pressed ? Theme.Mix(Theme.Red, Color.Black, 0.18f) : Hover ? Theme.Mix(Theme.Red, Color.White, 0.12f) : Theme.Red; fg = Color.White; break;
                case BtnKind.Secondary: bg = Pressed ? Theme.Border : Hover ? Theme.Mix(Theme.CardHi, Color.White, 0.06f) : Theme.CardHi; fg = Theme.Text; border = Hover ? Theme.BorderHi : Theme.Border; break;
                default: bg = Pressed ? Theme.Border : Hover ? Theme.CardHi : Color.Empty; fg = Hover ? Theme.Text : Theme.Dim; break;
            }
            if (!Enabled) { bg = _kind == BtnKind.Ghost ? Color.Empty : Theme.Mix(Theme.CardHi, Theme.ParentBg(this), 0.4f); fg = Theme.Mute; border = Color.Empty; }
            RectangleF r = new RectangleF(0, 0, Width, Height);
            float rad = Dpi.D(6);
            if (!bg.IsEmpty) Theme.Fill(g, r, rad, bg);
            if (!border.IsEmpty) Theme.Stroke(g, r, rad, border, 1);
            if (Focused && ShowFocusCues) Theme.Stroke(g, r, rad, Theme.AccentHi, 1);
            int x = Dpi.D(14);
            if (!HasText) { if (HasIcon) Theme.Glyph(g, _glyph, Theme.Icon, new Rectangle(0, 0, Width - DropW, Height), fg); else Theme.Txt(g, _glyph == G.More ? "•••" : "·", Theme.BodyB, new Rectangle(0, 0, Width, Height), fg, TextFormatFlags.HorizontalCenter); }
            else
            {
                if (HasIcon) { Theme.Glyph(g, _glyph, Theme.Icon, new Rectangle(x, 0, Dpi.D(16), Height), fg); x += Dpi.D(24); }
                Theme.Txt(g, Text, Font, new Rectangle(x, 0, Width - x - DropW - Dpi.D(8), Height), fg, 0);
            }
            if (DropW > 0)
            {
                using (Pen p = new Pen(Theme.A(fg, 70))) g.DrawLine(p, Width - DropW, Dpi.D(8), Width - DropW, Height - Dpi.D(8));
                if (Theme.IconSmall != null) Theme.Glyph(g, G.Chevron, Theme.IconSmall, new Rectangle(Width - DropW, 0, DropW, Height), fg);
                else Theme.Txt(g, "▾", Theme.Body, new Rectangle(Width - DropW, 0, DropW, Height), fg, TextFormatFlags.HorizontalCenter);
            }
            if (_dot) using (SolidBrush b = new SolidBrush(_kind == BtnKind.Primary ? Theme.OnAccent : Theme.Accent)) g.FillEllipse(b, Width - Dpi.D(11), Dpi.D(5), Dpi.D(7), Dpi.D(7));
        }
    }

    // ------------------------------------------------------------------ Schalter mit Animation
    public class ToggleSwitch : Ctl
    {
        bool _on; float _pos; Timer _t;
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            Size = new Size(Dpi.D(40), Dpi.D(22)); Cursor = Cursors.Hand; TabStop = true;
            _t = new Timer(); _t.Interval = 15;
            _t.Tick += delegate
            {
                float target = _on ? 1f : 0f;
                if (Math.Abs(_pos - target) <= 0.2f) { _pos = target; _t.Stop(); } else _pos += _pos < target ? 0.2f : -0.2f;
                Invalidate();
            };
        }
        public bool Checked
        {
            get { return _on; }
            set { if (_on == value) return; _on = value; _t.Start(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
        }
        public void SetSilent(bool v) { _on = v; _pos = v ? 1f : 0f; _t.Stop(); Invalidate(); }
        protected override void OnClick(EventArgs e) { Focus(); Checked = !Checked; base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; } base.OnKeyDown(e); }
        protected override void Dispose(bool disposing) { if (disposing) _t.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            float h = Height, w = Width;
            Color off = Hover ? Theme.BorderHi : Theme.Border, on = Hover ? Theme.AccentHi : Theme.Accent;
            Color track = Theme.Mix(off, on, _pos);
            if (!Enabled) track = Theme.Mix(track, Theme.ParentBg(this), 0.55f);
            Theme.Fill(g, new RectangleF(0, 0, w, h), h / 2, track);
            if (Focused && ShowFocusCues) Theme.Stroke(g, new RectangleF(0, 0, w, h), h / 2, Theme.AccentHi, 1);
            float k = h - Dpi.D(6), x = Dpi.D(3) + _pos * (w - k - Dpi.D(6));
            Color knob = Theme.Mix(Theme.Dim, Color.FromArgb(255, 250, 240), _pos);
            if (!Enabled) knob = Theme.Mix(knob, Theme.ParentBg(this), 0.5f);
            using (SolidBrush b = new SolidBrush(knob)) g.FillEllipse(b, x, Dpi.D(3), k, k);
        }
    }

    // ------------------------------------------------------------------ Zahlenfeld mit - und +
    public class NumberBox : Ctl
    {
        TextBox _tb; int _min, _max, _val, _hot, _dir; string _suffix; Timer _rep;
        public event EventHandler ValueChanged;

        public NumberBox(int min, int max, int val, string suffix)
        {
            _min = min; _max = max; _val = Util.Clamp(val, min, max); _suffix = suffix ?? "";
            Size = new Size(Dpi.D(120), Dpi.D(30)); TabStop = false;
            _tb = new TextBox();
            _tb.BorderStyle = BorderStyle.None; _tb.BackColor = Theme.Input; _tb.ForeColor = Theme.Text; _tb.Font = Theme.BodyB;
            _tb.TextAlign = HorizontalAlignment.Center; _tb.Text = _val.ToString();
            _tb.KeyPress += delegate (object s, KeyPressEventArgs e) { if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar) && !(e.KeyChar == '-' && _min < 0)) e.Handled = true; };
            _tb.KeyDown += TbKeyDown;
            _tb.Leave += delegate { Commit(); };
            _tb.GotFocus += delegate { Invalidate(); _tb.SelectAll(); };
            _tb.LostFocus += delegate { Invalidate(); };
            Controls.Add(_tb);
            _rep = new Timer();
            _rep.Tick += delegate { _rep.Interval = 60; Step(_dir); };
        }

        public int Value
        {
            get { return _val; }
            set { int v = Util.Clamp(value, _min, _max); _tb.Text = v.ToString(); if (v == _val) return; _val = v; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
        }
        public void SetSilent(int v) { _val = Util.Clamp(v, _min, _max); _tb.Text = _val.ToString(); Invalidate(); }
        public void SetRange(int min, int max) { _min = min; _max = max; SetSilent(_val); }
        public string Suffix { get { return _suffix; } set { _suffix = value ?? ""; LayoutBox(); Invalidate(); } }
        public bool WheelStep(int delta) { if (!_tb.Focused) return false; Step(delta > 0 ? 1 : -1); return true; }
        void Step(int d) { if ((ModifierKeys & Keys.Shift) != 0) d *= 10; Value = _val + d; }
        void Commit() { Value = Util.ToInt(_tb.Text, _val); }

        void TbKeyDown(object s, KeyEventArgs e)
        {
            int d = 0;
            if (e.KeyCode == Keys.Up) d = 1; else if (e.KeyCode == Keys.Down) d = -1; else if (e.KeyCode == Keys.PageUp) d = 10; else if (e.KeyCode == Keys.PageDown) d = -10;
            if (d != 0) { Commit(); Value = _val + d; e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { Commit(); _tb.SelectAll(); e.Handled = true; e.SuppressKeyPress = true; }
        }

        int Btn { get { return Dpi.D(26); } }
        void LayoutBox()
        {
            if (_tb == null) return;
            int sw = _suffix.Length > 0 ? Theme.TextW(_suffix, Theme.Small) + Dpi.D(4) : 0;
            int h = _tb.PreferredHeight;
            _tb.SetBounds(Btn, (Height - h) / 2 + 1, Math.Max(10, Width - 2 * Btn - sw), h);
        }
        protected override void OnLayout(LayoutEventArgs e) { LayoutBox(); base.OnLayout(e); }
        protected override void OnEnabledChanged(EventArgs e) { if (_tb != null) _tb.Visible = Enabled; base.OnEnabledChanged(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = e.X < Btn ? -1 : (e.X > Width - Btn ? 1 : 0);
            if (h != _hot) { _hot = h; Cursor = h != 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { _hot = 0; _rep.Stop(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (_hot == 0) { _tb.Focus(); return; }
            Commit(); _dir = _hot; Step(_dir); _rep.Interval = 400; _rep.Start();
        }
        protected override void OnMouseUp(MouseEventArgs e) { _rep.Stop(); base.OnMouseUp(e); }
        protected override void Dispose(bool disposing) { if (disposing) _rep.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            float rad = Dpi.D(6);
            Theme.Fill(g, r, rad, Theme.Input);
            Theme.Stroke(g, r, rad, _tb != null && _tb.Focused ? Theme.Accent : (Hover ? Theme.BorderHi : Theme.Border), 1);
            Color c = Enabled ? Theme.Dim : Theme.Mute;
            Color minus = _hot == -1 && Enabled ? Theme.AccentHi : c, plus = _hot == 1 && Enabled ? Theme.AccentHi : c;
            TextRenderer.DrawText(g, "\u2212", Theme.BodyB, new Rectangle(0, 0, Btn, Height), minus, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "+", Theme.BodyB, new Rectangle(Width - Btn, 0, Btn, Height), plus, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (!Enabled) Theme.Txt(g, _val.ToString(), Theme.BodyB, new Rectangle(Btn, 0, Width - 2 * Btn - (_suffix.Length > 0 ? Theme.TextW(_suffix, Theme.Small) + Dpi.D(4) : 0), Height), Theme.Mute, TextFormatFlags.HorizontalCenter);
            if (_suffix.Length > 0) Theme.Txt(g, _suffix, Theme.Small, new Rectangle(Width - Btn - Theme.TextW(_suffix, Theme.Small) - Dpi.D(4), 0, Theme.TextW(_suffix, Theme.Small) + Dpi.D(4), Height), Theme.Mute, 0);
        }
    }

    // ------------------------------------------------------------------ Textfeld
    public class TextField : Ctl
    {
        TextBox _tb; string _glyph, _cue;
        public event EventHandler ValueChanged;

        public TextField(string glyph, string cue)
        {
            _glyph = glyph; _cue = cue; Size = new Size(Dpi.D(230), Dpi.D(30));
            _tb = new TextBox();
            _tb.BorderStyle = BorderStyle.None; _tb.BackColor = Theme.Input; _tb.ForeColor = Theme.Text; _tb.Font = Theme.Body;
            _tb.TextChanged += delegate { if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); };
            _tb.GotFocus += delegate { Invalidate(); };
            _tb.LostFocus += delegate { Invalidate(); };
            _tb.HandleCreated += delegate { Native.Cue(_tb, _cue); };
            Controls.Add(_tb);
            Cursor = Cursors.IBeam;
        }
        public string Value { get { return _tb.Text; } set { _tb.Text = value ?? ""; } }
        public TextBox Box { get { return _tb; } }
        protected override void OnLayout(LayoutEventArgs e)
        {
            if (_tb == null) { base.OnLayout(e); return; }
            int x = _glyph != null && Theme.Icon != null ? Dpi.D(30) : Dpi.D(10);
            int h = _tb.PreferredHeight;
            _tb.SetBounds(x, (Height - h) / 2 + 1, Math.Max(10, Width - x - Dpi.D(10)), h);
            base.OnLayout(e);
        }
        protected override void OnMouseDown(MouseEventArgs e) { _tb.Focus(); base.OnMouseDown(e); }
        protected override void OnEnabledChanged(EventArgs e) { if (_tb != null) _tb.Visible = Enabled; base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Theme.Fill(g, r, Dpi.D(6), Theme.Input);
            Theme.Stroke(g, r, Dpi.D(6), _tb != null && _tb.Focused ? Theme.Accent : (Hover ? Theme.BorderHi : Theme.Border), 1);
            if (_glyph != null) Theme.Glyph(g, _glyph, Theme.IconSmall, new Rectangle(Dpi.D(8), 0, Dpi.D(16), Height), Theme.Mute);
            if (!Enabled && _tb != null) Theme.Txt(g, _tb.Text, Theme.Body, new Rectangle(Dpi.D(10), 0, Width - Dpi.D(20), Height), Theme.Mute, 0);
        }
    }

    // ------------------------------------------------------------------ Umschalter mit Segmenten
    public class Segmented : Ctl
    {
        string[] _items; int _sel, _hot = -1; int[] _x;
        public event EventHandler SelectedIndexChanged;
        public string[] Tips;

        public Segmented(params string[] items)
        {
            _items = items; Font = Theme.BodyB; Height = Dpi.D(30); Cursor = Cursors.Hand; TabStop = true;
            _x = new int[items.Length + 1];
            int x = Dpi.D(3);
            for (int i = 0; i < items.Length; i++) { _x[i] = x; x += Theme.TextW(items[i], Font) + Dpi.D(24); }
            _x[items.Length] = x; Width = Natural = x + Dpi.D(3);
            _nx = (int[])_x.Clone();
        }
        public readonly int Natural; readonly int[] _nx;
        // Bei anderer Breite die Segmente gleichmäßig verteilen
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_x == null || _nx == null) return;
            if (Width == Natural) { Array.Copy(_nx, _x, _x.Length); Invalidate(); return; }
            int n = _items.Length, inner = Width - Dpi.D(6);
            for (int i = 0; i <= n; i++) _x[i] = Dpi.D(3) + inner * i / n;
        }
        public int SelectedIndex
        {
            get { return _sel; }
            set { if (value == _sel || value < 0 || value >= _items.Length) return; _sel = value; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
        }
        public void SetSilent(int i) { _sel = Math.Max(0, Math.Min(_items.Length - 1, i)); Invalidate(); }
        int At(int x) { for (int i = 0; i < _items.Length; i++) if (x >= _x[i] && x < _x[i + 1]) return i; return -1; }
        protected override void OnMouseMove(MouseEventArgs e) { int h = At(e.X); if (h != _hot) { _hot = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { _hot = -1; base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { int h = At(e.X); if (h >= 0) SelectedIndex = h; base.OnMouseClick(e); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left && _sel > 0) SelectedIndex = _sel - 1;
            if (e.KeyCode == Keys.Right && _sel < _items.Length - 1) SelectedIndex = _sel + 1;
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Theme.Fill(g, r, Dpi.D(7), Theme.Input);
            Theme.Stroke(g, r, Dpi.D(7), Focused && ShowFocusCues ? Theme.AccentHi : Theme.Border, 1);
            for (int i = 0; i < _items.Length; i++)
            {
                RectangleF s = new RectangleF(_x[i], Dpi.D(3), _x[i + 1] - _x[i], Height - Dpi.D(6));
                Color fg = Enabled ? Theme.Dim : Theme.Mute;
                if (i == _sel) { Theme.Fill(g, s, Dpi.D(5), Enabled ? Theme.Accent : Theme.Mix(Theme.Border, Theme.Input, 0.3f)); fg = Enabled ? Theme.OnAccent : Theme.Dim; }
                else if (i == _hot && Enabled) { Theme.Fill(g, s, Dpi.D(5), Theme.CardHi); fg = Theme.Text; }
                Theme.Txt(g, _items[i], Font, Rectangle.Round(s), fg, TextFormatFlags.HorizontalCenter);
            }
        }
    }

    // ------------------------------------------------------------------ Auswahlliste (Dropdown)
    public class SelectBox : Ctl
    {
        List<string> _items = new List<string>(); int _sel = -1; ContextMenuStrip _menu;
        public string Caption;
        public event EventHandler SelectionChanged;

        public SelectBox(string caption, int width)
        {
            Caption = caption; Size = new Size(width, Dpi.D(34)); Cursor = Cursors.Hand; Font = Theme.BodyB; TabStop = true;
            _menu = Theme.Menu();
        }
        public void SetItems(IEnumerable<string> items, string selected)
        {
            _items = new List<string>(items); _sel = _items.IndexOf(selected); if (_sel < 0 && _items.Count > 0) _sel = 0; Invalidate();
        }
        public string SelectedItem { get { return _sel >= 0 && _sel < _items.Count ? _items[_sel] : null; } }
        public int SelectedIndex { get { return _sel; } }
        public void SetSilent(string item) { _sel = _items.IndexOf(item); Invalidate(); }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            _menu.Items.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                int idx = i;
                ToolStripMenuItem mi = new ToolStripMenuItem(_items[i]);
                mi.Checked = i == _sel;
                mi.Click += delegate { if (idx != _sel) { _sel = idx; Invalidate(); if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); } };
                _menu.Items.Add(mi);
            }
            _menu.MinimumSize = new Size(Width, 0);
            _menu.Show(this, new Point(0, Height + Dpi.D(4)));
        }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) OnClick(EventArgs.Empty); base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Theme.Fill(g, r, Dpi.D(7), Hover ? Theme.CardHi : Theme.Input);
            Theme.Stroke(g, r, Dpi.D(7), Focused && ShowFocusCues ? Theme.AccentHi : (Hover ? Theme.BorderHi : Theme.Border), 1);
            int x = Dpi.D(12);
            if (!string.IsNullOrEmpty(Caption))
            {
                Theme.Txt(g, Caption, Theme.SmallB, new Rectangle(x, 0, Width, Height), Theme.Accent, 0);
                x += Theme.TextW(Caption, Theme.SmallB) + Dpi.D(8);
            }
            Theme.Txt(g, SelectedItem ?? "–", Font, new Rectangle(x, 0, Width - x - Dpi.D(28), Height), Theme.Text, 0);
            if (Theme.IconSmall != null) Theme.Glyph(g, G.Chevron, Theme.IconSmall, new Rectangle(Width - Dpi.D(28), 0, Dpi.D(22), Height), Theme.Dim);
            else Theme.Txt(g, "▾", Theme.Body, new Rectangle(Width - Dpi.D(24), 0, Dpi.D(20), Height), Theme.Dim, 0);
        }
    }

    // ------------------------------------------------------------------ Schieberegler
    public class Slider : Ctl
    {
        int _min, _max, _val; bool _drag;
        public event EventHandler ValueChanged;

        public Slider(int min, int max, int val) { _min = min; _max = max; _val = val; Height = Dpi.D(24); Cursor = Cursors.Hand; TabStop = true; }
        public int Value
        {
            get { return _val; }
            set { int v = Util.Clamp(value, _min, _max); if (v == _val) return; _val = v; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
        }
        public void SetSilent(int v) { _val = Util.Clamp(v, _min, _max); Invalidate(); }
        public void WheelStep(int delta) { Value = _val + (delta > 0 ? 5 : -5); }
        int Pad { get { return Dpi.D(9); } }
        void FromX(int x) { int w = Math.Max(1, Width - 2 * Pad); Value = _min + (int)Math.Round((x - Pad) * (double)(_max - _min) / w); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left && Enabled) { Focus(); _drag = true; Capture = true; FromX(e.X); } }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (_drag) FromX(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _drag = false; Capture = false; }
        protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) Value = _val - 1; else if (e.KeyCode == Keys.Right) Value = _val + 1;
            else if (e.KeyCode == Keys.PageUp) Value = _val + 10; else if (e.KeyCode == Keys.PageDown) Value = _val - 10;
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            float w = Width - 2 * Pad, cy = Height / 2f, th = Dpi.D(4);
            float kx = Pad + w * (_val - _min) / Math.Max(1, _max - _min);
            Theme.Fill(g, new RectangleF(Pad, cy - th / 2, w, th), th / 2, Theme.Border);
            Color acc = Enabled ? Theme.Accent : Theme.Mute;
            Theme.Fill(g, new RectangleF(Pad, cy - th / 2, kx - Pad, th), th / 2, acc);
            float k = Dpi.D(Hover || _drag ? 16 : 14);
            using (SolidBrush b = new SolidBrush(Enabled ? Color.FromArgb(255, 250, 240) : Theme.Dim)) g.FillEllipse(b, kx - k / 2, cy - k / 2, k, k);
            using (Pen p = new Pen(acc, Dpi.D(3))) g.DrawEllipse(p, kx - k / 2 + 1, cy - k / 2 + 1, k - 2, k - 2);
            if (Focused && ShowFocusCues) using (Pen p = new Pen(Theme.A(Theme.AccentHi, 120), 1)) g.DrawEllipse(p, kx - k / 2 - 3, cy - k / 2 - 3, k + 6, k + 6);
        }
    }

    // ------------------------------------------------------------------ Info-Symbol mit Tooltip
    public class InfoIcon : Ctl
    {
        public InfoIcon(string tip) { Size = new Size(Dpi.D(18), Dpi.D(18)); Cursor = Cursors.Help; SetTip(tip); }
        public void SetTip(string tip) { Theme.Tip.SetToolTip(this, tip); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            Color c = Hover ? Theme.AccentHi : Theme.Mute;
            if (Theme.IconSmall != null) Theme.Glyph(g, G.Info, Theme.IconSmall, new Rectangle(0, 0, Width, Height), c);
            else
            {
                using (Pen p = new Pen(c, 1.3f)) g.DrawEllipse(p, 2, 2, Width - 5, Height - 5);
                Theme.Txt(g, "i", Theme.SmallB, new Rectangle(0, 0, Width, Height), c, TextFormatFlags.HorizontalCenter);
            }
        }
    }

    // ------------------------------------------------------------------ Karten und Zeilen
    public class Card : Panel, IAutoHeight
    {
        public string Title, Glyph, Note, Key;
        public bool Hidden, Collapsible, Collapsed;
        public Func<int, int> Layouter;
        public Color NoteColor = Theme.Dim;
        public readonly List<Control> HeaderControls = new List<Control>();
        public int HeaderReserve;
        public event EventHandler Toggled;

        public Card(string title, string glyph)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card; Title = title; Glyph = glyph;
        }
        public int HeaderH { get { return string.IsNullOrEmpty(Title) ? Dpi.D(14) : Dpi.D(48); } }
        // Eingeklappt: Inhalt bleibt, wird aber durch die Kartenhöhe abgeschnitten (kein Visible-Umschalten nötig)
        public int LayoutFor(int width)
        {
            int h = LayoutBody(width);
            if (!Collapsible) return h;
            return Collapsed ? HeaderH : h;
        }
        protected virtual int LayoutBody(int width) { return Layouter != null ? Layouter(width) : Height; }
        public void SetNote(string n) { Note = n; Invalidate(new Rectangle(Width / 3, 0, Width, HeaderH)); }
        public void Toggle() { Collapsed = !Collapsed; Invalidate(); if (Toggled != null) Toggled(this, EventArgs.Empty); }
        protected override void OnMouseMove(MouseEventArgs e) { Cursor = Collapsible && e.Y < HeaderH ? Cursors.Hand : Cursors.Default; base.OnMouseMove(e); }
        protected override void OnMouseClick(MouseEventArgs e) { if (Collapsible && e.Y < HeaderH && e.Button == MouseButtons.Left) Toggle(); base.OnMouseClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.ParentBg(this)); Theme.Hq(g);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Theme.Fill(g, r, Dpi.D(10), Theme.Card);
            Theme.Stroke(g, r, Dpi.D(10), Theme.Border, 1);
            if (string.IsNullOrEmpty(Title)) return;
            using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, Dpi.D(16), Dpi.D(17), Dpi.D(3), Dpi.D(14));
            int x = Dpi.D(28);
            if (Glyph != null && Theme.Icon != null) { Theme.Glyph(g, Glyph, Theme.Icon, new Rectangle(x, Dpi.D(12), Dpi.D(18), Dpi.D(24)), Theme.Dim); x += Dpi.D(26); }
            int right = Width - Dpi.D(16) - HeaderReserve;
            if (Collapsible)
            {
                Rectangle cr = new Rectangle(right - Dpi.D(18), Dpi.D(12), Dpi.D(18), Dpi.D(24));
                if (Theme.IconSmall != null) Theme.Glyph(g, Collapsed ? G.Chevron : G.ChevronUp, Theme.IconSmall, cr, Theme.Dim);
                else Theme.Txt(g, Collapsed ? "▾" : "▴", Theme.Body, cr, Theme.Dim, TextFormatFlags.HorizontalCenter);
                right -= Dpi.D(28);
            }
            int tw = Math.Min(Theme.TextW(Title.ToUpperInvariant(), Theme.Head) + 4, Math.Max(10, right - x));
            Theme.Txt(g, Title.ToUpperInvariant(), Theme.Head, new Rectangle(x, Dpi.D(12), tw, Dpi.D(24)), Theme.Text, 0);
            if (!string.IsNullOrEmpty(Note) && right - x - tw > Dpi.D(40))
                Theme.Txt(g, Note, Theme.Small, new Rectangle(x + tw + Dpi.D(10), Dpi.D(12), right - x - tw - Dpi.D(10), Dpi.D(24)), NoteColor, TextFormatFlags.Right);
        }
    }

    public class FormRow : Panel
    {
        public string Label; public Control Editor; public InfoIcon Info; public FlatButton ResetBtn; public int EditorW;
        public bool Changed, LiveMark;

        public FormRow(string label, string tip, Control editor, int editorW, bool resettable)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card; Label = label; Editor = editor; EditorW = editorW; Height = Dpi.D(40);
            if (tip != null) { Info = new InfoIcon(tip); Controls.Add(Info); }
            if (resettable)
            {
                ResetBtn = new FlatButton(null, G.Undo, BtnKind.Ghost);
                ResetBtn.Size = new Size(Dpi.D(26), Dpi.D(26)); ResetBtn.Visible = false; ResetBtn.TabStop = false;
                Theme.Tip.SetToolTip(ResetBtn, I18n.T("Auf Standardwert zurücksetzen", "Reset to default"));
                Controls.Add(ResetBtn);
            }
            Controls.Add(editor);
            HookHover(this);
        }
        void HookHover(Control c)
        {
            c.MouseEnter += delegate { SetHover(true); };
            c.MouseLeave += delegate { SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position))); };
            foreach (Control ch in c.Controls) HookHover(ch);
        }
        void SetHover(bool h) { Color c = h ? Theme.RowHi : Theme.Card; if (BackColor != c) { BackColor = c; Invalidate(true); } }
        public void SetChanged(bool ch)
        {
            if (Changed == ch && (ResetBtn == null || ResetBtn.Visible == ch)) return;
            Changed = ch; if (ResetBtn != null) ResetBtn.Visible = ch; Invalidate();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int ex = Width - Dpi.D(10) - EditorW;
            Editor.SetBounds(ex, (Height - Editor.Height) / 2, EditorW, Editor.Height);
            if (ResetBtn != null) ResetBtn.Location = new Point(ex - Dpi.D(4) - ResetBtn.Width, (Height - ResetBtn.Height) / 2);
            if (Info != null)
            {
                int lw = Math.Min(Theme.TextW(Label, Theme.Body), Math.Max(10, ex - Dpi.D(70)));
                Info.Location = new Point(Dpi.D(16) + lw + Dpi.D(6), (Height - Info.Height) / 2);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            int maxW = Width - EditorW - Dpi.D(70);
            Theme.Txt(g, Label, Theme.Body, new Rectangle(Dpi.D(16), 0, Math.Max(10, Math.Min(Theme.TextW(Label, Theme.Body) + 2, maxW)), Height), Theme.Text, 0);
            if (Changed) { Theme.Hq(g); using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, Dpi.D(5), Height / 2 - Dpi.D(3), Dpi.D(6), Dpi.D(6)); }
            if (LiveMark && Info != null)
            {
                Rectangle r = new Rectangle(Info.Right + Dpi.D(4), 0, Dpi.D(16), Height);
                if (Theme.IconSmall != null) Theme.Glyph(g, G.Bolt, Theme.IconSmall, r, Theme.AccentHi);
                else Theme.Txt(g, "⚡", Theme.Small, r, Theme.AccentHi, 0);
            }
        }
    }

    public class SettingsCard : Card
    {
        public readonly List<Control> Rows = new List<Control>();
        public SettingsCard(string title, string glyph) : base(title, glyph) { }
        public void Add(Control row) { Rows.Add(row); Controls.Add(row); }
        protected override int LayoutBody(int width)
        {
            int y = HeaderH - Dpi.D(4);
            foreach (Control r in Rows)
            {
                IAutoHeight ah = r as IAutoHeight;
                int h = ah != null ? ah.LayoutFor(width - Dpi.D(12)) : r.Height;
                r.SetBounds(Dpi.D(6), y, width - Dpi.D(12), h);
                y += h;
            }
            return y + Dpi.D(10);
        }
    }

    // ------------------------------------------------------------------ Scrollbereich mit Masonry-Layout
    // Eigenes Scrollen statt AutoScroll: Position wird immer auf den Inhalt begrenzt (kein Leerbereich), weiches Scrollen.
    public class ScrollHost : Panel
    {
        public readonly Panel Canvas;
        readonly ThinScroll _bar; readonly Timer _anim;
        int _off, _target, _contentH;

        public ScrollHost()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Theme.Bg;
            Canvas = new BufferedPanel(); Canvas.BackColor = Theme.Bg; Canvas.Location = Point.Empty;
            _bar = new ThinScroll(this);
            Controls.Add(_bar); Controls.Add(Canvas); _bar.BringToFront();
            _anim = new Timer(); _anim.Interval = 15;
            _anim.Tick += delegate
            {
                int d = _target - _off;
                if (Math.Abs(d) <= 2) { _off = _target; _anim.Stop(); } else _off += (int)Math.Round(d * 0.35);
                Apply();
            };
        }
        public int ContentWidth { get { return Math.Max(Dpi.D(200), ClientSize.Width - Dpi.D(12)); } }
        public int ContentHeight { get { return _contentH; } }
        public int Offset { get { return _off; } }
        public int Max { get { return Math.Max(0, _contentH - ClientSize.Height); } }
        public bool CanScroll { get { return Max > 0; } }

        public void SetContentHeight(int h)
        {
            _contentH = h;
            Canvas.Size = new Size(ContentWidth, Math.Max(h, ClientSize.Height));
            _target = Util.Clamp(_target, 0, Max); _off = Util.Clamp(_off, 0, Max);
            Apply();
        }
        void Apply()
        {
            if (Canvas.Top != -_off) Canvas.Top = -_off;
            _bar.SetBounds(ClientSize.Width - Dpi.D(10), Dpi.D(4), Dpi.D(8), Math.Max(0, ClientSize.Height - Dpi.D(8)));
            _bar.Visible = CanScroll;
            _bar.Invalidate();
        }
        public void ScrollBy(int delta)
        {
            int steps = Math.Max(1, Math.Abs(delta) / 120);
            ScrollTo(_target - Math.Sign(delta) * Dpi.D(96) * steps, true);
        }
        public void ScrollTo(int y, bool animate)
        {
            _target = Util.Clamp(y, 0, Max);
            if (animate) _anim.Start(); else { _off = _target; _anim.Stop(); Apply(); }
        }
        public void EnsureVisible(Control c)
        {
            int y = 0;
            for (Control p = c; p != null && p != Canvas; p = p.Parent) y += p.Top;
            if (y < _off) ScrollTo(y - Dpi.D(8), true);
            else if (y + c.Height > _off + ClientSize.Height) ScrollTo(Math.Min(y - Dpi.D(8), y + c.Height - ClientSize.Height + Dpi.D(16)), true);
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); SetContentHeight(_contentH); }
        protected override void Dispose(bool disposing) { if (disposing) _anim.Dispose(); base.Dispose(disposing); }

        public void Masonry(IList<Card> cards, int maxCols)
        {
            int w = ContentWidth, pad = Dpi.D(24), gap = Dpi.D(16);
            int cols = maxCols >= 3 && w >= Dpi.D(1060) ? 3 : (maxCols >= 2 && w >= Dpi.D(760) ? 2 : 1);
            int colW = (w - pad * 2 - gap * (cols - 1)) / cols;
            int[] hs = new int[cols];
            for (int i = 0; i < cols; i++) hs[i] = Dpi.D(4);
            Canvas.SuspendLayout();
            foreach (Card c in cards)
            {
                c.Visible = !c.Hidden;
                if (c.Hidden) continue;
                int ci = 0;
                for (int i = 1; i < cols; i++) if (hs[i] < hs[ci]) ci = i;
                int h = c.LayoutFor(colW);
                c.SetBounds(pad + ci * (colW + gap), hs[ci], colW, h);
                hs[ci] += h + gap;
            }
            int max = 0; foreach (int h in hs) max = Math.Max(max, h);
            Canvas.ResumeLayout(false);
            SetContentHeight(max + pad - gap);
        }
    }

    public class ThinScroll : Ctl
    {
        readonly ScrollHost _host; bool _drag; int _grab;
        public ThinScroll(ScrollHost host) { _host = host; Cursor = Cursors.Default; Visible = false; }
        Rectangle Thumb
        {
            get
            {
                int ch = Math.Max(1, _host.ContentHeight), vh = _host.ClientSize.Height;
                int th = Math.Max(Dpi.D(36), Height * vh / ch);
                int y = _host.Max > 0 ? (Height - th) * _host.Offset / _host.Max : 0;
                return new Rectangle(0, y, Width, th);
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Rectangle t = Thumb;
            if (t.Contains(e.Location)) { _drag = true; _grab = e.Y - t.Y; Capture = true; }
            else _host.ScrollBy(e.Y < t.Y ? 120 * 5 : -120 * 5);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_drag) return;
            int free = Math.Max(1, Height - Thumb.Height);
            _host.ScrollTo((e.Y - _grab) * _host.Max / free, false);
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _drag = false; Capture = false; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            Rectangle t = Thumb;
            Color c = _drag ? Theme.Accent : (Hover ? Theme.BorderHi : Theme.Border);
            int w = Hover || _drag ? Width : Dpi.D(5);
            Theme.Fill(g, new RectangleF(Width - w, t.Y, w, t.Height), w / 2f, c);
        }
    }

    public class BufferedPanel : Panel
    {
        public BufferedPanel() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); }
    }

    // Mausrad: scrollt immer den Bereich unter dem Mauszeiger; Zahlenfelder nur, wenn sie fokussiert sind.
    public class WheelRouter : IMessageFilter
    {
        bool _off;
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != 0x020A || _off || !Native.IsWin) return false;
            try
            {
                long lp = m.LParam.ToInt64();
                int x = (short)(lp & 0xFFFF), y = (short)((lp >> 16) & 0xFFFF);
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                Control c = Control.FromChildHandle(Native.WindowFromPoint(new Native.POINT(x, y)));
                for (Control p = c; p != null; p = p.Parent)
                {
                    NumberBox nb = p as NumberBox;
                    if (nb != null && nb.WheelStep(delta)) return true;
                    Slider sl = p as Slider;
                    if (sl != null && sl.Focused) { sl.WheelStep(delta); return true; }
                    if (p is ListBox || p is RichTextBox || (p is TextBox && ((TextBox)p).Multiline))
                    {
                        if (p.Handle == m.HWnd) return false;
                        Native.SendMessage(p.Handle, m.Msg, m.WParam, m.LParam); return true;
                    }
                    ScrollHost sh = p as ScrollHost;
                    if (sh != null) { if (sh.CanScroll) sh.ScrollBy(delta); return true; }
                }
            }
            catch { _off = true; }
            return false;
        }
    }

    // ------------------------------------------------------------------ Navigation und Status
    public class NavButton : Ctl
    {
        public string Glyph; bool _sel; public string Badge; public Color BadgeColor = Color.Empty;
        public NavButton(string text, string glyph) { Text = text; Glyph = glyph; Height = Dpi.D(62); Cursor = Cursors.Hand; Font = Theme.SmallB; }
        public bool Selected { get { return _sel; } set { _sel = value; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            RectangleF r = new RectangleF(Dpi.D(8), Dpi.D(3), Width - Dpi.D(16), Height - Dpi.D(6));
            if (_sel) Theme.Fill(g, r, Dpi.D(9), Theme.CardHi);
            else if (Hover && Enabled) Theme.Fill(g, r, Dpi.D(9), Theme.A(Theme.CardHi, 150));
            if (_sel) Theme.Fill(g, new RectangleF(Dpi.D(3), Dpi.D(18), Dpi.D(3), Height - Dpi.D(36)), Dpi.D(2), Theme.Accent);
            Color fg = !Enabled ? Theme.Mute : (_sel || Hover ? Theme.Text : Theme.Dim);
            Color ic = !Enabled ? Theme.Mute : (_sel ? Theme.Accent : fg);
            if (Theme.IconNav != null) Theme.Glyph(g, Glyph, Theme.IconNav, new Rectangle(0, Dpi.D(9), Width, Dpi.D(26)), ic);
            Theme.Txt(g, Text, Font, new Rectangle(Dpi.D(2), Theme.IconNav != null ? Dpi.D(36) : 0, Width - Dpi.D(4), Theme.IconNav != null ? Dpi.D(18) : Height), fg, TextFormatFlags.HorizontalCenter);
            if (!string.IsNullOrEmpty(Badge))
            {
                int bw = Math.Max(Dpi.D(18), Theme.TextW(Badge, Theme.SmallB) + Dpi.D(8));
                RectangleF b = new RectangleF(Width / 2 + Dpi.D(6), Dpi.D(5), bw, Dpi.D(17));
                Color bc = BadgeColor.IsEmpty ? (_sel ? Theme.AccentLo : Theme.Border) : BadgeColor;
                Theme.Fill(g, b, Dpi.D(8), bc);
                Theme.Txt(g, Badge, Theme.SmallB, Rectangle.Round(b), BadgeColor.IsEmpty ? (_sel ? Theme.AccentHi : Theme.Dim) : Color.White, TextFormatFlags.HorizontalCenter);
            }
        }
    }

    public enum SrvState { Offline, Starting, Running, NoModule }

    public class StatusPill : Ctl
    {
        SrvState _st = SrvState.Offline; Timer _t; float _phase; string _text = "Server offline";
        public StatusPill()
        {
            Height = Dpi.D(34); Cursor = Cursors.Hand; Font = Theme.BodyB; Fit();
            _t = new Timer(); _t.Interval = 40;
            _t.Tick += delegate { _phase += 0.025f; if (_phase > 1f) _phase = 0f; Invalidate(new Rectangle(0, 0, Dpi.D(36), Height)); };
        }
        public SrvState State { get { return _st; } }
        public void Set(SrvState st, string text)
        {
            if (st == _st && text == _text) return;
            _st = st; _text = text; Fit();
            if (st == SrvState.Offline) _t.Stop(); else _t.Start();
            Invalidate();
        }
        void Fit() { Width = Dpi.D(38) + Theme.TextW(_text, Font) + Dpi.D(14); }
        public Color DotColor { get { return _st == SrvState.Running ? Theme.Green : (_st == SrvState.Offline ? Theme.Mute : Theme.Amber); } }
        protected override void Dispose(bool disposing) { if (disposing) _t.Dispose(); base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Theme.Fill(g, r, Height / 2f, Hover ? Theme.CardHi : Theme.Input);
            Theme.Stroke(g, r, Height / 2f, Theme.Border, 1);
            float cx = Dpi.D(19), cy = Height / 2f, d = Dpi.D(9);
            Color c = DotColor;
            if (_st != SrvState.Offline)
            {
                float rr = d / 2 + _phase * Dpi.D(8);
                using (SolidBrush b = new SolidBrush(Theme.A(c, (int)(150 * (1 - _phase))))) g.FillEllipse(b, cx - rr, cy - rr, rr * 2, rr * 2);
            }
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
            Theme.Txt(g, _text, Font, new Rectangle(Dpi.D(34), 0, Width - Dpi.D(40), Height), _st == SrvState.Offline ? Theme.Dim : Theme.Text, 0);
        }
    }

    public class StatTile : Ctl
    {
        public string Glyph, Caption, Value = "–", Sub = ""; public Color Color = Theme.Accent;
        public StatTile(string caption, string glyph) { Caption = caption; Glyph = glyph; Height = Dpi.D(86); }
        public void Set(string value, string sub, Color c)
        {
            if (value == Value && sub == Sub && c == Color) return;
            Value = value; Sub = sub; Color = c; Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Theme.Fill(g, r, Dpi.D(10), Theme.Card);
            Theme.Stroke(g, r, Dpi.D(10), Theme.Border, 1);
            Theme.Fill(g, new RectangleF(0, Dpi.D(14), Dpi.D(3), Height - Dpi.D(28)), Dpi.D(2), Color);
            int x = Dpi.D(18);
            if (Theme.IconBig != null)
            {
                Theme.Fill(g, new RectangleF(x, (Height - Dpi.D(44)) / 2f, Dpi.D(44), Dpi.D(44)), Dpi.D(10), Theme.A(Color, 40));
                Theme.Glyph(g, Glyph, Theme.Icon, new Rectangle(x, (Height - Dpi.D(44)) / 2, Dpi.D(44), Dpi.D(44)), Color);
                x += Dpi.D(58);
            }
            int w = Width - x - Dpi.D(12);
            Theme.Txt(g, Caption.ToUpperInvariant(), Theme.SmallB, new Rectangle(x, Dpi.D(12), w, Dpi.D(18)), Theme.Dim, 0);
            Font vf = Theme.TextW(Value, Theme.Big) <= w ? Theme.Big : (Theme.TextW(Value, Theme.Mid) <= w ? Theme.Mid : Theme.Head);
            Theme.Txt(g, Value, vf, new Rectangle(x, Dpi.D(30), w, Dpi.D(30)), Theme.Text, 0);
            Theme.Txt(g, Sub, Theme.Small, new Rectangle(x, Dpi.D(60), w, Dpi.D(18)), Theme.Dim, 0);
        }
    }

    // ------------------------------------------------------------------ Teamvorschau
    public class TeamBar : Ctl
    {
        int _uh, _ub, _mh, _mb, _cap;
        public TeamBar() { Height = Dpi.D(78); }
        public void Set(BotPlan p, int cap) { _uh = p.HumansUS; _ub = p.BotsUS; _mh = p.HumansMEC; _mb = p.BotsMEC; _cap = cap; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            int us = _uh + _ub, mec = _mh + _mb, tot = Math.Max(1, us + mec);
            bool en = Enabled;
            Color cu = en ? Theme.Us : Theme.Mute, cm = en ? Theme.Mec : Theme.Mute;
            Theme.Fill(g, new RectangleF(0, 0, Width, Height), Dpi.D(8), Theme.Input);
            int pad = Dpi.D(12), w = Width - 2 * pad;
            Theme.Txt(g, "US", Theme.SmallB, new Rectangle(pad, Dpi.D(6), Dpi.D(30), Dpi.D(22)), cu, 0);
            Theme.Txt(g, us.ToString(), Theme.Head, new Rectangle(pad + Dpi.D(26), Dpi.D(6), Dpi.D(40), Dpi.D(22)), Theme.Text, 0);
            Theme.Txt(g, "MEC", Theme.SmallB, new Rectangle(Width - pad - Dpi.D(34), Dpi.D(6), Dpi.D(34), Dpi.D(22)), cm, TextFormatFlags.Right);
            Theme.Txt(g, mec.ToString(), Theme.Head, new Rectangle(Width - pad - Dpi.D(80), Dpi.D(6), Dpi.D(42), Dpi.D(22)), Theme.Text, TextFormatFlags.Right);
            bool fits = _cap <= 0 || us + mec <= _cap;
            string cap = _cap > 0 ? (us + mec) + " / " + _cap + I18n.T(" Plätze", " slots") : (us + mec) + I18n.T(" Spieler", " players");
            Theme.Txt(g, cap, Theme.SmallB, new Rectangle(pad, Dpi.D(6), w, Dpi.D(22)), fits ? Theme.Dim : Theme.Red, TextFormatFlags.HorizontalCenter);
            float by = Dpi.D(32), bh = Dpi.D(12), gap = Dpi.D(3);
            float wu = (w - gap) * us / (float)tot, wm = (w - gap) - wu;
            Theme.Fill(g, new RectangleF(pad, by, w, bh), bh / 2, Theme.Border);
            using (GraphicsPath clip = Theme.Round(new RectangleF(pad, by, w, bh), bh / 2))
            {
                g.SetClip(clip);
                float fu = us > 0 ? wu * _uh / us : 0, fm = mec > 0 ? wm * _mh / mec : 0;
                using (SolidBrush b = new SolidBrush(cu)) g.FillRectangle(b, pad, by, fu, bh);
                using (HatchBrush b = new HatchBrush(HatchStyle.WideUpwardDiagonal, Theme.A(cu, 220), Theme.A(cu, 120))) g.FillRectangle(b, pad + fu, by, wu - fu, bh);
                using (HatchBrush b = new HatchBrush(HatchStyle.WideUpwardDiagonal, Theme.A(cm, 220), Theme.A(cm, 120))) g.FillRectangle(b, pad + wu + gap, by, wm - fm, bh);
                using (SolidBrush b = new SolidBrush(cm)) g.FillRectangle(b, pad + w - fm, by, fm, bh);
                g.ResetClip();
            }
            Theme.Txt(g, Line(_uh, _ub), Theme.Small, new Rectangle(pad, Dpi.D(50), w / 2, Dpi.D(20)), Theme.Dim, 0);
            Theme.Txt(g, Line(_mh, _mb), Theme.Small, new Rectangle(pad + w / 2, Dpi.D(50), w / 2, Dpi.D(20)), Theme.Dim, TextFormatFlags.Right);
        }
        static string Line(int h, int b) { return h + (h == 1 ? I18n.T(" Mensch", " human") : I18n.T(" Menschen", " humans")) + " · " + b + " Bots"; }
    }

    // ------------------------------------------------------------------ Mapliste (dunkel, mit Drag & Drop)
    public class RowView
    {
        public string Index, Title, Sub, Badge; public Color BadgeColor = Theme.Accent; public bool Star, Warn; public object Tag;
    }

    public class MapListBox : ListBox
    {
        public bool AllowReorder; int _hot = -1, _dragFrom = -1, _insert = -1; Point _down;
        public event Action<int, int> Reordered;

        public MapListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = Dpi.D(44); BorderStyle = BorderStyle.None; IntegralHeight = false;
            BackColor = Theme.Card; ForeColor = Theme.Text; Font = Theme.Body;
            HandleCreated += delegate { Native.DarkScroll(this); };
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle r = e.Bounds;
            using (SolidBrush bb = new SolidBrush(BackColor)) g.FillRectangle(bb, r);
            if (e.Index < 0 || e.Index >= Items.Count) return;
            RowView v = Items[e.Index] as RowView;
            if (v == null) return;
            Theme.Hq(g);
            bool sel = (e.State & DrawItemState.Selected) != 0;
            RectangleF q = new RectangleF(r.X + Dpi.D(4), r.Y + Dpi.D(2), r.Width - Dpi.D(8), r.Height - Dpi.D(4));
            if (sel) { Theme.Fill(g, q, Dpi.D(7), Theme.Mix(BackColor, Theme.Accent, 0.16f)); Theme.Fill(g, new RectangleF(q.X, q.Y + Dpi.D(8), Dpi.D(3), q.Height - Dpi.D(16)), Dpi.D(2), Theme.Accent); }
            else if (e.Index == _hot) Theme.Fill(g, q, Dpi.D(7), Theme.RowHi);
            int x = r.X + Dpi.D(14);
            if (AllowReorder)
            {
                // Ziehgriff (drei Striche)
                using (Pen hp = new Pen(e.Index == _hot || sel ? Theme.Dim : Theme.Border, Math.Max(1, Dpi.D(2))))
                    for (int k = -1; k <= 1; k++) { int yy = r.Y + r.Height / 2 + k * Dpi.D(5); g.DrawLine(hp, x, yy, x + Dpi.D(12), yy); }
                x += Dpi.D(20);
            }
            if (!string.IsNullOrEmpty(v.Index)) { Theme.Txt(g, v.Index, Theme.SmallB, new Rectangle(x, r.Y, Dpi.D(24), r.Height), sel ? Theme.Accent : Theme.Mute, 0); x += Dpi.D(28); }
            int bw = string.IsNullOrEmpty(v.Badge) ? 0 : Math.Max(Dpi.D(34), Theme.TextW(v.Badge, Theme.SmallB) + Dpi.D(14));
            int right = r.Right - Dpi.D(12) - bw - (v.Star ? Dpi.D(24) : 0);
            Color tc = v.Warn ? Theme.Red : Theme.Text;
            Theme.Txt(g, v.Title, Theme.BodyB, new Rectangle(x, r.Y + Dpi.D(5), right - x, Dpi.D(18)), tc, 0);
            Theme.Txt(g, v.Sub, Theme.Small, new Rectangle(x, r.Y + Dpi.D(23), right - x, Dpi.D(16)), v.Warn ? Theme.Red : Theme.Dim, 0);
            if (bw > 0)
            {
                RectangleF b = new RectangleF(r.Right - Dpi.D(12) - bw, r.Y + (r.Height - Dpi.D(22)) / 2f, bw, Dpi.D(22));
                Theme.Fill(g, b, Dpi.D(11), Theme.A(v.BadgeColor, 46));
                Theme.Txt(g, v.Badge, Theme.SmallB, Rectangle.Round(b), v.BadgeColor, TextFormatFlags.HorizontalCenter);
            }
            if (v.Star)
            {
                Rectangle sr = new Rectangle(r.Right - Dpi.D(12) - bw - Dpi.D(24), r.Y, Dpi.D(20), r.Height);
                if (Theme.IconSmall != null) Theme.Glyph(g, G.StarFill, Theme.IconSmall, sr, Theme.Accent);
                else Theme.Txt(g, "★", Theme.Body, sr, Theme.Accent, TextFormatFlags.HorizontalCenter);
            }
            if (_insert >= 0)
            {
                int yy = -1;
                if (_insert == e.Index) yy = r.Y;
                else if (_insert == Items.Count && e.Index == Items.Count - 1) yy = r.Bottom - Dpi.D(2);
                if (yy >= 0) using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, r.X + Dpi.D(8), yy, r.Width - Dpi.D(16), Dpi.D(2));
            }
        }
        void InvalidateItem(int i) { if (i >= 0 && i < Items.Count) Invalidate(GetItemRectangle(i)); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = IndexFromPoint(e.Location);
            if (h != _hot) { int o = _hot; _hot = h; InvalidateItem(o); InvalidateItem(h); }
            if (AllowReorder && e.Button == MouseButtons.Left && _dragFrom >= 0 &&
                (Math.Abs(e.X - _down.X) > SystemInformation.DragSize.Width || Math.Abs(e.Y - _down.Y) > SystemInformation.DragSize.Height))
            {
                int from = _dragFrom; _dragFrom = -1;
                DoDragDrop(from, DragDropEffects.Move);
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { int o = _hot; _hot = -1; InvalidateItem(o); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = e.Location; _dragFrom = IndexFromPoint(e.Location); if (AllowReorder) AllowDrop = true; base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _dragFrom = -1; base.OnMouseUp(e); }
        protected override void OnDragOver(DragEventArgs e)
        {
            if (!AllowReorder || !e.Data.GetDataPresent(typeof(int))) { e.Effect = DragDropEffects.None; return; }
            e.Effect = DragDropEffects.Move;
            Point p = PointToClient(new Point(e.X, e.Y));
            int i = IndexFromPoint(p);
            if (i < 0) i = Items.Count; else if (p.Y > GetItemRectangle(i).Y + ItemHeight / 2) i++;
            if (i != _insert) { _insert = i; Invalidate(); }
            base.OnDragOver(e);
        }
        protected override void OnDragLeave(EventArgs e) { _insert = -1; Invalidate(); base.OnDragLeave(e); }
        protected override void OnDragDrop(DragEventArgs e)
        {
            int to = _insert; _insert = -1; Invalidate();
            if (!e.Data.GetDataPresent(typeof(int))) return;
            int from = (int)e.Data.GetData(typeof(int));
            if (to > from) to--;
            if (to >= 0 && to != from && Reordered != null) Reordered(from, to);
            base.OnDragDrop(e);
        }
    }

    // ------------------------------------------------------------------ Kartenvorschau
    public class MapPreview : Ctl
    {
        Image _img; public string Title = "", Sub = "";
        public MapPreview() { }
        public void Set(Image img, string title, string sub) { _img = img; Title = title ?? ""; Sub = sub ?? ""; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            RectangleF r = new RectangleF(0, 0, Width, Height);
            float rad = Dpi.D(12);
            Theme.Fill(g, r, rad, Theme.Card);
            if (_img != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                using (GraphicsPath p = Theme.Round(r, rad))
                {
                    g.SetClip(p);
                    float s = Math.Max(Width / (float)_img.Width, Height / (float)_img.Height);
                    float w = _img.Width * s, h = _img.Height * s;
                    g.DrawImage(_img, (Width - w) / 2, (Height - h) / 2, w, h);
                    Rectangle gr = new Rectangle(0, Height - Dpi.D(90), Width, Dpi.D(90));
                    using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(gr.X, gr.Y - 1, gr.Width, gr.Height + 2), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(215, 8, 10, 12), 90f))
                        g.FillRectangle(lb, gr);
                    g.ResetClip();
                }
            }
            else
            {
                if (Theme.IconBig != null) Theme.Glyph(g, G.Map, Theme.IconBig, new Rectangle(0, 0, Width, Height - Dpi.D(40)), Theme.Mute);
                Theme.Txt(g, string.IsNullOrEmpty(Title) ? I18n.T("Map auswählen", "Select a map") : I18n.T("Kein Kartenbild gefunden", "No map image found"), Theme.Small, new Rectangle(0, Height / 2 + Dpi.D(10), Width, Dpi.D(20)), Theme.Mute, TextFormatFlags.HorizontalCenter);
            }
            if (!string.IsNullOrEmpty(Title))
            {
                Theme.Txt(g, Title, Theme.Title, new Rectangle(Dpi.D(18), Height - Dpi.D(62), Width - Dpi.D(36), Dpi.D(30)), Color.White, 0);
                Theme.Txt(g, Sub, Theme.BodyB, new Rectangle(Dpi.D(18), Height - Dpi.D(34), Width - Dpi.D(36), Dpi.D(20)), Theme.AccentHi, 0);
            }
            Theme.Stroke(g, r, rad, Theme.Border, 1);
        }
    }

    // ------------------------------------------------------------------ Hinweis-Einblendung
    public enum ToastKind { Info, Ok, Warn, Error }

    public class Toast : Ctl
    {
        Timer _hide, _anim; string _msg = ""; ToastKind _kind; int _targetY, _step;
        public Toast()
        {
            Visible = false; Cursor = Cursors.Hand;
            _hide = new Timer(); _hide.Tick += delegate { _hide.Stop(); Visible = false; };
            _anim = new Timer(); _anim.Interval = 15;
            _anim.Tick += delegate { _step++; Top = _targetY + (int)(Dpi.D(18) * Math.Pow(1 - Math.Min(1, _step / 10.0), 2)); if (_step >= 10) _anim.Stop(); };
        }
        public void Show(string msg, ToastKind kind, Rectangle area)
        {
            _msg = msg; _kind = kind;
            Size sz = Theme.Measure(msg, Theme.BodyB, Dpi.D(420));
            Size = new Size(sz.Width + Dpi.D(64), Math.Max(Dpi.D(48), sz.Height + Dpi.D(26)));
            Left = area.Right - Width - Dpi.D(24);
            _targetY = area.Bottom - Height - Dpi.D(24);
            _step = 0; Top = _targetY + Dpi.D(18);
            Visible = true; BringToFront(); Invalidate();
            _anim.Start();
            _hide.Stop(); _hide.Interval = kind == ToastKind.Error ? 9000 : (kind == ToastKind.Warn ? 6000 : 3500); _hide.Start();
        }
        protected override void OnClick(EventArgs e) { Visible = false; base.OnClick(e); }
        protected override void Dispose(bool disposing) { if (disposing) { _hide.Dispose(); _anim.Dispose(); } base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e);
            Color c = _kind == ToastKind.Ok ? Theme.Green : (_kind == ToastKind.Warn ? Theme.Amber : (_kind == ToastKind.Error ? Theme.Red : Theme.Accent));
            RectangleF r = new RectangleF(0, 0, Width, Height);
            Theme.Fill(g, r, Dpi.D(10), Theme.CardHi);
            Theme.Stroke(g, r, Dpi.D(10), Theme.A(c, 160), 1);
            string gl = _kind == ToastKind.Ok ? G.Check : (_kind == ToastKind.Info ? G.Info : G.Warn);
            if (Theme.Icon != null) Theme.Glyph(g, gl, Theme.Icon, new Rectangle(Dpi.D(14), 0, Dpi.D(24), Height), c);
            else using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, Dpi.D(18), Height / 2 - Dpi.D(5), Dpi.D(10), Dpi.D(10));
            TextRenderer.DrawText(g, _msg, Theme.BodyB, new Rectangle(Dpi.D(46), Dpi.D(13), Width - Dpi.D(58), Height - Dpi.D(26)), Theme.Text, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
        }
    }

    // ------------------------------------------------------------------ Dialog (dunkel, rahmenlos)
    public class DarkDialog : Form
    {
        public int Result = -1; TextField _input; string _title, _msg; int _primary; bool _danger;

        DarkDialog(string title, string msg, string[] buttons, int primary, string input, bool danger)
        {
            _title = title; _msg = msg; _primary = primary; _danger = danger;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            BackColor = Theme.Card; Font = Theme.Body; KeyPreview = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            int w = Dpi.D(460), pad = Dpi.D(24);
            // Breit genug für alle Knöpfe (sonst rutscht der erste links aus dem Fenster)
            int bw = 0;
            foreach (string s in buttons) { FlatButton probe = new FlatButton(s, null, BtnKind.Secondary); bw += Math.Max(Dpi.D(96), probe.Width) + Dpi.D(10); probe.Dispose(); }
            w = Math.Max(w, bw - Dpi.D(10) + 2 * pad);
            int mh = Theme.Measure(msg, Theme.Body, w - 2 * pad).Height;
            int y = Dpi.D(62) + mh + Dpi.D(16);
            if (input != null)
            {
                _input = new TextField(null, null); _input.Value = input;
                _input.SetBounds(pad, y, w - 2 * pad, Dpi.D(34)); Controls.Add(_input); y += Dpi.D(50);
            }
            int x = w - pad;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                int idx = i;
                FlatButton b = new FlatButton(buttons[i], null, i == primary ? (danger ? BtnKind.Danger : BtnKind.Primary) : BtnKind.Secondary);
                b.MinimumSize = new Size(Dpi.D(96), 0); if (b.Width < Dpi.D(96)) b.Width = Dpi.D(96);
                x -= b.Width; b.Location = new Point(x, y); x -= Dpi.D(10);
                b.Click += delegate { Result = idx; Close(); };
                Controls.Add(b);
            }
            ClientSize = new Size(w, y + Dpi.D(34) + pad);
        }
        protected override void OnShown(EventArgs e) { base.OnShown(e); if (_input != null) { _input.Box.Focus(); _input.Box.SelectAll(); } }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Result = -1; e.Handled = true; e.SuppressKeyPress = true; Close(); }
            else if (e.KeyCode == Keys.Enter) { Result = _primary; e.Handled = true; e.SuppressKeyPress = true; Close(); }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Theme.Card);
            using (Pen p = new Pen(Theme.BorderHi)) g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            using (SolidBrush b = new SolidBrush(_danger ? Theme.Red : Theme.Accent)) g.FillRectangle(b, 0, 0, ClientSize.Width, Dpi.D(3));
            int pad = Dpi.D(24);
            Theme.Txt(g, _title, Theme.Title, new Rectangle(pad, Dpi.D(18), ClientSize.Width - 2 * pad, Dpi.D(32)), Theme.Text, 0);
            TextRenderer.DrawText(g, _msg, Theme.Body, new Rectangle(pad, Dpi.D(62), ClientSize.Width - 2 * pad, ClientSize.Height), Theme.Dim, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        public static int Ask(IWin32Window owner, string title, string msg, int primary, bool danger, params string[] buttons)
        {
            using (DarkDialog d = new DarkDialog(title, msg, buttons, primary, null, danger)) { d.ShowDialog(owner); return d.Result; }
        }
        public static bool Confirm(IWin32Window owner, string title, string msg, string yes, bool danger)
        {
            return Ask(owner, title, msg, 1, danger, I18n.T("Abbrechen", "Cancel"), yes) == 1;
        }
        public static void Info(IWin32Window owner, string title, string msg)
        {
            Ask(owner, title, msg, 0, false, "OK");
        }
        public static string Input(IWin32Window owner, string title, string msg, string def)
        {
            using (DarkDialog d = new DarkDialog(title, msg, new string[] { I18n.T("Abbrechen", "Cancel"), I18n.T("Speichern", "Save") }, 1, def ?? "", false))
            {
                d.ShowDialog(owner);
                return d.Result == 1 ? d._input.Value : null;
            }
        }
    }
}
