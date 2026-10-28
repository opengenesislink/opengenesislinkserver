using System;
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

        Console.WriteLine("OGLWeather: " + checks + " checks PASS, " +
            wetDays + " wet days, " + dryDays + " dry days, " + snowDays + " snowy days");
    }
}
