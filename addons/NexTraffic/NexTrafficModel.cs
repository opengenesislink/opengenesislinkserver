// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGenesisLINK.Addons.NexTraffic
{
    public sealed class TrafficJunction
    {
        public string Name { get; set; }
        public int OffsetSeconds { get; set; }
    }

    public sealed class TrafficPhase
    {
        public string AxisA { get; set; }
        public string AxisB { get; set; }
        public int RemainingSeconds { get; set; }
    }

    public static class NexTrafficModel
    {
        // The two directions never have green or yellow simultaneously.
        // All-red interlock is intentionally included on both transitions.
        public static TrafficPhase At(long unixSeconds, int offsetSeconds,
            int greenSeconds, int yellowSeconds, int allRedSeconds)
        {
            greenSeconds = Math.Clamp(greenSeconds, 10, 120);
            yellowSeconds = Math.Clamp(yellowSeconds, 2, 10);
            allRedSeconds = Math.Clamp(allRedSeconds, 1, 10);
            int half = greenSeconds + yellowSeconds + allRedSeconds;
            int cycle = half * 2;
            int pos = (int)(((unixSeconds + offsetSeconds) % cycle + cycle) % cycle);

            if (pos < greenSeconds)
                return new TrafficPhase { AxisA = "GREEN", AxisB = "RED",
                    RemainingSeconds = greenSeconds - pos };
            pos -= greenSeconds;
            if (pos < yellowSeconds)
                return new TrafficPhase { AxisA = "YELLOW", AxisB = "RED",
                    RemainingSeconds = yellowSeconds - pos };
            pos -= yellowSeconds;
            if (pos < allRedSeconds)
                return new TrafficPhase { AxisA = "RED", AxisB = "RED",
                    RemainingSeconds = allRedSeconds - pos };
            pos -= allRedSeconds;
            if (pos < greenSeconds)
                return new TrafficPhase { AxisA = "RED", AxisB = "GREEN",
                    RemainingSeconds = greenSeconds - pos };
            pos -= greenSeconds;
            if (pos < yellowSeconds)
                return new TrafficPhase { AxisA = "RED", AxisB = "YELLOW",
                    RemainingSeconds = yellowSeconds - pos };
            pos -= yellowSeconds;
            return new TrafficPhase { AxisA = "RED", AxisB = "RED",
                RemainingSeconds = allRedSeconds - pos };
        }

        public static List<TrafficJunction> Parse(string input)
        {
            var result = new List<TrafficJunction>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(input)) return result;
            foreach (string entry in input.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = entry.Split(':');
                if (pair.Length != 2)
                    throw new FormatException("Junction format: Name:OffsetSeconds");
                string name = pair[0].Trim();
                if (!ValidName(name) || !names.Add(name))
                    throw new FormatException("Invalid/duplicate junction: " + name);
                if (!int.TryParse(pair[1], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int offset) ||
                    offset < -86400 || offset > 86400)
                    throw new FormatException("Junction offset out of range: " + name);
                result.Add(new TrafficJunction { Name = name, OffsetSeconds = offset });
                if (result.Count > 32)
                    throw new FormatException("At most 32 junctions per simulator");
            }
            return result;
        }

        public static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 32)
                return false;
            foreach (char c in name)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' '))
                    return false;
            return true;
        }

        public static string Wire(TrafficJunction junction, TrafficPhase phase,
            long unixSeconds)
        {
            return "NEXTRAFFIC|1|" + junction.Name + "|" + phase.AxisA + "|" +
                phase.AxisB + "|" +
                phase.RemainingSeconds.ToString(CultureInfo.InvariantCulture) + "|" +
                unixSeconds.ToString(CultureInfo.InvariantCulture);
        }
    }
}
