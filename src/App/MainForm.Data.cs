// Hauptfenster: Laden, Speichern - BF2 Manager. C# 5 (.NET Framework 4.x).
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
    public partial class MainForm
    {
        // ================================================================== Laden, Speichern
        void LoadAll()
        {
            if (_setupMode || _mod.Length == 0) return;
            _applying = true;
            try
            {
                _vals = Defs.Defaults();
                string sf = Path.Combine(CfgDir, "serverSettings.con");
                if (!File.Exists(sf)) sf = Path.Combine(ModSettingsDir, "serversettings.con");
                Dictionary<string, string> con = ConFile.Read(sf);
                foreach (SettingDef d in Defs.All) { string raw; if (!d.IsSpecial && con.TryGetValue(d.Key, out raw)) _vals[d.Key] = Defs.FromCon(d, raw); }
                CoopState ct = CoopTxt.Read(CoopTxtPath);
                _st = new CoopState();
                if (ct != null) { _st.Commander = ct.Commander; _st.HumansUS = ct.HumansUS; _st.HumansMEC = ct.HumansMEC; _st.Defaults = ct.Defaults; _st.Next = ct.Next; _st.Adaptive = ct.Adaptive; _st.AdaptStep = ct.AdaptStep; _st.AdaptMax = ct.AdaptMax; }
                foreach (int s in new int[] { 16, 32, 64 }) if (!_st.Defaults.ContainsKey(s)) _st.Defaults[s] = new CoopState().Defaults[s];
                if (_coop && ct != null && ct.Maps.Count > 0) _st.Maps = ct.Maps;
                else _st.Maps = Maplist.Read(Path.Combine(CfgDir, "maplist.con"));
                _vals["@commander"] = _st.Commander ? "1" : "0";
                _saved = new Dictionary<string, string>(_vals, StringComparer.OrdinalIgnoreCase);

                ApplyVals();
                _hUS.SetSilent(_st.HumansUS); _hMEC.SetSilent(_st.HumansMEC);
                _adaptToggle.SetSilent(_st.Adaptive); _adaptStep.SetSilent(_st.AdaptStep); _adaptMax.SetSilent(_st.AdaptMax);
                foreach (int s in new int[] { 16, 32, 64 }) _botEd[s].SetOpt(_st.Defaults[s]);
                _nextToggle.SetSilent(ct != null && ct.Next != null);
                _nextEd.SetOpt(ct != null && ct.Next != null ? ct.Next : _st.Defaults[32]);
                _nextId = ct != null && ct.Next != null ? ct.Next.Id : "";
                UpdateHumans();
                LoadAiAndNames();
                _nav[1].Enabled = _coop;
                _curRot = null;
                RefreshRot(_st.Maps.Count > 0 ? 0 : -1);
                OnRotSelect();
                LayoutMapRight();
                if (_page == 3) LayoutLive();
                if (!_coop && _page == PgBots) SelectPage(PgServer);
                _side.Footer = "v" + AppInfo.Version; _side.Invalidate();
            }
            catch (Exception ex) { Toast(T("Laden fehlgeschlagen: ", "Loading failed: ") + ex.Message, ToastKind.Error); Util.Log(AppPaths.Dir, ex.ToString()); }
            finally { _applying = false; }
            ClearDirty();
            _botHost.Masonry(_botCards, 3);
            StartScan();
        }

        void StartScan()
        {
            int gen = ++_scanGen;
            string game = GameDir, mod = _mod;
            _scanning = true; _avail = new List<AvailMap>(); _availIx.Clear(); RefreshAvail();
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<AvailMap> res;
                try { res = MapScanner.Scan(game, mod, AppPaths.Cache); } catch { res = new List<AvailMap>(); }
                try { BeginInvoke((MethodInvoker)delegate { OnScanned(gen, res); }); } catch { }
            });
        }

        void OnScanned(int gen, List<AvailMap> res)
        {
            if (gen != _scanGen) return;
            _scanning = false;
            _avail = res; _availIx.Clear();
            foreach (AvailMap a in res) _availIx[a.Key] = a;
            List<string> bad = InvalidRotation();
            // Mod ohne Coop-Maps (z. B. Pirates auf dem Dedicated Server): automatisch Conquest
            bool hasCoop = false, hasCq = false;
            foreach (AvailMap a in res) { if (a.Mode == "gpm_coop") hasCoop = true; if (a.Mode == "gpm_cq") hasCq = true; }
            if (_coop && res.Count > 0 && !hasCoop && hasCq && !_dirty)
            {
                Toast(T("„", "“") + _mod + T("“ hat keine Coop-Maps – Conquest wird verwendet.", "” has no coop maps – using Conquest."), ToastKind.Info);
                _segType.SetSilent(1); SwitchContext(_mod, false);
                return;
            }
            RefreshAvail();
            RefreshRot(_st.Maps.Count > 0 ? Math.Max(0, Math.Min(_lbRot.SelectedIndex, _st.Maps.Count - 1)) : -1);
            _curRot = null; OnRotSelect();
            if (bad.Count > 0)
            {
                PushUndo();
                _st.Maps.RemoveAll(delegate (MapEntry m) { return m.Mode != ModeName || !_availIx.ContainsKey(m.Key); });
                RefreshRot(_st.Maps.Count > 0 ? 0 : -1); RefreshAvail(); MarkDirty();
                Toast(T("Aus der Rotation entfernt (im Mod nicht vorhanden oder ohne Bot-Daten): ", "Removed from the rotation (missing in the mod or without bot data): ") + string.Join(", ", bad.GetRange(0, Math.Min(3, bad.Count)).ToArray()) + (bad.Count > 3 ? " …" : "") + T(" – „Rückgängig“ möglich.", " – “Undo” available."), ToastKind.Warn);
            }
            if (res.Count == 0) Toast(T("Keine Maps im Mod gefunden: ", "No maps found in mod: ") + Path.Combine(GameDir, "mods", _mod, "levels"), ToastKind.Warn);
        }

        // Rotationseinträge, deren Map/Größe/Modus es im Mod nicht gibt
        List<string> InvalidRotation()
        {
            List<string> bad = new List<string>();
            if (_avail.Count == 0) return bad;
            foreach (MapEntry m in _st.Maps)
                if (m.Mode != ModeName || !_availIx.ContainsKey(m.Key)) bad.Add(Util.Pretty(m.Name) + " " + m.Size);
            return bad;
        }

        void CollectState()
        {
            _st.HumansUS = _hUS.Value; _st.HumansMEC = _hMEC.Value;
            _st.Commander = _vals["@commander"] != "0";
            foreach (int s in new int[] { 16, 32, 64 }) _st.Defaults[s] = _botEd[s].Opt;
            _st.Adaptive = _adaptToggle.Checked; _st.AdaptStep = _adaptStep.Value; _st.AdaptMax = _adaptMax.Value;
        }

        bool SaveAll(bool quiet)
        {
            if (_setupMode) return false;
            CollectState();
            List<string> bad = InvalidRotation();
            if (bad.Count > 0)
            {
                _st.Maps.RemoveAll(delegate (MapEntry m) { return m.Mode != ModeName || !_availIx.ContainsKey(m.Key); });
                RefreshRot(Math.Min(_lbRot.SelectedIndex, _st.Maps.Count - 1)); RefreshAvail();
                Toast(T("Aus der Rotation entfernt (nicht im Mod): ", "Removed from rotation (not in mod): ") + string.Join(", ", bad.ToArray()), ToastKind.Warn);
            }
            if (_st.Maps.Count == 0) { if (!quiet) Toast(T("Die Rotation ist leer – bitte Maps hinzufügen.", "The rotation is empty – please add maps."), ToastKind.Warn); return false; }
            if (_coop && !quiet)
            {
                List<string> over = new List<string>();
                foreach (MapEntry m in _st.Maps)
                {
                    BotPlan p = Planner.Plan(OptFor(m), _st.HumansUS, _st.HumansMEC);
                    if (p.Total > m.Size) over.Add(Util.Pretty(m.Name) + " " + m.Size + ": " + p.Total + T(" Spieler", " players"));
                }
                if (over.Count > 0 && DarkDialog.Ask(this, T("Mehr Spieler als Plätze", "More players than slots"),
                    T("Bei diesen Maps übersteigen Menschen + Bots die Mapgröße:\n", "On these maps humans + bots exceed the map size:\n") + string.Join("\n", over.GetRange(0, Math.Min(6, over.Count)).ToArray()) +
                    (over.Count > 6 ? "\n…" : "") + T("\n\nTrotzdem speichern?", "\n\nSave anyway?"), 1, false, T("Abbrechen", "Cancel"), T("Speichern", "Save")) != 1) return false;
            }
            try
            {
                string dir = CfgDir, sf = Path.Combine(dir, "serverSettings.con"), mf = Path.Combine(dir, "maplist.con");
                Directory.CreateDirectory(dir);
                Util.Backup(new string[] { sf, mf, CoopTxtPath }, DocsDir);
                Dictionary<string, string> conVals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (SettingDef d in Defs.All) if (!d.IsSpecial) conVals[d.Key] = Defs.ToCon(d, _vals[d.Key]);
                CoopState disk = CoopTxt.Read(CoopTxtPath);
                if (_coop)
                {
                    BotPlan p = Planner.Plan(OptFor(_st.Maps[0]), _st.HumansUS, _st.HumansMEC);
                    conVals["sv.coopBotCount"] = p.Count.ToString(); conVals["sv.coopBotRatio"] = p.Ratio.ToString(); conVals["sv.coopBotDifficulty"] = p.Diff.ToString();
                    _st.Next = disk == null ? null : disk.Next;
                    CoopTxt.Write(CoopTxtPath, _st);
                }
                else
                {
                    if (disk == null) disk = new CoopState();
                    disk.Commander = _st.Commander;
                    CoopTxt.Write(CoopTxtPath, disk);
                }
                ConFile.Save(sf, conVals);
                Maplist.Write(mf, _st.Maps);
                if (_aiDirty && _ai != null && _ai.Path != null)
                {
                    _ai.Override = _aiOverride.Checked ? 1 : 0; _ai.IncludeHumans = _aiInclude.Checked ? 1 : 0; _ai.MaxBots = _aiMax.Value;
                    _ai.Save(DocsDir); _aiDirty = false;
                }
                if (_namesDirty && _namesPath != null)
                {
                    List<string> n = NamesFromBox();
                    if (n.Count > 0) BotNames.Write(_namesPath, n, _namesQuoted, DocsDir);
                    _namesDirty = false;
                }
                _saved = new Dictionary<string, string>(_vals, StringComparer.OrdinalIgnoreCase);
                ClearDirty();
                if (!quiet) Toast(T("Gespeichert.", "Saved."), ToastKind.Ok);
                return true;
            }
            catch (UnauthorizedAccessException) { NoRights(); return false; }
            catch (Exception ex) { Util.Log(AppPaths.Dir, ex.ToString()); Toast(T("Speichern fehlgeschlagen: ", "Saving failed: ") + ex.Message, ToastKind.Error); return false; }
        }

        void NoRights()
        {
            if (DarkDialog.Ask(this, T("Keine Schreibrechte", "No write access"), T("Der Manager darf im Spielordner nicht schreiben. Bitte unter Extras einmal „Einrichten“ ausführen.", "The manager cannot write to the game folder. Please run “Set up” under Extras once."), 1, false, T("Schließen", "Close"), T("Zu Extras", "Go to Extras")) == 1) SelectPage(PgExtras);
        }

        bool ConfirmDiscard()
        {
            // Es wird automatisch gespeichert – hier nur noch ausstehende Änderungen sofort schreiben
            AutoSaveNow();
            return true;
        }

        void SwitchContext(string mod, bool coop)
        {
            if (mod == _mod && coop == _coop) return;
            if (!ConfirmDiscard()) { _selMod.SetSilent(_mod); _segType.SetSilent(_coop ? 0 : 1); return; }
            _mod = mod; _coop = coop; UpdateCoopHints();
            _cfg.Set("LastMod", _mod); _cfg.Set("LastType", _coop ? "coop" : "conquest"); _cfg.Save();
            _preview.Set(null, "", "");
            foreach (Image i in _img.Values) if (i != null) i.Dispose();
            _img.Clear();
            ApplySetupMode();
            LoadAll();
            WarnModule();
            Toast((_coop ? "Coop" : "Conquest") + " · " + _mod, ToastKind.Info);
            UpdateFunCard();
        }
    }
}
