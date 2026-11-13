// Hauptfenster: Felder, Konstruktor, Start - BF2 Manager. C# 5 (.NET Framework 4.x).
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
    public partial class MainForm : Form
    {
        static string T(string de, string en) { return I18n.T(de, en); }
        const int PgServer = 0, PgBots = 1, PgMaps = 2, PgLive = 3, PgExtras = 4, PgPlayer = 5, PageCount = 6;

        readonly string _shotDir;
        readonly AppConfig _cfg;
        public bool Restart;
        List<string> _mods = new List<string>();
        string _mod = ""; bool _coop = true, _setupMode;
        Dictionary<string, string> _vals = Defs.Defaults(), _saved = Defs.Defaults();
        CoopState _st = new CoopState();
        List<AvailMap> _avail = new List<AvailMap>();
        readonly Dictionary<string, AvailMap> _availIx = new Dictionary<string, AvailMap>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Image> _img = new Dictionary<string, Image>();
        bool _applying, _dirty, _scanning, _running, _module, _aiDirty, _namesDirty, _busy; int _scanGen;
        DateTime _srvStart = DateTime.MinValue;
        readonly LogTail _tail = new LogTail();
        WinTimer _timer, _nextTimer;
        readonly HashSet<string> _collapsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        HeaderBar _header; SideBar _side; Panel _body; readonly Panel[] _pages = new Panel[PageCount]; readonly NavButton[] _nav = new NavButton[PageCount]; int _page = -1;
        StatusPill _pill; FlatButton _bServer, _bGame, _bSave, _bMore; SelectBox _selMod; Segmented _segType; Toast _toast;
        ContextMenuStrip _moreMenu;

        ScrollHost _srvHost; readonly List<Card> _srvCards = new List<Card>(); ToggleSwitch _advToggle; Banner _srvBanner; FlatButton _bLiveSend;
        readonly Dictionary<string, FormRow> _rows = new Dictionary<string, FormRow>(StringComparer.OrdinalIgnoreCase);

        ScrollHost _botHost; readonly List<Card> _botCards = new List<Card>(); NumberBox _hUS, _hMEC; ToggleSwitch _botAdvToggle;
        readonly Dictionary<int, BotEditor> _botEd = new Dictionary<int, BotEditor>();
        Card _cAi, _cNames; ToggleSwitch _aiOverride, _aiInclude; NumberBox _aiMax; TextBox _names; Para _aiInfo; bool _namesQuoted; string _namesPath; AiFile _ai;

        Panel _mapsBody; Card _cAvail, _cRot, _cOv; TextField _filter; MapListBox _lbAvail, _lbRot; MapPreview _preview;
        ToggleSwitch _ovToggle; TextLabel _ovLabel; BotEditor _ovEd; Para _ovInfo; FlatButton _bAdd, _bUp, _bDown, _bDel, _bClear; TextLabel _rotEmpty, _availEmpty;
        List<AvailMap> _availView = new List<AvailMap>(); ScrollHost _mapRight; MapEntry _curRot;

        ScrollHost _liveHost; readonly StatTile[] _tiles = new StatTile[4]; Card _cOffline, _cAct, _cPlayers, _cNext, _cLog;
        FlatButton _bNextMap, _bRestart, _bMapsLive, _bSay, _bSend, _bStartBig; TextField _say, _cmd; BotEditor _nextEd; ToggleSwitch _nextToggle; RichTextBox _log;
        Para _playersEmpty; readonly List<FormRow> _playerRows = new List<FormRow>(); string _playerSig = ""; string _nextId = "";

        ScrollHost _exHost; readonly List<Card> _exCards = new List<Card>(); Card _cSetup, _cUnlock; TextLabel _gameDirLabel; readonly Dictionary<string, ToggleSwitch> _modToggles = new Dictionary<string, ToggleSwitch>();
        readonly List<FormRow> _modRows = new List<FormRow>(); FlatButton _bSetup, _bGameDir; Para _setupInfo, _setupLog; ToggleSwitch _unlockServer, _unlockClient; FormRow _rowUnlockServer, _rowUnlockClient;
        Segmented _segLang; readonly Dictionary<string, ModStatus> _status = new Dictionary<string, ModStatus>(StringComparer.OrdinalIgnoreCase);

        string GameDir { get { return _cfg.S("GameDir", ""); } }
        string DocsDir { get { return _cfg.S("DocsDir", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); } }
        string ModSettingsDir { get { return Path.Combine(GameDir, "mods", _mod, "settings"); } }
        string CoopTxtPath { get { return Path.Combine(ModSettingsDir, "coop_server.txt"); } }
        string CfgDir { get { return Path.Combine(DocsDir, "Battlefield 2", "ServerConfigs", _coop ? "coop" : "conquest"); } }
        string ModeName { get { return _coop ? "gpm_coop" : "gpm_cq"; } }

        public MainForm(AppConfig cfg, string shotDir)
        {
            _cfg = cfg; _shotDir = shotDir;
            Text = AppInfo.Name; BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.Body; KeyPreview = true;
            DoubleBuffered = true; AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.CenterScreen;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(Dpi.D(_cfg.I("WinW", 1240)), wa.Width - 20), Math.Min(Dpi.D(_cfg.I("WinH", 800)), wa.Height - 20));
            MinimumSize = new Size(Math.Min(Dpi.D(960), wa.Width), Math.Min(Dpi.D(600), wa.Height));
            try { Icon = Icon.ExtractAssociatedIcon(AppPaths.Exe); } catch { }
            foreach (string c in _cfg.List("Collapsed")) _collapsed.Add(c);

            string portable = GameLocator.Portable();
            if (portable != null && !string.Equals(_cfg.S("GameDir", ""), portable, StringComparison.OrdinalIgnoreCase)) { _cfg.Set("GameDir", portable); _cfg.Save(); }
            _mods = _cfg.List("Mods");
            // Spaß-Aufsätze sind keine eigenen Mods zum Bearbeiten – Regeln stehen jetzt pro Map in der Rotation
            if (GameLocator.IsGameDir(GameDir))
            {
                string last = _cfg.S("LastMod", "");
                string lb = last.Length > 0 ? FunMods.BaseOf(GameDir, last) : null;
                if (lb != null) _cfg.Set("LastMod", lb);
                int before = _mods.Count;
                _mods.RemoveAll(delegate (string m) { return FunMods.BaseOf(GameDir, m) != null; });
                if (_mods.Count != before || lb != null) { _cfg.Set("Mods", _mods); _cfg.Save(); }
            }
            _setupMode = !GameLocator.IsGameDir(GameDir) || _mods.Count == 0;
            if (_mods.Count > 0) { _mod = _cfg.S("LastMod", _mods[0]); if (!_mods.Contains(_mod)) _mod = _mods[0]; }
            _coop = _cfg.S("LastType", "coop") != "conquest";

            SuspendLayout();
            BuildShell(); BuildServer(); BuildBots(); BuildMaps(); BuildLive(); BuildPlayer(); BuildExtras(); BuildServerExtras();
            ResumeLayout(true);

            _timer = new WinTimer(); _timer.Interval = 2000; _timer.Tick += delegate { RefreshLive(); UpdateGameState(); };
            _nextTimer = new WinTimer(); _nextTimer.Interval = 700; _nextTimer.Tick += delegate { _nextTimer.Stop(); WriteNext(); };
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkTitle(this); }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshStatus();
            if (_setupMode) ApplySetupMode(); else { AutoUpdateModules(); LoadAll(); WarnModule(); }
            // Die meisten nutzen den Manager zum Mitspielen: immer in „Spielen“ starten – nur wenn der eigene Server schon läuft, in „Hosten“
            _host = !_setupMode && Srv.IsRunning();
            _segRole.SetSilent(_host ? 1 : 0);
            LayoutNav(); LayoutHeader(); ApplyRoleCards();
            if (_host) ShowServerView(); else SelectPage(PgPlayer);
            RefreshLive();
            _timer.Start();
            StartLan();
            RefreshChecks(true);
            if (_shotDir != null) { RunShots(); return; }
            ShowUpdateNotes();
            ShowWelcome(false);
            if (Program.MovedFrom != null) AfterMove(); else OfferMoveOnce();
            if (_cfg.B("AutoUpdate", true)) CheckForUpdates(false);
        }

        static void Run(ProcessStartInfo psi) { Process p = Process.Start(psi); if (p != null) p.Dispose(); }
        void Toast(string msg, ToastKind kind) { _toast.Show(msg, kind, _body.Bounds); }
        void MarkDirty() { if (_applying) return; _dirty = true; UpdateLiveBanner(); ScheduleSave(); }
        void ClearDirty() { _dirty = false; _bSave.Dot = false; UpdateLiveBanner(); }

    }
}
