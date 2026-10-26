# OpenGenesisLINK – Operator Admin Center 4.0

Stand: 26.10.2026.

## Bedienoberfläche

Das geschützte World-API-Adminpanel verwendet jetzt ein konsistentes, an AdminLTE 4 orientiertes Layout: dunkle, kategorisierte und ein-/ausklappbare Sidebar, feststehende Kopfzeile, helle Arbeitsfläche, kompakte KPI-Karten, lesbare Tabellen und inline GET/POST/PUT/PATCH/DELETE-Formulare. Login, Gruppen-/Userverwaltung, API-/Schlüsselverwaltung, Grid/Simulatoren und Media-API bleiben angeschlossen an **bestehende serverseitige** Admin-Berechtigungen. Die Oberfläche hat kein CDN zur Laufzeit und benötigt keine Browser-seitigen GitHub-Token.

Alte Einträge aus dem Versionsverlauf vor `0.9.3.10` werden **in der UI ausgefiltert**. Historische DB-Migrationen, alte Asset-IDs, bestehende Viewer-Protokolle, Backups und kompatibilitätsrelevanter Servercode werden ausdrücklich nicht blind entfernt.

## Operator Control Center 5.0 – Dark Mode (26.10.2026)

Die helle AdminLTE-4-Skin wurde durch einen konsistenten Dark-Operator-Look ersetzt:
Anthrazit-Blau für die Hauptfläche, dunkle Header-/Sidebar-/Karten-/Formular- und
Tabellenflächen, Cyan ausschließlich als Funktions- und Statusakzent. Auch die
vorher helle sichere Login-Karte wird dunkel ausgegeben. Ein Light-Mode ist für
dieses Control Center nicht vorgesehen; es wird kein Theme aus dem CDN geladen.

Die Sidebar enthält 27 bestehende Seitenziele, neu nach Betriebsaufgaben sortiert,
mit einheitlichen lokalen SVG-Icons, aufklappbaren Sektionen, Tastatur-Fokusmarkierung
und responsivem Öffnen/Schließen. Bestehende data-page-Kennungen bleiben erhalten;
API-Endpunkte, Berechtigungsprüfungen und Konsolenfunktionen sind unverändert.

Die bisherige AdminLTE-Light-CSS-Schicht ist ersetzt, nicht durch ein weiteres
optionales Theme überdeckt. Die CI-Prüfung
tools/ci/verify_ogl_api_adminlte_viewers.py überprüft auch die finale Dark-Skin,
die Sidebar-Einträge und die Anmeldeseite. Responsive Prüfungen empfohlen bei
Desktop >= 1280 px, Tablet 900 px und Mobile 390 px.

Deployment: Admin und Login gehören zur OGL.API.dll. ROBUST aktualisieren,
Browserseite hart neu laden (z. B. Strg+F5), sämtliche Seiten-/Sidebar-Pfade und
Firestorm-Login im Staging prüfen. Keine Datenbankmigration für dieses Redesign.

## ROBUST-Befehle

`GET /api/v1/admin/console` listet die von ROBUST registrierten Konsolenbefehle via `MainConsole.Instance.Commands.GetHelp(...)` und den aktuellen Ausführungsauftrag. `POST /api/v1/admin/console` nimmt `{"command":"show info","confirmation":"AUSFÜHREN"}` entgegen und dispatcht über `MainConsole.Instance.RunCommand(...)` – **kein OS-Shell-/SSH-Ersatz**. UserLevel mindestens 200, echtes lokales UserAccount, `admin:*`-Scope, explizite Browserbestätigung und maximal ein gleichzeitiger Auftrag sind notwendig. Der ausführende Prozess schreibt Ergebnisse in das normale tagesweise ROBUST-Log. Befehle, die interaktive Konsoleneingaben erwarten oder den Prozess beenden, können im Webinterface nicht vollständig synchron abgearbeitet werden; die lokale Konsole bleibt als Rückfallweg verfügbar. Ein Auftrag mit `running` ist nicht zwangsläufig beendet, wenn der Browser neu geladen wird. **Keine automatische Wiederholung** von Mutationen.

Die GUI enthält zusätzlich einen Befehlskatalog, eine direkte Eingabe, Auftragsstatus und Zugriff auf nach Level filterbare, nach Datum/NodeId sortierte ROBUST-/OpenSim-Logs.

## Viewer-Nutzungsstatistik

Die Login-Komponente schreibt erst **nach erfolgreichem Login** ein anonymes Ereignis mit den vom Viewer übermittelten Feldern `channel` und `version` ins ROBUST-Tageslog. Es werden dafür keine Avatar-UUID, IP, MAC, id0 oder Passwortdaten erfasst. Linebreaks werden entfernt und Feldlängen begrenzt. Die Statistik unter `GET /api/v1/admin/viewers?days=1|7|14|30` wertet die letzten 1 bis 30 Tage aus, gruppiert **Viewer + Version** und liefert erfolgreiche Anmeldungen sowie Anteile. Nur lokale UserLevel-200-Administratoren mit `admin:*` dürfen den Bericht lesen. Es ist keine echte Gleichzeitigkeitsschätzung: eine Person mit mehreren Logins wird mehrfach gezählt; Client-Versionen sind selbst gemeldete Werte. Historische Logins von vor Einführung der Statistik sind nicht rückwirkend verfügbar.

