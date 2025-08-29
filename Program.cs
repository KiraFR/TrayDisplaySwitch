using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32; // Registry + SystemEvents

// Alias to disambiguate Timer
using WinFormsTimer = System.Windows.Forms.Timer;

namespace TrayDisplaySwitch
{
    internal sealed class TrayDisplayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;

        private bool _isLightTheme;
        private Icon? _ownedIcon;
        private readonly WinFormsTimer _themePollTimer;

        private static string DisplaySwitchPath => Path.Combine(Environment.SystemDirectory, "DisplaySwitch.exe");

        public TrayDisplayAppContext()
        {
            _menu = new ContextMenuStrip();

            _menu.Items.Add("PC screen only", null, (_, __) => RunDisplaySwitchMode(DisplayMode.Internal));
            _menu.Items.Add("Duplicate", null, (_, __) => RunDisplaySwitchMode(DisplayMode.Clone));
            _menu.Items.Add("Extend", null, (_, __) => RunDisplaySwitchMode(DisplayMode.Extend));
            _menu.Items.Add("Second screen only", null, (_, __) => RunDisplaySwitchMode(DisplayMode.External));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Display settings…", null, (_, __) => OpenDisplaySettings());
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Exit", null, (_, __) => ExitThread());

            _isLightTheme = IsLightTheme();
            _ownedIcon = LoadEmbeddedIcon(GetIconResourceName(_isLightTheme));
            var trayIcon = _ownedIcon ?? SystemIcons.Application;

            _notifyIcon = new NotifyIcon
            {
                Icon = trayIcon,
                ContextMenuStrip = _menu,
                Visible = true,
                Text = "Display switch: duplicate/extend/PC/external"
            };

            _notifyIcon.DoubleClick += (_, __) => RunDisplaySwitchMode(DisplayMode.Extend);

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            _themePollTimer = new WinFormsTimer { Interval = 3000 };
            _themePollTimer.Tick += (_, __) => RefreshThemeIcon();
            _themePollTimer.Start();

            if (!File.Exists(DisplaySwitchPath))
            {
                ShowBalloon("DisplaySwitch not found", $"Expected file: {DisplaySwitchPath}");
            }
        }

        private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General ||
                e.Category == UserPreferenceCategory.Color ||
                e.Category == UserPreferenceCategory.VisualStyle)
            {
                RefreshThemeIcon();
            }
        }

        private void RefreshThemeIcon()
        {
            bool nowLight = IsLightTheme();
            if (nowLight == _isLightTheme) return;

            _isLightTheme = nowLight;

            var newIcon = LoadEmbeddedIcon(GetIconResourceName(_isLightTheme));
            if (newIcon != null)
            {
                var old = _ownedIcon;
                _notifyIcon.Icon = newIcon;
                _ownedIcon = newIcon;
                old?.Dispose();
            }
        }

        private static Icon? LoadEmbeddedIcon(string resourceLogicalName)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using Stream? s = asm.GetManifestResourceStream(resourceLogicalName);
                if (s != null) return new Icon(s);
            }
            catch { }
            return null;
        }

        private static void OpenDisplaySettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:display",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowBalloon("Failed to open display settings", ex.Message);
            }
        }

        private static void RunDisplaySwitchMode(DisplayMode mode)
        {
            string args = Program.GetArgs(mode);
            RunDisplaySwitch(args);
        }

        private static void RunDisplaySwitch(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = DisplaySwitchPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.SystemDirectory
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ShowBalloon("Failed to switch display mode", ex.Message);
            }
        }

        protected override void ExitThreadCore()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _themePollTimer.Stop();
            _themePollTimer.Dispose();

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _ownedIcon?.Dispose();
            _menu.Dispose();

            base.ExitThreadCore();
        }

        private static void ShowBalloon(string title, string? message)
        {
            using var tmp = new NotifyIcon { Icon = SystemIcons.Application, Visible = true };
            tmp.BalloonTipTitle = title;
            tmp.BalloonTipText = message ?? string.Empty;
            tmp.ShowBalloonTip(3000);
        }

        internal static string GetIconResourceName(bool isLightTheme) =>
            isLightTheme ? "TrayDisplaySwitch.icon_light.ico"
                         : "TrayDisplaySwitch.icon_dark.ico";

        internal static bool IsLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("SystemUsesLightTheme") is int v)
                    return v == 1;
            }
            catch { }
            return true;
        }
    }

    public enum DisplayMode { Internal, Clone, Extend, External }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using var mutex = new Mutex(true, @"Global\TrayDisplaySwitchMutex", out bool createdNew);
            if (!createdNew) return;

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayDisplayAppContext());
        }

        internal static string GetArgs(DisplayMode mode) => mode switch
        {
            DisplayMode.Internal => "/internal",
            DisplayMode.Clone    => "/clone",
            DisplayMode.Extend   => "/extend",
            DisplayMode.External => "/external",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
