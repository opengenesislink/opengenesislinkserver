# OpenGenesisLINK World API – Sci-Fi Control Center 2.0

Stand: 25.10.2026. **Ein echtes Redesign der vorhandenen geschützten World-API-Verwaltung, kein separater Mockup-Prototyp.**

## Zugriff und Branding

`GET /api/v1/docs` bleibt die abgesicherte Admin-Anmeldeseite. Die eigentliche Oberfläche wird nur nach lokaler GridUser-Anmeldung mit UserLevel mindestens 200 und einer signierten Session aus `GET /api/v1/docs/panel` geladen. Der vorhandene Login-, TOTP-, Logout- und API-/Node-Autorisierungsweg wird **nicht ersetzt**.

Das vom Betreiber gelieferte `OpenGenesisLINK World API`-Logo wurde als WebP auf 56×56 px optimiert und in `NexOglBranding.cs` eingebettet. Kein externer Bildhost und kein nachgeneriertes Bild. Dieselbe Grafik erscheint im Login und in der Control-Center-Sidebar.

## Navigationsstruktur

Die Sidebar besitzt aufklappbare Hauptgruppen mit sinnvollen Unterseiten:

- **Übersicht & Monitoring:** Dashboard, Statistik, Diagnose, GitHub & Releases
- **Grid & Simulatoren:** Grid-/Regionenplaner, Simulator-Nodes, Estates/Land, Konsole & Jobs
- **Einwohner & Communities:** Benutzer/Avatare, Gruppen, Inventar/Assets, Profile/Media
- **Wirtschaft & Content:** Economy/NV$, Marketplace/Medien, Orte/Events/Suche, Experiences
- **API & Sicherheit:** API-Schlüssel/Scopes, Sicherheit/Audit, API-Explorer, Integrationen, Admin-Anmeldung
- **Software & Planung:** Changelog, Versionen, Roadmap

Jede Fach-Unterseite zeigt **tatsächlich registrierte Endpunkte und HTTP-Methoden** des zur Laufzeit geladenen OpenAPI-Vertrags. Die gefilterten Operationen öffnen den bestehenden echten API-Explorer mit authentifizierter Anfrageausführung. Nicht verfügbare Endpunkte werden ausdrücklich als nicht verfügbar dargestellt, nicht als Mockdaten. Native Grid-/Node-/Estate-Lifecycle-Aktionen verbleiben in ihren etablierten Verwaltungsseiten; keine bestehende Funktion wird entfernt.

## GitHub-Versionierung

Neue GitHub-/Release-Seite plus drei prominente Dashboard-Kacheln. Aus **offiziellen GitHub-REST-Daten**:

1. `/repos/opengenesislink/opengenesislinkserver/releases/latest`: der neueste veröffentlichte, nicht vorab veröffentlichte Stable-Release. **Keine aus dem Versionsstring erratene Stable-Freigabe**.
2. `/repos/opengenesislink/opengenesislinkserver/commits/main`: Development-Branch mit gekürzter SHA und aktuellem Committext. Ein Branch ist keine Release-Freigabe.
3. `/repos/opengenesislink/opengenesislinkserver/actions/workflows/nexjast-ci.yml/runs?branch=main&per_page=1`: der letzte main-CI-Lauf, mit Ergebnis oder laufendem Status.

GitHub-Abfragen werden **serverseitig** durch den neuen World-Endpunkt `GET /api/v1/repository/status` ausgeführt: ein fest konfigurierter öffentlicher Repository-Host, keine API-Schlüssel, kein Browser-Zugriff auf `api.github.com`. Der ROBUST-Prozess hält die Ergebnisse fünf Minuten zwischengespeichert. Die geschützte Admin-Oberfläche liest ausschließlich den **Same-Origin-World-API-Endpunkt**, damit die restriktive `connect-src 'self'`-Policy von Adminpanel und Login-Frame unverändert bleiben kann. Nicht erreichbare GitHub-API oder Rate-Limit-Fehler werden als **nicht verfügbar** beziehungsweise explizit zwischengespeichert angezeigt; es werden **keine** Release-Tags, Commits, CI-Werte oder Installationsstände erfunden.

**Wichtig:** Die GitHub-Stable-Version ist nicht zwingend die installierte Version auf ROBUST. Für die aktuell laufende Serverversion bleibt `GET /api/v1/version` die Quelle. Das Dashboard zeigt diese unabhängig.

## Administrator-Funktionen

- Einheitliches dunkles Design mit leuchtendem Cyan/Blau, responsive Sidebar und kompakten Dienstkacheln.
- Echtzeit-Kennzahlen zu registrierten Einwohnern und Online-Sessions aus `/api/v1/statistics/summary`, Servicezustand aus `/api/v1/health` und Endpoint-Zahl aus `/api/v1/openapi.json`.
- Bessere API-Schlüsselliste: durchsuchbar nach ID/Name/Scope und filterbar nach aktiv/deaktiviert; bestehende Funktionen zum Erstellen, Ändern, Deaktivieren, Löschen und zum Generieren sicherer Secrets bleiben erhalten.
- Fachseiten für Inventar, Gruppen, User, NV$, Media, Sicherheit, Marketplace, Jobs und weitere Bereiche sind keine unabhängigen zweiten APIs. Sie benutzen den zentralen OpenAPI-Vertrag, dessen Scopes und den bestehenden Request-Runner.
- **Kein unbeschränkter Shell- oder OpenSim-Konsolen-Remote-Executor.** Nur tatsächlich vorhandene, serverseitig berechtigte Operationen erscheinen. Für bisher ausschließlich via Textkonsole angebotene Befehle (z. B. freie Asset-Löschbefehle, dynamische Script-Konsole, beliebige Shell) gibt es noch keinen sicheren, explizit autorisierten HTTP-Handler. Sie werden nicht als funktionierend ausgegeben und nicht durch ungefilterte Remote-Commands ersetzt. Das ist eine Sicherheitsgrenze, kein UI-Dummy.

## QA / Release

In der NexVerse CI läuft `verify_worldapi_scifi_admin_center.py`. Zusätzlich zu bestehenden API-, Login-, Schlüssel-, Grid- und Estate-Regressionen werden die Logo-Einbettung, Navigations-IDs, Release-Quellen, Zugriffsschutz, Datenquellen und die Syntax des **tatsächlichen** Browser-JavaScript mit Node überprüft.

Vor Produktionsübernahme: GitHub-API-Zugriff unter KeyHelp-/Apache-HTTPS und CSP prüfen, alle Unterseiten (mobil/desktop) bedienen, Keys/Scopes nur mit autorisiertem Administrator testen, Node/Region/Estates mit Staging-Datensatz testen. GitHub-Release-Status darf nie die lokale Build-Abnahme ersetzen. Code-Merge ist **kein Live-Deployment**.
