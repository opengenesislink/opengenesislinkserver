# G1 Datenintegritaet – PBR/CAPS und Gruppenabfragen

Stand: 2026-11-11. Dieser Change ist ein **Code-Fix**, keine produktive
Datenbankmigration oder ein automatischer Versions-Rollout.

## PBR – Mutation erst nach Preflight

`MaterialsModule.ModifyMaterialParams` prueft eine begrenzte Zahl an
Materialaenderungen schon **vor** der ersten Objektmutation. Ungueltige
glTF-JSON-Overrides (Laenge, Typ, Grenzwert oder Texturindex) werden abgewiesen;
sie duerfen kein zuvor gueltiges `asset_id` hinterlassen. Fuer ein Gesicht
bleibt die vorhandene RenderMaterial-Struktur kompatibel.

Die Vorsondierung nimmt nur Objekte, fuer die die Viewer-Capability
Editierberechtigung besitzt. Damit werden unberechtigte Objekte weiterhin
uebergangen. Der glTF-Konverter prueft nun insbesondere
`baseColorFactor` (4 Werte), `emissiveFactor` (3 Werte),
`metallicFactor` und `roughnessFactor` (je 1 Wert) auf endliche,
numerische Komponenten von 0 bis 1.

**Abnahme**: `OglPbrMaterialRegression` plus echter Firestorm-Test mit
berechtigtem und unberechtigtem Objekt, Materialwechsel,
negativem Face-Index, erst gueltigem dann ungueltigem Request-Element,
Regionwechsel, Relog und SQL-Roundtrip. Die CAP ist weiterhin kein
vollstaendiger glTF-/GLB-Modellimport.

## Gruppen – bislang leere SQL-Methoden

`RetrievePrincipalGroupMembership` und
`RetrievePrincipalGroupMemberships` geben fuer MySQL/MariaDB
und PostgreSQL nicht mehr immer `null` zurueck, sondern fragen
die vorhandenen Membership-Tabellen ab. Ungueltige IDs ergeben
`null` bzw. ein leeres Array.

Kein Schemaeingriff, keine In-Place-Datenmigration. Die aelteren
`GetGroupRoles`-Berechtigungsfragen gehoeren in ein eigenes Security-Gate.

## Ausdruecklich nicht integriert

PR #414 umfasst separate PostgreSQL-Estate-/Profiltransaktionen.
Die bestehende Passwort-Recovery / Portal-Migration und die
FSAssets/OGL AssetService-Migration benoetigen isolierte
End-to-End-Abnahmen. Diese Arbeiten werden nicht nebenbei auf
einem produktiven Datenbestand durchgefuehrt.
