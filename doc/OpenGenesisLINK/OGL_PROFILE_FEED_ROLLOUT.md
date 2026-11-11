# OpenGenesisLINK Generation 1 - Feed und NV$ Rollout

Status: **kontrolliertes Betreiber-Deployment**. Diese Anleitung betrifft den
in `main` gemergten Feed v1 sowie die NV$-Gebuehrenfixes #432/#434.
CI-Green ist keine bestaetigte Produktivabnahme. Vor Aktivierung auf einem
getrennten MariaDB-/Simulator-Staging die Schritte abarbeiten.

## 1. Was tatsaechlich funktioniert

- NV$-Preflight fuer kostenpflichtige Classifieds, Gruppen und Uploads:
  fehlende Systemgebuehrenwallet wird **vor** dem Erzeugen erkannt.
  Ledger-Timeout nach externer Mutation bleibt ein zu pruefender
  Abstimmungsfall, keine magische Exactly-once-Transaktion.
- OGL Profilfeed v2: Posts, Kommentare, Likes und Folgen per
  OAuth-geschuetzter World-API; direkt bedienbares responsives
  Stadt-NexVerse-Webprofil bei erfolgreichem PKCE-Login.
- Feed aktiviert nur auf **derselben MariaDB** wie der lokale
  UserAccountService. Schreibvorgaenge sperren die Accountzeile;
  Selbstloeschung und administrative Kernloeschung entfernen eigene
  Feed-Daten in derselben DB-Transaktion.
- Feed-Aufbewahrung: konfigurierbar zwischen 7 und 3650 Tagen,
  Standard **180 Tage** fuer Posts/Kommentare. Nach Ablauf keine
  Auslieferung, stuendliche bereinigende Pruefung per Timer
  (auch ohne Profilabrufe) und bei Zugriffen.
  Likes von abgelaufenen Posts werden kaskadierend geloescht;
  Follows bestehen bis zum Entfolgen/Kontoloeschen.

**Nicht behaupten:** nativer editierbarer TrixieStorm-Feed-Tab,
Snapshot-Upload, Push-Meldungen, Timeline oder weltweite HG-Foederation.
Die unveraenderte Firestorm-Webansicht ist lesend; Schreiben erfolgt ueber
das authentifizierte Portal bzw. die World-API. Viewer-Ticket:
https://github.com/opengenesislink/trixiestormviewer/issues/2

## 2. Datensicherung und Voraussetzungen

Vor jedem ersten Umschalten:

1. Aktuellen Git-Commit, Region-/ROBUST-Konfiguration, laufende
   .NET-10-Version und Healthchecks dokumentieren.
2. Konsistentes MariaDB-Backup samt Schema und Transaktionslogs erstellen,
   **Wiederherstellung** auf Staging pruefen. SQL-Backup niemals ins Git legen.
3. Schluessel/Passwoerter nur als Umgebungsvariablen oder geschuetzte
   lokale Konfig bereitstellen; keine API-Keys und keine Produktionsdaten
   in CI-Logs, Shell-History oder Chat-Nachrichten kopieren.
4. `[UserAccountService]` und `[UserProfilesService]` muessen denselben
   **StorageProvider und ConnectionString** verwenden, auf MariaDB/MySQL.
   Streng gleiche Schreibweise verwenden: abweichende DSN wird absichtlich
   **abgelehnt**. Beide Dienste muessen die bestehenden lokalen Avatare
   aus demselben UserAccounts-Schema lesen.
5. Sicherstellen, dass bestehende `ogl_profile_feed_*`-Tabellen entweder
   vollstaendig vorhanden oder vollstaendig abwesend sind. Teilinstallationen
   verhindern Loeschvorgaenge. Ungepruefte Fremddatenbanken nicht mischen.
6. Vor einem positiven NV$-Gebuehrentest **unbezahlte Classifieds/Groups**
   aus vorigen Fehlversuchen anhand der World-API-Buchungen abgleichen.
   Keine automatischen Doppelbuchungen.

Vor dem Umschalten die **schreibgeschuetzte** Konfigurationspruefung
auf dem jeweiligen Node ausfuehren:

```bash
python3 tools/release/check_g1_fee_feed_rollout.py --sim bin/OpenSim.ini
python3 tools/release/check_g1_fee_feed_rollout.py --robust bin/Robust.HG.ini
```

Das Tool stellt keine Internetverbindung her, druckt keine Tokens und aendert
keine Dateien. Warnungen muessen betrieblich bewertet werden, ein Fehler
blockiert den Rollout.

## 3. System-Fee-Wallet sicher einrichten

Fuer eine positive Systemgebuehr ist eine existierende **Business-Wallet**
im zentralen NV$-Ledger erforderlich, getrennt von Avatar- und MasterBank-
Payout-Accounts. Die ID muss auf allen betroffenen Simulator-Nodes gleich sein.

