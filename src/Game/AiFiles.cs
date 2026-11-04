// botnames.ai und aiDefault.ai - BF2 Manager. C# 5 (.NET Framework 4.x).
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
    // ------------------------------------------------------------------ botnames.ai und aiDefault.ai
    public static class BotNames
    {
        static readonly Regex Rx = new Regex(@"^\s*aiSettings\.addBotName\s+(.*?)\s*$", RegexOptions.IgnoreCase);

        public static string Find(string gameDir, string mod) { return FindIn(Path.Combine(gameDir, "mods", mod, "ai"), "botnames.ai"); }

        public static string FindIn(string dir, string file)
        {
            try { foreach (string f in Directory.GetFiles(dir)) if (string.Equals(Path.GetFileName(f), file, StringComparison.OrdinalIgnoreCase)) return f; } catch { }
            return null;
        }

        public static List<string> Read(string path, out bool quoted)
        {
            quoted = false;
            List<string> r = new List<string>();
            foreach (string l in Util.ReadLines(path))
            {
                Match m = Rx.Match(l);
                if (!m.Success) continue;
                string v = m.Groups[1].Value;
                if (v.Length >= 2 && v.StartsWith("\"") && v.EndsWith("\"")) { v = v.Substring(1, v.Length - 2); quoted = true; }
                if (v.Length > 0) r.Add(v);
            }
            return r;
        }

        public static void Write(string path, IEnumerable<string> names, bool quoted, string docsDir)
        {
            Util.Backup(new string[] { path }, docsDir);
            List<string> keep = new List<string>();
            foreach (string l in Util.ReadLines(path)) if (!Rx.IsMatch(l)) keep.Add(l);
            while (keep.Count > 0 && keep[keep.Count - 1].Trim().Length == 0) keep.RemoveAt(keep.Count - 1);
            foreach (string n in names) keep.Add("aiSettings.addBotName " + (quoted ? "\"" + n + "\"" : n));
            Util.WriteAtomic(path, string.Join("\r\n", keep.ToArray()) + "\r\n", Util.Ansi);
        }

        // Nur ASCII, keine Anführungszeichen; mehrfache Leerzeichen zusammenfassen
        public static string Clean(string n)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in n.Trim()) if (c >= 32 && c < 127 && c != '"') sb.Append(c);
            return Regex.Replace(sb.ToString(), @"\s+", " ");
        }
    }

    // ai\AIRadioMessages.ai: legt fest, welche Bot-Meldungen als Funkspruch hörbar sind.
    // Aus = die Zeilen werden mit Markierung auskommentiert; Bots handeln weiter wie gewohnt, nur ohne Funk.
    public static class BotRadio
    {
        const string Mark = "rem bf2m-off ";
        static readonly Regex Rx = new Regex(@"^\s*aiComm\.addAIRadioMessage\b", RegexOptions.IgnoreCase);

        public static string Find(string gameDir, string mod) { return BotNames.FindIn(Path.Combine(gameDir, "mods", mod, "ai"), "airadiomessages.ai"); }

        // true = Funk an, false = aus, null = Datei fehlt
        public static bool? State(string path)
        {
            if (path == null || !File.Exists(path)) return null;
            int on = 0, off = 0;
            foreach (string l in Util.ReadLines(path))
            {
                if (Rx.IsMatch(l)) on++;
                else if (l.StartsWith(Mark) && Rx.IsMatch(l.Substring(Mark.Length))) off++;
            }
            if (on == 0 && off == 0) return null;
            return on > 0;
        }

        public static void Set(string path, bool on, string docsDir)
        {
            Util.Backup(new string[] { path }, docsDir);
            List<string> lines = new List<string>(Util.ReadLines(path));
            for (int i = 0; i < lines.Count; i++)
            {
                if (!on && Rx.IsMatch(lines[i])) lines[i] = Mark + lines[i];
                else if (on && lines[i].StartsWith(Mark)) lines[i] = lines[i].Substring(Mark.Length);
            }
            Util.WriteAtomic(path, string.Join("\r\n", lines.ToArray()) + "\r\n", Util.Ansi);
        }
    }

    public class AiFile
    {
        public string Path; public int Override = -1, MaxBots = -1, IncludeHumans = -1; public string Skill;

        public static AiFile Load(string gameDir, string mod)
        {
            AiFile a = new AiFile();
            a.Path = BotNames.FindIn(System.IO.Path.Combine(gameDir, "mods", mod, "ai"), "aidefault.ai");
            if (a.Path == null) return a;
            foreach (string l in Util.ReadLines(a.Path))
            {
                Match m = Regex.Match(l, @"^\s*aiSettings\.(\w+)\s+(\S+)", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                string k = m.Groups[1].Value.ToLowerInvariant(), v = m.Groups[2].Value;
                if (k == "overridemenusettings") a.Override = Util.ToInt(v, -1);
                else if (k == "setmaxnbots") a.MaxBots = Util.ToInt(v, -1);
                else if (k == "maxbotsincludehumans") a.IncludeHumans = Util.ToInt(v, -1);
                else if (k == "setbotskill") a.Skill = v;
            }
            return a;
        }

        // overrideMenuSettings 1 lässt aiDefault.ai Botzahl, Verteilung und Schwierigkeit fest vorgeben –
        // dann wirkt nichts, was der Manager einstellt (z. B. bf2sf64 und bfp2: immer 31 Bots).
        public bool OverridesManager { get { return Path != null && Override == 1; } }

        // Schaltet das Überschreiben aus (mit Sicherung). true = Datei geändert.
        public static bool DisableOverride(string gameDir, string mod, string docsDir)
        {
            AiFile a = Load(gameDir, mod);
            if (!a.OverridesManager) return false;
            a.Override = 0;
            a.Save(docsDir);
            return true;
        }

        public void Save(string docsDir)
        {
            Util.Backup(new string[] { Path }, docsDir);
            List<string> lines = new List<string>(Util.ReadLines(Path));
            Dictionary<string, string> vals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Override >= 0) vals["overrideMenuSettings"] = Override.ToString();
            if (MaxBots >= 0) vals["setMaxNBots"] = MaxBots.ToString();
            if (IncludeHumans >= 0) vals["maxBotsIncludeHumans"] = IncludeHumans.ToString();
            HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lines.Count; i++)
            {
                Match m = Regex.Match(lines[i], @"^(\s*aiSettings\.)(\w+)(\s+)(\S+)(.*)$", RegexOptions.IgnoreCase);
                string v;
                if (m.Success && vals.TryGetValue(m.Groups[2].Value, out v)) { lines[i] = m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value + v + m.Groups[5].Value; done.Add(m.Groups[2].Value); }
            }
            foreach (KeyValuePair<string, string> kv in vals) if (!done.Contains(kv.Key)) lines.Add("aiSettings." + kv.Key + " " + kv.Value);
            Util.WriteAtomic(Path, string.Join("\r\n", lines.ToArray()) + "\r\n", Util.Ansi);
        }
    }
}
