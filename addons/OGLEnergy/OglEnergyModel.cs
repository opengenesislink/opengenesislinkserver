// SPDX-License-Identifier: MPL-2.0
// Deterministic, read-only simulated power grid for Stadt-NexVerse.
// Values represent simulated kW, NOT real measured energy or billing.
using System;
using System.Globalization;

namespace OpenGenesisLINK.Addons.OGLEnergy
{
    public sealed class OglEnergySettings
    {
        public int SolarCapacityKw { get; }
        public int WindCapacityKw { get; }
        public int HydroCapacityKw { get; }
        public int BaseCapacityKw { get; }
        public int BaseDemandKw { get; }

        public OglEnergySettings(int solar, int wind, int hydro, int baseline, int demand)
        {
            if (solar < 0 || solar > 100000 || wind < 0 || wind > 100000 ||
                hydro < 0 || hydro > 100000 || baseline < 0 || baseline > 100000 ||
                demand < 100 || demand > 100000 ||
                (solar + wind + hydro + baseline) == 0)
                throw new ArgumentOutOfRangeException(nameof(demand),
                    "Power capacities must be 0..100000 kW with a positive total; demand 100..100000 kW");
            SolarCapacityKw = solar;
            WindCapacityKw = wind;
            HydroCapacityKw = hydro;
            BaseCapacityKw = baseline;
            BaseDemandKw = demand;
        }
    }

    public sealed class OglEnergyReading
    {
        public string Condition { get; init; }
        public int SupplyKw { get; init; }
        public int DemandKw { get; init; }
        public int SolarKw { get; init; }
        public int WindKw { get; init; }
        public int HydroKw { get; init; }
        public int BaseKw { get; init; }
        public int UtilizationPct { get; init; }
        public int ReserveKw => SupplyKw - DemandKw;
    }

    public static class OglEnergyModel
    {
        public static OglEnergyReading At(DateTimeOffset utc, TimeZoneInfo zone,
            OglEnergySettings config)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            if (config == null) throw new ArgumentNullException(nameof(config));

            DateTime local = TimeZoneInfo.ConvertTime(utc, zone).DateTime;
            double hour = local.TimeOfDay.TotalHours;
            // Deterministic simplified generation curve: solar 06:00-20:00,
            // wind and hydro fluctuate. No connection to physical metering
            // or to the OGLWeather weather simulator is claimed.
            double daylight = Math.Max(0.0,
                Math.Sin(Math.PI * (hour - 6.0) / 14.0));
            if (hour < 6 || hour >= 20) daylight = 0;
            double wind = Math.Clamp(0.57 +
                0.23 * Math.Sin(2.0 * Math.PI * (local.DayOfYear / 11.0 + hour / 24.0)) +
                0.08 * Math.Cos(2.0 * Math.PI * (hour / 9.0)), 0.15, 0.93);
            double hydro = 0.80 + 0.08 *
                Math.Sin(2.0 * Math.PI * local.DayOfYear / 365.2425);
            int solarKw = (int)Math.Round(config.SolarCapacityKw * daylight);
            int windKw = (int)Math.Round(config.WindCapacityKw * wind);
            int hydroKw = (int)Math.Round(config.HydroCapacityKw * hydro);
            int baseKw = config.BaseCapacityKw;
            int supply = solarKw + windKw + hydroKw + baseKw;

            // Morning/evening activity and deterministic daily cycle.
            double morning = Math.Exp(-Math.Pow((hour - 8) / 2.8, 2));
            double evening = Math.Exp(-Math.Pow((hour - 19) / 3.5, 2));
            double demandFactor = 0.76 + 0.23 * morning + 0.48 * evening;
            int demand = Math.Max(1, (int)Math.Round(config.BaseDemandKw * demandFactor));
            int utilization = supply == 0 ? 999 :
                Math.Clamp((int)Math.Round(demand * 100.0 / supply), 0, 999);
            string status = supply >= demand ? "NORMAL" :
                supply * 4 >= demand * 3 ? "WARN" : "OUTAGE";

            return new OglEnergyReading
            {
                Condition = status,
                SolarKw = solarKw,
                WindKw = windKw,
                HydroKw = hydroKw,
                BaseKw = baseKw,
                SupplyKw = supply,
                DemandKw = demand,
                UtilizationPct = utilization
            };
        }

        // Exact 12-field, versioned, bounded wire format for server-authored
        // inworld region chat. No identifiers, prices or control commands.
        public static string Wire(DateTimeOffset utc, TimeZoneInfo zone,
            OglEnergyReading data)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            if (data == null) throw new ArgumentNullException(nameof(data));
            DateTime local = TimeZoneInfo.ConvertTime(utc, zone).DateTime;
            return "OGLENERGY|1|" +
                local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "|" +
                data.Condition + "|" +
                data.SupplyKw.ToString(CultureInfo.InvariantCulture) + "|" +
                data.DemandKw.ToString(CultureInfo.InvariantCulture) + "|" +
                data.SolarKw.ToString(CultureInfo.InvariantCulture) + "|" +
                data.WindKw.ToString(CultureInfo.InvariantCulture) + "|" +
                data.HydroKw.ToString(CultureInfo.InvariantCulture) + "|" +
                data.BaseKw.ToString(CultureInfo.InvariantCulture) + "|" +
                data.UtilizationPct.ToString(CultureInfo.InvariantCulture) + "|" +
                data.ReserveKw.ToString(CultureInfo.InvariantCulture);
        }
    }
}
