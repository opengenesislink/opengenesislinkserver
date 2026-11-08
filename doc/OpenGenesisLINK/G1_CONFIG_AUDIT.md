# OpenGenesisLINK G1 – Vollstaendiger Konfigurationsabgleich
**Stand:** 8. November 2026  
**Projekt:** `opengenesislink/opengenesislinkserver`  
**Basis:** `main` vor dieser Aenderung: `78a2fb3cfa83cc33f62f6fdac7b31aae2d794d6a`  
**Original OpenSimulator:** `opensim/opensim`, `master` vom 7. November 2026, `9d00a5555225b17d3d1063cb0f3496fb1b6217f9`.

## Befund und Umfang

- Die aktive `bin/OpenSim.ini` besitzt nach dem optionalen Performance-Block **64 aktive Sektionen** und **269 explizite Schluessel**. Keine Sektion ist ohne aktive Einstellung.
- Die aktive `bin/Robust.HG.ini` besitzt **52 aktive Sektionen** und **315 explizite Schluessel**. Keine Sektion ist ohne aktive Einstellung.
- `OpenSim.ini.example` und `Robust.HG.ini.example` sind ebenfalls ohne leere Sektionen, und **jeder Schluessel der jeweiligen Vorlage ist im zugehoerigen aktiven Profil vorhanden**.
- `OpenSimDefaults.ini` besitzt 55 Sektionen mit 394 Schluesseln. Das sind gemeinsame Defaults, die nicht pauschal in die knappe Betreiber-INI kopiert werden duerfen. Einige historischen Sektionen sind nur Kommentar-/Erweiterungspunkte; sie erzeugen **keine** automatisch implementierte Funktion.
- Addon-`.ini.example`-Dateien und `config-include/*` bleiben eigene Ebenen. Sie sind **nicht** durch das blosse Vorhandensein eines Schluessels in der Hauptdatei ersetzt.

## Prioritaet 1 – keine ungeplanten Aenderungen

| Bereich | Schutzgrund | Kontrollierte Aenderung |
|---|---|---|
| ROBUST `[Const]`, `[Network]`, `[Hypergrid]`, `[GridInfoService]` | Grid-Identitaet und HG-/Viewer-Teleport; bestehende Teleportlinks und Grid-Adressen koennen ungultig werden | Nur mit FQDN-, Reverse-Proxy-, Login- und Gatekeeper-Tests |
| ROBUST `[DatabaseService]`, `[ServiceList]` | SQL-Konsistenz und Assembly-/Service-Registrierung | Backup, Schema-Migration, CI und Fallback |
| `[NexBus]` / `[NexVerseNodeAgent]` | HMAC-Vertrauensanker, signierte interne Events, Node-Erkennung | Identitaeten/Keys synchron auf ROBUST und allen Simulatoren rotieren |
| `[OGLFeatureControl]` | ES256-Policy-Signatur, OAR-/IAR-Archiv-Freigabe | Privaten Schluessel ausschliesslich auf ROBUST halten; alle Nodes bei Keyrotation pruefen |
| `[NexEconomy]`, `[NexEconomyViewer]` | NV$ Ledger, idempotente Buchungen und Viewer-Wallet | Keine Provider-/Waehrungs-/Wallet-Wechsel ohne Buchhaltungsabgleich |
| `[LoginService]`, `[NexVerseWorldApi]`, `[AccessControl]` | Registrierung, Auth, Proxy-Trust, Admin-/Viewer-Freigabe | Aenderungen nur nach Berechtigungs- und TLS-Test |
| `[HGAssetService]`, `[HGInventoryService]` | HG-Asset-Rechte, Inventarzugriff | Nie Export- oder Public-Legacy-Gates ungeprueft oeffnen |
| `[OpenGenesisLINKIAR]` / `[OglIarSelfService]` | Echte NAS-Backups via WebDAV | SSL-Validierung, Privatrechte und erfolgreichen Import testen |
| `[Startup]`, `[EntityTransfer]`, `[YEngine]`, `[OGLShutdown]` | Runtime/Physics, Scriptstatus, HG-/Regionswechsel, geordnete Evakuierung | Nur zusammen mit OAR-/IAR-, Rez-, TP- und Neustart-Tests |
| `[OfflineIMEmail]`, `[OfflineIMMailRelay]` | SMTP/IMAP plus signierte Antwort-Token | Host, TLS, RelayDomain, HMAC und Worker gemeinsam testen |
| `[OGLVoice]` | Signierte Viewer-/LiveKit-Sitzungen | ROBUST bewusst deaktiviert; Aktivierung nur mit Tenant, HTTPS, Node-ACL und Server-Credentials |

**„Nicht aendern“ bedeutet:** produktive, miteinander gekoppelte Parameter nicht isoliert oder ohne Migrations-/Rollbackplan veraendern. Sie sind nicht technisch unveraenderlich.

## Prioritaet 2 – Schalter, die absichtlich AUS bleiben

- ROBUST `[OGLVoice] Enabled = false`: Diese Angabe dokumentiert den bisherigen Zustand; Audio wird nicht durch das Anlegen der INI-Sektion implementiert. `EnableSessionAuthority` und `EnableFirestormGateway` bleiben aus. LiveKit-API-Secrets duerfen nicht in Git landen.
- ROBUST `[NexVerseWorldApi] PasswordRecoveryEnabled = false`: Eine Recovery-Freigabe erfordert einen gesonderten API-Key, vertrauliches Challenge-Handling und ein geprueftes Portal.
- ROBUST `AdminDocsTrustedProxyAddresses = ""`: Keine Client-/Wildcard-IP in die Proxy-Allowlist.
- Simulator `[SMTP] enabled = false`: Dies ist der allgemeine LSL-Maildienst und **nicht** der bereits konfigurierte Offline-IM-Mailversand.
- Simulator `[XBakes] URL = ""`: Der alte externe Bake-Endpunkt ist deaktiviert. Avatar-Appearance und ROBUST BakedTextureService sind eigenstaendige Funktionen.
- Simulator `[JsonStore] Enabled = false`, `[DataSnapshot] index_sims = false`: Keine unbeabsichtigte Legacy-K/V- oder Suchdaten-Exposition.
- Simulator `[OGLMeshLandImpact] Enabled = true`: Entspricht dem bereits freigegebenen G1-Stadt-NexVerse-Profil; im gemeinsamen `OpenSimDefaults.ini` bewusst aus.

## Gefundene funktionale Luecken – geschlossen

1. ROBUST `[OGLVoice]` fehlte vollstaendig. Die neun bekannten Schluessel sind jetzt mit sicheren, **deaktivierten** Ausgangswerten vorhanden; kein neuer Server oder Token-Service wird dadurch gestartet.
2. ROBUST `NexGroups.EnableGridWideLand = true` fehlte, ist aber bereits der dokumentierte `GetBoolean(..., true)`-Default.
3. ROBUST `NexVerseWorldApi`: `PasswordRecoveryEnabled`, `AdminDocsTrustedProxyAddresses`, `DatabaseMaintenanceCommandTimeoutSeconds` wurden anhand des eigenen Codes und der HG-Beispielkonfiguration ergaenzt.
4. Simulator: ehemals leere Sektionen `Estates`, `SMTP`, `ClientStack.LindenUDP`, `Chat`, `EntityTransfer`, `Wind`, `Materials`, `DataSnapshot`, `YEngine`, `MediaOnAPrim`, `Terrain`, `LandManagement`, `XBakes` wurden mit **tatsaechlich vom eigenen Code akzeptierten** Schluesseln belegt.
5. Die wichtigsten geerbten Kernblöcke `RegionReady`, `EstateManagement`, `Inventory`, `Attachments`, `Mesh`, `PacketPool`, `Monitoring`, `Statistics`, `JsonStore` und `OGLShutdown` sind als explizite, unveraenderte Defaults dokumentiert.
6. Simulator `NexVerseNodeAgent.LogForwardingEnabled = true` war bereits Code-Default, jetzt explizit dokumentiert. Logs koennen vertrauliche Diagnosen enthalten: nur ueber sichere Transporte.
7. HG-Beispiel `[Hypergrid]` enthielt nur auskommentierte Werte: `HomeURI` und `GatekeeperURI` sind nun ueber `[Const]` definiert. Produktions-FQDNs wurden nicht ueberschrieben.
8. CI prueft Konfigurationsvertraege, die INI-Kompaktheit nach aktiven Schluesseln, und existierende HTTP-Autoports-Regeln. Die umfangreichen Kommentare zaehlen nicht als Konfigurationswachstum.

