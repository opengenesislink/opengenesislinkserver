# NexVerse Messenger – Implementierungsstatus

## Fertig im PR-Zweig
- .NET-10-Contracts und eigenständiger ASP.NET-Core-Messenger-Dienst mit PWA.
- Echter Avatar-Login über World-API-OAuth2/OIDC Authorization Code + PKCE S256 statt eigener Passwortdatenbank.
- Lokale und aktive Avatar-Identität gegen World API geprüft.
- Gehashte dauerhafte Sitzungscookies, verschlüsselte Tokens, Rotationslogik und CSRF-Schutz.
- World-API-Proxies für Freunde, lokale Direkt-IM, opt-in Archiv und NexGroups.
- Persistente Absicherung gegen mehrfaches Senden gleicher Clientnachrichten-ID.
- Mobilonline via aktuellem PWA-Heartbeat und Sichtbarkeit nur für entsprechend berechtigte bestätigte Freunde.
- PWA mit responsive Layout und statischem Offline-Cache ohne persönliche API-Daten.
- Linux/Windows-.NET10-Tests und HTTP-Smoke-Gate.

## Noch offen
- Echte HG-Sendeintegration, Live-Gruppenchat/Inworld-Gruppensitzungen und Web Push.
- Push auf iOS nach Suspended/Lockscreen ist derzeit ausdrücklich nicht unterstützt.
- Echte Grid-End-to-End-Prüfung inklusive Viewer, Offline-IM, Blocks, Archiv-Opt-in und Datenschutzlöschung.
- Cluster-fähiger Session-/Idempotenz-Store; die aktuelle SQLite-Lösung muss als einzelner API-Prozess laufen.
- Das im PR hinterlegte SVG ist ein Platzhalter: Das zuvor genehmigte Messengerlogo muss als echte PWA-Icons (auch PNG für iOS) übernommen werden.

Dies ist ein testbares MVP, aber noch keine vollständig fertige Messenger-Plattform.
