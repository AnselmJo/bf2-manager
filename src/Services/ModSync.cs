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
            List<string[]> todo = new List<string[]>();
            Collect(src, dst, 0, todo);
            MakeJunctions(todo);
            return todo.Count;
        }

        // Fehlende Unterordner verknüpfen; echte Ordner des Aufsatzes (z. B. levels mit einer angepassten Map) eine Ebene tiefer auffüllen
        static void Collect(string src, string dst, int depth, List<string[]> todo)
        {
            foreach (string d in Directory.GetDirectories(src))
            {
                string link = Path.Combine(dst, Path.GetFileName(d));
                if (!Directory.Exists(link)) todo.Add(new string[] { link, d });
                else if (depth < 2 && (File.GetAttributes(link) & FileAttributes.ReparsePoint) == 0) Collect(d, link, depth + 1, todo);
            }
        }

        // Viele Verknüpfungen mit einem einzigen cmd-Aufruf anlegen (eine Map-Sammlung hat leicht 100 Ordner)
        static void MakeJunctions(List<string[]> todo)
        {
            if (todo.Count == 0) return;
            string bat = Path.Combine(Path.GetTempPath(), "bf2sm_links_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".cmd");
            StringBuilder sb = new StringBuilder("@echo off\r\nchcp 65001 >nul\r\n");
            foreach (string[] t in todo) sb.Append("mklink /J \"").Append(t[0]).Append("\" \"").Append(t[1]).Append("\" >nul\r\n");
            File.WriteAllText(bat, sb.ToString(), new UTF8Encoding(false));
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi)) { p.WaitForExit(60000); }
            }
            finally { try { File.Delete(bat); } catch { } }
            foreach (string[] t in todo) if (!Directory.Exists(t[0])) throw new Exception(I18n.T("Verknüpfung nicht möglich: ", "Could not create link: ") + t[0]);
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
            lock (_map)
                foreach (KeyValuePair<string, Entry> kv in _map)
                    if (kv.Key.EndsWith("|" + mod, StringComparison.OrdinalIgnoreCase))
                    {
                        kv.Value.Time = DateTime.MinValue;
                        ManifestStore.Invalidate(Path.Combine(Path.Combine(kv.Key.Substring(0, kv.Key.Length - mod.Length - 1), "mods"), mod));
                    }
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
            Action<int> step = delegate (int p) { if (progress != null) progress(lo + p * span / 100); };
            string fp = Lan.Fingerprint(ManifestStore.Get(root, step));
            if (b != null) { lo = 5; span = 95; fp += "+" + Lan.Fingerprint(ManifestStore.Get(Path.Combine(Path.Combine(gameDir, "mods"), b), step)); }
            return fp;
        }
    }

    // Dateilisten (Manifeste) der eigenen Mods: je Ordner nur eine Berechnung gleichzeitig, kurz gemerkt.
    // Datei-Dienst und Prüfsumme teilen sie sich – die große erste Prüfung läuft so nur einmal.
    public static class ManifestStore
    {
        class Entry { public List<FileEntry> List; public DateTime Time; public bool Busy; public int Pct; public string Error; public readonly object Gate = new object(); }
        static readonly Dictionary<string, Entry> _map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        public static int MaxAgeSeconds = 120;

        static Entry For(string root)
        {
            string key = Path.GetFullPath(root).TrimEnd('\\');
            lock (_map) { Entry e; if (!_map.TryGetValue(key, out e)) { e = new Entry(); _map[key] = e; } return e; }
        }

        // Sofort: aktuelle Liste oder null (dann läuft die Berechnung im Hintergrund; pct = Fortschritt)
        public static List<FileEntry> TryGet(string root, out int pct)
        {
            Entry e = For(root);
            lock (e.Gate)
            {
                pct = e.Pct;
                if (e.List != null && (DateTime.Now - e.Time).TotalSeconds < MaxAgeSeconds) return e.List;
                if (e.Busy) return null;
                e.Busy = true; e.Pct = 0; e.Error = null;
            }
            ThreadPool.QueueUserWorkItem(delegate { Build(root, e, null); });
            return null;
        }

        // Wartet auf die Liste (rechnet selbst oder wartet auf eine laufende Berechnung)
        public static List<FileEntry> Get(string root, Action<int> progress)
        {
            Entry e = For(root);
            while (true)
            {
                lock (e.Gate)
                {
                    if (e.List != null && (DateTime.Now - e.Time).TotalSeconds < MaxAgeSeconds) { if (progress != null) progress(100); return e.List; }
                    if (!e.Busy) { e.Busy = true; e.Pct = 0; e.Error = null; break; }
                }
                if (progress != null) progress(e.Pct);
                Thread.Sleep(250);
            }
            Build(root, e, progress);
            lock (e.Gate) { if (e.List == null) throw new Exception(e.Error ?? "manifest"); return e.List; }
        }

        static void Build(string root, Entry e, Action<int> progress)
        {
            List<FileEntry> list = null; string err = null;
            try { list = Lan.BuildManifest(root, delegate (int n, int all) { int p = all > 0 ? n * 100 / all : 100; e.Pct = p; if (progress != null) progress(p); }, null); }
            catch (Exception ex) { err = ex.Message; }
            lock (e.Gate) { e.List = list; e.Time = DateTime.Now; e.Busy = false; e.Pct = 100; e.Error = err; }
        }

        public static void Invalidate(string root) { Entry e = For(root); lock (e.Gate) e.Time = DateTime.MinValue; }
    }

    // Holt alles, was zum Mitspielen auf dem Server-PC fehlt: den Mod, bei einem Aufsatz zuerst dessen Basis-Mod
    public static class ModSync
    {
        public class Result { public int Files; public long Bytes; public int Links; public List<string> Mods = new List<string>(); }

        public static Result Run(LanHost h, string gameDir, Action<string> status, Func<bool> cancel)
        {
            Result res = new Result();
            string recipe = null;
            try { recipe = Retry(delegate { return Lan.GetText(h, "/file?mod=" + Uri.EscapeDataString(h.Mod) + "&p=" + FunMods.RecipeFile, 15000); }, status, cancel); }
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
                status(step + I18n.T("Hole Dateiliste von „", "Fetching file list of “") + mod + I18n.T("“ …", "” …"));
                List<FileEntry> remote = Lan.ParseManifest(GetManifest(h, mod, step, status, cancel));
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
                DateTime t0 = DateTime.Now;
                foreach (FileEntry e in todo)
                {
                    if (cancel()) throw new OperationCanceledException();
                    // Pro Datei bis zu drei Versuche; ein abgebrochener Versuch zählt nicht zum Fortschritt
                    for (int attempt = 1; ; attempt++)
                    {
                        long mine = 0;
                        try
                        {
                            Lan.Fetch(h, mod, e, root, delegate (long n)
                            {
                                mine += n; done += n;
                                int pct = total > 0 ? (int)(done * 100 / total) : 100;
                                if (pct != lastPct) { lastPct = pct; status(step + I18n.T("Übertrage „", "Transferring “") + mod + I18n.T("“ … ", "” … ") + pct + " %  (" + SizeText(done) + " / " + SizeText(total) + Rate(done, t0) + ")"); }
                            }, cancel);
                            break;
                        }
                        catch (Exception ex)
                        {
                            done -= mine;
                            if (ex is OperationCanceledException || attempt >= 3 || !Transient(ex)) throw;
                            status(step + I18n.T("Verbindung unterbrochen – neuer Versuch (", "Connection interrupted – retrying (") + (attempt + 1) + "/3) …");
                            Wait(3000, cancel);
                        }
                    }
                    res.Files++;
                }
                res.Bytes += total;
                ModPrints.Invalidate(mod); ManifestStore.Invalidate(root);
            }
            if (b != null) { res.Links = FunMods.Link(gameDir, h.Mod); ModPrints.Invalidate(h.Mod); ManifestStore.Invalidate(Path.Combine(Path.Combine(gameDir, "mods"), h.Mod)); }
            return res;
        }

        // Dateiliste holen. Neuere Server-PCs antworten sofort mit 503 „busy|Prozent“, solange sie ihre Dateien
        // das erste Mal prüfen – dann zeigen wir den Fortschritt und fragen alle 2 s wieder, statt in einen Timeout zu laufen.
        public static string GetManifest(LanHost h, string mod, string step, Action<string> status, Func<bool> cancel)
        {
            DateTime moved = DateTime.Now; int lastPct = -1, fails = 0;
            while (true)
            {
                if (cancel()) throw new OperationCanceledException();
                try { return Lan.GetText(h, "/manifest?mod=" + Uri.EscapeDataString(mod), 900000); }
                catch (WebException ex)
                {
                    int pct;
                    if (IsBusy(ex, out pct))
                    {
                        if (pct != lastPct) { lastPct = pct; moved = DateTime.Now; }
                        if ((DateTime.Now - moved).TotalMinutes > 10) throw new Exception(I18n.T("Der Server-PC kommt beim Prüfen seiner Dateien nicht voran.", "The server PC makes no progress checking its files."));
                        status(step + I18n.T("Der Server-PC prüft einmalig alle Dateien von „", "The server PC is checking all files of “") + mod + I18n.T("“: ", "” once: ") + pct + I18n.T(" % – bitte warten, es geht gleich von selbst weiter …", " % – please wait, it continues by itself …"));
                        Wait(2000, cancel);
                        continue;
                    }
                    if (++fails >= 3 || !Transient(ex)) throw;
                    status(step + I18n.T("Keine Antwort vom Server-PC – neuer Versuch (", "No answer from the server PC – retrying (") + (fails + 1) + "/3) …");
                    Wait(3000, cancel);
                }
            }
        }

        public static bool IsBusy(WebException ex, out int pct)
        {
            pct = 0;
            HttpWebResponse r = ex.Response as HttpWebResponse;
            if (r == null || (int)r.StatusCode != 503) return false;
            try { using (StreamReader sr = new StreamReader(r.GetResponseStream())) { string s = sr.ReadToEnd(); int i = s.IndexOf('|'); if (i >= 0) int.TryParse(s.Substring(i + 1).Trim(), out pct); } } catch { }
            return true;
        }

        // Vorübergehende Netzfehler (Timeout, Verbindung abgebrochen) – ein neuer Versuch lohnt sich
        public static bool Transient(Exception ex)
        {
            WebException we = ex as WebException;
            if (we != null)
                return we.Status == WebExceptionStatus.Timeout || we.Status == WebExceptionStatus.ConnectFailure || we.Status == WebExceptionStatus.ConnectionClosed
                    || we.Status == WebExceptionStatus.ReceiveFailure || we.Status == WebExceptionStatus.SendFailure || we.Status == WebExceptionStatus.KeepAliveFailure
                    || we.Status == WebExceptionStatus.PipelineFailure || we.Status == WebExceptionStatus.RequestCanceled;
            return ex is IOException;
        }

        static T Retry<T>(Func<T> f, Action<string> status, Func<bool> cancel)
        {
            for (int attempt = 1; ; attempt++)
            {
                try { return f(); }
                catch (Exception ex)
                {
                    if (attempt >= 3 || !Transient(ex)) throw;
                    status(I18n.T("Keine Antwort vom Server-PC – neuer Versuch (", "No answer from the server PC – retrying (") + (attempt + 1) + "/3) …");
                    Wait(3000, cancel);
                }
            }
        }

        static void Wait(int ms, Func<bool> cancel)
        {
            for (int i = 0; i < ms; i += 200) { if (cancel()) throw new OperationCanceledException(); Thread.Sleep(200); }
        }

        static string Rate(long done, DateTime t0)
        {
            double s = (DateTime.Now - t0).TotalSeconds;
            return s >= 2 && done > 0 ? ", " + SizeText((long)(done / s)) + "/s" : "";
        }

        public static string SizeText(long b)
        {
            if (b >= 1L << 30) return (b / (double)(1L << 30)).ToString("0.0") + " GB";
            if (b >= 1L << 20) return (b / (double)(1L << 20)).ToString("0.0") + " MB";
            return Math.Max(1, b / 1024) + " KB";
        }
    }
}
