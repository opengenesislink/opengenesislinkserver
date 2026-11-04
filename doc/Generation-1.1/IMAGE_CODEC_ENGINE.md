# Generation 1.1 — OGL Image Codec Engine (G11-02, Phase 1)

**Stand:** Experimenteller, opt-in Codec-Pfad im Simulator; kein produktives Rollout und noch kein Austausch der nativen OpenJPEG-Bibliothek. Branch: `GEN1.1NET10`.

## Ziel und Kompatibilität

Das Wire-/Asset-Format bleibt JPEG2000 (J2C/J2K bzw. kompatibles JP2). Vorhandene Asset-UUIDs, Inventare, Map-/Sculpt-/Mesh-Daten, Viewer- und Hypergrid-Protokolle werden nicht umgewandelt. Eine alternative Decoding-Engine darf nicht behaupten, korrupte Codestreams verlustfrei wiederherstellen zu können. Insbesondere bleibt HTJ2K vorerst **kein** Standardformat.

Die Implementierung befindet sich in:

- `OpenSim/Region/CoreModules/Agent/TextureSender/OglImageCodecEngine.cs` — `IOglImageCodecProvider`, OpenJPEG- und CSJ2K-Adapter, Begrenzungen, Fehlerkategorien, strikte Layerprüfungen.
- `OpenSim/Region/CoreModules/Agent/TextureSender/J2KDecoderModule.cs` — opt-in-Anbindung an das bestehende `IJ2KDecoder`-Interface.

### Aktivierung nur in einer G1.1-Testumgebung

```ini
[Startup]
OglImageCodecEnabled = true
```

`false` ist der vorläufige Standard. `UseCSJ2K` steuert weiterhin den bisherigen G1-Pfad, solange die neue Engine deaktiviert ist.

Bei aktiver Engine wird **OpenJPEG zuerst** angesprochen. Schlägt das Decoding fehl, versucht die Engine den vorhandenen CSJ2K-Provider. Eingabedaten über 32 MiB werden in diesem Pfad vor dem nativen Decode abgewiesen. Die Engine erfindet bei einem Fehler keine Layergrenzen; fehlgeschlagene Layer-Decodes erhalten einen leeren Satz und werden nicht in den bestehenden Decode-Cache geschrieben. Erfolgssignale werden nicht aus Fallback-Schätzungen abgeleitet.

Dieses Verhalten gilt **nur** für den neuen Pfad. Die ältere Decoderimplementierung und ihre bestehenden Fallbacks bleiben für G1-Kompatibilität zunächst unangetastet.

## Derzeit nicht abgedeckt

Das Interface wird zunächst vom `J2KDecoderModule` verwendet. Unmittelbare `OpenJPEG.DecodeToImage`-Aufrufe bestehen unter anderem in WorldMap, Warp3D/LegacyMap, DynamicTexture, Sculpt/Meshing, GetTexture und der World API. Diese müssen nach einer vollständigen Daten- und Testinventarisierung kontrolliert migriert werden.

Die vorhandene native OpenJPEG/CSJ2K-Bibliothek bleibt in dieser Phase unverändert. Native ABI-Sicherheit, ARM64-Paketierung, Speicher- und CPU-Limits für **dekomprimierte** Pixel, Hang-/Timeout-Abbruch sowie Fuzzing benötigen noch eigene Implementierung und Nachweise. Die 32-MiB-Grenze ist kein Schutz gegen alle Dekompressionsbomben.

## Abnahme vor produktiver Umschaltung

1. Echte valide J2C/JP2, historische Grenzfälle und kaputte Assets: Decode-Erfolg und Pixel-/Layer-Parität gegen die bisherige Produktionsbaseline.
2. Firestorm/TrixieStorm: Texturen, Quality-Layer/Mip-Streaming, Sculpties, Mesh, Terrain, Karten und Profilbilder.
3. Linux x64, Linux ARM64, Windows x64: native Ladefähigkeit, keine Crashes, kontrollierte Fehlermeldungen und begrenzter Speichereinsatz.
4. Belastungs- und Fuzzing-Tests mit parallelen Texturanfragen; überprüfbare Datenintegrität.
5. Kein Codec-Rollout in G1-`main`; separater Staging-Test, Feature-Flag und Rollback.

Automatischer funktionaler Unit-/Regressionstest: `dotnet run -c Release --project tools/ci/OglImageCodecRegression/OglImageCodecRegression.csproj`. Der Test prüft Input-Grenzen, Primär-/Fallback-Orchestrierung und abgelehnte Layergrenzen mit Test-Providern. Er ersetzt noch **keinen** Real-Asset-, Native-Interop- oder Viewer-Test.
