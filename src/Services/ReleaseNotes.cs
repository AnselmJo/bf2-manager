// Release-Notes (Markdown aus GitHub-Releases) in einfache, formatierbare Zeilen zerlegen - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.Text;

namespace BF2Coop
{
    public enum NoteKind { Heading, Bullet, Para, Rule, Blank }

    public class NoteRun
    {
        public string Text; public bool Bold, Italic, Code;
    }

    public class NoteLine
    {
        public NoteKind Kind; public int Level; // Level: Überschrift 1-6, Aufzählung 0 = oberste Ebene
        public List<NoteRun> Runs = new List<NoteRun>();
        public string PlainText { get { StringBuilder sb = new StringBuilder(); foreach (NoteRun r in Runs) sb.Append(r.Text); return sb.ToString(); } }
    }

    // Kleiner Markdown-Leser für die Release-Texte: Überschriften (#), Aufzählungen (- / *), Trennlinie (---),
    // **fett**, *kursiv*, `Code` und [Text](Link) (nur der Text). Mehr nutzt CHANGELOG.md nicht.
    public static class ReleaseNotes
    {
        // Der Release-Workflow stellt einen Download-Hinweis für die GitHub-Seite voran ("Download **BF2Manager.exe** under *Assets* …", dann "---").
        // Im Manager ist der Hinweis sinnlos und wird entfernt.
        public static string StripDownloadHint(string md)
        {
            if (string.IsNullOrEmpty(md)) return "";
            string t = md.Replace("\r\n", "\n");
            int rule = -1, pos = 0, lineNo = 0;
            while (pos <= t.Length && lineNo < 8)
            {
                int nl = t.IndexOf('\n', pos); if (nl < 0) nl = t.Length;
                if (t.Substring(pos, nl - pos).Trim() == "---") { rule = pos; break; }
                pos = nl + 1; lineNo++;
            }
            if (rule < 0) return t;
            string head = t.Substring(0, rule);
            if (head.IndexOf("BF2Manager.exe", StringComparison.OrdinalIgnoreCase) < 0 || head.IndexOf("Assets", StringComparison.OrdinalIgnoreCase) < 0) return t;
            int after = t.IndexOf('\n', rule);
            return after < 0 ? "" : t.Substring(after + 1).TrimStart('\n');
        }

        public static List<NoteLine> Parse(string md)
        {
            List<NoteLine> lines = new List<NoteLine>();
            foreach (string raw in StripDownloadHint(md).Split('\n'))
            {
                string l = raw.TrimEnd();
                NoteLine n = new NoteLine();
                string trimmed = l.TrimStart();
                int indent = l.Length - trimmed.Length;
                if (trimmed.Length == 0)
                {
                    if (lines.Count > 0 && lines[lines.Count - 1].Kind != NoteKind.Blank) { n.Kind = NoteKind.Blank; lines.Add(n); }
                    continue;
                }
                if (trimmed == "---" || trimmed == "***" || trimmed == "___") { n.Kind = NoteKind.Rule; lines.Add(n); continue; }
                int h = 0; while (h < trimmed.Length && trimmed[h] == '#') h++;
                if (h > 0 && h <= 6 && h < trimmed.Length && trimmed[h] == ' ')
                {
                    n.Kind = NoteKind.Heading; n.Level = h; n.Runs = Inline(trimmed.Substring(h + 1).Trim());
                }
                else if (trimmed.Length > 1 && (trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+') && trimmed[1] == ' ')
                {
                    n.Kind = NoteKind.Bullet; n.Level = indent / 2; n.Runs = Inline(trimmed.Substring(2).Trim());
                }
                else { n.Kind = NoteKind.Para; n.Runs = Inline(trimmed); }
                lines.Add(n);
            }
            while (lines.Count > 0 && lines[lines.Count - 1].Kind == NoteKind.Blank) lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        // Zeilen als schlichter Text (z. B. für Tooltips oder die Zwischenablage)
        public static string ToPlain(string md)
        {
            StringBuilder sb = new StringBuilder();
            foreach (NoteLine n in Parse(md))
            {
                if (n.Kind == NoteKind.Blank) sb.Append('\n');
                else if (n.Kind == NoteKind.Rule) sb.Append("\n");
                else if (n.Kind == NoteKind.Bullet) sb.Append(new string(' ', 2 * n.Level)).Append("• ").Append(n.PlainText).Append('\n');
                else sb.Append(n.PlainText).Append('\n');
            }
            return sb.ToString().Trim();
        }

        public static List<NoteRun> Inline(string s)
        {
            List<NoteRun> runs = new List<NoteRun>();
            StringBuilder cur = new StringBuilder();
            bool bold = false, italic = false, code = false;
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '`') { Flush(runs, cur, bold, italic, code); code = !code; i++; continue; }
                if (!code && c == '*' && i + 1 < s.Length && s[i + 1] == '*') { Flush(runs, cur, bold, italic, code); bold = !bold; i += 2; continue; }
                if (!code && c == '*' && OpensOrCloses(s, i, italic)) { Flush(runs, cur, bold, italic, code); italic = !italic; i++; continue; }
                if (!code && c == '[')
                {
                    int close = s.IndexOf("](", i + 1, StringComparison.Ordinal);
                    int end = close > 0 ? s.IndexOf(')', close + 2) : -1;
                    if (close > 0 && end > 0) { cur.Append(s.Substring(i + 1, close - i - 1)); i = end + 1; continue; }
                }
                cur.Append(c); i++;
            }
            Flush(runs, cur, bold, italic, code);
            return runs;
        }

        // Ein einzelnes * ist nur Markierung, wenn es an ein Wort grenzt (nicht "3 * 4").
        static bool OpensOrCloses(string s, int i, bool open)
        {
            if (open) return i > 0 && s[i - 1] != ' ';
            return i + 1 < s.Length && s[i + 1] != ' ' && s.IndexOf('*', i + 1) > i;
        }

        static void Flush(List<NoteRun> runs, StringBuilder cur, bool bold, bool italic, bool code)
        {
            if (cur.Length == 0) return;
            NoteRun r = new NoteRun(); r.Text = cur.ToString(); r.Bold = bold; r.Italic = italic; r.Code = code;
            runs.Add(r); cur.Length = 0;
        }
    }
}
