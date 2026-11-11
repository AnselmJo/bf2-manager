// Tests.cs - automatische Tests ohne Test-Framework (läuft in CI unter Windows und lokal mit Mono). C# 5.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BF2Coop.Tests
{
    public static class Runner
    {
        static int _pass, _fail;
        static string _tmp;

        static void Check(bool ok, string name)
        {
            if (ok) { _pass++; Console.WriteLine("PASS  " + name); }
            else { _fail++; Console.WriteLine("FAIL  " + name); }
        }

        static void Run(string name, Action a)
        {
            try { a(); }
            catch (Exception ex) { _fail++; Console.WriteLine("FAIL  " + name + " -> " + ex.GetType().Name + ": " + ex.Message); }
        }

        public static int Main()
        {
            _tmp = Path.Combine(Path.GetTempPath(), "bf2sm_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmp);
            I18n.En = true;
            Run("settings", Settings);
            Run("planner", Planner_);
            Run("cooptxt", CoopTxtRoundtrip);
            Run("versions", Versions);
            Run("hash", Hash);
            Run("laa", Laa);
            Run("unlock", Unlock);
            Run("lan", LanTests);
            Run("fun", FunTests);
            Run("leaderboard", Board);
            Run("profiles", ProfileFiles);
            Run("profilemanage", ProfileManage);
            Run("radio", Radio);
            Run("gamefolder", GameFolder);
            Run("moddesc", ModDesc);
            Run("commands", Cmds);
            Run("scrub", Scrub);
            Run("serverargs", ServerArgs);
            Run("notes", Notes);
            Run("arm", Arm);
            Run("coopmaps", CoopMaps);
            try { Directory.Delete(_tmp, true); } catch { }
            Console.WriteLine();
            Console.WriteLine(_pass + " passed, " + _fail + " failed");
            return _fail == 0 ? 0 : 1;
        }

        static void MakeMap(string levels, string name, string desc, params string[] entries)
        {
            string dir = Path.Combine(levels, name);
            Directory.CreateDirectory(Path.Combine(dir, "info"));
            if (desc != null) File.WriteAllText(Path.Combine(dir, "info", name + ".desc"), desc);
            using (System.IO.Compression.ZipArchive z = System.IO.Compression.ZipFile.Open(Path.Combine(dir, "server.zip"), System.IO.Compression.ZipArchiveMode.Create))
                foreach (string e in entries) using (StreamWriter w = new StreamWriter(z.CreateEntry(e).Open())) w.Write("x");
        }

        static void CoopMaps()
        {
            string game = Path.Combine(_tmp, "mapgame"), lv = Path.Combine(game, "mods", "bfp2", "levels");
            string coopDesc = "<map><modes><mode type=\"gpm_cq\"></mode><mode type=\"gpm_coop\"><maptype ai=\"1\" players=\"16\"/></mode></modes></map>";
            string cqDesc = "<map><modes><mode type=\"gpm_cq\"></mode><mode type=\"sp2\"></mode></modes></map>";
            // wie black_beards_atol: Bot-Daten + Coop in der Beschreibung -> spielbar
            MakeMap(lv, "good", coopDesc, "AIPathFinding/Infantry.qtr", "GameModes/gpm_coop/16/AI/StrategicAreas.ai", "GameModes/gpm_coop/16/GamePlayObjects.con", "GameModes/gpm_cq/16/GamePlayObjects.con");
            // wie wake_island_1707: Coop-Ordner ohne Wegnetz
            MakeMap(lv, "nopath", coopDesc, "GameModes/gpm_coop/16/GamePlayObjects.con", "GameModes/gpm_cq/16/GamePlayObjects.con");
            // wie storm_the_bastion: Bot-Daten vorhanden, Beschreibung ohne Coop -> Server lädt die Map nicht
            MakeMap(lv, "nodesc", cqDesc, "AIPathFinding/Infantry.qtr", "GameModes/gpm_coop/16/AI/StrategicAreas.ai", "GameModes/gpm_cq/16/GamePlayObjects.con");
            // Coop 32 ohne StrategicAreas, Coop 16 vollständig
            MakeMap(lv, "partial", coopDesc, "AIPathFinding/Infantry.qtr", "GameModes/gpm_coop/16/AI/StrategicAreas.ai", "GameModes/gpm_coop/32/GamePlayObjects.con");
            List<AvailMap> maps = MapScanner.Scan(game, "bfp2", Path.Combine(_tmp, "mapcache"));
            Predicate<string> has = delegate (string key) { return maps.Exists(delegate (AvailMap a) { return a.Key == key; }); };
            Check(has("good|gpm_coop|16"), "coop maps: complete map is offered");
            Check(!has("nopath|gpm_coop|16") && has("nopath|gpm_cq|16"), "coop maps: no bot path -> no coop, conquest still offered");
            Check(!has("nodesc|gpm_coop|16"), "coop maps: description without gpm_coop -> not offered");
            Check(has("partial|gpm_coop|16") && !has("partial|gpm_coop|32"), "coop maps: only sizes with StrategicAreas");

            // gpm_coop.py fehlt (wie bfp2): aus gpm_cq.py anlegen, nie überschreiben
            string gm = Path.Combine(game, "mods", "bfp2", "python", "game", "gamemodes");
            Directory.CreateDirectory(gm);
            File.WriteAllText(Path.Combine(gm, "gpm_cq.py"), "# conquest\r\nif g_debug: print \"gpm_cq.py initialized\"\r\ndef init():\r\n\tpass\r\n");
            Check(CoopScript.Missing(game, "bfp2"), "coop script: missing detected");
            Check(CoopScript.Create(game, "bfp2"), "coop script: created");
            string coop = File.ReadAllText(Path.Combine(gm, "gpm_coop.py"));
            Check(coop.Contains("def init():") && coop.Contains("gpm_coop.py initialized") && !coop.Contains("gpm_cq.py initialized"), "coop script: same logic as gpm_cq.py");
            File.WriteAllText(Path.Combine(gm, "gpm_coop.py"), "own");
            Check(!CoopScript.Create(game, "bfp2") && File.ReadAllText(Path.Combine(gm, "gpm_coop.py")) == "own", "coop script: existing file never overwritten");
        }

        static void Arm()
        {
            Check(ArmSupport.WithLayer("~ HIGHDPIAWARE", true) == "~ HIGHDPIAWARE ARM64CHPEDISABLED", "arm layer: added to existing entry");
            Check(ArmSupport.WithLayer(null, true) == "~ ARM64CHPEDISABLED", "arm layer: new entry");
            Check(ArmSupport.WithLayer("~ HIGHDPIAWARE ARM64CHPEDISABLED", false) == "~ HIGHDPIAWARE" && ArmSupport.WithLayer("~ ARM64CHPEDISABLED", false) == null, "arm layer: removed, others kept");
            Check(ArmSupport.WithLayer("~ ARM64CHPEDISABLED", true) == "~ ARM64CHPEDISABLED", "arm layer: not duplicated");
            Check(ArmSupport.HasLayer("~ HIGHDPIAWARE ARM64SAFEEMULATION") && !ArmSupport.HasLayer("~ HIGHDPIAWARE") && !ArmSupport.HasLayer(null), "arm layer: safe emulation also counts");

            // CPU-Patch an einer künstlichen DLL mit der echten Code-Signatur aus RendDX9.dll
            byte[] sig = { 0xFF, 0xD6, 0x83, 0xC4, 0x0C, 0x85, 0xC0, 0x75, 0x5D, 0x8B, 0x45, 0xF8, 0x25, 0xFF, 0x00, 0x00, 0x00, 0x83, 0xF8, 0x01, 0x74, 0x43 };
            byte[] dll = new byte[4096]; dll[0] = (byte)'M'; dll[1] = (byte)'Z'; Array.Copy(sig, 0, dll, 2000, sig.Length);
            string f = Path.Combine(_tmp, "RendDX9.dll"); File.WriteAllBytes(f, dll);
            Check(ArmSupport.CpuPatchState(f) == PatchState.Off, "cpu patch: original detected");
            ArmSupport.SetCpuPatch(f, true);
            byte[] p = File.ReadAllBytes(f);
            Check(p[2007] == 0xEB && p[2008] == 0x43 && ArmSupport.CpuPatchState(f) == PatchState.On, "cpu patch: jnz 5D -> jmp 43");
            Check(File.Exists(f + ".vor_cpu.bak") && File.ReadAllBytes(f + ".vor_cpu.bak")[2007] == 0x75, "cpu patch: backup of the original");
            Check(ArmSupport.SetCpuPatch(f, true).Contains("already") , "cpu patch: second run changes nothing");
            ArmSupport.SetCpuPatch(f, false);
            Check(ArmSupport.CpuPatchState(f) == PatchState.Off, "cpu patch: removed again");
            string other = Path.Combine(_tmp, "Other.dll"); File.WriteAllBytes(other, new byte[512]);
            Check(ArmSupport.CpuPatchState(other) == PatchState.Unknown, "cpu patch: unknown file version detected");
            bool threw = false; try { ArmSupport.SetCpuPatch(other, true); } catch (InvalidOperationException) { threw = true; }
            Check(threw && File.ReadAllBytes(other).Length == 512, "cpu patch: unknown file left unchanged");

            // Emulationsschalter in einem eigenen Test-Zweig der Registry (HKCU\Software\BF2ManagerTests)
            string exe = @"C:\Games\BF2\BF2.exe";
            using (Microsoft.Win32.RegistryKey root = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\BF2ManagerTests"))
            {
                try
                {
                    ArmSupport.SetLayer(root, exe, true);
                    Check(ArmSupport.LayerValue(root, exe) == "~ ARM64CHPEDISABLED", "arm layer: written to the registry");
                    ArmSupport.SetLayer(root, exe, false);
                    Check(ArmSupport.LayerValue(root, exe) == null, "arm layer: entry removed when empty");
                }
                finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\BF2ManagerTests", false); }
            }
        }

        static void Notes()
        {
            // Anfang des echten Release-Texts von v1.2.3 (vom Release-Workflow erzeugt)
            string md = "Download **BF2Manager.exe** under *Assets* below. Start it (SmartScreen: *More info › Run anyway*). Already installed? The manager offers the update itself.\r\n\r\n---\r\n\r\n" +
                "Bug fixes and an easier download.\r\n\r\n### Fixed\r\n- **Console results were invisible:** commands (e.g. `admin.listPlayers`) ran.\r\n" +
                "- *Keep vehicles off* is marked *Conquest only*.\r\n  - nested item\r\n\r\n### New\r\n- **Link** to the [latest release](https://github.com/AnselmJo/bf2-manager/releases) at the top.\r\n";
            List<NoteLine> l = ReleaseNotes.Parse(md);
            Check(l.Count > 0 && l[0].Kind == NoteKind.Para && l[0].PlainText == "Bug fixes and an easier download.", "notes: GitHub download hint removed");
            NoteLine h = l.Find(delegate (NoteLine n) { return n.Kind == NoteKind.Heading; });
            Check(h != null && h.Level == 3 && h.PlainText == "Fixed", "notes: ### heading without hashes");
            NoteLine b = l.Find(delegate (NoteLine n) { return n.Kind == NoteKind.Bullet; });
            Check(b != null && b.Runs[0].Bold && b.Runs[0].Text == "Console results were invisible:", "notes: bullet with bold start");
            Check(b.Runs.Exists(delegate (NoteRun r) { return r.Code && r.Text == "admin.listPlayers"; }), "notes: inline code");
            string plain = ReleaseNotes.ToPlain(md);
            Check(plain.IndexOf('*') < 0 && plain.IndexOf('`') < 0 && plain.IndexOf("###") < 0, "notes: no markdown characters left (" + plain.Length + " chars)");
            Check(plain.Contains("latest release") && !plain.Contains("https://"), "notes: link shows its text only");
            Check(l.Exists(delegate (NoteLine n) { return n.Kind == NoteKind.Bullet && n.Level == 1 && n.PlainText == "nested item"; }), "notes: nested bullet");
            Check(l.Exists(delegate (NoteLine n) { return n.Kind == NoteKind.Bullet && n.Runs.Exists(delegate (NoteRun r) { return r.Italic && r.Text == "Conquest only"; }); }), "notes: italic");
            Check(ReleaseNotes.StripDownloadHint("Intro\n\n---\n\nRest") == "Intro\n\n---\n\nRest", "notes: other text before a rule is kept");
            Check(ReleaseNotes.Inline("3 * 4 = 12")[0].Text == "3 * 4 = 12", "notes: lone asterisk is not italic");
        }

        static void ServerArgs()
        {
            string a = Srv.Args("bf2sf64", true, @"C:\Users\aj\Documents\Battlefield 2\ServerConfigs\coop\");
            Check(a.Contains("+config \"C:/Users/aj/Documents/Battlefield 2/ServerConfigs/coop/serverSettings.con\""), "server args: absolute config path with forward slashes");
            Check(a.Contains("+mapList \"C:/Users/aj/Documents/Battlefield 2/ServerConfigs/coop/maplist.con\""), "server args: absolute maplist path");
            Check(!a.Contains("@HOME@"), "server args: no @HOME@ (BF2 may resolve it to another Documents folder)");
            Check(a.StartsWith("+ai 1 +gameMode gamemode ") && a.Contains("+modPath mods/bf2sf64"), "server args: coop flags and mod");
            Check(!Srv.Args("bf2", false, "D:/cfg/conquest").Contains("+ai 1"), "server args: conquest without AI flags");

            // Ende des Servers einordnen (gemessen: fehlende Konfiguration -> Code 0 nach ~32 s; Assertion -> Code 42)
            Check(Srv.ExitIsConfigProblem(0, 32), "server exit: code 0 after 32 s is a config problem (no restart)");
            Check(!Srv.ExitIsConfigProblem(0, 3600), "server exit: code 0 after an hour is a normal shutdown");
            Check(!Srv.ExitIsConfigProblem(42, 10), "server exit: code 42 is a crash, not a config problem");
            Check(Srv.ExitReason(0, 32).Contains("32 s") && Srv.ExitReason(42, 10).Contains("42") && Srv.ExitReason(-1073741819, 5).Contains("-1073741819"), "server exit: reasons mention time or code");

            // Dokumentenordner hat sich geändert: Server-Konfigurationen werden übernommen, vorhandene nicht überschrieben
            string oldDocs = Path.Combine(_tmp, "docs_old"), newDocs = Path.Combine(_tmp, "docs_new");
            string oldCfg = Path.Combine(oldDocs, "Battlefield 2", "ServerConfigs", "coop"), newCfg = Path.Combine(newDocs, "Battlefield 2", "ServerConfigs", "coop");
            Directory.CreateDirectory(oldCfg); Directory.CreateDirectory(newCfg);
            File.WriteAllText(Path.Combine(oldCfg, "maplist.con"), "old maplist");
            File.WriteAllText(Path.Combine(oldCfg, "serverSettings.con"), "old settings");
            File.WriteAllText(Path.Combine(newCfg, "serverSettings.con"), "new settings");
            string r = AppPaths.SyncDocs(oldDocs, newDocs);
            Check(r == newDocs, "docs sync: switches to the current Documents folder");
            Check(File.ReadAllText(Path.Combine(newCfg, "maplist.con")) == "old maplist", "docs sync: missing server config copied over");
            Check(File.ReadAllText(Path.Combine(newCfg, "serverSettings.con")) == "new settings", "docs sync: existing file not overwritten");
            Check(AppPaths.SyncDocs(newDocs, newDocs) == newDocs && AppPaths.SyncDocs(null, newDocs) == newDocs, "docs sync: unchanged and first start");
            Check(AppPaths.SyncDocs(oldDocs, Path.Combine(_tmp, "missing")) == oldDocs, "docs sync: keeps stored folder if the current one does not exist");
        }

        static void Settings()
        {
            Check(Defs.All.Count > 60, "settings table loaded (" + Defs.All.Count + ")");
            bool groups = true; foreach (SettingDef d in Defs.All) if (Defs.GroupOf(d.Group) == null) groups = false;
            Check(groups, "every setting has a group");
            SettingDef t = Defs.Get("sv.timeLimit");
            Check(Defs.ToCon(t, "30") == "1800" && Defs.FromCon(t, "1800") == "30", "minutes <-> seconds");
            SettingDef ff = Defs.Get("sv.soldierFriendlyFire");
            Check(Defs.ToCon(ff, "1") == "100" && Defs.FromCon(ff, "50") == "1", "friendly fire percent");
        }

        static void Planner_()
        {
            BotOpt o = BotOpt.Make(24, 30);
            BotPlan p = Planner.Plan(o, 6, 4);
            Check(p.Count == 24 && p.Total == 34, "fixed count: 24 bots + 10 humans = 34");
            Check(p.TotalMEC > p.TotalUS, "enemy advantage gives MEC more players (US " + p.TotalUS + " / MEC " + p.TotalMEC + ")");
            BotOpt f = BotOpt.Make(8, 30); f.Mode = "fill"; f.Target = 32;
            BotPlan q = Planner.Plan(f, 8, 0);
            Check(q.Count == 24, "fill up to 32 with 8 humans = 24 bots");
            BotPlan c = Planner.Plan(BotOpt.Make(40, 30), 1, 0);
            Check(c.Count == 32 && c.Requested == 40 && c.Capped, "engine limit: 40 bots requested -> 32");
            BotOpt cr = BotOpt.Make(40, 30); cr.DistKind = "ratio"; cr.DistVal = 75;
            BotPlan cp = Planner.Plan(cr, 0, 0);
            Check(cp.BotsMEC == 24 && cp.BotsUS == 8, "engine limit: ratio applies to the 32 bots that spawn (8 US / 24 MEC, as measured)");
            Check(new CoopState().Defaults[64].Count <= Planner.EngineMaxBots, "64-player default within the engine limit");

            // aiDefault.ai mit overrideMenuSettings 1 (wie bf2sf64/bfp2) wird abgeschaltet, mit Sicherung
            string game = Path.Combine(_tmp, "aigame"), ai = Path.Combine(game, "mods", "bf2sf64", "ai");
            Directory.CreateDirectory(ai);
            File.WriteAllText(Path.Combine(ai, "aidefault.ai"), "aiSettings.setNSides 2\r\naiSettings.overrideMenuSettings 1\r\naiSettings.setMaxNBots 31\r\naiSettings.setBotSkill 0.5\r\n");
            Check(AiFile.Load(game, "bf2sf64").OverridesManager, "ai override detected");
            Check(AiFile.DisableOverride(game, "bf2sf64", Path.Combine(_tmp, "aidocs")), "ai override disabled");
            AiFile after = AiFile.Load(game, "bf2sf64");
            Check(!after.OverridesManager && after.Override == 0 && after.MaxBots == 31 && after.Skill == "0.5", "ai override off, other values kept");
            Check(!AiFile.DisableOverride(game, "bf2sf64", Path.Combine(_tmp, "aidocs")), "ai override: second run changes nothing");
            Check(Directory.Exists(Path.Combine(_tmp, "aidocs", "BF2_Backups")), "ai override: backup created");
        }

        static void CoopTxtRoundtrip()
        {
            CoopState st = new CoopState();
            st.HumansUS = 5; st.HumansMEC = 2; st.Commander = false; st.Adaptive = true; st.AdaptStep = 7; st.AdaptMax = 21;
            st.Maps.Add(MapEntry.Make("strike_at_karkand", "gpm_coop", 32));
            MapEntry m = MapEntry.Make("gulf_of_oman", "gpm_coop", 64); m.Opt = BotOpt.Make(40, 60); st.Maps.Add(m);
            string f = Path.Combine(_tmp, "coop_server.txt");
            CoopTxt.Write(f, st);
            CoopState r = CoopTxt.Read(f);
            Check(r.HumansUS == 5 && r.HumansMEC == 2 && !r.Commander, "coop_server.txt humans/commander");
            Check(r.Adaptive && r.AdaptStep == 7 && r.AdaptMax == 21, "coop_server.txt adaptive");
            Check(r.Maps.Count == 2 && r.Maps[1].Opt != null && r.Maps[1].Opt.Count == 40 && r.Maps[1].Opt.Diff == 60, "coop_server.txt maps with override");
        }

        static void Versions()
        {
            Check(Updater.Compare("1.2.0", "1.1.1") > 0, "1.2.0 > 1.1.1");
            Check(Updater.Compare("v1.2.0", "1.2.0") == 0, "v prefix ignored");
            Check(Updater.Compare("1.2.0-beta.1", "1.2.0") < 0, "beta < release");
            Check(Updater.Compare("1.2.0-beta.2", "1.2.0-beta.10") < 0, "beta.2 < beta.10");
            Check(Updater.Compare("1.10.0", "1.9.9") > 0, "numeric not lexical");
        }

        static void Hash()
        {
            string h = new string('a', 64);
            Check(Updater.ExpectedHash(h + "  BF2Manager.exe\n") == h, "SHA256SUMS line");
            Check(Updater.ExpectedHash("x  other.exe\n") == null, "SHA256SUMS other file ignored");
            string f = Path.Combine(_tmp, "abc.txt"); File.WriteAllText(f, "abc", Encoding.ASCII);
            Check(Updater.Sha256(f) == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "sha256 of 'abc'");
        }

        static string FakeExe(string name, byte[] extra)
        {
            byte[] d = new byte[0x200];
            d[0] = (byte)'M'; d[1] = (byte)'Z';
            BitConverter.GetBytes(0x80).CopyTo(d, 0x3C);
            d[0x80] = (byte)'P'; d[0x81] = (byte)'E';
            BitConverter.GetBytes((ushort)0x0102).CopyTo(d, 0x80 + 22);
            if (extra != null) extra.CopyTo(d, 0x100);
            string f = Path.Combine(_tmp, name);
            File.WriteAllBytes(f, d);
            return f;
        }

        static void Laa()
        {
            string f = FakeExe("laa.exe", null);
            Check(Pe.LaaState(f) == PatchState.Off, "LAA off initially");
            Pe.SetLaa(f, true);
            Check(Pe.LaaState(f) == PatchState.On, "LAA on after patch");
            Check(File.Exists(f + ".vor_4gb.bak"), "LAA backup created");
            Pe.SetLaa(f, false);
            Check(Pe.LaaState(f) == PatchState.Off, "LAA off after undo");
        }

        static void Unlock()
        {
            byte[] pat = { 0x8B, 0x75, 0x10, 0x39, 0x70, 0x10, 0x7C, 0x14, 0x8B, 0xCB, 0xE8 };
            string f = FakeExe("BF2.exe", pat);
            Check(ClientUnlock.State(f) == PatchState.Off, "unlock off initially");
            ClientUnlock.Apply(f, true);
            Check(ClientUnlock.State(f) == PatchState.On, "unlock on");
            Pe.SetLaa(f, true);
            ClientUnlock.Apply(f, false);
            Check(ClientUnlock.State(f) == PatchState.Off && Pe.LaaState(f) == PatchState.On, "unlock undo keeps 4 GB patch");
        }

        static void LanTests()
        {
            Check(Lan.Parse(Lan.ProbeMagic + "abc123", System.Net.IPAddress.Loopback) == null, "echo probe is not a server");
            string src = Path.Combine(_tmp, "copy_src.bin"), dst = Path.Combine(_tmp, "copy_dst.bin");
            File.WriteAllText(src, "x"); List<string> clog = new List<string>();
            List<Dictionary<string, object>> ct = new List<Dictionary<string, object>>(); ct.Add(Tasks.Copy(src, dst));
            Check(Elevation.Execute(ct, clog) && File.ReadAllText(dst) == "x", "copy task");
            List<FileEntry> loc = new List<FileEntry>(), rem = new List<FileEntry>();
            loc.Add(E("a.txt", 1, "11")); loc.Add(E("b.txt", 2, "22")); loc.Add(E("only_local.txt", 3, "33"));
            rem.Add(E("a.txt", 1, "11")); rem.Add(E("b.txt", 2, "99")); rem.Add(E("levels\\x\\client.zip", 5, "55"));
            SyncPlan p = Lan.Diff(loc, rem);
            Check(p.Missing.Count == 1 && p.Different.Count == 1 && p.Extra.Count == 1 && p.Bytes == 7, "manifest diff");
            Check(Lan.SafePath(_tmp, "..\\evil.txt") == null && Lan.SafePath(_tmp, "C:\\x") == null && Lan.SafePath(_tmp, "levels\\a.zip") != null, "safe paths");
            Check(Lan.Excluded("settings\\serversettings.con") && Lan.Excluded("python\\game\\coopbots.py") && !Lan.Excluded("levels\\a\\client.zip"), "excluded server-only files");
            Check(Lan.ParseManifest("..\\x|1|aa\nok.txt|2|bb\n").Count == 1, "manifest rejects traversal");
            string root = Path.Combine(_tmp, "mod"); Directory.CreateDirectory(Path.Combine(root, "levels"));
            File.WriteAllText(Path.Combine(root, "levels", "a.txt"), "hello");
            List<FileEntry> m = Lan.BuildManifest(root, null, null);
            Check(m.Count == 1 && m[0].Hash == "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d", "manifest sha1");
            LanHost h = Lan.Parse(Lan.Payload("My Server", 16567, "bf2all64", "karkand", 3, 64, "coop", true), System.Net.IPAddress.Parse("10.0.0.5"));
            Check(h != null && h.Name == "My Server" && h.Mod == "bf2all64" && h.Running && h.Humans == 3, "beacon payload roundtrip");
            LanHost h2 = Lan.Parse(Lan.Payload("S", 16567, "bf2all64", "karkand", 3, 64, "coop", true) + "|abc123|32|24|1.0", System.Net.IPAddress.Parse("10.0.0.5"));
            LanHost h3 = Lan.Parse(Lan.Payload("S", 16567, "bf2all64", "karkand", 3, 64, "coop", true) + "|abc123|32|24|1.0", System.Net.IPAddress.Parse("10.0.0.6"));
            Check(h2.Size == 32 && h2.Bots == 24 && h2.ModVersion == "1.0" && h2.Key == h3.Key, "beacon v2 fields, one entry per PC");
            Check(string.IsNullOrEmpty(h2.Fp), "beacon without fingerprint (older manager)");
            LanHost h4 = Lan.Parse(Lan.Payload("S", 16567, "bf2all64", "karkand", 3, 64, "coop", true) + "|abc123|32|24|1.0|0123456789abcdef", System.Net.IPAddress.Parse("10.0.0.5"));
            Check(h4.Fp == "0123456789abcdef" && h4.ModVersion == "1.0", "beacon v3 fingerprint");
            // Prüfsumme: unabhängig von Reihenfolge und Groß-/Kleinschreibung, empfindlich für Inhalt
            List<FileEntry> f1 = new List<FileEntry>(), f2 = new List<FileEntry>(), f3 = new List<FileEntry>();
            f1.Add(E("a.txt", 1, "AA")); f1.Add(E("Levels\\b.zip", 2, "bb"));
            f2.Add(E("levels/B.zip", 2, "BB")); f2.Add(E("A.TXT", 1, "aa"));
            f3.Add(E("a.txt", 1, "aa")); f3.Add(E("levels\\b.zip", 2, "bc"));
            Check(Lan.Fingerprint(f1) == Lan.Fingerprint(f2) && Lan.Fingerprint(f1) != Lan.Fingerprint(f3) && Lan.Fingerprint(f1).Length == 16, "fingerprint stable and content-sensitive");
            // Spaß-Aufsatz: Rezept lesen, ungültige Basis ablehnen
            Check(FunMods.BaseFromRecipe("# x\r\nBase=bf2all64\r\nC4Mags=99\r\n") == "bf2all64", "recipe base");
            Check(FunMods.BaseFromRecipe("Base=..\\bf2") == null && FunMods.BaseFromRecipe("Base=a/b") == null && FunMods.BaseFromRecipe("C4Mags=1") == null, "recipe rejects bad base");
            // Aufsatz-Ordner: Verknüpfung zum Basis-Mod zählt nicht zum Manifest; Link legt fehlende Verknüpfungen an
            string game = Path.Combine(_tmp, "game"), bmod = Path.Combine(game, "mods", "basemod"), fmod = Path.Combine(game, "mods", "basemod_fun");
            Directory.CreateDirectory(Path.Combine(bmod, "levels")); Directory.CreateDirectory(Path.Combine(bmod, "menu"));
            File.WriteAllText(Path.Combine(bmod, "levels", "l.txt"), "level"); File.WriteAllText(Path.Combine(bmod, "mod.desc"), "base");
            Directory.CreateDirectory(fmod); File.WriteAllText(Path.Combine(fmod, FunMods.RecipeFile), "Base=basemod\r\n"); File.WriteAllText(Path.Combine(fmod, "fun_objects.zip"), "zip");
            int links = FunMods.Link(game, "basemod_fun");
            Check(links == 2 && File.Exists(Path.Combine(fmod, "levels", "l.txt")) && FunMods.Link(game, "basemod_fun") == 0, "fun pack links base folders once");
            List<FileEntry> fm = Lan.BuildManifest(fmod, null, null);
            Check(fm.Count == 2, "manifest skips linked folders");
            string fp = ModPrints.Compute(game, "basemod_fun");
            Check(fp.Contains("+") && fp.EndsWith(Lan.Fingerprint(Lan.BuildManifest(bmod, null, null))), "fun pack fingerprint includes base mod");
            Check(ModPrints.Compute(game, "nomod") == "" && FunMods.BaseOf(game, "basemod") == null, "fingerprint of missing mod, plain mod has no base");
            List<int> steps = new List<int>();
            string fp2 = ModPrints.Compute(game, "basemod_fun", delegate (int pc) { steps.Add(pc); });
            bool rising = steps.Count > 0; for (int i = 1; i < steps.Count; i++) if (steps[i] < steps[i - 1]) rising = false;
            Check(fp2 == fp && rising && steps[steps.Count - 1] == 100, "fingerprint progress rises to 100 %");
            int bp; double bs;
            Check(!ModPrints.Busy(game, "basemod_fun", out bp, out bs), "no fingerprint job running");
            foreach (string d in Directory.GetDirectories(fmod)) Directory.Delete(d);   // nur die Verknüpfungen entfernen, nicht den Inhalt des Basis-Mods
            Check(File.Exists(Path.Combine(bmod, "levels", "l.txt")), "removing links keeps base files");
        }

        static void FunTests()
        {
            string tw = "ObjectTemplate.activeSafe GenericFireArm c4_explosives\r\nObjectTemplate.ammo.magSize 1\r\nObjectTemplate.create GenericProjectile c4_explosives_Projectile\r\nObjectTemplate.detonation.explosionForce 17\r\nObjectTemplate.ammo.magSize 3\r\n";
            string r = FunPack.SetInTemplate(tw, "c4_explosives", "ammo.nrOfMags", "99", "ammo.magSize");
            Check(r.Contains("magSize 1\r\nObjectTemplate.ammo.nrOfMags 99\r\nObjectTemplate.create") && r.IndexOf("nrOfMags") == r.LastIndexOf("nrOfMags"), "fun: value inserted only in its own template");
            r = FunPack.SetInTemplate(r, "c4_explosives", "ammo.nrOfMags", "15", "ammo.magSize");
            Check(r.Contains("nrOfMags 15") && !r.Contains("nrOfMags 99"), "fun: existing value replaced");
            Check(FunPack.SetInTemplate(tw, "c4_explosives_Projectile", "detonation.explosionForce", "300", null).Contains("explosionForce 300"), "fun: projectile value set");
            Check(FunPack.SetInTemplate(tw, "c4_explosives", "detonation.explosionForce", "300", null) == tw, "fun: other template untouched");
            Check(FunPack.SetInTemplate(tw, "missing", "x", "1", null) == tw, "fun: unknown template unchanged");
            string def = "Material.active 24\r\nMaterial.type 4\r\nMaterial.active 1\r\nMaterial.type 0\r\nMaterial.active 80\r\nMaterial.type 2\r\nMaterial.active 3\r\nMaterial.type 3\r\n";
            string set = "MaterialManager.createCell 24 1\r\nMaterialManager.damageMod 0.33\r\nMaterialManager.createCell 80 24\r\nMaterialManager.damageMod 1\r\nMaterialManager.createCell 24 3\r\nMaterialManager.damageMod 0.5\r\n";
            int nf, nb; FunOptions o = new FunOptions(); o.BlastSoldierPct = 2;
            string m = FunPack.PatchMaterials(set, def, o, out nf, out nb);
            Check(nf == 1 && nb == 1 && m.Contains("createCell 24 1\r\nMaterialManager.damageMod 0\r\n") && m.Contains("damageMod 0.02") && m.Contains("damageMod 0.5"), "fun: fall and blast damage cells, vehicles untouched");
            o.NoFallDamage = false; o.BlastSoldierPct = 100;
            FunPack.PatchMaterials(set, def, o, out nf, out nb);
            Check(nf == 0 && nb == 0, "fun: nothing changed when switched off");
            string kit = "ObjectTemplate.create Kit US_Specops\r\nObjectTemplate.addTemplate USHGR_M67\r\nObjectTemplate.addTemplate c4_explosives\r\nObjectTemplate.addTemplate kni_knife\r\n";
            string k2 = FunPack.AddAfter(kit, "c4_explosives", "\r\nObjectTemplate.addTemplate usmin_claymore");
            Check(k2.Contains("c4_explosives\r\nObjectTemplate.addTemplate usmin_claymore\r\nObjectTemplate.addTemplate kni_knife"), "fun: kit item added after C4 with CRLF line ends");
            string k3 = FunPack.AddAfter(kit.Replace("c4_explosives", "at_mine"), null, "\r\nObjectTemplate.addTemplate c4_explosives");
            Check(k3.Contains("USHGR_M67\r\nObjectTemplate.addTemplate c4_explosives\r\nObjectTemplate.addTemplate at_mine"), "fun: C4 added to another kit after its first item");
            Check(FunPack.AddAfter("ObjectTemplate.create Kit X\r\n", null, "y") == "ObjectTemplate.create Kit X\r\n", "fun: kit without items unchanged");
            string eng = "ObjectTemplate.activeSafe Engine J_Engine\r\nObjectTemplate.setEngineType c_ETNewCar2\r\nObjectTemplate.setTorque 225\r\nObjectTemplate.activeSafe Sound S\r\nObjectTemplate.setTorque 1\r\n";
            Check(FunPack.Section(eng, "J_Engine").Contains("setTorque 225") && !FunPack.Section(eng, "J_Engine").Contains("setTorque 1\r"), "fun: template section ends at next template");
            // Tastenplanung: belegte Tasten im Kit werden nie doppelt vergeben (sonst stürzt der Server ab)
            Dictionary<string, int> slot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            slot["c4"] = 5; slot["clay"] = 5; slot["grapple"] = 6; slot["nv"] = 7; slot["mask"] = 8; slot["rifle"] = 3;
            Dictionary<string, List<string>> ki = new Dictionary<string, List<string>>();
            ki["spec"] = new List<string> { "rifle", "c4" }; ki["sfspec"] = new List<string> { "c4", "grapple", "nv", "mask" }; ki["sniper"] = new List<string> { "rifle", "clay" };
            int key = FunPack.BestKey(new int[] { 6, 7, 8 }, "clay", new List<string> { "sniper" }, new List<string> { "spec", "sfspec" }, ki, slot, -1);
            Check(key == 6, "fun keys: claymore gets a key free where it can be added");
            Check(FunPack.UsedKeys(ki["sfspec"], slot, null).IndexOf(key) >= 0, "fun keys: kit with that key taken is detected (no add there)");
            Check(FunPack.BestKey(new int[] { 6, 7, 8 }, "clay", new List<string> { "sfspec" }, new List<string>(), ki, slot, -1) == -1, "fun keys: no free key -> item stays");
            Check(FunPack.BestKey(new int[] { 6, 7, 8 }, "mine", new List<string>(), new List<string> { "spec" }, ki, slot, 6) == 7, "fun keys: mine avoids the claymore key");
            Check(FunPack.KitItems("ObjectTemplate.addTemplate a\r\nrem ObjectTemplate.addTemplate b\r\nObjecttemplate.addTemplate c\r\n").Count == 2, "fun keys: kit items ignore rem lines");
            Dictionary<string, object> pre = new Dictionary<string, object>();
            pre["Vals"] = new Dictionary<string, string>();
            Check(PresetFile.Summary(pre) == "Einstellungen, Bots" || PresetFile.Summary(pre) == "settings, bots", "preset summary: settings only");
            pre["Mod"] = "bf2all64"; pre["Type"] = "coop"; pre["Maps"] = new List<string> { "a gpm_coop 16", "b gpm_coop 32" }; pre["Fun"] = new Dictionary<string, string>();
            string ps = PresetFile.Summary(pre);
            Check(ps.Contains("bf2all64 · Coop · 2 Maps") && (ps.Contains("Spaßrunde") || ps.Contains("fun round")), "preset summary: rotation and fun round");
            string pf = Path.Combine(_tmp, "presets", "x.json");
            PresetFile.Write(pf, pre);
            Dictionary<string, object> back2 = PresetFile.Read(pf);
            Check(Convert.ToString(back2["Mod"]) == "bf2all64" && PresetFile.Summary(back2) == ps && !File.Exists(pf + ".part"), "preset file roundtrip");
            FunOptions o2 = new FunOptions(); o2.C4Mags = 7; o2.EndlessSprint = false; o2.GravityPct = 40;
            FunOptions back = FunOptions.FromDict(o2.ToDict());
            Check(back.C4Mags == 7 && !back.EndlessSprint && back.GravityPct == 40 && back.RocketJump, "fun: options roundtrip through recipe");
            Check(FunOptions.FromDict(FunMods.ParseRecipe("Base=x\r\nC4Mags=abc\r\n")).C4Mags == new FunOptions().C4Mags, "fun: bad recipe values keep defaults");
        }

        static FileEntry E(string rel, long size, string hash) { FileEntry e = new FileEntry(); e.Rel = rel; e.Size = size; e.Hash = hash; return e; }

        static void Board()
        {
            string f = Path.Combine(_tmp, "coop_rounds.txt");
            File.WriteAllText(f, "round|100|map|2|10|0|2\nplayer|Ansi|2|50|10|2\nplayer|Kater|2|30|5|5\nround|200|map|1|0|10|2\nplayer|Ansi|2|20|3|6\n");
            int rounds;
            List<LeaderRow> r = Leaderboard.Read(f, 0, out rounds);
            Check(rounds == 2 && r.Count == 2 && r[0].Name == "Ansi" && r[0].Score == 70 && r[0].Rounds == 2 && r[0].Wins == 1, "leaderboard totals");
            r = Leaderboard.Read(f, 150, out rounds);
            Check(rounds == 1 && r.Count == 1 && r[0].Score == 20, "leaderboard since");
        }

        static void ProfileFiles()
        {
            string f = Path.Combine(_tmp, "Video.con");
            File.WriteAllText(f, "VideoSettings.setResolution 800x600@60Hz\r\nVideoSettings.setViewDistanceScale 0.75\r\n");
            Dictionary<string, string> v = new Dictionary<string, string>();
            v["setResolution"] = "1920x1080@60Hz"; v["setTerrainQuality"] = "2";
            Profiles.Set(f, "VideoSettings", v);
            Check(Profiles.Get(f, "VideoSettings", "setResolution") == "1920x1080@60Hz", "profile value replaced");
            Check(Profiles.Get(f, "VideoSettings", "setTerrainQuality") == "2", "profile value added");
            Check(Profiles.Get(f, "VideoSettings", "setViewDistanceScale") == "0.75", "other values kept");
            Check(Profiles.CleanName(" Ansi der Große ") == "Ansi_der_Groe", "player name cleaned");
        }

        static void ProfileManage()
        {
            string root = Path.Combine(_tmp, "Profiles"), docs = Path.Combine(_tmp, "docs");
            Directory.CreateDirectory(Path.Combine(root, "Default"));
            File.WriteAllText(Path.Combine(root, "Default", "Video.con"), "VideoSettings.setResolution 800x600@60Hz\r\n");
            File.WriteAllText(Path.Combine(root, "Default", "Profile.con"), "LocalProfile.setName \"Default\"\r\n");
            Profiles.RootOverride = root;
            try
            {
                Check(Profiles.All().Count == 0 && Profiles.Default() == null, "template folder Default is not a profile");
                Bf2Profile a = Profiles.Create("Ansi", docs);
                Check(a != null && a.Id == "0001" && a.Nick == "Ansi" && a.Name == "Ansi", "profile created as 0001");
                Check(Profiles.DefaultId() == "0001", "new profile becomes default (Global.con created)");
                Check(File.Exists(Path.Combine(a.Dir, "Video.con")), "template files copied");
                Check(Profiles.NameProblem("ansi", null) != null, "duplicate name rejected");
                Check(Profiles.NameProblem("ansi", a) == null, "own name allowed for the same profile");
                Check(Profiles.NameProblem("defaultPlayer", null) != null && Profiles.NameProblem("ab", null) != null, "placeholder and short names rejected");
                Bf2Profile b = Profiles.Create("Zweiter", docs);
                Check(b.Id == "0002" && Profiles.DefaultId() == "0002", "second profile 0002");
                Profiles.Rename(a, "Anselm", docs);
                Bf2Profile a2 = null; foreach (Bf2Profile p in Profiles.All()) if (p.Id == "0001") a2 = p;
                Check(a2 != null && a2.Name == "Anselm" && a2.Nick == "Anselm", "rename sets account and nick");
                File.WriteAllText(Path.Combine(a2.Dir, "General.con"), "broken");
                Bf2Profile r = Profiles.Repair(a2, "Anselm", docs);
                Check(r.Id == "0001" && r.Nick == "Anselm" && !File.Exists(Path.Combine(r.Dir, "General.con")) && Profiles.DefaultId() == "0001", "repair keeps id and name, fresh files");
                Check(Directory.Exists(Path.Combine(docs, "BF2_Backups")), "repair creates a backup");
                Profiles.Delete(r, docs);
                Check(!Directory.Exists(r.Dir) && Profiles.DefaultId() == "0002", "delete moves default to the remaining profile");
            }
            finally { Profiles.RootOverride = null; }
        }

        static void GameFolder()
        {
            string root = Path.Combine(_tmp, "gf");
            // Manager neben dem Spielordner: Battlefield2\BF2Manager.exe + Battlefield2\Battlefield 2\BF2.exe
            string lan = Path.Combine(root, "lan"), game = Path.Combine(lan, "Battlefield 2");
            Directory.CreateDirectory(game); File.WriteAllText(Path.Combine(game, "BF2.exe"), "x");
            Check(GameLocator.PortableFrom(lan) == game, "manager next to the game folder finds it");
            Check(GameLocator.PortableFrom(game) == game, "manager inside the game folder");
            string sub = Path.Combine(game, "_LAN"); Directory.CreateDirectory(sub);
            Check(GameLocator.PortableFrom(sub) == game, "manager in a subfolder of the game folder");
            // anderer Name, Server-only-Installation
            string lan2 = Path.Combine(root, "lan2"), g2 = Path.Combine(lan2, "BF2 Kopie"); Directory.CreateDirectory(g2); File.WriteAllText(Path.Combine(g2, "bf2_w32ded.exe"), "x");
            Check(GameLocator.PortableFrom(lan2) == g2, "other game folder name (bf2) and server-only install");
            // kein Spiel in der Nähe
            string none = Path.Combine(root, "none"); Directory.CreateDirectory(Path.Combine(none, "Photos"));
            Check(GameLocator.PortableFrom(none) == null && GameLocator.PortableFrom("") == null, "no game nearby gives null");
        }

        static void Radio()
        {
            string f = Path.Combine(_tmp, "AIRadioMessages.ai"), docs = Path.Combine(_tmp, "docs");
            File.WriteAllText(f, "rem voice name\r\naiComm.addAIRadioMessage \"medic\" 10 20 20\r\naiComm.addAIRadioMessage \"defend\" 10 10 1\r\n");
            Check(BotRadio.State(f) == true, "bot radio on initially");
            BotRadio.Set(f, false, docs);
            Check(BotRadio.State(f) == false && File.ReadAllText(f).StartsWith("rem voice name"), "bot radio off, other lines kept");
            BotRadio.Set(f, true, docs);
            Check(BotRadio.State(f) == true && File.ReadAllText(f).Contains("\r\naiComm.addAIRadioMessage \"medic\" 10 20 20"), "bot radio on again, line restored");
            Check(BotRadio.State(Path.Combine(_tmp, "missing.ai")) == null, "missing radio file");
        }

        static void ModDesc()
        {
            string g = Path.Combine(_tmp, "game"); string md = Path.Combine(g, "mods", "testmod"); Directory.CreateDirectory(md);
            File.WriteAllText(Path.Combine(md, "mod.desc"), "<?xml version=\"1.0\"?>\n<mod>\n <title>Test Mod</title>\n <version>2.5</version>\n</mod>\n");
            ModInfo mi = ModInfo.Read(g, "testmod");
            Check(mi.Title == "Test Mod" && mi.Version == "2.5", "mod.desc title and version");
            string xp = Path.Combine(g, "mods", "xpack"); Directory.CreateDirectory(xp);
            File.WriteAllText(Path.Combine(xp, "mod.desc"), "<mod><title>MODDESC_XP_TITLE</title><version>1.5</version></mod>");
            Check(ModInfo.Read(g, "xpack").Title == "xpack", "mod.desc translation key falls back to folder name");
            Check(GameLocator.VersionText(Path.Combine(g, "missing.exe")) == "missing", "version text for a missing exe");
        }

        static void Cmds()
        {
            List<CmdDef> all = Commands.All(null);
            CmdDef say = all.Find(delegate (CmdDef c) { return c.Cmd == "game.sayAll"; });
            Check(say != null && say.Build("Hi \"you\"") == "game.sayAll \"Hi 'you'\"", "sayAll quoting");
            CmdDef spawn = all.Find(delegate (CmdDef c) { return c.Cmd == "sv.spawnTime"; });
            Check(spawn != null && spawn.Build("10") == "sv.spawnTime 10", "setting command from table");
            bool allowed = true;
            foreach (CmdDef c in all) if (!(c.Cmd.StartsWith("sv.") || c.Cmd.StartsWith("mapList.") || c.Cmd.StartsWith("admin.") || c.Cmd.StartsWith("game.say") || c.Cmd.StartsWith("@"))) allowed = false;
            Check(allowed, "all commands pass the server whitelist");
        }

        static void Scrub()
        {
            string t = Diagnostics.Scrub("sv.password \"geheim\"\nserver 192.168.1.20 public 8.8.8.8\n");
            Check(!t.Contains("geheim") && t.Contains("192.168.1.20") && !t.Contains("8.8.8.8"), "diagnostics scrub password and public IP");
        }
    }
}
