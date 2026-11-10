// Hauptfenster: Spaßrunde (Spaß-Aufsatz für den gewählten Mod) – nur bei „Erweitert“ sichtbar - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace BF2Coop
{
    public partial class MainForm
    {
        Card _cFun; Para _funInfo, _funLog; FlatButton _bFunBuild, _bFunBack, _bFunRemove; bool _funBusy;
        readonly Dictionary<string, Control> _funEd = new Dictionary<string, Control>();

        // Basis des gewählten Mods: bei einem Aufsatz dessen Basis-Mod, sonst der Mod selbst
        string FunBase { get { string b = FunMods.BaseOf(GameDir, _mod); return b ?? _mod; } }

        void BuildFunCard()
        {
            _cFun = MakeCard("srv.fun", T("Spaßrunde", "Fun round"), G.Bolt, true);
            FunOptions o = LoadFunOptions();
            _funInfo = new Para(""); _funInfo.Color = Theme.Dim;
            List<Control> rows = new List<Control>();
            rows.Add(_funInfo);
            rows.Add(FunNum("C4Mags", T("C4 pro Spieler", "C4 per player"), T("Anzahl C4-Ladungen pro Leben (Original 5). Bis zu 30 liegen gleichzeitig in der Welt.", "C4 charges per life (original 5). Up to 30 can lie in the world at once."), 0, 99, o.C4Mags, ""));
            rows.Add(FunNum("C4Force", T("C4-Wucht (Trampolin)", "C4 force (trampoline)"), T("Wie stark die C4-Explosion Soldaten und Fahrzeuge wegschleudert (Original 17). 300 = C4-Trampolin.", "How hard the C4 explosion throws soldiers and vehicles (original 17). 300 = C4 trampoline."), 0, 5000, o.C4Force, ""));
            rows.Add(FunNum("BlastSoldierPct", T("Sprengschaden an Soldaten", "Blast damage to soldiers"), T("Schaden von C4, Sprung-Claymore und Raketen-Explosion an Soldaten in Prozent (Original 100). Klein = man fliegt, statt zu sterben. Volltreffer mit der Rakete zählen weiter voll.", "Damage of C4, jump claymore and rocket blast to soldiers in percent (original 100). Low = you fly instead of dying. Direct rocket hits still count fully."), 0, 100, o.BlastSoldierPct, "%"));
            rows.Add(FunBool("NoFallDamage", T("Kein Fallschaden", "No fall damage"), T("Stürze und Landungen verletzen nicht mehr.", "Falls and landings no longer hurt."), o.NoFallDamage));
            rows.Add(FunBool("ExtraExplosives", T("Spec Ops: Mega-Mine und Sprung-Claymore", "Spec Ops: mega mine and jump claymore"), T("Spec Ops bekommt zusätzlich die Panzermine (wirft Fahrzeuge meterweit) und die Claymore als Sprungfalle für Soldaten. Beide liegen wie C4 auf Taste 5 – mehrmals drücken wechselt.\nNeue eigene Waffen lässt Battlefield 2 ohne Spieldateien nicht zu, daher diese beiden.", "Spec Ops also gets the anti-tank mine (throws vehicles far) and the claymore as a jump trap for soldiers. Both sit on key 5 like C4 – press repeatedly to switch.\nBattlefield 2 does not allow new weapons without game files, hence these two."), o.ExtraExplosives));
            rows.Add(FunBool("RocketJump", T("Raketensprung", "Rocket jump"), T("Die Panzerfaust-Explosion schleudert stark und verletzt Soldaten kaum – auf den Boden schießen und springen. Volltreffer töten weiter.", "The rocket blast throws hard and barely hurts soldiers – shoot the ground and jump. Direct hits still kill."), o.RocketJump));
            rows.Add(FunNum("RocketSpeedPct", T("Raketentempo", "Rocket speed"), T("Höchsttempo der Panzerfaust-Raketen in Prozent (100 = normal). 25 = langsam genug zum Ausweichen.", "Top speed of rockets in percent (100 = normal). 25 = slow enough to dodge."), 10, 100, o.RocketSpeedPct, "%"));
            rows.Add(FunNum("RocketMags", T("Raketen pro Spieler", "Rockets per player"), T("Anzahl Raketen pro Leben (Original 5–6).", "Rockets per life (original 5–6)."), 0, 99, o.RocketMags, ""));
            rows.Add(FunBool("FastReload", T("Panzerfaust lädt sofort nach", "Instant rocket reload"), T("Nachladen in 0,3 statt 3 Sekunden.", "Reload in 0.3 instead of 3 seconds."), o.FastReload));
            rows.Add(FunBool("EndlessAmmo", T("Fast unendlich Munition", "Nearly endless ammo"), T("Alle Handwaffen bekommen 99 Magazine.", "All hand weapons get 99 magazines."), o.EndlessAmmo));
            rows.Add(FunBool("EndlessSprint", T("Sprinten ohne Ausdauer", "Sprint without stamina"), T("Sprinten erschöpft nicht mehr, Springen kostet keine Ausdauer.", "Sprinting never runs out, jumping costs no stamina."), o.EndlessSprint));
            rows.Add(FunNum("GravityPct", T("Schwerkraft (hohe Sprünge)", "Gravity (high jumps)"), T("Schwerkraft in Prozent (100 = normal). 40 = Mond: hohe Sprünge, weite Flüge – gilt für alles, auch Fahrzeuge und Granaten.", "Gravity in percent (100 = normal). 40 = moon: high jumps, long flights – applies to everything incl. vehicles and grenades."), 20, 100, o.GravityPct, "%"));
            _bFunBuild = new FlatButton(T("Erstellen und verwenden", "Create and use"), G.Bolt, BtnKind.Primary);
            Theme.Tip.SetToolTip(_bFunBuild, T("Erzeugt den kleinen Mod „<Mod>_fun“ (verweist auf den Basis-Mod, ändert nichts an ihm) und wählt ihn aus. Danach den Server (neu) starten. Mitspieler bekommen ihn beim „Beitreten“ automatisch (wenige KB).", "Creates the small mod “<mod>_fun” (links to the base mod, does not change it) and selects it. Then (re)start the server. Players get it automatically when they “Join” (a few KB)."));
            _bFunBuild.Click += delegate { BuildFun(); };
            _bFunBack = new FlatButton(T("Zurück zum Basis-Mod", "Back to base mod"), G.Undo, BtnKind.Secondary);
            _bFunBack.Click += delegate { string b = FunMods.BaseOf(GameDir, _mod); if (b != null && _mods.Contains(b)) SwitchMod(b); };
            _bFunRemove = new FlatButton(T("Entfernen", "Remove"), null, BtnKind.Ghost);
            _bFunRemove.Click += delegate { RemoveFun(); };
            ButtonRow br = Buttons(_bFunBuild, _bFunBack, _bFunRemove);
            rows.Add(br);
            _funLog = new Para(""); _funLog.Color = Theme.Text; rows.Add(_funLog);
            foreach (Control c in rows) _cFun.Controls.Add(c);
            _cFun.Layouter = Stack(_cFun, delegate
            {
                List<Control> l = new List<Control>();
                foreach (Control c in rows) if (c != _funLog || _funLog.Text.Length > 0) l.Add(c);
                return l;
            });
            _cFun.Hidden = !_advToggle.Checked;
            _srvCards.Add(_cFun); _srvHost.Canvas.Controls.Add(_cFun);
            HookCollapse(_cFun, _srvHost, _srvCards, 2);
            UpdateFunCard();
        }

        FormRow FunNum(string key, string label, string tip, int min, int max, int val, string unit)
        {
            NumberBox n = new NumberBox(min, max, val, unit); n.ValueChanged += delegate { SaveFunOptions(); };
            _funEd[key] = n; return new FormRow(label, tip, n, n.Width, false);
        }

        FormRow FunBool(string key, string label, string tip, bool val)
        {
            ToggleSwitch t = new ToggleSwitch(); t.SetSilent(val); t.CheckedChanged += delegate { SaveFunOptions(); };
            _funEd[key] = t; return new FormRow(label, tip, t, t.Width, false);
        }

        FunOptions LoadFunOptions()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> kv in new FunOptions().ToDict()) { string s = _cfg.S("Fun." + kv.Key, null); if (s != null) d[kv.Key] = s; }
            return FunOptions.FromDict(d);
        }

        FunOptions ReadFunEditors()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Control> kv in _funEd)
                d[kv.Key] = kv.Value is ToggleSwitch ? (((ToggleSwitch)kv.Value).Checked ? "1" : "0") : ((NumberBox)kv.Value).Value.ToString();
            return FunOptions.FromDict(d);
        }

        void SaveFunOptions()
        {
            foreach (KeyValuePair<string, string> kv in ReadFunEditors().ToDict()) _cfg.Set("Fun." + kv.Key, kv.Value);
            _cfg.Save();
        }

        void UpdateFunCard()
        {
            if (_cFun == null) return;
            string b = FunMods.BaseOf(GameDir, _mod);
            bool exists = Directory.Exists(Path.Combine(GameDir, "mods", FunPack.NameFor(FunBase)));
            _funInfo.Text = b != null
                ? T("Aktiv: Spaß-Aufsatz „", "Active: fun pack “") + _mod + T("“ auf Basis von „", "” based on “") + b + T("“. Werte ändern und „Erstellen“ baut ihn neu; danach Server neu starten.", "”. Change values and “Create” rebuilds it; then restart the server.")
                : T("Für seltene Spaßrunden: erzeugt aus „", "For the odd fun round: creates a small mod from “") + _mod + T("“ einen kleinen Mod „", "”, “") + FunPack.NameFor(_mod) + T("“ mit diesen Regeln. Der Basis-Mod bleibt unverändert; Mitspieler holen den Aufsatz beim „Beitreten“ automatisch.", "”, with these rules. The base mod stays unchanged; players fetch the pack automatically when they “Join”.");
            _bFunBack.Visible = b != null;
            _bFunRemove.Visible = exists;
            _bFunBuild.Enabled = !_funBusy && _mod.Length > 0;
            _cFun.PerformLayout(); _cFun.Invalidate();
        }

        void SwitchMod(string mod)
        {
            _selMod.SetItems(_mods, mod);
            SwitchContext(mod, _coop);
            UpdateFunCard();
        }

        void BuildFun()
        {
            if (_funBusy || _mod.Length == 0) return;
            string baseMod = FunBase, name = FunPack.NameFor(baseMod);
            if (_running && (string.Equals(_mod, name, StringComparison.OrdinalIgnoreCase)))
            { Toast(T("Bitte zuerst den Server beenden – der laufende Server nutzt den Aufsatz.", "Please stop the server first – the running server uses the pack."), ToastKind.Warn); return; }
            if (Srv.GameRunning()) { Toast(T("Bitte zuerst Battlefield 2 beenden.", "Please close Battlefield 2 first."), ToastKind.Warn); return; }
            if (!Installer.CanWrite(Path.Combine(GameDir, "mods"))) { NoRights(); return; }
            FunOptions o = ReadFunEditors(); SaveFunOptions();
            _funBusy = true; _funLog.Text = T("Erstelle „", "Creating “") + name + T("“ …", "” …"); UpdateFunCard(); _srvHost.Masonry(_srvCards, 2);
            string gd = GameDir;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<string> log = null; string err = null;
                try { log = FunPack.Build(gd, baseMod, o); }
                catch (Exception ex) { err = ex.Message; Util.Log(AppPaths.Dir, ex.ToString()); }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _funBusy = false;
                        if (err != null) { _funLog.Text = "⚠ " + err; UpdateFunCard(); _srvHost.Masonry(_srvCards, 2); return; }
                        _funLog.Text = "✓ " + string.Join("\n✓ ", log.ToArray());
                        if (!_mods.Contains(name)) { _mods.Add(name); _cfg.Set("Mods", _mods); _cfg.Save(); }
                        RefreshStatus();
                        SwitchMod(name);
                        _srvHost.Masonry(_srvCards, 2);
                        Toast(T("Spaß-Aufsatz bereit: ", "Fun pack ready: ") + name + (_running ? T(" – Server neu starten, damit er gilt.", " – restart the server to apply it.") : ""), ToastKind.Ok);
                    });
                }
                catch { }
            });
        }

        void RemoveFun()
        {
            string baseMod = FunBase, name = FunPack.NameFor(baseMod);
            if (_running && string.Equals(_mod, name, StringComparison.OrdinalIgnoreCase)) { Toast(T("Bitte zuerst den Server beenden.", "Please stop the server first."), ToastKind.Warn); return; }
            if (!DarkDialog.Confirm(this, T("Spaß-Aufsatz entfernen", "Remove fun pack"), T("„", "“") + name + T("“ löschen? Der Basis-Mod „", "” will be deleted. The base mod “") + baseMod + T("“ bleibt unverändert.", "” stays unchanged."), T("Entfernen", "Remove"), true)) return;
            try
            {
                if (string.Equals(_mod, name, StringComparison.OrdinalIgnoreCase) && _mods.Contains(baseMod)) SwitchMod(baseMod);
                FunPack.Remove(GameDir, name);
                if (_mods.Remove(name)) { _cfg.Set("Mods", _mods); _cfg.Save(); _selMod.SetItems(_mods, _mod); }
                _funLog.Text = ""; RefreshStatus(); UpdateFunCard(); _srvHost.Masonry(_srvCards, 2);
                Toast(T("Entfernt: ", "Removed: ") + name, ToastKind.Ok);
            }
            catch (Exception ex) { Toast(T("Entfernen fehlgeschlagen: ", "Removing failed: ") + ex.Message, ToastKind.Error); }
        }
    }
}
