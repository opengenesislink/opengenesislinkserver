// SPDX-License-Identifier: MPL-2.0
using System;

namespace NexVerse.Server.Api
{
    /// <summary>
    /// Value-only projection for local UUID and Hypergrid UUI friend keys.
    /// HG friend records may contain a federation secret after the third
    /// semicolon; NEVER return it through an HTTP endpoint.
    /// </summary>
    internal static class NexRelationshipPresenceModel
    {
        internal sealed class Identity
        {
            public Guid Id;
            public bool IsHypergrid;
            public string HomeGrid;
            public string ForeignName;
        }

        internal sealed class Activity
        {
            public string LastOnlineAtUtc;
            public long? LastOnlineMinutes;
            public long? LastOnlineHours;
            public string LastOnlineLabel;
        }

        internal static bool TryParseIdentity(string friendKey, out Identity identity)
        {
            identity = null;
            if (string.IsNullOrWhiteSpace(friendKey))
                return false;
            string value = friendKey.Trim();
            if (value.Length == 36 &&
                Guid.TryParseExact(value, "D", out Guid localId) &&
                localId != Guid.Empty)
            {
                identity = new Identity
                {
                    Id = localId,
                    IsHypergrid = false,
                    HomeGrid = null,
                    ForeignName = string.Empty
                };
                return true;
            }

            // Standard OpenSim UUI: UUID;homeGridURI;First Last[;secret].
            // Split at most four fields and discard the fourth immediately.
            // Require the UUID segment to be exactly 36 chars.
            int separators = 0;
            foreach (char ch in value)
                if (ch == ';') separators++;
            if (separators < 2 || separators > 3)
                return false;
            string[] parts = value.Split(';', 4);
            if ((parts.Length != 3 && parts.Length != 4) ||
                !Guid.TryParseExact(parts[0], "D", out Guid foreignId) ||
                foreignId == Guid.Empty ||
                parts[1].Length == 0 || parts[1].Length > 512 ||
                !Uri.TryCreate(parts[1], UriKind.Absolute, out Uri home) ||
                (home.Scheme != Uri.UriSchemeHttp &&
                 home.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(home.UserInfo) ||
                string.IsNullOrWhiteSpace(parts[2]) ||
                parts[2].Length > 128)
                return false;

            identity = new Identity
            {
                Id = foreignId,
                IsHypergrid = true,
                // Keep the normalized grid URI, no URL query, fragments,
                // credentials or UUI federation secret.
                HomeGrid = home.GetLeftPart(UriPartial.Path).TrimEnd('/'),
                ForeignName = parts[2].Trim()
            };
            return true;
        }

        /// <summary>
        /// "Last online" is based ONLY on a real logout stored in the
        /// local GridUser service. Online and remote-grid activity must not
        /// be inferred from older logouts or last-login values.
        /// </summary>
        internal static Activity LastLogout(
            DateTime logout, DateTimeOffset now, bool authorized, bool online)
        {
            var result = new Activity();
            if (!authorized || online || logout == default)
                return result;

            DateTime utc = logout.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(logout, DateTimeKind.Utc)
                : logout.ToUniversalTime();
            var stamp = new DateTimeOffset(utc);
            if (stamp <= DateTimeOffset.FromUnixTimeSeconds(0) ||
                stamp > now)
                return result;

            long minutes = (long)Math.Max(
                0, Math.Floor((now - stamp).TotalMinutes));
            result.LastOnlineAtUtc = stamp.ToString("yyyy-MM-ddTHH:mm:ssZ",
                System.Globalization.CultureInfo.InvariantCulture);
            result.LastOnlineMinutes = minutes;
            result.LastOnlineHours = minutes / 60;
            if (minutes < 1)
                result.LastOnlineLabel = "gerade eben";
            else if (minutes < 60)
                result.LastOnlineLabel = "vor " + minutes +
                    (minutes == 1 ? " Minute" : " Minuten");
            else if (minutes < 1440)
            {
                long hours = minutes / 60;
                result.LastOnlineLabel = "vor " + hours +
                    (hours == 1 ? " Stunde" : " Stunden");
            }
            else
            {
                long days = minutes / 1440;
                result.LastOnlineLabel = "vor " + days +
                    (days == 1 ? " Tag" : " Tagen");
            }
            return result;
        }
    }
}
