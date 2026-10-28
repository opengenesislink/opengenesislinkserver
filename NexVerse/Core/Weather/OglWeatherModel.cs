// SPDX-License-Identifier: MPL-2.0
// Pure, deterministic weather simulation. No external weather API and no network calls.
using System;
using System.Globalization;

namespace OpenGenesisLINK.Addons.OGLWeather
{
    public sealed class WeatherReading
    {
        public string Condition { get; set; }
        public int TemperatureC { get; set; }
        public int Intensity { get; set; }
        public int WindKmh { get; set; }
    }

    public sealed class WeatherDay
    {
        public DateTime Date { get; set; }
        public string Condition { get; set; }
        public int MinC { get; set; }
        public int MaxC { get; set; }
        public int PrecipitationHours { get; set; }
    }

    /// <summary>
    /// Every forecast and every actual state comes from the same immutable
    /// UTC-to-local calculation. The public seed defines a simulator-wide
    /// climate. There is nothing to persist or revise on restart.
    /// </summary>
    public static class OglWeatherModel
    {
        public static WeatherReading At(DateTimeOffset utc, int seed, TimeZoneInfo zone)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            return AtLocal(TimeZoneInfo.ConvertTime(utc, zone).DateTime, seed);
        }

        public static WeatherReading AtLocal(DateTime local, int seed)
        {
            DateTime day = local.Date;
            int ordinal = day.DayOfYear;
            int segment = local.Hour / 6;

            // July warmth, January frost and a smooth afternoon temperature peak.
            // The fixed daily anomaly is shared by all hourly predictions.
            int anomaly = (int)(Hash(seed, day.Year, ordinal, 0, 11) % 9) - 4;
            double seasonal = 11.5 + 12.5 * Math.Cos(
                2.0 * Math.PI * (ordinal - 204.0) / 365.2425);
            double diurnal = 4.0 * Math.Cos(
                2.0 * Math.PI * (local.Hour - 15.0) / 24.0);
            int temperature = (int)Math.Round(seasonal + diurnal + anomaly,
                MidpointRounding.AwayFromZero);

            // Exactly four fixed six-hour precipitation periods per local day.
            // They never depend on time of query or mutable random state.
            int chance = (int)(Hash(seed, day.Year, ordinal, segment, 23) % 100);
            string weather = chance < 20 ? "sun" :
                chance < 64 ? "cloud" : temperature <= 1 ? "snow" : "rain";
            bool wet = weather == "rain" || weather == "snow";
            return new WeatherReading
            {
                Condition = weather,
                TemperatureC = temperature,
                Intensity = wet
                    ? 20 + (int)(Hash(seed, day.Year, ordinal, segment, 29) % 71)
                    : 0,
                WindKmh = 3 + (int)(Hash(seed, day.Year, ordinal, segment, 31) % 36)
            };
        }

        public static WeatherDay ForecastDay(DateTime localDate, int seed, TimeZoneInfo zone)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            DateTime date = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
            // DST creates 23-hour or 25-hour calendar days. Iterate actual
            // UTC instants so predicted precipitation hours always match
            // the hours the simulator truly observes.
            DateTime fromUtc = TimeZoneInfo.ConvertTimeToUtc(date, zone);
            DateTime untilUtc = TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1), zone);
            int min = int.MaxValue;
            int max = int.MinValue;
            int rain = 0;
            int snow = 0;
            int cloud = 0;
            int sunny = 0;

            // Sample every actually occurring hour of this local calendar day.
            for (DateTime hourUtc = fromUtc; hourUtc < untilUtc;
                 hourUtc = hourUtc.AddHours(1))
            {
                DateTime actualLocal = TimeZoneInfo.ConvertTimeFromUtc(hourUtc, zone);
                WeatherReading weather = AtLocal(actualLocal, seed);
                min = Math.Min(min, weather.TemperatureC);
                max = Math.Max(max, weather.TemperatureC);
                if (weather.Condition == "rain") rain++;
                else if (weather.Condition == "snow") snow++;
                else if (weather.Condition == "cloud") cloud++;
                else sunny++;
            }

            // A day labelled as rain/snow contains that actual precipitation
            // in the forecast model. A mix is reported as its larger share.
            string summary = snow > 0 && snow >= rain ? "snow" :
                rain > 0 ? "rain" : cloud >= sunny ? "cloud" : "sun";

            return new WeatherDay
            {
                Date = date, MinC = min, MaxC = max,
                Condition = summary, PrecipitationHours = rain + snow
            };
        }

        public static string NowWire(DateTimeOffset utc, int seed, TimeZoneInfo zone)
        {
            DateTime local = TimeZoneInfo.ConvertTime(utc, zone).DateTime;
            WeatherReading reading = AtLocal(local, seed);
            return "OGLWX|1|NOW|" +
                local.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture) + "|" +
                reading.Condition + "|" + reading.TemperatureC.ToString(CultureInfo.InvariantCulture) +
                "|" + reading.Intensity.ToString(CultureInfo.InvariantCulture) +
                "|" + reading.WindKmh.ToString(CultureInfo.InvariantCulture);
        }

        public static string DayWire(DateTime localDate, int seed, int index, TimeZoneInfo zone)
        {
            WeatherDay day = ForecastDay(localDate, seed, zone);
            return "OGLWX|1|DAY|" + index.ToString(CultureInfo.InvariantCulture) + "|" +
                day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|" +
                day.Condition + "|" + day.MinC.ToString(CultureInfo.InvariantCulture) +
                "|" + day.MaxC.ToString(CultureInfo.InvariantCulture) +
                "|" + day.PrecipitationHours.ToString(CultureInfo.InvariantCulture);
        }

        private static uint Hash(int seed, int year, int day, int slot, int salt)
        {
            unchecked
            {
                uint h = 2166136261;
                h = (h ^ (uint)seed) * 16777619;
                h = (h ^ (uint)year) * 16777619;
                h = (h ^ (uint)day) * 16777619;
                h = (h ^ (uint)slot) * 16777619;
                h = (h ^ (uint)salt) * 16777619;
                h ^= h >> 16;
                h *= 0x7feb352d;
                h ^= h >> 15;
                h *= 0x846ca68b;
                return h ^ (h >> 16);
            }
        }
    }
}
