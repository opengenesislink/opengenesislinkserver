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

## Public HTTP-Startseite (alle Ports)

Wenn ein Browser einen HTTP-`GET /` mit `Accept: text/html` absetzt, rendert `BaseHttpServer` jetzt eine neutrale OpenGenesisLINK-Informationsseite mit Projektlink. Sie gibt bewusst keine Administrator- oder internen Grid-Daten preis. XML-RPC, LLSD, Simulator- und Viewer-Protokolle auf `/` sowie andere API-Routen behalten den bisherigen Dispatch. Die alte „Hippo“-404-Fallbackmeldung wurde ersetzt. Ein vorhandenes `http_404.html` kann die Fallback-404 weiterhin wie bisher übersteuern; für die Browser-Root-Seite ist das irrelevant.

## Deployment und Abnahme

1. ROBUST-, OpenSim-, Datenbank- und Konfigurationsbackup erstellen.
2. Neuen .NET-Build mit `OGL.API.dll` erzeugen, alte Plugin-Datei aus der Ladeumgebung entfernen und alle `ServiceList`-Einträge gegenprüfen.
3. Auf Staging Browser-`GET /` und Viewer-XML-RPC-/LLSD-Login nebeneinander prüfen, ebenso OpenSim-Regionsport, HTTPS-Proxy und Hypergrid-Gatekeeper.
4. Admin-Sitzung, API-/Schlüsselmutationen und registrierte Konsolenbefehle testen. Kritische Befehle nur in nicht-produktiver Umgebung testen.
5. Firestorm und einen zweiten Viewer anmelden; `/api/v1/admin/viewers?days=1` auf richtige Namen/Versionen und fehlende Personenkennungen prüfen.
6. Tageslogs (ROBUST, lokaler OpenSim und NexBus NodeId) nach Level/Datum suchen, Logrotation und Pagination testen.

GitHub-CI-Status ist **kein** Ersatz für diese End-to-End-Abnahme.
