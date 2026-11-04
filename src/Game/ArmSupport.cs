// Windows on ARM (z. B. Mac mit Parallels): Einstellungen, ohne die BF2 unter der x86-Emulation nicht läuft - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BF2Coop
{
    // Auf ARM-PCs läuft BF2 in der x86-Emulation von Windows. Zwei Dinge sind dort nötig (gemessen auf Windows 11 ARM64 / Parallels / Apple M4):
    // 1. Emulationsschalter ARM64CHPEDISABLED für BF2.exe und bf2_w32ded.exe: Die "hybriden" Systembibliotheken rechnen cos() falsch;
    //    D3DX bricht dann beim Menü-Shader ab ("swiffmenu.fx: unexpected input register type") und das Spiel stürzt nach dem Intro ab,
    //    der Server endet nach etwa 25 s mit Code 42.
    // 2. RendDX9.dll kennt nur CPUs von Intel und AMD. Die emulierte CPU meldet sich als "Virtual CPU" und löst
    //    "Unknown processor family" aus – BF2 beendet sich. Der Patch behandelt eine unbekannte CPU wie eine bekannte Intel-CPU.
    public static class ArmSupport
    {
        public const string Layer = "ARM64CHPEDISABLED";
        const string LayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        static readonly string[] OkLayers = { "ARM64CHPEDISABLED", "ARM64SAFEEMULATION", "ARM64STRICTEXECUTION", "ARM64VERYSTRICTEXECUTION" };

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        public static bool? FakeArm; // nur für Tests und Screenshots

        public static bool IsArmPc()
        {
            if (FakeArm.HasValue) return FakeArm.Value;
            try { ushort pm, nm; if (IsWow64Process2(GetCurrentProcess(), out pm, out nm)) return nm == 0xAA64; } catch { }
            string a = (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") ?? Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "").ToUpperInvariant();
            return a == "ARM64";
        }

        // ---- Emulationsschalter (HKCU, pro exe-Pfad, keine Adminrechte nötig)
        public static string LayerValue(string exe) { return LayerValue(Registry.CurrentUser, exe); }

        public static string LayerValue(RegistryKey root, string exe)
        {
            using (RegistryKey k = root.OpenSubKey(LayersKey)) { return k == null ? null : k.GetValue(exe) as string; }
        }

        public static bool HasLayer(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (string t in value.Split(' ')) foreach (string ok in OkLayers) if (string.Equals(t, ok, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Fügt den Schalter zu einem vorhandenen Eintrag hinzu ("~ HIGHDPIAWARE" -> "~ HIGHDPIAWARE ARM64CHPEDISABLED") oder entfernt ihn.
        public static string WithLayer(string value, bool on)
        {
            List<string> tokens = new List<string>();
            foreach (string t in (value ?? "").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (t != "~" && !string.Equals(t, Layer, StringComparison.OrdinalIgnoreCase)) tokens.Add(t);
            if (on) tokens.Add(Layer);
            return tokens.Count == 0 ? null : "~ " + string.Join(" ", tokens.ToArray());
        }

        public static void SetLayer(string exe, bool on) { SetLayer(Registry.CurrentUser, exe, on); }

        public static void SetLayer(RegistryKey root, string exe, bool on)
        {
            using (RegistryKey k = root.CreateSubKey(LayersKey))
            {
                string nv = WithLayer(k.GetValue(exe) as string, on);
                if (nv == null) k.DeleteValue(exe, false); else k.SetValue(exe, nv, RegistryValueKind.String);
            }
        }

        // ---- CPU-Patch für RendDX9.dll / RendDX9x2.dll
        // ... call esi (strncmp mit "GenuineIntel"); add esp,0C; test eax,eax; jnz <Assertion> ; mov eax,[ebp-8]; and eax,FF; cmp eax,1; je ...
        // jnz +5D (75 5D) wird zu jmp +43 (EB 43): Sprung zum Ergebnis "bekannte Intel-CPU" statt zur Assertion. Keine Relocation betroffen.
        static readonly byte[] CpuSig = { 0xFF, 0xD6, 0x83, 0xC4, 0x0C, 0x85, 0xC0, 0x75, 0x5D, 0x8B, 0x45, 0xF8, 0x25, 0xFF, 0x00, 0x00, 0x00, 0x83, 0xF8, 0x01, 0x74, 0x43 };
        const int CpuJmp = 7;

        public static string[] RendDlls(string gameDir) { return new string[] { Path.Combine(gameDir, "RendDX9.dll"), Path.Combine(gameDir, "RendDX9x2.dll") }; }

        public static PatchState CpuPatchState(string dll)
        {
            try
            {
                if (!File.Exists(dll)) return PatchState.Missing;
                byte[] d = File.ReadAllBytes(dll);
                if (FindSig(d, 0x75, 0x5D) >= 0) return PatchState.Off;
                if (FindSig(d, 0xEB, 0x43) >= 0) return PatchState.On;
                return PatchState.Unknown;
            }
            catch { return PatchState.Unknown; }
        }

        public static string SetCpuPatch(string dll, bool on)
        {
            if (Pe.InUse(dll)) throw new InvalidOperationException(I18n.T("Battlefield 2 läuft noch. Bitte zuerst beenden.", "Battlefield 2 is still running. Please close it first."));
            byte[] d = File.ReadAllBytes(dll);
            byte fromOp = on ? (byte)0x75 : (byte)0xEB, fromRel = on ? (byte)0x5D : (byte)0x43;
            byte toOp = on ? (byte)0xEB : (byte)0x75, toRel = on ? (byte)0x43 : (byte)0x5D;
            string name = Path.GetFileName(dll);
            if (FindSig(d, toOp, toRel) >= 0) return name + ": " + (on ? I18n.T("CPU-Patch bereits aktiv", "CPU patch already active") : I18n.T("bereits original", "already original"));
            int i = FindSig(d, fromOp, fromRel);
            if (i < 0) throw new InvalidOperationException(name + ": " + I18n.T("Die erwartete Code-Stelle wurde nicht gefunden (andere Version) - nichts geändert.", "The expected code location was not found (different version) - nothing changed."));
            if (FindSig(d, fromOp, fromRel, i + 1) >= 0) throw new InvalidOperationException(name + ": " + I18n.T("Code-Stelle nicht eindeutig - nichts geändert.", "Code location is not unique - nothing changed."));
            Pe.BackupOnce(dll, ".vor_cpu.bak");
            d[i + CpuJmp] = toOp; d[i + CpuJmp + 1] = toRel;
            File.WriteAllBytes(dll, d);
            return name + ": " + (on ? I18n.T("CPU-Patch aktiviert", "CPU patch enabled") : I18n.T("CPU-Patch entfernt", "CPU patch removed"));
        }

        static int FindSig(byte[] d, byte op, byte rel) { return FindSig(d, op, rel, 0); }

        static int FindSig(byte[] d, byte op, byte rel, int start)
        {
            byte[] sig = (byte[])CpuSig.Clone(); sig[CpuJmp] = op; sig[CpuJmp + 1] = rel;
            return Pe.Find(d, sig, start);
        }

        // Ist alles eingerichtet? (für die PC-Prüfung)
        public static List<string> Missing(string gameDir)
        {
            List<string> m = new List<string>();
            foreach (string exe in new string[] { Path.Combine(gameDir, "BF2.exe"), Path.Combine(gameDir, "bf2_w32ded.exe") })
                if (File.Exists(exe) && !HasLayer(LayerValue(exe))) m.Add(Path.GetFileName(exe) + ": " + I18n.T("Emulationsschalter fehlt", "emulation setting missing"));
            foreach (string dll in RendDlls(gameDir))
                if (CpuPatchState(dll) == PatchState.Off) m.Add(Path.GetFileName(dll) + ": " + I18n.T("CPU-Patch fehlt", "CPU patch missing"));
            return m;
        }

        // Richtet alles ein; liefert Protokollzeilen.
        public static List<string> Apply(string gameDir)
        {
            List<string> log = new List<string>();
            foreach (string exe in new string[] { Path.Combine(gameDir, "BF2.exe"), Path.Combine(gameDir, "bf2_w32ded.exe") })
            {
                if (!File.Exists(exe)) continue;
                if (HasLayer(LayerValue(exe))) { log.Add(Path.GetFileName(exe) + ": " + I18n.T("Emulationsschalter bereits gesetzt", "emulation setting already set")); continue; }
                SetLayer(exe, true); log.Add(Path.GetFileName(exe) + ": " + I18n.T("Emulationsschalter gesetzt", "emulation setting set"));
            }
            foreach (string dll in RendDlls(gameDir))
                if (CpuPatchState(dll) == PatchState.Off) log.Add(SetCpuPatch(dll, true));
            return log;
        }
    }
}
