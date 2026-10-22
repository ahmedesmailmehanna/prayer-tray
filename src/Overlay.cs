using System;
using System.Drawing;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PrayerTray
{
    /// <summary>
    /// Transparent two-line text that sits on the taskbar left of the clock:
    ///     Asr 15:21
    ///   in 1:23:45
    /// Windows has no API for putting text in the taskbar, so this is a small transparent
    /// tool window kept on top of it. Drag it sideways to move it.
    /// </summary>
    class Overlay : Form
    {
        public event EventHandler LeftClicked;
        public event EventHandler DragEnded;
        public int OffsetX;

        readonly Font font1 = new Font("Segoe UI", 9f);
        readonly Font font2 = new Font("Segoe UI Semibold", 9f);
        string line1 = "", line2 = "";
        Color textColor = Color.White;
        int baseX;
        bool dragging, moved;
        int startCursorX, startOffset;
        bool light;
        DateTime themeCheckedAt = DateTime.MinValue;

        public Overlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(12, 12, 12);
            TransparencyKey = BackColor;
            Size = new Size(Ui.S(100), Ui.S(40));
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000 | 0x8;   // TOOLWINDOW | NOACTIVATE | TOPMOST
                return cp;
            }
        }

        /// <summary>Called every second.</summary>
        public void Render(PrayerTime next, bool enabled)
        {
            IntPtr taskbar = Native.FindWindow("Shell_TrayWnd", null);
            Rectangle tb = Native.RectOf(taskbar);
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            bool taskbarHidden = tb.IsEmpty || tb.Bottom > screen.Bottom + 2 || tb.Top < screen.Top - 2 || tb.Width < tb.Height;
            if (!enabled || taskbarHidden || Native.ForegroundIsFullscreen())
            {
                if (Visible) Hide();
                return;
            }

            // Follow the light/dark taskbar setting (checked every few seconds)
            if ((DateTime.Now - themeCheckedAt).TotalSeconds > 5)
            {
                themeCheckedAt = DateTime.Now;
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        object v = k == null ? null : k.GetValue("SystemUsesLightTheme");
                        light = v is int && (int)v == 1;
                    }
                }
                catch { light = false; }
            }
            Color key = light ? Color.FromArgb(242, 242, 242) : Color.FromArgb(12, 12, 12);
            if (BackColor.ToArgb() != key.ToArgb()) { BackColor = key; TransparencyKey = key; }
            Color fore = light ? Color.Black : Color.White;

            string l1, l2;
            if (next == null)
            {
                l1 = "Prayer times";
                l2 = "offline";
            }
            else
            {
                TimeSpan left = next.Time - DateTime.Now;
                l1 = next.Name + " " + Fmt.Time(next.Time);
                l2 = "in " + Fmt.Clock(left);
                if (left.TotalMinutes <= 15) fore = Ui.Soon;
            }

            // Size to the text (digits measured as zeros so it doesn't jiggle every second)
            int w = Math.Max(TextRenderer.MeasureText(l1, font1).Width,
                             TextRenderer.MeasureText(Regex.Replace(l2, @"\d", "0"), font2).Width) + Ui.S(8);
            int h = Math.Min(tb.Height, Ui.S(44));

            // Just left of the tray icons / clock
            Rectangle tray = Native.RectOf(Native.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null));
            int anchor = (tray.IsEmpty || tray.Left <= tb.Left) ? tb.Right - Ui.S(280) : tray.Left;
            baseX = anchor - w - Ui.S(6);

            if (!dragging)
            {
                Rectangle b = new Rectangle(baseX + OffsetX, tb.Top + (tb.Height - h) / 2, w, h);
                if (Bounds != b) Bounds = b;
            }
            if (l1 != line1 || l2 != line2 || fore.ToArgb() != textColor.ToArgb())
            {
                line1 = l1; line2 = l2; textColor = fore;
                Invalidate();
            }
            if (!Visible) Show();
            Native.KeepOnTop(Handle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            int half = ClientSize.Height / 2;
            using (StringFormat f = new StringFormat())
            using (Brush b = new SolidBrush(textColor))
            {
                f.Alignment = StringAlignment.Center;
                f.LineAlignment = StringAlignment.Far;
                g.DrawString(line1, font1, b, new RectangleF(0, 0, ClientSize.Width, half + 1), f);
                f.LineAlignment = StringAlignment.Near;
                g.DrawString(line2, font2, b, new RectangleF(0, half - 1, ClientSize.Width, ClientSize.Height - half + 1), f);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            moved = false;
            startCursorX = Cursor.Position.X;
            startOffset = OffsetX;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            int dx = Cursor.Position.X - startCursorX;
            if (Math.Abs(dx) > 3) moved = true;
            if (moved)
            {
                OffsetX = startOffset + dx;
                Left = baseX + OffsetX;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !dragging) return;
            dragging = false;
            Capture = false;
            EventHandler handler = moved ? DragEnded : LeftClicked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { font1.Dispose(); font2.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
