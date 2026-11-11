// Bot-Werte, Planung, coop_server.txt - BF2 Manager. C# 5 (.NET Framework 4.x).
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
    // ------------------------------------------------------------------ Bots
    public class BotOpt
    {
        public int Count = 8, Target = 16, DistVal = 20, Diff = 30;
        public string Mode = "fixed", DistKind = "enemy", Id = "";

        public BotOpt Clone() { return (BotOpt)MemberwiseClone(); }

        public static BotOpt Make(int count, int diff)
        {
            BotOpt o = new BotOpt(); o.Count = count; o.Target = count + 8; o.Diff = diff; return o;
        }

        public string ToTokens()
        {
            List<string> t = new List<string>();
            t.Add("count=" + Count);
            t.Add(Mode == "fill" ? "mode=fill:" + Target : "mode=fixed");
            t.Add("dist=" + DistKind + ":" + DistVal);
            t.Add("diff=" + Diff);
            if (!string.IsNullOrEmpty(Id)) t.Add("id=" + Id);
            return string.Join(" ", t.ToArray());
        }

        public static BotOpt FromTokens(IEnumerable<string> tokens)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string t in tokens)
            {
                int i = t.IndexOf('=');
                if (i > 0) d[t.Substring(0, i)] = t.Substring(i + 1);
            }
            BotOpt o = new BotOpt();
            string v;
            if (d.TryGetValue("count", out v)) o.Count = Util.ToInt(v, 8);
            if (d.TryGetValue("mode", out v))
            {
                if (v.StartsWith("fill", StringComparison.OrdinalIgnoreCase))
                {
                    o.Mode = "fill";
                    int j = v.IndexOf(':');
                    o.Target = j >= 0 ? Util.ToInt(v.Substring(j + 1), 16) : 16;
                }
                else o.Mode = "fixed";
            }
            if (d.TryGetValue("dist", out v))
            {
                int j = v.IndexOf(':');
                if (j > 0) { o.DistKind = v.Substring(0, j).ToLowerInvariant() == "ratio" ? "ratio" : "enemy"; o.DistVal = Util.ToInt(v.Substring(j + 1), 20); }
            }
            if (d.TryGetValue("diff", out v)) o.Diff = Util.ToInt(v, 30);
            if (d.TryGetValue("id", out v)) o.Id = v;
            o.Count = Util.Clamp(o.Count, 0, 128); o.Target = Util.Clamp(o.Target, 1, 128);
            o.Diff = Util.Clamp(o.Diff, 0, 100); o.DistVal = Util.Clamp(o.DistVal, 0, o.DistKind == "ratio" ? 100 : 300);
            return o;
        }
    }

    public class BotPlan
    {
        public int Count, Ratio, Diff, BotsUS, BotsMEC, HumansUS, HumansMEC;
        public int Requested; // gewünschte Botzahl vor der Begrenzung durch das Spiel
        public bool Capped { get { return Requested > Count; } }
        public int TotalUS { get { return HumansUS + BotsUS; } }
        public int TotalMEC { get { return HumansMEC + BotsMEC; } }
        public int Total { get { return TotalUS + TotalMEC; } }
    }

    public static class Planner
    {
        // Der Coop-Server setzt höchstens 32 Bots ein – unabhängig von sv.maxPlayers und aiDefault.ai (setMaxNBots).
        // Gemessen mit bf2all64: 40 angefordert -> 32 (16/16), mit Ratio 75 -> 32 (8 US / 24 MEC).
        public const int EngineMaxBots = 32;

        static int R(double x) { return (int)Math.Floor(x + 0.5); }

        // Kleinste Abweichung: welche Ratio ergibt gerundet genau b Bots im Team MEC? (gleich wie coopbots.py)
        public static int RatioFor(int count, int b)
        {
            if (count <= 0) return 50;
            int best = -1; double bestDist = 1000, target = b * 100.0 / count;
            for (int r = 0; r <= 100; r++)
            {
                if (R(count * r / 100.0) == b)
                {
                    double dd = Math.Abs(r - target);
                    if (dd < bestDist) { bestDist = dd; best = r; }
                }
            }
            return best < 0 ? R(target) : best;
        }

        public static BotPlan Plan(BotOpt o, int hUS, int hMEC)
        {
            int humans = hUS + hMEC;
            int count = o.Mode == "fill" ? o.Target - humans : o.Count;
            if (count < 0) count = 0;
            int requested = count;
            if (count > EngineMaxBots) count = EngineMaxBots;
            int ratio;
            if (o.DistKind == "enemy")
            {
                double x = o.DistVal / 100.0;
                int mecTotal = R((1 + x) * (humans + count) / (2 + x));
                int b = Util.Clamp(mecTotal - hMEC, 0, count);
                ratio = RatioFor(count, b);
            }
            else ratio = o.DistVal;
            ratio = Util.Clamp(ratio, 0, 100);
            BotPlan p = new BotPlan();
            p.Count = count; p.Requested = requested; p.Ratio = ratio; p.Diff = Util.Clamp(o.Diff, 0, 100);
            p.BotsMEC = R(count * ratio / 100.0); p.BotsUS = count - p.BotsMEC;
            p.HumansUS = hUS; p.HumansMEC = hMEC;
            return p;
        }
    }

    public class MapEntry
    {
        public string Name, Mode;
        public int Size;
        public BotOpt Opt;
        // Spaßregeln nur für diese Map (null = normale Regeln); Schlüssel wie FunOptions.ToDict
        public Dictionary<string, string> Fun;
        public string Key { get { return Name + "|" + Mode + "|" + Size; } }
        public MapEntry Clone()
        {
            MapEntry m = (MapEntry)MemberwiseClone(); m.Opt = Opt == null ? null : Opt.Clone();
            m.Fun = Fun == null ? null : new Dictionary<string, string>(Fun, StringComparer.OrdinalIgnoreCase); return m;
        }
        public static MapEntry Make(string name, string mode, int size) { MapEntry m = new MapEntry(); m.Name = name; m.Mode = mode; m.Size = size; return m; }
    }

    public class AvailMap
    {
        public string Name, Mode, Dir, Display;
        public int Size;
        public string Key { get { return Name + "|" + Mode + "|" + Size; } }
    }

    // ------------------------------------------------------------------ coop_server.txt
    public class CoopState
    {
        public int HumansUS = 8, HumansMEC = 0;
        public bool Commander = true;
        public Dictionary<int, BotOpt> Defaults = new Dictionary<int, BotOpt>();
        public List<MapEntry> Maps = new List<MapEntry>();
        public BotOpt Next;
        public bool Adaptive; public int AdaptStep = 5, AdaptMax = 30;

        public CoopState()
        {
            Defaults[16] = BotOpt.Make(8, 30); Defaults[32] = BotOpt.Make(24, 30); Defaults[64] = BotOpt.Make(Planner.EngineMaxBots, 30);
        }

        public BotOpt OptFor(MapEntry m)
        {
            if (m.Opt != null) return m.Opt;
            BotOpt o;
            if (Defaults.TryGetValue(m.Size, out o)) return o;
            return BotOpt.Make(8, 30);
        }
    }

    public static class CoopTxt
    {
        public static CoopState Read(string path)
        {
            if (!File.Exists(path)) return null;
            CoopState st = new CoopState();
            foreach (string raw in Util.ReadLines(path))
            {
                string line = raw;
                int h = line.IndexOf('#'); if (h >= 0) line = line.Substring(0, h);
                string[] p = line.Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length == 0) continue;
                string key = p[0].ToLowerInvariant();
                if (key == "humans" && p.Length > 1) { st.HumansUS = Util.ToInt(p[1], 8); st.HumansMEC = p.Length > 2 ? Util.ToInt(p[2], 0) : 0; }
                else if (key == "commander" && p.Length > 1) st.Commander = p[1] != "0";
                else if (key == "default" && p.Length > 1) st.Defaults[Util.ToInt(p[1], 0)] = BotOpt.FromTokens(Util.Skip(p, 2));
                else if (key == "map" && p.Length > 3)
                {
                    MapEntry m = MapEntry.Make(p[1], p[2].ToLowerInvariant(), Util.ToInt(p[3], 0));
                    if (p.Length > 4) m.Opt = BotOpt.FromTokens(Util.Skip(p, 4));
                    st.Maps.Add(m);
                }
                else if (key == "next") st.Next = BotOpt.FromTokens(Util.Skip(p, 1));
                else if (key == "adaptive" && p.Length > 1)
                {
                    st.Adaptive = p[1] == "1";
                    if (p.Length > 2) st.AdaptStep = Util.Clamp(Util.ToInt(p[2], 5), 1, 25);
                    if (p.Length > 3) st.AdaptMax = Util.Clamp(Util.ToInt(p[3], 30), 5, 60);
                }
            }
            return st;
        }

        public static void Write(string path, CoopState st)
        {
            List<string> l = new List<string>();
            l.Add("# BF2 Manager - written by the app, read by coopbots.py at every map change.");
            l.Add("# bots: count=N mode=fixed | mode=fill:TARGET (bots = TARGET - humans)");
            l.Add("# distribution: dist=ratio:P (P % of bots on team MEC) | dist=enemy:X (team MEC has X % more players)");
            l.Add("version 2");
            l.Add("humans " + st.HumansUS + " " + st.HumansMEC);
            l.Add("commander " + (st.Commander ? "1" : "0"));
            l.Add("adaptive " + (st.Adaptive ? "1" : "0") + " " + st.AdaptStep + " " + st.AdaptMax);
            foreach (int s in new int[] { 16, 32, 64 })
            {
                BotOpt o; if (st.Defaults.TryGetValue(s, out o)) l.Add("default " + s + " " + o.ToTokens());
            }
            foreach (MapEntry m in st.Maps)
                l.Add("map " + m.Name + " " + m.Mode + " " + m.Size + (m.Opt != null ? " " + m.Opt.ToTokens() : ""));
            if (st.Next != null) l.Add("next " + st.Next.ToTokens());
            Util.WriteAtomic(path, string.Join("\r\n", l.ToArray()) + "\r\n", Encoding.ASCII);
        }
    }

    // ------------------------------------------------------------------ Abend-Bestenliste aus coop_rounds.txt
    public class LeaderRow { public string Name; public int Score, Kills, Deaths, Rounds, Wins; }

    public static class Leaderboard
    {
        // Rundenzeilen: round|zeit|map|sieger|tickets1|tickets2|menschenTeam ; danach player|name|team|punkte|kills|tode
        public static List<LeaderRow> Read(string path, long since, out int rounds)
        {
            rounds = 0;
            Dictionary<string, LeaderRow> rows = new Dictionary<string, LeaderRow>(StringComparer.OrdinalIgnoreCase);
            bool inRound = false; int winner = 0;
            foreach (string l in Util.ReadLines(path))
            {
                string[] p = l.Split('|');
                if (p[0] == "round" && p.Length >= 4)
                {
                    long t; long.TryParse(p[1], out t);
                    inRound = t >= since;
                    winner = Util.ToInt(p[3], 0);
                    if (inRound) rounds++;
                }
                else if (p[0] == "player" && p.Length >= 6 && inRound)
                {
                    LeaderRow r;
                    if (!rows.TryGetValue(p[1], out r)) { r = new LeaderRow(); r.Name = p[1]; rows[p[1]] = r; }
                    r.Score += Util.ToInt(p[3], 0); r.Kills += Util.ToInt(p[4], 0); r.Deaths += Util.ToInt(p[5], 0); r.Rounds++;
                    if (winner != 0 && Util.ToInt(p[2], 0) == winner) r.Wins++;
                }
            }
            List<LeaderRow> list = new List<LeaderRow>(rows.Values);
            list.Sort(delegate (LeaderRow a, LeaderRow b) { int c = b.Score.CompareTo(a.Score); return c != 0 ? c : b.Kills.CompareTo(a.Kills); });
            return list;
        }
    }
}
