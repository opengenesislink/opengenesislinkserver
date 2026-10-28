// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenGenesisLINK.Addons.NexTransit
{
    public sealed class TransitRoute
    {
        public string Line { get; set; }
        public string Destination { get; set; }
        public int HeadwayMinutes { get; set; }
        public int FirstMinute { get; set; }
        public string StopRegion { get; set; }
    }

    public sealed class Departure
    {
        public string Line { get; set; }
        public string Destination { get; set; }
        public int MinutesUntil { get; set; }
        public int DepartureMinute { get; set; }
    }

    public static class NexTransitModel
    {
        // All routes are configured; no imaginary transport vehicle, money
        // transfer, regional teleport, or external data is implied.
        public static List<TransitRoute> ParseRoutes(string config)
        {
            var routes = new List<TransitRoute>();
            if (string.IsNullOrWhiteSpace(config)) return routes;
            foreach (string entry in config.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = entry.Split(',');
                if (f.Length != 5)
                    throw new FormatException("Routes: line,destination,headway,startMinute,stopRegion");
                string line = f[0].Trim();
                string dest = f[1].Trim();
                string region = f[4].Trim();
                if (!ValidLabel(line, 16) || !ValidLabel(dest, 48) ||
                    !(region == "*" || ValidLabel(region, 64)))
                    throw new FormatException("Invalid NexTransit name");
                if (!int.TryParse(f[2].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int headway) ||
                    headway < 1 || headway > 720)
                    throw new FormatException("Headway must be 1..720 minutes");
                if (!int.TryParse(f[3].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int first) ||
                    first < 0 || first >= 1440)
                    throw new FormatException("Start minute must be 0..1439");
                routes.Add(new TransitRoute { Line = line, Destination = dest,
                    HeadwayMinutes = headway, FirstMinute = first, StopRegion = region });
                if (routes.Count > 100)
                    throw new FormatException("At most 100 configured NexTransit routes");
            }
            return routes;
        }

        private static bool ValidLabel(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > max) return false;
            foreach (char c in value)
                if (!(char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ||
                      c == '-' || c == '_' || c == '.' || c == '/')) return false;
            return true;
        }

        public static List<Departure> Upcoming(DateTime local,
            IEnumerable<TransitRoute> routes, string regionName, int limit)
        {
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            limit = Math.Clamp(limit, 1, 10);
            int minute = local.Hour * 60 + local.Minute;
            var result = new List<Departure>();
            foreach (TransitRoute r in routes)
            {
                if (r.StopRegion != "*" && !string.Equals(
                    regionName, r.StopRegion, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Anchored to the local calendar day: after the final service
                // of this day, the first departure is tomorrow.
                int target = r.FirstMinute;
                if (minute > target)
                {
                    int n = (minute - target + r.HeadwayMinutes - 1) /
                        r.HeadwayMinutes;
                    target += n * r.HeadwayMinutes;
                }
                if (target >= 1440)
                    target = r.FirstMinute + 1440;
                int wait = target - minute;
                result.Add(new Departure
                {
                    Line = r.Line, Destination = r.Destination,
                    DepartureMinute = target % 1440, MinutesUntil = wait
                });
            }
            return result.OrderBy(x => x.MinutesUntil)
                .ThenBy(x => x.Line, StringComparer.Ordinal).Take(limit).ToList();
        }

        public static string Wire(DateTime local, IEnumerable<Departure> departures)
        {
            var fragments = new List<string>();
            foreach (Departure d in departures)
            {
                string clock = (d.DepartureMinute / 60).ToString("00",
                    CultureInfo.InvariantCulture) + ":" +
                    (d.DepartureMinute % 60).ToString("00", CultureInfo.InvariantCulture);
                fragments.Add(d.Line + "," + d.Destination + "," + clock + "," +
                    d.MinutesUntil.ToString(CultureInfo.InvariantCulture));
            }
            string wire = "NEXTRANSIT|1|" +
                local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                "|" + string.Join(";", fragments);
            if (wire.Length > 960)
                throw new FormatException("Transit wire payload too large for LSL");
            return wire;
        }
    }
}
