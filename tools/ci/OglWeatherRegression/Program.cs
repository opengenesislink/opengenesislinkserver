using System;
using System.Text.Json;
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
        using (JsonDocument legacy = JsonDocument.Parse(
            OglWeatherViewerState.Serialize(
                "region", now, seed, berlin, 200, true)))
        {
            Require(legacy.RootElement.GetProperty("precipitation")
                .GetProperty("legacy_prim_emitters_active").GetBoolean(),
                "native viewers can suppress double precipitation");
        }
        Console.WriteLine("OGLWeather: " + checks + " checks PASS, " +
            wetDays + " wet days, " + dryDays + " dry days, " + snowDays + " snowy days");
    }
}
