# NexVerse Messenger MVP – Betrieb und Testinstallation

Der Messenger ist ein eigener ASP.NET-Core-10-Prozess mit PWA unter derselben HTTPS-Origin. Er ist NICHT automatisch Bestandteil der ROBUST-Hauptausführung. Zum Test muss die World API bereits funktionieren.

## 1. World-API-OAuth-Client registrieren

Auf einer autorisierten World API als Administrator einen dedizierten, aktivierten OAuth-Client anlegen (nicht dieselben Daten wie Admin-/Service-API-Keys verwenden):

POST https://world.stadt-nexverse.de/api/v1/auth/clients
Authorization: Bearer ADMIN_ACCESS_TOKEN
Content-Type: application/json

Beispielbody:

    {
      "name": "NexVerse Messenger",
      "client_type": "confidential",
      "redirect_uris": ["https://messenger.stadt-nexverse.de/auth/callback"],
      "allowed_scopes": [
        "openid", "offline_access", "users:read",
        "relationships:read", "relationships:write", "groups:read"
      ]
    }

Aus der Antwort client.client_id und das nur einmal angezeigte client_secret sicher hinterlegen. Alternativ lässt die World API einen public PKCE-Client ohne Secret zu. Die eingetragene Redirect-URI muss mit der Messenger-Konfiguration EXAKT übereinstimmen.

Die World API verifiziert den Avatar beim OAuth-Consent-Flow. Fremde HG-Avatare sind keine gültigen lokalen Login-Principals. Der Messenger ruft anschließend /api/v1/users/me ab und verlangt local_to_grid=true sowie active=true.

## 2. Konfiguration

Beispiel für den Messenger-Dienst als Umgebung (Werte anpassen, echte Geheimnisse nie ins Git-Repository schreiben):

    ASPNETCORE_ENVIRONMENT=Production
    ASPNETCORE_URLS=http://127.0.0.1:8099
    AllowedHosts=messenger.stadt-nexverse.de
    Messenger__WorldApiBaseUrl=https://world.stadt-nexverse.de/
    Messenger__PublicBaseUrl=https://messenger.stadt-nexverse.de/
    Messenger__ClientId=CLIENT_ID_AUS_WELT_API
    Messenger__ClientSecret=SECRET_AUS_WELT_API
    Messenger__DatabasePath=/var/lib/ogl-messenger/private/session.sqlite
    Messenger__DataProtectionKeysPath=/var/lib/ogl-messenger/private/keys
    Messenger__TrustedProxyIp=127.0.0.1
    Messenger__SessionDays=30

Nur ein API-Prozess darf den Single-Node-Session-Store bedienen. Der Dienst muss unter einem eigenen unprivilegierten Linux-Systemkonto laufen. Das private Stammverzeichnis (z. B. /var/lib/ogl-messenger/private) muss für andere Benutzer unzugänglich sein. Die Anwendung beschränkt auf Linux ihre DB-/Key-Verzeichnisse auf 0700 und die SQLite-Hauptdatei auf 0600. Die Keys müssen bei Upgrades/Backups **zusammen mit der DB** erhalten bleiben: ohne die alten Data-Protection-Keys sind gespeicherte Refresh-Tokens unlesbar.

Der Dienst prüft, ob WorldApiBaseUrl und PublicBaseUrl HTTPS verwenden. Unvollständige Konfiguration blockiert alle Auth-/API-Routen mit 503; /healthz und das statische UI sind weiterhin erreichbar.

## 3. Bauen und Starten

Im ausgecheckten Serverrepository (.NET SDK 10):

    dotnet build OpenSim/Addons/OGLMessenger/Server/OGLMessenger.Server.csproj -c Release
    dotnet run --no-launch-profile -c Release --project OpenSim/Addons/OGLMessenger/Server/OGLMessenger.Server.csproj

Für einen systemd-Dienst empfiehlt sich ein publish in ein eigenes Verzeichnis, ein EnvironmentFile mit restriktiven Rechten und ein eigener Dienstbenutzer. Secret-Werte nicht als Kommandozeilenargumente übergeben.

## 4. HTTPS-Reverse-Proxy (KeyHelp/Apache-Beispiel)

Einen HTTPS-VHost für messenger.stadt-nexverse.de anlegen (gültiges TLS-Zertifikat); innerhalb des VHosts beispielsweise:

    ProxyPreserveHost On
    ProxyPass / http://127.0.0.1:8099/
    ProxyPassReverse / http://127.0.0.1:8099/
    RequestHeader set X-Forwarded-Proto "https"

Dabei muss der Hostname vom Proxy weitergereicht werden. Nur die unmittelbare Proxy-IP darf in Messenger__TrustedProxyIp eingetragen sein; keine wildcard TrustedProxies verwenden. Falls KeyHelp und Messenger auf verschiedenen Hosts liegen, ist der interne Listener über WireGuard/private Firewall erreichbar zu halten; nicht ungeschützt ins Internet öffnen.

## 5. Funktionstest

- Öffnen: https://messenger.stadt-nexverse.de/
- Mit lokalem Avatar anmelden: Redirect zur World-API-Consent-Seite und zurück zur PWA. Keine Avatar-Passworteingabe direkt im Messenger.
- Zwei registrierte, bestätigte lokale Freunde testen (auch offline): IM in der PWA senden; im Viewer kontrollieren, ob die native IM erscheint.
- World-API-IM-Archiv aktivieren (muss serverseitig verfügbar sein), IM-Verlauf/Events prüfen. Deaktivieren löscht den gespeicherten Verlauf gemäß bestehender World-API-Semantik.
- Freundesstatus: PWA 30 Sekunden im Vordergrund offen lassen, nach spätestens 90 Sekunden ohne Heartbeats gilt das Mobilgerät als offline. Sichtbarkeit abhängig von den vorhandenen Freunden-/Online-Rechten.
- Gruppenliste und Gruppen-Notices prüfen.
- Logout und nachfolgende /api/v1/auth/me-Abfrage müssen 401 ergeben; Refresh/Token darf nicht unautorisiert wieder zugänglich sein.

Lokale Sicherheitstests:

    dotnet run --configuration Release --project tools/ci/OGLMessengerRegression/OGLMessengerRegression.csproj
    python3 tools/ci/verify_messenger_http.py

## Bewusst nicht aktiv

Ausgehende HG-IMs, Live-Gruppenchat, Push-Nachrichten und bestätigte Endzustellungs-/Lesebestätigungen. Der aktuelle Messenger markiert diese Features als nicht verfügbar, statt stillschweigend Nachrichtenzustellung vorzutäuschen.

Für **iPhone-Push** ist später eine echte Web-Push-Abonnementverwaltung mit VAPID, serverseitigem Pushversand und iOS-Tests nötig; das PWA-Manifest und der aktuelle Service Worker allein reichen dafür nicht.
