// Mod-Abgleich beim Beitreten, Prüfsummen der Mods und Spaß-Aufsätze - BF2 Manager. C# 5 (.NET Framework 4.x).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace BF2Coop
{
    // Spaß-Aufsatz: kleiner Mod-Ordner (mods/<name>) mit fun.recipe, eigenen Archiven und Ordner-Verknüpfungen
    // (Junctions) auf den Basis-Mod. Übertragen werden nur die eigenen Dateien; die Verknüpfungen legt jeder PC selbst an.
    public static class FunMods
    {
        public const string RecipeFile = "fun.recipe";

        public static Dictionary<string, string> ParseRecipe(string text)
        {
            Dictionary<string, string> r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#") || l.StartsWith(";")) continue;
                int i = l.IndexOf('=');
                if (i > 0) r[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim();
            }
            return r;
        }

        public static bool ValidModName(string m)
        {
            return !string.IsNullOrEmpty(m) && m.IndexOfAny(new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) < 0 && !m.Contains("..") && m.Trim() == m;
        }

        // Basis-Mod aus fun.recipe, sonst null
        public static string BaseFromRecipe(string text)
        {
            string b; return ParseRecipe(text).TryGetValue("Base", out b) && ValidModName(b) ? b : null;
        }

        public static string BaseOf(string gameDir, string mod)
        {
            try
            {
                string f = Path.Combine(Path.Combine(Path.Combine(gameDir, "mods"), mod), RecipeFile);
                return File.Exists(f) ? BaseFromRecipe(File.ReadAllText(f)) : null;
            }
            catch { return null; }
        }

        // Fehlende Ordner des Aufsatzes als Verknüpfung auf den Basis-Mod anlegen; liefert die Zahl neuer Verknüpfungen
        public static int Link(string gameDir, string mod)
        {
            string b = BaseOf(gameDir, mod);
            if (b == null) return 0;
            string mods = Path.Combine(gameDir, "mods"), src = Path.Combine(mods, b), dst = Path.Combine(mods, mod);
            if (!Directory.Exists(src)) throw new Exception(I18n.T("Basis-Mod fehlt: ", "Base mod missing: ") + b);
            int n = 0;
            foreach (string d in Directory.GetDirectories(src))
            {
                string link = Path.Combine(dst, Path.GetFileName(d));
                if (Directory.Exists(link)) continue;
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + d + "\"");
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi)) { p.WaitForExit(15000); }
                if (!Directory.Exists(link)) throw new Exception(I18n.T("Verknüpfung nicht möglich: ", "Could not create link: ") + link);
                n++;
            }
            return n;
        }
    }

    // Prüfsummen der eigenen Mods, im Hintergrund berechnet und kurz gemerkt (die Datei-Hashes selbst sind im Cache von BuildManifest)
    public static class ModPrints
    {
        class Entry { public string Fp; public DateTime Time; public bool Busy; public int Pct; public DateTime Started; }
        static readonly Dictionary<string, Entry> _map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        public static int MaxAgeSeconds = 90;

        // Liefert die bekannte Prüfsumme (evtl. etwas älter) und stößt bei Bedarf eine Neuberechnung an; null = noch unbekannt
        public static string Get(string gameDir, string mod)
        {
            if (string.IsNullOrEmpty(gameDir) || !FunMods.ValidModName(mod)) return null;
            string key = gameDir + "|" + mod; Entry e;
            lock (_map)
            {
                if (!_map.TryGetValue(key, out e)) { e = new Entry(); _map[key] = e; }
                if (e.Busy || (e.Fp != null && (DateTime.Now - e.Time).TotalSeconds < MaxAgeSeconds)) return e.Fp;
                e.Busy = true; e.Pct = 0; e.Started = DateTime.Now;
            }
            Entry entry = e;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string fp = null;
                try { fp = Compute(gameDir, mod, delegate (int p) { entry.Pct = p; }); } catch { }
                lock (_map) { entry.Fp = fp; entry.Time = DateTime.Now; entry.Busy = false; entry.Pct = 100; }
            });
            return e.Fp;
        }

        // Läuft gerade eine Berechnung? pct = Fortschritt, seconds = Laufzeit bisher
        public static bool Busy(string gameDir, string mod, out int pct, out double seconds)
        {
            pct = 0; seconds = 0; Entry e;
            lock (_map)
            {
                if (!_map.TryGetValue(gameDir + "|" + mod, out e) || !e.Busy) return false;
                pct = e.Pct; seconds = (DateTime.Now - e.Started).TotalSeconds; return true;
            }
        }

        public static void Invalidate(string mod)
        {
            lock (_map) foreach (KeyValuePair<string, Entry> kv in _map) if (kv.Key.EndsWith("|" + mod, StringComparison.OrdinalIgnoreCase)) kv.Value.Time = DateTime.MinValue;
        }

        // Ein Aufsatz zählt nur als gleich, wenn auch sein Basis-Mod gleich ist
        public static string Compute(string gameDir, string mod) { return Compute(gameDir, mod, null); }

        public static string Compute(string gameDir, string mod, Action<int> progress)
        {
            string root = Path.Combine(Path.Combine(gameDir, "mods"), mod);
            if (!Directory.Exists(root)) return "";
            string b = FunMods.BaseOf(gameDir, mod);
            // Bei einem Aufsatz ist der Basis-Mod der große Teil: Aufsatz 0–5 %, Basis 5–100 %
            int lo = 0, span = b != null ? 5 : 100;
            Action<int, int> step = delegate (int n, int all) { if (progress != null && all > 0) progress(lo + n * span / all); };
            string fp = Lan.Fingerprint(Lan.BuildManifest(root, step, null));
            if (b != null) { lo = 5; span = 95; fp += "+" + Lan.Fingerprint(Lan.BuildManifest(Path.Combine(Path.Combine(gameDir, "mods"), b), step, null)); }
            return fp;
        }
    }

    // Holt alles, was zum Mitspielen auf dem Server-PC fehlt: den Mod, bei einem Aufsatz zuerst dessen Basis-Mod
    public static class ModSync
    {
        public class Result { public int Files; public long Bytes; public int Links; public List<string> Mods = new List<string>(); }

        public static Result Run(LanHost h, string gameDir, Action<string> status, Func<bool> cancel)
        {
            Result res = new Result();
            string recipe = null;
            try { recipe = Lan.GetText(h, "/file?mod=" + Uri.EscapeDataString(h.Mod) + "&p=" + FunMods.RecipeFile, 15000); }
            catch (WebException ex)
            {
                HttpWebResponse r = ex.Response as HttpWebResponse;
                if (r == null || r.StatusCode != HttpStatusCode.NotFound) throw;
            }
            string b = recipe != null ? FunMods.BaseFromRecipe(recipe) : null;
            if (b != null) res.Mods.Add(b);
            res.Mods.Add(h.Mod);
            for (int i = 0; i < res.Mods.Count; i++)
            {
                string mod = res.Mods[i], root = Path.Combine(Path.Combine(gameDir, "mods"), mod);
                string step = res.Mods.Count > 1 ? "(" + (i + 1) + "/" + res.Mods.Count + ") " : "";
                status(step + I18n.T("Hole Dateiliste von „", "Fetching file list of “") + mod + I18n.T("“ … (beim ersten Mal prüft der Server-PC alle Dateien)", "” … (the first time the server PC checks all files)"));
                List<FileEntry> remote = Lan.ParseManifest(Lan.GetText(h, "/manifest?mod=" + Uri.EscapeDataString(mod), 900000));
                if (remote.Count == 0) throw new Exception(I18n.T("Der Server-PC liefert keine Dateien für „", "The server PC returned no files for “") + mod + I18n.T("“.", "”."));
                int last = -1;
                List<FileEntry> local = Lan.BuildManifest(root, delegate (int n, int all)
                {
                    int pct = all > 0 ? n * 100 / all : 0;
                    if (pct / 5 != last / 5) { last = pct; status(step + I18n.T("Prüfe eigene Dateien von „", "Checking own files of “") + mod + I18n.T("“ … ", "” … ") + pct + " %"); }
                }, cancel);
                SyncPlan plan = Lan.Diff(local, remote);
                List<FileEntry> todo = new List<FileEntry>(plan.Missing); todo.AddRange(plan.Different);
                long total = plan.Bytes, done = 0; int lastPct = -1;
                foreach (FileEntry e in todo)
                {
                    if (cancel()) throw new OperationCanceledException();
                    Lan.Fetch(h, mod, e, root, delegate (long n)
                    {
                        done += n;
                        int pct = total > 0 ? (int)(done * 100 / total) : 100;
                        if (pct != lastPct) { lastPct = pct; status(step + I18n.T("Übertrage „", "Transferring “") + mod + I18n.T("“ … ", "” … ") + pct + " %  (" + SizeText(done) + " / " + SizeText(total) + ")"); }
                    }, cancel);
                    res.Files++;
                }
                res.Bytes += total;
                ModPrints.Invalidate(mod);
            }
            if (b != null) { res.Links = FunMods.Link(gameDir, h.Mod); ModPrints.Invalidate(h.Mod); }
            return res;
        }

        public static string SizeText(long b)
        {
            if (b >= 1L << 30) return (b / (double)(1L << 30)).ToString("0.0") + " GB";
            if (b >= 1L << 20) return (b / (double)(1L << 20)).ToString("0.0") + " MB";
            return Math.Max(1, b / 1024) + " KB";
        }
    }
}
