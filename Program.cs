using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace TrayDisplaySwitch
{
	internal sealed class TrayDisplayAppContext : ApplicationContext
	{
		private readonly NotifyIcon _notifyIcon;
		private readonly ContextMenuStrip _menu;

		// Chemin absolu sûr vers DisplaySwitch.exe (évite le détournement via PATH)
		private static string DisplaySwitchPath => Path.Combine(Environment.SystemDirectory, "DisplaySwitch.exe");

		public TrayDisplayAppContext()
		{
			_menu = new ContextMenuStrip();

			_menu.Items.Add("Écran PC uniquement", null, (_, __) => RunDisplaySwitchMode(DisplayMode.Internal));
			_menu.Items.Add("Dupliquer", null, (_, __) => RunDisplaySwitchMode(DisplayMode.Clone));
			_menu.Items.Add("Étendre", null, (_, __) => RunDisplaySwitchMode(DisplayMode.Extend));
			_menu.Items.Add("Second écran uniquement", null, (_, __) => RunDisplaySwitchMode(DisplayMode.External));
			_menu.Items.Add(new ToolStripSeparator());
			_menu.Items.Add("Paramètres d’affichage…", null, (_, __) => OpenDisplaySettings());
			_menu.Items.Add(new ToolStripSeparator());
			_menu.Items.Add("Quitter", null, (_, __) => ExitThread());

			var trayIcon = LoadEmbeddedIcon() ?? SystemIcons.Application;

			_notifyIcon = new NotifyIcon
			{
				Icon = trayIcon,
				ContextMenuStrip = _menu,
				Visible = true,
				Text = "Affichage: dupliquer/étendre/PC/externe"
			};

			_notifyIcon.DoubleClick += (_, __) => RunDisplaySwitchMode(DisplayMode.Extend);

			if (!File.Exists(DisplaySwitchPath))
			{
				ShowBalloon("DisplaySwitch introuvable", $"Fichier attendu: {DisplaySwitchPath}");
			}
		}

		private static Icon? LoadEmbeddedIcon()
		{
			try
			{
				var asm = Assembly.GetExecutingAssembly();
				// Nom de ressource : <namespace>.<nom_fichier>
				using Stream? s = asm.GetManifestResourceStream("TrayDisplaySwitch.icon.ico");
				if (s != null) return new Icon(s);
			}
			catch { /* ignore */ }
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
				ShowBalloon("Impossible d’ouvrir les paramètres", ex.Message);
			}
		}

		private static void RunDisplaySwitchMode(DisplayMode mode)
		{
			// Liste blanche stricte des arguments
			string args = mode switch
			{
				DisplayMode.Internal => "/internal",
				DisplayMode.Clone => "/clone",
				DisplayMode.Extend => "/extend",
				DisplayMode.External => "/external",
				_ => throw new ArgumentOutOfRangeException(nameof(mode))
			};
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
				ShowBalloon("Échec du changement d’affichage", ex.Message);
			}
		}

		protected override void ExitThreadCore()
		{
			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();
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
	}

	internal enum DisplayMode { Internal, Clone, Extend, External }

	internal static class Program
	{
		[STAThread]
		private static void Main()
		{
			// Mutex global (string verbatim pour éviter CS1009)
			using var mutex = new Mutex(true, @"Global\TrayDisplaySwitchMutex", out bool createdNew);
			if (!createdNew) return; // déjà en cours

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new TrayDisplayAppContext());
		}
	}
}
