// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;

namespace OpenGenesisLINK.Addons.OGLWeather
{
    /// <summary>
    /// Bounded deterministic emitter placement for an arbitrary simulator region.
    /// All coordinates are internal to the region. No parcel or prim writes.
    /// </summary>
    public static class OglWeatherEmitterPlan
    {
        /// <summary>
        /// A client at the centre of a 256m region is typically 40m or more
        /// from the old fixed grid's nearest emitter. Standard Firestorm has
        /// no native weather renderer, so put temporary LL emitters above
        /// actual root avatars instead of relying on distant static sources.
        /// The list is bounded; only XY inside this region is accepted.
        /// </summary>
        public static List<(float X, float Y)> NearAvatars(
            int width, int height, IReadOnlyList<(float X, float Y)> avatars,
            int maximum)
        {
            maximum = Math.Clamp(maximum, 1, 64);
            var result = new List<(float X, float Y)>();
            if (avatars == null || width <= 0 || height <= 0)
                return result;

            foreach ((float x, float y) in avatars)
            {
                if (result.Count >= maximum) break;
                if (float.IsNaN(x) || float.IsNaN(y) ||
                    x < 0 || y < 0 || x >= width || y >= height)
                    continue;
                // Avoid multiple almost identical sources for grouped users.
                bool nearby = false;
                foreach ((float existingX, float existingY) in result)
                {
                    float dx = existingX - x;
                    float dy = existingY - y;
                    if (dx * dx + dy * dy < 64f)
                    {
                        nearby = true;
                        break;
                    }
                }
                if (!nearby) result.Add((x, y));
            }
            return result;
        }

        public static List<(float X, float Y)> Positions(
            int width, int height, int spacing, int maximum)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            spacing = Math.Clamp(spacing, 32, 256);
            maximum = Math.Clamp(maximum, 1, 64);

            int columns = Math.Max(1, (int)Math.Ceiling(width / (double)spacing));
            int rows = Math.Max(1, (int)Math.Ceiling(height / (double)spacing));

            // On varregions, distribute a bounded number ACROSS the region.
            // Never fill only the first corner when max emitter count is hit.
            while (columns * (long)rows > maximum)
            {
                if (columns >= rows && columns > 1)
                    columns--;
                else if (rows > 1)
                    rows--;
                else break;
            }

            List<(float X, float Y)> result = new List<(float X, float Y)>();
            for (int x = 0; x < columns; x++)
                for (int y = 0; y < rows; y++)
                    result.Add((
                        (float)((x + 0.5) * width / columns),
                        (float)((y + 0.5) * height / rows)));
            return result;
        }
    }
}
