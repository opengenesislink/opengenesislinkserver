// SPDX-License-Identifier: MPL-2.0
// Read-only XML/TTS feed for OGLWeather. Shares the same deterministic
// climate model as the simulator and existing JSON World API projection.
// This endpoint only prepares text: no OpenAI/ElevenLabs keys or network calls.
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace OpenGenesisLINK.Addons.OGLWeather
{
    public static class OglWeatherXmlProjection
    {
        private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

        public static string Serialize(DateTimeOffset utc, int seed,
            TimeZoneInfo zone, string displayName)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name must not be empty", nameof(displayName));

            DateTimeOffset instant = utc.ToUniversalTime();
            DateTimeOffset local = TimeZoneInfo.ConvertTime(instant, zone);
            WeatherReading current = OglWeatherModel.At(instant, seed, zone);
            WeatherDay today = OglWeatherModel.ForecastDay(local.Date, seed, zone);
            WeatherDay[] days = new WeatherDay[5];
            for (int i = 0; i < days.Length; ++i)
                days[i] = OglWeatherModel.ForecastDay(
                    local.Date.AddDays(i + 1), seed, zone);

            string narration = Narration(local, displayName.Trim(), current, days);

            // Use a UTF-8 stream instead of XmlWriter(StringBuilder), which
            // would incorrectly declare the resulting XML as UTF-16.
            using var stream = new MemoryStream();
            using (XmlWriter xml = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                CloseOutput = false
            }))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("oglweather");
                xml.WriteAttributeString("version", "1");
                xml.WriteAttributeString("source", "simulated_oglweather");
                xml.WriteAttributeString("simulation", "true");

                xml.WriteStartElement("meta");
                xml.WriteElementString("display_name", displayName.Trim());
                xml.WriteElementString("timezone", zone.Id);
                xml.WriteElementString("language", "de-DE");
                xml.WriteElementString("observed_at_utc", instant.ToString("O", CultureInfo.InvariantCulture));
                xml.WriteElementString("local_time", local.ToString("O", CultureInfo.InvariantCulture));
                xml.WriteElementString("temperature_unit", "C");
                xml.WriteElementString("wind_unit", "km/h");
                xml.WriteEndElement();

                xml.WriteStartElement("current");
                xml.WriteAttributeString("condition", current.Condition);
                xml.WriteAttributeString("condition_de", OglWeatherPortalProjection.ConditionLabelDe(current.Condition));
                xml.WriteAttributeString("temperature_c", Number(current.TemperatureC));
                xml.WriteAttributeString("wind_kmh", Number(current.WindKmh));
                xml.WriteAttributeString("precipitation_intensity_percent", Number(current.Intensity));
                xml.WriteEndElement();

                xml.WriteStartElement("today");
                WriteDay(xml, today);
                xml.WriteEndElement();

                xml.WriteStartElement("forecast");
                xml.WriteAttributeString("days", "5");
                foreach (WeatherDay day in days)
                    WriteDay(xml, day);
                xml.WriteEndElement();

                // Plain prose for ElevenLabs TTS; may also be fed into an
                // OpenAI text workflow for editing before speech synthesis.
                // No SSML, executable instructions or provider API requests.
                xml.WriteStartElement("narration");
                xml.WriteAttributeString("language", "de-DE");
                xml.WriteAttributeString("format", "plain-text");
                xml.WriteString(narration);
                xml.WriteEndElement();

                xml.WriteEndElement();
                xml.WriteEndDocument();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static string Number(int value) =>
            value.ToString(CultureInfo.InvariantCulture);

        private static void WriteDay(XmlWriter xml, WeatherDay day)
        {
            xml.WriteStartElement("day");
            xml.WriteAttributeString("date", day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            xml.WriteAttributeString("weekday_de", day.Date.ToString("dddd", De));
            xml.WriteAttributeString("condition", day.Condition);
            xml.WriteAttributeString("condition_de", OglWeatherPortalProjection.ConditionLabelDe(day.Condition));
            xml.WriteAttributeString("temperature_min_c", Number(day.MinC));
            xml.WriteAttributeString("temperature_max_c", Number(day.MaxC));
            xml.WriteAttributeString("precipitation_hours", Number(day.PrecipitationHours));
            xml.WriteEndElement();
        }

        private static string SpeakTemperature(int value)
        {
            return value < 0
                ? "minus " + Number(-value) + " Grad Celsius"
                : Number(value) + " Grad Celsius";
        }

        private static string SpeakWeather(string code)
        {
            return code switch
            {
                "sun" => "sonnig",
                "cloud" => "bewölkt",
                "rain" => "regnerisch",
                "snow" => "verschneit",
                _ => "wechselhaft"
            };
        }

        private static string Narration(DateTimeOffset local, string name,
            WeatherReading now, WeatherDay[] days)
        {
            var text = new StringBuilder(900);
            text.Append("Hier ist der Wetterbericht für ").Append(name).Append(". ");
            text.Append("Stand: ").Append(local.ToString("dddd, d. MMMM yyyy", De))
                .Append(", ").Append(local.ToString("HH:mm", De)).Append(" Uhr. ");
            text.Append("Aktuell ist es ").Append(SpeakWeather(now.Condition))
                .Append(" bei ").Append(SpeakTemperature(now.TemperatureC)).Append(". ");
            text.Append("Der Wind erreicht ").Append(Number(now.WindKmh))
                .Append(" Kilometer pro Stunde. ");
            if (now.Condition == "rain" || now.Condition == "snow")
                text.Append("Die Niederschlagsintensität beträgt ")
                    .Append(Number(now.Intensity)).Append(" Prozent. ");

            text.Append("Die Vorhersage für die nächsten fünf Tage: ");
            foreach (WeatherDay day in days)
            {
                text.Append(day.Date.ToString("dddd, d. MMMM", De)).Append(": ")
                    .Append(SpeakWeather(day.Condition)).Append(", ")
                    .Append("Temperaturen zwischen ")
                    .Append(SpeakTemperature(day.MinC)).Append(" und ")
                    .Append(SpeakTemperature(day.MaxC)).Append(". ");
                if (day.PrecipitationHours > 0)
                    text.Append("Etwa ").Append(Number(day.PrecipitationHours))
                        .Append(day.PrecipitationHours == 1
                            ? " Stunde mit Niederschlag. "
                            : " Stunden mit Niederschlag. ");
            }
            text.Append("Das war der simulierte Wetterdienst von Stadt-NexVerse.");
            return text.ToString();
        }
    }
}
