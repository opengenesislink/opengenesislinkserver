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

## Umsetzung in PR #418 (nach dem ersten Audit)

- Die bisherige Aussage, ein lokaler Passwortadapter müsse vor dem Login neu gebaut werden, gilt für das vorliegende MVP **nicht mehr**. Das bereits vorhandene World-API-OAuth2/OIDC-Authorization-Code-Verfahren mit PKCE S256 verifiziert Avatarname und Passwort direkt im vertrauenswürdigen World-API-Loginformular; der Messenger sieht kein Avatarpasswort.
- Der API-Server überprüft danach per GET /api/v1/users/me, dass der Principal ein **aktiver lokaler** Avatar ist. Fremde HG-Avatare können nicht als lokale Accounts auftreten.
- Direkte lokale IMs nutzen den bestehenden POST /api/v1/messages aus NexCitizenImApi. Dessen bestehende Prüfungen für bestätigt befreundete lokale Avatare, Limits und native IM/Offline-Zustellung bleiben maßgeblich. Der Messenger übernimmt diese Rechte nicht selbst und behauptet keine Empfangsquittung.
- Opt-in-Historie nutzt GET/PATCH /api/v1/messages/settings sowie die bereits bestehende Speicherung. Es wird **kein** zweiter Inworld-Offline-IM-Eigentümer erstellt.
- Gruppen: GET /api/v1/groups/mine und GET /api/v1/groups/{groupId}/notices. Kein direkter Aufruf von IGroupsMessagingModule aus dem Webprozess; das würde ohne kontrollierte Authentifizierung Gruppenrechte umgehen.
- HG-Kontaktliste: vorhandenes GET /api/v1/relationships/{owner} bewahrt die Identität/Home-Grid-Herkunft und das im Freundesprofil vorliegende Präsenz-Sichtbarkeitsrecht. Ausgehende HG-IMs bleiben gesperrt.
- Separate Mobil-Präsenz speichert nur einen zeitlich begrenzten Heartbeat in der Messenger-Sitzung und modifiziert die autoritative Inworld-Presence **nicht**.
- Noch kein eigenes Redis-/MariaDB-Messenger-Cluster, keine WebPush- oder Server-zu-Server-Ingress-Brücke. Die eingeführte SQLite-Persistenz ist eine isolierte **Single-Node-Session-/Idempotenzdatenbank**, nicht ein Ersatz für den OpenSim-Nachrichtenspeicher.

Das Verhalten muss vor der produktiven Freischaltung noch mit mindestens zwei echten lokalen Avataren sowie vorhandener HG-Verbindung durchgespielt werden.
