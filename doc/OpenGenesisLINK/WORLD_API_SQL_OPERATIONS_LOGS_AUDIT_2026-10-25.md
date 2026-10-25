# OpenGenesisLINK – Betreiber-SQL, World-API und Tabellen-Lifecycle

Stand: 25.10.2026. Analyse der vom Betreiber bereitgestellten `nexverse_daten.sql` (**Datenexport; kein Schemasnapshot, keine CREATE TABLE-Befehle**) und Quervergleich gegen den aktuellen Code in `opengenesislink/opengenesislinkserver`.

## Datengrundlage und Grenzen

Der HeidiSQL-Export enthält **63 benannte Tabellen**, davon **44 mit INSERT-Anweisungen** und **19 mit ~0 Zeilen**. Die Angaben `~… rows` stammen aus Exportkommentaren und sind ungefähre Datensatzmengen. Die Anzahl der `INSERT INTO`-Zeilen ist durch Batch-Inserts nicht gleich der Zeilenanzahl. Der Export enthält personenbezogene Daten, Authentifizierungshashes und Inventar-/Assetdaten; deshalb werden **keine SQL-Nutzdaten** ins Repository übernommen und keine Benutzer-/Geheimdaten zitiert.

Repräsentative Tabellen im Dump (ungefähre Zeilen): `fsassets` 336.728; `primshapes` 11.178; `prims` 10.920; `inventoryitems` 10.279; `primitems` 3.660; `Avatars` 2.669; `inventoryfolders` 1.152; `portal_im_entries` 460; `GridUser` 233; `land` 159; `regionsettings`, `terrain`, `bakedterrain` und `estate_map` jeweils ca. 150; `regions` 49; `UserAccounts` 16. Für Tabellen ohne Datensätze sind Spaltendefinitionen im Datenexport **nicht enthalten**; die tatsächliche DDL ist mit `SHOW CREATE TABLE` zu verifizieren.

## World-API-Potenzial (keine direkt freigegebenen SQL-Endpunkte)

| Tabellen/Daten | Naheliegende API-Verwendung | Status/Schutz |
|---|---|---|
| `UserAccounts`, `auth`, `GridUser` | Benutzer anlegen, suchen, aktualisieren, sperren/deaktivieren, Online-/Home-Region | **Bestehende** `GET/POST /api/v1/users`, `GET/PATCH/DELETE /api/v1/users/{principalId}`; Authentifizierung nur über Account-Services, niemals Hash-/Salt-Ausgabe |
| `Friends`, `Avatars`, `userprofile`, `userdata` | Anzeigenamen, soziale Beziehungen, Profilbilder und Sichtbarkeit | Profile/SocialGraph über vorhandene Dienste; Privacy und Owner/Administrator-Rechte |
| `regions`, `regionsettings`, `regionextra`, `terrain`, `bakedterrain`, `spawn_points` | Grid/Regionenstatus, Estate-Einstellungen, Terrain- und Spawnverwaltung | API via bestehendem Region-/Grid-/Estate-Service; keine direkten DELETE-Tabellenoperationen |
| `prims`, `primshapes`, `primitems`, `land` | Objekt-/Parzellen-Statistiken, begrenzte Regionen-/Land-Mutationen | Regionseigentum, Simulator-Zustand und Linkset-Integrität beachten; keine frei bearbeitbaren Rohdaten-REST-Tabellen |
| `inventoryitems`, `inventoryfolders`, `fsassets` | Inventarstruktur, Asset-Vorschaubilder, Medientyp, Speicherstatistik | Bestehende Inventar- und Media-API; Kopier-/Transfer-/Owner-Rechte und AssetService-Weg strikt einhalten |
| `os_groups_*` | Gruppen, Rollen, Einladungen, Notices, Länder-/Estate-Rechte | Bestehende NexGroups API; über Dienste und GroupPowers, nicht SQL-Manipulation |
| `ogl_ledger_*`, `ogl_economy_*` | Konten, Ledger, Zahlungen, Handel und Land-Mietverträge | Bestehende BankingCommerce-/Ledger-Services; Transaktionen nur mit Double-Entry, Idempotenz und Audit |
| `portal_im_entries` | Portalnachrichten und Zustellprotokoll | Eigene IM-/Portal-Dienste und Privatsphäre; kein globaler öffentlich lesbarer Nachrichtenfeed |
| `ogl_password_recovery` | Reset-Challenges / Passwortänderung | Bestehende `/api/v1/auth/password-recovery`-Routen; niemals Tokens exportieren oder Roh-DELETE anbieten |

Neue generische `GET/PUT/POST/DELETE /table/{name}`-Endpunkte werden **nicht** implementiert: Das würde Gruppenrechte, Prim-Referenzen, Inventory-Permissions, atomare Buchungen und OpenSim-Konsistenz verletzen. Das neue Admin-Center kann alle bestehenden OpenAPI-Operationen unmittelbar mit signiertem Admin-Bearer und expliziter Bestätigung ausführen, inklusive Einwohneranlage, Soft-Delete, gruppen-/regionbezogenen Aktionen und Asset-Operationen, soweit diese serverseitig tatsächlich existieren.

