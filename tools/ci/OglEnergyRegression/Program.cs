// Regression tests for the deterministic OGLEnergy power grid and LSL wire.
using System;
using OpenGenesisLINK.Addons.OGLEnergy;

internal static class Program
{
    private static int m_Checks;

    private static void Require(bool valid, string message)
    {
        m_Checks++;
        if (!valid) throw new Exception("OGLEnergy regression failure: " + message);
    }

    private static void MustReject(Action action, string message)
    {
        bool failed = false;
        try { action(); }
        catch (ArgumentOutOfRangeException) { failed = true; }
        Require(failed, message);
    }

    private static void Main()
    {
        TimeZoneInfo berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var settings = new OglEnergySettings(900, 1000, 500, 850, 2200);
        MustReject(() => new OglEnergySettings(0, 0, 0, 0, 2200),
            "zero generator capacity rejected");
        MustReject(() => new OglEnergySettings(-1, 10, 10, 10, 2200),
            "negative capacity rejected");
        MustReject(() => new OglEnergySettings(100001, 10, 10, 10, 2200),
            "unbounded capacity rejected");
        MustReject(() => new OglEnergySettings(100, 100, 100, 100, 0),
            "zero demand rejected");

        var first = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        var initial = OglEnergyModel.At(first, berlin, settings);
        Require(initial.SupplyKw ==
            initial.SolarKw + initial.WindKw + initial.HydroKw + initial.BaseKw,
            "production sums correctly");
        Require(initial.ReserveKw == initial.SupplyKw - initial.DemandKw,
            "reserve equals supply less consumption");
        Require(initial.BaseKw == settings.BaseCapacityKw,
            "base capacity dispatched predictably");
        Require(initial.Condition == OglEnergyModel.At(first, berlin, settings).Condition,
            "pure model repeatability");
        string[] wire = OglEnergyModel.Wire(first, berlin, initial).Split('|');
        Require(wire.Length == 12 && wire[0] == "OGLENERGY" &&
            wire[1] == "1" && wire[3] == initial.Condition,
            "12-field LSL protocol with condition");
        Require(int.Parse(wire[4]) == initial.SupplyKw &&
            int.Parse(wire[5]) == initial.DemandKw &&
            int.Parse(wire[11]) == initial.ReserveKw,
            "LSL production, demand and reserve match model");

        int[] statuses = new int[3];
        // Full-year evaluation spans ordinary days and CET/CEST transitions.
        for (int hour = 0; hour < 365 * 24; hour++)
        {
            DateTimeOffset at = first.AddHours(hour);
            OglEnergyReading current = OglEnergyModel.At(at, berlin, settings);
            Require(current.SupplyKw >= 0 && current.SupplyKw <= 400000,
                "bounded generation");
            Require(current.DemandKw >= 1 && current.DemandKw <= 150000,
                "bounded demand");
            Require(current.SolarKw >= 0 && current.SolarKw <= settings.SolarCapacityKw,
                "bounded solar output");
            Require(current.WindKw >= 0 && current.WindKw <= settings.WindCapacityKw,
                "bounded wind output");
            Require(current.HydroKw >= 0 && current.HydroKw <= settings.HydroCapacityKw,
                "bounded hydro output");
            Require(current.BaseKw == settings.BaseCapacityKw,
                "stable base power");
            Require(current.SupplyKw == current.SolarKw + current.WindKw +
                current.HydroKw + current.BaseKw, "conservation of production");
            Require(current.ReserveKw == current.SupplyKw - current.DemandKw,
                "conservation of reserve");
            if (current.Condition == "NORMAL")
            {
                Require(current.SupplyKw >= current.DemandKw,
                    "normal only when demand is met");
                statuses[0]++;
            }
            else if (current.Condition == "WARN")
            {
                Require(current.SupplyKw < current.DemandKw &&
                    current.SupplyKw * 4 >= current.DemandKw * 3,
                    "warning status within shortage boundary");
                statuses[1]++;
            }
            else if (current.Condition == "OUTAGE")
            {
                Require(current.SupplyKw * 4 < current.DemandKw * 3,
                    "outage only on significant shortage");
                statuses[2]++;
            }
            else throw new Exception("Unknown energy condition");
            Require(OglEnergyModel.Wire(at, berlin, current).Split('|').Length == 12,
                "full-year protocol remains bounded");
        }
        Require(statuses[0] > 0 && statuses[1] > 0 && statuses[2] > 0,
            "all three states observable across a representative full year");
        var forcedOutage = OglEnergyModel.At(first, berlin,
            new OglEnergySettings(0, 0, 0, 100, 100000));
        Require(forcedOutage.Condition == "OUTAGE" &&
            forcedOutage.SupplyKw == 100, "underpowered grid reports outage");
        var stable = OglEnergyModel.At(first, berlin,
            new OglEnergySettings(0, 0, 0, 100000, 100));
        Require(stable.Condition == "NORMAL", "adequately powered grid reports normal");

        DateTimeOffset dstFirst = new DateTimeOffset(2027, 3, 28, 0, 30, 0, TimeSpan.Zero);
        DateTimeOffset dstNext = dstFirst.AddHours(1);
        string a = OglEnergyModel.Wire(dstFirst, berlin,
            OglEnergyModel.At(dstFirst, berlin, settings));
        string b = OglEnergyModel.Wire(dstNext, berlin,
            OglEnergyModel.At(dstNext, berlin, settings));
        Require(a.Split('|')[2].Contains("01:30") &&
                b.Split('|')[2].Contains("03:30"),
            "local clock respects forward DST leap");

        Console.WriteLine("OGLEnergy: " + m_Checks + " checks PASS; " +
            "NORMAL=" + statuses[0] + " WARN=" + statuses[1] +
            " OUTAGE=" + statuses[2]);
    }
}
