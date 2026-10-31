// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenGenesisLINK.Addons.OGLWeather
{
    /// <summary>
    /// Transient, per-viewer overcast EEP for standard Firestorm. No changes
    /// to estate/parcel assets or persistent region-environment settings.
    /// Existing personal environment overrides are never overwritten.
    /// </summary>
    public static class OglWeatherViewerEnvironment
    {
        public static ViewerEnvironment Overcast(ViewerEnvironment original,
            string condition, int intensity)
        {
            if (original == null || (condition != "rain" &&
                condition != "snow" && condition != "cloud"))
                return null;
            OSD document = original.ToOSD();
            if (document is not OSDMap root ||
                !root.TryGetValue("day_cycle", out OSD day) || day is not OSDMap dayMap ||
                !dayMap.TryGetValue("frames", out OSD framesOSD) ||
                framesOSD is not OSDMap frames)
                return null;

            float strength = Math.Clamp(intensity / 100f, 0.1f, 1f);
            bool precipitation = condition == "rain" || condition == "snow";
            float cover = precipitation ? 0.82f + strength * 0.16f : 0.63f;
            float sun = precipitation ? 0.38f - 0.18f * strength : 0.68f;
            int changed = 0;
            foreach (KeyValuePair<string, OSD> frame in frames)
            {
                if (frame.Value is not OSDMap sky ||
                    !sky.TryGetValue("type", out OSD type) ||
                    type.AsString() != "sky")
                    continue;
                // Keep the day cycle, water, horizon, sun direction and
                // viewer's current time. Only change cloud and light colours.
                sky["cloud_shadow"] = cover;
                sky["cloud_color"] = precipitation
                    ? new Vector3(0.38f, 0.40f, 0.44f)
                    : new Vector3(0.56f, 0.57f, 0.60f);
                sky["sunlight_color"] = new Vector4(sun, sun, sun * 1.06f, 0.3f);
                sky["glow"] = new Vector3(0f, 0f, -0.7f);
                sky["moisture_level"] = precipitation ? 0.85f : 0.5f;
                if (sky.TryGetValue("legacy_haze", out OSD hazeOSD) &&
                    hazeOSD is OSDMap haze)
                    haze["ambient"] = precipitation
                        ? new Vector3(0.57f, 0.59f, 0.64f)
                        : new Vector3(0.79f, 0.80f, 0.83f);
                changed++;
            }
            if (changed == 0)
                return null;
            var result = new ViewerEnvironment();
            result.FromOSD(root);
            return result;
        }

        public static void Apply(Scene scene,
            Dictionary<UUID, ViewerEnvironment> owned,
            WeatherReading weather)
        {
            if (scene == null || owned == null || weather == null)
                return;
            IEnvironmentModule environment =
                scene.RequestModuleInterface<IEnvironmentModule>();
            if (environment == null)
                return;
            var seen = new HashSet<UUID>();
            foreach (ScenePresence avatar in scene.GetScenePresences())
            {
                if (avatar == null || avatar.IsChildAgent || avatar.IsNPC ||
                    avatar.IsDeleted || avatar.IsInTransit)
                    continue;
                seen.Add(avatar.UUID);
                if (owned.TryGetValue(avatar.UUID, out ViewerEnvironment old))
                {
                    if (ReferenceEquals(old, avatar.Environment))
                        continue;
                    // Another system or a user changed their local override.
                    // We no longer own this environment; don't revert it.
                    owned.Remove(avatar.UUID);
                }
                if (avatar.Environment != null)
                    continue;
                ViewerEnvironment newWeather = Overcast(
                    environment.GetRegionEnvironment(), weather.Condition,
                    weather.Intensity);
                if (newWeather == null)
                    continue;
                avatar.Environment = newWeather;
                owned[avatar.UUID] = newWeather;
                environment.WindlightRefreshForced(avatar, 0);
            }
            foreach (UUID id in new List<UUID>(owned.Keys))
                if (!seen.Contains(id))
                    owned.Remove(id);
        }

        public static void Clear(Scene scene,
            Dictionary<UUID, ViewerEnvironment> owned)
        {
            if (owned == null || owned.Count == 0)
                return;
            IEnvironmentModule environment =
                scene?.RequestModuleInterface<IEnvironmentModule>();
            if (scene != null)
                foreach (ScenePresence avatar in scene.GetScenePresences())
                {
                    if (avatar == null ||
                        !owned.TryGetValue(avatar.UUID, out ViewerEnvironment ours))
                        continue;
                    if (ReferenceEquals(avatar.Environment, ours))
                    {
                        avatar.Environment = null;
                        environment?.WindlightRefreshForced(avatar, 0);
                    }
                }
            owned.Clear();
        }
    }
}
