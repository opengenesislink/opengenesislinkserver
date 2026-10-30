// SPDX-License-Identifier: MPL-2.0
using System;

namespace OpenSim.Framework
{
    /// <summary>
    /// Generation 1 admission policy for self-reported viewer names.
    /// This is a compatibility allowlist, NOT verified client attestation:
    /// a modified viewer can forge both the channel and version strings.
    /// </summary>
    public static class OglApprovedViewerPolicy
    {
        private static readonly string[] s_AllowedNames =
        {
            "Firestorm", "OpenGenesisLINK", "OGL Viewer", "Radegast-ng", "Radegast"
        };

        private static readonly string[] s_KnownCopybotNames =
        {
            "Darkstorm", "Hydrastorm", "Copybot"
        };

        public static bool IsAllowed(string viewer, string channel)
        {
            if (ContainsKnownCopybotName(viewer) || ContainsKnownCopybotName(channel))
                return false;

            if (StartsWithApprovedName(viewer))
                return string.IsNullOrWhiteSpace(channel) ||
                       StartsWithApprovedName(channel) ||
                       IsNumericVersion(channel);

            return StartsWithApprovedName(channel) && IsNumericVersion(viewer);
        }

        private static bool ContainsKnownCopybotName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (string name in s_KnownCopybotNames)
            {
                if (value.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool StartsWithApprovedName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim();
            foreach (string name in s_AllowedNames)
            {
                if (!normalized.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (normalized.Length == name.Length)
                    return true;

                char next = normalized[name.Length];
                if (char.IsWhiteSpace(next) || next == '-' || next == '_')
                    return true;
            }
            return false;
        }

        private static bool IsNumericVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            string normalized = value.Trim();
            // Official OGL viewer builds may report v0.4.0.0 instead of
            // 0.4.0.0. Allow a single leading v/V only when followed by
            // digits; all other prefixes and viewer families stay blocked.
            if (normalized.Length > 1 &&
                (normalized[0] == 'v' || normalized[0] == 'V') &&
                char.IsDigit(normalized[1]))
                normalized = normalized.Substring(1);
            if (!char.IsDigit(normalized[0]))
                return false;

            foreach (char c in normalized)
            {
                if (!char.IsDigit(c) && c != '.' && c != '-' &&
                    c != '_' && c != '(' && c != ')' && !char.IsWhiteSpace(c))
                    return false;
            }
            return true;
        }
    }
}