**Wichtig:** Einstellungen sind keine Implementierung. Nicht durch Code gedeckte Phantasie-Flags wurden **nicht** hinzugefuegt. Die aktiven Werte stellen keine automatisierte Ermittlung aller moeglichen `GetString/GetBoolean/GetInt`-Aufrufe und aller externen Plugins dar.

## Offener, eigenstaendiger Sicherheitsbefund

In `OpenSim/Region/CoreModules/World/Estate/EstateModule.cs` ist ein historischer statischer Default-Token fuer `[Estates]` hinterlegt. Ein blosser INI-Default kann die Sicherheit eines exponierten HTTP-Handers nicht garantieren. Die bestehenden Handler-/Port-Werte wurden **nicht** riskant umgestellt. Empfehlung: in einem separaten Security-PR einen zufaelligen, geschuetzten Token oder eine authentifizierte interne Route erzwingen und Estate-API-Gates testen.

## Gezielter Vergleich gegen Original OpenSimulator (7.–8.11.2026)

Upstream-Referenz: https://github.com/opensim/opensim (Branch `master`, SHA `9d00a5555225b17d3d1063cb0f3496fb1b6217f9`).

| Upstream-Commit | Modul / Unterschied | G1-Einordnung |
|---|---|---|
| `acb3dd2d6785` (04.11.) | `Warp3DImageModule`: Null-/Laengencheck vor JPEG2000-Texturdekodierung | **Relevant, im G1 sinngemaess bereits abgesichert:** G1 LunaTexture klassifiziert inkompatible Asset-Payloads und behandelt leere Daten. Kein blindes Ueberschreiben der erweiterten G1-Pipeline. Kurze 1–2-Byte-Payloads als extra Regression pruefen |
| `5a5ffee0df12` (04.11.) | Materialeintrag loeschen ohne Index-Overflow | **Bereits vorhanden:** G1 `MaterialsModule` prueft `indx >= entries.Length` |
| `092a7f54b637` (04.11.) | PBR-Override-Eintraege im LLUDP-Protokoll spaeter/anders kodieren | **Bereits teilweise vorhanden:** G1 `LLClientView` nutzt `new OSDllsdxml(ovr.data)`; nur Viewer-Protokolltests entscheiden ueber weitere Uebernahme |
| `f53491faa148` (04.11.) | UDP-UseCircuitCode und Cache-Reinigung / neue `ExpiringCacheOS.Remove(out ...)` | **Moeglicher Folgekandidat** bei mehrfachen Login-/Reconnect-Circuits. Vor Backport Session-/Logout-/Hypergrid-Tests, nicht im INI-PR mischen |
| `755aa0e4e68b` (07.11.) | PostgreSQL-Inventar-Abfrage: gequotete Spaltennamen | **Nicht fuer aktuelle MariaDB-Deployment-Konfiguration erforderlich**, nur bei PostgreSQL-Provider relevant |
| `8e3f6932197c` (06.11.) | PostgreSQL-18-Estate-Manager/-Bans | **Nur PostgreSQL**, separates Backend-Review noetig |
| `cbab20a5735a` (05.11.) | PostgreSQL-UserProfiles-Upserts und Insertkorrekturen | **Nur PostgreSQL**, nicht ohne Migration in MariaDB-Pfade portieren |
| `929b6dc79390` (07.11.) | Bereinigung eines `OutboundUrlFilter.Split`-Ausdrucks | **In G1 bereits sauber:** einfache `Split(new char[]{'|'}, RemoveEmptyEntries)` |
| `9d00a5555225` (07.11.) | Whitespace-Korrektur | Keine funktionale Uebernahme erforderlich |

Dies ist ein **gezielter Abgleich** aktueller Upstream-Aenderungen und gemeinsam genutzter Module, **kein automatischer Voll-Diff oder Merge aller 2.900+ Upstream-Dateien**. G1-Regionen, Addons, .NET-10-Build, Hypergrid, OpenMetaverse-Interop und eigene Datenbankschemata haben Vorrang.

## Anhang A – Aktive ROBUST-HG-Sektionen und Schluessel

Alle Schluesselnamen, **keine geheimen Werte**. `Robust.HG.ini.example` liefert Installationsdefaults; die Stadt-NexVerse-Werte werden nicht durch die Beispiel-INI ersetzt.

### `[Environment]` – Service-/Modulkonfiguration

`NEXVERSE_DB_PASSWORD`, `NEXVERSE_ROBUST_CERT_PASSWORD`, `NEXVERSE_NATIVE_TOKEN_SIGNING_KEY`, `NEXVERSE_NEXBUS_SHARED_KEY`, `NEXVERSE_NEXBUS_PEERS`, `NEXVERSE_IM_RELAY_SIGNING_KEY`, `NEXVERSE_IM_RELAY_IMAP_PASSWORD`, `NEX_PORTAL_IM_SECRET`

### `[Const]` – Grid-FQDNs und feste Public-/Private-Ports

`BaseHostname`, `BaseURL`, `PublicPort`, `PrivatePort`

### `[Startup]` – Simulatorstart, Skriptengine, Physik, Regionssicht, Zertifikate

`RegistryLocation`, `ConfigDirectory`, `ConsoleHistoryFileEnabled`, `ConsoleHistoryFile`, `ConsoleHistoryFileLines`, `EnableRobustSelfsignedCertSupport`, `RobustCertRenewOnStartup`, `RobustCertFileName`, `RobustCertPassword`, `RobustCertHostName`, `RobustCertHostIp`, `NoVerifyCertChain`, `NoVerifyCertHostname`

### `[ServiceList]` – ROBUST-Service-Connector und Assembly-Auswahl

`AssetServiceConnector`, `InventoryInConnector`, `GridServiceConnector`, `GridInfoServerInConnector`, `NexVerseWorldApiConnector`, `NexEconomyConnector`, `AuthenticationServiceConnector`, `AvatarServiceConnector`, `LLLoginServiceInConnector`, `PresenceServiceConnector`, `UserAccountServiceConnector`, `GridUserServiceConnector`, `AgentPreferencesServiceConnector`, `FriendsServiceConnector`, `MapAddServiceConnector`, `MapGetServiceConnector`, `GroupsServiceConnector`, `UserProfilesServiceConnector`, `MuteListConnector`, `GatekeeperServiceInConnector`, `UserAgentServerConnector`, `HeloServiceInConnector`, `HGFriendsServerConnector`, `InstantMessageServerConnector`, `HGInventoryServiceConnector`, `HGAssetServiceConnector`, `HGGroupsServiceConnector`

### `[Network]` – Listener, Reverse-Proxy und Rate-Grenzen

`port`, `AllowllHTTPRequestIn`

### `[Hypergrid]` – HG-Home/Gatekeeper-FQDNs

`HomeURI`, `GatekeeperURI`

### `[AccessControl]` – Viewer-Zugang und Zugangspolitik

