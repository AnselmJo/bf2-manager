// Einrichtung des Bot-Moduls - BF2 Manager. C# 5 (.NET Framework 4.x).
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
    // ------------------------------------------------------------------ Status und Einrichtung
    public class ModStatus
    {
        public string Mod; public bool Installed, Hooked, Writable; public int Version;
        public bool Ok { get { return Installed && Hooked && Writable && Version >= AppInfo.ModuleVersion; } }
        public bool Outdated { get { return Installed && Version < AppInfo.ModuleVersion; } }
    }

    // Spielmodus-Skript für Coop. Im Basisspiel ist gpm_coop.py bis auf Kommentare gleich gpm_cq.py.
    // Manchen Mods fehlt es (z. B. bfp2) – dann scheitert jeder Coop-Start mit "failed to initialize gamemode".
    // Es wird aus dem mod-eigenen gpm_cq.py erzeugt, damit die Regeln des Mods erhalten bleiben. Nie überschrieben.
    public static class CoopScript
    {
        static readonly Encoding Lat1 = Encoding.GetEncoding(28591);

        static string Dir(string gameDir, string mod) { return Path.Combine(gameDir, "mods", mod, "python", "game", "gamemodes"); }

        public static bool Missing(string gameDir, string mod)
        {
            string d = Dir(gameDir, mod);
            return File.Exists(Path.Combine(d, "gpm_cq.py")) && !File.Exists(Path.Combine(d, "gpm_coop.py"));
        }

        // true = angelegt
        public static bool Create(string gameDir, string mod)
        {
            if (!Missing(gameDir, mod)) return false;
            string d = Dir(gameDir, mod);
            string t = File.ReadAllText(Path.Combine(d, "gpm_cq.py"), Lat1)
                .Replace("print \"gpm_cq.py initialized\"", "print \"gpm_coop.py initialized\"")
                .Replace("print \"gpm_cq.py uninitialized\"", "print \"gpm_coop.py uninitialized\"");
            string nl = t.Contains("\r\n") ? "\r\n" : "\n";
            File.WriteAllText(Path.Combine(d, "gpm_coop.py"), "# gpm_coop.py - created by BF2 Manager from this mod's gpm_cq.py (co-op uses the same game logic as conquest, as in the base game)" + nl + t, Lat1);
            return true;
        }
    }

    public static class Installer
    {
        static readonly Encoding Lat1 = Encoding.GetEncoding(28591);

        public static ModStatus Check(string gameDir, string mod)
        {
            ModStatus s = new ModStatus(); s.Mod = mod;
            string md = Path.Combine(gameDir, "mods", mod);
            string cb = Path.Combine(md, "python", "game", "coopbots.py");
            s.Installed = File.Exists(cb);
            if (s.Installed)
            {
                Match m = Regex.Match(SafeRead(cb), @"MODULE_VERSION\s*=\s*(\d+)");
                s.Version = m.Success ? Util.ToInt(m.Groups[1].Value, 0) : 1;
            }
            s.Hooked = SafeRead(Path.Combine(md, "python", "game", "scoringCommon.py")).Contains("game.coopbots");
            s.Writable = CanWrite(Path.Combine(md, "settings"));
            return s;
        }

        static string SafeRead(string p) { try { return File.Exists(p) ? File.ReadAllText(p, Lat1) : ""; } catch { return ""; } }

        // Gruppe "Benutzer" (SID S-1-5-32-545) darf ändern; braucht Adminrechte
        public static void Grant(string dir)
        {
            ProcessStartInfo psi = new ProcessStartInfo("icacls.exe", "\"" + dir + "\" /grant *S-1-5-32-545:(OI)(CI)M /T /C /Q");
            psi.UseShellExecute = false; psi.CreateNoWindow = true;
            using (Process p = Process.Start(psi)) { p.WaitForExit(); if (p.ExitCode != 0) throw new Exception("icacls " + p.ExitCode); }
        }

        // Schreibt nur die Moduldatei (ohne Adminrechte, wenn die Einrichtung Schreibrechte vergeben hat)
        public static void WriteModule(string gameDir, string mod)
        {
            string md = Path.Combine(gameDir, "mods", mod);
            string abs = Path.Combine(md, "settings", "coop_server.txt").Replace('\\', '/');
            string code = ModuleSource().Replace("@CFG_ABS@", abs).Replace("@CFG_REL@", "mods/" + mod + "/settings/coop_server.txt");
            File.WriteAllText(Path.Combine(md, "python", "game", "coopbots.py"), code, Encoding.ASCII);
        }

        public static bool CanWrite(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string t = Path.Combine(dir, ".bf2sm_test");
                File.WriteAllText(t, "x"); File.Delete(t);
                return true;
            }
            catch { return false; }
        }

        public static string ModuleSource()
        {
            using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("coopbots.py"))
            using (StreamReader r = new StreamReader(st, Encoding.ASCII)) return r.ReadToEnd();
        }

        // Läuft mit Adminrechten. Protokoll-Zeilen: "+ text" = ok, "! text" = Warnung.
        public static bool Run(string gameDir, string docsDir, List<string> mods, List<string> log)
        {
            try
            {
                List<string> dirs = new List<string> { Path.Combine(gameDir, "python"), Path.Combine(gameDir, "admin") };
                foreach (string m in mods) dirs.Add(Path.Combine(gameDir, "mods", m));
                foreach (string d in dirs)
                {
                    if (!Directory.Exists(d)) continue;
                    try
                    {
                        Grant(d);
                        log.Add("+ " + I18n.T("Schreibrechte: ", "Write access: ") + d);
                    }
                    catch (Exception ex) { log.Add("! " + I18n.T("Schreibrechte fehlgeschlagen: ", "Granting write access failed: ") + d + " (" + ex.Message + ")"); }
                }
                List<string> touch = new List<string>();
                foreach (string m in mods) touch.Add(Path.Combine(gameDir, "mods", m, "python", "game", "scoringCommon.py"));
                string bk = Util.Backup(touch, docsDir);
                if (bk != null) log.Add("+ " + I18n.T("Sicherung: ", "Backup: ") + bk);
                string tpl = ModuleSource();
                foreach (string mod in mods)
                {
                    string md = Path.Combine(gameDir, "mods", mod);
                    string abs = Path.Combine(md, "settings", "coop_server.txt").Replace('\\', '/');
                    string code = tpl.Replace("@CFG_ABS@", abs).Replace("@CFG_REL@", "mods/" + mod + "/settings/coop_server.txt");
                    File.WriteAllText(Path.Combine(md, "python", "game", "coopbots.py"), code, Encoding.ASCII);
                    log.Add("+ " + mod + ": " + I18n.T("Bot-Modul installiert", "bot module installed"));

                    string sc = Path.Combine(md, "python", "game", "scoringCommon.py");
                    string txt = File.ReadAllText(sc, Lat1);
                    if (txt.Contains("game.coopbots")) log.Add("+ " + mod + ": " + I18n.T("scoringCommon.py bereits eingebunden", "scoringCommon.py already hooked"));
                    else
                    {
                        string nl = txt.Contains("\r\n") ? "\r\n" : "\n";
                        List<string> lines = new List<string>(Regex.Split(txt, "\r?\n"));
                        int idx = -1; string ind = "\t";
                        for (int i = 0; i < lines.Count; i++)
                        {
                            Match m = Regex.Match(lines[i], @"^(\s+)host\.registerGameStatusHandler\(onGameStatusChanged\)");
                            if (m.Success) { idx = i; ind = m.Groups[1].Value; break; }
                        }
                        if (idx < 0) log.Add("! " + mod + ": " + I18n.T("Einhängestelle in scoringCommon.py nicht gefunden", "hook point in scoringCommon.py not found"));
                        else
                        {
                            lines.InsertRange(idx + 1, new string[] { ind + "# BF2 Manager", ind + "try:", ind + ind + "import game.coopbots", ind + ind + "game.coopbots.init()", ind + "except:", ind + ind + "print \"coopbots could not be loaded\"" });
                            File.WriteAllText(sc, string.Join(nl, lines.ToArray()), Lat1);
                            log.Add("+ " + mod + ": " + I18n.T("scoringCommon.py eingebunden", "scoringCommon.py hooked"));
                        }
                    }

                    try { if (CoopScript.Create(gameDir, mod)) log.Add("+ " + mod + ": " + I18n.T("Coop-Spielmodus angelegt (gpm_coop.py fehlte)", "co-op game mode created (gpm_coop.py was missing)")); }
                    catch (Exception ex) { log.Add("! " + mod + ": gpm_coop.py: " + ex.Message); }
                    try { if (AiFile.DisableOverride(gameDir, mod, docsDir)) log.Add("+ " + mod + ": " + I18n.T("aiDefault.ai gibt die Bots nicht mehr fest vor (overrideMenuSettings 0)", "aiDefault.ai no longer forces the bots (overrideMenuSettings 0)")); }
                    catch (Exception ex) { log.Add("! " + mod + ": aiDefault.ai: " + ex.Message); }

                    string cfg = Path.Combine(md, "settings", "coop_server.txt");
                    if (!File.Exists(cfg))
                    {
                        CoopState st = new CoopState();
                        List<MapEntry> old = Maplist.Read(Path.Combine(docsDir, "Battlefield 2", "ServerConfigs", "coop", "maplist.con"));
                        if (old.Count > 0) st.Maps = old;
                        else foreach (int s in new int[] { 16, 32, 64 }) st.Maps.Add(MapEntry.Make("strike_at_karkand", "gpm_coop", s));
                        CoopTxt.Write(cfg, st);
                        log.Add("+ " + mod + ": " + I18n.T("coop_server.txt angelegt", "coop_server.txt created"));
                    }
                }
                return true;
            }
            catch (Exception ex) { log.Add("! " + ex.Message); return false; }
        }
    }
}
