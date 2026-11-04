// Programmstart - BF2 Manager. C# 5 (.NET Framework 4.x).
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
    // ------------------------------------------------------------------ Programmstart
    static class Program
    {
        public static string UpdatedFrom, MovedFrom; // MovedFrom: alter Pfad, wenn sich der Manager in den Spielordner kopiert hat

        [STAThread]
        static void Main(string[] args)
        {
            if (Elevation.HandleTask(args)) return;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e) { Fail(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e) { Fail(e.ExceptionObject as Exception); };
            Native.DpiAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string shot = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--shot") shot = args[i + 1];
                if (args[i] == "--updated") UpdatedFrom = args[i + 1];
                if (args[i] == "--moved") MovedFrom = args[i + 1];
            }
            // GitHub verlangt TLS 1.2; .NET 4.x aktiviert es nicht immer von selbst
            try { System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072; } catch { }

            bool created;
            using (Mutex mx = new Mutex(true, "BF2Manager_Single", out created))
            {
                if (!created && shot == null)
                {
                    // Nach einem Update wartet die neue Version, bis die alte beendet ist
                    bool got = false;
                    if (UpdatedFrom != null || MovedFrom != null) { try { got = mx.WaitOne(20000); } catch (AbandonedMutexException) { got = true; } }
                    if (!got) { MessageBox.Show(AppInfo.Name + " is already running.", AppInfo.Name); return; }
                }
                Directory.CreateDirectory(AppPaths.Dir);
                AppPaths.Migrate();
                AppConfig cfg;
                try { cfg = File.Exists(AppPaths.Config) ? AppConfig.Load(AppPaths.Config) : AppConfig.Empty(AppPaths.Config); }
                catch { cfg = AppConfig.Empty(AppPaths.Config); }
                string lang = cfg.S("Lang", null);
                I18n.En = lang != null ? lang == "en" : !System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase);
                if (!GameLocator.IsGameDir(cfg.S("GameDir", null))) { string g = GameLocator.Find(); if (g != null) cfg.Set("GameDir", g); }
                string docs = AppPaths.SyncDocs(cfg.S("DocsDir", null), AppPaths.Docs);
                if (docs != cfg.S("DocsDir", null)) { cfg.Set("DocsDir", docs); try { cfg.Save(); } catch { } }
                Dpi.Init();
                Theme.Init();
                ToolStripManager.Renderer = new DarkRenderer();
                Application.AddMessageFilter(new WheelRouter());
                MainForm f;
                do
                {
                    I18n.En = cfg.S("Lang", I18n.En ? "en" : "de") == "en";
                    f = new MainForm(cfg, shot);
                    Application.Run(f);
                } while (f.Restart);
                GC.KeepAlive(mx);
            }
        }

        static void Fail(Exception ex)
        {
            Util.Log(AppPaths.Dir, ex != null ? ex.ToString() : "?");
            try { MessageBox.Show(I18n.T("Unerwarteter Fehler:\n", "Unexpected error:\n") + (ex != null ? ex.Message : "") + "\n\n" + Path.Combine(AppPaths.Dir, "manager_error.log"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch { }
        }
    }
}
