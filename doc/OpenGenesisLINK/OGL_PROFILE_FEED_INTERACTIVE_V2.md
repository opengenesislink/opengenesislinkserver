# OpenGenesisLINK Generation 1: interaktiver Profilfeed (v2)

Der Feed ist in der ausgelieferten ROBUST-Konfiguration standardmaessig
aktiv. Die Serverpruefungen fuer die gemeinsame MariaDB und die lokale
Konto-Loeschung bleiben verpflichtend; bei Fehlern wird das Modul
abgewiesen. Die interaktive OAuth-App wird beim ersten Start mit einem
stabilen, oeffentlichen First-Party-Client automatisch eingerichtet.
Wenn ein Administrator diesen Client deaktiviert oder veraendert, wird
er **nicht** automatisch wieder aktiviert.

Ab dieser Stufe kann
ein lokaler Stadt-NexVerse-Avatar im eingebetteten Firestorm-Webprofil
nach einer **eigenstaendigen, sicheren OAuth-Anmeldung** direkt posten,
kommentieren, liken, folgen und seine "Abonniert"-Timeline ansehen.
Ein nativer TrixieStorm-Tab ist dafuer nicht erforderlich.

**Datenschutz/Technik:** Der Viewer uebergibt dem eingebetteten Browser
weder Passwort noch simulatorseitigen Maschinenschluessel.
Die Anmeldung wird allein ueber den bestehenden `/oauth/authorize`-Dienst
mit Avatarname, Passwort und ggf. TOTP durchgefuehrt. OAuth 2.0
Authorization Code mit PKCE S256, zufaelligem `state`, registriertem
exaktem Redirect und kurzem Zugriffstoken. Keine Passwoerter im Feed,
keine OAuth-Tokens in Query-Parametern, keine Browser-Maschinenkeys.
Der Token wird **ausschliesslich in sessionStorage** des HTTPS-Ursprungs
verwahrt und laeuft standardmaessig nach 1800 Sekunden ab; kein
`offline_access`/Refresh-Token. Logout entfernt und widerruft den Token
(soweit der Revocation-Dienst erreichbar ist).
In Browsern ohne `crypto.subtle` oder `sessionStorage` kann man die
Weboberflaeche nur lesend verwenden. Inworld-Viewer-Browser muessen
HTTPS und PKCE/WebCrypto unterstuetzen.

## 1. Voraussetzungen

- `EnableProfileFeed=true` und die gemeinsame MariaDB aus
  [Rollout v1](OGL_PROFILE_FEED_ROLLOUT.md).
- `[NexVerseWorldApi] EnableNativeTokens=true` und funktionierendes
  OIDC/OAuth-Backend mit gesichertem Signing Key. Ohne das keine
  interaktive Feed-Anmeldung.
- `[NexVerseWorldApi] PublicBaseUrl` muss **exakt** die oeffentliche
  HTTPS-Origin des Feeds sein, z. B. `https://world.stadt-nexverse.de`.
- Reverse Proxy muss `/feed`, `/api/v1/feed`, `/oauth/authorize`,
  `/oauth/token`, `/oauth/revoke` auf denselben ROBUST routen.
- Avatar-Login funktioniert ueber den bestehenden interaktiven
  `/oauth/authorize`-Browser-Flow. UserLevel/Scopes stammen aus
  `NexAuthorizationPolicy`. Keine manuelle Weitergabe von Tokens.

## 2. OAuth-Client wird standardmaessig automatisch eingerichtet

Bei einer normalen ROBUST-Installation reicht:

```ini
[NexVerseWorldApi]
EnableProfileFeed = true
EnableNativeTokens = true
ProfileFeedRetentionDays = 180
ProfileFeedOAuthClientId = ""
PublicBaseUrl = "https://world.stadt-nexverse.de"
```

ROBUST erstellt beim ersten Start einen **oeffentlichen First-Party-OAuth-
Client** `Stadt NexVerse Profile Feed` im bestehenden, persistenten
OAuth-Speicher. Der Client hat ausschliesslich
`profile:read profile:write relationships:read relationships:write` und
die exakt registrierte HTTPS-Redirect-Adresse
`https://world.stadt-nexverse.de/feed`. Es wird kein geheimer
Client-Key in den Browser gegeben.

Weitere ROBUST-Neustarts verwenden denselben Client, ohne Duplikate.
Ein absichtlich deaktivierter oder in Redirect/Berechtigungen
veraenderter Client wird **nicht** stillschweigend wiederhergestellt:
die interaktive Anmeldung bleibt dann aus, waehrend das Feedprofil
lesbar bleibt. Ein Administrator kann stattdessen einen eigenen
Public Client registrieren und dessen ID explizit in
`ProfileFeedOAuthClientId` setzen. `EnableProfileFeed=false` deaktiviert
das gesamte Feed-Modul.

