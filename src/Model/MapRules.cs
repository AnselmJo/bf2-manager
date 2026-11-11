// Spaßregeln pro Rotations-Eintrag speichern und laden - BF2 Manager. C# 5 (.NET Framework 4.x).
// Datei im Einstellungsordner des Mods (wird nicht übers LAN abgeglichen): eine Zeile pro Map mit Regeln,
// "<Position>|<Map-Schlüssel>|Key=Wert;Key=Wert". Beim Laden zählt erst die Position, sonst der erste freie gleiche Schlüssel.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BF2Coop
{
    public static class MapRules
    {
        public const string FileName = "manager_maprules.txt";

        public static void Save(string file, List<MapEntry> maps)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i].Fun == null) continue;
                List<string> kv = new List<string>();
                foreach (KeyValuePair<string, string> p in maps[i].Fun) kv.Add(p.Key + "=" + p.Value);
                sb.Append(i).Append('|').Append(maps[i].Key).Append('|').Append(string.Join(";", kv.ToArray())).Append("\r\n");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            if (sb.Length == 0) { if (File.Exists(file)) File.Delete(file); return; }
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        }

        public static void Apply(string file, List<MapEntry> maps)
        {
            foreach (MapEntry m in maps) m.Fun = null;
            if (!File.Exists(file)) return;
            List<string[]> rows = new List<string[]>();
            foreach (string l in File.ReadAllLines(file)) { string[] p = l.Split(new char[] { '|' }, 5); if (p.Length == 5) rows.Add(new string[] { p[0], p[1] + "|" + p[2] + "|" + p[3], p[4] }); }
            List<string[]> rest = new List<string[]>();
            foreach (string[] r in rows)
            {
                int i = Util.ToInt(r[0], -1);
                if (i >= 0 && i < maps.Count && maps[i].Key.Equals(r[1], StringComparison.OrdinalIgnoreCase) && maps[i].Fun == null) maps[i].Fun = Parse(r[2]);
                else rest.Add(r);
            }
            foreach (string[] r in rest)
                foreach (MapEntry m in maps)
                    if (m.Fun == null && m.Key.Equals(r[1], StringComparison.OrdinalIgnoreCase)) { m.Fun = Parse(r[2]); break; }
        }

        public static Dictionary<string, string> Parse(string s)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string kv in (s ?? "").Split(';')) { int e = kv.IndexOf('='); if (e > 0) d[kv.Substring(0, e).Trim()] = kv.Substring(e + 1).Trim(); }
            return d;
        }
    }
}
