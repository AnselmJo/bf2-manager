// Hauptfenster: Testmodus: Screenshots - BF2 Manager. C# 5 (.NET Framework 4.x).
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
        // ================================================================== Testmodus: Screenshots
        void RunShots()
        {
            List<Action> steps = new List<Action>();
            LanHost fake = new LanHost(); fake.Ip = "192.168.1.20"; fake.Name = "Ansis LAN Coop"; fake.Mod = "bf2all64"; fake.Map = "strike_at_karkand"; fake.Humans = 6; fake.Max = 64; fake.Running = true; fake.Version = AppInfo.Version; fake.GamePort = 16567; fake.HttpPort = Lan.HttpPort;
            LanHost fake2 = new LanHost(); fake2.Ip = "192.168.1.31"; fake2.Name = "Kater PC"; fake2.Mod = "bfp2"; fake2.Map = ""; fake2.Running = false; fake2.Version = "1.2.0"; fake2.GamePort = 16567; fake2.HttpPort = Lan.HttpPort;
            _fakeHosts = new List<LanHost> { fake, fake2 };
            CommandDialog[] dlg = new CommandDialog[1]; WelcomeDialog[] wel = new WelcomeDialog[1]; BotNowDialog[] bn = new BotNowDialog[1];
            fake.Size = 32; fake.Bots = 24; fake.ModVersion = "1.0"; fake.Id = "a1";
            LanHost fake3 = new LanHost(); fake3.Ip = "192.168.1.44"; fake3.Name = "Pirates Abend"; fake3.Mod = "bfp2"; fake3.Map = "frylar"; fake3.Humans = 2; fake3.Max = 32; fake3.Running = true; fake3.Version = AppInfo.Version; fake3.Mode = "cq"; fake3.Size = 32; fake3.Id = "b2"; fake3.GamePort = 16567; fake3.HttpPort = Lan.HttpPort;
            fake.Mode = "coop";
            _fakeHosts = new List<LanHost> { fake, fake2, fake3 };
            steps.Add(delegate { SetRole(true); ShowServerView(); if (_lbRot.Items.Count > 1) _lbRot.SelectedIndex = 1; PushUndo(); });
            steps.Add(delegate { Shot("host_server"); SelectPage(PgBots); });
            steps.Add(delegate { Shot("host_bots"); _botHost.ScrollTo(99999, false); });
            steps.Add(delegate { Shot("host_bots_scrolled"); SelectPage(PgServer); });
            steps.Add(delegate { Shot("host_options"); _advToggle.Checked = !_advToggle.Checked; });
            steps.Add(delegate { Shot("host_options_adv"); _advToggle.Checked = !_advToggle.Checked; SelectPage(PgMaps); if (_st.Maps.Count > 0) { _lbRot.SelectedIndex = 0; ApplyFunPreset("c4"); _mapRight.ScrollTo(99999, false); } });
            steps.Add(delegate { Shot("host_server_funrules"); _mapRight.ScrollTo(0, false); SelectPage(PgExtras); });
            steps.Add(delegate { Shot("host_settings"); Srv.FakeRunning = true; RefreshLive(); ShowServerView(); });
            steps.Add(delegate { Shot("host_live"); ShowResult("admin.listPlayers", "Id:255 - C. Bergqvist is remote  is an AI bot. | Id:254 - S. Parkinson is remote  is an AI bot. | Id:3 - Ansi  is remote"); });
            steps.Add(delegate { Shot("host_live_result"); _module = true; bn[0] = new BotNowDialog("Dalian Plant", _st.Defaults[16].Clone(), 16, 6, 0); bn[0].StartPosition = FormStartPosition.Manual; bn[0].Location = PointToScreen(new Point(Dpi.D(200), Dpi.D(60))); bn[0].Show(this); });
            steps.Add(delegate { Shot("botnow"); bn[0].Close(); _cmdResult.Text = ""; LayoutLive(); _forceConfig = true; SelectPage(PgMaps); });
            steps.Add(delegate { Shot("host_server_running"); SelectPage(PgServer); _vals["sv.maxPlayers"] = "48"; UpdateLiveBanner(); });
            steps.Add(delegate { Shot("host_options_running"); Srv.FakeRunning = false; _userStopped = false; RefreshLive(); });
            steps.Add(delegate { Shot("host_crashed"); SetRole(false); });
            steps.Add(delegate { RefreshServers(true); RefreshPlayHealth(); _plHost.Masonry(_plCards, 1); });
            steps.Add(delegate { Shot("play"); _fakeHosts = new List<LanHost>(); RefreshServers(true); _plHost.Masonry(_plCards, 1); });
            // Neuer Spieler ohne Profil: leerer Profilordner (nur lesend)
            steps.Add(delegate { Shot("play_empty"); Profiles.RootOverride = Path.Combine(_shotDir, "no_profiles"); RefreshPlayer(); });
            steps.Add(delegate { Shot("play_noprofile"); Profiles.RootOverride = null; RefreshPlayer(); wel[0] = new WelcomeDialog(GameDir, null); wel[0].StartPosition = FormStartPosition.Manual; wel[0].Location = PointToScreen(new Point(Dpi.D(200), Dpi.D(80))); wel[0].Show(this); });
            steps.Add(delegate { Shot("welcome"); wel[0].Close(); SelectPage(PgExtras); BeginInvoke((MethodInvoker)delegate { ReShadeHelp(); }); });
            steps.Add(delegate { Shot("reshade"); foreach (Form f in OwnedForms) if (f is DarkDialog) f.Close(); });
            steps.Add(delegate { Shot("play_settings"); _cCheck.Toggle(); });
            steps.Add(delegate { Shot("play_settings_check"); _cCheck.Toggle(); _exHost.ScrollTo(99999, false); });
            steps.Add(delegate { Shot("play_settings_scrolled"); dlg[0] = new CommandDialog(Commands.All(_vals), true); dlg[0].StartPosition = FormStartPosition.Manual; dlg[0].Location = PointToScreen(new Point(Dpi.D(160), Dpi.D(90))); dlg[0].Show(this); });
            steps.Add(delegate { Shot("commands"); dlg[0].Close(); });
            int step = 0;
            WinTimer t = new WinTimer(); t.Interval = 900;
            t.Tick += delegate
            {
                try { if (step < steps.Count) { steps[step++](); return; } }
                catch (Exception ex) { Util.Log(AppPaths.Dir, ex.ToString()); }
                t.Stop(); _dirty = false; Close();
            };
            t.Start();
        }

        void Shot(string name)
        {
            // Aus dem Fenster gezeichnet statt vom Bildschirm kopiert – klappt auch bei gesperrtem oder verdecktem Bildschirm
            using (Bitmap b = new Bitmap(ClientSize.Width, ClientSize.Height))
            {
                using (Bitmap full = new Bitmap(Width, Height))
                using (Graphics g = Graphics.FromImage(b))
                {
                    DrawToBitmap(full, new Rectangle(0, 0, Width, Height));
                    Point o = PointToScreen(Point.Empty);
                    g.DrawImage(full, Location.X - o.X, Location.Y - o.Y);
                    foreach (Form f in OwnedForms)
                    {
                        if (!f.Visible) continue;
                        using (Bitmap fb = new Bitmap(f.Width, f.Height))
                        {
                            f.DrawToBitmap(fb, new Rectangle(0, 0, f.Width, f.Height)); g.DrawImage(fb, f.Left - o.X, f.Top - o.Y);
                            Directory.CreateDirectory(_shotDir); fb.Save(Path.Combine(_shotDir, name + "_dialog.png"), System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }
                Directory.CreateDirectory(_shotDir);
                b.Save(Path.Combine(_shotDir, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
