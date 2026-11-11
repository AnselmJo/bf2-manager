// Hauptfenster: Vorlagen – mehrere gespeicherte Konfigurationen neben dem einen Standard - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BF2Coop
{
    // Inhalt einer Vorlage: Einstellungen und Bots immer; Rotation (mit Mod und Modus) und Spaßrunde wahlweise
    public static class PresetFile
    {
        public static string Summary(Dictionary<string, object> d)
        {
            object o; List<string> p = new List<string>();
            p.Add(I18n.T("Einstellungen, Bots", "settings, bots"));
            if (d.TryGetValue("Maps", out o) && o is System.Collections.ICollection)
            {
                string mod = d.TryGetValue("Mod", out o) ? Convert.ToString(o) : "";
                string type = d.TryGetValue("Type", out o) && Convert.ToString(o) == "conquest" ? "Conquest" : "Coop";
                p.Add(mod + " · " + type + " · " + ((System.Collections.ICollection)d["Maps"]).Count + " Maps");
            }
            if (d.ContainsKey("Fun")) p.Add(I18n.T("Spaßrunde", "fun round"));
            return string.Join("  ·  ", p.ToArray());
        }

        public static Dictionary<string, object> Read(string file)
        {
            return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(file, Encoding.UTF8));
        }

        public static void Write(string file, Dictionary<string, object> d)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string tmp = file + ".part";
            File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }

        public static string PathFor(string name) { return Path.Combine(AppPaths.Presets, Util.SafeName(name.Trim()) + ".json"); }

        public static List<string> All()
        {
            List<string> r = new List<string>();
            if (Directory.Exists(AppPaths.Presets)) r.AddRange(Directory.GetFiles(AppPaths.Presets, "*.json"));
            r.Sort(StringComparer.OrdinalIgnoreCase);
            return r;
        }
    }

    public partial class MainForm
    {
        readonly List<Control> _presetRows = new List<Control>();
        Para _presetEmpty;

        // Vorlage aus dem aktuellen Stand; withMaps = Rotation, Mod und Modus; withFun = Werte der Spaßrunde
        Dictionary<string, object> PresetSnapshot(bool withMaps, bool withFun)
        {
            Dictionary<string, object> d = Snapshot(withMaps);
            if (!withMaps) { d.Remove("Mod"); d.Remove("Type"); }
            if (withFun)
            {
                Dictionary<string, string> f = new Dictionary<string, string>(LoadFunOptions().ToDict());
                d["Fun"] = f;
            }
            d["Saved"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            return d;
        }

        void SavePresetAs()
        {
            string name = DarkDialog.Input(this, T("Neue Vorlage", "New preset"), T("Name der Vorlage, z. B. „Karkand 16 Bots“ oder „Spaßabend“:", "Name of the preset, e.g. “Karkand 16 bots” or “Fun night”:"), _mod + (_coop ? " Coop" : " Conquest"));
            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0) return;
            int what = DarkDialog.Ask(this, T("Was soll die Vorlage enthalten?", "What should the preset contain?"),
                T("Einstellungen und Bot-Werte sind immer dabei.\n\n• Mit Rotation: auch Mod, Modus und Map-Liste – Laden wechselt dorthin.\n• Mit Spaßrunde: zusätzlich die Werte der Spaßrunde.", "Settings and bot values are always included.\n\n• With rotation: also mod, mode and map list – loading switches there.\n• With fun round: also the fun round values."),
                2, false, T("Abbrechen", "Cancel"), T("Nur Einstellungen", "Settings only"), T("Mit Rotation", "With rotation"), T("+ Spaßrunde", "+ fun round")) - 1;
            if (what < 0) return;
            string file = PresetFile.PathFor(name);
            if (File.Exists(file) && !DarkDialog.Confirm(this, T("Vorlage ersetzen", "Replace preset"), T("Es gibt schon eine Vorlage „", "A preset “") + name.Trim() + T("“. Ersetzen?", "” already exists. Replace it?"), T("Ersetzen", "Replace"), false)) return;
            try
            {
                PresetFile.Write(file, PresetSnapshot(what >= 1, what >= 2));
                RefreshPresets();
                Toast(T("Vorlage gespeichert: ", "Preset saved: ") + name.Trim(), ToastKind.Ok);
            }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
        }

        void OverwritePreset(string file)
        {
            try
            {
                Dictionary<string, object> old = PresetFile.Read(file);
                string name = Path.GetFileNameWithoutExtension(file);
                if (!DarkDialog.Confirm(this, T("Vorlage aktualisieren", "Update preset"), T("„", "“") + name + T("“ mit dem aktuellen Stand überschreiben? Enthalten bleibt: ", "” will be overwritten with the current state. Contents stay: ") + PresetFile.Summary(old), T("Überschreiben", "Overwrite"), false)) return;
                PresetFile.Write(file, PresetSnapshot(old.ContainsKey("Maps"), old.ContainsKey("Fun")));
                RefreshPresets();
                Toast(T("Vorlage aktualisiert.", "Preset updated."), ToastKind.Ok);
            }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
        }

        void RenamePreset(string file)
        {
            string old = Path.GetFileNameWithoutExtension(file);
            string name = DarkDialog.Input(this, T("Vorlage umbenennen", "Rename preset"), T("Neuer Name:", "New name:"), old);
            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0 || name.Trim() == old) return;
            string dst = PresetFile.PathFor(name);
            if (File.Exists(dst)) { Toast(T("Diesen Namen gibt es schon.", "This name already exists."), ToastKind.Warn); return; }
            try { File.Move(file, dst); RefreshPresets(); }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
        }

        void DeletePreset(string file)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!DarkDialog.Confirm(this, T("Vorlage löschen", "Delete preset"), T("„", "“") + name + T("“ löschen?", "” will be deleted."), T("Löschen", "Delete"), true)) return;
            try { File.Delete(file); RefreshPresets(); } catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
        }

        // Vorlage laden: mit Rotation wird vorher zu Mod und Modus gewechselt; die Spaßrunde übernimmt ihre Werte
        void LoadPreset(string file)
        {
            try
            {
                Dictionary<string, object> d = PresetFile.Read(file);
                object o;
                bool withMaps = d.ContainsKey("Maps");
                string hint = null; bool applyMaps = withMaps;
                if (d.TryGetValue("Fun", out o) && o is Dictionary<string, object>)
                {
                    Dictionary<string, string> f = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)o) f[kv.Key] = Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture);
                    ApplyFunOptions(FunOptions.FromDict(f));
                }
                if (withMaps)
                {
                    string mod = d.TryGetValue("Mod", out o) ? Convert.ToString(o) : _mod;
                    bool coop = !d.TryGetValue("Type", out o) || Convert.ToString(o) != "conquest";
                    // Spaß-Aufsatz der Vorlage fehlt hier: Rotation beim Basis-Mod übernehmen, Aufsatz neu erstellen lassen
                    if (!_mods.Contains(mod) && mod.EndsWith("_fun", StringComparison.OrdinalIgnoreCase) && _mods.Contains(mod.Substring(0, mod.Length - 4)))
                    {
                        hint = T("Der Spaß-Aufsatz „", "The fun pack “") + mod + T("“ fehlt – unter Optionen › Spaßrunde „Erstellen und verwenden“ drücken.", "” is missing – press “Create and use” under Options › Fun round.");
                        mod = mod.Substring(0, mod.Length - 4);
                    }
                    if (!_mods.Contains(mod))
                    {
                        hint = T("Mod „", "Mod “") + mod + T("“ ist hier nicht eingerichtet – nur Einstellungen und Bots übernommen.", "” is not set up here – only settings and bots applied.");
                        applyMaps = false;
                    }
                    else if (mod != _mod || coop != _coop) { _selMod.SetSilent(mod); _segType.SetSilent(coop ? 0 : 1); SwitchContext(mod, coop); }
                }
                ApplySnapshot(d, applyMaps);
                Toast(hint ?? T("Vorlage geladen: ", "Preset loaded: ") + Path.GetFileNameWithoutExtension(file), hint != null ? ToastKind.Warn : ToastKind.Ok);
            }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
        }

        void ApplyFunOptions(FunOptions o)
        {
            foreach (KeyValuePair<string, string> kv in o.ToDict()) _cfg.Set("Fun." + kv.Key, kv.Value);
            _cfg.Save();
            foreach (KeyValuePair<string, Control> kv in _funEd)
            {
                string v; if (!o.ToDict().TryGetValue(kv.Key, out v)) continue;
                if (kv.Value is ToggleSwitch) ((ToggleSwitch)kv.Value).SetSilent(v == "1");
                else ((NumberBox)kv.Value).SetSilent(Util.ToInt(v, 0));
            }
        }

        // Liste der Vorlagen in der Karte „Standard und Vorlagen“ neu aufbauen
        void RefreshPresets()
        {
            if (_cStandard == null) return;
            foreach (Control c in _presetRows) { _cStandard.Controls.Remove(c); c.Dispose(); }
            _presetRows.Clear();
            foreach (string f in PresetFile.All())
            {
                string file = f, name = Path.GetFileNameWithoutExtension(f), sum;
                try { sum = PresetFile.Summary(PresetFile.Read(f)); } catch { sum = T("nicht lesbar", "unreadable"); }
                Para p = new Para(name + "\n" + sum); p.Color = Theme.Text;
                FlatButton bLoad = new FlatButton(T("Laden", "Load"), G.Down, BtnKind.Secondary);
                Theme.Tip.SetToolTip(bLoad, T("Übernimmt die Vorlage. Mit Rotation wird vorher zu ihrem Mod und Modus gewechselt. Wirkt wie eine eigene Änderung (automatisch gespeichert, „Rückgängig“ möglich).", "Applies the preset. With rotation, switches to its mod and mode first. Acts like your own change (saved automatically, “Undo” available)."));
                bLoad.Click += delegate { LoadPreset(file); };
                FlatButton bOver = new FlatButton(T("Mit aktuellem Stand überschreiben", "Overwrite with current state"), G.Save, BtnKind.Ghost);
                bOver.Click += delegate { OverwritePreset(file); };
                ContextMenuStrip menu = Theme.Menu();
                ToolStripMenuItem ren = new ToolStripMenuItem(T("Umbenennen …", "Rename …")); ren.Click += delegate { RenamePreset(file); };
                ToolStripMenuItem del = new ToolStripMenuItem(T("Löschen …", "Delete …")); del.Click += delegate { DeletePreset(file); };
                menu.Items.AddRange(new ToolStripItem[] { ren, del });
                FlatButton bMore = new FlatButton("⋯", null, BtnKind.Ghost); bMore.DropMenu = menu; bMore.MenuOnly = true; bMore.Fit();
                ButtonRow br = Buttons(bLoad, bOver, bMore);
                _presetRows.Add(p); _presetRows.Add(br);
                _cStandard.Controls.Add(p); _cStandard.Controls.Add(br);
            }
            _presetEmpty.Text = _presetRows.Count == 0 ? T("Noch keine Vorlagen. „Neue Vorlage“ speichert den aktuellen Stand unter einem Namen – z. B. eine Rotation für den Spaßabend.", "No presets yet. “New preset” saves the current state under a name – e.g. a rotation for the fun night.") : "";
            if (_srvHost != null) _srvHost.Masonry(_srvCards, 2);
        }
    }
}
