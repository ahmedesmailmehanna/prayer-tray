using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrayerTray
{
    /// <summary>Settings window. Shown automatically on first run.</summary>
    class SettingsForm : Form
    {
        readonly AppSettings cfg;
        readonly bool firstRun;
        readonly TextBox txtCity, txtCountry;
        readonly ComboBox cmbMethod, cmbAsr;
        readonly CheckBox chkNotify, chkShowText, chkStartup;
        readonly Label lblStatus;
        readonly Button btnSave, btnCancel;

        public SettingsForm(AppSettings cfg, bool firstRun)
        {
            this.cfg = cfg;
            this.firstRun = firstRun;

            Text = firstRun ? "Welcome to PrayerTray" : "PrayerTray settings";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Ui.S(420), Ui.S(470));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            int x = Ui.S(16), w = Ui.S(388), labelW = Ui.S(100);

            AddLabel("Location", x, Ui.S(14), w, true);
            AddLabel("City", x, Ui.S(47), labelW, false);
            txtCity = new TextBox { Location = new Point(Ui.S(120), Ui.S(44)), Width = Ui.S(284), Text = cfg.City };
            Controls.Add(txtCity);
            AddLabel("Country", x, Ui.S(81), labelW, false);
            txtCountry = new TextBox { Location = new Point(Ui.S(120), Ui.S(78)), Width = Ui.S(284), Text = cfg.Country };
            Controls.Add(txtCountry);

            AddLabel("Calculation method", x, Ui.S(120), w, true);
            cmbMethod = new ComboBox { Location = new Point(x, Ui.S(144)), Width = w, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMethod.Items.AddRange(Methods.Names);
            cmbMethod.SelectedIndex = Methods.IndexOf(cfg.Method);
            Controls.Add(cmbMethod);
            Label hint = AddLabel("Pick the one your local mosque follows.", x, Ui.S(172), w, false);
            hint.ForeColor = SystemColors.GrayText;

            AddLabel("Asr time", x, Ui.S(200), w, true);
            cmbAsr = new ComboBox { Location = new Point(x, Ui.S(224)), Width = w, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAsr.Items.AddRange(new object[] { "Standard (Shafi'i, Maliki, Hanbali)", "Hanafi (later Asr)" });
            cmbAsr.SelectedIndex = cfg.School == 1 ? 1 : 0;
            Controls.Add(cmbAsr);

            chkShowText = AddCheck("Show the countdown as text on the taskbar, next to the clock", Ui.S(266), cfg.ShowText);
            chkNotify = AddCheck("Show a notification when each prayer time arrives", Ui.S(292), cfg.Notify);
            chkStartup = AddCheck("Start PrayerTray when I sign in to Windows", Ui.S(318), firstRun || StartupEntry.IsEnabled());

            Label tip = AddLabel(
                "Tip: Windows may hide new tray icons behind the ^ arrow next to the clock. " +
                "To keep the square visible, drag it from there onto the taskbar, or turn on PrayerTray in " +
                "Settings > Personalization > Taskbar > Other system tray icons.",
                x, Ui.S(352), w, false);
            tip.Height = Ui.S(56);
            tip.ForeColor = SystemColors.GrayText;

            lblStatus = AddLabel("", x, Ui.S(430), Ui.S(190), false);
            btnSave = new Button { Text = firstRun ? "Start" : "Save", Location = new Point(Ui.S(216), Ui.S(424)), Size = new Size(Ui.S(90), Ui.S(30)) };
            btnCancel = new Button { Text = "Cancel", Location = new Point(Ui.S(314), Ui.S(424)), Size = new Size(Ui.S(90), Ui.S(30)), DialogResult = DialogResult.Cancel };
            btnSave.Click += delegate { SaveClicked(); };
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        Label AddLabel(string text, int x, int y, int w, bool bold)
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

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            if (txtCity.Text.Length == 0) txtCity.Focus();
        }

        void SaveClicked()
        {
            string city = txtCity.Text.Trim();
            string country = txtCountry.Text.Trim();
            if (city.Length == 0 || country.Length == 0)
            {
                MessageBox.Show(this, "Please enter your city and country.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool locationChanged = firstRun
                || !string.Equals(city, cfg.City, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(country, cfg.Country, StringComparison.OrdinalIgnoreCase);
            if (locationChanged && !CheckLocation(city, country)) return;

            cfg.City = city;
            cfg.Country = country;
            cfg.Method = Methods.Ids[cmbMethod.SelectedIndex];
            cfg.School = cmbAsr.SelectedIndex;
            cfg.Notify = chkNotify.Checked;
            cfg.ShowText = chkShowText.Checked;
            cfg.Save();
            StartupEntry.Set(chkStartup.Checked);
            DialogResult = DialogResult.OK;
            Close();
        }

        bool CheckLocation(string city, string country)
        {
            lblStatus.Text = "Checking location...";
            lblStatus.Refresh();
            UseWaitCursor = true;
            btnSave.Enabled = false;
            try
            {
                Api.CheckLocation(city, country);
                return true;
            }
            catch (LocationNotFoundException)
            {
                MessageBox.Show(this, "Couldn't find \"" + city + ", " + country + "\".\n\nCheck the spelling (English names work best) and try again.",
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
    }
}
