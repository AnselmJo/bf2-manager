// NotesDialog.cs - formatierte Release-Notes (Überschriften, Aufzählungen, fett/kursiv/Code), scrollbar. C# 5.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BF2Coop
{
    public class NotesDialog : Form
    {
        readonly string _title;
        readonly RichTextBox _rt;

        NotesDialog(string title, string markdown, string footer)
        {
            _title = title;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            BackColor = Theme.Card; Font = Theme.Body; KeyPreview = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            int w = Dpi.D(640), pad = Dpi.D(24), top = Dpi.D(62);
            int h = Math.Min(Dpi.D(620), Math.Max(Dpi.D(320), Screen.FromPoint(Cursor.Position).WorkingArea.Height * 3 / 4));
            ClientSize = new Size(w, h);

            _rt = new RichTextBox();
            _rt.BorderStyle = BorderStyle.None; _rt.ReadOnly = true; _rt.DetectUrls = false; _rt.WordWrap = true;
            _rt.ScrollBars = RichTextBoxScrollBars.Vertical; _rt.BackColor = Theme.Card; _rt.ForeColor = Theme.Text; _rt.Font = Theme.Body;
            _rt.TabStop = false;
            _rt.SetBounds(pad, top, w - 2 * pad + Dpi.D(8), h - top - Dpi.D(78));
            _rt.HandleCreated += delegate { Native.DarkScroll(_rt); };
            Controls.Add(_rt);
            Render(markdown, footer);

            FlatButton ok = new FlatButton("OK", null, BtnKind.Primary);
            ok.MinimumSize = new Size(Dpi.D(96), 0); if (ok.Width < Dpi.D(96)) ok.Width = Dpi.D(96);
            ok.Location = new Point(w - pad - ok.Width, h - pad - Dpi.D(34));
            ok.Click += delegate { Close(); };
            Controls.Add(ok);
        }

        void Render(string markdown, string footer)
        {
            Font head = new Font(Theme.Head.FontFamily, Theme.Head.Size, FontStyle.Bold);
            Font bold = Theme.BodyB, italic = new Font(Theme.Body, FontStyle.Italic), boldItalic = new Font(Theme.Body, FontStyle.Bold | FontStyle.Italic);
            List<NoteLine> lines = ReleaseNotes.Parse(markdown);
            if (lines.Count == 0) Add(I18n.T("Keine Angaben zu den Änderungen.", "No change notes available."), Theme.Body, Theme.Dim);
            bool first = true;
            foreach (NoteLine n in lines)
            {
                _rt.SelectionIndent = 0; _rt.SelectionHangingIndent = 0;
                switch (n.Kind)
                {
                    case NoteKind.Blank: Add("\n", Theme.Body, Theme.Text); break;
                    case NoteKind.Rule: Add("\n", Theme.Small, Theme.Dim); break;
                    case NoteKind.Heading:
                        if (!first) Add("\n", Theme.Small, Theme.Text);
                        _rt.SelectionColor = Theme.Accent;
                        foreach (NoteRun r in n.Runs) Add(r.Text, n.Level <= 2 ? Theme.Title : head, Theme.Accent);
                        Add("\n", Theme.Body, Theme.Text);
                        break;
                    case NoteKind.Bullet:
                        _rt.SelectionIndent = Dpi.D(8 + 16 * n.Level); _rt.SelectionHangingIndent = Dpi.D(14);
                        Add("•  ", bold, Theme.Accent);
                        Runs(n.Runs, bold, italic, boldItalic);
                        Add("\n", Theme.Body, Theme.Text);
                        break;
                    default:
                        Runs(n.Runs, bold, italic, boldItalic);
                        Add("\n", Theme.Body, Theme.Text);
                        break;
                }
                first = false;
            }
            if (!string.IsNullOrEmpty(footer)) { _rt.SelectionIndent = 0; _rt.SelectionHangingIndent = 0; Add("\n" + footer, Theme.Body, Theme.Dim); }
            _rt.SelectionStart = 0; _rt.SelectionLength = 0;
        }

        void Runs(List<NoteRun> runs, Font bold, Font italic, Font boldItalic)
        {
            foreach (NoteRun r in runs)
            {
                if (r.Code) { Add(r.Text, Theme.Mono, Theme.Text); continue; }
                Font f = r.Bold && r.Italic ? boldItalic : (r.Bold ? bold : (r.Italic ? italic : Theme.Body));
                Add(r.Text, f, r.Bold ? Theme.Text : Theme.Dim);
            }
        }

        void Add(string text, Font f, Color c)
        {
            _rt.SelectionStart = _rt.TextLength; _rt.SelectionLength = 0;
            _rt.SelectionFont = f; _rt.SelectionColor = c;
            _rt.SelectedText = text;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; Close(); }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Theme.Card);
            using (Pen p = new Pen(Theme.BorderHi)) g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, 0, 0, ClientSize.Width, Dpi.D(3));
            int pad = Dpi.D(24);
            Theme.Txt(g, _title, Theme.Title, new Rectangle(pad, Dpi.D(18), ClientSize.Width - 2 * pad, Dpi.D(32)), Theme.Text, 0);
        }

        public static void Show(IWin32Window owner, string title, string markdown, string footer)
        {
            using (NotesDialog d = new NotesDialog(title, markdown, footer)) d.ShowDialog(owner);
        }
    }
}
