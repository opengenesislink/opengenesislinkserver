# OpenGenesisLINK Media API – vollständige World-API-Integration

Stand: 25.10.2026. Der Medienkatalog ergänzt die bereits existierenden Avatar- und Gruppenservices, ohne eine zweite Asset-Datenbank oder einen zweiten AssetService zu starten. **Produktiv nutzbar erst nach Deployment und Staging-Abnahme des integrierten Builds.**

## 1. Endpunkte

Basis: `https://world.stadt-nexverse.de`.

| Methode | URL | Zugriffsregeln |
|---|---|---|
| GET | `/api/v1/media/groups/{groupId}/image` | `groups:read`, maßgebliche Gruppeninsignie |
| PUT | `/api/v1/media/groups/{groupId}/image` | Resident `groups:manage` **und** gruppeninterne `ChangeActions`-Berechtigung oder Founder, plus eigenes Upload |
| GET | `/api/v1/media/profiles/{principalId}/image` | öffentlich bei `PublishProfile`, privat nur Owner/Admin |
| PUT | `/api/v1/media/profiles/{principalId}/image` | `profile:write`, eigener Avatar und eigenes Upload |
| GET | `/api/v1/media/profiles/{principalId}/first-life` | `profile:read`, immer Owner/Admin |
| PUT | `/api/v1/media/profiles/{principalId}/first-life` | `profile:write`, eigener Avatar und eigenes Upload |
| POST | `/api/v1/media/uploads` | Resident `profile:write` für sich selbst; vertrauenswürdiger Publisher mit `media:publish`-Maschinenkey für lokalen Avatar |
| GET | `/api/v1/media/assets/{assetId}` | **Nur im Medienkatalog registrierte** Uploads; öffentlich nur bei expliziter Veröffentlichung, sonst Owner/Admin |
| PATCH | `/api/v1/media/assets/{assetId}` | Owner mit `profile:write` oder dedizierter `media:publish`-Publisher |
| GET | `/api/v1/media/inventory/{itemId}/image` | `inventory:read`, **nur wirklich eigenes Inventaritem** mit erlaubtem Bildtyp |
| GET | `/api/v1/media/marketplace/{listingId}/image` | öffentlich, wenn vertrauenswürdiger Publisher eine Bildbindung für diese Listing-ID registriert hat |
| PUT | `/api/v1/media/marketplace/{listingId}/image` | dedizierter `media:publish`-Maschinenkey, verifiziertes registriertes Eigentümer-Upload |
| DELETE | `/api/v1/media/marketplace/{listingId}/image` | `media:publish`-Maschinenkey |

Die URL-Muster sind **nur** nach Deployment der entsprechenden World-Version erreichbar. Die bestehende `GET /api/v1/profiles/{principalId}/image`-Route bleibt kompatibel.

`GET`-Bildantworten liefern `image/png`/`image/jpeg`, nicht JSON. Für die gewünschte Vorschaubildgröße kann `?size=64|128|256|512` verwendet werden. Die bestehende Media API limitiert Ausgangs-/Eingangsbilder (bis 2048 Pixel Kantenlänge, 8 MiB) und liefert ausschließlich aktuell autorisierte Referenzen.

## 2. Upload (Portal-Backend oder eingeloggter Bewohner)

```http
POST /api/v1/media/uploads
Content-Type: application/json
Authorization: Bearer <resident-token-mit-profile:write>

{
  "content_type": "image/png",
  "data_base64": "<base64>",
}
```

In gültigem JSON das letzte Komma weglassen. Unterstützt sind PNG/JPEG bis 4 MiB; der Server prüft Signatur und Abmessungen (max. 2048×2048), **transkodiert für OpenSim/Firestorm nach JPEG2000** und schreibt das Texture-Asset über den **bereits gestarteten ROBUST AssetService**. So bleibt das Bild inworld nutzbar. Antwort HTTP 201 enthält `asset_id`, `owner_id`, `content_type`, `image_url` und `visibility=private`.

Ein Maschinen-Client verwendet statt Bearer den Header `X-NexVerse-Api-Key` mit explizit eingeräumtem `media:publish`; bei solchen Uploads ist im JSON zusätzlich die überprüfte lokale `owner_id` erforderlich. Dieser Key darf nur auf dem Portal-Backend stehen, nicht in Browser oder URLs.

## 3. Aktivieren, Zuordnen, Veröffentlichen

**Eigene Profil-/Gruppenbilder ändern:**

```http
PUT /api/v1/media/profiles/{principalId}/image
Authorization: Bearer <eigener-token>
Content-Type: application/json

{"asset_id":"<id-aus-eigenem-upload>"}
```

Entsprechende `PUT`-Routen gibt es für `/first-life` und `/groups/{groupId}/image`; Gruppenänderungen prüfen zusätzlich die tatsächlichen Gruppenrechte.

**Eigenes Upload explizit öffentlich/privat schalten:**

