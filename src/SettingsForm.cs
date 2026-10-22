using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PrayerTray
{
    /// <summary>Settings window. Shown automatically on first run.</summary>
    class SettingsForm : Form
    {
        readonly AppSettings cfg;
        readonly bool firstRun;
        readonly TextBox txtCity, txtCountry;
        readonly Button btnDetect;
        readonly Label lblDetect;
        readonly CheckBox chkAutoLocate;
        readonly ComboBox cmbMethod, cmbAsr;
        readonly CheckBox chkNotify, chkShowText, chkStartup;
        readonly AdhanRow adhanMain, adhanFajr;
        readonly Label lblStatus;
        readonly Button btnSave, btnCancel;
        readonly System.Windows.Forms.Timer playTimer;

        // Coordinates and the place name they belong to (from cfg or from "Detect")
        double coordLat, coordLon;
        string coordCity, coordCountry;

        public SettingsForm(AppSettings cfg, bool firstRun)
        {
            this.cfg = cfg;
            this.firstRun = firstRun;
            coordLat = cfg.HasCoords ? cfg.Latitude : double.NaN;
            coordLon = cfg.HasCoords ? cfg.Longitude : double.NaN;
            coordCity = cfg.City;
            coordCountry = cfg.Country;

            Text = firstRun ? "Welcome to PrayerTray" : "PrayerTray settings";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            int x = Ui.S(16), w = Ui.S(388), y = Ui.S(14);

            // ---- Location ----
            AddLabel("Location", x, y, w, true); y += Ui.S(30);
            AddLabel("City", x, y + Ui.S(3), Ui.S(100), false);
            txtCity = new TextBox { Location = new Point(Ui.S(120), y), Width = Ui.S(284), Text = cfg.City };
            Controls.Add(txtCity); y += Ui.S(34);
            AddLabel("Country", x, y + Ui.S(3), Ui.S(100), false);
            txtCountry = new TextBox { Location = new Point(Ui.S(120), y), Width = Ui.S(284), Text = cfg.Country };
            Controls.Add(txtCountry); y += Ui.S(34);
            btnDetect = new Button { Text = "Detect my location", Location = new Point(Ui.S(120), y), Size = new Size(Ui.S(150), Ui.S(28)) };
            btnDetect.Click += delegate { DetectClicked(); };
            Controls.Add(btnDetect);
            lblDetect = AddLabel("", Ui.S(276), y + Ui.S(5), Ui.S(128), false);
            lblDetect.ForeColor = SystemColors.GrayText;
            y += Ui.S(36);
            chkAutoLocate = AddCheck("Update my location automatically when I travel", y, cfg.AutoLocate); y += Ui.S(38);

            // ---- Times ----
            AddLabel("Calculation method", x, y, w, true); y += Ui.S(24);
            cmbMethod = new ComboBox { Location = new Point(x, y), Width = w, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMethod.Items.AddRange(Methods.Names);
            cmbMethod.SelectedIndex = Methods.IndexOf(cfg.Method);
            Controls.Add(cmbMethod); y += Ui.S(28);
            Label hint = AddLabel("Pick the one your local mosque follows.", x, y, w, false);
            hint.ForeColor = SystemColors.GrayText; y += Ui.S(28);

            AddLabel("Asr time", x, y, w, true); y += Ui.S(24);
            cmbAsr = new ComboBox { Location = new Point(x, y), Width = w, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAsr.Items.AddRange(new object[] { "Standard (Shafi'i, Maliki, Hanbali)", "Hanafi (later Asr)" });
            cmbAsr.SelectedIndex = cfg.School == 1 ? 1 : 0;
            Controls.Add(cmbAsr); y += Ui.S(40);

            // ---- Display ----
            chkShowText = AddCheck("Show the countdown as text on the taskbar, next to the clock", y, cfg.ShowText); y += Ui.S(26);
            chkStartup = AddCheck("Start PrayerTray when I sign in to Windows", y, firstRun || StartupEntry.IsEnabled()); y += Ui.S(38);

            // ---- Notifications & adhan ----
            AddLabel("Notifications", x, y, w, true); y += Ui.S(24);
            chkNotify = AddCheck("Show a notification when each prayer time arrives", y, cfg.Notify); y += Ui.S(30);
            adhanMain = new AdhanRow(this, "Adhan sound (optional)", "No sound", cfg.AdhanName, cfg.AdhanFile, y); y += Ui.S(56);
            adhanFajr = new AdhanRow(this, "Fajr adhan (optional, otherwise the one above is used)", "Same as above", cfg.FajrAdhanName, cfg.FajrAdhanFile, y); y += Ui.S(64);
            chkNotify.CheckedChanged += delegate { UpdateAdhanEnabled(); };
            UpdateAdhanEnabled();

            // ---- Buttons ----
            lblStatus = AddLabel("", x, y + Ui.S(6), Ui.S(190), false);
            btnSave = new Button { Text = firstRun ? "Start" : "Save", Location = new Point(Ui.S(216), y), Size = new Size(Ui.S(90), Ui.S(30)) };
            btnCancel = new Button { Text = "Cancel", Location = new Point(Ui.S(314), y), Size = new Size(Ui.S(90), Ui.S(30)), DialogResult = DialogResult.Cancel };
            btnSave.Click += delegate { SaveClicked(); };
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            AcceptButton = btnSave;
            CancelButton = btnCancel;
            y += Ui.S(46);

            ClientSize = new Size(Ui.S(420), y);

            // Flip "Stop" back to "Play" when a preview ends
            playTimer = new System.Windows.Forms.Timer { Interval = 500 };
            playTimer.Tick += delegate
            {
                if (!AdhanPlayer.IsPlaying) { adhanMain.SetPlaying(false); adhanFajr.SetPlaying(false); }
            };
            playTimer.Start();
        }

        // ---------- helpers ----------

        internal Label AddLabel(string text, int x, int y, int w, bool bold)
        {
            Label l = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, Ui.S(20)) };
            if (bold) l.Font = new Font(Font, FontStyle.Bold);
            Controls.Add(l);
            return l;
        }

        CheckBox AddCheck(string text, int y, bool on)
        {
            CheckBox c = new CheckBox { Text = text, Location = new Point(Ui.S(16), y), Size = new Size(Ui.S(388), Ui.S(22)), Checked = on };
            Controls.Add(c);
            return c;
        }

        void UpdateAdhanEnabled()
        {
            adhanMain.Enabled = chkNotify.Checked;
            adhanFajr.Enabled = chkNotify.Checked;
        }

        internal void StopPreviews()
        {
            AdhanPlayer.Stop();
            adhanMain.SetPlaying(false);
            adhanFajr.SetPlaying(false);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            if (firstRun && txtCity.Text.Length == 0) DetectClicked();   // try to fill it in for them
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            playTimer.Stop();
            StopPreviews();
            base.OnFormClosed(e);
        }

        // ---------- location ----------

        void DetectClicked()
        {
            btnDetect.Enabled = false;
            lblDetect.Text = "Detecting...";
            ThreadPool.QueueUserWorkItem(delegate
            {
                LocationResult r = null;
                try { r = Locator.Detect(); } catch { }
                try { BeginInvoke((MethodInvoker)delegate { DetectFinished(r); }); } catch { }
            });
        }

        void DetectFinished(LocationResult r)
        {
            btnDetect.Enabled = true;
            if (r == null)
            {
                lblDetect.Text = "Couldn't detect it";
                return;
            }
            coordLat = r.Latitude;
            coordLon = r.Longitude;
            coordCity = r.City;
            coordCountry = r.Country;
            txtCity.Text = r.City;
            txtCountry.Text = r.Country;
            lblDetect.Text = "Found via " + (r.Source == "Windows Location" ? "Windows" : "internet");
        }

        bool UsingCoords(string city, string country)
        {
            return !double.IsNaN(coordLat)
                && string.Equals(city, coordCity, StringComparison.OrdinalIgnoreCase)
                && string.Equals(country, coordCountry, StringComparison.OrdinalIgnoreCase);
        }

        // ---------- save ----------

        void SaveClicked()
        {
            string city = txtCity.Text.Trim();
            string country = txtCountry.Text.Trim();
            bool useCoords = UsingCoords(city, country);

            if (!useCoords && (city.Length == 0 || country.Length == 0))
            {
                MessageBox.Show(this, "Please enter your city and country, or click \"Detect my location\".",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool changed = firstRun
                || useCoords != cfg.HasCoords
                || (useCoords && (coordLat != cfg.Latitude || coordLon != cfg.Longitude))
                || !string.Equals(city, cfg.City, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(country, cfg.Country, StringComparison.OrdinalIgnoreCase);
            if (changed && !CheckLocation(city, country, useCoords)) return;

            // Copy chosen adhan files into the data folder
            try
            {
                adhanMain.Apply(ref cfg.AdhanFile, ref cfg.AdhanName, "adhan");
                adhanFajr.Apply(ref cfg.FajrAdhanFile, ref cfg.FajrAdhanName, "adhan-fajr");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't use that sound file:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            cfg.City = city;
            cfg.Country = country;
            cfg.Latitude = useCoords ? coordLat : double.NaN;
            cfg.Longitude = useCoords ? coordLon : double.NaN;
            cfg.AutoLocate = chkAutoLocate.Checked;
            cfg.Method = Methods.Ids[cmbMethod.SelectedIndex];
            cfg.School = cmbAsr.SelectedIndex;
            cfg.Notify = chkNotify.Checked;
            cfg.ShowText = chkShowText.Checked;
            cfg.Save();
            StartupEntry.Set(chkStartup.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }

        bool CheckLocation(string city, string country, bool useCoords)
        {
            lblStatus.Text = "Checking location...";
            lblStatus.Refresh();
            UseWaitCursor = true;
            btnSave.Enabled = false;
            try
            {
                if (useCoords) Api.CheckCoords(coordLat, coordLon);
                else Api.CheckLocation(city, country);
                return true;
            }
            catch (LocationNotFoundException)
            {
                MessageBox.Show(this, "Couldn't find \"" + city + ", " + country + "\".\n\n" +
                    "Check the spelling (English names work best), or click \"Detect my location\".",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            catch (Exception ex)
            {
                return MessageBox.Show(this, "Couldn't reach the prayer-times server:\n" + ex.Message + "\n\nSave this location anyway?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
            }
            finally
            {
                lblStatus.Text = "";
                UseWaitCursor = false;
                btnSave.Enabled = true;
            }
        }

        // ---------- one "pick a sound" row ----------

        class AdhanRow
        {
            readonly SettingsForm form;
            readonly Label title;
            readonly TextBox box;
            readonly Button browse, play, clear;
            string currentFile;          // file used for preview
            string pendingSource;        // null = unchanged, "" = cleared, else a newly chosen file

            public AdhanRow(SettingsForm form, string label, string emptyText, string currentName, string currentPath, int y)
            {
                this.form = form;
                title = form.AddLabel(label, Ui.S(16), y, Ui.S(388), false);
                y += Ui.S(22);
                box = new TextBox { Location = new Point(Ui.S(16), y + Ui.S(2)), Width = Ui.S(196), ReadOnly = true, TabStop = false };
                box.Text = string.IsNullOrEmpty(currentName) ? emptyText : currentName;
                browse = new Button { Text = "Choose...", Location = new Point(Ui.S(218), y), Size = new Size(Ui.S(70), Ui.S(27)) };
                play = new Button { Text = "Play", Location = new Point(Ui.S(294), y), Size = new Size(Ui.S(52), Ui.S(27)) };
                clear = new Button { Text = "Clear", Location = new Point(Ui.S(352), y), Size = new Size(Ui.S(52), Ui.S(27)) };
                form.Controls.Add(box);
                form.Controls.Add(browse);
                form.Controls.Add(play);
                form.Controls.Add(clear);

                currentFile = ExistingFile(currentPath);
                browse.Click += delegate { Browse(); };
                play.Click += delegate { TogglePlay(); };
                clear.Click += delegate { form.StopPreviews(); pendingSource = ""; currentFile = null; box.Text = emptyText; UpdateButtons(); };
                UpdateButtons();
            }

            static string ExistingFile(string path)
            {
                return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
            }

            public bool Enabled
            {
                set
                {
                    title.Enabled = value; box.Enabled = value; browse.Enabled = value;
                    UpdateButtons();
                    if (!value) { play.Enabled = false; clear.Enabled = false; }
                }
            }

            void UpdateButtons()
            {
                play.Enabled = browse.Enabled && currentFile != null;
                clear.Enabled = browse.Enabled && currentFile != null;
            }

            void Browse()
            {
                using (OpenFileDialog dlg = new OpenFileDialog { Filter = AdhanPlayer.FileFilter, Title = "Choose an adhan sound" })
                {
                    if (dlg.ShowDialog(form) != DialogResult.OK) return;
                    form.StopPreviews();
                    pendingSource = dlg.FileName;
                    currentFile = dlg.FileName;
                    box.Text = Path.GetFileName(dlg.FileName);
                    UpdateButtons();
                }
            }

            void TogglePlay()
            {
                if (play.Text == "Stop") { form.StopPreviews(); return; }
                form.StopPreviews();
                if (AdhanPlayer.Play(currentFile)) SetPlaying(true);
                else MessageBox.Show(form, "Windows couldn't play this file. Try an MP3 or WAV file.", form.Text,
                         MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            public void SetPlaying(bool playing)
            {
                play.Text = playing ? "Stop" : "Play";
            }

            /// <summary>On Save: copy a newly chosen file into the data folder, or forget a cleared one.</summary>
            public void Apply(ref string file, ref string name, string baseName)
            {
                if (pendingSource == null) return;
                if (pendingSource.Length == 0)
                {
                    AdhanPlayer.Remove(baseName);
                    file = "";
                    name = "";
                    return;
                }
                file = AdhanPlayer.Import(pendingSource, baseName);
                name = Path.GetFileName(pendingSource);
            }
        }
    }
}
