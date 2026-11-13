// Regel-Sets: benannte Spaßregeln, die Maps zugewiesen werden. Ändert man ein Set, gilt das für alle Maps damit - BF2 Manager. C# 5.
// Gespeichert in %APPDATA%\BF2Manager\funsets.json. Eine Map verweist nur auf den Namen (MapEntry.Fun = { "@set": Name }).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BF2Coop
{
    public static class FunSets
    {
        public const string SetKey = "@set";
        static List<KeyValuePair<string, Dictionary<string, string>>> _sets;
        public static string FileOverride;   // Tests
        static string FilePath { get { return FileOverride ?? Path.Combine(AppPaths.Dir, "funsets.json"); } }

        static void Ensure()
        {
            if (_sets != null) return;
            _sets = new List<KeyValuePair<string, Dictionary<string, string>>>();
            try
            {
                if (File.Exists(FilePath))
                {
                    object raw = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(FilePath, Encoding.UTF8));
                    object[] arr = raw as object[];
                    if (arr != null)
                        foreach (object o in arr)
                        {
                            Dictionary<string, object> e = o as Dictionary<string, object>; if (e == null) continue;
                            object n, r; if (!e.TryGetValue("name", out n) || !e.TryGetValue("rules", out r)) continue;
                            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            Dictionary<string, object> rr = r as Dictionary<string, object>;
                            if (rr != null) foreach (KeyValuePair<string, object> kv in rr) d[kv.Key] = Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture);
                            _sets.Add(new KeyValuePair<string, Dictionary<string, string>>(Convert.ToString(n), d));
                        }
                    return;
                }
            }
            catch { }
            Seed();
            Save();
        }

        // Startinhalt beim ersten Mal – danach frei bearbeitbar und löschbar
        static void Seed()
        {
            FunOptions o;
            o = FunOptions.Neutral(); o.WeaponRule = 3; o.C4Mags = 99; o.C4Force = 400; o.BlastSoldierPct = 2; o.NoFallDamage = true; Add(I18n.T("Nur C4", "C4 only"), o);
            o = FunOptions.Neutral(); o.WeaponRule = 4; o.RocketForce = 250; o.RocketDamagePct = 5; o.RocketSpeedPct = 25; o.RocketMags = 50; o.RocketReloadMs = 300; o.NoFallDamage = true; Add(I18n.T("Raketen-Chaos", "Rocket chaos"), o);
            o = FunOptions.Neutral(); o.WeaponRule = 5; o.SniperForce = 300; o.BlastSoldierPct = 2; o.EndlessAmmo = true; Add(I18n.T("Sniper-Duell", "Sniper duel"), o);
            o = FunOptions.Neutral(); o.NoVehicles = true; Add(I18n.T("Nur Infanterie", "Infantry only"), o);
            o = FunOptions.Neutral(); o.WaterKills = true; o.GravityPct = 60; o.NoFallDamage = true; o.EndlessSprint = true; Add(I18n.T("Wasser ist Lava", "Water is lava"), o);
            Add(I18n.T("Alles Spaß", "All the fun"), new FunOptions());
        }

        static void Add(string name, FunOptions o) { _sets.Add(new KeyValuePair<string, Dictionary<string, string>>(name, o.ToDict())); }

        public static void Save()
        {
            Ensure();
            List<object> arr = new List<object>();
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in _sets) { Dictionary<string, object> e = new Dictionary<string, object>(); e["name"] = kv.Key; e["rules"] = kv.Value; arr.Add(e); }
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string tmp = FilePath + ".part";
            File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(arr), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }

        public static void Reload() { _sets = null; }

        public static List<string> Names() { Ensure(); List<string> r = new List<string>(); foreach (KeyValuePair<string, Dictionary<string, string>> kv in _sets) r.Add(kv.Key); return r; }

        public static Dictionary<string, string> Get(string name)
        {
            Ensure();
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in _sets) if (kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        public static void Set(string name, Dictionary<string, string> rules)
        {
            Ensure();
            for (int i = 0; i < _sets.Count; i++)
                if (_sets[i].Key.Equals(name, StringComparison.OrdinalIgnoreCase)) { _sets[i] = new KeyValuePair<string, Dictionary<string, string>>(_sets[i].Key, rules); Save(); return; }
            _sets.Add(new KeyValuePair<string, Dictionary<string, string>>(name, rules)); Save();
        }

        public static bool Rename(string from, string to)
        {
            Ensure(); to = Clean(to);
            if (to.Length == 0 || (Get(to) != null && !to.Equals(from, StringComparison.OrdinalIgnoreCase))) return false;
            for (int i = 0; i < _sets.Count; i++)
                if (_sets[i].Key.Equals(from, StringComparison.OrdinalIgnoreCase)) { _sets[i] = new KeyValuePair<string, Dictionary<string, string>>(to, _sets[i].Value); Save(); return true; }
            return false;
        }

        public static void Delete(string name)
        {
            Ensure();
            _sets.RemoveAll(delegate (KeyValuePair<string, Dictionary<string, string>> kv) { return kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase); });
            Save();
        }

        // Name ohne Zeichen, die die Map-Regeldatei trennen
        public static string Clean(string name)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in (name ?? "").Trim()) if (c != ';' && c != '=' && c != '|' && c != '\r' && c != '\n') sb.Append(c);
            return sb.ToString().Trim();
        }

        public static string UniqueName(string wish)
        {
            string b = Clean(wish); if (b.Length == 0) b = I18n.T("Regel-Set", "Rule set");
            if (Get(b) == null) return b;
            for (int i = 2; ; i++) if (Get(b + " " + i) == null) return b + " " + i;
        }

        // Regeln einer Map: über das Set aufgelöst; ältere Einträge (Regeln direkt an der Map) bleiben lesbar
        public static Dictionary<string, string> RulesOf(MapEntry m)
        {
            if (m == null || m.Fun == null) return null;
            string n; if (m.Fun.TryGetValue(SetKey, out n)) return Get(n);
            return m.Fun;
        }

        public static string SetNameOf(MapEntry m) { string n; return m != null && m.Fun != null && m.Fun.TryGetValue(SetKey, out n) ? n : null; }

        public static Dictionary<string, string> Ref(string name) { Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); d[SetKey] = name; return d; }

        // Alte Einträge mit Regeln direkt an der Map in Sets umwandeln (gleiche Regeln = gleiches Set). true = etwas geändert
        public static bool Migrate(List<MapEntry> maps)
        {
            bool changed = false;
            foreach (MapEntry m in maps)
            {
                if (m.Fun == null || m.Fun.ContainsKey(SetKey)) continue;
                FunOptions o = FunOptions.FromDict(m.Fun);
                if (o.IsNeutral()) { m.Fun = null; changed = true; continue; }
                string found = null;
                foreach (string n in Names()) if (FunOptions.FromDict(Get(n)).Hash() == o.Hash()) { found = n; break; }
                if (found == null) { found = UniqueName(I18n.T("Eigene Regeln", "Custom rules")); Set(found, o.ToDict()); }
                m.Fun = Ref(found); changed = true;
            }
            return changed;
        }
    }
}
