using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PrayerTray
{
    /// <summary>
    /// Plays the adhan (MP3, WAV or WMA) with Windows' built-in media player (MCI),
    /// so no extra libraries are needed. Call only from the UI thread.
    /// </summary>
    static class AdhanPlayer
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr callback);

        const string Alias = "prayertray_adhan";
        static bool isOpen;

        public static readonly string FileFilter = "Audio files (*.mp3;*.wav;*.wma)|*.mp3;*.wav;*.wma|All files (*.*)|*.*";

        /// <summary>Starts playing a file. Returns false if it can't be played.</summary>
        public static bool Play(string path)
        {
            Stop();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            if (mciSendString("open \"" + path + "\" type mpegvideo alias " + Alias, null, 0, IntPtr.Zero) != 0) return false;
            isOpen = true;
            if (mciSendString("play " + Alias, null, 0, IntPtr.Zero) != 0) { Stop(); return false; }
            return true;
        }

        public static void Stop()
        {
            if (!isOpen) return;
            mciSendString("stop " + Alias, null, 0, IntPtr.Zero);
            mciSendString("close " + Alias, null, 0, IntPtr.Zero);
            isOpen = false;
        }

        /// <summary>True while a sound is playing. Closes the player once the sound has finished.</summary>
        public static bool IsPlaying
        {
            get
            {
                if (!isOpen) return false;
                StringBuilder mode = new StringBuilder(32);
                mciSendString("status " + Alias + " mode", mode, mode.Capacity, IntPtr.Zero);
                if (mode.ToString() == "playing") return true;
                Stop();
                return false;
            }
        }

        /// <summary>
        /// Copies a chosen sound into %APPDATA%\PrayerTray so it keeps working even if the
        /// original file is moved or deleted. Returns the copy's path.
        /// </summary>
        public static string Import(string sourcePath, string baseName)
        {
            Directory.CreateDirectory(AppSettings.DataDir);
            string dest = Path.Combine(AppSettings.DataDir, baseName + Path.GetExtension(sourcePath).ToLowerInvariant());
            if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) return dest;
            Stop();   // the old copy may be playing
            string tmp = dest + ".tmp";
            File.Copy(sourcePath, tmp, true);
            foreach (string old in Directory.GetFiles(AppSettings.DataDir, baseName + ".*"))
            {
                if (string.Equals(old, tmp, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(old); } catch { }
            }
            File.Move(tmp, dest);
            return dest;
        }

        public static void Remove(string baseName)
        {
            Stop();
            try
            {
                foreach (string old in Directory.GetFiles(AppSettings.DataDir, baseName + ".*")) File.Delete(old);
            }
            catch { }
        }
    }
}
