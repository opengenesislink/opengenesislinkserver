# NexVerse Messenger API – implementiertes MVP

Basis: eine gemeinsame HTTPS-Origin; Browser benötigt keine World-API-Bearer-Tokens. Alle Methoden außer /healthz, /auth/start, /auth/callback und den statischen PWA-Dateien setzen eine gültige lokale Messenger-Cookie-Sitzung voraus.

## Anmelden und Sitzungen

- GET /auth/start – Redirect zum bereits vorhandenen World-API-OAuth2-Consent/Login mit PKCE S256.
- GET /auth/callback?code=&state= – einmaligen OAuth-Code gegen Tokens tauschen, /api/v1/users/me prüfen (lokal, aktiv), sicheres HttpOnly-Sitzungscookie ausstellen. Keine Passwörter im Messenger.
- GET /api/v1/auth/me – aktuell verifizierter Avatar (principalId, username, displayName), CSRF-Token.
- POST /api/v1/auth/logout – Sitzung und Refresh-Token widerrufen, CSRF erforderlich.
- POST /api/v1/presence/heartbeat – aktive mobile PWA-Präsenz (ca. 90s TTL), CSRF erforderlich.
- GET /api/v1/capabilities – explizite Liste aktuell verfügbarer und nicht unterstützter Features.

Sitzungen verbleiben maximal 30 Tage auf dem Server und überstehen einen Dienstneustart bei persistiertem SQLite-Store und Data-Protection-Keyring.

## Freunde und IM

- GET /api/v1/friends – geprüfte eigene Beziehungen aus World API; mobile_online und messenger_status nur entsprechend Sichtbarkeit lokaler bestätigter Freundschaften.
- GET /api/v1/conversations – Konversationen aus opt-in World-API-Archiv.
- GET /api/v1/messages?after=N – neue Archiv-Events (5s PWA-Frontend-Polling).
- GET /api/v1/messages/with/{localPeerId} – Verlauf zu einem bestimmten Avatar.
- GET /api/v1/messages/settings – aktuelle Archiv-Einwilligung.
- PATCH /api/v1/messages/settings – JSON { enabled: true|false }, CSRF erforderlich. Deaktivierung löscht Historie.
- POST /api/v1/im – CSRF erforderlich. Beispiel:
    { "clientMessageId": "6bf47d12-96cd-4d57-b191-3760b8b4bc5d",
      "recipient": { "agentId": "00000000-0000-0000-0000-000000000001",
                     "homeGridUri": null },
      "body": "Hallo aus NexVerse" }

Der Sendeweg unterstützt aktuell **nur bestätigte lokale Freunde**. Er delegiert an /api/v1/messages der World API und von dort an die native IM-/Offline-Transportlogik. Nicht bestätigte HG-Endzustellung darf nicht als zuverlässig gelten. 202 bedeutet *vom Backend angenommen*, nicht *vom Empfänger gelesen*. UTF-8-Limit 1024 Bytes. SQLite reserviert pro (sender, clientMessageId) genau eine Sendeaktion; nach unklarer Netzwerkantwort meldet die API 409, statt selbständig zu duplizieren.

## NexGroups

- GET /api/v1/groups – eigene Gruppenmitgliedschaften, aus /api/v1/groups/mine.
- GET /api/v1/groups/{groupId}/notices – serverseitig autorisierte Gruppen-Mitteilungen.

**Nicht in diesem MVP:** POST Live-Gruppenchat, Web Push, native HG-IM-Sends, Delivery-/Read-Receipts, Geräte-Verwaltung, Chat-Anhänge und eigenständige Gruppenchat-Retentionslogik.

## Grenzen und Betriebsmodell

- SQLite-Datenbank ausschließlich für die **Messenger-Sitzungen**, Mobil-Präsenz und HTTP-Idempotenz. Das bestehende opt-in World-API-IM-Archiv wird nicht kopiert.
- Kein Multi-Node-Failover, keine Browser-Offline-Nachrichtenwarteschlange, kein Push bei suspendiertem iPhone.
- Browser dürfen die Authentifizierung der World API nie durch selbst geschriebene Cookie-/Password- oder Hash-Logik ersetzen.