Nach einem Upgrade ROBUST kontrolliert neu starten, dann
`https://world.stadt-nexverse.de/feed?name=First.Last`
im Viewer oeffnen und die Anmeldung mit einem Testavatar pruefen.
Vorhandene Posts/Kommentare werden nicht neu angelegt oder geloescht.

## 3. Bedienung im Profil

1. Eigene oder fremde lokale Avatarprofile im Viewer oeffnen und auf
   **Mit Avatar anmelden** klicken.
2. Im OAuth-Formular mit Avatarname, Passwort und ggf. TOTP anmelden.
   Danach kehrt der Browser zu demselben Profil zurueck.
3. Im **eigenen** Profil erscheint "Was gibt es Neues?" zum Schreiben.
   Maximal 1500 Zeichen, hoechstens drei Posts/10 Minuten und
   30 Posts/24 Stunden. Der Avatar kann eigene Posts loeschen.
4. In **oeffentlichen** fremden Profilen kann man
   **Folgen/Entfolgen**, kommentieren (maximal 800 Zeichen, 10/min)
   und **Gefaellt mir** setzen/entfernen. Eigene Kommentare lassen
   sich loeschen.
5. Mit **Abonniert** sieht man den persoenlichen Feed aller
   **noch oeffentlichen** gefolgten lokalen Avatare. Ein spaeter
   privat gestelltes Profil verschwindet aus dieser Timeline.
6. **Abmelden** beendet die Browser-Sitzung; Token abgelaufen:
   erneut anmelden. Kein dauerhafter Zugriff auf Avatarpasswoerter.

Nur der Server prueft Owner, Scopes und Privatsphaere. Namen, Posttexte
und Kommentare werden fuer HTML serverseitig escaped bzw.
in JavaScript nur mit `textContent` eingefuegt. Die HTTP-HTML-
Antwort setzt `Cache-Control: no-store` und ein Nonce-basiertes
`Content-Security-Policy`, keine externen Skripte.

## 4. API-Erweiterungen

- `GET /api/v1/feed/me` - lokaler Bearer-Avatar
  (`profile:read`)
- `GET /api/v1/feed/timeline` - eigene gefolgte **oeffentliche**
  Posts (`relationships:read`)
- `GET /api/v1/feed/{principalId}` - optional personalisierte
  `following` und `liked_post_ids` bei gueltigem Bearer
- `GET /api/v1/feed/{principalId}/{postId}/comments` -
  sichtbarer Kommentar-Verfassername

Vorhandene POST-/DELETE-/PUT-APIs bleiben unveraendert; SQLite wird
nicht als neuer Feed-Datenspeicher verwendet. Das System ueberschreibt
keine bestehenden Viewer-Account-Daten.

## 5. Abnahme

- Anonyme Feedansicht und gesperrter Feed; Eigentuemer sieht Privat-
  Feed erst nach OAuth; fremder Avatar wird serverseitig abgewiesen.
- Login auf demselben Browser/HTTPS-Origin; falscher `state` und
  fehlender PKCE-Verifier werden abgewiesen.
- Login mit TOTP, erneutes Laden und Logout. Kein Token in
  Webprofil-URL, Proxyzugriffslog oder Referer.
- Eigene Posts, Kommentare, Likes und Follow-Hierarchie mit
  **zwei** Testavatares; fremder Profileigentumspfad 403.
- `Abonniert` nach einem PublishProfile-Opt-out:
  Post darf **nicht** mehr angezeigt werden.
- Mobile Webansicht, Firestorm OpenSim 7.2.4 und TrixieStorm:
  testen, ob Inworld-CEF `crypto.subtle` und sessionStorage
  freigibt. Bei fehlenden Features im externen Browser testen.
- Reverse-Proxy routet OAuth und Feed; Backup und
  Wiederherstellung der MariaDB bleiben Rollout-Voraussetzung.

## 6. Nicht implementiert: Erweiterungsstufen

Dies ist **nicht** bereits volle Second-Life-Paritaet. Es fehlen:
Snapshot-/Fotoupload (erfordert sichere Asset-Verknuepfung und
Metadatenbereinigung), granularere Leserechte (Freunde/nur Einwohner),
Meldesystem/Moderationsqueue, Benachrichtigungen, Suche nach Tags und
Orten, geplante Beitragsveroeffentlichung sowie Hypergrid-Foederation.

Diese Funktionen duerfen erst nach separaten Sicherheits- und
Datenschutztests produktiv zugeschaltet werden; z. B. keine
automatische Veroeffentlichung von Standort oder Onlinezeiten.