Die World API stellt einen autorisierten Provisionierungspfad bereit:
`POST https://world.stadt-nexverse.de/api/v1/economy/accounts/ensure`.
Er akzeptiert **nur** berechtigte Service-/Administratorzugriffe mit
`economy:transfer`, nicht normale Resident-Tokens. Beispiel-JSON:

```json
{
  "account_id": "UUID-EINER-EIGENEN-SYSTEMWALLET",
  "account_class": "business",
  "display_name": "Stadt NexVerse - Systemgebuehren"
}
```

Die UUID vorher sicher mit einem UUIDv4-Generator anlegen, nie ein
existierendes fremdes Avatar-Konto umwidmen. Nach Provisionierung
`GET /api/v1/economy/balance?account_id=<wallet-uuid>`
mit berechtigtem Admin-/Service-Token pruefen. Eine positive Testbuchung
muss im unveraenderlichen Ledger erscheinen. Die Wallet-ID ist nicht geheim,
die Authentifizierungsdaten sind es.

Auf **jedem** Simulator-Node in der effektiven `OpenSim.ini`:

```ini
[NexEconomyViewer]
Enabled = true
WorldApiBaseUrl = "https://world.stadt-nexverse.de"
ApiKey = "${Environment|NEXVERSE_ECONOMY_API_KEY}"
FeeWalletId = "HIER-DIE-ECHTE-SYSTEMWALLET-UUID"
UploadCharge = 0
GroupCreationCharge = 0
```

Die Einstellung ersetzt vorhandene Werte in **einer** Sektion, keine
doppelten INI-Sektionen anlegen. Upload- und Gruppenentgelte koennen
erst nach bestaetigter Buchungsprobe positiv konfiguriert werden.
Ein Classified-Preis kann vom Viewer kommen und muss mit demselben
Gebuehrenwallet-Test bestaetigt werden.

## 4. Feed-Migration und Aktivierung

Zuerst ROBUST im Staging mit neuem Code und bei Bedarf voruebergehend
`EnableProfileFeed=false` starten. Der konfigurierte Standard ist nun
`true`, sofern die gemeinsamen MariaDB-/Loeschbedingungen erfuellt sind.
Live-Healthcheck `/api/v1/health` und aktuelle OAuth-Scopes pruefen.
Optional und empfohlen: Migration
`doc/OpenGenesisLINK/migrations/20261111_ogl_profile_feed.sql` vorab
in dieselbe MariaDB einspielen, gefolgt von
`doc/OpenGenesisLINK/migrations/20261111_ogl_profile_feed_activity.sql`.
Ohne manuellen Import erzeugt der
Feed beim ersten Start mit `CREATE TABLE IF NOT EXISTS` die vier
Basis- und zwei zusaetzliche InnoDB-Aktivitaetstabellen. Dies ist eine **Schema-Migration**, kein bloesser
Schalter. Bei Produktionsbetrieb vorher Backup.

Auf ROBUST im vorhandenen Block:

```ini
[NexVerseWorldApi]
EnableProfileFeed = true
ProfileFeedRetentionDays = 180
ProfileFeedOAuthClientId = ""

[GridInfoService]
web_profile_url = https://world.stadt-nexverse.de/feed?name=[AGENT_NAME]
```

Die zweite Sektion nur bearbeiten, nicht doppelt anlegen. Die HTTPS-
Reverse-Proxy-Route `/feed` und die zugelassenen `/api/v1/feed`-Pfade
muessen auf denselben ROBUST zeigen. Den externen Zugriff auf
Maschinen-Keys sperren. Kein Token in `web_profile_url` oder Querystring.
Bei aktiviertem Feed die Anzeige im Firestorm 7.2.4 und TrixieStorm
(nach Viewer-Anbindung) pruefen; GridInfo kann im Viewer gecacht sein.

## 5. Abnahme / Go-No-Go

**Alle** folgenden Faelle in Staging mit zwei Test-Avataren und
einer dedizierten Fee-Wallet durchspielen:

- Gratis- und kostenpflichtige Classifieds, Gruppen und Uploads;
  bei leerer FeeWalletId **keine** neue unbezahlte Anzeige, kein Crash.
- Salden zu Beginn/Ende, exakt **eine** erklaerbare Gebuehr pro
  erfolgreichem Test; unterbrochene World-API-Buchungen in Ledger
  und Profil-/Gruppenzustand manuell abstimmen.
- Normale Avatar-Transfers und Landkaeufe trotz Systemfee-Einstellung.
- Feed: Eigentuemer erstellt und loescht Post; zwei lokale Avatare
  kommentieren, folgen, liken; fremde HG-UUID, fehlende Scopes,
  gesperrtes Profil und ungueltige IDs werden abgewiesen.
