// Hauptfenster: Spaßregeln pro Map (rechts neben der Rotation) - BF2 Manager. C# 5.
// Jede Map der Rotation kann eigene Regeln haben – einzelne, mehrere oder alle. Der Manager baut daraus beim Start
// automatisch einen kleinen Aufsatz (<Mod>_fun_xxxxxx) und startet den Server nur neu, wenn die nächste Map andere Regeln braucht.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BF2Coop
{
    public partial class MainForm
    {
        Card _cFunMap; ToggleSwitch _fmToggle; TextLabel _fmLabel; Para _fmInfo; ButtonRow _fmPresets;
        readonly List<Control> _fmRows = new List<Control>();
        readonly Dictionary<string, Control> _funEd = new Dictionary<string, Control>();

        void BuildFunMapCard()
        {
            _cFunMap = new Card(T("Spaßregeln", "Fun rules"), G.Bolt);
            _fmToggle = new ToggleSwitch();
            _fmLabel = new TextLabel(T("Eigene Regeln", "Custom rules"), Theme.BodyB);
            Theme.Tip.SetToolTip(_fmToggle, T("Spaßregeln für diese Map\nAus: normales Spiel.\nAn: nur für diese Map gelten die Regeln unten – z. B. nur C4 mit 400 Wucht. Der Server startet dafür vor der Map kurz neu (etwa 20 Sekunden); Mitspieler mit „Automatisch beitreten“ sind danach von selbst wieder drin.", "Fun rules for this map\nOff: normal game.\nOn: the rules below apply to this map only – e.g. C4 only with force 400. The server restarts briefly before the map (about 20 seconds); players with “Join automatically” are back in by themselves."));
            _fmToggle.CheckedChanged += delegate { OnFunMapToggle(); };
            _fmInfo = new Para(T("Normale Regeln. Einschalten, um für genau diese Map einzelne oder alle Spaßregeln zu setzen.", "Normal rules. Switch on to set some or all fun rules for exactly this map.")); _fmInfo.Color = Theme.Dim;
            FunOptions o = FunOptions.Neutral();
            _fmPresets = new ButtonRow();
            foreach (string[] p in new string[][] { new[] { "c4", "Nur C4", "C4 only" }, new[] { "rocket", "Raketen-Chaos", "Rocket chaos" }, new[] { "sniper", "Sniper-Duell", "Sniper duel" }, new[] { "infantry", "Nur Infanterie", "Infantry only" }, new[] { "lava", "Boden ist Lava", "Floor is lava" }, new[] { "all", "Alles Spaß", "All the fun" }, new[] { "normal", "Zurücksetzen", "Reset" } })
            {
                string id = p[0];
                FlatButton b = new FlatButton(T(p[1], p[2]), id == "normal" ? G.Undo : null, BtnKind.Ghost); b.Height = Dpi.D(30); b.Fit();
                b.Click += delegate { ApplyFunPreset(id); };
                _fmPresets.Add(b);
            }
            Theme.Tip.SetToolTip(_fmPresets, T("Schnellwahl – danach einzelne Werte anpassen.", "Quick pick – then adjust single values."));
            _fmRows.Add(_fmPresets);
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
            foreach (Control c in new Control[] { _fmToggle, _fmLabel, _fmInfo }) _cFunMap.Controls.Add(c);
            foreach (Control c in _fmRows) _cFunMap.Controls.Add(c);
            _cFunMap.HeaderControls.Add(_fmToggle); _cFunMap.HeaderControls.Add(_fmLabel);
            _cFunMap.Layouter = delegate (int w)
            {
                _fmToggle.Location = new Point(w - Dpi.D(20) - _fmToggle.Width, Dpi.D(13));
                _fmLabel.Location = new Point(_fmToggle.Left - Dpi.D(8) - _fmLabel.Width, Dpi.D(9));
                _cFunMap.HeaderReserve = w - _fmLabel.Left - Dpi.D(8);
                bool on = _curRot != null && _curRot.Fun != null;
                _fmInfo.Visible = !on; foreach (Control c in _fmRows) c.Visible = on;
                int y = _cFunMap.HeaderH - Dpi.D(4);
                if (!on) { int ph = _fmInfo.HeightFor(w - Dpi.D(44)); _fmInfo.SetBounds(Dpi.D(22), y, w - Dpi.D(44), ph); return y + ph + Dpi.D(12); }
                foreach (Control c in _fmRows)
                {
                    if (c is ButtonRow) { int h = ((ButtonRow)c).LayoutFor(w - Dpi.D(40)); c.SetBounds(Dpi.D(20), y + Dpi.D(2), w - Dpi.D(40), h); y += h + Dpi.D(10); }
                    else { c.SetBounds(Dpi.D(6), y, w - Dpi.D(12), c.Height); y += c.Height; }
                }
                return y + Dpi.D(10);
            };
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
            SelectBox s = new SelectBox(null, Dpi.D(300)); s.SetItems(items, RuleName(val));
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

        // Auswahl in der Rotation geändert
        void UpdateFunMapUi()
        {
            if (_cFunMap == null) return;
            bool old = _applying; _applying = true;
            _fmToggle.SetSilent(_curRot != null && _curRot.Fun != null);
            _applying = old;
            SetFunEditors(_curRot != null && _curRot.Fun != null ? FunOptions.FromDict(_curRot.Fun) : FunOptions.Neutral());
        }

        void OnFunMapToggle()
        {
            if (_applying || _curRot == null) return;
            _curRot.Fun = _fmToggle.Checked ? FunOptions.Neutral().ToDict() : null;
            SetFunEditors(_curRot.Fun != null ? FunOptions.FromDict(_curRot.Fun) : FunOptions.Neutral());
            RefreshRot(); MarkDirty(); LayoutMapRight();
            if (_fmToggle.Checked) Toast(T("Spaßregeln an – Schnellwahl oder einzelne Werte setzen.", "Fun rules on – use a quick pick or set single values."), ToastKind.Info);
        }

        void OnFunMapEdit()
        {
            if (_applying || _curRot == null || _curRot.Fun == null) return;
            _curRot.Fun = ReadFunEditors().ToDict();
            RefreshRot(); MarkDirty();
        }

        // Schnellwahl: von „normal“ ausgehend ein paar Werte setzen
        void ApplyFunPreset(string id)
        {
            if (_curRot == null) return;
            FunOptions o = FunOptions.Neutral();
            if (id == "c4") { o.WeaponRule = 3; o.C4Mags = 99; o.C4Force = 400; o.BlastSoldierPct = 2; o.NoFallDamage = true; }
            else if (id == "rocket") { o.WeaponRule = 4; o.RocketForce = 250; o.RocketDamagePct = 5; o.RocketSpeedPct = 25; o.RocketMags = 50; o.RocketReloadMs = 300; o.NoFallDamage = true; }
            else if (id == "sniper") { o.WeaponRule = 5; o.SniperForce = 300; o.BlastSoldierPct = 2; o.EndlessAmmo = true; }
            else if (id == "infantry") { o.NoVehicles = true; }
            else if (id == "lava") { o.WaterKills = true; o.GravityPct = 60; o.NoFallDamage = true; o.EndlessSprint = true; }
            else if (id == "all") o = new FunOptions();
            _curRot.Fun = o.ToDict();
            SetFunEditors(o); RefreshRot(); MarkDirty(); LayoutMapRight();
        }

        // Kurzbeschreibung für die Rotationszeile
        static string FunShort(MapEntry m)
        {
            if (m.Fun == null) return null;
            FunOptions o = FunOptions.FromDict(m.Fun);
            if (o.IsNeutral()) return T("Spaßregeln: noch nichts geändert", "fun rules: nothing changed yet");
            string d = FunPack.Describe(o).Replace("!", "");
            return d.Length > 60 ? d.Substring(0, 58) + " …" : d;
        }
    }
}