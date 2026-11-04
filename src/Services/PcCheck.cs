// PC-Check: Voraussetzungen prüfen und beheben - BF2 Manager. C# 5 (.NET Framework 4.x).
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
    public enum CheckStatus { Ok, Info, Warn, Error }

    public class CheckItem
    {
        public string Id, Title, Detail, FixLabel, Url;
        public CheckStatus Status;
        public List<Dictionary<string, object>> Tasks = new List<Dictionary<string, object>>();
        public bool CanFix { get { return Tasks.Count > 0 || Url != null || Id == "profile" || Id == "game" || Id == "arm"; } }
        public bool NeedsFix { get { return Status == CheckStatus.Warn || Status == CheckStatus.Error; } }
    }

    // Nur lesen: ist die Registrierung von Battlefield 2 in der Windows-Registry vorhanden?
    public static class Registration
    {
        public static bool Present()
        {
            foreach (RegistryView v in new RegistryView[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using (RegistryKey b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, v))
                    using (RegistryKey k = b.OpenSubKey(@"SOFTWARE\Electronic Arts\EA Games\Battlefield 2\ergc"))
                        if (k != null) { object o = k.GetValue(""); if (o != null && o.ToString().Length > 0) return true; }
                }
                catch { }
            }
            return false;
        }
    }

    public static class PcCheck
    {
        static string T(string de, string en) { return I18n.T(de, en); }

        public static List<CheckItem> Run(string gameDir, string docs) { return Run(gameDir, docs, null); }

        public static List<CheckItem> Run(string gameDir, string docs, bool? echo)
        {
            List<CheckItem> r = new List<CheckItem>();
            CheckItem g = New(r, "game", T("Spielordner", "Game folder"));
            if (!GameLocator.IsGameDir(gameDir)) { g.Status = CheckStatus.Error; g.Detail = T("Nicht gefunden. Den Ordner wählen, in dem BF2.exe liegt.", "Not found. Choose the folder that contains BF2.exe."); g.FixLabel = T("Ordner wählen", "Choose folder"); return r; }
            string exe = Path.Combine(gameDir, "BF2.exe"), ded = Path.Combine(gameDir, "bf2_w32ded.exe");
            g.Status = CheckStatus.Ok;
            string ver = GameLocator.FileVersion(exe);
            g.Detail = gameDir + (ver != null && ver != "?" ? "  ·  BF2 " + ver : "") + (GameLocator.IsBf2Hub(gameDir) ? "  ·  BF2Hub" : "") + (GameLocator.Portable() != null ? T("  ·  LAN-Ordner", "  ·  LAN folder") : "");

            // Spiel-Registrierung: nur Anzeige. Die Seriennummer gehört zum eigenen Exemplar und wird nicht vom Manager gesetzt.
            if (File.Exists(exe))
            {
                CheckItem reg = New(r, "reg", T("Registrierung", "Registration"));
                if (Registration.Present()) { reg.Status = CheckStatus.Ok; reg.Detail = T("Battlefield 2 ist auf diesem PC registriert.", "Battlefield 2 is registered on this PC."); }
                else
                {
                    reg.Status = CheckStatus.Info;
                    reg.Detail = T("Im Windows-Register fehlt der Eintrag von Battlefield 2 (Seriennummer). Ohne ihn startet das Spiel oder der Serverbeitritt evtl. nicht. Das Spiel mit der eigenen Seriennummer installieren bzw. registrieren – der Manager kann das nicht übernehmen.",
                        "The Windows registry entry of Battlefield 2 (serial number) is missing. Without it the game or joining a server may not work. Install or register the game with your own serial number – the manager cannot do this for you.");
                }
            }

            CheckItem w = New(r, "write", T("Schreibrechte", "Write access"));
            bool canWrite = Installer.CanWrite(Path.Combine(gameDir, "mods"));
            w.Status = canWrite ? CheckStatus.Ok : CheckStatus.Warn;
            w.Detail = canWrite ? T("Der Manager darf im Spielordner schreiben.", "The manager can write to the game folder.") : T("Ohne Schreibrechte funktionieren Patches, LAN-Abgleich und Einstellungen nicht.", "Without write access patches, LAN sync and settings do not work.");
            if (!canWrite) { w.Tasks.Add(Tasks.Grant(gameDir)); w.FixLabel = T("Schreibrechte vergeben", "Grant write access"); }

            CheckItem dx = New(r, "dx", "DirectX 9");
            if (File.Exists(exe))
            {
                List<string> need = DirectX.Needed(gameDir), miss = DirectX.Missing(gameDir, need);
                if (need.Count == 0) { dx.Status = CheckStatus.Info; dx.Detail = T("Benötigte Dateien nicht erkennbar.", "Required files could not be detected."); }
                else if (miss.Count == 0) { dx.Status = CheckStatus.Ok; dx.Detail = string.Join(", ", need.ToArray()) + T(" vorhanden", " present"); }
                else
                {
                    dx.Status = CheckStatus.Error; dx.Detail = T("Fehlt: ", "Missing: ") + string.Join(", ", miss.ToArray());
                    string setup = DirectX.Setup(gameDir);
                    if (setup != null) { dx.Tasks.Add(Tasks.DirectX(setup)); dx.FixLabel = T("DirectX installieren", "Install DirectX"); }
                    else { dx.Detail += T(" · Tipp: die DirectX-Laufzeit (Juni 2010) von Microsoft nach _LAN\\DirectX entpacken, dann behebt der Manager das", " · Tip: extract the DirectX runtime (June 2010) from Microsoft to _LAN\\DirectX, then the manager can fix it"); }
                }
            }
            else { dx.Status = CheckStatus.Info; dx.Detail = T("Nur Server installiert (kein BF2.exe).", "Server only (no BF2.exe)."); }

            // Windows on ARM (z. B. Mac mit Parallels): ohne Emulationsschalter und CPU-Patch stürzen Spiel und Server ab
            if (ArmSupport.IsArmPc())
            {
                CheckItem arm = New(r, "arm", T("Windows on ARM", "Windows on ARM"));
                List<string> miss = ArmSupport.Missing(gameDir);
                arm.Status = miss.Count == 0 ? CheckStatus.Ok : CheckStatus.Warn;
                arm.Detail = miss.Count == 0
                    ? T("Spiel und Server sind für die x86-Emulation eingerichtet.", "Game and server are set up for x86 emulation.")
                    : T("Dieser PC hat einen ARM-Prozessor (z. B. Mac mit Parallels). Ohne Anpassung stürzt BF2 beim Start oder nach dem Intro ab und der Server endet nach kurzer Zeit. Fehlt: ", "This PC has an ARM processor (e.g. a Mac with Parallels). Without these settings BF2 crashes at start or after the intro and the server stops after a short time. Missing: ") + string.Join(", ", miss.ToArray());
                if (miss.Count > 0) arm.FixLabel = T("Einrichten", "Set up");
            }

            CheckItem fw = New(r, "firewall", T("Firewall", "Firewall"));
            List<string> progs = new List<string>();
            foreach (string p in new string[] { exe, ded, AppPaths.Exe }) if (File.Exists(p) && !Firewall.Has(p)) progs.Add(p);
            fw.Status = progs.Count == 0 ? CheckStatus.Ok : CheckStatus.Warn;
            fw.Detail = progs.Count == 0 ? T("Spiel, Server und Manager sind im Netzwerk erreichbar.", "Game, server and manager are reachable on the network.")
                : T("Ohne Freigabe finden andere PCs Server und Manager evtl. nicht: ", "Without rules other PCs may not find the server and manager: ") + string.Join(", ", progs.ConvertAll(delegate (string p) { return Path.GetFileName(p); }).ToArray());
            if (progs.Count > 0) { fw.Tasks.Add(Tasks.Firewall(progs)); fw.FixLabel = T("Freigaben anlegen", "Add rules"); }

            // Praxistest: kommt die eigene Bekanntmachung über das Netzwerk zurück? (null = noch nicht prüfbar)
            if (echo != null)
            {
                CheckItem lan = New(r, "lan", T("Server-Suche im LAN", "Server search on LAN"));
                if (echo == true) { lan.Status = CheckStatus.Ok; lan.Detail = T("Dieser PC empfängt seine eigene Bekanntmachung – Server im Netzwerk werden gefunden.", "This PC receives its own announcement – servers on the network are found."); }
                else
                {
                    lan.Status = CheckStatus.Warn;
                    lan.Detail = T("Die eigene Bekanntmachung kommt nicht zurück – andere Server erscheinen dann evtl. auch nicht. Meist blockiert die Windows-Firewall den Manager, z. B. nach dem Verschieben der Exe oder wenn die Windows-Abfrage nur für öffentliche Netze bestätigt wurde.",
                        "The own announcement does not come back – other servers may not appear either. Usually the Windows firewall blocks the manager, e.g. after moving the exe or when the Windows prompt was only confirmed for public networks.");
                    lan.Tasks.Add(Tasks.Firewall(new List<string> { AppPaths.Exe })); lan.FixLabel = T("Firewall freigeben", "Allow in firewall");
                }
            }

            CheckItem laa = New(r, "laa", T("4-GB-Speicher-Patch", "4 GB memory patch"));
            List<string> noLaa = new List<string>(); int unknown = 0;
            foreach (string p in new string[] { exe, ded }) { PatchState ls = Pe.LaaState(p); if (ls == PatchState.Off) noLaa.Add(p); else if (ls == PatchState.Unknown) unknown++; }
            laa.Status = noLaa.Count > 0 ? CheckStatus.Warn : (unknown > 0 ? CheckStatus.Info : CheckStatus.Ok);
            laa.Detail = noLaa.Count == 0 && unknown > 0 ? T("Nicht prüfbar (unbekanntes Dateiformat).", "Cannot be checked (unknown file format).") : noLaa.Count == 0 ? T("Spiel und Server dürfen bis zu 4 GB Arbeitsspeicher nutzen.", "Game and server may use up to 4 GB of memory.")
                : T("Ohne Patch nur 2 GB – große Mods und 64er-Maps können abstürzen.", "Without the patch only 2 GB – large mods and 64-player maps may crash.");
            foreach (string p in noLaa) laa.Tasks.Add(Tasks.Laa(p, true));
            if (noLaa.Count > 0) laa.FixLabel = T("Patch anwenden", "Apply patch");

            CheckItem un = New(r, "unlock", T("Zweitwaffen (Client-Unlock)", "Unlock weapons (client)"));
            PatchState cs = File.Exists(exe) ? ClientUnlock.State(exe) : PatchState.Missing;
            un.Status = cs == PatchState.On ? CheckStatus.Ok : (cs == PatchState.Off ? CheckStatus.Warn : CheckStatus.Info);
            un.Detail = cs == PatchState.On ? T("Freigeschaltet.", "Enabled.") : (cs == PatchState.Off ? T("Gesperrt – im LAN sonst keine Zweitwaffen.", "Locked – no unlock weapons on LAN otherwise.") : T("Nicht erkannt (andere BF2-Version oder kein BF2.exe).", "Not detected (different BF2 version or no BF2.exe)."));
            if (cs == PatchState.Off) { un.Tasks.Add(Tasks.Client(exe, true)); un.FixLabel = T("Freischalten", "Enable"); }

            CheckItem pr = New(r, "profile", T("Spielerprofil", "Player profile"));
            Bf2Profile def = Profiles.Default();
            if (def == null) { pr.Status = CheckStatus.Warn; pr.Detail = T("Noch kein Profil – der Manager legt es an, sobald du einen Namen eingibst.", "No profile yet – the manager creates it once you enter a name."); pr.FixLabel = T("Profil anlegen", "Create profile"); }
            else if (!def.Online && Profiles.IsPlaceholder(def.Nick)) { pr.Status = CheckStatus.Warn; pr.Detail = T("Spielername ist noch „", "Player name is still “") + (def.Nick ?? "") + T("“.", "”."); pr.FixLabel = T("Namen setzen", "Set name"); }
            else { pr.Status = CheckStatus.Ok; pr.Detail = def.Nick + " (" + def.Id + ")"; }
            return r;
        }

        static CheckItem New(List<CheckItem> r, string id, string title) { CheckItem c = new CheckItem(); c.Id = id; c.Title = title; r.Add(c); return c; }
    }
}
