// Spaß-Aufsatz bauen: mods/<Basis>_fun mit geänderten Waffen-, Soldaten- und Materialwerten - BF2 Manager. C# 5 (.NET Framework 4.x).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace BF2Coop
{
    // Einstellungen eines Spaß-Aufsatzes; 0 bzw. false = unverändert lassen
    public class FunOptions
    {
        public int C4Mags = 99;                 // C4 pro Spieler
        public int C4Force = 300;               // Wucht der C4-Explosion (Original 17)
        public bool NoFallDamage = true;
        public int BlastSoldierPct = 2;         // Schaden von C4 und Spaß-Explosionen an Soldaten in % (Original 100)
        public bool ExtraExplosives = true;     // Spec Ops: zusätzlich Sprung-Claymore (Taste 6) und Mega-Mine (Taste 7)
        public bool C4ForMore = true;           // C4 auch für Pionier, Scharfschütze und Panzerabwehr (Taste 5)
        public int MineForce = 2500;            // Panzermine: wirft Fahrzeuge (Original 10)
        public int ClaymoreForce = 300;         // Claymore: Sprungfalle für Soldaten (Original 7)
        public bool RocketJump = true;          // Panzerfaust-Explosion schiebt stark, verletzt Soldaten kaum (Volltreffer zählt weiter)
        public int RocketForce = 250;           // (Original 7)
        public int RocketSpeedPct = 25;         // Raketen-Höchsttempo in % (100 = normal; klein = ausweichbar)
        public int RocketMags = 50;             // Raketen pro Spieler (Original 5–6)
        public int RocketReloadMs = 300;        // Nachladezeit der Panzerfaust in ms (0 = unverändert, Original 3000)
        public bool EndlessAmmo = true;         // alle Handwaffen: 99 Magazine
        public bool EndlessSprint = true;
        public int GravityPct = 60;             // Schwerkraft in % (100 = normal, 60 = hohe Sprünge, 40 = Mond)
        public int VehiclePowerPct = 200;       // Motorkraft der Landfahrzeuge in % (100 = normal) – „Turbo“
        public bool WaterKills = false;         // Soldaten sterben bei Berührung mit Wasser („Der Boden ist Lava“)
        public int SniperForce = 300;           // Explosiv-Sniper: Einschlag schleudert (0 = aus); Treffer zählen weiter normal
        public int GrenadeForce = 0;            // Granaten-Sprung: Wucht der Handgranaten, verletzen dann kaum (0 = aus, Original 5)
        public int HealthPct = 100;             // Lebenspunkte aller Soldaten (auch Bots) in % (100 = normal)

        static readonly string[] Keys = { "HealthPct", "C4Mags", "C4Force", "NoFallDamage", "BlastSoldierPct", "ExtraExplosives", "C4ForMore", "MineForce", "ClaymoreForce", "RocketJump", "RocketForce", "RocketSpeedPct", "RocketMags", "RocketReloadMs", "EndlessAmmo", "EndlessSprint", "GravityPct", "VehiclePowerPct", "WaterKills", "SniperForce", "GrenadeForce" };

        public Dictionary<string, string> ToDict()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string k in Keys)
            {
                object v = GetType().GetField(k).GetValue(this);
                d[k] = v is bool ? ((bool)v ? "1" : "0") : Convert.ToString(v, CultureInfo.InvariantCulture);
            }
            return d;
        }

        public static FunOptions FromDict(Dictionary<string, string> d)
        {
            FunOptions o = new FunOptions();
            foreach (string k in Keys)
            {
                string s; if (d == null || !d.TryGetValue(k, out s)) continue;
                System.Reflection.FieldInfo f = typeof(FunOptions).GetField(k);
                if (f.FieldType == typeof(bool)) f.SetValue(o, s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase));
                else { int i; if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) f.SetValue(o, i); }
            }
            return o;
        }
    }

    public static class FunPack
    {
        static readonly Encoding Enc = Encoding.GetEncoding(28591);
        public const string ObjectsZip = "fun_objects.zip", CommonZip = "fun_common.zip";
        static readonly int[] SoldierMaterials = { 24, 25, 77 };   // Human_body, Human_head, Human_limbs
        const int C4Material = 80;                                 // Detonation_C4
        static readonly string[] Launchers = { "usatp_predator", "chat_eryx" };
        static readonly string[] Rockets = { "at_predator", "eryx" };

        public static string NameFor(string baseMod) { return baseMod + "_fun"; }

        class Mount { public string Abs, Rel, Root; }

        static List<Mount> Mounts(string gameDir, string baseMod, string file)
        {
            List<Mount> r = new List<Mount>();
            string src = Path.Combine(Path.Combine(gameDir, "mods"), baseMod), path = null;
            foreach (string f in Directory.GetFiles(src)) if (Path.GetFileName(f).Equals(file, StringComparison.OrdinalIgnoreCase)) path = f;
            if (path == null) return r;
            foreach (string l in File.ReadAllLines(path, Enc))
            {
                Match m = Regex.Match(l, @"^\s*fileManager\.mountArchive\s+(\S+)\s+(\S+)", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                string p = m.Groups[1].Value; bool game = Regex.IsMatch(p, @"^mods[/\\]", RegexOptions.IgnoreCase);
                Mount mt = new Mount();
                mt.Abs = game ? Path.Combine(gameDir, p.Replace('/', '\\')) : Path.Combine(src, p.Replace('/', '\\'));
                mt.Rel = game ? p.Replace('\\', '/') : "mods/" + baseMod + "/" + p.Replace('\\', '/');
                mt.Root = m.Groups[2].Value;
                r.Add(mt);
            }
            return r;
        }

        // Liest Dateien so, wie das Spiel sie sieht: aus dem ersten eingebundenen Archiv des Wurzelordners, das sie enthält
        class Reader : IDisposable
        {
            readonly List<KeyValuePair<Mount, ZipArchive>> _zips = new List<KeyValuePair<Mount, ZipArchive>>();
            public Reader(List<Mount> mounts) { foreach (Mount m in mounts) if (File.Exists(m.Abs)) _zips.Add(new KeyValuePair<Mount, ZipArchive>(m, ZipFile.OpenRead(m.Abs))); }

            public string Read(string root, string path, out string realPath)
            {
                realPath = null;
                foreach (KeyValuePair<Mount, ZipArchive> z in _zips)
                {
                    if (!z.Key.Root.Equals(root, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (ZipArchiveEntry e in z.Value.Entries)
                        if (e.FullName.Equals(path, StringComparison.OrdinalIgnoreCase)) { realPath = e.FullName; return ReadEntry(e); }
                }
                return null;
            }

            // Alle Dateien eines Wurzelordners, deren Pfad passt (je Pfad gewinnt das erste Archiv)
            public List<KeyValuePair<string, string>> Find(string root, Regex path)
            {
                List<KeyValuePair<string, string>> r = new List<KeyValuePair<string, string>>();
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<Mount, ZipArchive> z in _zips)
                {
                    if (!z.Key.Root.Equals(root, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (ZipArchiveEntry e in z.Value.Entries)
                        if (path.IsMatch(e.FullName) && seen.Add(e.FullName)) r.Add(new KeyValuePair<string, string>(e.FullName, ReadEntry(e)));
                }
                return r;
            }

            static string ReadEntry(ZipArchiveEntry e) { using (StreamReader sr = new StreamReader(e.Open(), Enc)) return sr.ReadToEnd(); }
            public void Dispose() { foreach (KeyValuePair<Mount, ZipArchive> z in _zips) z.Value.Dispose(); }
        }

        static string Nl(string t) { return t.Contains("\r\n") ? "\r\n" : "\n"; }

        // Setzt "ObjectTemplate.<key> <wert>" in einem Abschnitt; fehlt die Zeile, wird sie nach after (falls angegeben) eingefügt
        public static string SetValue(string text, string key, string value, string after)
        {
            Regex rx = new Regex(@"(?m)^(ObjectTemplate\." + Regex.Escape(key) + @")[ \t]+[^\r\n]*", RegexOptions.IgnoreCase);
            if (rx.IsMatch(text)) return rx.Replace(text, "${1} " + value);
            if (after == null) return text;
            Regex ra = new Regex(@"(?m)^(ObjectTemplate\." + Regex.Escape(after) + @"[ \t]+[^\r\n]*)", RegexOptions.IgnoreCase);
            return ra.IsMatch(text) ? ra.Replace(text, "${1}" + Nl(text) + "ObjectTemplate." + key + " " + value, 1) : text;
        }

        // Wert nur im Abschnitt einer bestimmten Vorlage (ab "create/activeSafe <Typ> <name>" bis zur nächsten Vorlage)
        public static string SetInTemplate(string text, string template, string key, string value, string after)
        {
            Match start = Regex.Match(text, @"(?mi)^ObjectTemplate\.(create|activeSafe)\s+\S+\s+" + Regex.Escape(template) + @"\s*$");
            if (!start.Success) return text;
            int from = start.Index + start.Length;
            Match next = Regex.Match(text.Substring(from), @"(?mi)^ObjectTemplate\.(create|activeSafe)\s+\S+\s+\S+");
            int to = next.Success ? from + next.Index : text.Length;
            return text.Substring(0, from) + SetValue(text.Substring(from, to - from), key, value, after) + text.Substring(to);
        }

        static string F(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        // Fügt Text hinter der ersten addTemplate-Zeile (bzw. der für template) ein – auch bei Windows-Zeilenenden
        public static string AddAfter(string kit, string template, string add)
        {
            Regex rx = new Regex(@"(?mi)^(ObjectTemplate\.addTemplate[ \t]+" + (template == null ? @"[^\s]+" : Regex.Escape(template)) + @")(?=[ \t]*\r?$)");
            return rx.Replace(kit, "${1}" + add.Replace("$", "$$"), 1);
        }

        // Zeilen direkt hinter "ObjectTemplate.create <Typ> <template>" einfügen (oder hinter einer Zeile after in diesem Abschnitt)
        public static string AddAfterCreate(string text, string template, string lines, string after = null)
        {
            Match start = Regex.Match(text, @"(?mi)^ObjectTemplate\.(?:create|activeSafe)\s+\S+\s+" + Regex.Escape(template) + @"(?=[ \t]*\r?$)");
            if (!start.Success) return text;
            int at = start.Index + start.Length;
            if (after != null)
            {
                string sec = Section(text, template) ?? "";
                Match a = Regex.Match(sec, @"(?mi)^" + Regex.Escape(after) + @"(?=[ \t]*\r?$)");
                if (a.Success) at = text.IndexOf(sec, start.Index, StringComparison.Ordinal) + a.Index + a.Length;
            }
            return text.Substring(0, at) + Nl(text) + lines + text.Substring(at);
        }

        // Taste (itemIndex) jeder Handwaffe bzw. jedes Gegenstands, wie das Spiel sie sieht
        static Dictionary<string, int> ItemSlots(Reader rd)
        {
            Dictionary<string, int> r = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> w in rd.Find("Objects", new Regex(@"^Weapons/Handheld/[^/]+/[^/]+\.tweak$", RegexOptions.IgnoreCase)))
                foreach (Match m in Regex.Matches(w.Value, @"(?mi)^ObjectTemplate\.(?:create|activeSafe)\s+\S+\s+(\S+?)(?=[ \t]*\r?$)"))
                {
                    string sec = Section(w.Value, m.Groups[1].Value);
                    Match ix = sec == null ? Match.Empty : Regex.Match(sec, @"(?mi)^ObjectTemplate\.itemIndex\s+(\d+)");
                    if (ix.Success && !r.ContainsKey(m.Groups[1].Value)) r[m.Groups[1].Value] = int.Parse(ix.Groups[1].Value);
                }
            return r;
        }

        public static List<string> KitItems(string kit)
        {
            List<string> r = new List<string>();
            foreach (Match m in Regex.Matches(kit, @"(?mi)^ObjectTemplate\.addTemplate[ \t]+([^\s]+)")) r.Add(m.Groups[1].Value);
            return r;
        }

        static bool Has(List<string> items, string name) { foreach (string s in items) if (s.Equals(name, StringComparison.OrdinalIgnoreCase)) return true; return false; }

        // Belegte Tasten eines Kits (ohne ignore)
        public static List<int> UsedKeys(List<string> items, Dictionary<string, int> slot, string ignore)
        {
            List<int> r = new List<int>(); int k;
            foreach (string s in items) if ((ignore == null || !s.Equals(ignore, StringComparison.OrdinalIgnoreCase)) && slot.TryGetValue(s, out k)) r.Add(k);
            return r;
        }

        // Taste, die in allen Pflicht-Kits frei ist und in möglichst vielen optionalen Kits; -1 = keine
        public static int BestKey(int[] candidates, string item, List<string> must, List<string> optional, Dictionary<string, List<string>> kitItems, Dictionary<string, int> slot, int exclude)
        {
            int best = -1, bestN = -1;
            foreach (int c in candidates)
            {
                if (c == exclude || FreeKey(new int[] { c }, item, must, kitItems, slot, -1) < 0) continue;
                int n = 0; foreach (string k in optional) if (UsedKeys(kitItems[k], slot, item).IndexOf(c) < 0) n++;
                if (n > bestN) { best = c; bestN = n; }
            }
            // Ohne Gewinn (kein optionales Kit frei) nicht verschieben
            return bestN > 0 || optional.Count == 0 && must.Count > 0 && best > 0 ? best : -1;
        }

        // Erste Taste aus candidates, die in allen Ziel-Kits frei ist (außer exclude); -1 = keine
        public static int FreeKey(int[] candidates, string item, List<string> kits, Dictionary<string, List<string>> kitItems, Dictionary<string, int> slot, int exclude)
        {
            foreach (int c in candidates)
            {
                if (c == exclude) continue;
                bool ok = true;
                foreach (string k in kits) if (UsedKeys(kitItems[k], slot, item).IndexOf(c) >= 0) { ok = false; break; }
                if (ok) return c;
            }
            return -1;
        }

        static double Num(string s) { return double.Parse(s, CultureInfo.InvariantCulture); }

        // Text des Abschnitts einer Vorlage (bis zur nächsten), sonst null
        public static string Section(string text, string template)
        {
            Match start = Regex.Match(text, @"(?mi)^ObjectTemplate\.(create|activeSafe)\s+\S+\s+" + Regex.Escape(template) + @"\s*$");
            if (!start.Success) return null;
            int from = start.Index + start.Length;
            Match next = Regex.Match(text.Substring(from), @"(?mi)^ObjectTemplate\.(create|activeSafe)\s+\S+\s+\S+");
            return text.Substring(from, next.Success ? next.Index : text.Length - from);
        }

        // Baut mods/<Basis>_fun neu. Ordner werden als Verknüpfung (Junction) auf den Basis-Mod angelegt.
        public static List<string> Build(string gameDir, string baseMod, FunOptions o)
        {
            List<string> log = new List<string>();
            string mods = Path.Combine(gameDir, "mods"), src = Path.Combine(mods, baseMod), name = NameFor(baseMod), dst = Path.Combine(mods, name);
            if (!Directory.Exists(src)) throw new Exception(I18n.T("Basis-Mod fehlt: ", "Base mod missing: ") + baseMod);
            if (FunMods.BaseOf(gameDir, baseMod) != null) throw new Exception(I18n.T("Ein Spaß-Aufsatz kann nicht Basis eines weiteren sein.", "A fun pack cannot be the base of another one."));
            List<Mount> srv = Mounts(gameDir, baseMod, "serverarchives.con"), cli = Mounts(gameDir, baseMod, "clientarchives.con");
            if (srv.Count == 0) throw new Exception(I18n.T("serverarchives.con des Basis-Mods nicht lesbar.", "Could not read the base mod's serverarchives.con."));

            Dictionary<string, string> obj = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), com = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string gli = null;
            using (Reader rd = new Reader(srv))
            {
                string real;
                // ---- C4
                string c4 = rd.Read("Objects", "Weapons/Handheld/c4_explosives/c4_explosives.tweak", out real);
                if (c4 != null)
                {
                    if (o.C4Mags > 0) c4 = SetInTemplate(c4, "c4_explosives", "ammo.nrOfMags", o.C4Mags.ToString(), "ammo.magSize");
                    if (o.C4Mags > 5) c4 = SetInTemplate(c4, "c4_explosives", "fire.maxProjectilesInWorld", Math.Min(o.C4Mags, 30).ToString(), null);
                    if (o.C4Force > 0) c4 = SetInTemplate(c4, "c4_explosives_Projectile", "detonation.explosionForce", o.C4Force.ToString(), null);
                    obj[real] = c4; log.Add(I18n.T("C4: ", "C4: ") + o.C4Mags + I18n.T(" Stück, Wucht ", " per player, force ") + o.C4Force);
                }
                else log.Add(I18n.T("⚠ C4 nicht gefunden", "⚠ C4 not found"));
                // ---- Sprengsätze: jede Art auf eine eigene, im Kit freie Taste. Zwei Gegenstände auf derselben Taste
                // im selben Kit lassen den Server abstürzen (bf2sf64 belegt 6–8 mit Enterhaken, Nachtsicht, Gasmaske).
                // Neue Waffen-Vorlagen lädt BF2 nicht aus zusätzlichen Dateien, daher vorhandene: C4, Claymore, Panzermine.
                if (o.ExtraExplosives || o.C4ForMore)
                {
                    string mine = rd.Read("Objects", "Weapons/Handheld/at_mine/at_mine.tweak", out real); string mineReal = real;
                    string clay = rd.Read("Objects", "Weapons/Handheld/usmin_claymore/usmin_claymore.tweak", out real); string clayReal = real;
                    Dictionary<string, int> slot = ItemSlots(rd);
                    List<KeyValuePair<string, string>> kitFiles = rd.Find("Objects", new Regex(@"^Kits/.*\.con$", RegexOptions.IgnoreCase));
                    Dictionary<string, List<string>> kitItems = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, string> k in kitFiles) kitItems[k.Key] = KitItems(k.Value);
                    List<string> spec = new List<string>(), withClay = new List<string>(), withMine = new List<string>();
                    foreach (KeyValuePair<string, List<string>> k in kitItems)
                    {
                        if (Has(k.Value, "c4_explosives")) spec.Add(k.Key);
                        if (Has(k.Value, "usmin_claymore")) withClay.Add(k.Key);
                        if (Has(k.Value, "at_mine")) withMine.Add(k.Key);
                    }
                    // Claymore: freie Taste in allen Kits, die sie haben bzw. bekommen
                    int clayKey = -1, mineKey = -1;
                    List<string> clayTarget = new List<string>(withClay); if (o.ExtraExplosives && clay != null) foreach (string s in spec) if (!clayTarget.Contains(s)) clayTarget.Add(s);
                    if (clay != null) clayKey = BestKey(new int[] { 6, 7, 8 }, "usmin_claymore", withClay, o.ExtraExplosives ? spec : new List<string>(), kitItems, slot, -1);
                    if (clayKey < 0 && clay != null) { clayTarget = withClay; }
                    else if (clay != null) slot["usmin_claymore"] = clayKey;
                    List<string> mineTarget = new List<string>(withMine); if (o.ExtraExplosives && mine != null) foreach (string s in spec) if (!mineTarget.Contains(s)) mineTarget.Add(s);
                    if (mine != null) mineKey = BestKey(new int[] { 7, 6, 8 }, "at_mine", withMine, o.ExtraExplosives ? spec : new List<string>(), kitItems, slot, clayKey);
                    if (mineKey < 0 && mine != null) { mineTarget = withMine; }
                    else if (mine != null) slot["at_mine"] = mineKey;
                    if (mine != null)
                    {
                        if (mineKey > 0) mine = SetInTemplate(mine, "at_mine", "itemIndex", mineKey.ToString(), null);
                        if (o.ExtraExplosives)
                        {
                            mine = SetInTemplate(mine, "at_mine_Projectile", "detonation.explosionForce", o.MineForce.ToString(), null);
                            mine = SetInTemplate(mine, "at_mine_Projectile", "detonation.explosionRadius", "12", null);
                            mine = SetInTemplate(mine, "at_mine", "ammo.nrOfMags", "10", "ammo.magSize");
                        }
                        obj[mineReal] = mine;
                    }
                    if (clay != null)
                    {
                        if (clayKey > 0) clay = SetInTemplate(clay, "usmin_claymore", "itemIndex", clayKey.ToString(), null);
                        if (o.ExtraExplosives)
                        {
                            clay = SetInTemplate(clay, "usmin_claymore_Projectile", "detonation.explosionForce", o.ClaymoreForce.ToString(), null);
                            clay = SetInTemplate(clay, "usmin_claymore_Projectile", "detonation.explosionMaterial", C4Material.ToString(), null);
                            clay = SetInTemplate(clay, "usmin_claymore", "ammo.nrOfMags", "10", "ammo.magSize");
                            clay = SetInTemplate(clay, "usmin_claymore", "fire.maxProjectilesInWorld", "10", null);
                        }
                        obj[clayReal] = clay;
                    }
                    int kits = 0, more = 0;
                    foreach (KeyValuePair<string, string> k in kitFiles)
                    {
                        string t = k.Value, nl = Nl(t);
                        List<string> items = kitItems[k.Key];
                        if (spec.Contains(k.Key))
                        {
                            if (!o.ExtraExplosives) continue;
                            string add = "";
                            bool addClay = clayKey > 0 && !Has(items, "usmin_claymore") && UsedKeys(items, slot, "usmin_claymore").IndexOf(clayKey) < 0;
                            if (addClay) add += nl + "ObjectTemplate.addTemplate usmin_claymore";
                            if (mineKey > 0 && !Has(items, "at_mine") && UsedKeys(items, slot, "at_mine").IndexOf(mineKey) < 0 && !(addClay && clayKey == mineKey)) add += nl + "ObjectTemplate.addTemplate at_mine";
                            if (add.Length == 0) continue;
                            obj[k.Key] = AddAfter(t, "c4_explosives", add);
                            kits++;
                        }
                        else if (o.C4ForMore && Regex.IsMatch(Path.GetFileName(k.Key), @"_(engineer|sniper|at)(_special)?\.con$", RegexOptions.IgnoreCase)
                                 && UsedKeys(items, slot, null).IndexOf(5) < 0)
                        {
                            string nt = AddAfter(t, null, nl + "ObjectTemplate.addTemplate c4_explosives");
                            if (nt == t) continue;
                            obj[k.Key] = nt; more++;
                        }
                    }
                    if (o.ExtraExplosives) log.Add(I18n.T("Spec Ops: C4 auf Taste 5", "Spec Ops: C4 on key 5")
                        + (clayKey > 0 ? I18n.T(", Sprung-Claymore auf ", ", jump claymore on ") + clayKey : I18n.T(", Claymore: keine freie Taste", ", claymore: no free key"))
                        + (mineKey > 0 ? I18n.T(", Mega-Mine auf ", ", mega mine on ") + mineKey : I18n.T(", Mine: keine freie Taste", ", mine: no free key")) + " – " + kits + " Kits");
                    if (o.C4ForMore) log.Add(I18n.T("C4 auch für Pionier, Scharfschütze und Panzerabwehr – ", "C4 also for engineer, sniper and anti-tank – ") + more + I18n.T(" Kits (nur wo Taste 5 frei ist)", " kits (only where key 5 is free)"));
                }
                // ---- Panzerfaust: Raketensprung, langsame Raketen, viele Raketen, schnelles Nachladen
                if (o.RocketJump || o.RocketSpeedPct != 100)
                    foreach (string rk in Rockets)
                    {
                        string p = "Weapons/Armament/missiles/" + rk + "/" + rk + ".tweak";
                        string t = rd.Read("Objects", p, out real);
                        if (t == null) continue;
                        if (o.RocketJump)
                        {
                            t = SetInTemplate(t, rk, "detonation.explosionForce", o.RocketForce.ToString(), null);
                            t = SetInTemplate(t, rk, "detonation.explosionMaterial", C4Material.ToString(), null);
                        }
                        if (o.RocketSpeedPct > 0 && o.RocketSpeedPct != 100)
                        {
                            Match ms = Regex.Match(t, @"(?mi)^ObjectTemplate\.maxSpeed\s+([\d.]+)"), ma = Regex.Match(t, @"(?mi)^ObjectTemplate\.acceleration\s+([\d.]+)");
                            double f = o.RocketSpeedPct / 100.0;
                            if (ms.Success) t = SetInTemplate(t, rk, "maxSpeed", F(double.Parse(ms.Groups[1].Value, CultureInfo.InvariantCulture) * f), null);
                            if (ma.Success) t = SetInTemplate(t, rk, "acceleration", F(double.Parse(ma.Groups[1].Value, CultureInfo.InvariantCulture) * f), null);
                            t = SetInTemplate(t, rk, "timeToLive", "CRD_NONE/" + F(10 / f) + "/0/0", null);
                        }
                        obj[real] = t;
                    }
                foreach (string ln in Launchers)
                {
                    string p = "Weapons/Handheld/" + ln + "/" + ln + ".tweak";
                    string t = rd.Read("Objects", p, out real);
                    if (t == null || (o.RocketMags <= 0 && o.RocketReloadMs <= 0 && o.RocketSpeedPct == 100)) continue;
                    if (o.RocketMags > 0) t = SetInTemplate(t, ln, "ammo.nrOfMags", o.RocketMags.ToString(), "ammo.magSize");
                    if (o.RocketReloadMs > 0)
                    {
                        double s = o.RocketReloadMs / 1000.0;
                        t = SetInTemplate(t, ln, "ammo.reloadTime", F(s), null);
                        t = SetInTemplate(t, ln, "ammo.minimumTimeUntilReload", F(Math.Min(s, 0.2)), null);
                    }
                    if (o.RocketSpeedPct > 0 && o.RocketSpeedPct != 100)
                    {
                        Match mv = Regex.Match(t, @"(?mi)^ObjectTemplate\.velocity\s+([\d.]+)");
                        if (mv.Success) t = SetInTemplate(t, ln, "velocity", F(double.Parse(mv.Groups[1].Value, CultureInfo.InvariantCulture) * o.RocketSpeedPct / 100.0), null);
                    }
                    obj[real] = t;
                }
                if (o.RocketJump || o.RocketSpeedPct != 100 || o.RocketMags > 0 || o.RocketReloadMs > 0)
                    log.Add(I18n.T("Panzerfaust: ", "Rocket launcher: ") + (o.RocketJump ? I18n.T("Raketensprung (Wucht ", "rocket jump (force ") + o.RocketForce + "), " : "") + I18n.T("Tempo ", "speed ") + o.RocketSpeedPct + " %, " + o.RocketMags + I18n.T(" Raketen", " rockets") + (o.RocketReloadMs > 0 ? I18n.T(", Nachladen ", ", reload ") + F(o.RocketReloadMs / 1000.0) + " s" : ""));
                // ---- Turbo: Motorkraft der Landfahrzeuge (Autos und Panzer); Höchstdrehzahl steigt halb so stark mit
                if (o.VehiclePowerPct > 0 && o.VehiclePowerPct != 100)
                {
                    double f = o.VehiclePowerPct / 100.0, fr = 1 + (f - 1) / 2; int n = 0;
                    foreach (KeyValuePair<string, string> v in rd.Find("Objects", new Regex(@"^Vehicles/Land/[^/]+/[^/]+\.tweak$", RegexOptions.IgnoreCase)))
                    {
                        string t = v.Value;
                        foreach (Match em in Regex.Matches(v.Value, @"(?mi)^ObjectTemplate\.(?:create|activeSafe)\s+Engine\s+(\S+)\s*$"))
                        {
                            string sec = Section(t, em.Groups[1].Value);
                            if (sec == null || !Regex.IsMatch(sec, @"(?mi)^ObjectTemplate\.setEngineType\s+c_ET(NewCar2|Tank|Car)\b")) continue;
                            Match tq = Regex.Match(sec, @"(?mi)^ObjectTemplate\.setTorque\s+([\d.]+)"), rpm = Regex.Match(sec, @"(?mi)^ObjectTemplate\.newCar2\.maxRpm\s+([\d.]+)");
                            if (tq.Success) t = SetInTemplate(t, em.Groups[1].Value, "setTorque", F(Num(tq.Groups[1].Value) * f), null);
                            if (rpm.Success) t = SetInTemplate(t, em.Groups[1].Value, "newCar2.maxRpm", F(Num(rpm.Groups[1].Value) * fr), null);
                            if (tq.Success) n++;
                        }
                        if (t != v.Value) obj[v.Key] = t;
                    }
                    log.Add(I18n.T("Turbo: ", "Turbo: ") + n + I18n.T(" Fahrzeugmotoren mit ", " vehicle engines at ") + o.VehiclePowerPct + I18n.T(" % Kraft", " % power"));
                }
                // ---- Fast unendlich Munition: alle Handwaffen 99 Magazine (C4 und Panzerfaust behalten ihre eigenen Werte)
                if (o.EndlessAmmo)
                {
                    int n = 0;
                    foreach (KeyValuePair<string, string> w in rd.Find("Objects", new Regex(@"^Weapons/Handheld/[^/]+/[^/]+\.tweak$", RegexOptions.IgnoreCase)))
                    {
                        string t = obj.ContainsKey(w.Key) ? obj[w.Key] : w.Value;
                        Match fa = Regex.Match(t, @"(?mi)^ObjectTemplate\.(?:create|activeSafe)\s+GenericFireArm\s+(\S+)");
                        if (!fa.Success || !Regex.IsMatch(t, @"(?mi)^ObjectTemplate\.createComponent\s+DefaultAmmoComp")) continue;
                        string nm = fa.Groups[1].Value.ToLowerInvariant();
                        if (nm == "c4_explosives" || Array.IndexOf(Launchers, nm) >= 0) continue;
                        if ((o.ExtraExplosives || o.C4ForMore) && (nm == "at_mine" || nm == "usmin_claymore")) continue;
                        Match cur = Regex.Match(t, @"(?mi)^ObjectTemplate\.ammo\.nrOfMags\s+(\d+)");
                        if (cur.Success && int.Parse(cur.Groups[1].Value) >= 99) continue;
                        // Viele Waffen setzen magSize nicht (Standardwert) – dann nach ammoType bzw. reloadTime einfügen
                        string nt = SetInTemplate(t, fa.Groups[1].Value, "ammo.nrOfMags", "99", "ammo.magSize");
                        if (nt == t) nt = SetInTemplate(t, fa.Groups[1].Value, "ammo.nrOfMags", "99", "ammo.ammoType");
                        if (nt == t) nt = SetInTemplate(t, fa.Groups[1].Value, "ammo.nrOfMags", "99", "ammo.reloadTime");
                        if (nt != t) { obj[w.Key] = nt; n++; }
                    }
                    log.Add(I18n.T("Munition: ", "Ammo: ") + n + I18n.T(" Handwaffen mit 99 Magazinen", " hand weapons with 99 magazines"));
                }
                // ---- Explosiv-Sniper: Geschosse der Scharfschützen-Waffen explodieren beim Einschlag und schleudern
                if (o.SniperForce > 0)
                {
                    HashSet<string> sn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, string> k in rd.Find("Objects", new Regex(@"^Kits/.*_sniper\.con$", RegexOptions.IgnoreCase)))
                        foreach (Match m in Regex.Matches(k.Value, @"(?mi)^ObjectTemplate\.addTemplate[ \t]+([^\s]+)")) sn.Add(m.Groups[1].Value);
                    int n = 0;
                    foreach (KeyValuePair<string, string> w in rd.Find("Objects", new Regex(@"^Weapons/Handheld/[^/]+/[^/]+\.tweak$", RegexOptions.IgnoreCase)))
                    {
                        string t = obj.ContainsKey(w.Key) ? obj[w.Key] : w.Value;
                        Match fa = Regex.Match(t, @"(?mi)^ObjectTemplate\.(?:create|activeSafe)\s+GenericFireArm\s+(\S+)");
                        if (!fa.Success || !sn.Contains(fa.Groups[1].Value) || Regex.IsMatch(fa.Groups[1].Value, "claymore|knife|pis|hgr|simrad", RegexOptions.IgnoreCase)) continue;
                        foreach (Match pm in Regex.Matches(t, @"(?mi)^ObjectTemplate\.create\s+GenericProjectile\s+(\S+)\s*$"))
                        {
                            string pn = pm.Groups[1].Value, sec = Section(t, pn);
                            if (sec == null) continue;
                            if (!Regex.IsMatch(sec, @"(?mi)^ObjectTemplate\.createComponent\s+DefaultDetonationComp")) t = AddAfterCreate(t, pn, "ObjectTemplate.createComponent DefaultDetonationComp");
                            string[][] vals = { new[] { "explosionMaterial", C4Material.ToString() }, new[] { "explosionRadius", "4" }, new[] { "explosionForce", o.SniperForce.ToString() }, new[] { "explosionDamage", "50" }, new[] { "endEffectTemplate", "e_exp_grenade" } };
                            string missing = "";
                            foreach (string[] kv in vals)
                            {
                                if (Regex.IsMatch(Section(t, pn), @"(?mi)^ObjectTemplate\.detonation\." + kv[0] + @"\s")) t = SetInTemplate(t, pn, "detonation." + kv[0], kv[1], null);
                                else missing += (missing.Length > 0 ? Nl(t) : "") + "ObjectTemplate.detonation." + kv[0] + " " + kv[1];
                            }
                            if (missing.Length > 0) t = AddAfterCreate(t, pn, missing, "ObjectTemplate.createComponent DefaultDetonationComp");
                            n++;
                        }
                        obj[w.Key] = t;
                    }
                    log.Add(I18n.T("Explosiv-Sniper: ", "Explosive sniper: ") + n + I18n.T(" Geschosse, Wucht ", " projectiles, force ") + o.SniperForce);
                }
                // ---- Granaten-Sprung: Handgranaten schleudern statt zu töten
                if (o.GrenadeForce > 0)
                {
                    int n = 0;
                    foreach (KeyValuePair<string, string> w in rd.Find("Objects", new Regex(@"^Weapons/Handheld/[^/]*hgr[^/]*/[^/]+\.tweak$", RegexOptions.IgnoreCase)))
                    {
                        if (Regex.IsMatch(w.Key, "smoke|flash", RegexOptions.IgnoreCase)) continue;
                        string t = obj.ContainsKey(w.Key) ? obj[w.Key] : w.Value;
                        foreach (Match pm in Regex.Matches(t, @"(?mi)^ObjectTemplate\.create\s+GenericProjectile\s+(\S+)\s*$"))
                        {
                            t = SetInTemplate(t, pm.Groups[1].Value, "detonation.explosionForce", o.GrenadeForce.ToString(), null);
                            t = SetInTemplate(t, pm.Groups[1].Value, "detonation.explosionMaterial", C4Material.ToString(), null);
                            n++;
                        }
                        obj[w.Key] = t;
                    }
                    log.Add(I18n.T("Granaten-Sprung: ", "Grenade jump: ") + n + I18n.T(" Granaten, Wucht ", " grenades, force ") + o.GrenadeForce);
                }
                // ---- Sprinten ohne Ausdauer
                if (o.EndlessSprint)
                {
                    int n = 0;
                    foreach (KeyValuePair<string, string> s in rd.Find("Objects", new Regex(@"^soldiers/[^/]+/[^/]+\.tweak$", RegexOptions.IgnoreCase)))
                    {
                        if (!Regex.IsMatch(s.Value, @"(?mi)^ObjectTemplate\.SprintDissipationTime")) continue;
                        string t = SetValue(s.Value, "SprintDissipationTime", "9999", null);
                        t = SetValue(t, "SprintLossAtJump", "0", null);
                        obj[s.Key] = t; n++;
                    }
                    foreach (KeyValuePair<string, string> k in rd.Find("Objects", new Regex(@"^Kits/.*\.con$", RegexOptions.IgnoreCase)))
                    {
                        string t = obj.ContainsKey(k.Key) ? obj[k.Key] : k.Value;
                        if (!Regex.IsMatch(t, @"(?mi)^ObjectTemplate\.sprintStaminaDissipationFactor")) continue;
                        obj[k.Key] = SetValue(t, "sprintStaminaDissipationFactor", "0", null);
                    }
                    log.Add(I18n.T("Sprinten ohne Ausdauer (", "Sprint without stamina (") + n + I18n.T(" Soldaten)", " soldiers)"));
                }
                // ---- Lebenspunkte der Soldaten
                if (o.HealthPct > 0 && o.HealthPct != 100)
                {
                    int n = 0;
                    foreach (KeyValuePair<string, string> s in rd.Find("Objects", new Regex(@"^soldiers/[^/]+/[^/]+\.tweak$", RegexOptions.IgnoreCase)))
                    {
                        string t = obj.ContainsKey(s.Key) ? obj[s.Key] : s.Value;
                        Match mh = Regex.Match(t, @"(?mi)^ObjectTemplate\.armor\.maxHitPoints\s+([\d.]+)");
                        if (!mh.Success) continue;
                        string hp = F(Math.Round(Num(mh.Groups[1].Value) * o.HealthPct / 100.0));
                        t = SetValue(t, "armor.maxHitPoints", hp, null);
                        t = SetValue(t, "armor.hitPoints", hp, null);
                        obj[s.Key] = t; n++;
                    }
                    log.Add(I18n.T("Lebenspunkte: ", "Health: ") + o.HealthPct + " % (" + n + I18n.T(" Soldaten)", " soldiers)"));
                }
                // ---- Wasser tötet: Soldaten verlieren sofort alle Lebenspunkte, sobald sie Wasser berühren
                if (o.WaterKills)
                {
                    int n = 0;
                    foreach (KeyValuePair<string, string> s in rd.Find("Objects", new Regex(@"^soldiers/[^/]+/[^/]+\.tweak$", RegexOptions.IgnoreCase)))
                    {
                        string t = obj.ContainsKey(s.Key) ? obj[s.Key] : s.Value;
                        if (!Regex.IsMatch(t, @"(?mi)^ObjectTemplate\.armor\.maxHitPoints")) continue;
                        t = SetValue(t, "armor.hpLostWhileInWater", "1000", "armor.maxHitPoints");
                        t = SetValue(t, "armor.hpLostWhileInDeepWater", "1000", "armor.maxHitPoints");
                        t = SetValue(t, "armor.waterDamageDelay", "0", "armor.maxHitPoints");
                        obj[s.Key] = t; n++;
                    }
                    log.Add(I18n.T("Wasser tötet (", "Water kills (") + n + I18n.T(" Soldaten)", " soldiers)"));
                }
                // ---- Materialtabelle: Fallschaden und Explosionsschaden an Soldaten
                if (o.NoFallDamage || o.BlastSoldierPct < 100)
                {
                    string mm = rd.Read("Common", "Material/materialManagerSettings.con", out real), mdReal;
                    string md = rd.Read("Common", "Material/materialManagerDefine.con", out mdReal);
                    if (mm != null && md != null) { int nf, nb; com[real] = PatchMaterials(mm, md, o, out nf, out nb); log.Add(I18n.T("Material: ", "Material: ") + nf + I18n.T(" Fallschaden-Einträge auf 0, ", " fall damage entries set to 0, ") + nb + I18n.T(" Spreng-Einträge auf ", " blast entries set to ") + o.BlastSoldierPct + " %"); }
                    else log.Add(I18n.T("⚠ Materialtabelle nicht gefunden", "⚠ Material table not found"));
                }
            }

            // ---- Ordner neu anlegen (nur einen vorhandenen Spaß-Aufsatz ersetzen)
            if (Directory.Exists(dst))
            {
                if (FunMods.BaseOf(gameDir, name) == null) throw new Exception(I18n.T("„", "“") + name + I18n.T("“ ist kein Spaß-Aufsatz (fun.recipe fehlt) – wird nicht überschrieben.", "” is not a fun pack (fun.recipe missing) – not overwritten."));
                Remove(gameDir, name);
            }
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src))
            {
                string fn = Path.GetFileName(f), ext = Path.GetExtension(f).ToLowerInvariant();
                if ((ext == ".con" || ext == ".desc" || ext == ".mep") && !fn.EndsWith("archives.con", StringComparison.OrdinalIgnoreCase)) File.Copy(f, Path.Combine(dst, fn));
            }
            string desc = Path.Combine(dst, "mod.desc");
            if (File.Exists(desc)) File.WriteAllText(desc, Regex.Replace(File.ReadAllText(desc, Enc), @"<title>\s*(.*?)\s*</title>", "<title> $1 Fun </title>"), Enc);
            // Schwerkraft: in GameLogicInit.con, die Server und Spieler laden – sonst ruckelt die Bewegung
            if (o.GravityPct > 0 && o.GravityPct != 100)
            {
                string g = null; foreach (string f in Directory.GetFiles(dst)) if (Path.GetFileName(f).Equals("GameLogicInit.con", StringComparison.OrdinalIgnoreCase)) g = f;
                if (g == null) g = Path.Combine(dst, "GameLogicInit.con");
                gli = (File.Exists(g) ? File.ReadAllText(g, Enc) : "");
                gli = gli.TrimEnd() + "\r\n\r\nrem Spass-Aufsatz (BF2 Manager)\r\nphysics.gravity " + F(-14.73 * o.GravityPct / 100.0) + "\r\n";
                File.WriteAllText(g, gli, Enc);
                log.Add(I18n.T("Schwerkraft: ", "Gravity: ") + o.GravityPct + " %");
            }
            WriteZip(Path.Combine(dst, ObjectsZip), obj);
            WriteZip(Path.Combine(dst, CommonZip), com);
            File.WriteAllText(Path.Combine(dst, "serverarchives.con"), Archives(srv), Enc);
            File.WriteAllText(Path.Combine(dst, "clientarchives.con"), Archives(cli.Count > 0 ? cli : srv), Enc);
            StringBuilder rc = new StringBuilder("# Spass-Aufsatz (BF2 Manager)\r\nBase=" + baseMod + "\r\n");
            foreach (KeyValuePair<string, string> kv in o.ToDict()) rc.Append(kv.Key).Append('=').Append(kv.Value).Append("\r\n");
            File.WriteAllText(Path.Combine(dst, FunMods.RecipeFile), rc.ToString(), Enc);
            FunMods.Link(gameDir, name);
            ModPrints.Invalidate(name);
            return log;
        }

        static string Archives(List<Mount> mounts)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("fileManager.mountArchive ").Append(ObjectsZip).Append(" Objects\r\n");
            sb.Append("fileManager.mountArchive ").Append(CommonZip).Append(" Common\r\n\r\n");
            foreach (Mount m in mounts) sb.Append("fileManager.mountArchive ").Append(m.Rel).Append(' ').Append(m.Root).Append("\r\n");
            return sb.ToString();
        }

        static void WriteZip(string path, Dictionary<string, string> files)
        {
            if (File.Exists(path)) File.Delete(path);
            using (ZipArchive z = ZipFile.Open(path, ZipArchiveMode.Create))
                foreach (KeyValuePair<string, string> kv in files)
                    using (StreamWriter w = new StreamWriter(z.CreateEntry(kv.Key).Open(), Enc)) w.Write(kv.Value);
        }

        // Soldat gegen Boden/Gebäude (Typ 0/1) = Fallschaden; Soldat gegen C4-Explosion = Sprengschaden
        public static string PatchMaterials(string settings, string define, FunOptions o, out int nFall, out int nBlast)
        {
            Dictionary<int, int> type = new Dictionary<int, int>(); int id = -1;
            foreach (string l in define.Replace("\r", "").Split('\n'))
            {
                Match a = Regex.Match(l, @"^Material\.active\s+(\d+)"), t = Regex.Match(l, @"^Material\.type\s+(\d+)");
                if (a.Success) id = int.Parse(a.Groups[1].Value);
                else if (t.Success && id >= 0) type[id] = int.Parse(t.Groups[1].Value);
            }
            string[] lines = settings.Replace("\r", "").Split('\n');
            int ca = -1, cb = -1; nFall = 0; nBlast = 0;
            string blast = F(o.BlastSoldierPct / 100.0);
            for (int i = 0; i < lines.Length; i++)
            {
                Match c = Regex.Match(lines[i], @"^MaterialManager\.createCell\s+(\d+)\s+(\d+)");
                if (c.Success) { ca = int.Parse(c.Groups[1].Value); cb = int.Parse(c.Groups[2].Value); continue; }
                if (ca < 0 || !Regex.IsMatch(lines[i], @"^MaterialManager\.damageMod\s+\S+")) continue;
                bool sa = Array.IndexOf(SoldierMaterials, ca) >= 0, sb = Array.IndexOf(SoldierMaterials, cb) >= 0;
                if (!sa && !sb) continue;
                int other = sb ? ca : cb, ot;
                if (o.NoFallDamage && type.TryGetValue(other, out ot) && (ot == 0 || ot == 1)) { lines[i] = "MaterialManager.damageMod 0"; nFall++; }
                else if (o.BlastSoldierPct < 100 && other == C4Material) { lines[i] = "MaterialManager.damageMod " + blast; nBlast++; }
            }
            return string.Join("\r\n", lines);
        }

        // Entfernt einen Spaß-Aufsatz: erst die Verknüpfungen (ohne den Basis-Mod anzufassen), dann die eigenen Dateien
        public static void Remove(string gameDir, string name)
        {
            string dst = Path.Combine(Path.Combine(gameDir, "mods"), name);
            if (!Directory.Exists(dst)) return;
            if (FunMods.BaseOf(gameDir, name) == null) throw new Exception(I18n.T("Kein Spaß-Aufsatz: ", "Not a fun pack: ") + name);
            foreach (string d in Directory.GetDirectories(dst))
                if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) Directory.Delete(d, false);
            Directory.Delete(dst, true);
        }
    }
}