`OGLApprovedViewersOnly`

### `[DatabaseService]` – gemeinsamer SQL-Datenbestand

`StorageProvider`, `ConnectionString`

### `[NexDiscovery]` – Land/Region-Discovery

`StorePath`, `TeleportBaseUri`

### `[NexGroups]` – gruppenweite Land-/Rechte-Auskunft

`Enabled`, `EnableGridWideLand`

### `[NexExperiences]` – NexVerse-Experiences-Store und Writer-Schutz

`StoreBackend`, `StorePath`

### `[NexEconomy]` – serverautoritative NV$-Buchhaltung

`Enabled`, `StorageProvider`, `ConnectionString`, `InitializeSchema`, `DailyLoginRewardEnabled`, `DailyLoginRewardTimezone`

### `[EstateDataStore]` – Service-/Modulkonfiguration

`LocalServiceModule`

### `[AssetService]` – Service-/Modulkonfiguration

`LocalServiceModule`, `BaseDirectory`, `SpoolDirectory`, `DaysBetweenAccessTimeUpdates`, `ShowConsoleStats`, `DefaultAssetLoader`, `AssetLoaderArgs`

### `[InventoryService]` – Service-/Modulkonfiguration

`LocalServiceModule`, `AllowDelete`

### `[GridService]` – Service-/Modulkonfiguration

`LocalServiceModule`, `AssetService`, `HypergridLinker`, `ExportSupported`

### `[AuthenticationService]` – Service-/Modulkonfiguration

`LocalServiceModule`

### `[OglAvatarTemplates]` – Service-/Modulkonfiguration

`Enabled`, `BenjaminModel`, `LunaModel`, `RequirePublishedModels`, `BenjaminPublishedModel`, `LunaPublishedModel`

### `[UserAccountService]` – Service-/Modulkonfiguration

`LocalServiceModule`, `AuthenticationService`, `PresenceService`, `GridService`, `InventoryService`, `AvatarService`, `GridUserService`, `CreateDefaultAvatarEntries`

### `[GridUserService]` – Service-/Modulkonfiguration

`LocalServiceModule`

### `[AgentPreferencesService]` – Service-/Modulkonfiguration

`LocalServiceModule`

### `[PresenceService]` – Service-/Modulkonfiguration

`LocalServiceModule`

### `[AvatarService]` – Service-/Modulkonfiguration

`LocalServiceModule`

### `[FriendsService]` – Service-/Modulkonfiguration

`LocalServiceModule`

### `[EstateService]` – Service-/Modulkonfiguration

`LocalServiceModule`

### `[LibraryService]` – Service-/Modulkonfiguration

`LibraryName`, `DefaultLibrary`

### `[LoginService]` – Login, Startregion, TOS, Land-/Marketplace-Adressen

`EnableNexVerseResidentNames`, `EnableFirestormBridgeBootstrap`, `FirestormBridgeVersion`, `LocalServiceModule`, `UserAccountService`, `GridUserService`, `AuthenticationService`, `InventoryService`, `AvatarService`, `PresenceService`, `GridService`, `SimulationService`, `LibraryService`, `FriendsService`, `UserAgentService`, `HGInventoryServicePlugin`, `Currency`, `WelcomeMessage`, `NexVerseTosEnabled`, `NexVerseTosVersion`, `NexVerseTosUrl`, `WelcomeMessages`, `AllowRemoteSetLoginLevel`, `MapTileURL`, `SearchURL`, `DestinationGuide`, `SRV_HomeURI`, `SRV_InventoryServerURI`, `SRV_AssetServerURI`, `SRV_ProfileServerURI`, `SRV_FriendsServerURI`, `SRV_IMServerURI`, `SRV_GroupsServerURI`, `DSTZone`, `AllowLoginFallbackToAnyRegion`

### `[MapImageService]` – Service-/Modulkonfiguration

`LocalServiceModule`

### `[NexMetrics]` – read-only Betriebsmetriken

`Enabled`, `Path`

### `[NexTelemetry]` – signierte OpenTelemetry-Ausgabe

`Enabled`, `Protocol`, `Endpoint`, `TracesEndpoint`, `MetricsEndpoint`, `ServiceName`, `ServiceInstanceId`, `Headers`, `ExportIntervalSeconds`, `BatchSize`, `QueueCapacity`, `TimeoutMilliseconds`, `MaxRetries`, `AllowInsecure`

### `[NexBus]` – signierte Server-Events und Knotensitzungen

`Enabled`, `NodeId`, `SharedKey`, `Peers`, `InboundPath`, `QueueCapacity`, `RequestTimeoutMilliseconds`, `DeduplicationWindow`, `NodeStaleAfterSeconds`

### `[OGLVoice]` – ROBUST Voice-Discovery (optionale Freigabe)

`Enabled`, `Mode`, `ServiceUrl`, `TenantId`, `IncludeHypergridGuests`, `EnableSessionAuthority`, `AllowedNodes`, `EnableFirestormGateway`, `MediaGatewayUrl`

### `[NexPortalIM]` – Portal-Messenger-Archiv

`Enabled`, `StoreBackend`, `StorageProvider`, `ConnectionString`, `RetentionDays`, `SharedSecret`

### `[NexNPCPortal]` – Service-/Modulkonfiguration

`Enabled`, `ExpectedRegionCount`

### `[NexNPCChat]` – Service-/Modulkonfiguration

`Enabled`, `RetentionDays`

### `[NexVerseWorldApi]` – NexVerse World API/Portal-Registrierung/Admin-Rechte

`Enabled`, `PortalAvatarRegistrationEnabled`, `PasswordRecoveryEnabled`, `PortalAccountSelfDeleteEnabled`, `PortalRegistrationTrustedProxyIPs`, `PortalRegistrationTrustedHttpPeers`, `AdminDocsTrustedProxyAddresses`, `PortalRegistrationDefaultRegionName`, `PortalRegistrationDefaultX`, `PortalRegistrationDefaultY`, `PortalRegistrationDefaultZ`, `PublicBaseUrl`, `EnablePrivilegedEndpoints`, `AdminMinimumUserLevel`, `TokenLifetimeSeconds`, `DatabaseMaintenanceCommandTimeoutSeconds`, `RateLimitEnabled`, `RateLimitRequests`, `RateLimitWindowSeconds`, `TrustForwardedFor`, `AuditStorePath`, `AuditConsoleMode`, `AuditConsolePublicReads`, `AuditConsoleShowIds`, `ApiKeyStorePath`, `IdempotencyStorePath`, `IdempotencyTtlSeconds`, `EnableNativeTokens`, `NativeTokenSigningKey`, `NativeTokenIssuer`, `NativeTokenAudience`, `AuthStorePath`, `OidcIssuer`, `OidcSigningKeyPath`, `OidcTokenLifetimeSeconds`, `AuthorizationCodeLifetimeSeconds`, `RefreshTokenLifetimeSeconds`, `UserAccountServiceModule`, `AuthenticationServiceModule`

### `[GridInfoService]` – oeffentliche Grid-/Viewer-Identitaet

`login`, `gridname`, `gridnick`, `welcome`, `about`, `register`, `help`, `password`, `gatekeeper`

### `[GatekeeperService]` – Service-/Modulkonfiguration

`LocalServiceModule`, `UserAccountService`, `UserAgentService`, `PresenceService`, `GridUserService`, `GridService`, `AuthenticationService`, `SimulationService`, `AllowTeleportsToAnyRegion`

### `[UserAgentService]` – Service-/Modulkonfiguration

`LocalServiceModule`, `GridUserService`, `GridService`, `GatekeeperService`, `PresenceService`, `FriendsService`, `UserAccountService`

