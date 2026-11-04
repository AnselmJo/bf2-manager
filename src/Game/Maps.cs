// Maps aus server.zip, Kartenbilder - BF2 Manager. C# 5 (.NET Framework 4.x).
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
    // ------------------------------------------------------------------ Maps aus server.zip (mit Cache)
    // Eine Coop-Map ist nur spielbar, wenn (gemessen mit bfp2, bf2all64, bf2sf64):
    //  - info/<map>.desc einen Modus gpm_coop nennt – sonst lädt der Server die Map nicht und beendet sich nach ~30 s mit Code 0,
    //  - server.zip ein Wegnetz für die Bots hat (AIPathFinding/*.qtr) und StrategicAreas.ai für die Größe – sonst können keine Bots spielen.
    public static class MapScanner
    {
        static readonly Regex Rx = new Regex(@"^gamemodes[/\\](gpm_coop|gpm_cq)[/\\](\d+)[/\\]", RegexOptions.IgnoreCase);
        static readonly Regex RxCoopAi = new Regex(@"^gamemodes[/\\]gpm_coop[/\\](\d+)[/\\]ai[/\\]strategicareas\.ai$", RegexOptions.IgnoreCase);
        static readonly Regex RxPath = new Regex(@"^aipathfinding[/\\][^/\\]+\.qtr$", RegexOptions.IgnoreCase);

        // Nennt die Kartenbeschreibung den Coop-Modus? Fehlt die Datei, wird nicht gefiltert.
        public static bool DescAllowsCoop(string mapDir)
        {
            try
            {
                string d = Path.Combine(mapDir, "info", Path.GetFileName(mapDir) + ".desc");
                if (!File.Exists(d)) return true;
                return Regex.IsMatch(File.ReadAllText(d), @"type\s*=\s*""gpm_coop""", RegexOptions.IgnoreCase);
            }
            catch { return true; }
        }

        public static List<AvailMap> Scan(string gameDir, string mod, string cacheDir)
        {
            List<AvailMap> res = new List<AvailMap>();
            string lv = Path.Combine(gameDir, "mods", mod, "levels");
            if (!Directory.Exists(lv)) return res;
            string cf = Path.Combine(cacheDir, "maps2_" + Util.SafeName(mod) + ".txt"); // 2: Coop nur mit Bot-Daten
            Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string l in Util.ReadLines(cf))
            {
                int i = l.IndexOf('|');
                if (i > 0) cache[l.Substring(0, i)] = l.Substring(i + 1);
            }
            List<string> outCache = new List<string>();
            bool changed = false;
            foreach (string dir in Directory.GetDirectories(lv))
            {
                string zip = Path.Combine(dir, "server.zip");
                if (!File.Exists(zip)) continue;
                FileInfo fi = new FileInfo(zip);
                string stamp = fi.LastWriteTimeUtc.Ticks + "|" + fi.Length;
                string pairs = null, c;
                if (cache.TryGetValue(zip, out c) && c.StartsWith(stamp + "|")) pairs = c.Substring(stamp.Length + 1);
                if (pairs == null) { pairs = ReadPairs(zip); changed = true; }
                outCache.Add(zip + "|" + stamp + "|" + pairs);
                string name = Path.GetFileName(dir);
                string disp = Util.Pretty(name);
                bool coopOk = DescAllowsCoop(dir);
                foreach (string pr in pairs.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] kv = pr.Split(':');
                    if (kv.Length != 2) continue;
                    if (kv[0] == "gpm_coop" && !coopOk) continue;
                    AvailMap a = new AvailMap();
                    a.Name = name; a.Mode = kv[0]; a.Size = Util.ToInt(kv[1], 0); a.Dir = dir; a.Display = disp;
                    res.Add(a);
                }
            }
            if (changed || outCache.Count != cache.Count)
            {
                try { Directory.CreateDirectory(cacheDir); File.WriteAllLines(cf, outCache.ToArray(), Encoding.UTF8); } catch { }
            }
            res.Sort(delegate (AvailMap a, AvailMap b)
            {
                int r = string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase);
                if (r != 0) return r;
                r = string.CompareOrdinal(a.Mode, b.Mode);
                return r != 0 ? r : a.Size.CompareTo(b.Size);
            });
            return res;
        }

        public static string ReadPairs(string zip)
        {
            List<string> list = new List<string>();
            HashSet<string> seen = new HashSet<string>(), coopAi = new HashSet<string>();
            bool path = false;
            try
            {
                using (ZipArchive z = ZipFile.OpenRead(zip))
                {
                    foreach (ZipArchiveEntry e in z.Entries)
                    {
                        if (RxPath.IsMatch(e.FullName)) path = true;
                        Match a = RxCoopAi.Match(e.FullName);
                        if (a.Success) coopAi.Add(a.Groups[1].Value);
                        Match m = Rx.Match(e.FullName);
                        if (!m.Success) continue;
                        int size = Util.ToInt(m.Groups[2].Value, 0);
                        string k = m.Groups[1].Value.ToLowerInvariant() + ":" + size;
                        if (size > 0 && size <= 64 && seen.Add(k)) list.Add(k);
                    }
                }
            }
            catch { }
            // Coop ohne Bot-Daten ist nicht spielbar (z. B. wake_island_1707 in bfp2)
            list.RemoveAll(delegate (string k) { return k.StartsWith("gpm_coop:") && (!path || !coopAi.Contains(k.Substring(9))); });
            return string.Join(",", list.ToArray());
        }
    }

    // ------------------------------------------------------------------ Kartenbilder (PNG oder DDS, Cache als PNG)
    public static class MapImages
    {
        public static Image Load(string cacheDir, string mod, AvailMap a)
        {
            try
            {
                string src = FindSource(a.Dir, a.Mode, a.Size);
                if (src == null) return null;
                string outp = Path.Combine(cacheDir, Util.SafeName(mod + "_" + a.Name + "_" + Path.GetFileNameWithoutExtension(src)) + ".png");
                if (!File.Exists(outp) || File.GetLastWriteTimeUtc(outp) < File.GetLastWriteTimeUtc(src))
                {
                    using (Bitmap bmp = DdsLoader.Load(src))
                    {
                        if (bmp == null) return null;
                        int s = Math.Min(512, Math.Max(bmp.Width, bmp.Height));
                        using (Bitmap t = new Bitmap(s, s))
                        {
                            using (Graphics g = Graphics.FromImage(t))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.DrawImage(bmp, 0, 0, s, s);
                            }
                            Directory.CreateDirectory(cacheDir);
                            t.Save(outp, ImageFormat.Png);
                        }
                    }
                }
                using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(outp)))
                using (Image tmp = Image.FromStream(ms))
                    return new Bitmap(tmp);
            }
            catch { return null; }
        }

        static string FindSource(string mapDir, string mode, int size)
        {
            string info = null;
            try
            {
                foreach (string d in Directory.GetDirectories(mapDir))
                    if (string.Equals(Path.GetFileName(d), "info", StringComparison.OrdinalIgnoreCase)) { info = d; break; }
            }
            catch { }
            if (info == null) return null;
            string[] files;
            try { files = Directory.GetFiles(info); } catch { return null; }
            string[] wanted = { mode + "_" + size + "_menumap", "gpm_coop_" + size + "_menumap", "gpm_cq_" + size + "_menumap" };
            foreach (string w in wanted)
                foreach (string f in files)
                {
                    string n = Path.GetFileNameWithoutExtension(f), ext = Path.GetExtension(f).ToLowerInvariant();
                    if (string.Equals(n, w, StringComparison.OrdinalIgnoreCase) && (ext == ".png" || ext == ".dds")) return f;
                }
            foreach (string f in files)
                if (Path.GetFileName(f).ToLowerInvariant().Contains("menumap")) return f;
            return null;
        }
    }

    public static class DdsLoader
    {
        public static Bitmap Load(string path)
        {
            byte[] d = File.ReadAllBytes(path);
            if (d.Length > 8 && d[0] == 0x89 && d[1] == 0x50)
                using (MemoryStream ms = new MemoryStream(d)) using (Bitmap png = new Bitmap(ms)) return new Bitmap(png);
            if (d.Length < 128 || d[0] != 0x44 || d[1] != 0x44 || d[2] != 0x53) return null;
            int h = BitConverter.ToInt32(d, 12), w = BitConverter.ToInt32(d, 16);
            string cc = Encoding.ASCII.GetString(d, 84, 4);
            if (cc != "DXT1" && cc != "DXT3" && cc != "DXT5") return null;
            if (w <= 0 || h <= 0 || w > 4096 || h > 4096) return null;
            int bs = cc == "DXT1" ? 8 : 16;
            int bw = (w + 3) / 4, bh = (h + 3) / 4;
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            byte[] px = new byte[bd.Stride * h];
            int[] r = new int[4], g = new int[4], b = new int[4];
            for (int blkY = 0; blkY < bh; blkY++)
                for (int blkX = 0; blkX < bw; blkX++)
                {
                    int o = 128 + (blkY * bw + blkX) * bs; if (bs == 16) o += 8;
                    if (o + 8 > d.Length) continue;
                    int c0 = d[o] | (d[o + 1] << 8), c1 = d[o + 2] | (d[o + 3] << 8);
                    r[0] = ((c0 >> 11) & 31) * 255 / 31; g[0] = ((c0 >> 5) & 63) * 255 / 63; b[0] = (c0 & 31) * 255 / 31;
                    r[1] = ((c1 >> 11) & 31) * 255 / 31; g[1] = ((c1 >> 5) & 63) * 255 / 63; b[1] = (c1 & 31) * 255 / 31;
                    if (c0 > c1 || bs == 16)
                    {
                        r[2] = (2 * r[0] + r[1]) / 3; g[2] = (2 * g[0] + g[1]) / 3; b[2] = (2 * b[0] + b[1]) / 3;
                        r[3] = (r[0] + 2 * r[1]) / 3; g[3] = (g[0] + 2 * g[1]) / 3; b[3] = (b[0] + 2 * b[1]) / 3;
                    }
                    else
                    {
                        r[2] = (r[0] + r[1]) / 2; g[2] = (g[0] + g[1]) / 2; b[2] = (b[0] + b[1]) / 2;
                        r[3] = 0; g[3] = 0; b[3] = 0;
                    }
                    uint idx = BitConverter.ToUInt32(d, o + 4);
                    for (int py = 0; py < 4; py++)
                        for (int qx = 0; qx < 4; qx++)
                        {
                            int x = blkX * 4 + qx, y = blkY * 4 + py;
                            if (x >= w || y >= h) continue;
                            int ci = (int)((idx >> (2 * (py * 4 + qx))) & 3);
                            int p = y * bd.Stride + x * 4;
                            px[p] = (byte)b[ci]; px[p + 1] = (byte)g[ci]; px[p + 2] = (byte)r[ci]; px[p + 3] = 255;
                        }
                }
            Marshal.Copy(px, 0, bd.Scan0, px.Length);
            bmp.UnlockBits(bd);
            return bmp;
        }
    }
}
