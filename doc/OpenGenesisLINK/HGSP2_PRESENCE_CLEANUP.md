# HGSP2 – automatische Pruefung verwaister Hypergrid-Presences

Status: Feature-Branch `feature/hgsp2-security-0941`, **noch nicht als Stable
ausgeliefert**. Eine produktive Datenbank wurde nicht angesprochen oder
veraendert.

## Ausgangsproblem

Die OpenSim-Tabelle `Presence` besitzt `UserID`, `SessionID`,
`SecureSessionID`, `RegionID` und `LastSeen`. **LastSeen ist im
bisherigen Protokoll kein regelmaessiger Avatar-Heartbeat.** Ein langer
Aufenthalt in einer Region ist deshalb kein Beweis fuer einen verwaisten
Eintrag. Zeitbasierte Komplettloeschungen von `Presence` sind unsicher.

Der neue Sweeper verwendet den bestehenden
`OpenSim.Services.PresenceService` und die native
`MySQLPresenceData`-Implementierung; keine zweite Datenbank oder
separater Dienst.

## Automatische, eng begrenzte Bereinigung

Bei aktiviertem HGSP2 und `PresenceCleanupEnabled=true` beginnt der
Timer nach 5 Minuten und prueft danach alle 15 Minuten maximal 50
Eintraege pro Lauf. Standard-Sicherheitsalter: **72 Stunden**.

Nur wenn **alle** folgenden Bedingungen erfuellt sind, darf eine Zeile
aus `Presence` entfernt werden:

1. `LastSeen` ist mindestens so alt wie konfiguriert.
2. Dieselbe Avatar-UUID besitzt einen **fremden**
   `GridUser.UserID`-Eintrag im Format `UUID;HomeURI;Name`.
3. In `UserAccounts` gibt es **kein lokales Konto** mit derselben UUID.
4. Entweder `RegionID=UUID.Zero` (Anmeldung hat nie eine Region
   erreicht) oder **kein** zum Avatar passender fremder
   `GridUser`-Datensatz steht noch auf `Online=true`.
5. Direkt vor dem `DELETE` werden UUID, `SessionID`, `RegionID`,
   `LastSeen` und die Punkte 2–4 erneut in **einer bedingten
   SQL-Löschanweisung** geprueft. Eine dazwischen erneuerte Sitzung
   darf nicht geloescht werden.

Die betroffenen RAM-Caches werden ausschliesslich fuer nachweislich
geloeschte Sitzungen invalidiert. Eine frischere Sitzung desselben
Avatars bleibt erhalten. Es werden **keine** Agenten gewaltsam aus
Regionen entfernt, keine Inventar- oder Assetdaten geloescht und kein
`GridUser`-Status blind zurueckgesetzt.

**Bewusste Grenze:** Ein fremder Avatar mit `Online=true`, gueltiger
`RegionID` und altem `LastSeen` kann entweder aktiv sein oder
einen fehlerhaften Logout haben. Ohne weitere bestaetigte
Regions-Liveness-Information darf dieser Eintrag **nicht** automatisch
geloescht werden. Das vermeidet unterbrochene legitime Aufenthalte.
Es gibt keinen Ersatz durch ein pauschales `DELETE FROM Presence WHERE
LastSeen < ...`.

Die automatische Bereinigung ist bisher **nur fuer MariaDB/MySQL**
implementiert. Andere bestehende Provider bleiben unveraendert, und
geben bei aktiviertem Cleaner eine sichtbare Warnung aus. Weitere
Backends duerfen nur mit entsprechend abgesicherten Pruefungen
hinzukommen. Kein Schemawechsel der `Presence`-Tabelle erforderlich.

## Konfiguration (nach Freigabe, nicht fuer ein aktuelles Live-System)

```ini
[OGLHypergridSecurity]
Enabled = true
PresenceCleanupEnabled = true
PresenceStaleAfterHours = 72
PresenceCleanupIntervalMinutes = 15
PresenceCleanupBatchSize = 50
```

Akzeptierte Grenzen: Alter 24–8760 Stunden, Intervall 5–1440 Minuten,
Batch 1–500. Ungueltige Werte werden beim Start abgelehnt.
Bei fehlender/abgeschalteter Sektion oder
`PresenceCleanupEnabled=false` laeuft **kein** Cleanup-Timer.

Die Ergebnisse erscheinen ohne Avatar-UUIDs in ROBUST-Logs:

```text
[HGSP2] Presence orphan cleaner enabled; min_age_h=72 interval_min=15 batch_max=50.
[HGSP2] Presence orphan cleanup: 2 confirmed foreign stale sessions removed ...
```

Auf MySQL-Fehler werden keine unbedingten Loeschungen ausgefuehrt.
Fehlerdetails mit moeglichen Connection-Strings werden nicht protokolliert.

## Tests

`tools/ci/HGSP2PresenceRegression/` startet ausschliesslich in GitHub
Actions eine disposable MariaDB-Datenbank auf
`127.0.0.1:19498/hgsp2_presence_ci`. Geprueft werden:

- Alter, wirklich fremder GridUser, Offline-Status, fehlende Region.
- Live-Avatar trotz altem Zeitstempel: **nicht** loeschen.
- Frischer Logout: nicht loeschen.
- Lokales Konto mit derselben UUID: nicht loeschen.
- Fehlende Fremdgrid-Zuordnung: nicht loeschen.
- Zweiter, online gemeldeter Fremdgrid-Kontext: nicht loeschen.
- Batch-Obergrenze, wiederholter Lauf und ungueltige Grenzwerte.

Der Test verwendet den echten `MySQLPresenceData`-Provider, keine
Mock-SQL-Abfrage. Die Testdatenbank wird nach dem CI-Lauf verworfen.

## Abnahmeschranke

Vor einem produktiven Release: Backups und Rollback, Einzeltest auf
einem separaten Datenbankklon, Test mit aktiven mehrstuendigen HG-Reisen,
Test von nicht sauber beendeten Reisen und Nachweis, dass lokale
Konten sowie neue Agent-Sessions erhalten bleiben. Unsichere, als
`Online=true` gemeldete Alteintraege duerfen nicht ohne zusaetzlichen
Online-Nachweis erzwungen geloescht werden.
