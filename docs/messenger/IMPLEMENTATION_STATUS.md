# Messenger implementation status

## Added
- A standalone .NET 10 contracts project with local-avatar authentication, message bridge, presence, group membership and idempotent storage interfaces.
- A standalone ASP.NET Core API skeleton with `GET /healthz` and `GET /api/v1/capabilities`.
- A fail-closed `POST /api/v1/auth/login` returning 503 until the trusted local account adapter is implemented.
- Integration review of the concrete `IMessageTransferModule.SendInstantMessage(GridInstantMessage, MessageResultNotification)` interface and `IAuthenticationService.Authenticate(UUID, string, int)`.

## Critical integration notes
- `IMessageTransferModule` documentation says that for `MessageFromAgent` failure, the result callback may not be invoked; the `OnUndeliveredMessage` event must also be accounted for. A callback cannot be interpreted as proof of durable recipient delivery.
- `IAuthenticationService` requires a principal UUID, so the avatar-name-to-UUID resolution must be restricted to local user accounts before authenticating. Do not pass raw password hashes through HTTP.
- The current API is not a functioning messenger. It intentionally refuses login until secure integration is available.
- Run with `dotnet run --project OpenSim/Addons/OGLMessenger/Server/OGLMessenger.Server.csproj` on .NET 10 for local development. Build and runtime have not yet been tested.

## Next tasks
1. Local user account lookup + password verification adapter, locked to local identities.
2. Hashed refresh session persistence and secure cookies, CSRF protection, logout.
3. MariaDB message/outbox schema with idempotency constraints.
4. Bridge routing and inbound events, handling offline ownership and HG routing.
5. PWA client and Web Push.
