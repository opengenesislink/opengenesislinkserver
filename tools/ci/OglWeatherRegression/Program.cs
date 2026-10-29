using System;
using System.Text.Json;
using System.Xml;
using OpenGenesisLINK.Addons.OGLWeather;

internal static class Program
{
    private static int checks;
    private static void Require(bool condition, string message)
    {
        ++checks;
        if (!condition) throw new InvalidOperationException("OGLWeather: " + message);
    }

    public static void Main()
    {
        TimeZoneInfo berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        int seed = 20261028;
        var now = new DateTimeOffset(2026, 10, 28, 16, 0, 0, TimeSpan.Zero);
        WeatherReading same1 = OglWeatherModel.At(now, seed, berlin);
        WeatherReading same2 = OglWeatherModel.At(now, seed, berlin);
        Require(same1.Condition == same2.Condition && same1.TemperatureC == same2.TemperatureC,
            "identical time and seed on independent regions must agree");
        Require(OglWeatherModel.NowWire(now, seed, berlin).StartsWith("OGLWX|1|NOW|"),
            "CURRENT LSL wire contract");

        // One year of predicted future days (including winter/summer, 2027 DST)
        // is compared against all 24 values that WILL occur in the same model.
        int wetDays = 0, snowDays = 0, dryDays = 0;
        DateTime start = new DateTime(2026, 10, 29);
        for (int d = 0; d < 365; ++d)
        {
            DateTime day = start.AddDays(d);
            WeatherDay prediction = OglWeatherModel.ForecastDay(day, seed, berlin);
            Require(prediction.MinC <= prediction.MaxC, "daily temperature limits");
            int countedWet = 0;
            bool sawRain = false, sawSnow = false;
            int min = int.MaxValue, max = int.MinValue;
            DateTime dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(day.Date, DateTimeKind.Unspecified), berlin);
            DateTime dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(day.Date.AddDays(1), DateTimeKind.Unspecified), berlin);
            for (DateTime hourUtc = dayStartUtc; hourUtc < dayEndUtc;
                 hourUtc = hourUtc.AddHours(1))
            {
                DateTime observedLocal = TimeZoneInfo.ConvertTimeFromUtc(hourUtc, berlin);
                WeatherReading observed = OglWeatherModel.AtLocal(observedLocal, seed);
                min = Math.Min(min, observed.TemperatureC);
                max = Math.Max(max, observed.TemperatureC);
                if (observed.Condition == "rain" || observed.Condition == "snow") countedWet++;
                if (observed.Condition == "rain") sawRain = true;
                if (observed.Condition == "snow") sawSnow = true;
                Require(observed.Condition != "snow" || observed.TemperatureC <= 1,
                    "snow requires freezing temperatures");
                Require(observed.Condition != "rain" || observed.TemperatureC > 1,
                    "rain requires positive temperatures");
                Require(observed.Intensity >= 0 && observed.Intensity <= 100,
                    "particle intensity bounded");
            }
            Require(prediction.MinC == min && prediction.MaxC == max,
                "forecast min/max equal actual readings");
            Require(prediction.PrecipitationHours == countedWet,
                "forecast wet hours equal actual rain/snow hours");
            Require(prediction.Condition != "rain" || sawRain,
                "daily rain prediction must actually rain");
            Require(prediction.Condition != "snow" || sawSnow,
                "daily snow prediction must actually snow");
            Require(OglWeatherModel.DayWire(day, seed, d % 5, berlin).StartsWith("OGLWX|1|DAY|"),
                "DAY LSL wire contract");
            if (countedWet > 0) wetDays++; else dryDays++;
            if (sawSnow) snowDays++;
        }
        Require(wetDays > 30 && dryDays > 5 && snowDays > 0,
            "plausible variety across all seasons");
        Require((TimeZoneInfo.ConvertTimeToUtc(new DateTime(2027, 3, 29), berlin) -
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(2027, 3, 28), berlin)).TotalHours == 23,
            "DST spring starts a 23-hour local calendar day");
        Require((TimeZoneInfo.ConvertTimeToUtc(new DateTime(2027, 11, 1), berlin) -
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(2027, 10, 31), berlin)).TotalHours == 25,
            "DST autumn starts a 25-hour local calendar day");

        // Berlin clock moves forward and back; the day-based simulator climate
        // remains stable under repeated queries and server restart.
        foreach (DateTimeOffset instant in new[] {
            new DateTimeOffset(2027, 3, 28, 0, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 10, 31, 0, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 10, 31, 1, 30, 0, TimeSpan.Zero)
        })
        {
            string first = OglWeatherModel.NowWire(instant, seed, berlin);
            string second = OglWeatherModel.NowWire(instant, seed, berlin);
            Require(first == second, "DST and restart yield same weather schedule");
        }

        // CAPS payload is a stable, read-only snapshot derived from the same
        // forecast that is delivered to the LSL weather board.
        string json = OglWeatherViewerState.Serialize(
            "00000000-0000-0000-0000-000000000001", now, seed, berlin, 200);
        using (JsonDocument parsed = JsonDocument.Parse(json))
        {
            JsonElement payload = parsed.RootElement;
            JsonElement precipitation = payload.GetProperty("precipitation");
            Require(payload.GetProperty("protocol").GetString() == "ogl.weather",
                "native viewer protocol identity");
            Require(payload.GetProperty("version").GetInt32() == 1,
                "native viewer protocol version");
            Require(precipitation.GetProperty("origin").GetString() == "clouds",
                "precipitation originates visually from clouds");
            Require(precipitation.GetProperty("rendering").GetString() == "viewer_native",
                "prim-free precipitation requires viewer-native rendering");
            Require(precipitation.GetProperty("requires_viewer_support").GetBoolean(),
                "no false claim of unchanged Firestorm compatibility");
            Require(precipitation.GetProperty("cloud_base_m").GetInt32() == 200,
                "cloud base is the configured height");
            Require(precipitation.GetProperty("condition").GetString() == same1.Condition,
                "CAPS current condition matches shared simulator model");
            JsonElement days = payload.GetProperty("forecast");
            Require(days.GetArrayLength() == 5, "CAPS contains exactly five forecast days");
            DateTime firstDay = TimeZoneInfo.ConvertTime(now, berlin).Date.AddDays(1);
            Require(days[0].GetProperty("date").GetString() ==
                firstDay.ToString("yyyy-MM-dd"), "forecast starts on next local day");
        }
        using (JsonDocument limits = JsonDocument.Parse(
            OglWeatherViewerState.Serialize("region", now, seed, berlin, 9999)))
        {
            Require(limits.RootElement.GetProperty("precipitation")
                .GetProperty("cloud_base_m").GetInt32() == 2048,
                "cloud height must not exceed configured maximum");
        }

        // 256 x 256: moderate number of emitters with guaranteed bounds;
        // 1024 x 1024: distribute across whole varregion, not one corner.
        var normal = OglWeatherEmitterPlan.Positions(256, 256, 96, 16);
        Require(normal.Count == 9, "256 region should use 3 by 3 emitters");
        var large = OglWeatherEmitterPlan.Positions(1024, 1024, 96, 16);
        Require(large.Count <= 16 && large.Count >= 9, "large region bounded");
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var point in large)
        {
            Require(point.X > 0 && point.X < 1024 &&
                point.Y > 0 && point.Y < 1024, "all emitters inside region");
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }
        Require(maxX - minX > 600 && maxY - minY > 600,
            "varregion emitters distributed across whole area");
        var small = OglWeatherEmitterPlan.Positions(256, 256, 1, 99999);
        Require(small.Count <= 64, "malicious config remains bounded");
        Require(OglWeatherEmitterPlan.Positions(128, 128, 256, 16).Count == 1,
            "small region has exactly one fallback emitter");
        // Feature configuration alone never indicates working Firestorm
        // emitters. In particular, an empty region or a failed object add
        // must not switch off future native viewer precipitation.
        Require(!OglWeatherViewerState.LegacyEmittersActive(false, 9),
            "disabled legacy fallback must not suppress native rendering");
        Require(!OglWeatherViewerState.LegacyEmittersActive(true, 0),
            "configured fallback without live prims must report inactive");
        Require(!OglWeatherViewerState.LegacyEmittersActive(true, -1),
            "invalid emitter count must not report active");
        Require(OglWeatherViewerState.LegacyEmittersActive(true, 1),
            "one live managed emitter reports active");
        Require(OglWeatherViewerState.LegacyEmittersActive(true, 9),
            "multiple live managed emitters report active");
        using (JsonDocument inactive = JsonDocument.Parse(
            OglWeatherViewerState.Serialize(
                "region", now, seed, berlin, 200,
                OglWeatherViewerState.LegacyEmittersActive(true, 0))))
        {
            Require(!inactive.RootElement.GetProperty("precipitation")
                .GetProperty("legacy_prim_emitters_active").GetBoolean(),
                "CAPS must not claim legacy emitters when region has none");
        }
        using (JsonDocument legacy = JsonDocument.Parse(
            OglWeatherViewerState.Serialize(
                "region", now, seed, berlin, 200, true)))
        {
            Require(legacy.RootElement.GetProperty("precipitation")
                .GetProperty("legacy_prim_emitters_active").GetBoolean(),
                "native viewers can suppress double precipitation");
        }
        // The portal's weather must be EXACTLY the simulated weather in
        // every corresponding hour, including the CET/CEST clock change.
        foreach (DateTimeOffset instant in new[]
        {
            now,
            new DateTimeOffset(2027, 3, 28, 0, 15, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 3, 28, 1, 15, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 10, 31, 0, 15, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 10, 31, 1, 15, 0, TimeSpan.Zero)
        })
        {
            using JsonDocument portal = JsonDocument.Parse(
                OglWeatherPortalProjection.Serialize(
                    instant, seed, berlin, "Stadt-NexVerse"));
            JsonElement data = portal.RootElement;
            Require(data.GetProperty("protocol").GetString() ==
                "ogl.weather.portal", "portal protocol");
            Require(data.GetProperty("simulation").GetBoolean(),
                "never claim that inworld weather is meteorological data");
            Require(data.GetProperty("timezone").GetString() == berlin.Id,
                "correct time zone");
            WeatherReading current = OglWeatherModel.At(instant, seed, berlin);
            JsonElement apiCurrent = data.GetProperty("current");
            Require(apiCurrent.GetProperty("condition").GetString() ==
                current.Condition, "portal current weather matches simulator");
            Require(apiCurrent.GetProperty("temperature_c").GetInt32() ==
                current.TemperatureC, "portal current temperature matches simulator");
            Require(apiCurrent.GetProperty("wind_kmh").GetInt32() ==
                current.WindKmh, "portal current wind matches simulator");
            Require(apiCurrent.GetProperty("precipitation_intensity_percent").GetInt32() ==
                current.Intensity, "portal current intensity matches simulator");
            JsonElement day = data.GetProperty("today");
            WeatherDay actualDay = OglWeatherModel.ForecastDay(
                TimeZoneInfo.ConvertTime(instant, berlin).Date, seed, berlin);
            Require(day.GetProperty("temperature_min_c").GetInt32() == actualDay.MinC,
                "portal daily low matches simulator model");
            Require(day.GetProperty("temperature_max_c").GetInt32() == actualDay.MaxC,
                "portal daily high matches simulator model");
            Require(day.GetProperty("precipitation_hours").GetInt32() ==
                actualDay.PrecipitationHours, "portal wet hours match simulator");
            JsonElement hourly = data.GetProperty("hourly");
            Require(hourly.GetArrayLength() == 24, "24 truly consecutive UTC hours");
            for (int h = 0; h < hourly.GetArrayLength(); h++)
            {
                JsonElement hour = hourly[h];
                DateTimeOffset timestamp = DateTimeOffset.Parse(
                    hour.GetProperty("time_utc").GetString());
                if (h > 0)
                {
                    DateTimeOffset prev = DateTimeOffset.Parse(
                        hourly[h - 1].GetProperty("time_utc").GetString());
                    Require((timestamp - prev).TotalHours == 1,
                        "hourly timestamps never skip UTC hours at DST");
                }
                WeatherReading simulation = OglWeatherModel.At(timestamp, seed, berlin);
                Require(hour.GetProperty("condition").GetString() ==
                    simulation.Condition, "hourly condition matches region weather");
                Require(hour.GetProperty("temperature_c").GetInt32() ==
                    simulation.TemperatureC, "hourly temperatures match region weather");
            }
            JsonElement days = data.GetProperty("forecast");
            Require(days.GetArrayLength() == 5, "portal has five forecast days");
            for (int i = 0; i < 5; i++)
            {
                WeatherDay actual = OglWeatherModel.ForecastDay(
                    TimeZoneInfo.ConvertTime(instant, berlin).Date.AddDays(i + 1),
                    seed, berlin);
                Require(days[i].GetProperty("temperature_min_c").GetInt32() ==
                    actual.MinC, "forecast low is deterministic");
                Require(days[i].GetProperty("temperature_max_c").GetInt32() ==
                    actual.MaxC, "forecast high is deterministic");
                Require(days[i].GetProperty("condition").GetString() ==
                    actual.Condition, "forecast condition is deterministic");
            }
        }
        Require(OglWeatherPortalProjection.ConditionLabelDe("snow") == "Schnee",
            "German snow condition");
        Require(OglWeatherPortalProjection.ConditionIcon("rain") == "cloud-rain",
            "weather icon mapping");

        // Operator-only overrides are bounded and expire automatically; the
        // shared deterministic model and future forecast are never changed.
        var testMode = new OglWeatherTestOverride();
        WeatherReading baseline = OglWeatherModel.At(now, seed, berlin);
        Require(!testMode.TryGet(now, baseline, out WeatherReading auto) &&
            auto.Condition == baseline.Condition &&
            auto.Intensity == baseline.Intensity, "default mode is automatic");
        Require(!testMode.Activate("sun", 80, 10, now),
            "only rain/snow can be forced");
        Require(!testMode.Activate("rain", 0, 10, now) &&
            !testMode.Activate("snow", 101, 10, now) &&
            !testMode.Activate("rain", 50, 0, now) &&
            !testMode.Activate("rain", 50, 61, now),
            "reject zero, excessive intensity or test duration");
        Require(testMode.Activate("rain", 80, 10, now),
            "operator can activate 80 percent rain");
        Require(testMode.TryGet(now, baseline, out WeatherReading rain) &&
            rain.Condition == "rain" && rain.Intensity == 80 &&
            rain.TemperatureC > 1 && rain.WindKmh == baseline.WindKmh,
            "rain test must yield viewer-compatible wet conditions");
        Require(OglWeatherTestOverride.NowWire(now, berlin, baseline) ==
            OglWeatherModel.NowWire(now, seed, berlin),
            "test NOW wire must preserve LSL protocol exactly");
        Require(OglWeatherTestOverride.NowWire(now, berlin, rain)
            .Split('|')[4] == "rain", "rain test LSL NOW matches override");
        using (JsonDocument viewerTest = JsonDocument.Parse(
            OglWeatherViewerState.Serialize("region", now, seed, berlin, 200,
                true, rain)))
        {
            JsonElement p = viewerTest.RootElement.GetProperty("precipitation");
            Require(p.GetProperty("condition").GetString() == "rain" &&
                p.GetProperty("intensity").GetInt32() == 80 &&
                p.GetProperty("test_override_active").GetBoolean(),
                "CAPS reports manually forced rain");
            Require(p.GetProperty("legacy_prim_emitters_active").GetBoolean(),
                "test weather retains actual legacy emitter flag");
        }
        Require(testMode.Activate("snow", 65, 1, now),
            "operator can switch rain to snow");
        Require(testMode.TryGet(now, baseline, out WeatherReading snow) &&
            snow.Condition == "snow" && snow.Intensity == 65 &&
            snow.TemperatureC <= 1, "snow test temperature must be valid");
        Require(!testMode.TryGet(now.AddMinutes(1), baseline,
                out WeatherReading expired) &&
            expired.Condition == baseline.Condition &&
            testMode.Status(now.AddMinutes(1)) == "auto",
            "test override automatically expires without persistence");
        Require(testMode.Activate("rain", 90, 15, now),
            "operator can reactivate after expiry");
        testMode.Clear();
        Require(!testMode.TryGet(now, baseline, out WeatherReading cleared) &&
            cleared.Condition == baseline.Condition, "auto restores climate now");
        using (JsonDocument noOverride = JsonDocument.Parse(
            OglWeatherViewerState.Serialize("region", now, seed, berlin, 200)))
        {
            Require(!noOverride.RootElement.GetProperty("precipitation")
                .GetProperty("test_override_active").GetBoolean(),
                "normal CAPS must advertise no test mode");
        }
        Require(OglWeatherModel.At(now, seed, berlin).Condition ==
            baseline.Condition,
            "temporary override never mutates the shared climate model");
        using (JsonDocument portalUnchanged = JsonDocument.Parse(
            OglWeatherPortalProjection.Serialize(now, seed, berlin,
                "Stadt-NexVerse")))
        {
            Require(portalUnchanged.RootElement.GetProperty("current")
                .GetProperty("condition").GetString() == baseline.Condition,
                "the public portal remains based on deterministic climate");
        }

        // XML feed shares the deterministic model with the JSON API, and
        // emits an UTF-8, properly escaped, useful TTS narration.
        foreach (DateTimeOffset instant in new[]
        {
            now,
            new DateTimeOffset(2027, 3, 28, 0, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 10, 31, 0, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 10, 31, 1, 30, 0, TimeSpan.Zero)
        })
        {
            const string title = "Stadt & NexVerse <Wetter>";
            string xmlBody = OglWeatherXmlProjection.Serialize(
                instant, seed, berlin, title);
            Require(xmlBody.StartsWith("<?xml version=\\"1.0\\" encoding=\\"utf-8\\"?>",
                    StringComparison.OrdinalIgnoreCase),
                "XML declaration matches UTF-8 response");
            var xmlDocument = new XmlDocument { XmlResolver = null };
            xmlDocument.LoadXml(xmlBody);
            XmlElement root = xmlDocument.DocumentElement;
            Require(root.Name == "oglweather" &&
                root.GetAttribute("version") == "1" &&
                root.GetAttribute("simulation") == "true",
                "XML version and simulated source");
            Require(xmlDocument.SelectSingleNode("/oglweather/meta/display_name")
                .InnerText == title, "XML escapes special characters");
            Require(xmlBody.Contains("&amp;") && xmlBody.Contains("&lt;"),
                "XML attribute/text escape is encoded correctly");
            XmlNode currentNode = xmlDocument.SelectSingleNode("/oglweather/current");
            WeatherReading expected = OglWeatherModel.At(instant, seed, berlin);
            Require(currentNode.Attributes["condition"].Value == expected.Condition,
                "XML current condition equals weather simulator");
            Require(int.Parse(currentNode.Attributes["temperature_c"].Value) ==
                expected.TemperatureC, "XML temperature matches weather simulator");
            Require(int.Parse(currentNode.Attributes["wind_kmh"].Value) ==
                expected.WindKmh, "XML wind matches weather simulator");
            Require(int.Parse(currentNode.Attributes["precipitation_intensity_percent"].Value) ==
                expected.Intensity, "XML intensity matches simulator");

            XmlNodeList daysXml = xmlDocument.SelectNodes("/oglweather/forecast/day");
            Require(daysXml.Count == 5, "XML has five future days");
            for (int d = 0; d < daysXml.Count; ++d)
            {
                DateTime localDay = TimeZoneInfo.ConvertTime(instant, berlin)
                    .Date.AddDays(d + 1);
                WeatherDay expectedDay = OglWeatherModel.ForecastDay(
                    localDay, seed, berlin);
                XmlNode dayXml = daysXml[d];
                Require(dayXml.Attributes["date"].Value ==
                    expectedDay.Date.ToString("yyyy-MM-dd"),
                    "XML forecast date matches tomorrow-first JSON format");
                Require(dayXml.Attributes["condition"].Value == expectedDay.Condition,
                    "XML forecast condition matches deterministic model");
                Require(int.Parse(dayXml.Attributes["temperature_min_c"].Value) ==
                    expectedDay.MinC &&
                    int.Parse(dayXml.Attributes["temperature_max_c"].Value) ==
                    expectedDay.MaxC &&
                    int.Parse(dayXml.Attributes["precipitation_hours"].Value) ==
                    expectedDay.PrecipitationHours,
                    "XML forecast daily limits match JSON and simulator");
            }

            XmlNode narration = xmlDocument.SelectSingleNode("/oglweather/narration");
            Require(narration.Attributes["language"].Value == "de-DE" &&
                narration.Attributes["format"].Value == "plain-text" &&
                narration.InnerText.Contains(title) &&
                narration.InnerText.Contains("fünf Tage") &&
                narration.InnerText.Contains("Stadt-NexVerse"),
                "XML includes ready-to-speak German narration");
            using JsonDocument portalJson = JsonDocument.Parse(
                OglWeatherPortalProjection.Serialize(
                    instant, seed, berlin, title));
            Require(portalJson.RootElement.GetProperty("current")
                .GetProperty("condition").GetString() ==
                currentNode.Attributes["condition"].Value,
                "XML and existing JSON API report same current weather");
        }
        Require(OglWeatherXmlProjection.Serialize(now, seed, berlin,
                "Stadt-NexVerse") ==
            OglWeatherXmlProjection.Serialize(now, seed, berlin,
                "Stadt-NexVerse"),
            "XML output is repeatable for a given instant");

        Console.WriteLine("OGLWeather: " + checks + " checks PASS, " +
            wetDays + " wet days, " + dryDays + " dry days, " + snowDays + " snowy days");
    }
}
