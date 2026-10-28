// SPDX-License-Identifier: MPL-2.0
// Temporary operator-only weather overrides for Firestorm precipitation testing.
// No HTTP, LSL write endpoint, database persistence or independent climate model.
using System;
using System.Globalization;

namespace OpenGenesisLINK.Addons.OGLWeather
{
    public sealed class OglWeatherTestOverride
    {
        private readonly object m_Lock = new object();
        private string m_Condition = "auto";
        private int m_Intensity;
        private DateTimeOffset m_ExpiresUtc;

        public bool Activate(string condition, int intensity, int durationMinutes,
            DateTimeOffset now)
        {
            if (condition != "rain" && condition != "snow")
                return false;
            if (intensity < 1 || intensity > 100 ||
                durationMinutes < 1 || durationMinutes > 60)
                return false;

            lock (m_Lock)
            {
                m_Condition = condition;
                m_Intensity = intensity;
                m_ExpiresUtc = now.AddMinutes(durationMinutes);
            }
            return true;
        }

        public void Clear()
        {
            lock (m_Lock)
            {
                m_Condition = "auto";
                m_Intensity = 0;
                m_ExpiresUtc = DateTimeOffset.MinValue;
            }
        }

        public bool TryGet(DateTimeOffset utc, WeatherReading natural,
            out WeatherReading effective)
        {
            if (natural == null)
                throw new ArgumentNullException(nameof(natural));

            lock (m_Lock)
            {
                if (m_Condition == "auto" || utc >= m_ExpiresUtc)
                {
                    if (utc >= m_ExpiresUtc)
                    {
                        m_Condition = "auto";
                        m_Intensity = 0;
                    }
                    effective = natural;
                    return false;
                }

                effective = new WeatherReading
                {
                    Condition = m_Condition,
                    Intensity = m_Intensity,
                    // The renderer's weather phase is consistent with the
                    // snow/rain temperature contract, without altering the
                    // deterministic daily forecast or public World API.
                    TemperatureC = m_Condition == "snow"
                        ? Math.Min(1, natural.TemperatureC)
                        : Math.Max(2, natural.TemperatureC),
                    WindKmh = natural.WindKmh
                };
                return true;
            }
        }

        public string Status(DateTimeOffset utc)
        {
            lock (m_Lock)
            {
                if (m_Condition == "auto" || utc >= m_ExpiresUtc)
                    return "auto";
                return m_Condition + " " +
                    m_Intensity.ToString(CultureInfo.InvariantCulture) +
                    "% until " + m_ExpiresUtc.ToString("O", CultureInfo.InvariantCulture);
            }
        }

        public static string NowWire(DateTimeOffset utc, TimeZoneInfo zone,
            WeatherReading reading)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            if (reading == null) throw new ArgumentNullException(nameof(reading));
            DateTime local = TimeZoneInfo.ConvertTime(utc, zone).DateTime;
            return "OGLWX|1|NOW|" +
                local.ToString("yyyy-MM-dd HH:00", CultureInfo.InvariantCulture) + "|" +
                reading.Condition + "|" +
                reading.TemperatureC.ToString(CultureInfo.InvariantCulture) + "|" +
                reading.Intensity.ToString(CultureInfo.InvariantCulture) + "|" +
                reading.WindKmh.ToString(CultureInfo.InvariantCulture);
        }
    }
}
