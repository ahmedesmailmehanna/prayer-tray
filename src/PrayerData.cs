using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace PrayerTray
{
    class PrayerTime
    {
        public string Name;
        public DateTime Time;
    }

    class DayTimes
    {
        public DateTime Day;
        public string Hijri;
        public List<PrayerTime> Times;
    }

    class LocationNotFoundException : Exception { }

    /// <summary>
    /// Downloads prayer times from api.aladhan.com and keeps at least the next 7 days
    /// cached on disk, so the app keeps working offline.
    /// </summary>
    class PrayerData
    {
        public static readonly string[] Names = { "Fajr", "Sunrise", "Dhuhr", "Asr", "Maghrib", "Isha" };
        static readonly string[] HijriMonths =
        {
            "Muharram", "Safar", "Rabi al-Awwal", "Rabi al-Thani", "Jumada al-Ula", "Jumada al-Akhirah",
            "Rajab", "Shaban", "Ramadan", "Shawwal", "Dhul-Qadah", "Dhul-Hijjah"
        };
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const int DaysAhead = 7;
        public static string ApiBase = Environment.GetEnvironmentVariable("PRAYERTRAY_API") ?? "https://api.aladhan.com/v1/";

        readonly AppSettings cfg;
        readonly string cacheDir;
        readonly string tag;
        DayTimes today, tomorrow;
        DateTime lastTry = DateTime.MinValue;
        volatile bool syncing, forceSync, reloadPending;

        public DayTimes Today { get { return today; } }

        public PrayerData(AppSettings cfg)
        {
            this.cfg = cfg;
            cacheDir = Path.Combine(AppSettings.DataDir, "cache");
            Directory.CreateDirectory(cacheDir);
            string where = cfg.HasCoords
                ? "geo_" + cfg.Latitude.ToString("0.00", Inv) + "_" + cfg.Longitude.ToString("0.00", Inv)
                : Safe(cfg.City) + "_" + Safe(cfg.Country);
            tag = where + "_m" + cfg.Method + "_s" + cfg.School;
        }

        static string Safe(string s) { return Regex.Replace(s.Trim(), @"[^\w]", "_"); }

        string CacheFile(DateTime d)
        {
            return Path.Combine(cacheDir, d.ToString("dd-MM-yyyy", Inv) + "_" + tag + ".json");
        }

        bool WeekCached()
        {
            DateTime d0 = DateTime.Today;
            for (int i = 0; i <= DaysAhead; i++)
                if (!File.Exists(CacheFile(d0.AddDays(i)))) return false;
            return true;
        }

        /// <summary>Called every second on the UI thread. Never blocks on the network.</summary>
        public void Tick()
        {
            if (!syncing && (forceSync || !WeekCached()) && (DateTime.Now - lastTry).TotalSeconds >= 60)
            {
                lastTry = DateTime.Now;
                syncing = true;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { Sync(); } catch { }
                    finally { syncing = false; }
                });
            }
            if (reloadPending) { reloadPending = false; today = null; tomorrow = null; }

            DateTime t = DateTime.Today;
            if (today == null || today.Day != t) today = LoadDay(t);
            if (tomorrow == null || tomorrow.Day != t.AddDays(1)) tomorrow = LoadDay(t.AddDays(1));
        }

        /// <summary>Re-download even though the week is cached (old data is kept until replaced).</summary>
        public void ForceRefresh()
        {
            forceSync = true;
            lastTry = DateTime.MinValue;
        }

        public PrayerTime Next()
        {
            if (today == null) return null;
            DateTime now = DateTime.Now;
            foreach (PrayerTime p in today.Times)
                if (p.Name != "Sunrise" && p.Time > now) return p;
            if (tomorrow != null) return tomorrow.Times[0];
            // Offline after Isha: estimate tomorrow's Fajr from today's
            return new PrayerTime { Name = "Fajr", Time = today.Times[0].Time.AddDays(1) };
        }

        // Runs on a background thread
        void Sync()
        {
            DateTime d0 = DateTime.Today;
            var months = new List<string>();
            for (int i = 0; i <= DaysAhead; i++)
            {
                DateTime d = d0.AddDays(i);
                string k = d.Year + "/" + d.Month;
                if (!months.Contains(k)) months.Add(k);
            }

            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            foreach (string ym in months)
            {
                Dictionary<string, object> root;
                if (cfg.HasCoords)
                {
                    string q = CoordQuery(cfg.Latitude, cfg.Longitude, cfg.Method, cfg.School);
                    try { root = Api.Get(ApiBase + "calendar/" + ym + q); }
                    catch (LocationNotFoundException)
                    {
                        // Older form of the same endpoint
                        string[] parts = ym.Split('/');
                        root = Api.Get(ApiBase + "calendar" + q + "&year=" + parts[0] + "&month=" + parts[1]);
                    }
                }
                else
                {
                    root = Api.Get(ApiBase + "calendarByCity/" + ym + Query(cfg.City, cfg.Country, cfg.Method, cfg.School));
                }
                object[] days = null;
                object raw;
                if (root.TryGetValue("data", out raw)) days = raw as object[];
                if (days == null) throw new Exception("Unexpected response");

                foreach (object o in days)
                {
                    var day = o as Dictionary<string, object>;
                    var greg = Json.Obj(Json.Obj(day, "date"), "gregorian");
                    DateTime d = DateTime.ParseExact(Json.Str(greg, "date", ""), "dd-MM-yyyy", Inv);
                    string file = CacheFile(d);
                    string tmp = file + ".tmp";
                    File.WriteAllText(tmp, ser.Serialize(day), Encoding.UTF8);
                    if (File.Exists(file)) File.Replace(tmp, file, null);
                    else File.Move(tmp, file);
                }
            }

            // Forget days that are over
            foreach (string f in Directory.GetFiles(cacheDir))
            {
                string n = Path.GetFileName(f);
                DateTime d;
                if (n.Length >= 10 && DateTime.TryParseExact(n.Substring(0, 10), "dd-MM-yyyy", Inv, DateTimeStyles.None, out d)
                    && d < d0.AddDays(-1))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            forceSync = false;
            reloadPending = true;
        }

        DayTimes LoadDay(DateTime day)
        {
            try
            {
                string f = CacheFile(day);
                if (!File.Exists(f)) return null;
                var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(f, Encoding.UTF8)) as Dictionary<string, object>;
                var timings = Json.Obj(d, "timings");
                var list = new List<PrayerTime>();
                foreach (string name in Names)
                {
                    string hhmm = Json.Str(timings, name, "").Trim();       // "05:12 (CET)"
                    if (hhmm.Length < 5) return null;
                    int h = int.Parse(hhmm.Substring(0, 2), Inv);
                    int m = int.Parse(hhmm.Substring(3, 2), Inv);
                    list.Add(new PrayerTime { Name = name, Time = day.Date.AddHours(h).AddMinutes(m) });
                }
                var hijri = Json.Obj(Json.Obj(d, "date"), "hijri");
                int month = Json.Int(Json.Obj(hijri, "month"), "number", 0);
                string hijriText = "";
                if (month >= 1 && month <= 12)
                    hijriText = string.Format("{0} {1} {2} AH", Json.Int(hijri, "day", 0), HijriMonths[month - 1], Json.Str(hijri, "year", ""));
                return new DayTimes { Day = day.Date, Hijri = hijriText, Times = list };
            }
            catch { return null; }
        }

        public static string Query(string city, string country, int method, int school)
        {
            return "?city=" + Uri.EscapeDataString(city.Trim()) + "&country=" + Uri.EscapeDataString(country.Trim())
                 + "&method=" + method + "&school=" + school;
        }

        public static string CoordQuery(double lat, double lon, int method, int school)
        {
            return "?latitude=" + lat.ToString("0.######", Inv) + "&longitude=" + lon.ToString("0.######", Inv)
                 + "&method=" + method + "&school=" + school;
        }
    }

    static class Http
    {
        /// <summary>GET a URL and parse the JSON object it returns.</summary>
        public static Dictionary<string, object> GetJson(string url, int timeoutMs)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.UserAgent = "PrayerTray";
            req.Accept = "application/json";
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            string body;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                body = r.ReadToEnd();
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return ser.DeserializeObject(body) as Dictionary<string, object>;
        }
    }

    static class Api
    {
        public static Dictionary<string, object> Get(string url)
        {
            Dictionary<string, object> d;
            try
            {
                d = Http.GetJson(url, 15000);
            }
            catch (WebException ex)
            {
                var hr = ex.Response as HttpWebResponse;
                if (hr != null && ((int)hr.StatusCode == 400 || (int)hr.StatusCode == 404)) throw new LocationNotFoundException();
                throw;
            }
            if (d == null || Json.Int(d, "code", 0) != 200) throw new LocationNotFoundException();
            return d;
        }

        /// <summary>Checks a city/country exists. Returns its time zone (e.g. "Europe/Budapest").</summary>
        public static string CheckLocation(string city, string country)
        {
            var d = Get(PrayerData.ApiBase + "timingsByCity" + PrayerData.Query(city, country, 3, 0));
            return Json.Str(Json.Obj(Json.Obj(d, "data"), "meta"), "timezone", "");
        }

        /// <summary>Checks prayer times can be calculated for these coordinates.</summary>
        public static string CheckCoords(double lat, double lon)
        {
            var d = Get(PrayerData.ApiBase + "timings" + PrayerData.CoordQuery(lat, lon, 3, 0));
            return Json.Str(Json.Obj(Json.Obj(d, "data"), "meta"), "timezone", "");
        }
    }

    static class Fmt
    {
        /// <summary>Clock time in the user's Windows format (24h or 12h).</summary>
        public static string Time(DateTime t)
        {
            return t.ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern);
        }

        /// <summary>"1:07:12"</summary>
        public static string Clock(TimeSpan s)
        {
            return string.Format("{0}:{1:D2}:{2:D2}", (int)Math.Floor(s.TotalHours), s.Minutes, s.Seconds);
        }

        /// <summary>"1h 07m 12s" / "7m 12s"</summary>
        public static string Long(TimeSpan s)
        {
            if (s.TotalHours >= 1) return string.Format("{0}h {1:D2}m {2:D2}s", (int)Math.Floor(s.TotalHours), s.Minutes, s.Seconds);
            return string.Format("{0}m {1:D2}s", s.Minutes, s.Seconds);
        }

        /// <summary>"1h 8m" / "8 min" (rounded up)</summary>
        public static string Short(TimeSpan s)
        {
            int m = (int)Math.Ceiling(s.TotalMinutes);
            if (m >= 60) return string.Format("{0}h {1}m", m / 60, m % 60);
            return m + " min";
        }
    }
}
