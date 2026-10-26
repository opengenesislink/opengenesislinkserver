// SPDX-License-Identifier: MPL-2.0
// Authenticated ROBUST process console bridge. Invokes registered commands,
// never /bin/sh, a subprocess, or an arbitrary filesystem operation.
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NexVerse.Core.Security;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using log4net;

namespace NexVerse.Server.Api
{
    internal sealed class OglOperatorConsoleApi
    {
        private static readonly ILog s_Log = LogManager.GetLogger(typeof(OglOperatorConsoleApi));
        private readonly NexApiAuthenticator m_Auth;
        private readonly object m_Lock = new();
        private string m_CurrentJob;
        private string m_State = "idle";
        private string m_Error;
        private string m_Command;
        private DateTimeOffset m_Started;

        internal OglOperatorConsoleApi(NexApiAuthenticator auth) =>
            m_Auth = auth ?? throw new ArgumentNullException(nameof(auth));

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.ContentType = "application/json; charset=utf-8";
            response.KeepAlive = false;
            response.AddHeader("Cache-Control", "private, no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (!m_Auth.TryAuthenticate(request, NexScopes.AdminAll,
                out NexPrincipal principal, out UserAccount admin,
                out int status, out string code))
            {
                Send(response, status, new { error = "admin_auth_required" });
                return;
            }
            if (admin == null || !admin.LocalToGrid || admin.UserLevel < 200 ||
                !principal.HasScope(NexScopes.AdminAll))
            {
                Send(response, 403, new { error = "operator_access_denied" });
                return;
            }

            var console = MainConsole.Instance;
            if (console == null)
            {
                Send(response, 503, new { error = "robust_console_unavailable" });
                return;
            }
            if (request.HttpMethod == "GET")
            {
                string[] help = Array.Empty<string>();
                try { help = console.Commands.GetHelp(new[] { "help", "all" }).ToArray(); }
                catch (Exception e)
                {
                    s_Log.Warn("[OGL-OPERATOR]: Cannot read registered console commands: " + e.GetType().Name);
                }
                lock (m_Lock)
                    Send(response, 200, new {
                        commands = help, job = m_CurrentJob, state = m_State,
                        last_command = m_Command, started_utc = m_Started, error = m_Error
                    });
                return;
            }
            if (request.HttpMethod != "POST")
            {
                Send(response, 405, new { error = "method_not_allowed" });
                return;
            }

            string command;
            string confirmation;
            try
            {
                using JsonDocument requestBody = JsonDocument.Parse(request.InputStream);
                command = requestBody.RootElement.GetProperty("command").GetString()?.Trim();
                confirmation = requestBody.RootElement.GetProperty("confirmation").GetString();
            }
            catch (Exception e) when (e is JsonException || e is KeyNotFoundException ||
                                      e is InvalidOperationException)
            {
                Send(response, 400, new { error = "invalid_command_body" });
                return;
            }
            if (string.IsNullOrWhiteSpace(command) || command.Length > 350 ||
                command.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0 ||
                confirmation != "AUSFÜHREN")
            {
                Send(response, 400, new { error = "command_or_confirmation_invalid" });
                return;
            }
            string id;
            lock (m_Lock)
            {
                if (m_State == "running")
                {
                    Send(response, 409, new { error = "console_job_in_progress", job = m_CurrentJob });
                    return;
                }
                id = Guid.NewGuid().ToString("N");
                m_CurrentJob = id;
                m_Command = command;
                m_State = "running";
                m_Error = null;
                m_Started = DateTimeOffset.UtcNow;
            }

            // The log contains the command but never the bearer token or body.
            // Console output remains in the existing daily ROBUST log viewer.
            s_Log.Warn("[OGL-OPERATOR-AUDIT]: Local administrator issued ROBUST console command job=" +
                id + " command=" + command);
            _ = Task.Run(() =>
            {
                try
                {
                    console.RunCommand(command);
                    lock (m_Lock) { m_State = "completed"; }
                }
                catch (Exception e)
                {
                    s_Log.Error("[OGL-OPERATOR]: Command job failed " + id, e);
                    lock (m_Lock) { m_State = "failed"; m_Error = e.GetType().Name; }
                }
            });
            Send(response, 202, new { job = id, state = "running",
                note = "Konsolen-Ausgabe erscheint im ROBUST-Tageslog. Interaktive Befehle erfordern weiterhin die lokale Konsole." });
        }

        private static void Send(IOSHttpResponse response, int status, object body)
        {
            response.StatusCode = status;
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(body);
        }
    }
}
