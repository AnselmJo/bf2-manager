// Programm-Patches und DirectX - BF2 Manager. C# 5 (.NET Framework 4.x).
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
    public enum PatchState { Unknown, Off, On, Missing }

    // Server: python\bf2\stats\unlocks.py setzt beim Verbinden alle Unlocks auf 0; im LAN kommt keine Online-Antwort.
    public static class ServerUnlock
    {
        static readonly Encoding Lat1 = Encoding.GetEncoding(28591);
        static readonly Regex RxList = new Regex(@"^(\s*)defaultUnlocks\s*=\s*\[([0-9,\s]*)\](.*)$", RegexOptions.Multiline);
        static readonly Regex RxFlag = new Regex(@"^(\s*LAN_ALL_UNLOCKS\s*=\s*)(\d)", RegexOptions.Multiline);
        public static string PathFor(string gameDir) { return Path.Combine(gameDir, "python", "bf2", "stats", "unlocks.py"); }

        public static PatchState State(string gameDir)
        {
            string p = PathFor(gameDir);
            if (!File.Exists(p)) return PatchState.Missing;
            string t = File.ReadAllText(p, Lat1);
            Match f = RxFlag.Match(t);
            if (f.Success) return f.Groups[2].Value == "1" ? PatchState.On : PatchState.Off;
            Match m = RxList.Match(t);
            if (!m.Success) return PatchState.Unknown;
            return m.Groups[2].Value.Contains("1") ? PatchState.On : PatchState.Off;
        }

        public static void Set(string gameDir, string docsDir, bool on)
        {
            string p = PathFor(gameDir);
            string t = File.ReadAllText(p, Lat1);
            Util.Backup(new string[] { p }, docsDir);
            if (RxFlag.IsMatch(t)) t = RxFlag.Replace(t, "${1}" + (on ? "1" : "0"), 1);
            else
            {
                Match m = RxList.Match(t);
                if (!m.Success) throw new InvalidOperationException(I18n.T("Unbekannter Aufbau von unlocks.py - nichts geändert.", "Unknown layout of unlocks.py - nothing changed."));
                int n = m.Groups[2].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (n == 0) n = 7;
                string[] v = new string[n];
                for (int i = 0; i < n; i++) v[i] = on ? "1" : "0";
                string line = m.Groups[1].Value + "defaultUnlocks = [" + string.Join(", ", v) + "]" + (on ? "  # BF2 Manager: LAN unlocks" : "");
                t = t.Substring(0, m.Index) + line + t.Substring(m.Index + m.Length);
            }
            File.WriteAllText(p, t, Lat1);
        }
    }

    // Client: zwei Bytes in BF2.exe (Prüfung, ob ein Kit-Gegenstand freigeschaltet ist)
    // Client: zwei Bytes in BF2.exe (Prüfung, ob ein Kit-Gegenstand freigeschaltet ist)
    public static class ClientUnlock
    {
        static readonly byte[] Orig = { 0x8B, 0x75, 0x10, 0x39, 0x70, 0x10, 0x7C, 0x14, 0x8B, 0xCB, 0xE8 };
        static readonly byte[] Done = { 0x8B, 0x75, 0x10, 0x39, 0x70, 0x10, 0x90, 0x90, 0x8B, 0xCB, 0xE8 };

        public static PatchState State(string exe)
        {
            try
            {
                if (!File.Exists(exe)) return PatchState.Missing;
                byte[] d = File.ReadAllBytes(exe);
                if (Pe.Find(d, Done, 0) >= 0) return PatchState.On;
                return Pe.Find(d, Orig, 0) >= 0 ? PatchState.Off : PatchState.Unknown;
            }
            catch { return PatchState.Unknown; }
        }

        // Ein- und Ausschalten ändert nur die zwei Bytes, damit andere Patches (4 GB) erhalten bleiben.
        public static string Apply(string exe, bool on)
        {
            if (Pe.InUse(exe)) throw new InvalidOperationException(I18n.T("Battlefield 2 läuft noch. Bitte zuerst beenden.", "Battlefield 2 is still running. Please close it first."));
            byte[] d = File.ReadAllBytes(exe);
            byte[] from = on ? Orig : Done, to = on ? Done : Orig;
            if (Pe.Find(d, to, 0) >= 0) return on ? I18n.T("Bereits freigeschaltet.", "Already unlocked.") : I18n.T("Bereits im Originalzustand.", "Already original.");
            int i = Pe.Find(d, from, 0);
            if (i < 0) throw new InvalidOperationException(I18n.T("Die erwartete Code-Stelle wurde nicht gefunden (andere BF2-Version) - nichts geändert.", "The expected code location was not found (different BF2 version) - nothing changed."));
            if (Pe.Find(d, from, i + 1) >= 0) throw new InvalidOperationException(I18n.T("Code-Stelle nicht eindeutig - nichts geändert.", "Code location is not unique - nothing changed."));
            Pe.BackupOnce(exe, ".vor_unlock.bak");
            d[i + 6] = to[6]; d[i + 7] = to[7];
            File.WriteAllBytes(exe, d);
            return on ? I18n.T("Zweitwaffen freigeschaltet.", "Unlock weapons enabled.") : I18n.T("Zweitwaffen-Patch entfernt.", "Unlock patch removed.");
        }
    }

    // ------------------------------------------------------------------ PE-Werkzeuge: 4-GB-Flag, Importe
    public static class Pe
    {
        public static int Find(byte[] d, byte[] pat, int start)
        {
            for (int i = start; i <= d.Length - pat.Length; i++)
            {
                int k = 0;
                while (k < pat.Length && d[i + k] == pat[k]) k++;
                if (k == pat.Length) return i;
            }
            return -1;
        }

        // Läuft genau diese exe? Windows sperrt eine laufende exe gegen Schreiben. Prüft die Datei selbst statt aller
        // BF2-Prozesse, damit ein Server aus einem anderen Ordner (oder eine Testkopie) nicht fälschlich blockiert.
        public static bool InUse(string exe)
        {
            try { using (new FileStream(exe, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } return false; }
            catch (FileNotFoundException) { return false; }
            catch (UnauthorizedAccessException) { return false; } // keine Schreibrechte: das Schreiben danach meldet es verständlich
            catch (IOException) { return true; }
        }

        public static void BackupOnce(string file, string suffix)
        {
            string bak = file + suffix;
            if (!File.Exists(bak)) File.Copy(file, bak);
        }

        static int Header(byte[] d)
        {
            if (d.Length < 0x40 || d[0] != 'M' || d[1] != 'Z') return -1;
            int pe = BitConverter.ToInt32(d, 0x3C);
            if (pe <= 0 || pe + 24 > d.Length || d[pe] != 'P' || d[pe + 1] != 'E' || d[pe + 2] != 0 || d[pe + 3] != 0) return -1;
            return pe;
        }

        // 4-GB-Patch: IMAGE_FILE_LARGE_ADDRESS_AWARE (0x0020) im COFF-Header
        public static PatchState LaaState(string exe)
        {
            try
            {
                if (!File.Exists(exe)) return PatchState.Missing;
                byte[] d = ReadHead(exe, 4096);
                int pe = Header(d);
                if (pe < 0) return PatchState.Unknown;
                return (BitConverter.ToUInt16(d, pe + 22) & 0x20) != 0 ? PatchState.On : PatchState.Off;
            }
            catch { return PatchState.Unknown; }
        }

        public static string SetLaa(string exe, bool on)
        {
            if (InUse(exe)) throw new InvalidOperationException(I18n.T("Spiel oder Server läuft noch. Bitte zuerst beenden.", "Game or server is still running. Please close it first."));
            byte[] d = File.ReadAllBytes(exe);
            int pe = Header(d);
            if (pe < 0) throw new InvalidOperationException(I18n.T("Keine gültige exe-Datei.", "Not a valid exe file."));
            ushort ch = BitConverter.ToUInt16(d, pe + 22);
            ushort nw = (ushort)(on ? ch | 0x20 : ch & ~0x20);
            if (nw == ch) return Path.GetFileName(exe) + ": " + (on ? I18n.T("4 GB bereits aktiv", "4 GB already active") : I18n.T("bereits original", "already original"));
            BackupOnce(exe, ".vor_4gb.bak");
            byte[] b = BitConverter.GetBytes(nw); d[pe + 22] = b[0]; d[pe + 23] = b[1];
            File.WriteAllBytes(exe, d);
            return Path.GetFileName(exe) + ": " + (on ? I18n.T("4 GB aktiviert", "4 GB enabled") : I18n.T("4 GB entfernt", "4 GB removed"));
        }

        static byte[] ReadHead(string file, int max)
        {
            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] b = new byte[Math.Min(max, (int)fs.Length)];
                int n = 0; while (n < b.Length) { int r = fs.Read(b, n, b.Length - n); if (r <= 0) break; n += r; }
                return b;
            }
        }

        // Namen der importierten DLLs (für die DirectX-Prüfung); null, wenn nicht lesbar
        public static List<string> Imports(string file)
        {
            try
            {
                byte[] d = File.ReadAllBytes(file);
                int pe = Header(d);
                if (pe < 0) return null;
                int nSec = BitConverter.ToUInt16(d, pe + 6), optSize = BitConverter.ToUInt16(d, pe + 20), opt = pe + 24;
                int magic = BitConverter.ToUInt16(d, opt);
                int dirOff = opt + (magic == 0x20B ? 112 : 96);
                int impRva = BitConverter.ToInt32(d, dirOff + 8);
                if (impRva == 0) return new List<string>();
                int sec = opt + optSize;
                Func<int, int> toOff = delegate (int rva)
                {
                    for (int i = 0; i < nSec; i++)
                    {
                        int s = sec + i * 40, va = BitConverter.ToInt32(d, s + 12), vs = Math.Max(BitConverter.ToInt32(d, s + 8), BitConverter.ToInt32(d, s + 16)), raw = BitConverter.ToInt32(d, s + 20);
                        if (rva >= va && rva < va + vs) return rva - va + raw;
                    }
                    return -1;
                };
                List<string> r = new List<string>();
                int p = toOff(impRva);
                for (int n = 0; p >= 0 && p + 20 <= d.Length && n < 512; n++, p += 20)
                {
                    int nameRva = BitConverter.ToInt32(d, p + 12);
                    if (nameRva == 0 && BitConverter.ToInt32(d, p) == 0) break;
                    int o = toOff(nameRva);
                    if (o < 0) continue;
                    int e = o; while (e < d.Length && d[e] != 0) e++;
                    r.Add(Encoding.ASCII.GetString(d, o, e - o));
                }
                return r;
            }
            catch { return null; }
        }
    }

    // ------------------------------------------------------------------ DirectX 9 (d3dx9_xx.dll)
    public static class DirectX
    {
        // Welche d3dx9-Dateien braucht das Spiel? (BF2.exe und die DLLs im Spielordner, z. B. RendDX9.dll)
        public static List<string> Needed(string gameDir)
        {
            List<string> r = new List<string>();
            List<string> files = new List<string>();
            try { files.AddRange(Directory.GetFiles(gameDir, "*.dll")); } catch { }
            files.Add(Path.Combine(gameDir, "BF2.exe"));
            foreach (string f in files)
            {
                if (!File.Exists(f)) continue;
                List<string> imp = Pe.Imports(f);
                if (imp == null) continue;
                foreach (string i in imp)
                    if (i.StartsWith("d3dx9_", StringComparison.OrdinalIgnoreCase) && !r.Contains(i.ToLowerInvariant())) r.Add(i.ToLowerInvariant());
            }
            r.Sort();
            return r;
        }

        // Fehlende Dateien: weder im 32-Bit-Systemordner noch im Spielordner
        public static List<string> Missing(string gameDir, List<string> needed)
        {
            List<string> r = new List<string>();
            string sys = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
            if (string.IsNullOrEmpty(sys)) sys = Environment.SystemDirectory;
            foreach (string n in needed)
                if (!File.Exists(Path.Combine(sys, n)) && !File.Exists(Path.Combine(gameDir, n))) r.Add(n);
            return r;
        }

        public static string Setup(string gameDir)
        {
            string p = Path.Combine(AppPaths.LanDir(gameDir), "DirectX", "DXSETUP.exe");
            return File.Exists(p) ? p : null;
        }

    }
}
