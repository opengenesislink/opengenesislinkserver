# NexVerse Messenger – OpenGenesisLINK Generation 1

**Status:** World-API-basiertes **MVP in der Entwicklung**. Der PR ersetzt das reine API-Gerüst durch eine installierbare PWA und einen eigenständigen .NET-10-Server. Noch keine vollständige WhatsApp-Alternative oder produktive Freigabe.

## Implementiert

- PWA für Desktop, Android und iPhone mit responsiver deutschsprachiger Dark-Mode-Oberfläche und Startbildschirm-Installation.
- Login über die vorhandene World API mit OAuth2 Authorization Code und PKCE S256. Avatarname/Passwort werden im World-API-Login verarbeitet, nicht im Messenger.
- Nur aktive lokale Stadt-NexVerse-Avatare dürfen sich anmelden. HG-Kontakte sind keine Messenger-Loginidentitäten.
- 30-Tage-Sitzung mit zufälligem Secure HttpOnly SameSite-Cookie, gespeicherten Cookie-Hashes, verschlüsselten OAuth-Tokens, persistierten Schlüsseldateien und Refresh-Rotation.
- CSRF-/Origin-Schutz, Login- und Senderate-Limits, 16-KiB-Eingabegrenze, verpflichtendes HTTPS und expliziter Reverse-Proxy-Vertrauensanker.
- Freundesliste mit vorhandenen Inworld-Präsenzrechten, separate private Mobilonline-Anzeige mittels 90-Sekunden-Heartbeat; fremde HG-Präsenz wird nicht erfunden.
- Direkt-IM an bestätigte lokale Freunde durch den nativen World-API/Inworld-IM-Pfad. Das SQLite-Ledger verhindert das doppelte Abschicken gleicher clientMessageId innerhalb eines Absenders. Bei unklarer Zustellung gibt es keinen automatischen Retry.
- Optionales World-API-Nachrichtenarchiv einschließlich nutzerseitiger Einwilligung, Verlauf und Löschmöglichkeit; 5-Sekunden-Vordergrundabfrage.
- Gruppenmitgliedschaften und lesbare Gruppenmitteilungen aus NexGroups.
- PWA-Service-Worker cached nur statische Dateien, nie Authentifizierung oder Nutzernachrichten.

## Noch nicht implementiert

- Ausgehende HG-IMs, HG-Offline-IM-Garantien. HG-Freunde werden gelistet, Senden ist ausdrücklich deaktiviert.
- Live-Gruppenchat und Gruppenchat-Brücke zwischen PWA und Inworld. Notices sind nur lesbar.
- Web Push (einschließlich iOS-Sperrbildschirm), Zustell- und Lesebestätigungen.
- Mehrere Messenger-API-Nodes mit gemeinsamem zentralem Session-Store.
- End-to-End-Abnahmetests mit echtem Avatar, Offline-Empfang, HG und mehreren Simulatorregionen.

## Dokumentation

Installation und OAuth-Registrierung: [DEPLOYMENT.md](DEPLOYMENT.md). Weitere Integrationsgrenzen: [INTEGRATION_AUDIT.md](INTEGRATION_AUDIT.md). API: [protocol.md](protocol.md).

Der Messenger ändert keine bestehenden Login-, IM-, Friends- oder Gruppenmodule.
