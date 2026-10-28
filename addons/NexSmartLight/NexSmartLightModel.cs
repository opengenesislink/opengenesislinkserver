// SPDX-License-Identifier: MPL-2.0
using System;
using System.Globalization;

namespace OpenGenesisLINK.Addons.NexSmartLight
{
    public static class NexSmartLightModel
    {
        public static int ParseTime(string value)
        {
            if (!TimeSpan.TryParseExact(value, @"hh\:mm",
                    CultureInfo.InvariantCulture, out TimeSpan t) ||
                t.TotalMinutes < 0 || t.TotalMinutes >= 1440)
                throw new FormatException("Expected HH:mm between 00:00 and 23:59");
            return (int)t.TotalMinutes;
        }

        public static bool IsOn(DateTime local, int fromMinute, int untilMinute)
        {
            if (fromMinute < 0 || fromMinute >= 1440 ||
                untilMinute < 0 || untilMinute >= 1440)
                throw new ArgumentOutOfRangeException(nameof(fromMinute));
            int now = local.Hour * 60 + local.Minute;
            if (fromMinute == untilMinute) return false; // safe default OFF
            if (fromMinute < untilMinute)
                return now >= fromMinute && now < untilMinute;
            return now >= fromMinute || now < untilMinute; // midnight crossover
        }

        public static string Wire(DateTime local, bool on)
        {
            return "NEXLIGHT|1|" + (on ? "ON" : "OFF") + "|" +
                local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
