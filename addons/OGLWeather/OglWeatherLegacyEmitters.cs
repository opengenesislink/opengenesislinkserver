// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenGenesisLINK.Addons.OGLWeather
{
    /// <summary>
    /// Server-owned, non-persistent, script-free LL particle emitters.
    /// Necessary for legacy Firestorm; the custom OGL viewer can use
    /// OGLWeatherState instead. No user must rez a prim.
    /// </summary>
    public static class OglWeatherLegacyEmitters
    {
        public static void Apply(Scene scene, List<SceneObjectGroup> owned,
            WeatherReading weather, int heightMeters, int spacingMeters,
            int maxEmitters)
        {
            if (scene == null || owned == null || weather == null)
                return;

            bool wet = weather.Condition == "rain" || weather.Condition == "snow";
            // Don't fill empty simulators with invisible objects.
            if (!wet || scene.GetNumberOfClients() == 0)
            {
                Clear(scene, owned);
                return;
            }

            List<(float X, float Y)> points = OglWeatherEmitterPlan.Positions(
                (int)scene.RegionInfo.RegionSizeX, (int)scene.RegionInfo.RegionSizeY,
                spacingMeters, maxEmitters);

            // Reuse the same non-persistent objects throughout the weather
            // event. If someone returned one, only recreate our tracked ones.
            if (owned.Count != points.Count ||
                owned.Exists(group => group == null || group.IsDeleted))
                Clear(scene, owned);

            UUID owner = scene.RegionInfo.EstateSettings?.EstateOwner ?? UUID.Zero;
            if (owner.IsZero())
            {
                // Never manufacture scene objects with an invalid owner.
                Clear(scene, owned);
                return;
            }

            for (int index = 0; index < points.Count; index++)
            {
                if (index == owned.Count)
                {
                    (float x, float y) = points[index];
                    float z = Math.Max(22f,
                        scene.GetGroundHeight(x, y) + heightMeters);
                    var position = new Vector3(x, y, z);

                    // A nonphysical, phantom, transparent box that is never
                    // backed up to the region database. No LSL task inventory.
                    PrimitiveBaseShape shape = PrimitiveBaseShape.CreateBox();
                    shape.SetScale(0.5f);
                    var texture = new Primitive.TextureEntry(Util.BLANK_TEXTURE_UUID);
                    texture.DefaultTexture.RGBA = new Color4(1f, 1f, 1f, 0f);
                    shape.Textures = texture;
                    SceneObjectGroup group = new SceneObjectGroup(
                        owner, position, Quaternion.Identity, shape);
                    group.RootPart.Name = "OGLWeather managed precipitation";
                    group.RootPart.Description = "Temporary simulator-managed weather - do not edit";
                    group.RootPart.AddFlag(PrimFlags.Phantom);
                    // AttachToBackup=false: no persistent objects in SQL/OAR.
                    if (!scene.AddNewSceneObject(group, false))
                    {
                        group.Dispose();
                        continue;
                    }
                    owned.Add(group);
                }

                SceneObjectGroup emitter = owned[index];
                if (emitter != null && !emitter.IsDeleted)
                {
                    emitter.RootPart.AddNewParticleSystem(
                        CreateParticles(weather, spacingMeters), false);
                    emitter.RootPart.ScheduleFullUpdate();
                }
            }
        }

        public static void Clear(Scene scene, List<SceneObjectGroup> owned)
        {
            if (owned == null) return;
            foreach (SceneObjectGroup group in owned)
            {
                if (group == null || group.IsDeleted) continue;
                try
                {
                    // False sends a kill packet to Firestorm; deleting only
                    // tracked object references avoids touching user content.
                    group.RootPart.RemoveParticleSystem();
                    scene?.DeleteSceneObject(group, false);
                }
                catch
                {
                    // Region teardown may already have deleted the objects.
                    // The caller logs any general update failure.
                }
            }
            owned.Clear();
        }

        public static Primitive.ParticleSystem CreateParticles(
            WeatherReading weather, int spacingMeters)
        {
            bool snow = weather.Condition == "snow";
            int intensity = Math.Clamp(weather.Intensity, 0, 100);
            int count = Math.Clamp(1 + intensity / 33, 1, 4);

            // Standard Second Life/OpenSim primitive particle protocol.
            // No custom viewer or asset texture required for the fallback.
            // The systems are intentionally low-density to limit FPS impact.
            return new Primitive.ParticleSystem
            {
                PartDataFlags = (Primitive.ParticleSystem.ParticleDataFlags)0x3,
                Pattern = (Primitive.ParticleSystem.SourcePattern)0x2,
                PartStartColor = snow
                    ? new Color4(0.95f, 0.97f, 1f, 0.82f)
                    : new Color4(0.70f, 0.82f, 0.97f, 0.72f),
                PartEndColor = snow
                    ? new Color4(1f, 1f, 1f, 0.0f)
                    : new Color4(0.70f, 0.82f, 0.97f, 0.0f),
                PartStartScaleX = snow ? 0.17f : 0.04f,
                PartStartScaleY = snow ? 0.17f : 0.38f,
                PartEndScaleX = snow ? 0.12f : 0.02f,
                PartEndScaleY = snow ? 0.12f : 0.25f,
                PartMaxAge = snow ? 17f : 8f,
                BurstRate = snow ? 0.42f : 0.32f,
                BurstPartCount = (byte)count,
                BurstRadius = Math.Min(28f, spacingMeters * 0.4f),
                BurstSpeedMin = snow ? 0.1f : 0.7f,
                BurstSpeedMax = snow ? 0.8f : 1.3f,
                PartAcceleration = snow
                    ? new Vector3(0.2f, 0.1f, -0.55f)
                    : new Vector3(0.3f, 0f, -3.0f),
                MaxAge = 0f
            };
        }
    }
}
