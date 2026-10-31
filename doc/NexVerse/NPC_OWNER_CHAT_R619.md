# R620 – NPC-Chatarchivierung in der bestehenden MariaDB

Status: Im Stadt-NexVerse Generation-1-Repository sind die NPC-Konfigurationsschalter in `bin/Robust.HG.ini` und `bin/OpenSim.ini` eingeschaltet. Die tatsächliche Kommunikation erfordert weiterhin private HMAC-Schlüssel und HTTPS-Endpunkte auf ROBUST und Simulatoren. Ohne Signaturschlüssel bleibt die API gesperrt.

## Abgrenzung und Datenschutz

Nur direkte private Instant Messages (IM) zwischen einem aktiv bestätigten NPC und seinem **eigenen** Avatar werden erfasst. Öffentlicher Region-Chat, Gruppen-IMs, Objekt-/LSL-Chat und Unterhaltungen zwischen NPC und Dritten werden nicht archiviert. Es werden keine Bürger-IM-Archive als NPC-Chats umgedeutet. Speicherung nur nach **expliziter persönlicher Einwilligung**, nicht per Admin-Key oder fremdem Owner-Parameter.

- Default: keine Speicherung; ohne Einwilligung wird der signierte IM-Ingest mit 403 abgewiesen.
- Abschalten löscht **sämtliche** bisher gespeicherten NPC-IMs dieses Eigentümers, während die Einwilligung deaktiviert wird.
- Separater Lösch-Endpunkt entfernt alle archivierten NPC-IMs des Besitzers, ohne die Einwilligung zu ändern.
- Speicherung ausschließlich in der bereits verwendeten OpenGenesisLINK-MariaDB. Die neuen InnoDB-Tabellen `npc_chat_consent` und `npc_chat_messages` sind logisch von den OpenSim- und Bürger-IM-Tabellen getrennt. **Keine SQLite-Dateien, kein SQLite-Fallback und keine zweite Datenbank.**
- Standard-Aufbewahrung 30 Tage, konfigurierbar 1–90 Tage; maximal 10.000 Einträge pro Eigentümer, 4 KiB pro Nachricht.
- Die Nachrichten sind **best effort**: Simulator-zu-World wird in einem flüchtigen, begrenzten Speicherpuffer mit maximal drei Zustellversuchen übertragen, nicht dauerhaft lokal gepuffert. Der API-Verlauf setzt deshalb `complete=false` und `capture_mode=best_effort_owner_npc_direct_im`. Nach einer Unterbrechung kann ein Teil fehlen. Es gibt **keine** rückwirkende Rekonstruktion.

## World/ROBUST – Beispiel

```ini
[NexNPCPortal]
Enabled = true
ExpectedRegionCount = 0

[NexNPCChat]
Enabled = true
RetentionDays = 30
```

`NexNpcChatStore` erbt ausschließlich `StorageProvider` und `ConnectionString` aus der bereits konfigurierten ROBUST-Sektion `[DatabaseService]`. Es werden die separaten Tabellen `npc_chat_consent` und `npc_chat_messages` in dieser MariaDB automatisch angelegt. Falls `[DatabaseService]` nicht auf MariaDB/MySQL zeigt oder die Verbindung nicht funktioniert, bleibt das NPC-Chatarchiv deaktiviert; kein SQLite-Fallback. DB-Zugangsdaten bleiben in der bestehenden gesicherten Konfiguration.

`ExpectedRegionCount = 0` ermittelt die aktiven Regionen automatisch aus der durch signierte NexBus-/NodeAgent-Ereignisse aufgebauten Node-Registry. Für jede dort aktive Region wird ein frischer, signierter NPC-Snapshot verlangt. Das Hinzufügen oder Entfernen einer aktiven Region erfordert somit keine INI-Änderung. Ohne aktuelle NodeAgent-Regionen oder bei fehlenden Snapshots antwortet die API mit 503 statt einer Teilmenge. Ein fester Wert zwischen 1 und 1024 bleibt für Installationen ohne dynamische NodeAgent-Ermittlung möglich, ist für Stadt NexVerse aber nicht voreingestellt.

World-Prozessumgebung (Secret selbst niemals in Git, INI oder Logs):

```text
NEX_NPC_INGEST_SECRET=<separates zufälliges 32+ Byte Secret>
NEX_NPC_CHAT_SECRET=<anderes zufälliges 32+ Byte Secret>
```