### `[HGInventoryService]` – Hypergrid-Inventar-Gates

`AllowLegacyPublicHGInventory`, `LocalServiceModule`, `UserAccountsService`, `AvatarService`, `AuthType`

### `[HGAssetService]` – Hypergrid-Asset-Sicherheit

`DisallowAllExport`, `MaxHGAssetExistIds`, `MaxHGAssetExistXmlChars`, `LocalServiceModule`, `UserAccountsService`, `AuthType`

### `[HGFriendsService]` – Service-/Modulkonfiguration

`LocalServiceModule`, `UserAgentService`, `FriendsService`, `UserAccountService`, `GridService`, `PresenceService`

### `[HGInstantMessageService]` – Service-/Modulkonfiguration

`LocalServiceModule`, `GridService`, `PresenceService`, `UserAgentService`, `InGatekeeper`

### `[Messaging]` – Service-/Modulkonfiguration

`OfflineIMService`

### `[OfflineIMMailRelay]` – IMAP-Rueckantworten auf Offline-Nachrichten

`Enabled`, `RelayDomain`, `RelaySigningKey`, `IMAPHost`, `IMAPPort`, `UseSslOnConnect`, `UseStartTls`, `IMAPUsername`, `IMAPPassword`, `PollSeconds`, `BatchSize`, `MaximumReplyCharacters`, `MaximumReplyLifetimeDays`

### `[Groups]` – Service-/Modulkonfiguration

`NexModerationStorePath`, `OfflineIMService`, `UserAccountService`

### `[UserProfilesService]` – Service-/Modulkonfiguration

`LocalServiceModule`, `Enabled`, `UserAccountService`, `AuthenticationServiceModule`

### `[BakedTextureService]` – Service-/Modulkonfiguration

`LocalServiceModule`, `BaseDirectory`

### `[MuteListService]` – Service-/Modulkonfiguration

`LocalServiceModule`

### `[OGLFeatureControl]` – ES256-Schluessel und OAR/IAR-Freigabe

`Enabled`, `PrivateKeyPath`, `StorePath`

### `[OGLWeatherPortal]` – oeffentliche Grid-Wetter-API

`Enabled`, `WeatherSeed`, `TimeZone`, `DisplayName`

### `[OglIarSelfService]` – ROBUST WebDAV-Inventory-Backup

`Enabled`, `StorageBackend`, `TemporaryStorageRoot`, `NasHost`, `WebDavBaseUrl`, `WebDavUsername`, `WebDavPasswordFile`, `SharedStorageRoot`, `RetentionDays`, `MaximumArchiveMiB`

## Anhang B – Aktive Simulator-Sektionen und Schluessel

