// SPDX-License-Identifier: BSD-3-Clause
using System;

namespace OpenSim.Region.CoreModules.World.Land
{
    /// <summary>
    /// Pure, deterministic land-impact arithmetic. The inputs are costs for
    /// the ENTIRE linked object, not rounded per-part costs.
    /// </summary>
    public static class OglMeshLandImpactMath
    {
        public static double ServerWeight(int partCount, int scriptCount, bool physical)
        {
            if (partCount <= 0)
                return 0;
            if (physical)
                return partCount;
            double weight = 0.5 * partCount;
            if (scriptCount > 1)
                weight += 0.25 * (scriptCount - 1);
            return Math.Min(partCount, weight);
        }

        public static int LandImpact(int partCount, bool containsMesh,
            double streaming, double physics, double server)
        {
            if (partCount <= 0)
                return 0;
            if (!containsMesh)
                return partCount;
            // Missing / malformed cost inputs cannot grant a discount.
            if (!double.IsFinite(streaming) || !double.IsFinite(physics) ||
                !double.IsFinite(server) || streaming < 0 || physics < 0 || server < 0)
                return partCount;
            double highest = Math.Max(streaming, Math.Max(physics, server));
            if (highest >= int.MaxValue)
                return int.MaxValue;
            return Math.Max(1, (int)Math.Ceiling(highest));
        }

        /// <summary>
        /// Mirrors the four-LOD weighted download model used by the built-in
        /// model uploader (MeshCost.cs), from compressed per-LOD byte lengths.
        /// </summary>
        public static double StreamingWeight(int high, int medium, int low,
            int lowest, double squaredDiameter)
        {
            if (high <= 0 || !double.IsFinite(squaredDiameter) || squaredDiameter <= 0)
                return double.NaN;
            medium = medium > 0 ? medium : high;
            low = low > 0 ? low : medium;
            lowest = lowest > 0 ? lowest : low;

            double ah = Math.Min(262144.0, squaredDiameter * 17.36);
            double am = Math.Min(262144.0, squaredDiameter * 277.78);
            double al = Math.Min(262144.0, squaredDiameter * 1111.11);
            double ae = 262144.0;

            double wh = Math.Max(1.0, ah);
            double wm = Math.Max(1.0, am - ah);
            double wl = Math.Max(1.0, al - am);
            double we = Math.Max(1.0, ae - al);
            double totalArea = wh + wm + wl + we;

            double h = Math.Max(16, high - 384);
            double m = Math.Max(16, medium - 384);
            double l = Math.Max(16, low - 384);
            double e = Math.Max(16, lowest - 384);
            return 0.004 * (h * wh + m * wm + l * wl + e * we) / totalArea;
        }
    }
}
