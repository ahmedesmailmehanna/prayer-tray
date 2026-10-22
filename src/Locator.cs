using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;

namespace PrayerTray
{
    class LocationResult
    {
        public string City = "";
        public string Country = "";
        public double Latitude;
        public double Longitude;
        public string Source = "";

        public string Display
        {
            get { return Country.Length > 0 ? City + ", " + Country : City; }
        }
    }

    /// <summary>
    /// Finds where the PC is. Tries Windows Location first (accurate, needs Location turned on
    /// in Windows privacy settings), then falls back to the internet connection's approximate
    /// location. Runs on a background thread; returns null if nothing worked.
    /// </summary>
    static class Locator
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static LocationResult Detect()
        {
            LocationResult r = null;
            try { r = FromWindows(); } catch { r = null; }
            if (r == null) r = FromInternet();
            return r;
        }

        // ---------- Windows Location (System.Device, loaded at runtime) ----------

        static LocationResult FromWindows()
        {
            Assembly asm = LoadSystemDevice();
            if (asm == null) return null;
            Type watcherType = asm.GetType("System.Device.Location.GeoCoordinateWatcher", true);
            Type accuracyType = asm.GetType("System.Device.Location.GeoPositionAccuracy", true);
            object watcher = Activator.CreateInstance(watcherType, Enum.Parse(accuracyType, "Default"));
            try
            {
                MethodInfo tryStart = watcherType.GetMethod("TryStart", new[] { typeof(bool), typeof(TimeSpan) });
                bool started = (bool)tryStart.Invoke(watcher, new object[] { true, TimeSpan.FromSeconds(10) });
                if (!started) return null;

                PropertyInfo positionProp = watcherType.GetProperty("Position");
                for (int i = 0; i < 50; i++)
                {
                    object position = positionProp.GetValue(watcher, null);
                    object loc = position.GetType().GetProperty("Location").GetValue(position, null);
                    bool unknown = (bool)loc.GetType().GetProperty("IsUnknown").GetValue(loc, null);
                    if (!unknown)
                    {
                        double lat = (double)loc.GetType().GetProperty("Latitude").GetValue(loc, null);
                        double lon = (double)loc.GetType().GetProperty("Longitude").GetValue(loc, null);
                        LocationResult r = ReverseGeocode(lat, lon);
                        r.Source = "Windows Location";
                        return r;
                    }
                    Thread.Sleep(200);
                }
                return null;
            }
            finally
            {
                IDisposable d = watcher as IDisposable;
                if (d != null) d.Dispose();
            }
        }

        static Assembly LoadSystemDevice()
        {
            foreach (string token in new[] { "b77a5c561934e089", "b03f5f7f11d50a3a" })
            {
                try { return Assembly.Load("System.Device, Version=4.0.0.0, Culture=neutral, PublicKeyToken=" + token); }
                catch { }
            }
            return null;
        }

        /// <summary>Turns coordinates into a city and country name (for display only).</summary>
        static LocationResult ReverseGeocode(double lat, double lon)
        {
            LocationResult r = new LocationResult { Latitude = lat, Longitude = lon };
            try
            {
                var d = Http.GetJson("https://api.bigdatacloud.net/data/reverse-geocode-client?latitude="
                    + lat.ToString("0.######", Inv) + "&longitude=" + lon.ToString("0.######", Inv)
                    + "&localityLanguage=en", 10000);
                r.City = First(Json.Str(d, "city", ""), Json.Str(d, "locality", ""), Json.Str(d, "principalSubdivision", ""));
                r.Country = Json.Str(d, "countryName", "");
            }
            catch { }
            if (r.City.Length == 0)
                r.City = lat.ToString("0.00", Inv) + ", " + lon.ToString("0.00", Inv);
            return r;
        }

        // ---------- Approximate location from the internet connection ----------

        static LocationResult FromInternet()
        {
            // ipapi.co
            try
            {
                var d = Http.GetJson("https://ipapi.co/json/", 10000);
                if (!Json.Bool(d, "error", false))
                {
                    LocationResult r = Make(Json.Str(d, "city", ""), Json.Str(d, "country_name", ""),
                                            Json.Dbl(d, "latitude", double.NaN), Json.Dbl(d, "longitude", double.NaN));
                    if (r != null) return r;
                }
            }
            catch { }

            // ipwho.is
            try
            {
                var d = Http.GetJson("https://ipwho.is/", 10000);
                if (Json.Bool(d, "success", false))
                {
                    LocationResult r = Make(Json.Str(d, "city", ""), Json.Str(d, "country", ""),
                                            Json.Dbl(d, "latitude", double.NaN), Json.Dbl(d, "longitude", double.NaN));
                    if (r != null) return r;
                }
            }
            catch { }

            // ipinfo.io ("loc": "47.49,19.04", "country": "HU")
            try
            {
                var d = Http.GetJson("https://ipinfo.io/json", 10000);
                string[] loc = Json.Str(d, "loc", "").Split(',');
                double lat, lon;
                if (loc.Length == 2
                    && double.TryParse(loc[0], NumberStyles.Float, Inv, out lat)
                    && double.TryParse(loc[1], NumberStyles.Float, Inv, out lon))
                {
                    string country = Json.Str(d, "country", "");
                    try { country = new RegionInfo(country).EnglishName; } catch { }
                    LocationResult r = Make(Json.Str(d, "city", ""), country, lat, lon);
                    if (r != null) return r;
                }
            }
            catch { }

            return null;
        }

        static LocationResult Make(string city, string country, double lat, double lon)
        {
            if (double.IsNaN(lat) || double.IsNaN(lon) || (lat == 0 && lon == 0)) return null;
            if (city.Length == 0) city = lat.ToString("0.00", Inv) + ", " + lon.ToString("0.00", Inv);
            return new LocationResult { City = city, Country = country, Latitude = lat, Longitude = lon, Source = "your internet connection" };
        }

        static string First(params string[] values)
        {
            foreach (string v in values) if (!string.IsNullOrEmpty(v)) return v;
            return "";
        }

        /// <summary>Distance in km between two points.</summary>
        public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double r = 6371, dLat = (lat2 - lat1) * Math.PI / 180, dLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * r * Math.Asin(Math.Sqrt(a));
        }
    }
}
