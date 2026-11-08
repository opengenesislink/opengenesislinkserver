# OpenGenesisLINK G1 – Mesh Land Impact (opt-in)

## Zweck

OpenSimulator zaehlt bei Landlimits standardmaessig jedes verlinkte Objektteil
als ein Prim. In Second Life ist die **Land Impact (LI)**-Quota fuer Mesh-Objekte
separat von der Anzahl der editierbaren Teile. Dieses Feature fuehrt eine
**eigene, abschaltbare OGL-Annaeherung** ein; es ist kein identischer
Linden-Lab-Land-Impact-Algorithmus.

## Verhalten

- Ohne Mesh im Linkset: klassischer PrimCount unveraendert.
- Bei Mesh im Linkset: **max(download/streaming, physics, server)** wird
  berechnet und fuer das gesamte Linkset **einmal aufgerundet**, mindestens 1.
- Download-Kosten: dieselbe gewichtete LOD-Kompressionsgroessen-Formel wie
  der vorhandene Mesh-Upload-Kostenrechner (`MeshCost.cs`), inkl. Objektgroesse.
- Physik: vorhandener Convex-Hull-Vertex-Faktor (0,04 pro Vertex) und
  die groesseren vorhandenen PhysicsActor-Kosten; `PhysicsShapeType = Prim`
  hat konservativ mindestens 1 pro Teil.
- Servergewicht: statische nichtphysische Linksets mit 0 Skripten
  **0,5 pro Teil**, Skriptzuschlaege, physische Linksets mindestens 1 pro Teil.
- Komplexe Meshes, groessere Objekte, aufwendige Physik oder Skripte
  koennen **mehr** LI kosten als vorher.
- Beispielsweise koennen 10 kleine, optimierte und ungeskriptete Mesh-Teile
  5 LI kosten; das ist **kein garantierter 50-%-Rabatt**.
- Fehlende, unlesbare, uebergrosse oder ungueltige Mesh-Assets erhalten
  keinen Rabatt. Die Asset-Header werden begrenzt und pro Region gecacht.
- Die physische Anzahl der Teile (`SceneObjectGroup.PrimCount`),
  `llGetNumberOfPrims`, `llGetLinkNumber`, die Linkset-Bearbeitung und
  alle gespeicherten Objekte bleiben unveraendert.

## Konfiguration

```ini
[OGLMeshLandImpact]
    Enabled = true
```

- `bin/OpenSimDefaults.ini`: **false** (sicherer genereller Default).
- `bin/OpenSim.ini.example`: **false** fuer neue/nicht getestete Grids.
- `bin/OpenSim.ini`: **true** fuer die Stadt-NexVerse-Quellkonfiguration.
- Aktive Betreiber-INI wird beim Deployment nicht ohne Weiteres
  automatisch ersetzt: Einstellung der produktiven `OpenSim.ini`
  kontrollieren und Simulator neu starten.

## Abrechnungs-Integration

`PrimCountModule` verwendet LI fuer Parzellen, Eigentuer-/Benutzer-Kontingente
und simweite Kapazitaeten. `LandObject` meldet den passenden LI in der
Eigentuemeranzeige. `PrimLimitsModule` prueft beim Duplizieren und
Parzellenwechsel dieselbe Belastung. Normale Inventar- und
JsonStore-Rezpfade verwenden die neue Zahl fuer die Berechtigungspruefung.
GetObjectCost- und ResourceCostSelected-CAPS liefern die berechneten
Linkset-Kosten, ohne die echten Linknummern zu veraendern.

## Grenzen und Rollout

1. **Nicht identisch mit Second Life**: Die Download- und
   Convex-Physik-Faktoren basieren auf den bestehenden OpenGenesisLINK-
   Uploader-Daten, nicht auf einem exakten proprietaeren SL-Modell.
2. Ein ueber mehrere Mesh-Teile direkt aus dem Viewer angelegtes Objekt
   wird in manchen alten UploadObject-CAPS-Pfaden schon pro einzelnem
   Teil geprueft; ein Folgeaudit dieser Legacy-Verkettung ist sinnvoll.
3. Tests vor Aktivierung: Inventar-Rez, Hochladen, Drag-Drop, Copy,
   Script-Rez, Link/Delink, Skalierung, Skript hinzufuegen/entfernen,
   physisch/nichtphysisch, Objekt mit fehlendem Mesh-Asset, Parzellen-
   und Regionslimit sowie Viewer-Mehr-Info-Dialog.
4. Vor Aktivierung Primbudget und Landnutzung sichern. Die Umstellung
   kann bestehende Mesh-Bauten neu bewerten.
5. Im Fehlerfall `Enabled = false` setzen und Simulator neu starten:
   danach gilt die bisherige Primzahl pro Linkset.
6. Kein Aendern von OAR/IAR-Archiven, persistenten Regionobjekten oder
   LSL-/Viewer-Protokollen.

## Tests

`dotnet run --configuration Release --project tools/ci/OglMeshLandImpactRegression/OglMeshLandImpactRegression.csproj`
und `python3 tools/ci/verify_ogl_mesh_land_impact.py`, plus G1 .NET10
Linux/Windows/ARM64 sowie NexVerse-Integrations-CI.

Bei hohen echten Mesh-Asset-Komplexitaeten werden abschliessende
Inworld-Vergleiche gegen Second Life separat benoetigt.
