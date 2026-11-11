// LAN: Server-Finder, Datei-Dienst, Abgleich und Sync - BF2 Manager. C# 5 (.NET Framework 4.x).
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
    public class LanHost
    {
        public string Ip, Name, Mod, Map, Version, Mode, Id, ModVersion;
        // Prüfsumme über alle Mod-Dateien (leer bei älteren Managern oder solange sie noch berechnet wird)
        public string Fp;
        // Letzte Ansage des Server-PCs (Kennung, Text) und der Mod der nächsten Playlist-Runde
        public string AnnId = "", AnnText = "", NextMod = "";
        public int GamePort, Humans, Max, HttpPort, Size, Bots;
        public bool Running, Self;
        public DateTime Seen;
        // Ein PC mit mehreren Netzwerkkarten erscheint nur einmal (eigene Kennung je Manager)
        public string Key { get { return string.IsNullOrEmpty(Id) ? Ip + ":" + HttpPort : Id; } }
    }

    public class FileEntry { public string Rel; public long Size; public string Hash; }

    public class SyncPlan
    {
        public List<FileEntry> Missing = new List<FileEntry>(), Different = new List<FileEntry>();
        public List<string> Extra = new List<string>();
        public long Bytes { get { long b = 0; foreach (FileEntry e in Missing) b += e.Size; foreach (FileEntry e in Different) b += e.Size; return b; } }
        public int Count { get { return Missing.Count + Different.Count; } }
    }

    public static class Lan
    {
        public const int BeaconPort = 29960, HttpPort = 29961;
        public const string Magic = "BF2SM1";
        // Prüfpaket von Spieler-PCs ohne Server: nur um zu testen, ob der eigene Empfang funktioniert (andere ignorieren es)
        public const string ProbeMagic = "BF2SM1P|";

        public static List<IPAddress> LocalAddresses()
        {
            List<IPAddress> r = new List<IPAddress>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation u in ni.GetIPProperties().UnicastAddresses)
                        if (u.Address.AddressFamily == AddressFamily.InterNetwork) r.Add(u.Address);
                }
            }
            catch { }
            return r;
        }

        static List<IPAddress> Broadcasts()
        {
            List<IPAddress> r = new List<IPAddress>();
            r.Add(IPAddress.Broadcast);
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation u in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (u.Address.AddressFamily != AddressFamily.InterNetwork || u.IPv4Mask == null) continue;
                        byte[] a = u.Address.GetAddressBytes(), m = u.IPv4Mask.GetAddressBytes(), b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~m[i]);
                        r.Add(new IPAddress(b));
                    }
                }
            }
            catch { }
            return r;
        }

        public static string Esc(string s) { return (s ?? "").Replace("|", "/").Replace("\n", " "); }

        // ---- Bekanntmachung im LAN (nur der Server-PC)
        public static void Announce(string payload)
        {
            byte[] data = Encoding.UTF8.GetBytes(payload);
            using (UdpClient u = new UdpClient())
            {
                u.EnableBroadcast = true;
                foreach (IPAddress b in Broadcasts()) { try { u.Send(data, data.Length, new IPEndPoint(b, BeaconPort)); } catch { } }
            }
        }

        public static string Payload(string name, int gamePort, string mod, string map, int humans, int max, string mode, bool running)
        {
            return Magic + "|" + Esc(name) + "|" + gamePort + "|" + Esc(mod) + "|" + Esc(map) + "|" + humans + "|" + max + "|" + HttpPort + "|" + AppInfo.Version + "|" + mode + "|" + (running ? "1" : "0");
        }

        public static LanHost Parse(string msg, IPAddress from)
        {
            string[] p = msg.Split('|');
            if (p.Length < 11 || p[0] != Magic) return null;
            LanHost h = new LanHost();
            h.Ip = from.ToString(); h.Name = p[1]; h.GamePort = Util.ToInt(p[2], 16567); h.Mod = p[3]; h.Map = p[4];
            h.Humans = Util.ToInt(p[5], 0); h.Max = Util.ToInt(p[6], 0); h.HttpPort = Util.ToInt(p[7], HttpPort); h.Version = p[8]; h.Mode = p[9]; h.Running = p[10] == "1";
            if (p.Length >= 15) { h.Id = p[11]; h.Size = Util.ToInt(p[12], 0); h.Bots = Util.ToInt(p[13], 0); h.ModVersion = p[14]; }
            if (p.Length >= 16) h.Fp = p[15];
            if (p.Length >= 19) { h.AnnId = p[16]; h.AnnText = p[17]; h.NextMod = p[18]; }
            h.Seen = DateTime.Now;
            foreach (IPAddress a in LocalAddresses()) if (a.Equals(from)) h.Self = true;
            return h;
        }

        // ---- Manifest: relativer Pfad, Größe, SHA1 (mit Cache nach Größe und Änderungszeit)
        static readonly string[] ExcludeDirs = { "settings\\", "python\\", "ai\\", "cache\\", "logs\\" };
        public static bool Excluded(string rel)
        {
            string r = rel.Replace('/', '\\').ToLowerInvariant();
            foreach (string d in ExcludeDirs) if (r.StartsWith(d)) return true;
            string f = Path.GetFileName(r);
            return f.EndsWith(".bak") || f.EndsWith(".part") || f.EndsWith(".log") || f.StartsWith("coop_") || f.StartsWith(".bf2sm");
        }

        public static List<FileEntry> BuildManifest(string modRoot, Action<int, int> progress, Func<bool> cancel)
        {
            string cacheFile = Path.Combine(AppPaths.Cache, "hash_" + Util.SafeName(modRoot.Replace(':', '_').Replace('\\', '_')) + ".txt");
            Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string l in Util.ReadLines(cacheFile)) { int i = l.LastIndexOf('|'); if (i > 0) cache[l.Substring(0, i)] = l.Substring(i + 1); }
            List<FileEntry> r = new List<FileEntry>();
            if (!Directory.Exists(modRoot)) return r;
            string[] files = ListFiles(modRoot).ToArray();
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            List<string> newCache = new List<string>();
            int n = 0;
            foreach (string f in files)
            {
                if (cancel != null && cancel()) throw new OperationCanceledException();
                n++;
                string rel = f.Substring(modRoot.Length).TrimStart('\\', '/');
                if (Excluded(rel)) continue;
                FileInfo fi = new FileInfo(f);
                string key = rel + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks, h;
                if (!cache.TryGetValue(key, out h))
                {
                    using (SHA1 sha = SHA1.Create())
                    using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) h = Updater.Hex(sha.ComputeHash(fs));
                }
                newCache.Add(key + "|" + h);
                FileEntry e = new FileEntry(); e.Rel = rel; e.Size = fi.Length; e.Hash = h; r.Add(e);
                if (progress != null && (n % 25 == 0 || n == files.Length)) progress(n, files.Length);
            }
            try { Directory.CreateDirectory(AppPaths.Cache); File.WriteAllLines(cacheFile, newCache.ToArray(), Encoding.UTF8); } catch { }
            return r;
        }

        // Alle Dateien unterhalb von root; verknüpfte Ordner (Junctions eines Spaß-Aufsatzes) zählen nicht dazu
        public static List<string> ListFiles(string root)
        {
            List<string> r = new List<string>();
            Stack<string> dirs = new Stack<string>(); dirs.Push(root);
            while (dirs.Count > 0)
            {
                string d = dirs.Pop();
                r.AddRange(Directory.GetFiles(d));
                foreach (string sub in Directory.GetDirectories(d))
                    if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0) dirs.Push(sub);
            }
            return r;
        }

        // Kurze Prüfsumme über das Manifest: gleich = gleiche Dateien mit gleichem Inhalt
        public static string Fingerprint(List<FileEntry> m)
        {
            List<FileEntry> s = new List<FileEntry>(m);
            s.Sort(delegate (FileEntry a, FileEntry b) { return string.Compare(a.Rel, b.Rel, StringComparison.OrdinalIgnoreCase); });
            StringBuilder sb = new StringBuilder();
            foreach (FileEntry e in s) sb.Append(e.Rel.ToLowerInvariant().Replace('/', '\\')).Append('|').Append(e.Size).Append('|').Append(e.Hash.ToLowerInvariant()).Append('\n');
            using (SHA1 sha = SHA1.Create()) return Updater.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Substring(0, 16).ToLowerInvariant();
        }

        public static string ManifestText(List<FileEntry> m)
        {
            StringBuilder sb = new StringBuilder();
            foreach (FileEntry e in m) sb.Append(e.Rel).Append('|').Append(e.Size).Append('|').Append(e.Hash).Append('\n');
            return sb.ToString();
        }

        public static List<FileEntry> ParseManifest(string text)
        {
            List<FileEntry> r = new List<FileEntry>();
            foreach (string l in text.Replace("\r", "").Split('\n'))
            {
                string[] p = l.Split('|');
                if (p.Length < 3) continue;
                if (p[0].Contains("..") || Path.IsPathRooted(p[0])) continue;
                FileEntry e = new FileEntry(); e.Rel = p[0]; long.TryParse(p[1], out e.Size); e.Hash = p[2]; r.Add(e);
            }
            return r;
        }

        public static SyncPlan Diff(List<FileEntry> local, List<FileEntry> remote)
        {
            SyncPlan plan = new SyncPlan();
            Dictionary<string, FileEntry> loc = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (FileEntry e in local) loc[e.Rel] = e;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FileEntry r in remote)
            {
                seen.Add(r.Rel);
                FileEntry l;
                if (!loc.TryGetValue(r.Rel, out l)) plan.Missing.Add(r);
                else if (l.Size != r.Size || !l.Hash.Equals(r.Hash, StringComparison.OrdinalIgnoreCase)) plan.Different.Add(r);
            }
            foreach (FileEntry l in local) if (!seen.Contains(l.Rel)) plan.Extra.Add(l.Rel);
            return plan;
        }

        // Sicherer Zielpfad innerhalb des Mod-Ordners, sonst null
        public static string SafePath(string root, string rel)
        {
            if (string.IsNullOrEmpty(rel) || rel.Contains("..") || rel.Contains(":") || rel.StartsWith("\\") || rel.StartsWith("/") || Path.IsPathRooted(rel)) return null;
            char sep = Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root, rel.Replace('\\', sep).Replace('/', sep)));
            string r = Path.GetFullPath(root).TrimEnd(sep) + sep;
            return full.StartsWith(r, StringComparison.OrdinalIgnoreCase) ? full : null;
        }

        public static string Url(LanHost h, string path) { return "http://" + h.Ip + ":" + h.HttpPort + path; }

        // Lädt eine Datei vom Server-PC, prüft SHA1 und ersetzt die lokale Datei erst danach
        public static void Fetch(LanHost h, string mod, FileEntry e, string modRoot, Action<long> onBytes, Func<bool> cancel)
        {
            string target = SafePath(modRoot, e.Rel);
            if (target == null) throw new Exception("invalid path " + e.Rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string part = target + ".part";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Url(h, "/file?mod=" + Uri.EscapeDataString(mod) + "&p=" + Uri.EscapeDataString(e.Rel)));
            req.Timeout = 30000; req.ReadWriteTimeout = 60000; req.Proxy = null;
            using (WebResponse resp = req.GetResponse())
            using (Stream src = resp.GetResponseStream())
            using (FileStream dst = new FileStream(part, FileMode.Create, FileAccess.Write))
            using (SHA1 sha = SHA1.Create())
            {
                byte[] buf = new byte[262144]; int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    if (cancel != null && cancel()) throw new OperationCanceledException();
                    dst.Write(buf, 0, n); sha.TransformBlock(buf, 0, n, null, 0);
                    if (onBytes != null) onBytes(n);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                if (!Updater.Hex(sha.Hash).Equals(e.Hash, StringComparison.OrdinalIgnoreCase)) { dst.Close(); File.Delete(part); throw new Exception(I18n.T("Prüfsumme falsch: ", "Checksum mismatch: ") + e.Rel); }
            }
            if (File.Exists(target)) { File.SetAttributes(target, FileAttributes.Normal); File.Delete(target); }
            File.Move(part, target);
        }

        public static string GetText(LanHost h, string path, int timeout)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Url(h, path));
            req.Timeout = timeout; req.ReadWriteTimeout = timeout; req.Proxy = null;
            using (WebResponse resp = req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
        }
    }

    // ---- Empfang der Bekanntmachungen (alle PCs)
    public class LanListener : IDisposable
    {
        UdpClient _udp; Thread _th; volatile bool _stop;
        readonly Dictionary<string, LanHost> _hosts = new Dictionary<string, LanHost>();
        public string Error;
        // Kennung dieses Managers; kommt die eigene Bekanntmachung zurück, funktioniert der Empfang (Firewall offen)
        public string OwnId; public DateTime SelfSeen = DateTime.MinValue;

        public void Start()
        {
            try
            {
                _udp = new UdpClient();
                _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udp.Client.Bind(new IPEndPoint(IPAddress.Any, Lan.BeaconPort));
                _th = new Thread(Loop); _th.IsBackground = true; _th.Start();
            }
            catch (Exception ex) { Error = ex.Message; }
        }

        void Loop()
        {
            while (!_stop)
            {
                try
                {
                    IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] d = _udp.Receive(ref ep);
                    string msg = Encoding.UTF8.GetString(d);
                    if (msg.StartsWith(Lan.ProbeMagic)) { if (OwnId != null && msg.Substring(Lan.ProbeMagic.Length) == OwnId) SelfSeen = DateTime.Now; continue; }
                    LanHost h = Lan.Parse(msg, ep.Address);
                    if (h != null) { if (OwnId != null && h.Id == OwnId) SelfSeen = DateTime.Now; lock (_hosts) _hosts[h.Key] = h; }
                }
                catch { if (_stop) return; Thread.Sleep(200); }
            }
        }

        public List<LanHost> Hosts()
        {
            List<LanHost> r = new List<LanHost>();
            lock (_hosts) foreach (LanHost h in _hosts.Values) if ((DateTime.Now - h.Seen).TotalSeconds < 12) r.Add(h);
            r.Sort(delegate (LanHost a, LanHost b) { int c = b.Running.CompareTo(a.Running); return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            return r;
        }

        public void Clear() { lock (_hosts) _hosts.Clear(); }

        public void Dispose() { _stop = true; try { if (_udp != null) _udp.Close(); } catch { } }
    }

    // ---- Datei-Dienst des Server-PCs (nur lesend, nur Mod-Ordner und die eigene exe)
    public class LanFileServer : IDisposable
    {
        TcpListener _tcp; Thread _th; volatile bool _stop;
        readonly string _gameDir;
        public string Error;

        readonly int _port;
        public LanFileServer(string gameDir) : this(gameDir, Lan.HttpPort) { }
        public LanFileServer(string gameDir, int port) { _gameDir = gameDir; _port = port; }

        public void Start()
        {
            try
            {
                _tcp = new TcpListener(IPAddress.Any, _port);
                _tcp.Start();
                _th = new Thread(Loop); _th.IsBackground = true; _th.Start();
            }
            catch (Exception ex) { Error = ex.Message; }
        }

        void Loop()
        {
            while (!_stop)
            {
                try { TcpClient c = _tcp.AcceptTcpClient(); ThreadPool.QueueUserWorkItem(delegate { Handle(c); }); }
                catch { if (_stop) return; Thread.Sleep(200); }
            }
        }

        // Dateiliste aus dem gemeinsamen Speicher: fertig = Liste, sonst null und pct (die Berechnung läuft im Hintergrund weiter).
        // Früher hielt die erste große Prüfung die Anfrage minutenlang fest – der Spieler-PC lief dann in einen Timeout.
        public List<FileEntry> Manifest(string mod, out int pct) { return ManifestStore.TryGet(Path.Combine(_gameDir, "mods", mod), out pct); }

        void Handle(TcpClient c)
        {
            try
            {
                using (c)
                using (NetworkStream ns = c.GetStream())
                {
                    c.ReceiveTimeout = 10000; c.SendTimeout = 120000;
                    string reqLine = ReadHead(ns);
                    if (reqLine == null) return;
                    string[] parts = reqLine.Split(' ');
                    if (parts.Length < 2 || parts[0] != "GET") { Send(ns, 405, "text/plain", Encoding.ASCII.GetBytes("method")); return; }
                    string path = parts[1], query = "";
                    int q = path.IndexOf('?'); if (q >= 0) { query = path.Substring(q + 1); path = path.Substring(0, q); }
                    Dictionary<string, string> args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string kv in query.Split('&')) { int e = kv.IndexOf('='); if (e > 0) args[kv.Substring(0, e)] = Uri.UnescapeDataString(kv.Substring(e + 1).Replace('+', ' ')); }
                    string mod; args.TryGetValue("mod", out mod);
                    if (mod != null && (mod.Contains("..") || mod.Contains("\\") || mod.Contains("/") || !Directory.Exists(Path.Combine(_gameDir, "mods", mod)))) { Send(ns, 404, "text/plain", Encoding.ASCII.GetBytes("mod")); return; }
                    if (path == "/info")
                        Send(ns, 200, "text/plain", Encoding.UTF8.GetBytes(Lan.Magic + "|" + AppInfo.Version + "|" + string.Join(",", GameLocator.AllMods(_gameDir).ToArray())));
                    else if (path == "/manifest" && mod != null)
                    {
                        // Noch nicht fertig: sofort „beschäftigt“ mit Fortschritt melden; der Spieler-PC fragt dann wieder
                        int pct; List<FileEntry> m = Manifest(mod, out pct);
                        if (m == null) Send(ns, 503, "text/plain", Encoding.ASCII.GetBytes("busy|" + pct));
                        else Send(ns, 200, "text/plain", Encoding.UTF8.GetBytes(Lan.ManifestText(m)));
                    }
                    else if (path == "/file" && mod != null)
                    {
                        string rel; args.TryGetValue("p", out rel);
                        string full = Lan.SafePath(Path.Combine(_gameDir, "mods", mod), rel);
                        if (full == null || Lan.Excluded(rel) || !File.Exists(full)) { Send(ns, 404, "text/plain", Encoding.ASCII.GetBytes("file")); return; }
                        SendFile(ns, full);
                    }
                    else if (path == "/manager") SendFile(ns, AppPaths.Exe);
                    else if (path == "/manager.sha256") Send(ns, 200, "text/plain", Encoding.ASCII.GetBytes(Updater.Sha256(AppPaths.Exe) + "  " + AppInfo.ExeName + "\n"));
                    else Send(ns, 404, "text/plain", Encoding.ASCII.GetBytes("not found"));
                }
            }
            catch { }
        }

        static string ReadHead(NetworkStream ns)
        {
            StringBuilder sb = new StringBuilder();
            int b, total = 0;
            while ((b = ns.ReadByte()) >= 0 && total++ < 8192)
            {
                sb.Append((char)b);
                if (sb.Length >= 4 && sb.ToString(sb.Length - 4, 4) == "\r\n\r\n") break;
            }
            string s = sb.ToString();
            int i = s.IndexOf("\r\n");
            return i > 0 ? s.Substring(0, i) : null;
        }

        static void Send(NetworkStream ns, int code, string type, byte[] body)
        {
            byte[] h = Encoding.ASCII.GetBytes("HTTP/1.0 " + code + " " + (code == 200 ? "OK" : (code == 503 ? "Busy" : "Error")) + "\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
            ns.Write(h, 0, h.Length); ns.Write(body, 0, body.Length);
        }

        static void SendFile(NetworkStream ns, string full)
        {
            using (FileStream fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] h = Encoding.ASCII.GetBytes("HTTP/1.0 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: " + fs.Length + "\r\nConnection: close\r\n\r\n");
                ns.Write(h, 0, h.Length);
                byte[] buf = new byte[262144]; int n;
                while ((n = fs.Read(buf, 0, buf.Length)) > 0) ns.Write(buf, 0, n);
            }
        }

        public void Dispose() { _stop = true; try { if (_tcp != null) _tcp.Stop(); } catch { } }
    }
}