```http
PATCH /api/v1/media/assets/{assetId}
Authorization: Bearer <eigener-token>
Content-Type: application/json

{"public":true}
```

Eine private Media-Asset-ID darf nicht von anderen Einwohnern allein per UUID abgerufen werden. Umgekehrt macht ein Medien-Upload nicht pauschal andere Inventarobjekte öffentlich.

**Marketplace-Bild veröffentlichen (NUR Backend):**

```http
PUT /api/v1/media/marketplace/{listingId}/image
X-NexVerse-Api-Key: <maschinenkey-mit-media:publish>
Content-Type: application/json

{"asset_id":"<registriertes-upload>","owner_id":"<lokaler-avatar-owner>"}
```

Die World API validiert die Zuordnung des Bilds zum registrierten Owner. **Die eigentliche Listing-ID und deren Besitzer/Berechtigung muss das Marketplace-Backend autoritativ prüfen, bevor es den signierten Publisher-Key benutzt.** Der World-Server stellt absichtlich kein eigenes zweites Marketplace-Verkaufs-/Kaufverzeichnis bereit; stattdessen hält er die sichere **Publikationsbindung Listing → Bild** in einer separaten Tabelle. `DELETE` entfernt sie wieder und der öffentliche URL-Abruf liefert 404.

## 4. Persistenz & Privatsphäre

Zwei neue Tabellen im bestehenden ROBUST-MariaDB-Schema werden automatisch angelegt:

- `ogl_media_uploads`: Texture-UUID, Avatar-Eigentümer, Original-MIME, Sichtbarkeit, Erstellungszeit
- `ogl_media_marketplace`: Listing-ID, zugeordneter Upload, Eigentümer und Aktualisierungszeit (FK für nur zuvor registrierte Uploads)

`[NexVerseWorldApi] EnableManagedMedia = true` (Standard bei MariaDB). Ist MariaDB oder der bereits aktive AssetService nicht verfügbar, verweigern die schreibenden Media-Endpunkte ihren Dienst mit 503; **World-Login und ältere Bildrouten bleiben nutzbar**. MariaDB-DB-Benutzer benötigt CREATE TABLE/SELECT/INSERT/UPDATE/DELETE. Für andere Datenbankanbieter gibt es keinen stillen SQLite-Fallback.

`media:publish` ist **maschinenkey-only**; es gehört nicht zu den automatisch zugewiesenen Einwohner-/Rollenrechten. Alle Browser- und Besitzerrechte werden bei jedem Request erneut geprüft. Gruppen- und First-Life-Bilder sind niemals allein wegen ihrer UUID öffentlich. Private Bilder werden nicht in öffentlichen CDN-Caches geteilt (`Cache-Control: private, no-store`). Die AssetService-Daten bleiben unverändert; **bestehende beliebige Texture-UUIDs können nicht unkontrolliert publiziert werden**.

Für JPEG2000-Encoding/Decoding muss die OpenJPEG-/Grafiklaufzeit auf ROBUST vorhanden sein. Ein fehlender nativer Encoder soll einen klaren 503 auslösen; kein korruptes PNG darf als OpenSim-Texture-Asset gespeichert werden.

## 5. Stadtportal-Arbeitsauftrag und Abnahme

1. Das Portal-Backend nutzt Einwohner-OAuth/Bearer für persönliche Bilder, einen dedizierten `media:publish`-Maschinenkey ausschließlich für autorisierte Marketplace-Uploads/Bildbindungen. Die **Marketplace-Konto-/Listing-Owner-Prüfung passiert im Portal**, bevor dieser privilegierte Key verwendet wird.
2. Hochladen als PNG/JPEG; neue `asset_id` aus 201 übernehmen. Profilsichtbarkeit respektieren. Gruppen- und Profil-JSON führen bereits `image_url` und `media_image_url`.
3. Listen-/Kachelübersichten nutzen die Media-URLs; Gruppendarstellung braucht `groups:read` und einen authentifizierten Kontext. **Keine geheimen Bearer-Tokens in img-src-Queryparametern**.
4. Einen frisch angelegten Testbewohner verwenden: Upload → Profil- und Gruppenbindung → Firestorm-Anzeige → GET PNG → private/public-Umschaltung.
5. Marketplace: eigenen Test-Listing-Eigentümer verifizieren → mit Maschinenkey hochladen/veröffentlichen → anonym GET 200 → DELETE → GET 404. URL-Manipulation, fremde Asset-UUIDs, falscher Owner, fremdes Inventaritem und fehlende Scopes müssen abgelehnt werden.
6. Nachträgliche Rechte-/Sichtbarkeitsänderungen, fehlende Assets und große/dekodierfehlerhafte Bilder testen. Backup/Restore der beiden Tabellen vorbereiten.

**Die GitHub-CI ersetzt diese Realitätsabnahme nicht. Kein produktives Aktivieren, bevor die End-to-End-Prüfungen erfolgreich sind.**