G1 Performance Engine (PR #416) fuegt den deaktivierten Abschnitt `OGLPerformance` fuer interne NodeAgent-Diagnostik hinzu. Die optionalen Flotsam-Tuningoptionen `FileWriteConcurrency`, `MaxPendingFileWrites`, `MemoryCacheMaxMiB` und `MemoryCacheMaxEntries` gehoeren zur separaten `[AssetCache]`-Konfiguration und werden nicht durch die Betreiber-Haupt-INI erzwungen. Details: `doc/OpenGenesisLINK/OGL_PERFORMANCE_ENGINE_1_0.md`.

Die angefuehrten Werte stehen nach dieser Aktualisierung in `bin/OpenSim.ini`. Zusätzliche defaults koennen geerbt oder durch `config-include/GridHypergrid.ini` gesetzt sein.

### `[OGLPerformance]` – optionale read-only CPU-, RAM- und Heartbeat-Telemetrie

`Enabled` (Standard: `false`; Messwerte werden ausschliesslich ueber den authentifizierten NodeAgent-Heartbeat uebertragen).

### `[OGLFeatureControl]` – ES256-Schluessel und OAR/IAR-Freigabe

`Enabled`, `PublicKeyPath`

### `[NexVerseAvatarEvacuation]` – Simulator-/Regionmodul-Konfiguration

`Enabled`, `WaitSeconds`

### `[NexVerseNodeAgent]` – signierte Simulator/ROBUST-Kommunikation

`Enabled`, `NodeId`, `PeerUrl`, `SharedKey`, `LogForwardingEnabled`, `InboundPath`, `HeartbeatSeconds`, `QueueCapacity`, `RequestTimeoutMilliseconds`, `DeduplicationWindow`, `CrossRegionObjectMessaging`, `CrossRegionChatMessaging`, `AllowCrossOwnerObjectMessages`, `EstateScriptMessaging`, `GridScriptMessaging`, `ObjectMessagesPerSecond`, `ObjectMessageMaxAgeSeconds`, `ManagedRegionCommands`, `UpdateStagingEnabled`, `UpdateStagingRoot`, `UpdateStagingBuild`, `ManagedRegionConfigDirectory`, `ManagedRegionInternalAddress`, `ManagedRegionExternalHostName`, `ManagedRegionType`, `ManagedRegionPortMin`, `ManagedRegionPortMax`

### `[OpenGenesisLINKOAR]` – OAR-Sicherung/Restore

`Enabled`, `StorageRoot`, `MaximumArchiveMiB`, `MigrationStorageId`

### `[Const]` – Grid-FQDNs und feste Public-/Private-Ports

`BaseHostname`, `BaseURL`, `PublicPort`, `PrivURL`, `PrivatePort`

### `[OGLMeshLandImpact]` – landbasierte Mesh-Quota statt roher Linkanzahl

`Enabled`

### `[Startup]` – Simulatorstart, Skriptengine, Physik, Regionssicht, Zertifikate

`OGLExtendedRegionVisibility`, `OGLIarLiveProgress`, `meshing`, `physics`, `EnableCertConverter`, `PemCertPublicKey`, `PemCertPrivateKey`, `outputCertName`, `outputCertPassword`, `EnableSelfsignedCertSupport`, `CertRenewOnStartup`, `CertFileName`, `CertPassword`, `CertHostName`, `CertHostIp`

### `[AccessControl]` – Viewer-Zugang und Zugangspolitik

`OGLApprovedViewersOnly`

### `[Map]` – Simulator-/Regionmodul-Konfiguration

`GenerateMaptiles`, `FastMapAsyncStartup`, `FastMapStartupDelaySeconds`, `MapImageModule`, `TextureOnMapTile`, `DrawPrimOnMapTile`, `TexturePrims`, `RenderMeshes`, `RenderMaxHeight`

### `[Permissions]` – Simulator-/Regionmodul-Konfiguration

`automatic_gods`, `implicit_gods`, `allow_grid_gods`

### `[Estates]` – interner Estate-Kommunikationshandler

`EstateCommunicationsHandler`, `Port`

### `[SMTP]` – separater LSL-Email-Server (derzeit aus)

`enabled`

### `[Network]` – Listener, Reverse-Proxy und Rate-Grenzen

`http_listener_port`, `ExternalHostNameForLSL`, `shard`

### `[NexVerseAutoPorts]` – dynamische Simulator-HTTP-Ports

`Enabled`, `HttpPortMin`, `HttpPortMax`

### `[XMLRPC]` – Simulator-/Regionmodul-Konfiguration

`XmlRpcRouterModule`, `XmlRpcPort`

### `[ClientStack.LindenUDP]` – Viewer-UDP, Bandbreite, ACK und Timeouts

`scene_throttle_max_bps`, `TextureSendLimit`, `AckTimeout`, `PausedAckTimeout`

### `[ClientStack.LindenCaps]` – Viewer-CAPS, Uploads und Objekt-/Inventar-HTTP

`Cap_GetTexture`, `Cap_GetMesh`, `Cap_AvatarPickerSearch`, `Cap_GetDisplayNames`, `Cap_SetDisplayName`

### `[SimulatorFeatures]` – Simulator-/Regionmodul-Konfiguration

`SearchServerURI`, `DestinationGuideURI`

### `[Chat]` – lokale Chatreichweiten

`enabled`, `whisper_distance`, `say_distance`, `shout_distance`

### `[EntityTransfer]` – Teleport, Grenzuebergang und Hypergrid

`AllowAvatarCrossing`, `DisableObjectTransfer`, `LevelHGTeleport`, `DisableInterRegionTeleportCancellation`, `LandingPointBehavior`

### `[Messaging]` – Simulator-/Regionmodul-Konfiguration

`OfflineMessageModule`

### `[OfflineIMEmail]` – Offline-IM-Mailversand via SMTP STARTTLS

`Enabled`, `RelayDomain`, `SubjectTemplate`, `RelaySigningKey`, `ReplyLifetimeDays`, `SMTPHost`, `SMTPPort`, `UseStartTls`, `UseSslOnConnect`, `SMTPUsername`, `SMTPPassword`, `PerSenderPerHour`, `PerRecipientPerHour`, `GlobalPerHour`

### `[BulletSim]` – Simulator-/Regionmodul-Konfiguration

`AvatarToAvatarCollisionsByDefault`

### `[Wind]` – Simulator-Wind (nicht OGLWeather)

`enabled`, `wind_update_rate`, `wind_plugin`, `strength`

### `[Materials]` – PBR/Materialien und Request-Limits

`enable_materials`, `MaxMaterialsPerTransaction`

### `[DataSnapshot]` – Legacy-Suche/Indexierung (aus)

`index_sims`, `data_exposure`, `gridname`, `default_snapshot_period`, `snapshot_cache_directory`

### `[Economy]` – Simulator-/Regionmodul-Konfiguration

`economymodule`

### `[OGLViewerAdvisor]` – Simulator-/Regionmodul-Konfiguration

`Enabled`, `IntervalMinutes`, `OfficialViewerPrefixes`, `Message`

### `[NexDiscoveryViewer]` – Simulator-/Regionmodul-Konfiguration

`Enabled`, `LegacyFirestormCompatibility`, `WorldApiBaseUrl`, `GroupLandApiKey`, `RequestTimeoutMilliseconds`

### `[NexExperiencesViewer]` – Viewer-Experiences, derzeit aus

`Enabled`, `WorldApiBaseUrl`, `ApiKey`, `RequestTimeoutMilliseconds`, `FirestormReadCaps`, `FirestormPermissionCaps`, `ScriptPendingConsent`, `NativeExperiencePrompt`, `ViewerPermissionsApiKey`, `FirestormManageCaps`, `ViewerManageApiKey`

### `[NexEconomyViewer]` – Simulator-/Regionmodul-Konfiguration

`Enabled`, `WorldApiBaseUrl`, `ApiKey`, `FeeWalletId`, `UploadCharge`, `GroupCreationCharge`, `RequestTimeoutMilliseconds`, `BalanceRefreshSeconds`, `AllowHypergridGuests`, `MasterBankEnabled`, `MasterBankAccountId`, `MasterBankOperatorId`, `MasterBankAuthorizedScripts`, `MasterBankMaxPayout`, `CurrencyPurchasePortalUrl`

### `[YEngine]` – Standard-LSL-/OSSL-Skripting

`Enabled`, `UseSourceHashCode`, `MinTimerInterval`

### `[XEngine]` – Legacy-Skripting / kompatible Fallbacks

`AppDomainLoading`

### `[NexPortalIM]` – Portal-Messenger-Archiv

`Enabled`, `IngestUrl`, `SharedSecret`

### `[OSSL]` – Simulator-/Regionmodul-Konfiguration

`Include-osslDefaultEnable`

### `[Groups]` – Simulator-/Regionmodul-Konfiguration

`Enabled`, `Module`, `ServicesConnectorModule`, `LocalService`, `GroupsServerURI`, `MessagingEnabled`, `MessagingModule`, `NoticesEnabled`, `MessageOnlineUsersOnly`

### `[InterestManagement]` – Simulator-/Regionmodul-Konfiguration

`FastRezNearbyFirst`

### `[MediaOnAPrim]` – Simulator-/Regionmodul-Konfiguration

`Enabled`

### `[NPC]` – NPC/Avatar-Simulation

`Enabled`, `NexNPCSnapshotEnabled`, `NexNPCChatEnabled`

### `[Terrain]` – Terrain-Erstellung/Viewer-Aktualisierung

`InitialTerrain`, `SendTerrainUpdatesByViewDistance`, `OglTerrainBrushStrengthMultiplier`

### `[LandManagement]` – Grundstueck-Updates und Ban-Linien

`LimitParcelLayerUpdateDistance`, `ParcelLayerViewDistance`, `ShowParcelBansLines`

### `[UserProfiles]` – Simulator-/Regionmodul-Konfiguration

`ProfileServiceURL`

### `[FirestormBridge]` – Simulator-/Regionmodul-Konfiguration

`Enabled`

### `[Textures]` – Simulator-/Regionmodul-Konfiguration

`DynamicTexturePreviousRetentionSeconds`, `DynamicTexturePreviousRetentionLimit`

### `[Appearance]` – Avatar-Aussehen/Backen

`PersistBakedTextures`, `ReuseTextures`, `DelayBeforeAppearanceSend`, `DelayBeforeAppearanceSave`, `WearablesRequestDelayMs`, `ResendAppearanceUpdates`

### `[XBakes]` – alter externer Bake-Endpunkt (aus)

`URL`

### `[NexTraffic]` – Ampel- und Strassenstatus

`Enabled`, `LSLChannel`, `RefreshSeconds`, `GreenSeconds`, `YellowSeconds`, `AllRedSeconds`, `Junctions`

### `[NexTransit]` – OePNV/Sollfahrplan

`Enabled`, `LSLChannel`, `RefreshSeconds`, `TimeZone`, `MaxDepartures`, `Routes`

### `[NexSmartLight]` – dynamische Strassenbeleuchtung

`Enabled`, `LSLChannel`, `RefreshSeconds`, `TimeZone`, `NightStarts`, `NightEnds`

### `[OGLWeather]` – Stadt-NexVerse Wetter- und Partikeladdon

`Enabled`, `WeatherSeed`, `TimeZone`, `LSLChannel`, `CurrentBroadcastSeconds`, `ForecastBroadcastSeconds`, `CloudBaseMeters`, `LegacyParticleEmitters`, `ViewerEnvironmentEffects`, `EmitterHeightMeters`, `EmitterSpacingMeters`, `MaxEmittersPerRegion`, `VisualRefreshSeconds`, `TestWeatherMode`, `TestWeatherIntensity`, `TestWeatherDurationMinutes`

### `[OGLEnergy]` – Stadtweite Energiedaten

`Enabled`, `LSLChannel`, `RefreshSeconds`, `TimeZone`, `SolarCapacityKw`, `WindCapacityKw`, `HydroCapacityKw`, `BaseCapacityKw`, `BaseDemandKw`

### `[OpenGenesisLINKIAR]` – WebDAV-IAR-Backups

`Enabled`, `StorageBackend`, `NasHost`, `WebDavBaseUrl`, `WebDavUsername`, `WebDavPasswordFile`, `MaximumArchiveMiB`

### `[RegionReady]` – Login-Gate bis Region bereit

`enabled`, `channel_notify`, `login_disable`

### `[EstateManagement]` – Estate-Neustarts

`AllowRegionRestartFromClient`

### `[Inventory]` – objektgebundene Inventar- und Rezlogik

`CoalesceMultipleObjectsToInventory`

### `[Attachments]` – Avatar-Attachments

`Enabled`

### `[Mesh]` – Mesh-Upload und Viewer-Kompatibilitaet

`AllowMeshUpload`

### `[PacketPool]` – Simulator-/Regionmodul-Konfiguration

`RecycleBaseUDPPackets`

### `[Monitoring]` – Simulator-/Regionmodul-Konfiguration

`Enabled`

### `[Statistics]` – Simulator-/Regionmodul-Konfiguration

`NumberOfFrames`

### `[JsonStore]` – Legacy-Skript-K/V-Storage (aus)

`Enabled`, `EnableObjectStore`, `MaxStringSpace`

### `[OGLShutdown]` – geordnetes Herunterfahren und Evakuierung

`RegionCloseTimeoutSeconds`

### `[Architecture]` – Service-Auswahl fuer Grid/Standalone

`Include-Architecture`

## Anhang C – Standard-Optionen (geerbt vs explizit)

Datenquelle: `bin/OpenSimDefaults.ini`. Diese Einstellungen duerfen nicht alle parallel in `OpenSim.ini` kopiert werden: eine bewusste globale Default-Ebene erleichtert G1-Updates. Falls ein Default in OpenSim.ini fehlt, wird er **nicht automatisch als Fehler** klassifiziert.

### `[NexVerseNodeAgent]` (26 Defaults; 26 explizit, 0 geerbt)

**Auch im aktiven Profil:** `Enabled`, `NodeId`, `PeerUrl`, `SharedKey`, `InboundPath`, `HeartbeatSeconds`, `QueueCapacity`, `RequestTimeoutMilliseconds`, `DeduplicationWindow`, `CrossRegionObjectMessaging`, `CrossRegionChatMessaging`, `AllowCrossOwnerObjectMessages`, `EstateScriptMessaging`, `GridScriptMessaging`, `ObjectMessagesPerSecond`, `ObjectMessageMaxAgeSeconds`, `ManagedRegionCommands`, `UpdateStagingEnabled`, `UpdateStagingRoot`, `UpdateStagingBuild`, `ManagedRegionConfigDirectory`, `ManagedRegionInternalAddress`, `ManagedRegionExternalHostName`, `ManagedRegionType`, `ManagedRegionPortMin`, `ManagedRegionPortMax`

**Nicht lokal ueberschrieben:** —

### `[OpenGenesisLINKOAR]` (4 Defaults; 4 explizit, 0 geerbt)

**Auch im aktiven Profil:** `Enabled`, `StorageRoot`, `MaximumArchiveMiB`, `MigrationStorageId`

**Nicht lokal ueberschrieben:** —

### `[OGLMeshLandImpact]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `Enabled`

**Nicht lokal ueberschrieben:** —

### `[Startup]` (58 Defaults; 13 explizit, 45 geerbt)

**Auch im aktiven Profil:** `meshing`, `physics`, `EnableCertConverter`, `PemCertPublicKey`, `PemCertPrivateKey`, `outputCertName`, `outputCertPassword`, `EnableSelfsignedCertSupport`, `CertRenewOnStartup`, `CertFileName`, `CertPassword`, `CertHostName`, `CertHostIp`

**Nicht lokal ueberschrieben:** `ConsolePrompt`, `ConsoleHistoryFileEnabled`, `ConsoleHistoryFile`, `ConsoleHistoryFileLines`, `save_crashes`, `crash_dir`, `startup_console_commands_file`, `shutdown_console_commands_file`, `async_call_method`, `MaxPoolThreads`, `JobEngineEnabled`, `RegistryLocation`, `ConfigDirectory`, `clientstack_plugin`, `region_info_source`, `allow_regionless`, `see_into_region`, `MaxPrimUndos`, `NonPhysicalPrimMax`, `PhysicalPrimMax`, `ClampPrimSize`, `ClampNegativeZ`, `LinksetPrims`, `AllowScriptCrossing`, `TrustBinaries`, `InworldRestartShutsDown`, `FrameTime`, `UpdateObjectsEveryNFrames`, `UpdateAgentsEveryNFrames`, `UpdateEntityMovementEveryNFrames`, `UpdateCoarseLocationsEveryNFrames`, `UpdatePhysicsEveryNFrames`, `UpdateEventsEveryNFrames`, `UpdateTerrainEveryNFrames`, `UpdateStorageEveryNFrames`, `UpdateTempCleaningEveryNSeconds`, `MinimumTimeBeforePersistenceConsidered`, `MaximumTimeBeforePersistenceConsidered`, `collidable_prim`, `physical_prim`, `DefaultScriptEngine`, `enableflyslow`, `Stats_URI`, `UseTrashOnDelete`, `LogOverloads`

### `[Map]` (14 Defaults; 8 explizit, 6 geerbt)

**Auch im aktiven Profil:** `GenerateMaptiles`, `FastMapAsyncStartup`, `FastMapStartupDelaySeconds`, `MapImageModule`, `DrawPrimOnMapTile`, `TextureOnMapTile`, `TexturePrims`, `RenderMeshes`

**Nicht lokal ueberschrieben:** `TexturePrimSize`, `LunaTextureEnabled`, `LunaTextureRasterFallback`, `LunaTexturePlaceholder`, `LunaTextureDiagnosticLimit`, `LunaTextureDiagnosticStore`

### `[Permissions]` (4 Defaults; 3 explizit, 1 geerbt)

**Auch im aktiven Profil:** `allow_grid_gods`, `automatic_gods`, `implicit_gods`

**Nicht lokal ueberschrieben:** `serverside_object_permissions`

### `[RegionReady]` (3 Defaults; 3 explizit, 0 geerbt)

**Auch im aktiven Profil:** `enabled`, `channel_notify`, `login_disable`

**Nicht lokal ueberschrieben:** —

### `[EstateManagement]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `AllowRegionRestartFromClient`

**Nicht lokal ueberschrieben:** —

### `[UserProfiles]` (0 Defaults; 0 explizit, 0 geerbt)

**Nicht lokal ueberschrieben:** —

### `[SMTP]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `enabled`

**Nicht lokal ueberschrieben:** —

### `[Network]` (11 Defaults; 2 explizit, 9 geerbt)

**Auch im aktiven Profil:** `http_listener_port`, `shard`

**Nicht lokal ueberschrieben:** `ConsoleUser`, `ConsolePass`, `console_port`, `http_listener_ssl`, `http_listener_sslport`, `http_listener_cn`, `http_listener_cert_path`, `http_listener_cert_pass`, `OutboundDisallowForUserScripts`

### `[ScriptsHttpRequestModule]` (0 Defaults; 0 explizit, 0 geerbt)

**Nicht lokal ueberschrieben:** —

### `[AccessControl]` (0 Defaults; 0 explizit, 0 geerbt)

**Nicht lokal ueberschrieben:** —

### `[ClientStack.LindenUDP]` (0 Defaults; 0 explizit, 0 geerbt)

**Nicht lokal ueberschrieben:** —

### `[ClientStack.LindenCaps]` (68 Defaults; 5 explizit, 63 geerbt)

**Auch im aktiven Profil:** `Cap_GetDisplayNames`, `Cap_GetTexture`, `Cap_GetMesh`, `Cap_SetDisplayName`, `Cap_AvatarPickerSearch`

**Nicht lokal ueberschrieben:** `Cap_AttachmentResources`, `Cap_ChatSessionRequest`, `Cap_CopyInventoryFromNotecard`, `Cap_DispatchRegionInfo`, `Cap_EstateAccess`, `Cap_EnvironmentSettings`, `Cap_EventQueueGet`, `Cap_FetchInventory`, `Cap_ObjectMedia`, `Cap_ObjectMediaNavigate`, `Cap_FetchLib`, `Cap_FetchLibDescendents`, `Cap_GetMesh2`, `Cap_GetAsset`, `Cap_GetObjectCost`, `Cap_GetObjectPhysicsData`, `Cap_GroupProposalBallot`, `Cap_GroupMemberData`, `Cap_HomeLocation`, `Cap_LandResources`, `Cap_MapLayer`, `Cap_MapLayerGod`, `Cap_NewFileAgentInventory`, `Cap_NewFileAgentInventoryVariablePrice`, `Cap_ObjectAdd`, `Cap_ParcelPropertiesUpdate`, `Cap_ParcelMediaURLFilterList`, `Cap_ParcelNavigateMedia`, `Cap_ParcelVoiceInfoRequest`, `Cap_ProductInfoRequest`, `Cap_ProvisionVoiceAccountRequest`, `Cap_RemoteParcelRequest`, `Cap_RequestTextureDownload`, `Cap_SearchStatRequest`, `Cap_SearchStatTracking`, `Cap_SendPostcard`, `Cap_SendUserReport`, `Cap_SendUserReportWithScreenshot`, `Cap_ServerReleaseNotes`, `Cap_SimConsole`, `Cap_SimulatorFeatures`, `Cap_StartGroupProposal`, `Cap_TextureStats`, `Cap_UntrustedSimulatorMessage`, `Cap_UpdateAgentInformation`, `Cap_UpdateAgentLanguage`, `Cap_UpdateGestureAgentInventory`, `Cap_UpdateNotecardAgentInventory`, `Cap_UpdateScriptAgent`, `Cap_UpdateGestureTaskInventory`, `Cap_UpdateNotecardTaskInventory`, `Cap_UpdateScriptTask`, `Cap_InventoryThumbnailUpload`, `InventoryThumbnailUploadMaxBytes`, `Cap_UploadBakedTexture`, `Cap_UploadObjectAsset`, `Cap_ViewerStartAuction`, `Cap_ViewerStats`, `Cap_WebFetchInventoryDescendents`, `Cap_FetchInventoryDescendents2`, `Cap_FetchInventory2`, `Cap_FetchLib2`, `Cap_FetchLibDescendents2`

### `[Chat]` (4 Defaults; 4 explizit, 0 geerbt)

**Auch im aktiven Profil:** `enabled`, `whisper_distance`, `say_distance`, `shout_distance`

**Nicht lokal ueberschrieben:** —

### `[EntityTransfer]` (5 Defaults; 5 explizit, 0 geerbt)

**Auch im aktiven Profil:** `AllowAvatarCrossing`, `DisableObjectTransfer`, `LevelHGTeleport`, `DisableInterRegionTeleportCancellation`, `LandingPointBehavior`

**Nicht lokal ueberschrieben:** —

### `[Messaging]` (1 Defaults; 0 explizit, 1 geerbt)

**Nicht lokal ueberschrieben:** `InstantMessageModule`

### `[Inventory]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `CoalesceMultipleObjectsToInventory`

**Nicht lokal ueberschrieben:** —

### `[Appearance]` (6 Defaults; 6 explizit, 0 geerbt)

**Auch im aktiven Profil:** `PersistBakedTextures`, `DelayBeforeAppearanceSave`, `DelayBeforeAppearanceSend`, `WearablesRequestDelayMs`, `ResendAppearanceUpdates`, `ReuseTextures`

**Nicht lokal ueberschrieben:** —

### `[Attachments]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `Enabled`

**Nicht lokal ueberschrieben:** —

### `[Mesh]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `AllowMeshUpload`

**Nicht lokal ueberschrieben:** —

### `[Textures]` (4 Defaults; 2 explizit, 2 geerbt)

**Auch im aktiven Profil:** `DynamicTexturePreviousRetentionSeconds`, `DynamicTexturePreviousRetentionLimit`

**Nicht lokal ueberschrieben:** `ReuseDynamicTextures`, `ReuseDynamicLowDataTextures`

### `[ODEPhysicsSettings]` (28 Defaults; 0 explizit, 28 geerbt)

**Nicht lokal ueberschrieben:** `collect_stats`, `world_gravityx`, `world_gravityy`, `world_gravityz`, `avatar_terminal_velocity`, `world_stepsize`, `world_solver_iterations`, `filter_collisions`, `nm_terraincontact_friction`, `nm_terraincontact_bounce`, `nm_terraincontact_erp`, `m_terraincontact_friction`, `m_terraincontact_bounce`, `m_terrainContact_erp`, `m_avatarobjectcontact_friction`, `m_avatarobjectcontact_bounce`, `objectcontact_friction`, `objectcontact_bounce`, `av_density`, `av_movement_divisor_walk`, `av_movement_divisor_run`, `geometry_default_density`, `body_frames_auto_disable`, `geom_updates_before_throttled_update`, `maximum_mass_object`, `mesh_sculpted_prim`, `mesh_lod`, `mesh_physical_lod`

### `[BulletSim]` (20 Defaults; 1 explizit, 19 geerbt)

**Auch im aktiven Profil:** `AvatarToAvatarCollisionsByDefault`

**Nicht lokal ueberschrieben:** `BulletEngine`, `UseSeparatePhysicsThread`, `TerrainImplementation`, `TerrainMeshMagnification`, `AvatarHeightLowFudge`, `AvatarHeightMidFudge`, `AvatarHeightHighFudge`, `AvatarTerminalVelocity`, `LinkImplementation`, `LinksetOffsetCenterOfMass`, `MeshSculptedPrim`, `ForceSimplePrimMeshing`, `ShouldRemoveZeroWidthTriangles`, `ShouldUseAssetHulls`, `MaxCollisionsPerFrame`, `MaxUpdatesPerFrame`, `PhysicsLoggingEnabled`, `PhysicsLoggingDir`, `VehicleLoggingEnabled`

### `[Wind]` (4 Defaults; 4 explizit, 0 geerbt)

**Auch im aktiven Profil:** `enabled`, `wind_update_rate`, `wind_plugin`, `strength`

**Nicht lokal ueberschrieben:** —

### `[Trees]` (4 Defaults; 0 explizit, 4 geerbt)

**Nicht lokal ueberschrieben:** `enabled`, `active_trees`, `update_rate`, `allowGrow`

### `[VectorRender]` (0 Defaults; 0 explizit, 0 geerbt)

**Nicht lokal ueberschrieben:** —

### `[LL-Functions]` (52 Defaults; 0 explizit, 52 geerbt)

**Nicht lokal ueberschrieben:** `AllowGodFunctions`, `RestrictEmail`, `max_listens_per_region`, `max_listens_per_script`, `max_external_urls_per_simulator`, `UseSimpleBoxesInGetBoundingBox`, `AddStatsInGetBoundingBox`, `LowerAvatarBoundingBoxStandingXconst`, `UpperAvatarBoundingBoxStandingXconst`, `LowerAvatarBoundingBoxStandingYconst`, `UpperAvatarBoundingBoxStandingYconst`, `LowerAvatarBoundingBoxStandingZconst`, `LowerAvatarBoundingBoxStandingZcoeff`, `UpperAvatarBoundingBoxStandingZconst`, `UpperAvatarBoundingBoxStandingZcoeff`, `LowerAvatarBoundingBoxGroundsittingXconst`, `UpperAvatarBoundingBoxGroundsittingXconst`, `LowerAvatarBoundingBoxGroundsittingYconst`, `UpperAvatarBoundingBoxGroundsittingYconst`, `LowerAvatarBoundingBoxGroundsittingZconst`, `LowerAvatarBoundingBoxGroundsittingZcoeff`, `UpperAvatarBoundingBoxGroundsittingZconst`, `UpperAvatarBoundingBoxGroundsittingZcoeff`, `LowerAvatarBoundingBoxSittingXconst`, `UpperAvatarBoundingBoxSittingXconst`, `LowerAvatarBoundingBoxSittingYconst`, `UpperAvatarBoundingBoxSittingYconst`, `LowerAvatarBoundingBoxSittingZconst`, `LowerAvatarBoundingBoxSittingZcoeff`, `UpperAvatarBoundingBoxSittingZconst`, `UpperAvatarBoundingBoxSittingZcoeff`, `PrimBoundingBoxSafetyCoefficientX`, `PrimBoundingBoxSafetyCoefficientY`, `PrimBoundingBoxSafetyCoefficientZ`, `UseLlCastRayV3`, `FloatToleranceInLlCastRay`, `FloatTolerance2InLlCastRay`, `PrimDetailLevelInLlCastRay`, `SculptDetailLevelInLlCastRay`, `MeshDetailLevelInLlCastRay`, `AvatarDetailLevelInLlCastRay`, `MaxHitsInLlCastRay`, `MaxHitsPerPrimInLlCastRay`, `MaxHitsPerObjectInLlCastRay`, `DetectExitHitsInLlCastRay`, `DoAttachmentsInLlCastRay`, `ThrottleTimeInMsInLlCastRay`, `AvailableTimeInMsPerRegionInLlCastRay`, `AvailableTimeInMsPerAvatarInLlCastRay`, `RequiredAvailableTimeInMsInLlCastRay`, `MaximumAvailableTimeInMsInLlCastRay`, `UseMeshCacheInLlCastRay`

### `[DataSnapshot]` (5 Defaults; 5 explizit, 0 geerbt)

**Auch im aktiven Profil:** `index_sims`, `data_exposure`, `gridname`, `default_snapshot_period`, `snapshot_cache_directory`

**Nicht lokal ueberschrieben:** —

### `[Economy]` (0 Defaults; 0 explizit, 0 geerbt)

**Nicht lokal ueberschrieben:** —

### `[YEngine]` (3 Defaults; 3 explizit, 0 geerbt)

**Auch im aktiven Profil:** `Enabled`, `UseSourceHashCode`, `MinTimerInterval`

**Nicht lokal ueberschrieben:** —

### `[XEngine]` (15 Defaults; 0 explizit, 15 geerbt)

**Nicht lokal ueberschrieben:** `Enabled`, `MinThreads`, `MaxThreads`, `IdleTimeout`, `Priority`, `MaxScriptEventQueue`, `ThreadStackSize`, `ScriptStopStrategy`, `WriteScriptSourceToDebugFile`, `CompileWithDebugInformation`, `SaveInterval`, `MaintenanceInterval`, `EventLimit`, `KillTimedOutScripts`, `WaitForEventCompletionOnScriptStop`

### `[Concierge]` (4 Defaults; 0 explizit, 4 geerbt)

**Nicht lokal ueberschrieben:** `enabled`, `whoami`, `password`, `regions`

### `[Hypergrid]` (1 Defaults; 0 explizit, 1 geerbt)

**Nicht lokal ueberschrieben:** `safemode`

### `[Groups]` (6 Defaults; 3 explizit, 3 geerbt)

**Auch im aktiven Profil:** `Enabled`, `Module`, `MessageOnlineUsersOnly`

**Nicht lokal ueberschrieben:** `DebugEnabled`, `DebugMessagingEnabled`, `GroupsCacheTimeout`

### `[PacketPool]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `RecycleBaseUDPPackets`

**Nicht lokal ueberschrieben:** —

### `[InterestManagement]` (11 Defaults; 0 explizit, 11 geerbt)

**Nicht lokal ueberschrieben:** `UpdatePrioritizationScheme`, `ReprioritizationEnabled`, `ReprioritizationInterval`, `RootReprioritizationDistance`, `ChildReprioritizationDistance`, `ObjectsCullingByDistance`, `RootTerseUpdatePeriod`, `ChildTerseUpdatePeriod`, `RootPositionUpdateTolerance`, `RootRotationUpdateTolerance`, `RootVelocityUpdateTolerance`

### `[Monitoring]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `Enabled`

**Nicht lokal ueberschrieben:** —

### `[Statistics]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `NumberOfFrames`

**Nicht lokal ueberschrieben:** —

### `[MediaOnAPrim]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `Enabled`

**Nicht lokal ueberschrieben:** —

### `[NPC]` (0 Defaults; 0 explizit, 0 geerbt)

**Nicht lokal ueberschrieben:** —

### `[Terrain]` (3 Defaults; 3 explizit, 0 geerbt)

**Auch im aktiven Profil:** `InitialTerrain`, `SendTerrainUpdatesByViewDistance`, `OglTerrainBrushStrengthMultiplier`

**Nicht lokal ueberschrieben:** —

### `[LandManagement]` (3 Defaults; 3 explizit, 0 geerbt)

**Auch im aktiven Profil:** `LimitParcelLayerUpdateDistance`, `ParcelLayerViewDistance`, `ShowParcelBansLines`

**Nicht lokal ueberschrieben:** —

### `[JsonStore]` (3 Defaults; 3 explizit, 0 geerbt)

**Auch im aktiven Profil:** `Enabled`, `EnableObjectStore`, `MaxStringSpace`

**Nicht lokal ueberschrieben:** —

### `[AssetService]` (3 Defaults; 0 explizit, 3 geerbt)

**Nicht lokal ueberschrieben:** `DefaultAssetLoader`, `AssetLoaderArgs`, `AssetLoaderEnabled`

### `[GridService]` (1 Defaults; 0 explizit, 1 geerbt)

**Nicht lokal ueberschrieben:** `StorageProvider`

### `[AutoBackupModule]` (1 Defaults; 0 explizit, 1 geerbt)

**Nicht lokal ueberschrieben:** `AutoBackupModuleEnabled`

### `[Sounds]` (2 Defaults; 0 explizit, 2 geerbt)

**Nicht lokal ueberschrieben:** `Module`, `MaxDistance`

### `[ServiceThrottle]` (1 Defaults; 0 explizit, 1 geerbt)

**Nicht lokal ueberschrieben:** `Interval`

### `[Dwell]` (1 Defaults; 0 explizit, 1 geerbt)

**Nicht lokal ueberschrieben:** `DwellModule`

### `[ServerReleaseNotes]` (1 Defaults; 0 explizit, 1 geerbt)

**Nicht lokal ueberschrieben:** `ServerReleaseNotesURL`

### `[Modules]` (1 Defaults; 0 explizit, 1 geerbt)

**Nicht lokal ueberschrieben:** `Include-modules`

### `[XMLRPC]` (2 Defaults; 2 explizit, 0 geerbt)

**Auch im aktiven Profil:** `XmlRpcRouterModule`, `XmlRpcPort`

**Nicht lokal ueberschrieben:** —

### `[OGLShutdown]` (1 Defaults; 1 explizit, 0 geerbt)

**Auch im aktiven Profil:** `RegionCloseTimeoutSeconds`

**Nicht lokal ueberschrieben:** —

## Nachweis / Test

- `python3 tools/ci/verify_g1_config_contract.py`
- `python3 tools/ci/verify_opensim_autoports.py`
- `dotnet build OpenSim.sln --configuration Release` plus bestehende NexVerse, Linux x64, Windows x64 und ARM64 Actions
- Nach dem Einspielen: ROBUST-Login und GridInfo, lokale/HG-Anmeldung, Offline-IM-Mail, Mesh-Upload, Estate-Rechte, Scripts, WebDAV-IAR-Restore und dynamische NodeAgent-Meldungen pruefen.
- Secrets und produktive Daten vor einem Update sichern; es findet **keine** automatische Veraenderung auf dem laufenden Simulator statt.