- Moderation: Lokaler Administrator nur mit **UserLevel >= 200 UND
  admin:*** kann in fremden Feeds Posts/Kommentare loeschen. Jeder
  solche Vorgang wird als `feed.post.moderate` bzw.
  `feed.comment.moderate` im bestehenden Audit-Sink protokolliert.
  Einfacher User-Level oder ein fremder Maschinenkey reicht nicht.
  Eine Benutzer-Meldefunktion/Moderationsqueue fehlt weiterhin.
- Privates Profil erscheint nicht anonym. HTML/XSS wird escaped;
  Token/Passwoerter duerfen nicht in Logs und Browser-URLs auftauchen.
- Staging-Kontoloeschung: der geloeschte Avatar hatte eigene Posts,
  Kommentare auf einem fremden Profil, Likes, Followers/Follows.
  Danach in **allen sechs Tabellen** keine Referenz auf diese UUID.
  Post-Fremdkommentare auf dem geloeschten eigenen Feed fallen per
  Fremdschluessel-Kaskade ebenfalls weg.
- Zwei parallele Requests Post-vs-Kontoloeschung: es darf kein
  Post nach geloeschtem UserAccount persistiert sein.
- Retention mit alten Staging-Datensaetzen: nicht mehr sichtbar und
  nach bereinigendem Feed-Aufruf entfernt. Das Backup darf nicht
  wieder ungeprueft alte Beitraege veroeffentlichen.
- MariaDB offline, Reverse-Proxy-Fehler, abgelaufener OAuth-Token:
  kontrollierte Fehler; andere ROBUST-/Simulator-Dienste bleiben nutzbar.

Nur bei allen bestandenen Schritten Rollout freigeben.

## 6. Gestaffelter Rollout

1. **ROBUST**-Build mit neuem `main` auf Staging, Migration und APIs
   validieren. Ein Backup der Produktion muss vorher vorhanden sein.
2. Eine Test-Simulatornode mit dem identischen Serverstand aktualisieren,
   Wallet und Profil-/Group-/Upload-Pfade testen.
3. Zunaechst kostenpflichtige Aktionen auf weitere Nodes schalten,
   Ledger-/Job-Error-Rate und Transaktionsdifferenzen beobachten.
4. Feed separat erst nach Datenloesch-/Privatsphaere-Tests freigeben.
   Der Viewer kann nach eigenstaendiger OAuth-PKCE-Anmeldung direkt
   Statusbeitraege, Likes und Kommentare bearbeiten. Browser ohne
   WebCrypto bleiben lesend.
5. Erst danach uebrige Nodes/Regionen rolling aktualisieren. Keine
   gleichzeitigen grossflaechigen Neustarts, keine automatischen
   Datenbank-Loeschjobs ausser der dokumentierten Retention.

## 7. Rollback

Bei Fehlern `EnableProfileFeed=false` setzen und ROBUST kontrolliert
neu starten; `web_profile_url` voruebergehend entfernen/deaktivieren.
Die sechs Feed-Tabellen **nicht** loeschen: dies sind Einwohnerdaten
einschliesslich privater Opt-in-Einstellungen.
Aktive Schreib-/Loeschoperationen vor einem Downgrade drainen.
Wird auf einen **alten** ROBUST-Build vor diesem Fix zurueckgestellt,
`PortalAccountSelfDeleteEnabled=false` bis zur Wiederherstellung
des neuen, feed-bewussten Loeschpfades setzen; alte Loeschroutinen
koennen sonst Feed-Orphans erzeugen.

Eine Datenbank-Ruecksicherung darf nur mit dokumentiertem
Wiederanlauf-/Reconciliation-Verfahren erfolgen. Rueckspielen geloeschter
Feed-Posts oder Konten aus einem alten Snapshot ist keine sichere
Routine-Rollback-Methode. NV$-Ledgerbuchungen nie mit SQL direkt
aendern; nur den autorisierten Reversal-/Reconciliation-Prozess nutzen.

## 8. Restgrenzen

Die Gebuehrenbuchung und die Profilspeicherung laufen weiterhin in
getrennten Services. Ohne dauerhaftes `pending -> paid/published`
und stabiles Idempotency-/Refund-Konzept bleibt ein seltener Spaetfehler
moeglich. Deshalb fuer den Erstrollout kostenpflichtige Operationen
einzeln testen und abgleichen; keine allgemeine Exactly-once-Garantie
behaupten. Das Feed-Feature ersetzt keine Moderations-/Meldestelle;
Rechte, Backup-Aufbewahrung und Loeschanfragen organisatorisch regeln.
