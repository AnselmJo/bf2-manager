// Hauptfenster: Updates (GitHub-Releases) - BF2 Manager. C# 5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace BF2Coop
{
    public partial class MainForm
    {
        Card _cUpdate; Para _updInfo; FlatButton _bUpdCheck, _bUpdInstall, _bUpdNotes, _bUpdRollback; ToggleSwitch _updAuto;
        ReleaseInfo _updFound; bool _updBusy, _updExit;

        void BuildUpdateCard()
        {
            _cUpdate = new Card("Updates", G.Sync);
            _updAuto = new ToggleSwitch(); _updAuto.SetSilent(_cfg.B("AutoUpdate", true));
            FormRow rAuto = new FormRow(T("Beim Start automatisch aktualisieren", "Update automatically at start"),
                T("Sucht beim Start nach einer neuen Version auf GitHub und installiert sie. Nie während ein Server läuft oder Änderungen ungespeichert sind. Ohne Internet passiert nichts.", "Looks for a new version on GitHub at start and installs it. Never while a server runs or changes are unsaved. Without internet nothing happens."), _updAuto, _updAuto.Width, false);
            _updAuto.CheckedChanged += delegate { _cfg.Set("AutoUpdate", _updAuto.Checked); _cfg.Save(); };
            _updInfo = new Para(T("Installiert: ", "Installed: ") + AppInfo.Version); _updInfo.Color = Theme.Dim;
            _bUpdCheck = new FlatButton(T("Nach Updates suchen", "Check for updates"), G.Refresh, BtnKind.Secondary);
            _bUpdCheck.Click += delegate { CheckForUpdates(true); };
            _bUpdInstall = new FlatButton(T("Jetzt installieren", "Install now"), G.Save, BtnKind.Primary);
            _bUpdInstall.Click += delegate { if (_updFound != null) InstallUpdate(_updFound.ExeUrl, _updFound.SumsUrl, _updFound.Version, _updFound.Notes); };
            _bUpdNotes = new FlatButton(T("Was ist neu?", "What's new?"), G.Info, BtnKind.Ghost);
            Theme.Tip.SetToolTip(_bUpdNotes, T("Zeigt die Änderungen der neuen Version, bevor du sie installierst.", "Shows the changes in the new version before you install it."));
            _bUpdNotes.Click += delegate { if (_updFound != null) NotesDialog.Show(this, T("Neu in Version ", "New in version ") + _updFound.Version, _updFound.Notes, null); };
            _bUpdRollback = new FlatButton(T("Vorherige Version", "Previous version"), G.Undo, BtnKind.Ghost);
            Theme.Tip.SetToolTip(_bUpdRollback, T("Stellt die Version vor dem letzten Update wieder her.", "Restores the version before the last update."));
            _bUpdRollback.Click += delegate { Rollback(); };
            ButtonRow br = Buttons(_bUpdCheck, _bUpdInstall, _bUpdNotes, _bUpdRollback);
            foreach (Control c in new Control[] { rAuto, _updInfo, br }) _cUpdate.Controls.Add(c);
            _cUpdate.Layouter = Stack(_cUpdate, delegate
            {
                _bUpdInstall.Visible = _bUpdNotes.Visible = _updFound != null; _bUpdRollback.Visible = Updater.CanRollback;
                foreach (Control b in new Control[] { _bUpdInstall, _bUpdNotes, _bUpdRollback }) if (!b.Visible) br.Controls.Remove(b); else if (!br.Controls.Contains(b)) br.Controls.Add(b);
                return new List<Control> { rAuto, _updInfo, br };
            });
        }

        // manual = vom Knopf (mit Rückmeldung); sonst still beim Start
        void CheckForUpdates(bool manual)
        {
            if (_updBusy) return;
            _updBusy = true;
            if (manual) { _updInfo.Text = T("Suche nach Updates …", "Checking for updates …"); RelayoutExtras(); }
            bool pre = false;
            ThreadPool.QueueUserWorkItem(delegate
            {
                ReleaseInfo r = null; string err = null;
                try { r = Updater.Latest(pre, manual ? 15000 : 5000); } catch (Exception ex) { err = ex.Message; }
                try { BeginInvoke((MethodInvoker)delegate { UpdateChecked(r, err, manual); }); } catch { }
            });
        }

        void UpdateChecked(ReleaseInfo r, string err, bool manual)
        {
            _updBusy = false;
            _cfg.Set("UpdateChecked", DateTime.Now.ToString("yyyy-MM-dd HH:mm")); _cfg.Save();
            _updFound = r;
            if (r == null)
            {
                _updInfo.Text = T("Installiert: ", "Installed: ") + AppInfo.Version + (err != null ? T(" · Prüfung nicht möglich: ", " · check not possible: ") + err : T(" · aktuell", " · up to date"));
                if (manual) Toast(err != null ? T("Keine Verbindung zu GitHub.", "No connection to GitHub.") : T("Du hast die neueste Version.", "You have the latest version."), err != null ? ToastKind.Warn : ToastKind.Ok);
                RelayoutExtras();
                return;
            }
            _updInfo.Text = T("Neue Version ", "New version ") + r.Version + T(" verfügbar (installiert: ", " available (installed: ") + AppInfo.Version + ")";
            _nav[PgExtras].Badge = "↑"; _nav[PgExtras].BadgeColor = Theme.Accent; _nav[PgExtras].Invalidate();
            RelayoutExtras();
            bool canAuto = !manual && _cfg.B("AutoUpdate", true) && !_running && !_dirty && !Srv.GameRunning() && _shotDir == null;
            if (canAuto) InstallUpdate(r.ExeUrl, r.SumsUrl, r.Version, r.Notes);
            else if (!manual) Toast(T("Update ", "Update ") + r.Version + T(" verfügbar – Extras › Updates.", " available – Extras › Updates."), ToastKind.Info);
        }

        void RelayoutExtras() { if (_exHost != null) _exHost.Masonry(_exCards, 2); }

        // Herunterladen, prüfen, einspielen, neu starten (gilt auch für Updates vom Server-PC im LAN)
        void InstallUpdate(string exeUrl, string sumsUrl, string version, string notes)
        {
            if (_updBusy) return;
            if (_running) { Toast(T("Bitte zuerst den Server beenden.", "Please stop the server first."), ToastKind.Warn); return; }
            if (_dirty && !ConfirmDiscard()) return;
            _updBusy = true;
            Toast(T("Lade Version ", "Downloading version ") + version + " …", ToastKind.Info);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string file = null, err = null;
                try
                {
                    int lastPct = -1;
                    file = Updater.Download(exeUrl, sumsUrl, delegate (long done, long total)
                    {
                        int pct = total > 0 ? (int)(done * 100 / total) : 0;
                        if (pct / 10 != lastPct / 10) { lastPct = pct; try { BeginInvoke((MethodInvoker)delegate { _updInfo.Text = T("Lade ", "Downloading ") + version + " … " + pct + " %"; }); } catch { } }
                    }, 60000);
                }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _updBusy = false;
                        if (err != null) { Toast(T("Update fehlgeschlagen: ", "Update failed: ") + err, ToastKind.Error); _updInfo.Text = err; RelayoutExtras(); return; }
                        try
                        {
                            File.WriteAllText(Updater.NotesFile, version + "\n" + (notes ?? ""));
                            Updater.Install(file);
                            Updater.Restart(AppInfo.Version);
                            _updExit = true;
                            Close();
                        }
                        catch (Exception ex) { Toast(T("Update fehlgeschlagen: ", "Update failed: ") + ex.Message, ToastKind.Error); }
                    });
                }
                catch { }
            });
        }

        void Rollback()
        {
            if (!Updater.CanRollback) return;
            if (!DarkDialog.Confirm(this, T("Vorherige Version", "Previous version"), T("Die vorherige Version wiederherstellen und neu starten?", "Restore the previous version and restart?"), T("Wiederherstellen", "Restore"), false)) return;
            try { Updater.Rollback(); Updater.Restart(AppInfo.Version); _updExit = true; Close(); }
            catch (Exception ex) { Toast(ex.Message, ToastKind.Error); }
        }

        // Nach einem Update: Änderungen einmal anzeigen
        void ShowUpdateNotes()
        {
            Updater.CleanupAfterStart();
            if (Program.UpdatedFrom == null) return;
            string notes = "";
            try { if (File.Exists(Updater.NotesFile)) { notes = File.ReadAllText(Updater.NotesFile); File.Delete(Updater.NotesFile); } } catch { }
            int nl = notes.IndexOf('\n');
            if (nl >= 0) notes = notes.Substring(nl + 1);
            if (notes.Trim().Length == 0) notes = T("Das Update wurde installiert.", "The update was installed.");
            NotesDialog.Show(this, T("Aktualisiert auf ", "Updated to ") + AppInfo.Version, notes,
                _mods.Count > 0 ? T("Falls ein Server läuft: einmal neu starten, damit das Bot-Modul aktuell ist.", "If a server is running: restart it once so the bot module is up to date.") : null);
        }
    }
}
