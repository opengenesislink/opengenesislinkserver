// SPDX-License-Identifier: MPL-2.0
// OpenGenesisLINK native viewer weather state. This is a *rendering contract*;
// unmodified Firestorm cannot render its precipitation without an emitter.
using System;
using System.Text.Json;

namespace OpenGenesisLINK.Addons.OGLWeather
{
    public static class OglWeatherViewerState
    {
        /// <summary>
        /// The CAPS flag reports live managed emitters in this region, never
        /// merely that the feature is enabled in the simulator configuration.
        /// A missing estate owner, empty region, or failed rez must not cause
        /// a future native viewer to suppress precipitation.
        /// </summary>
        public static bool LegacyEmittersActive(bool enabled, int liveEmitterCount)
        {
            return enabled && liveEmitterCount > 0;
        }

        public static string Serialize(string regionId, DateTimeOffset utc,
            int seed, TimeZoneInfo timeZone, int cloudBaseMeters,
            bool legacyParticleEmitters = false)
        {
            if (string.IsNullOrWhiteSpace(regionId))
                throw new ArgumentException("Region ID required", nameof(regionId));
            if (timeZone == null)
                throw new ArgumentNullException(nameof(timeZone));

            cloudBaseMeters = Math.Clamp(cloudBaseMeters, 64, 2048);
            DateTime local = TimeZoneInfo.ConvertTime(utc, timeZone).Date;
            WeatherReading current = OglWeatherModel.At(utc, seed, timeZone);
            var forecast = new object[5];
            for (int i = 0; i < 5; i++)
            {
                WeatherDay day = OglWeatherModel.ForecastDay(
                    local.AddDays(i + 1), seed, timeZone);
                forecast[i] = new
                {
                    date = day.Date.ToString("yyyy-MM-dd"),
                    condition = day.Condition,
                    min_c = day.MinC,
                    max_c = day.MaxC,
                    precipitation_hours = day.PrecipitationHours
                };
            }

            // Region-scoped read-only state; the viewer must generate its own
            // GPU precipitation without virtual/hidden in-world primitives.
            return JsonSerializer.Serialize(new
            {
                protocol = "ogl.weather",
                version = 1,
                region_id = regionId,
                valid_at_utc = utc.ToString("O"),
                precipitation = new
                {
                    condition = current.Condition,
                    intensity = current.Intensity,
                    temperature_c = current.TemperatureC,
                    wind_kmh = current.WindKmh,
                    cloud_base_m = cloudBaseMeters,
                    origin = "clouds",
                    rendering = "viewer_native",
                    requires_viewer_support = true,
                    legacy_prim_emitters_active = legacyParticleEmitters
                },
                forecast
            });
        }
    }
}
