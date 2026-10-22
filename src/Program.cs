using System;
using System.IO;
using System.Management;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace PrayerTray
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.ThreadException += (s, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            try
            {
                Run();
            }
            catch (Exception ex)
            {
                ReportCrash(ex);
            }
        }

        static void Run()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "PrayerTray.Exe.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("PrayerTray is already running - look for it next to the clock.",
                        "PrayerTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Native.SetProcessDPIAware();
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                RemoveOldScriptVersion();

                AppSettings cfg = AppSettings.Load();
                if (!cfg.IsConfigured)
                {
                    using (var f = new SettingsForm(cfg, true))
                    {
                        if (f.ShowDialog() != DialogResult.OK) return;
                    }
                }
                Application.Run(new TrayApp(cfg));
            }
        }

        /// <summary>
        /// The first versions of PrayerTray were a PowerShell script. If that one is still
        /// installed, offer to stop it and remove it from startup so there aren't two countdowns.
        /// </summary>
        static void RemoveOldScriptVersion()
        {
            try
            {
                string oldShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "PrayerTray.lnk");
                Mutex oldMutex;
                bool oldRunning = Mutex.TryOpenExisting("PrayerTrayApp_SingleInstance", out oldMutex);
                if (oldMutex != null) oldMutex.Dispose();
                if (!oldRunning && !File.Exists(oldShortcut)) return;

                var answer = MessageBox.Show(
                    "The older PowerShell version of PrayerTray is installed on this PC.\n\n" +
                    "Close it and remove it from startup, so this new version replaces it?",
                    "PrayerTray", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;

                if (File.Exists(oldShortcut)) File.Delete(oldShortcut);
                using (var search = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'powershell.exe' OR Name = 'pwsh.exe'"))
                {
                    foreach (ManagementObject p in search.Get())
                    {
                        string cmd = p["CommandLine"] as string;
                        if (cmd == null || cmd.IndexOf("PrayerTray.ps1", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        try { System.Diagnostics.Process.GetProcessById(Convert.ToInt32(p["ProcessId"])).Kill(); } catch { }
                    }
                }
            }
            catch { }
        }

        static void ReportCrash(Exception ex)
        {
            if (ex == null) return;
            string log = Path.Combine(AppSettings.DataDir, "error.log");
            try
            {
                Directory.CreateDirectory(AppSettings.DataDir);
                File.AppendAllText(log, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }
            try
            {
                MessageBox.Show("PrayerTray ran into a problem:\n\n" + ex.Message +
                    "\n\nDetails were saved to:\n" + log, "PrayerTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
