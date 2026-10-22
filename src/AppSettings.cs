using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PrayerTray
{
    /// <summary>User settings, stored in %APPDATA%\PrayerTray\settings.json</summary>
    class AppSettings
    {
        public string City = "";
        public string Country = "";
        public int Method = 3;
        public int School = 0;
        public bool Notify = true;
        public bool ShowText = true;
        public int OverlayOffset = 0;
        public bool PinAttempted = false;

        // Exact coordinates (set by "Detect my location"). NaN = use City/Country instead.
        public double Latitude = double.NaN;
        public double Longitude = double.NaN;
        public bool AutoLocate = false;

        // Adhan sounds, copied into the data folder. Empty = no sound.
        public string AdhanFile = "";
        public string AdhanName = "";
        public string FajrAdhanFile = "";
        public string FajrAdhanName = "";

        public bool HasCoords { get { return !double.IsNaN(Latitude) && !double.IsNaN(Longitude); } }

        public static string DataDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrayerTray"); }
        }

        static string FilePath { get { return Path.Combine(DataDir, "settings.json"); } }

        public bool IsConfigured { get { return HasCoords || (City.Trim().Length > 0 && Country.Trim().Length > 0); } }

        /// <summary>"Budapest, Hungary" (or just the city when there's no country)</summary>
        public string PlaceName { get { return Country.Trim().Length > 0 ? City + ", " + Country : City; } }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(FilePath)) as Dictionary<string, object>;
                if (d == null) return s;
                s.City = Json.Str(d, "City", s.City);
                s.Country = Json.Str(d, "Country", s.Country);
                s.Method = Json.Int(d, "Method", s.Method);
                s.School = Json.Int(d, "School", s.School);
                s.Notify = Json.Bool(d, "Notify", s.Notify);
                s.ShowText = Json.Bool(d, "ShowText", s.ShowText);
                s.OverlayOffset = Json.Int(d, "OverlayOffset", s.OverlayOffset);
                s.PinAttempted = Json.Bool(d, "PinAttempted", s.PinAttempted);
                s.Latitude = Json.Dbl(d, "Latitude", double.NaN);
                s.Longitude = Json.Dbl(d, "Longitude", double.NaN);
                s.AutoLocate = Json.Bool(d, "AutoLocate", s.AutoLocate);
                s.AdhanFile = Json.Str(d, "AdhanFile", "");
                s.AdhanName = Json.Str(d, "AdhanName", "");
                s.FajrAdhanFile = Json.Str(d, "FajrAdhanFile", "");
                s.FajrAdhanName = Json.Str(d, "FajrAdhanName", "");
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                var d = new Dictionary<string, object>();
                d["City"] = City;
                d["Country"] = Country;
                d["Method"] = Method;
                d["School"] = School;
                d["Notify"] = Notify;
                d["ShowText"] = ShowText;
                d["OverlayOffset"] = OverlayOffset;
                d["PinAttempted"] = PinAttempted;
                d["Latitude"] = HasCoords ? (object)Latitude : null;
                d["Longitude"] = HasCoords ? (object)Longitude : null;
                d["AutoLocate"] = AutoLocate;
                d["AdhanFile"] = AdhanFile;
                d["AdhanName"] = AdhanName;
                d["FajrAdhanFile"] = FajrAdhanFile;
                d["FajrAdhanName"] = FajrAdhanName;
                File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(d));
            }
            catch { }
        }
    }

    /// <summary>Small helpers for reading JavaScriptSerializer output.</summary>
    static class Json
    {
        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as Dictionary<string, object>;
            return null;
        }

        public static string Str(Dictionary<string, object> d, string key, string def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null) return Convert.ToString(v, CultureInfo.InvariantCulture);
            return def;
        }

        public static int Int(Dictionary<string, object> d, string key, int def)
        {
            string s = Str(d, key, null);
            int r;
            if (s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out r)) return r;
            return def;
        }

        public static double Dbl(Dictionary<string, object> d, string key, double def)
        {
            string s = Str(d, key, null);
            double r;
            if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out r)) return r;
            return def;
        }

        public static bool Bool(Dictionary<string, object> d, string key, bool def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is bool) return (bool)v;
            return def;
        }
    }

    /// <summary>"Start with Windows" via HKCU\...\Run</summary>
    static class StartupEntry
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "PrayerTray";

        public static bool IsEnabled()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(Name) != null;
        }

        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(Name, "\"" + Application.ExecutablePath + "\"");
                    else if (k.GetValue(Name) != null) k.DeleteValue(Name, false);
                }
            }
            catch { }
        }

        /// <summary>If the exe was moved, point the startup entry at its new location.</summary>
        public static void RefreshPath()
        {
            if (IsEnabled()) Set(true);
        }
    }

    /// <summary>Calculation methods supported by the AlAdhan API.</summary>
    static class Methods
    {
        public static readonly int[] Ids = { 3, 5, 2, 4, 1, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
        public static readonly string[] Names =
        {
            "Muslim World League",
            "Egyptian General Authority of Survey",
            "Islamic Society of North America (ISNA)",
            "Umm al-Qura University, Makkah",
            "University of Islamic Sciences, Karachi",
            "Institute of Geophysics, University of Tehran",
            "Gulf Region",
            "Kuwait",
            "Qatar",
            "Majlis Ugama Islam Singapura",
            "Union des Organisations Islamiques de France",
            "Diyanet Isleri Baskanligi, Turkey",
            "Spiritual Administration of Muslims of Russia",
            "Moonsighting Committee Worldwide",
            "Dubai",
            "JAKIM, Malaysia",
            "Tunisia",
            "Algeria",
            "KEMENAG, Indonesia",
            "Morocco",
            "Comunidade Islamica de Lisboa",
            "Jordan"
        };

        public static int IndexOf(int id)
        {
            int i = Array.IndexOf(Ids, id);
            return i < 0 ? 0 : i;
        }
    }
}