## Die 19 im Export leeren Tabellen

**Nicht löschen – weiterhin im Quellcode/Migrationen referenziert:**

- `classifieds`: Profile/Search/Discovery und UserProfiles-Migrationen
- `estateban`, `estate_groups`, `estate_users`: Estate-Zugriffslisten/Bans; MySQL/PGSQL/SQLite EstateStore-Migrationen
- `hg_traveling_data`: Hypergrid-TravelService / HGTravelStore-Migrationen
- `MuteList`: MuteListService-/Store-Implementierungen
- `ogl_economy_commerce_orders`, `ogl_economy_land_leases`, `ogl_economy_land_listings`, `ogl_economy_payment_requests`, `ogl_economy_transfer_policies`, `ogl_ledger_account_events`: `NexLedgerSqlStore` und Economy-CI-Regressionspfade
- `ogl_password_recovery`: `NexPasswordRecoveryStore` (0 offene Challenges ist erwartbar)
- `os_groups_notices`: Gruppen-Notices und Gruppenmigration
- `regionban`, `regionextra`, `regionwindlight`, `spawn_points`: RegionStore-/Simulator-Migrationen, Terrain/Environment/Regionzustand
- `userdata`: UserProfilesStore und Benutzer-/Profilkompatibilität

**Ergebnis:** Aus diesem Export lässt sich **keine** Tabelle zweifelsfrei als totes Schema beweisen. Leer bedeutet „zum Zeitpunkt des Dumps nicht benutzt“, **nicht** „von Code und Migrationen ungenutzt“. Daher keine DROP TABLE-Anweisungen und kein riskantes Entfernen aus den Schema-Migrationen.

## Nächster sicherer Datenbankabgleich

Vor jeder späteren Schema-Entfernung vollständigen DDL-/Laufzeitabgleich erstellen, zum Beispiel in einer **isolierten Staging-DB**:

```sql
SELECT TABLE_NAME, TABLE_ROWS, CREATE_TIME, UPDATE_TIME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
ORDER BY TABLE_NAME;

SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_KEY
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
ORDER BY TABLE_NAME, ORDINAL_POSITION;
```

Dazu pro Tabelle Schreib-/Leseaufrufe, Migrationserzeugung für MySQL/PGSQL/SQLite, Konfigurations-Featureflags, Provider, Hypergrid- und Viewerkompatibilität prüfen; ausschließlich gesicherte, explizit freigegebene Migrationen verwenden.

## Ergänzte Operator-Funktionen

- Geschützter Tageslog-Endpunkt `GET /api/v1/admin/logs`; einzelne Logzeilen `GET /api/v1/admin/logs/{Robust|OpenSim}/{yyyy-MM-dd}/entries?limit=500&offset=0` ausschließlich für echte lokale Administratoren mit UserLevel >= 200.
- Log4net schreibt pro Tag `bin/logs/Robust_dd_MM_yyyy.log` und `bin/logs/OpenSim_dd_MM_yyyy.log`. Auch vorhandene Altlogs im früheren `bin/*.log` müssen bei Bedarf gesondert archiviert werden.
- **Fremde Nodes:** `NexVerseNodeAgent` sendet bis zu 32-KiB-Chunks signiert über denselben NexBus-HMAC-/HTTPS-Endpunkt. ROBUST quittiert erst nach Prüfen/Speichern; ein verlorener HTTP-Status führt zur byte-identischen Wiederholung. Der Dateiname enthält die vertraute `NodeId`: `bin/logs/OpenSim_<NodeId>_dd_MM_yyyy.log`. Keine Auslieferung dieser Daten an andere NexBus-Peers, keine frei zugängliche Log-URL.
- Im Web-Dashboard: ausklappbare Tage für lokalen ROBUST sowie lokale/verbundene OpenSim-Nodes, Filter, Pagination und vorhandener persistenter Admin-Auditfeed `GET /api/v1/audit`.
- Für fremde Server ist `LogForwardingEnabled=true` Standard im Beispieldokument; eine **HTTPS**-NexBus-Gegenstelle und sicher formatierte `NodeId` sind zwingend. Logs können vertrauliche Nutzerdaten enthalten; Zugriff nur für Betreiber, Rotations-/Retention-Policy organisatorisch festlegen.

**Produktionsabnahme:** Sicherung von MariaDB und bisherigen Logs, Update von ROBUST **und jeder beteiligten Simulator-Node**, überprüfter TLS-/NexBus-Rückkanal, Auth-/Log-Tests, Fehlerszenarien (Abbruch, Wiederholung, Partition, NodeId-Manipulation), Firestorm-/OpenSim-Login und SQL-Schema-Abgleich. Ein Merge ist keine Live-Installation.