Das bestehende MariaDB-Datenbankkonto des ROBUST-Prozesses benötigt für die beiden neuen Tabellen `CREATE`, `SELECT`, `INSERT`, `UPDATE` und `DELETE`. Bei der initialen Inbetriebnahme muss das Anlegen der Tabellen erlaubt sein. Benutzer- und NPC-IMs erhalten keine lokalen Datenbankdateien. HMAC-geschützte interne Pfade dürfen **nur** für autorisierte Simulatoren über HTTPS/WireGuard erreichbar sein; der Reverse-Proxy soll diese internen Pfade nicht öffentlich anbieten.

## Simulator – Beispiel

```ini
[NPC]
Enabled = true
NexNPCSnapshotEnabled = true
NexNPCChatEnabled = true
```

```text
NEX_NPC_INGEST_URL=https://<internal-robust-host>/internal/nexnpc/snapshot/v1
NEX_NPC_INGEST_SECRET=<identischer R617-Secret wie ROBUST>
NEX_NPC_CHAT_INGEST_URL=https://<internal-robust-host>/internal/nexnpc/chat/v1
NEX_NPC_CHAT_SECRET=<identischer NPC-Chat-Secret wie ROBUST>
```

HTTPS muss mit dem vertrauenswürdigen Zertifikat funktionieren. Kein TLS-Bypass. Beide Dienste kontrolliert neu starten, nachdem Build/CI vollständig erfolgreich waren. Der Simulator sendet private Owner/NPC-IM-Ereignisse, er speichert sie nicht ohne serverseitige Einwilligung.

## Persönliche Endpunkte auf world.stadt-nexverse.de

Verlangen einen authentifizierten **lokalen** aktiven Einwohner samt persönlichem Bearer-Token. Service-Key, Admin-Key, freie Owner-UUID und NPC-UUID allein sind keine Eigentümerautorisierung.

| Methode | Route | Berechtigung |
| --- | --- | --- |
| GET | /api/v1/npcs/mine | relationships:read |
| GET | /api/v1/npcs/chat/settings | relationships:read |
| PUT | /api/v1/npcs/chat/settings | relationships:write |
| DELETE | /api/v1/npcs/chat/history | relationships:write |
| GET | /api/v1/npcs/{npcId}/messages?cursor=... | relationships:read |

Erst die **bewusste** Aktivierung per `PUT /api/v1/npcs/chat/settings` mit diesem JSON führt zur Aufzeichnung nachfolgender Owner/NPC-Direkt-IMs:

```json
{"enabled":true,"acknowledge_private_owner_npc_im":true}
```

Abschalten (löscht außerdem die eigene Historie):

```json
{"enabled":false}
```

Das Lesen eigener NPC-IMs setzt weiterhin `GET /api/v1/npcs/mine` mit bestätigter Eigentümerzuordnung aus R617 voraus. Die chronologische Liste liefert `npc_id`, `owner_principal_id`, `messages` mit `message_id`, `npc_id`, `sender` (`npc`/`owner`), `text`, `sent_at` (RFC3339), `next_cursor` sowie `complete=false`. `cursor` ist die letzte erhaltene Nachrichten-Sequenz. Bei Ingest- oder Speicherfehlern wird `503 npc_chat_archive_unavailable` zurückgegeben, nicht eine leere Erfolgsliste. Bei fehlender Einwilligung: `403 npc_chat_opt_in_required`.

## Grenzen / Rollout-Gates

Diese Version umfasst **keine** vollständige persistente Zustellgarantie und keinen Zugriff auf Gespräche Dritter. Das Portal muss Nutzer ausdrücklich auf den Best-Effort-Status und die begrenzte 30-Tage-Aufbewahrung hinweisen. Für garantiert vollständige Archive wäre künftig ein zustellbestätigter persistenter Outbox-Mechanismus **nach einer vorab synchronisierten Consent-Prüfung** erforderlich.

Vor produktivem Betrieb: `NEX_NPC_INGEST_SECRET` und `NEX_NPC_CHAT_SECRET` als **zwei unabhängige** 32+-Byte-Schlüssel in der Prozessumgebung von ROBUST und den Simulatoren bereitstellen. Auf den Simulatoren zusätzlich die beiden `NEX_NPC_*_INGEST_URL` als vertrauenswürdige interne HTTPS-Ziele setzen. Diese Secrets und Ziele dürfen nicht als geheime Werte im öffentlichen GitHub abgelegt werden. Nach einem Code-Update ROBUST und die Simulatoren kontrolliert neu starten. Zusätzlich vollständigen Release-Build, R620-MariaDB-Test, gültige NodeAgent-/NPC-Snapshots und die Internal-API-Firewall prüfen. Ohne diese Nachweise **nicht** von produktiver Freigabe sprechen.
