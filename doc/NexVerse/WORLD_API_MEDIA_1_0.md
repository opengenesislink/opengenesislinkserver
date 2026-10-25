# OpenGenesisLINK Media API 1.0 – Gruppen-/Profilbilder

Stand: 25.10.2026. Phase 1: **read-only** und durch Entitätszuordnung abgesichert. Für World-/ROBUST-Versionen mit diesem Feature-Commit; vor einem Deployment als nicht verfügbar behandeln.

## HTTP-Endpunkte

| Methode | Endpunkt | Schutz |
|---|---|---|
| GET | `/api/v1/media/groups/{groupId}/image` | `groups:read` (inkl. serverseitiger Gruppenreferenzprüfung) |
| GET | `/api/v1/media/profiles/{principalId}/image` | öffentlich nur bei `profileAllowPublish`; sonst `profile:read` und Owner/Admin |
| GET | `/api/v1/media/profiles/{principalId}/first-life` | **immer** `profile:read` und Avatar-Owner/Admin |

Die vorhandene Route `GET /api/v1/profiles/{principalId}/image` bleibt unverändert.

Alle drei Media-Endpunkte akzeptieren `?size=64|128|256|512` als optionale maximale Pixelkante. Ohne `size` wird das Original (PNG/JPEG) oder die JPEG2000→PNG-Konvertierung ausgegeben; mit `size` wird nur verkleinert, nicht vergrößert. Antwort ist ein **echtes Bild** (`image/png` oder `image/jpeg`), **kein JSON**. Es gilt `Cache-Control: private, no-store`; keine Freigabe an geteilte CDNs. Eine optionale Größe garantiert nicht automatisch Zugriff.

### Beispiele

```http
GET /api/v1/media/groups/00000000-0000-0000-0000-000000000001/image?size=256
Authorization: Bearer <resident-token-with-groups:read>
```

```http
GET /api/v1/media/profiles/00000000-0000-0000-0000-000000000002/image?size=128
```

```http
GET /api/v1/media/profiles/00000000-0000-0000-0000-000000000002/first-life
Authorization: Bearer <owner-token-with-profile:read>
```

**Die Beispiel-UUIDs sind Platzhalter**, keine gültigen Konten oder Gruppen. Öffentliche Profilbilder dürfen ohne Bearer gelesen werden; Gruppen- und First-Life-Bilder benötigen Credentials.

### Referenzen in Entitäts-JSON

`GET /api/v1/groups/{groupId}` liefert künftig zusätzlich im `group`-Objekt `insignia_id` und die **relative** `image_url`. Beim Avatarprofil bleiben `profile_image_url` und `first_life_image` bestehen; neu: `media_image_url` und `first_life_image_url`. Ein nicht gesetztes Bild ergibt `null`. URLs sind nur Referenzen: Zugriffsrechte werden beim Abruf erneut geprüft.

Die Gruppe kann weiterhin eine nicht veröffentlichte Insignie besitzen. Der Media-Endpunkt benötigt daher ausdrücklich `groups:read`; **Browser ohne Resident-Session erhalten 401**, auch wenn die Gruppe im Viewer angezeigt wird. Das Portal muss autorisierte Anfragen über seine Backend-/Session-Infrastruktur leiten. Ein öffentlicher Bildzugriff wäre eine gesondert abzusichernde Produktentscheidung.

### Fehlercodes

- 200 Bilddaten; 400 ungültige Größe.
- 401/403 nicht authentifiziert bzw. erforderlicher Scope fehlt.
- 404 ungültige Entität, nicht freigegebene oder fehlende Bildreferenz.
- 405 nur GET erlaubt; 413 Bilddaten bzw. Dimensionen überschreiten Grenzwerte.
- 415 Bildtyp/Bilddaten nicht dekodierbar; 503 Profil-, Gruppen- oder AssetService nicht erreichbar.

### Speicher-/Sicherheitsarchitektur

- Referenzauflösung ausschließlich über `GroupsService.GetGroupRecord(...).GroupPicture` bzw. `IProfilesData.GetAvatarProperties(...).ImageId/FirstLifeImageId`, **niemals** aus URL-Asset-ID, Browser-Headern oder nicht verifizierten Clientparametern.
- Derselbe von ROBUST registrierte `AssetServiceConnector.GetLocalService(...)` wie die bestehende World API; **kein neuer AssetService, keine Datenbank und keine Asset-Kopie**.
- Nur bildfähige Assettypen; maximal 8 MiB vor der Decodierung; gerenderte maximale Kante 2048 Pixel. JPEG2000-Konvertierung nutzt den OpenJPEG-Dekoder.
- First-Life-Bilder sind selbst bei öffentlichem Avatarprofil nur für Owner/Admin auslieferbar. Keine Secrets in Querystrings. Keine Bearer/API-Keys an fremde Bild-CDNs weitergeben.
- Kein `GET /api/v1/media/assets/{assetId}`: Eine Asset-UUID ist **kein** Leseberechtigungsnachweis. Marketplace, Inventar und freie Asset-IDs bleiben für eine später explizit geprüfte Autorisierung/Veröffentlichung zurückgestellt.

### Deployment-/Akzeptanzgrenzen

Funktioniert nur, wenn World API und die bereits konfigurierten `UserProfilesService`-/`NexGroups`-Module sowie ROBUST AssetService bereit sind. Gruppenbilder benötigen den eingeschalteten NexGroups-Dienst. Kompatibilitäts- und Sicherheitsregressionen werden im GitHub-Actions-Workflow ausgeführt; danach Bildabrufe, fehlende Assets, Fehlerfälle und Berechtigungswechsel im Staging-Portal/Firestorm verifizieren.

**Nicht implementiert in Phase 1:** Asset-Rohdownloads, Marketplace-Produktbilder, Upload neuer Bilder, Public-Gruppenbilder ohne Authentifizierung, persistenter Thumbnail-Cache oder CDN-Publication.
