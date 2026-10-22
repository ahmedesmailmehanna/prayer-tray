using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PrayerTray
{
    static class Native
    {
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr handle);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string name);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        public static Rectangle RectOf(IntPtr h)
        {
            RECT r;
            if (h == IntPtr.Zero || !GetWindowRect(h, out r)) return Rectangle.Empty;
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        static string ClassOf(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            return sb.ToString();
        }

        /// <summary>Keeps a window above the taskbar without taking focus.</summary>
        public static void KeepOnTop(IntPtr h)
        {
            const uint NOSIZE = 0x0001, NOMOVE = 0x0002, NOACTIVATE = 0x0010;
            SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, NOSIZE | NOMOVE | NOACTIVATE);
        }

        /// <summary>True while a game, video or presentation covers the whole screen.</summary>
        public static bool ForegroundIsFullscreen()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            string c = ClassOf(h);
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return false;
            Rectangle r = RectOf(h);
            Rectangle s = Screen.FromHandle(h).Bounds;
            return r.Left <= s.Left && r.Top <= s.Top && r.Right >= s.Right && r.Bottom >= s.Bottom;
        }
    }

    /// <summary>DPI scaling helper (the app is DPI-aware, so sizes are in real pixels).</summary>
    static class Ui
    {
        static float scale = 0;

        public static int S(double v)
        {
            if (scale == 0)
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            }
            return (int)Math.Round(v * scale);
        }

        public static readonly Color Bg = Color.FromArgb(30, 32, 38);
        public static readonly Color Text = Color.FromArgb(236, 238, 242);
        public static readonly Color Muted = Color.FromArgb(130, 136, 148);
        public static readonly Color Accent = Color.FromArgb(45, 190, 160);
        public static readonly Color Soon = Color.FromArgb(240, 160, 50);
        public static readonly Color Icon = Color.FromArgb(15, 118, 110);
        public static readonly Color IconSoon = Color.FromArgb(217, 119, 6);
    }
}
