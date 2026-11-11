# OpenGenesisLINK G1 – Second-Life Render Material LSL

Dieses Aenderungspaket implementiert
`llSetRenderMaterial(string material, integer face)`,
`llSetLinkRenderMaterial(integer link, string material, integer face)`
und `llGetRenderMaterial(integer face)` als native YEngine-LSL-Funktionen.

## Verhalten

- RenderMaterial-UUID oder Materialinventarname im Skript-Prim; andere
  Inventartypen werden nicht stillschweigend als Material akzeptiert.
- `ALL_SIDES` fuer Set; beim Get wird Face 0 verwendet.
- `NULL_KEY` entfernt die aktuelle glTF-Materialzuordnung.
- Linksets werden gemaess vorhandener `GetLinkParts`-Semantik aufgeloest;
  die drei neuen Methoden verwenden einen gemeinsamen, beschraenkten
  Copy-on-Write-Pfad `OglRenderMaterialFaces`.
- Das Ersetzen eines Materials loescht Farbe/Metall/Alpha-Overrides,
  behaelt die in `ti` gespeicherten UV-Transformationen
  (Repeats/Offset/Rotation) bei.
- Materialaenderungen triggern FullUpdate + MaterialOvr und
  Objektpersistenz. Die Setter verursachen jeweils 200 ms LSL-Sleep.
- Ohne volle Copy/Modify/Transfer-Rechte gibt die Getter-Funktion bei
  unbekannter Materialinventar-ID keinen Material-UUID preis.

## Grenzen

Der bestehende `PRIM_RENDER_MATERIAL=49`-Konstantenwert wird nicht
geaendert; eine separate Verhaltensparitaetspruefung fuer die
`PRIM_RENDER_MATERIAL`-Listenregel bleibt erforderlich. Neue Funktionen
allein beweisen keine SL-Paritaet fuer alle Material-Ownership-
und Permissions-Edge-Cases oder einen nativen .glb/.gltf-Uploader.

## Abnahme

```bash
dotnet run -c Release --project tools/ci/OglRenderMaterialLslRegression/OglRenderMaterialLslRegression.csproj
```

Zusatztests im echten Firestorm/TrixieStorm: direkte Material-UUID,
Material per Namen, ungueltiges Material, alle Faces, Linkset-Kindprim,
Eigentuemerwechsel, Relog, OAR und Hypergrid. Alle Faelle sollten
gegen die offizielle Second-Life-LSL-Dokumentation abgeglichen werden.
