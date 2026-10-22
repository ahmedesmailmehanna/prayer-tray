using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PrayerTray
{
    /// <summary>The panel that opens when you click the icon or the countdown.</summary>
    class PopupForm : Form
    {
        readonly Label lblDate, lblHijri, lblNext, lblCount, lblFoot;
        readonly List<Label[]> rows = new List<Label[]>();
        readonly Font fontSmall = new Font("Segoe UI", 9f);
        readonly Font fontRow = new Font("Segoe UI", 11f);
        readonly Font fontRowBold = new Font("Segoe UI", 11f, FontStyle.Bold);
        readonly Font fontBig = new Font("Segoe UI", 22f, FontStyle.Bold);
        DateTime hiddenAt = DateTime.MinValue;

        public PopupForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            BackColor = Ui.Bg;
            Size = new Size(Ui.S(280), Ui.S(372));

            int w = Ui.S(280) - Ui.S(36);
            lblDate = AddLabel(Ui.S(18), Ui.S(12), w, Ui.S(20), fontSmall, Ui.Muted, ContentAlignment.MiddleLeft);
            lblHijri = AddLabel(Ui.S(18), Ui.S(32), w, Ui.S(20), fontSmall, Ui.Muted, ContentAlignment.MiddleLeft);
            lblNext = AddLabel(Ui.S(18), Ui.S(60), w, Ui.S(24), fontRow, Ui.Text, ContentAlignment.MiddleLeft);
            lblCount = AddLabel(Ui.S(14), Ui.S(86), w, Ui.S(44), fontBig, Ui.Accent, ContentAlignment.MiddleLeft);
            for (int i = 0; i < PrayerData.Names.Length; i++)
            {
                int y = Ui.S(150) + i * Ui.S(30);
                Label n = AddLabel(Ui.S(18), y, Ui.S(130), Ui.S(28), fontRow, Ui.Text, ContentAlignment.MiddleLeft);
                Label t = AddLabel(Ui.S(150), y, Ui.S(280) - Ui.S(168), Ui.S(28), fontRow, Ui.Text, ContentAlignment.MiddleRight);
                n.Text = PrayerData.Names[i];
                rows.Add(new[] { n, t });
            }
            lblFoot = AddLabel(Ui.S(18), Ui.S(340), w, Ui.S(20), fontSmall, Ui.Muted, ContentAlignment.MiddleLeft);
        }

        Label AddLabel(int x, int y, int w, int h, Font font, Color color, ContentAlignment align)
        {
            Label l = new Label();
            l.Location = new Point(x, y);
            l.Size = new Size(w, h);
            l.Font = font;
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.TextAlign = align;
            Controls.Add(l);
            return l;
        }

        public void Toggle(PrayerData data, PrayerTime next, string location)
        {
            if (Visible) { Hide(); return; }
            if ((DateTime.Now - hiddenAt).TotalMilliseconds < 400) return;   // the click that just closed it
            ShowNow(data, next, location);
        }

        public void ShowNow(PrayerData data, PrayerTime next, string location)
        {
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Right - Width - Ui.S(12), wa.Bottom - Height - Ui.S(12));
            lblFoot.Text = location;
            Show();
            Activate();
            UpdateView(data, next);
        }

        public void UpdateView(PrayerData data, PrayerTime next)
        {
            DateTime now = DateTime.Now;
            lblDate.Text = now.ToString("dddd, d MMMM yyyy");
            DayTimes today = data.Today;
            if (next == null || today == null)
            {
                lblHijri.Text = "";
                lblNext.Text = "Can't reach the prayer-times server";
                lblCount.Text = "--";
                foreach (Label[] r in rows) r[1].Text = "--:--";
                return;
            }
            TimeSpan left = next.Time - now;
            lblHijri.Text = today.Hijri;
            lblNext.Text = next.Name + " at " + Fmt.Time(next.Time);
            lblCount.Text = Fmt.Long(left);
            lblCount.ForeColor = left.TotalMinutes <= 15 ? Ui.Soon : Ui.Accent;

            for (int i = 0; i < rows.Count; i++)
            {
                PrayerTime p = today.Times[i];
                Label[] r = rows[i];
                r[1].Text = Fmt.Time(p.Time);
                Color c; Font f;
                if (p.Time == next.Time) { c = Ui.Accent; f = fontRowBold; }
                else if (p.Time <= now) { c = Ui.Muted; f = fontRow; }
                else { c = Ui.Text; f = fontRow; }
                r[0].ForeColor = c; r[1].ForeColor = c;
                r[0].Font = f; r[1].Font = f;
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Hide();
            hiddenAt = DateTime.Now;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Color.FromArgb(70, 74, 84)))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                e.Graphics.DrawLine(pen, Ui.S(18), Ui.S(140), ClientSize.Width - Ui.S(18), Ui.S(140));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { fontSmall.Dispose(); fontRow.Dispose(); fontRowBold.Dispose(); fontBig.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
