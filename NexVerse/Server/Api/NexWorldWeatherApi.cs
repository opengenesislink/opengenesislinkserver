// SPDX-License-Identifier: MPL-2.0
// Public read-only Stadtportal weather, based on the EXACT same forecast
// code as the simulator. The default is disabled: deployers must explicitly
// opt in and use the same WeatherSeed and TimeZone as the Region modules.
using System;
using System.Net;
using System.Text;
using Nini.Config;
using OpenGenesisLINK.Addons.OGLWeather;
using OpenSim.Framework.Servers.HttpServer;

namespace NexVerse.Server.Api
{
    internal sealed class NexWorldWeatherApi
    {
        private readonly bool m_Enabled;
        private readonly int m_Seed;
        private readonly TimeZoneInfo m_Zone;
        private readonly string m_DisplayName;
        private readonly bool m_Valid;

        public NexWorldWeatherApi(IConfigSource source)
        {
            IConfig cfg = source?.Configs["OGLWeatherPortal"];
            m_Enabled = cfg?.GetBoolean("Enabled", false) ?? false;
            if (!m_Enabled) return;

            try
            {
                m_Seed = cfg.GetInt("WeatherSeed", 20261028);
                string zoneName = cfg.GetString("TimeZone", "Europe/Berlin")
                    .Trim().Trim('"');
                m_Zone = TimeZoneInfo.FindSystemTimeZoneById(zoneName);
                m_DisplayName = cfg.GetString("DisplayName", "Stadt-NexVerse").Trim();
                // Never risk a bad public configuration taking down ROBUST.
                m_Valid = m_DisplayName.Length > 0 && m_DisplayName.Length <= 80;
            }
            catch (Exception)
            {
                m_Valid = false;
            }
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (request == null ||
                !string.Equals(request.HttpMethod, "GET",
                    StringComparison.OrdinalIgnoreCase))
            {
                response.AddHeader("Allow", "GET");
                Error(response, HttpStatusCode.MethodNotAllowed,
                    "method_not_allowed");
                return;
            }

            if (!m_Enabled || !m_Valid)
            {
                Error(response, HttpStatusCode.ServiceUnavailable,
                    !m_Enabled ? "weather_portal_disabled" :
                    "weather_portal_invalid_configuration");
                return;
            }

            try
            {
                // Public non-sensitive simulation data. No Bearer token is
                // required, but the existing World API rate gate is applied.
                response.RawBuffer = Encoding.UTF8.GetBytes(
                    OglWeatherPortalProjection.Serialize(
                        DateTimeOffset.UtcNow, m_Seed, m_Zone, m_DisplayName));
                response.AddHeader("Cache-Control", "public, max-age=30");
                response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch (Exception)
            {
                Error(response, HttpStatusCode.ServiceUnavailable,
                    "weather_unavailable");
            }
        }

        private static void Error(IOSHttpResponse response,
            HttpStatusCode status, string error)
        {
            response.AddHeader("Cache-Control", "no-store");
            response.StatusCode = (int)status;
            response.RawBuffer = Encoding.UTF8.GetBytes(
                "{\"error\":\"" + error + "\"}");
        }
    }
}
