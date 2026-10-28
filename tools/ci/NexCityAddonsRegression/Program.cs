using System;
using System.Collections.Generic;
using OpenGenesisLINK.Addons.NexTraffic;
using OpenGenesisLINK.Addons.NexTransit;
using OpenGenesisLINK.Addons.NexSmartLight;

internal static class Program
{
    static int count;
    static void Check(bool ok, string label)
    {
        count++;
        if (!ok) throw new Exception("NexCity: " + label);
    }
    static void MustReject(Action call, string label)
    {
        bool rejected = false;
        try { call(); } catch (FormatException) { rejected = true; }
        Check(rejected, label);
    }

    public static void Main()
    {
        // Two conflicting greens or green-vs-yellow never occur.
        foreach (int offset in new[] { -1000, 0, 11, 327, 86400 })
        {
            for (long time = 0; time < 86400; time++)
            {
                TrafficPhase p = NexTrafficModel.At(time, offset, 30, 5, 3);
                Check(!(p.AxisA != "RED" && p.AxisB != "RED"),
                    "no conflicting phases");
                Check(p.RemainingSeconds > 0 && p.RemainingSeconds <= 30,
                    "bounded phase duration");
                if (p.AxisA == "YELLOW") Check(p.AxisB == "RED", "safe amber A");
                if (p.AxisB == "YELLOW") Check(p.AxisA == "RED", "safe amber B");
            }
        }
        var junctions = NexTrafficModel.Parse("Hauptstrasse:0;Marktplatz:11");
        Check(junctions.Count == 2, "traffic junction configuration");
        Check(NexTrafficModel.Wire(junctions[0],
            NexTrafficModel.At(123456, 0, 30, 5, 3), 123456)
            .StartsWith("NEXTRAFFIC|1|Hauptstrasse|"), "traffic LSL wire");
        MustReject(() => NexTrafficModel.Parse("Hauptstrasse:0;Hauptstrasse:7"),
            "reject duplicate traffic junctions");
        MustReject(() => NexTrafficModel.Parse("Road|Drop:0"),
            "reject wire injection in junction name");

        var routes = NexTransitModel.ParseRoutes(
            "101,Magdeburg,10,0,*;150,Jenseits,20,15,Gummybear Island");
        var t = new DateTime(2026, 10, 28, 18, 7, 0);
        var main = NexTransitModel.Upcoming(t, routes, "Freiburg Sued", 5);
        Check(main.Count == 1 && main[0].MinutesUntil == 3, "regional route filter");
        var gummy = NexTransitModel.Upcoming(t, routes, "Gummybear Island", 5);
        Check(gummy.Count == 2 && gummy[0].MinutesUntil == 3,
            "two routes at shared stop");
        Check(NexTransitModel.Wire(t, gummy).StartsWith("NEXTRANSIT|1|"),
            "transit LSL wire");
        var nearMidnight = NexTransitModel.Upcoming(
            new DateTime(2026, 10, 28, 23, 59, 0), routes,
            "Freiburg Sued", 5);
        Check(nearMidnight[0].MinutesUntil == 1 &&
            nearMidnight[0].DepartureMinute == 0,
            "daily timetable rolls to next calendar day");
        MustReject(() => NexTransitModel.ParseRoutes(
            "101,Magdeburg,0,0,*"), "reject zero headway");
        MustReject(() => NexTransitModel.ParseRoutes(
            "101,Magdeburg|bad,10,0,*"), "reject injection in target");

        int start = NexSmartLightModel.ParseTime("18:00");
        int end = NexSmartLightModel.ParseTime("06:00");
        Check(NexSmartLightModel.IsOn(new DateTime(2026, 10, 28, 19, 0, 0),
            start, end), "evening lights on");
        Check(NexSmartLightModel.IsOn(new DateTime(2026, 10, 29, 0, 0, 0),
            start, end), "midnight lights on");
        Check(!NexSmartLightModel.IsOn(new DateTime(2026, 10, 29, 6, 0, 0),
            start, end), "morning lights off");
        Check(!NexSmartLightModel.IsOn(new DateTime(2026, 10, 29, 12, 0, 0),
            start, end), "daytime lights off");
        Check(!NexSmartLightModel.IsOn(t, start, start),
            "equal on/off means disabled");
        Check(NexSmartLightModel.Wire(t, true).StartsWith("NEXLIGHT|1|ON|"),
            "light LSL wire");
        MustReject(() => NexSmartLightModel.ParseTime("25:30"),
            "reject invalid night hour");
        // Clock civil time handles German summertime transitions when
        // an offset-aware TimeZoneInfo converts to local before IsOn().
        TimeZoneInfo berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        DateTime first = TimeZoneInfo.ConvertTime(
            new DateTimeOffset(2027, 3, 28, 1, 30, 0, TimeSpan.Zero),
            berlin).DateTime;
        Check(first.Hour == 3 && NexSmartLightModel.IsOn(first, start, end),
            "DST forward keeps pre-dawn lamps on");
        Console.WriteLine("NexTraffic/NexTransit/NexSmartLight: PASS (" +
            count + " checks)");
    }
}
