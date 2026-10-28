// SPDX-License-Identifier: MPL-2.0
// Public, frontend-ready projection of the SAME deterministic OGLWeather model
// used in all simulator regions. No live weather API, storage or HTTP calls.
using System;
using System.Globalization;
using System.Text.Json;

namespace OpenGenesisLINK.Addons.OGLWeather
{
    public static class OglWeatherPortalProjection
    {
        public static string ConditionLabelDe(string code)
        {
            return code switch
            {
                "sun" => "Sonnig",
                "cloud" => "Bewölkt",
                "rain" => "Regen",
                "snow" => "Schnee",
                _ => "Unbekannt"
            };
        }

        public static string ConditionIcon(string code)
        {
            return code switch
            {
                "sun" => "sun",
                "cloud" => "cloud",
                "rain" => "cloud-rain",
                "snow" => "cloud-snow",
                _ => "cloud"
            };
        }

        private static object ProjectDay(WeatherDay day)
        {
            return new
            {
                date = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                weekday_de = day.Date.ToString("ddd", CultureInfo.GetCultureInfo("de-DE")),
                condition = day.Condition,
                condition_label_de = ConditionLabelDe(day.Condition),
                icon = ConditionIcon(day.Condition),
                temperature_min_c = day.MinC,
                temperature_max_c = day.MaxC,
                precipitation_hours = day.PrecipitationHours
            };
        }

        public static string Serialize(DateTimeOffset utc, int seed,
            TimeZoneInfo zone, string displayName)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name must not be empty", nameof(displayName));

            DateTimeOffset instant = utc.ToUniversalTime();
            DateTimeOffset localInstant = TimeZoneInfo.ConvertTime(instant, zone);
            WeatherReading now = OglWeatherModel.At(instant, seed, zone);
            WeatherDay today = OglWeatherModel.ForecastDay(localInstant.Date, seed, zone);
            object[] days = new object[5];
            for (int i = 0; i < days.Length; i++)
                days[i] = ProjectDay(OglWeatherModel.ForecastDay(
                    localInstant.Date.AddDays(i + 1), seed, zone));

            // Step by UTC hours, not naive local time, so the 23h/25h DST
            // transition can be displayed without duplicating or losing instants.
            DateTimeOffset hourStart = new DateTimeOffset(
                instant.UtcDateTime.Date.AddHours(instant.Hour), TimeSpan.Zero);
            object[] hourly = new object[24];
            for (int i = 0; i < hourly.Length; i++)
            {
                DateTimeOffset hourUtc = hourStart.AddHours(i);
                DateTimeOffset hourLocal = TimeZoneInfo.ConvertTime(hourUtc, zone);
                WeatherReading weather = OglWeatherModel.At(hourUtc, seed, zone);
                hourly[i] = new
                {
                    time_utc = hourUtc.ToString("O", CultureInfo.InvariantCulture),
                    time_local = hourLocal.ToString("O", CultureInfo.InvariantCulture),
                    hour = hourLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
                    condition = weather.Condition,
                    condition_label_de = ConditionLabelDe(weather.Condition),
                    icon = ConditionIcon(weather.Condition),
                    temperature_c = weather.TemperatureC,
                    precipitation_intensity_percent = weather.Intensity,
                    wind_kmh = weather.WindKmh
                };
            }

            return JsonSerializer.Serialize(new
            {
                protocol = "ogl.weather.portal",
                version = 1,
                source = "simulated_oglweather",
                simulation = true,
                display_name = displayName.Trim(),
                timezone = zone.Id,
                observed_at_utc = instant.ToString("O", CultureInfo.InvariantCulture),
                local_time = localInstant.ToString("O", CultureInfo.InvariantCulture),
                units = new { temperature = "°C", wind = "km/h" },
                current = new
                {
                    condition = now.Condition,
                    condition_label_de = ConditionLabelDe(now.Condition),
                    icon = ConditionIcon(now.Condition),
                    temperature_c = now.TemperatureC,
                    precipitation_intensity_percent = now.Intensity,
                    wind_kmh = now.WindKmh
                },
                today = ProjectDay(today),
                hourly,
                forecast = days,
                forecast_days = days.Length
            });
        }
    }
}
