using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PrayerTray
{
    /// <summary>Owns the tray icon, the taskbar countdown, the popup and the 1-second timer.</summary>
    class TrayApp : ApplicationContext
    {
        readonly AppSettings cfg;
        PrayerData data;
        readonly NotifyIcon tray;
        readonly PopupForm popup;
        readonly Overlay overlay;
        readonly System.Windows.Forms.Timer timer;
        readonly ToolStripMenuItem miShowText;
        System.Windows.Forms.Timer pinTimer;
        int pinTries;
        IntPtr iconHandle = IntPtr.Zero;
        string iconKey = "";
        PrayerTime prevNext;
        readonly ToolStripMenuItem miStopAdhan;

        // Automatic location updates (runs in the background every few hours)
        const int LocateEveryHours = 3;
        DateTime nextLocateAt = DateTime.Now.AddSeconds(15);
        volatile bool locating;
        volatile LocationResult locateResult;

        public TrayApp(AppSettings cfg)
        {
            this.cfg = cfg;
            StartupEntry.RefreshPath();
            data = new PrayerData(cfg);
            popup = new PopupForm();

            overlay = new Overlay();
            overlay.OffsetX = cfg.OverlayOffset;
            overlay.LeftClicked += delegate { TogglePopup(); };
            overlay.DragEnded += delegate { cfg.OverlayOffset = overlay.OffsetX; cfg.Save(); };

            var menu = new ContextMenuStrip();
            miStopAdhan = new ToolStripMenuItem("Stop adhan") { Visible = false };
            miStopAdhan.Font = new Font(miStopAdhan.Font, FontStyle.Bold);
            miStopAdhan.Click += delegate { AdhanPlayer.Stop(); };
            menu.Items.Add(miStopAdhan);
            menu.Opening += delegate { miStopAdhan.Visible = AdhanPlayer.IsPlaying; };
            menu.Items.Add("Show prayer times", null, delegate { popup.ShowNow(data, data.Next(), Location()); });
            menu.Items.Add("Refresh times", null, delegate { data.ForceRefresh(); Tick(); });
            menu.Items.Add("Settings...", null, delegate { OpenSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            miShowText = new ToolStripMenuItem("Show countdown on taskbar") { CheckOnClick = true, Checked = cfg.ShowText };
            miShowText.Click += delegate { cfg.ShowText = miShowText.Checked; cfg.Save(); Tick(); };
            menu.Items.Add(miShowText);
            menu.Items.Add("Reset countdown position", null, delegate { overlay.OffsetX = 0; cfg.OverlayOffset = 0; cfg.Save(); Tick(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Uninstall...", null, delegate { Uninstall(); });
            menu.Items.Add("Exit", null, delegate { Quit(); });
            overlay.ContextMenuStrip = menu;

            tray = new NotifyIcon();
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) TogglePopup(); };
            tray.BalloonTipClicked += delegate { AdhanPlayer.Stop(); };
            SetIcon("...", Ui.Icon);
            SetTip("Prayer times: loading...");
            tray.Visible = true;

            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += delegate { Tick(); };
            timer.Start();
            Tick();

            if (!cfg.PinAttempted)
            {
                pinTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                pinTimer.Tick += delegate { TryPinIcon(); };
                pinTimer.Start();
            }
        }

        string Location()
        {
            return cfg.PlaceName + "  -  " + Methods.Names[Methods.IndexOf(cfg.Method)];
        }

        void TogglePopup()
        {
            // While the adhan is playing, a click on the icon or countdown just stops it
            if (AdhanPlayer.IsPlaying) { AdhanPlayer.Stop(); return; }
            popup.Toggle(data, data.Next(), Location());
        }

        void Tick()
        {
            try
            {
                data.Tick();
                PrayerTime next = data.Next();
                if (next == null)
                {
                    SetIcon("!", Color.DimGray);
                    SetTip("Prayer times: offline, retrying...");
                }
                else
                {
                    TimeSpan left = next.Time - DateTime.Now;
                    int mins = (int)Math.Ceiling(left.TotalMinutes);
                    SetIcon(mins < 60 ? mins.ToString() : (mins / 60) + "h", mins <= 15 ? Ui.IconSoon : Ui.Icon);
                    SetTip(next.Name + " in " + Fmt.Short(left) + " (" + Fmt.Time(next.Time) + ")");

                    if (cfg.Notify && prevNext != null && prevNext.Time != next.Time && (DateTime.Now - prevNext.Time).TotalMinutes < 2)
                        PrayerTimeArrived(prevNext);
                    prevNext = next;
                }
                if (popup.Visible) popup.UpdateView(data, next);
                overlay.Render(next, cfg.ShowText);
                CheckLocation();
            }
            catch { }
        }

        /// <summary>Notification + optional adhan when a prayer time comes in.</summary>
        void PrayerTimeArrived(PrayerTime p)
        {
            string sound = cfg.AdhanFile;
            if (p.Name == "Fajr" && !string.IsNullOrEmpty(cfg.FajrAdhanFile) && File.Exists(cfg.FajrAdhanFile)) sound = cfg.FajrAdhanFile;
            bool playing = !string.IsNullOrEmpty(sound) && AdhanPlayer.Play(sound);
            tray.ShowBalloonTip(playing ? 15000 : 8000, "Prayer time",
                "It's time for " + p.Name + (playing ? ". Click here to stop the adhan." : "."), ToolTipIcon.Info);
        }

        /// <summary>When "update my location automatically" is on, re-detect every few hours.</summary>
        void CheckLocation()
        {
            LocationResult found = locateResult;
            if (found != null)
            {
                locateResult = null;
                ApplyDetectedLocation(found);
            }
            if (!cfg.AutoLocate || locating || DateTime.Now < nextLocateAt) return;
            nextLocateAt = DateTime.Now.AddHours(LocateEveryHours);
            locating = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { locateResult = Locator.Detect(); } catch { }
                finally { locating = false; }
            });
        }

        void ApplyDetectedLocation(LocationResult r)
        {
            if (!cfg.AutoLocate) return;
            bool sameName = string.Equals(r.City, cfg.City, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(r.Country, cfg.Country, StringComparison.OrdinalIgnoreCase);
            // Ignore small jumps (internet-based locations wobble between nearby towns)
            if (cfg.HasCoords && Locator.DistanceKm(cfg.Latitude, cfg.Longitude, r.Latitude, r.Longitude) < 25) return;
            if (!cfg.HasCoords && sameName) return;

            cfg.City = r.City;
            cfg.Country = r.Country;
            cfg.Latitude = r.Latitude;
            cfg.Longitude = r.Longitude;
            cfg.Save();
            data = new PrayerData(cfg);
            prevNext = null;
            if (!sameName)
                tray.ShowBalloonTip(8000, "Location updated", "Prayer times are now for " + cfg.PlaceName + ".", ToolTipIcon.Info);
        }

        void SetTip(string text)
        {
            if (text.Length > 63) text = text.Substring(0, 63);   // Windows limit
            if (tray.Text != text) tray.Text = text;
        }

        /// <summary>Draws the coloured square with the minutes/hours left.</summary>
        void SetIcon(string text, Color back)
        {
            string key = text + "|" + back.ToArgb();
            if (key == iconKey) return;
            iconKey = key;

            int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            IntPtr h;
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (Font font = new Font("Segoe UI", size * (text.Length <= 2 ? 0.66f : 0.5f), FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(back);
                    g.DrawString(text, font, Brushes.White, new RectangleF(0, 0, size, size), fmt);
                }
                h = bmp.GetHicon();
            }
            Icon old = tray.Icon;
            tray.Icon = Icon.FromHandle(h);
            if (old != null) old.Dispose();
            if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
            iconHandle = h;
        }

        /// <summary>
        /// Windows 11 hides new tray icons in the ^ overflow. On first run, mark ours as
        /// "always show" (same as the switch in Settings > Taskbar > Other system tray icons).
        /// </summary>
        void TryPinIcon()
        {
            pinTries++;
            bool done = false;
            try
            {
                string exeName = "\\" + Path.GetFileName(Application.ExecutablePath);
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings"))
                {
                    if (root == null) done = true;   // Windows 10: nothing to do
                    else
                    {
                        foreach (string name in root.GetSubKeyNames())
                        {
                            using (RegistryKey k = root.OpenSubKey(name, true))
                            {
                                string path = k == null ? null : k.GetValue("ExecutablePath") as string;
                                if (path != null && path.EndsWith(exeName, StringComparison.OrdinalIgnoreCase))
                                {
                                    k.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                                    done = true;
                                }
                            }
                        }
                    }
                }
            }
            catch { done = true; }

            if (done || pinTries >= 30)
            {
                pinTimer.Stop();
                cfg.PinAttempted = true;
                cfg.Save();
            }
        }

        void OpenSettings()
        {
            using (var f = new SettingsForm(cfg, false))
            {
                if (f.ShowDialog() != DialogResult.OK) return;
            }
            data = new PrayerData(cfg);   // location or method may have changed
            prevNext = null;
            nextLocateAt = DateTime.Now.AddHours(LocateEveryHours);   // they just set it themselves
            miShowText.Checked = cfg.ShowText;
            Tick();
        }

        void Uninstall()
        {
            var answer = MessageBox.Show(
                "This removes PrayerTray from startup, deletes its settings and saved prayer times, and closes it.\n\n" +
                "Afterwards you can delete PrayerTray.exe.\n\nContinue?",
                "Uninstall PrayerTray", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            StartupEntry.Set(false);
            Quit();
            try { Directory.Delete(AppSettings.DataDir, true); } catch { }
            try { Process.Start("explorer.exe", "/select,\"" + Application.ExecutablePath + "\""); } catch { }
        }

        void Quit()
        {
            AdhanPlayer.Stop();
            timer.Stop();
            if (pinTimer != null) pinTimer.Stop();
            tray.Visible = false;
            tray.Dispose();
            overlay.Close();
            popup.Close();
            if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
            ExitThread();
        }
    }
}
