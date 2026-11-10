# OpenGenesisLINK Messenger integration audit — first pass

Inspected current main code paths on 2026-11-10:

- `OpenSim/Region/CoreModules/Avatar/InstantMessage/InstantMessageModule.cs` implements `ISharedRegionModule` and acquires `IMessageTransferModule` from the scene. Hooks `OnNewClient` and `OnIncomingInstantMessage`.
- `OpenSim/Framework/GridInstantMessage.cs` exposes `fromAgentID`, `toAgentID`, `dialog`, `fromGroup`, `message`, `imSessionID`, `offline`, `binaryBucket`, and timestamps. It does not provide a durable messenger message ID; the bridge must maintain a separate mapping.
- `OpenSim/Region/CoreModules/Avatar/Friends/FriendsModule.cs` implements `IFriendsModule` and uses `IPresenceService` and `IFriendsService`.
- `OpenSim/Region/CoreModules/Avatar/Friends/HGFriendsModule.cs` is a distinct HG integration path requiring further authorization and origin checks.
- `OpenSim/Addons/Groups/GroupsMessagingModule.cs` implements `IGroupsMessagingModule` and uses `IMessageTransferModule`, `IUserManagement`, `IGroupsServicesConnector`. The active configured module must be detected before integration.
- `OpenSim/Region/CoreModules/Avatar/InstantMessage/OfflineMessageModule.cs` and `OpenSim/Addons/OfflineIM/OfflineIMRegionModule.cs` both exist; deployment configuration determines the active path.

## Findings

1. Do not inject fabricated client sessions or write directly into region chat.
2. Implement an authenticated, local-only service bridge through existing module/service interfaces.
3. Outbound messages must be stored with idempotency keys before delivery attempts.
4. Do not claim a remote HG IM was delivered without remote acknowledgement.
5. Only one offline delivery path may own a message; otherwise duplicates are likely.
6. Group notices and live group chat must be separate APIs with independent authorization.
7. Mobile presence is a messenger-specific state; do not overwrite region presence records.

## Next code gates

- Trace the concrete `IMessageTransferModule` dispatch and remote HG routing behavior.
- Inspect current account/password validation service before implementing login.
- Determine which offline IM and group modules are enabled in the production configuration.
- Implement the authenticated bridge and persistence/outbox behind the contracts.
- Add integration tests using a disposable local grid before enabling write operations.

The contracts project is intentionally independent of region binaries. It can be compiled with `dotnet build OpenSim/Addons/OGLMessenger/Contracts/OGLMessenger.Contracts.csproj` on .NET 10. This is a source-only scaffold; compilation has not yet been verified in CI.
