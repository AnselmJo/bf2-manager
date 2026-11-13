// Hauptfenster: Spaßregeln pro Map über Regel-Sets (rechts neben der Rotation) - BF2 Manager. C# 5.
// Eine Map bekommt ein Regel-Set (oder normale Regeln). Ändert man ein Set, gilt das für alle Maps damit.
// Maps mit demselben Set laufen ohne Neustart nacheinander; erst ein anderes Set (oder Mod) braucht einen kurzen Neustart.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BF2Coop
{
    public partial class MainForm
    {
        Card _cFunMap; SelectBox _fmSet; Para _fmInfo, _fmSetInfo; ButtonRow _fmSetBtns;
        readonly List<Control> _fmRows = new List<Control>();
        readonly Dictionary<string, Control> _funEd = new Dictionary<string, Control>();

        static string NormalLabel { get { return T("Normal", "Normal"); } }
        static string NewSetLabel { get { return T("+ Neues Regel-Set …", "+ New rule set …"); } }

        void BuildFunMapCard()
        {
            _cFunMap = new Card(T("Spaßregeln", "Fun rules"), G.Bolt);
            _fmSet = new SelectBox(null, Dpi.D(210));
            Theme.Tip.SetToolTip(_fmSet, T("Regel-Set dieser Map\nNormal = normales Spiel. Ein Regel-Set ist eine benannte Sammlung von Spaßregeln – mehrere Maps können dasselbe Set nutzen; ändert man es, gilt das für alle.\nMaps mit demselben Set laufen ohne Neustart nacheinander.", "Rule set of this map\nNormal = normal game. A rule set is a named collection of fun rules – several maps can share it; change it once and it applies to all.\nMaps with the same set run back to back without a restart."));
            _fmSet.SelectionChanged += delegate { OnFunSetPicked(); };
            _fmInfo = new Para(T("Normales Spiel. Oben ein Regel-Set wählen oder ein neues anlegen. Rechtsklick auf Maps in der Rotation weist Sets schnell zu.", "Normal game. Pick a rule set above or create a new one. Right-click maps in the rotation to assign sets quickly.")); _fmInfo.Color = Theme.Dim;
            _fmSetInfo = new Para(""); _fmSetInfo.Color = Theme.Dim;
            FlatButton bRen = new FlatButton(T("Umbenennen", "Rename"), null, BtnKind.Ghost), bDup = new FlatButton(T("Kopieren", "Copy"), null, BtnKind.Ghost), bDel = new FlatButton(T("Löschen", "Delete"), null, BtnKind.Ghost), bAll = new FlatButton(T("Allen Maps geben", "Give to all maps"), null, BtnKind.Ghost);
            foreach (FlatButton b in new FlatButton[] { bRen, bDup, bAll, bDel }) { b.Height = Dpi.D(28); b.Fit(); }
            Theme.Tip.SetToolTip(bDup, T("Legt eine Kopie dieses Sets an und gibt sie dieser Map – zum Abwandeln, ohne die anderen Maps zu ändern.", "Creates a copy of this set and gives it to this map – to vary it without changing the other maps."));
            Theme.Tip.SetToolTip(bAll, T("Gibt dieses Set allen Maps der Rotation.", "Gives this set to all maps of the rotation."));
            bRen.Click += delegate { RenameFunSet(); }; bDup.Click += delegate { CopyFunSet(); }; bDel.Click += delegate { DeleteFunSet(); }; bAll.Click += delegate { GiveSetToAll(); };
            _fmSetBtns = Buttons(bRen, bDup, bAll, bDel);
            FunOptions o = FunOptions.Neutral();
            _fmRows.Add(_fmSetInfo); _fmRows.Add(_fmSetBtns);
            _fmRows.Add(FunRule("WeaponRule", o.WeaponRule));
            _fmRows.Add(FunBool("NoVehicles", T("Ohne Fahrzeuge", "No vehicles"), T("Auf dieser Map gibt es keine Panzer, Jeeps, Helis, Jets und Boote – der Manager baut für die Map eine Fassung ohne Fahrzeug-Spawner. Stationäre Waffen und Commander-Anlagen bleiben.", "No tanks, jeeps, helicopters, jets or boats on this map – the manager builds a version of the map without vehicle spawners. Stationary weapons and commander assets stay."), o.NoVehicles));
            _fmRows.Add(FunNum("C4Mags", T("C4 pro Spieler", "C4 per player"), T("Anzahl C4-Ladungen pro Leben (Original 5). Bis zu 30 liegen gleichzeitig in der Welt.", "C4 charges per life (original 5). Up to 30 can lie in the world at once."), 0, 99, o.C4Mags, ""));
            _fmRows.Add(FunNum("C4Force", T("C4-Wucht (Trampolin)", "C4 force (trampoline)"), T("Wie stark die C4-Explosion Soldaten und Fahrzeuge wegschleudert (Original 17). 300 = C4-Trampolin.", "How hard the C4 explosion throws soldiers and vehicles (original 17). 300 = C4 trampoline."), 0, 5000, o.C4Force, ""));
            _fmRows.Add(FunNum("BlastSoldierPct", T("C4-Schaden an Soldaten", "C4 damage to soldiers"), T("Schaden von C4, Sprung-Claymore und Explosiv-Sniper an Soldaten in Prozent (Original 100). Klein = man fliegt, statt zu sterben. Fahrzeuge bekommen weiter vollen Schaden.", "Damage of C4, jump claymore and explosive sniper to soldiers in percent (original 100). Low = you fly instead of dying. Vehicles still take full damage."), 0, 100, o.BlastSoldierPct, "%"));
            _fmRows.Add(FunBool("NoFallDamage", T("Kein Fallschaden", "No fall damage"), T("Stürze und Landungen verletzen nicht mehr.", "Falls and landings no longer hurt."), o.NoFallDamage));
            _fmRows.Add(FunBool("ExtraExplosives", T("Spec Ops: Sprung-Claymore und Mega-Mine", "Spec Ops: jump claymore and mega mine"), T("Spec Ops hat drei Sprengsätze auf eigenen Tasten:\n5 = C4 (Trampolin, fernzünden)\n6 = Sprung-Claymore (Stolperfalle, schleudert Soldaten)\n7 = Mega-Mine (löst bei Fahrzeugen aus, wirft sie meterweit)\nAuch das Mausrad blättert durch. Sind diese Tasten im Mod schon belegt (bf2sf64: Enterhaken, Nachtsicht, Gasmaske), bleiben die Extras weg – der Manager prüft das beim Erstellen. Eigene neue C4-Arten lässt Battlefield 2 ohne neue Spieldateien nicht zu, daher diese beiden mit ihrem eigenen Aussehen.", "Spec Ops gets three explosives on their own keys:\n5 = C4 (trampoline, remote)\n6 = jump claymore (trip wire, throws soldiers)\n7 = mega mine (triggered by vehicles, throws them far)\nThe mouse wheel also cycles. If these keys are taken in the mod (bf2sf64: grappling hook, night vision, gas mask), the extras are left out – the manager checks this when creating. Battlefield 2 does not allow new C4 types without new game files, hence these two with their own look."), o.ExtraExplosives));
            _fmRows.Add(FunBool("C4Engineer", T("C4 auch für Pioniere", "C4 for engineers too"), T("Pioniere bekommen C4 auf Taste 5 (ihre Mine wandert dafür auf eine freie Taste). Nur wo Taste 5 frei ist.", "Engineers get C4 on key 5 (their mine moves to a free key). Only where key 5 is free."), o.C4Engineer));
            _fmRows.Add(FunBool("C4Sniper", T("C4 auch für Scharfschützen", "C4 for snipers too"), T("Scharfschützen bekommen C4 auf Taste 5 (ihre Claymore wandert dafür auf eine freie Taste). Nur wo Taste 5 frei ist.", "Snipers get C4 on key 5 (their claymore moves to a free key). Only where key 5 is free."), o.C4Sniper));
            _fmRows.Add(FunBool("C4AT", T("C4 auch für Panzerabwehr", "C4 for anti-tank too"), T("Die Panzerabwehr bekommt C4 auf Taste 5.", "Anti-tank gets C4 on key 5."), o.C4AT));
            
            _fmRows.Add(FunNum("RocketForce", T("Raketen-Wucht", "Rocket force"), T("Wie stark die Raketen-Explosion schleudert (Original 7). 250 = Raketensprung: auf den Boden schießen und abheben. 0 = unverändert.", "How hard the rocket blast throws (original 7). 250 = rocket jump: shoot the ground and take off. 0 = unchanged."), 0, 5000, o.RocketForce, ""));
            _fmRows.Add(FunNum("RocketDamagePct", T("Raketen-Explosionsschaden", "Rocket blast damage"), T("Schaden der Raketen-Explosion in Prozent (100 = normal) – auch an Fahrzeugen. Klein = man fliegt, statt zu sterben. Volltreffer zählen unverändert.", "Damage of the rocket blast in percent (100 = normal) – vehicles included. Low = you fly instead of dying. Direct hits count unchanged."), 0, 500, o.RocketDamagePct, "%"));
            _fmRows.Add(FunNum("RocketSpeedPct", T("Raketentempo", "Rocket speed"), T("Höchsttempo der Panzerfaust-Raketen in Prozent (100 = normal). 25 = langsam genug zum Ausweichen.", "Top speed of rockets in percent (100 = normal). 25 = slow enough to dodge."), 10, 100, o.RocketSpeedPct, "%"));
            _fmRows.Add(FunNum("RocketMags", T("Raketen pro Spieler", "Rockets per player"), T("Anzahl Raketen pro Leben (Original 5–6).", "Rockets per life (original 5–6)."), 0, 99, o.RocketMags, ""));
            _fmRows.Add(FunNum("RocketReloadMs", T("Nachladen Panzerfaust", "Rocket reload"), T("Nachladezeit in Millisekunden (Original 3000 = 3 s). 100 = 0,1 s. 0 = unverändert.", "Reload time in milliseconds (original 3000 = 3 s). 100 = 0.1 s. 0 = unchanged."), 0, 5000, o.RocketReloadMs, "ms"));
            _fmRows.Add(FunNum("VehiclePowerPct", T("Turbo für Fahrzeuge", "Vehicle turbo"), T("Motorkraft von Autos und Panzern in Prozent (100 = normal). 200 = doppelte Kraft und deutlich schneller. Gilt dauerhaft – eine eigene Boost-Taste lässt Battlefield 2 ohne neue Spieldateien nicht zu.", "Engine power of cars and tanks in percent (100 = normal). 200 = double power and much faster. Always on – Battlefield 2 does not allow a separate boost key without new game files."), 50, 500, o.VehiclePowerPct, "%"));
            _fmRows.Add(FunNum("HealthPct", T("Lebenspunkte der Soldaten", "Soldier health"), T("Lebenspunkte aller Soldaten in Prozent (100 = normal). 200 = man hält doppelt so viel aus – gut bei vielen Explosionen. Gilt auch für Bots.", "Health of all soldiers in percent (100 = normal). 200 = you take twice as much – good with lots of explosions. Applies to bots too."), 25, 500, o.HealthPct, "%"));
            _fmRows.Add(FunBool("WaterKills", T("Wasser ist tödlich", "Water is deadly"), T("Wer Wasser berührt, stirbt sofort – „Der Boden ist Lava“. Gilt für Soldaten, auch für Bots. Fahrzeuge bleiben wie sie sind.", "Touching water kills instantly – “the floor is lava”. Applies to soldiers, bots included. Vehicles stay as they are."), o.WaterKills));
            _fmRows.Add(FunNum("SniperForce", T("Explosiv-Sniper (Wucht)", "Explosive sniper (force)"), T("Geschosse der Scharfschützengewehre explodieren beim Einschlag und schleudern alles in der Nähe weg. Der Treffer selbst zählt normal. 0 = aus.", "Sniper rifle rounds explode on impact and throw everything nearby. The hit itself counts as usual. 0 = off."), 0, 3000, o.SniperForce, ""));
            _fmRows.Add(FunNum("GrenadeForce", T("Handgranaten-Wucht", "Grenade force"), T("Wie stark Handgranaten schleudern (Original 5). Mit wenig Schaden = Granaten-Sprung: Granate vor die Füße, abheben. 0 = unverändert.", "How hard hand grenades throw (original 5). With low damage = grenade jump: drop one at your feet, take off. 0 = unchanged."), 0, 3000, o.GrenadeForce, ""));
            _fmRows.Add(FunNum("GrenadeDamagePct", T("Handgranaten-Schaden", "Grenade damage"), T("Explosionsschaden der Handgranaten in Prozent (100 = normal).", "Blast damage of hand grenades in percent (100 = normal)."), 0, 500, o.GrenadeDamagePct, "%"));
            _fmRows.Add(FunBool("EndlessAmmo", T("Fast unendlich Munition", "Nearly endless ammo"), T("Alle Handwaffen bekommen 99 Magazine.", "All hand weapons get 99 magazines."), o.EndlessAmmo));
            _fmRows.Add(FunBool("EndlessSprint", T("Sprinten ohne Ausdauer", "Sprint without stamina"), T("Sprinten erschöpft nicht mehr, Springen kostet keine Ausdauer.", "Sprinting never runs out, jumping costs no stamina."), o.EndlessSprint));
            _fmRows.Add(FunNum("GravityPct", T("Schwerkraft (hohe Sprünge)", "Gravity (high jumps)"), T("Schwerkraft in Prozent (100 = normal). 40 = Mond: hohe Sprünge, weite Flüge – gilt für alles, auch Fahrzeuge und Granaten.", "Gravity in percent (100 = normal). 40 = moon: high jumps, long flights – applies to everything incl. vehicles and grenades."), 20, 100, o.GravityPct, "%"));
            foreach (Control c in new Control[] { _fmSet, _fmInfo }) _cFunMap.Controls.Add(c);
            foreach (Control c in _fmRows) _cFunMap.Controls.Add(c);
            _cFunMap.HeaderControls.Add(_fmSet);
            _cFunMap.Layouter = delegate (int w)
            {
                _fmSet.Location = new Point(w - Dpi.D(20) - _fmSet.Width, Dpi.D(9));
                _cFunMap.HeaderReserve = _fmSet.Width + Dpi.D(28);
                bool on = FunSets.SetNameOf(_curRot) != null;
                _fmInfo.Visible = !on; foreach (Control c in _fmRows) c.Visible = on;
                int y = _cFunMap.HeaderH - Dpi.D(4);
                if (!on) { int ph = _fmInfo.HeightFor(w - Dpi.D(44)); _fmInfo.SetBounds(Dpi.D(22), y, w - Dpi.D(44), ph); return y + ph + Dpi.D(12); }
                foreach (Control c in _fmRows)
                {
                    if (c is ButtonRow) { int h = ((ButtonRow)c).LayoutFor(w - Dpi.D(40)); c.SetBounds(Dpi.D(20), y, w - Dpi.D(40), h); y += h + Dpi.D(10); }
                    else if (c is Para) { int h = ((Para)c).HeightFor(w - Dpi.D(44)); c.SetBounds(Dpi.D(22), y, w - Dpi.D(44), h); y += h + Dpi.D(6); }
                    else { c.SetBounds(Dpi.D(6), y, w - Dpi.D(12), c.Height); y += c.Height; }
                }
                return y + Dpi.D(10);
            };
            RefreshFunSetItems();
        }
        FormRow FunNum(string key, string label, string tip, int min, int max, int val, string unit)
        {
            NumberBox n = new NumberBox(min, max, val, unit); n.ValueChanged += delegate { OnFunMapEdit(); };
            _funEd[key] = n; return new FormRow(label, tip, n, n.Width, false);
        }

        FormRow FunBool(string key, string label, string tip, bool val)
        {
            ToggleSwitch t = new ToggleSwitch(); t.SetSilent(val); t.CheckedChanged += delegate { OnFunMapEdit(); };
            _funEd[key] = t; return new FormRow(label, tip, t, t.Width, false);
        }

        static readonly string[] RuleNames = { "Alle Waffen|All weapons", "Nur Messer|Knife only", "Nur Pistole (+ Messer)|Pistol only (+ knife)", "Nur C4 (+ Messer)|C4 only (+ knife)", "Nur Panzerfaust (+ Messer)|Rocket launcher only (+ knife)", "Nur Scharfschützengewehr (+ Messer)|Sniper rifle only (+ knife)", "Nur Schrotflinte (+ Messer)|Shotgun only (+ knife)", "Nur MG (+ Messer)|Machine gun only (+ knife)", "Nur Handgranaten (+ Messer)|Hand grenades only (+ knife)", "Nur Defibrillator (+ Messer)|Defibrillator only (+ knife)" };
        static string RuleName(int i) { string[] p = RuleNames[Math.Max(0, Math.Min(RuleNames.Length - 1, i))].Split('|'); return T(p[0], p[1]); }

        FormRow FunRule(string key, int val)
        {
            List<string> items = new List<string>(); for (int i = 0; i < RuleNames.Length; i++) items.Add(RuleName(i));
            SelectBox s = new SelectBox(null, Dpi.D(230)); s.SetItems(items, RuleName(val));
            s.SelectionChanged += delegate { OnFunMapEdit(); };
            _funEd[key] = s;
            return new FormRow(T("Waffen-Regel", "Weapon rule"), T("Alle Klassen bekommen nur noch diese Waffenart – dazu Messer und Fallschirm. Freischaltungen und Klassenwaffen fallen weg; Bots spielen mit denselben Regeln.\nZ. B. „Nur Scharfschützengewehr“ für eine Sniper-Runde oder „Nur Messer“ fürs Finale.", "All classes only get this weapon type – plus knife and parachute. Unlocks and class weapons are dropped; bots play by the same rules.\nE.g. “Sniper rifle only” for a sniper round or “Knife only” for the finale."), s, s.Width, false);
        }

        static int RuleIndex(string label) { for (int i = 0; i < RuleNames.Length; i++) if (RuleName(i) == label) return i; return 0; }

        FunOptions ReadFunEditors()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Control> kv in _funEd)
                d[kv.Key] = kv.Value is ToggleSwitch ? (((ToggleSwitch)kv.Value).Checked ? "1" : "0")
                          : kv.Value is SelectBox ? RuleIndex(((SelectBox)kv.Value).SelectedItem).ToString() : ((NumberBox)kv.Value).Value.ToString();
            FunOptions o = FunOptions.Neutral(); Dictionary<string, string> all = o.ToDict();
            foreach (KeyValuePair<string, string> kv in d) all[kv.Key] = kv.Value;
            return FunOptions.FromDict(all);
        }

        void SetFunEditors(FunOptions o)
        {
            bool old = _applying; _applying = true;
            Dictionary<string, string> d = o.ToDict();
            foreach (KeyValuePair<string, Control> kv in _funEd)
            {
                string v; if (!d.TryGetValue(kv.Key, out v)) continue;
                if (kv.Value is ToggleSwitch) ((ToggleSwitch)kv.Value).SetSilent(v == "1");
                else if (kv.Value is SelectBox) ((SelectBox)kv.Value).SetSilent(RuleName(Util.ToInt(v, 0)));
                else ((NumberBox)kv.Value).SetSilent(Util.ToInt(v, 0));
            }
            _applying = old;
        }

        // Auswahlliste: Normal, alle Sets, Neues Set
        void RefreshFunSetItems()
        {
            if (_fmSet == null) return;
            List<string> items = new List<string> { NormalLabel };
            items.AddRange(FunSets.Names());
            items.Add(NewSetLabel);
            string cur = FunSets.SetNameOf(_curRot);
            _fmSet.SetItems(items, cur != null && FunSets.Get(cur) != null ? cur : NormalLabel);
        }

        // Auswahl in der Rotation geändert
        void UpdateFunMapUi()
        {
            if (_cFunMap == null) return;
            bool old = _applying; _applying = true;
            RefreshFunSetItems();
            _applying = old;
            string n = FunSets.SetNameOf(_curRot);
            Dictionary<string, string> r = n != null ? FunSets.Get(n) : null;
            SetFunEditors(r != null ? FunOptions.FromDict(r) : FunOptions.Neutral());
            UpdateSetInfo();
        }

        void UpdateSetInfo()
        {
            string n = FunSets.SetNameOf(_curRot);
            if (n == null) return;
            int here = 0; foreach (MapEntry m in _st.Maps) if (string.Equals(FunSets.SetNameOf(m), n, StringComparison.OrdinalIgnoreCase)) here++;
            _fmSetInfo.Text = here > 1 ? T("Gilt für ", "Used by ") + here + T(" Maps dieser Rotation – Änderungen gelten für alle.", " maps of this rotation – changes apply to all.")
                                       : T("Änderungen gelten für alle Maps mit diesem Set.", "Changes apply to all maps with this set.");
        }

        void OnFunSetPicked()
        {
            if (_applying || _curRot == null) return;
            string pick = _fmSet.SelectedItem;
            if (pick == NewSetLabel)
            {
                string name = DarkDialog.Input(this, T("Neues Regel-Set", "New rule set"), T("Name, z. B. „C4-Party“ oder „Nur Messer“:", "Name, e.g. “C4 party” or “Knife only”:"), FunSets.UniqueName(T("Neues Regel-Set", "New rule set")));
                name = FunSets.Clean(name);
                if (name.Length == 0) { UpdateFunMapUi(); return; }
                name = FunSets.UniqueName(name);
                FunSets.Set(name, FunOptions.Neutral().ToDict());
                _curRot.Fun = FunSets.Ref(name);
                Toast(T("Regel-Set „", "Rule set “") + name + T("“ angelegt – unten die Regeln einstellen.", "” created – set the rules below."), ToastKind.Info);
            }
            else if (pick == NormalLabel || pick == null) _curRot.Fun = null;
            else _curRot.Fun = FunSets.Ref(pick);
            UpdateFunMapUi(); RefreshRot(); MarkDirty(); LayoutMapRight();
        }

        void OnFunMapEdit()
        {
            if (_applying || _curRot == null) return;
            string n = FunSets.SetNameOf(_curRot);
            if (n == null) return;
            FunSets.Set(n, ReadFunEditors().ToDict());
            RefreshRot(); MarkDirty();
        }

        void RenameFunSet()
        {
            string n = FunSets.SetNameOf(_curRot); if (n == null) return;
            string to = FunSets.Clean(DarkDialog.Input(this, T("Regel-Set umbenennen", "Rename rule set"), T("Neuer Name:", "New name:"), n));
            if (to.Length == 0 || to == n) return;
            if (!FunSets.Rename(n, to)) { Toast(T("Diesen Namen gibt es schon.", "This name already exists."), ToastKind.Warn); return; }
            foreach (MapEntry m in _st.Maps) if (string.Equals(FunSets.SetNameOf(m), n, StringComparison.OrdinalIgnoreCase)) m.Fun = FunSets.Ref(to);
            RewriteSetRefsInOtherMods(n, to);
            UpdateFunMapUi(); RefreshRot(); MarkDirty();
        }

        void CopyFunSet()
        {
            string n = FunSets.SetNameOf(_curRot); if (n == null) return;
            string to = FunSets.UniqueName(n + T(" Kopie", " copy"));
            FunSets.Set(to, new Dictionary<string, string>(FunSets.Get(n), StringComparer.OrdinalIgnoreCase));
            _curRot.Fun = FunSets.Ref(to);
            UpdateFunMapUi(); RefreshRot(); MarkDirty();
            Toast(T("Kopie „", "Copy “") + to + T("“ gilt jetzt nur für diese Map.", "” now applies to this map only."), ToastKind.Info);
        }

        void DeleteFunSet()
        {
            string n = FunSets.SetNameOf(_curRot); if (n == null) return;
            int here = 0; foreach (MapEntry m in _st.Maps) if (string.Equals(FunSets.SetNameOf(m), n, StringComparison.OrdinalIgnoreCase)) here++;
            if (!DarkDialog.Confirm(this, T("Regel-Set löschen", "Delete rule set"), T("„", "“") + n + T("“ löschen? ", "” will be deleted. ") + (here > 1 ? here + T(" Maps dieser Rotation", " maps of this rotation") : T("Diese Map", "This map")) + T(" (und Maps in anderen Mods mit diesem Set) spielen danach mit normalen Regeln.", " (and maps in other mods with this set) then play with normal rules."), T("Löschen", "Delete"), true)) return;
            FunSets.Delete(n);
            foreach (MapEntry m in _st.Maps) if (string.Equals(FunSets.SetNameOf(m), n, StringComparison.OrdinalIgnoreCase)) m.Fun = null;
            RewriteSetRefsInOtherMods(n, null);
            UpdateFunMapUi(); RefreshRot(); MarkDirty(); LayoutMapRight();
        }

        void GiveSetToAll()
        {
            string n = FunSets.SetNameOf(_curRot); if (n == null) return;
            foreach (MapEntry m in _st.Maps) m.Fun = FunSets.Ref(n);
            UpdateFunMapUi(); RefreshRot(); MarkDirty();
        }

        // Set einer Map direkt zuweisen (Rechtsklick in der Rotation)
        void AssignFunSet(MapEntry m, string name)
        {
            m.Fun = name == null ? null : FunSets.Ref(name);
            if (m == _curRot) UpdateFunMapUi();
            RefreshRot(); MarkDirty(); LayoutMapRight();
        }

        // Umbenennen/Löschen auch in den Regeldateien der übrigen Mods nachziehen
        void RewriteSetRefsInOtherMods(string from, string to)
        {
            foreach (string mod in _mods)
            {
                if (mod == _mod) continue;
                string f = Path.Combine(Path.Combine(Path.Combine(Path.Combine(GameDir, "mods"), mod), "settings"), MapRules.FileName);
                try
                {
                    if (!File.Exists(f)) continue;
                    List<string> lines = new List<string>();
                    foreach (string l in File.ReadAllLines(f))
                    {
                        if (l.EndsWith("|" + FunSets.SetKey + "=" + from, StringComparison.OrdinalIgnoreCase)) { if (to != null) lines.Add(l.Substring(0, l.Length - from.Length) + to); }
                        else lines.Add(l);
                    }
                    File.WriteAllLines(f, lines.ToArray());
                }
                catch { }
            }
        }

        // Rotationszeile: Name des Sets
        static string FunShort(MapEntry m)
        {
            if (m == null || m.Fun == null) return null;
            string n = FunSets.SetNameOf(m);
            if (n != null) return FunSets.Get(n) != null ? n : null;
            return FunPack.Describe(FunOptions.FromDict(m.Fun)).Replace("!", "");
        }
    }
}