Die zugehörige Verwaltungsseite **Viewer & Versionen** zeigt Zeitraum, Gesamtanmeldungen, erkannte Versionskombinationen und eine Tabelle.

## OGL.API.dll statt NexVerse.Server.Api.dll

Im Prebuild ist das API-Projekt jetzt `OGL.API`; die Namespaces bleiben bewusst unter `NexVerse.Server.Api` stabil, damit Quellcode, Einbettung und bestehende Serialisierungsbeziehungen nicht unnötig brechen. Die ROBUST-`ServiceList`-Einträge verwenden die neue **Datei** `OGL.API.dll`. Tests, CI-Checks und generierte Projektverweise sind darauf angepasst. Beim Deployment ist der alte `NexVerse.Server.Api.dll`-Build nicht mehr zu verwenden; vor dem Austausch Services stoppen, Konfigurationen sichern und sämtliche ServiceList-/Deploymentreferenzen aktualisieren. **Niemals** die alte DLL als zweite aktive Plugin-Assembly parallel laden.

## Public HTTP-Startseite (alle Ports) – OpenGenesisLINK Promo

**Modernisiert am 26.10.2026:** Der bereits vorhandene serverinterne
Root-Handler liefert jetzt eine hochwertige, vollständig lokale
OpenGenesisLINK-Produktstartseite in Dark Mode mit responsive Hero,
illustrativem Grid-/Region-/World-API-/Viewer-Schaubild, Feature-Karten,
Links auf Projektwebseite und GitHub sowie barrierearmen
Navigationselementen. Keine externe CSS-/JS-/Bild-/Schrift-Abhängigkeit,
kein Tracking, keine Grid- oder Betriebsgeheimnisse.

Der `BaseHttpServer` liefert **unabhängig vom eingestellten HTTP-Port**
auf exaktem `GET /` ohne Querystring die Seite bei
`Accept: text/html` (Browser) oder `Accept: */*` (curl/wget,
sofern kein eigener Root-GET-Handler registriert ist). Die Seite ist
somit auf ROBUST-Port `8002`, `80` oder Simulator-HTTP-Ports
verfügbar, **sofern dieser TCP-Port wirklich zu OpenGenesisLINK führt**
und ein vorgeschalteter Proxy den Root-Pfad weiterleitet. Sie
konfiguriert keine Firewall und keinen Reverse Proxy.

Unverändert bleiben: Root-`POST` mit XML-RPC/LLSD/JSON-RPC,
`?method=...`-Abfragen, spezielle HTTP-Routen, CAPS,
Hypergrid und Viewer-Login. `/index.php` ohne Querystring leitet
auf `/` um; `/index.php?method=...` behält seine bisherige Funktion.
Andere nicht vorhandene URLs liefern **weiterhin HTTP 404**.
Ein vorhandenes `bin/http_404.html` kann weiterhin das 404-Design
übersteuern. Die öffentliche Seite ist weder Admin-Login noch
Statusmonitor.

Die Seite erhält `Cache-Control: public, max-age=300`,
`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
`Referrer-Policy: no-referrer` und eine restriktive CSP,
die nur eingebettetes CSS erlaubt. Es werden **keine**
API-Schlüssel, Node-IDs, Benutzer-/Regionsdaten oder dynamischen
Versionsinformationen eingebettet.

Schnellprüfung nach kontrolliertem Update / Neustart:

```bash
# Port je nach tatsächlicher ROBUST-/Simulator-Konfiguration:
curl -i -H 'Accept: text/html' http://127.0.0.1:8002/
curl -i http://127.0.0.1:8002/
# Erwartet: HTTP 200, HTML-Titel OpenGenesisLINK und CSP-Header.
# Ein falscher Port oder Proxy ohne Root-Weiterleitung bleibt unerreichbar.
```

CI-Gate: `tools/ci/verify_ogl_public_server_home.py` prüft
eingebettetes HTML, externe Ressourcen, Responsive-/Accessibility-
Grundlagen, Sicherheit und die unveränderten Viewer-Protokoll-Handler.
Eine visuelle Prüfung im Desktop- und Smartphone-Browser und ein
Firestorm-Login-/Hypergrid-Staging-Test sind vor Produktivfreigabe nötig.

## Deployment und Abnahme

1. ROBUST-, OpenSim-, Datenbank- und Konfigurationsbackup erstellen.
2. Neuen .NET-Build mit `OGL.API.dll` erzeugen, alte Plugin-Datei aus der Ladeumgebung entfernen und alle `ServiceList`-Einträge gegenprüfen.
3. Auf Staging Browser-`GET /` und Viewer-XML-RPC-/LLSD-Login nebeneinander prüfen, ebenso OpenSim-Regionsport, HTTPS-Proxy und Hypergrid-Gatekeeper.
4. Admin-Sitzung, API-/Schlüsselmutationen und registrierte Konsolenbefehle testen. Kritische Befehle nur in nicht-produktiver Umgebung testen.
5. Firestorm und einen zweiten Viewer anmelden; `/api/v1/admin/viewers?days=1` auf richtige Namen/Versionen und fehlende Personenkennungen prüfen.
6. Tageslogs (ROBUST, lokaler OpenSim und NexBus NodeId) nach Level/Datum suchen, Logrotation und Pagination testen.

GitHub-CI-Status ist **kein** Ersatz für diese End-to-End-Abnahme.
