# OpenGenesisLINK Profile Feed v1 — Stadt NexVerse

Status: **opt-in Entwicklungsfeature**, kein Live-Deployment. Grundlage:
Second Life zeigt den Feed-Reiter als eingebettete Webansicht. OpenSim/Firestorm
können über GridInfo `web_profile_url` eine eigene Feed-Webseite beziehen.

## Funktionsumfang

- Eigene Textbeiträge (max. 1.500 Zeichen), chronologisch neueste zuerst.
- Kommentare (max. 800 Zeichen) mit Bearbeiter-UUID, Likes, Folgen/Entfolgen.
- Beitragslöschung durch Eigentümer, Kommentarlöschung durch Verfasser
  **oder Profilinhaber**.
- Öffentliche HTML-Ansicht im Firestorm-Feed-Reiter:
  `https://world.stadt-nexverse.de/feed?name=Vorname.Nachname`
- Profile-ID-basierte JSON-Schnittstelle `/api/v1/feed/{principalId}`.
  `GET` liefert Beiträge, Anzahl Kommentare/Likes und Follower;
  `GET /api/v1/feed/{principalId}/{postId}/comments` liefert Kommentare.
- `POST /api/v1/feed/{principalId}` erstellt **nur für den eigenen Avatar**
  Beiträge, Bearer-Scope `profile:write`.
- `DELETE /api/v1/feed/{principalId}/{postId}` entfernt eigenen Beitrag.
- `POST /api/v1/feed/{principalId}/{postId}/comments`, `DELETE
  /api/v1/feed/{principalId}/{postId}/comments/{commentId}`.
- `PUT/DELETE /api/v1/feed/{principalId}/{postId}/likes` und
  `PUT/DELETE /api/v1/feed/{principalId}/following`.
  Diese benötigen jeweils `relationships:write`, gültigen lokalen Avatar
  und einen veröffentlichten oder eigenen Feed.

Alle Schreibaufrufe nutzen JSON `{"text":"Hallo NexVerse!"}` und
`Authorization: Bearer <OAuth-Access-Token>`. **Keine** öffentlichen
Schreibzugriffe, URL-Tokens, Viewer-Session-Spoofing oder Passwörter im Feed.
Rate-Limits: höchstens 3 Beiträge/10 Minuten sowie 30/24 Stunden, maximal
10 Kommentare/Minute je lokaler Avatar (zuzüglich API-Rate-Limit).

## Datenschutz

Die öffentliche Leseberechtigung hängt **immer** vom maßgeblichen
`userprofile.profileAllowPublish` ab. Ist das Profil privat, gibt
`GET` nur mit eigenem `profile:read`-Token oder berechtigtem
Administrator-Token Daten frei; die anonyme Viewer-Webseite bleibt gesperrt.
Unbekannte oder fremde Hypergrid-Identitäten erhalten keinen Feed.
Posts/Kommentare werden HTML-escaped gerendert (keine aktiven HTML-Inhalte).
Aus dem vorhandenen Inventar werden keine Medien oder privaten Texturen
übernommen. Externe Bild-URLs sind in Phase 1 nicht unterstützt.

**Die Webansicht im unveränderten Firestorm ist vorerst lesend.**
Der Viewer stellt dem Feed-Webbrowser kein sicheres OAuth-Token zur Verfügung.
Texte, Kommentare, Likes und Followeraktionen werden deshalb über die
authentifizierte World API bedient; die Portalseite muss dafür noch ihre
Bedienoberfläche implementieren. Keine automatische Übernahme der Second-Life-
Feed-Daten; Stadt NexVerse betreibt einen eigenständigen Datenspeicher.

## Konfiguration

Die produktive `bin/Robust.HG.ini` wird von diesem PR **nicht** verändert.
Vor Freigabe auf Staging vorhandene Konfiguration sichern, dann:

```ini
[NexVerseWorldApi]
EnableProfileFeed = true

[GridInfoService]
web_profile_url = https://world.stadt-nexverse.de/feed?name=[AGENT_NAME]
```

Die `web_profile_url`-Zeile gehört in die **bereits vorhandene**
`[GridInfoService]`-Sektion. Keine doppelte Sektion anlegen.
Die externe HTTPS-URL muss die neue `/feed`-Route auf ROBUST proxien und
im Browser erreichbar sein. Firestorm kann gecachte Gridinformationen haben,
daher nach Änderung Gridinfo aktualisieren bzw. Viewer neu anmelden.

Nur mit `[UserProfilesService] Enabled = true` und dem vorhandenen
MariaDB-Profilprovider. Beim ersten Aktivieren entstehen (InnoDB):
`ogl_profile_feed_posts`, `ogl_profile_feed_comments`,
`ogl_profile_feed_likes`, `ogl_profile_feed_follows`.
Die Tabellen werden nicht vom normalen Profil-PATCH verändert.

Vor dem Upgrade: vollständiges DB-Backup und Staging-Test; Ausfall des
optionalen Feeds darf ROBUST nicht am Start hindern. Abschalten mit
`EnableProfileFeed=false` löscht keine Beiträge.
Kein automatischer produktiver Rollout.

## Abnahme vor Aktivierung

1. Tests für OpenAPI, bestehende Avatarprofile, OAuth, MariaDB und Viewer-Login.
2. Profil öffentlich: Anzeige in Firestorm und als JSON; privat: anonym HTTP 404.
3. OAuth-Bearer owner/non-owner, Fremd-Grid-Agent, Scope und ungültige UUIDs.
4. Beitrags-/Kommentarlimits, Cross-Profile-ID-Missbrauch, Likes, Folgen,
   Kommentarentfernung und persistente DB-Speicherung.
5. Unicode/HTML-Injection, lange UTF-8-Anfragen, Browser-CSP und kein
   Abfluss von Sessiontokens.
6. Reverse-Proxy-URL, Firestorm 7.2.4 OpenSim, GridInfo-Cache und
   unverändertes Profil-/Login-Protokoll.
7. Datenschutz: Löschung der Feeddaten bei Konto-Selbstlöschung separat
   integrieren **bevor** das Feature im Produktivbetrieb aktiviert wird.

## Noch nicht implementiert

- Upload und Anzeige von Fotos/Snapshots im Feed.
- Viewer-Web-Login mit OAuth-PKCE für direktes Schreiben im Feed-Reiter.
- Benachrichtigungen, Timeline gefolgter Personen, Freunde-beschränkte
  Sichtbarkeit, Moderationsqueue, Meldesystem und Hypergrid-Federation.
- Verknüpfung der Beitragstabellen mit dem Account-Löschflow.
  **Nicht auf Produktion aktivieren, bis dieser Punkt umgesetzt ist.**
