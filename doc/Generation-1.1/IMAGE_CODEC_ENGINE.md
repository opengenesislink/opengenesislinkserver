# OpenGenesisLINK Generation 1.1 – OGL Image Codec Engine (G11-02)

**Status (04.11.2026):** Opt-in Hybrid-Decoder mit OpenJPEG 2.5.4 wurde auf Linux x64 für **synthetische J2C- und JP2-Codestreams** erfolgreich getestet. Kein produktiver Rollout; kein vollständiger nativer JPEG2000-Decoder-Ersatz. Entwicklungsbranch: `GEN1.1NET10`.

## Architektur und Verantwortlichkeiten

- `OglImageCodecEngine.cs`: begrenzte Eingaben (max. 32 MiB), typisierte Fehler, unabhängiger primärer/fallback Codec, strikte Prüfung von Layergrenzen, keine erdachten Layerwerte.
- `OglOpenJpeg2CodecProvider.cs`: Hybrid aus **OpenJPEG 2.x** (vollständige native Codestream-Decodierung und Metadatenvalidierung) und **CSJ2K** (Ermittlung wirklicher JPEG2000-Layerpositionen). Bei Abweichung der Layerzahl oder ungültigen Byte-Offsets verwirft der Provider den Decode. JP2-Boxen werden mit Längenprüfung gelesen und der darin enthaltene J2C-Codestream an den Layerparser übergeben. Vorläufig nur JP2-Dateien mit letzter `jp2c`-Box.
- `addon-modules/OGLImageCodec/native/ogl_openjpeg2.c`: kleiner Native-ABI-Adapter über die OpenJPEG-2.x-C-API. Keine Zeiger auf native Speicherobjekte werden an .NET zurückgegeben; der Decoder akzeptiert nur einen einzelnen LRCP-Tile mit höchstens 1024 Layern, 5 Komponenten, 8192 Pixeln pro Achse und 32 Mio. Pixeln. Beschädigte oder nicht unterstützte Codestreams liefern einen Fehler.
- `J2KDecoderModule.cs`: schaltet den experimentellen Pfad mittels Konfiguration hinzu; bei fehlender nativer Bibliothek wird eine Warnung protokolliert und der CSJ2K-Kompatibilitätspfad bleibt erreichbar.
- `OglCsJ2KCodecProvider`: Kompatibilitäts-Fallback; der bisherige produktive G1-Pfad bleibt unverändert.

Wichtig: Die öffentliche OpenJPEG-2.x-Codestream-Index-API stellt beim Decodieren keine nutzbaren Paketpositionen bereit. **OpenJPEG validiert deshalb, CSJ2K berechnet die Layergrenzen.** Die Implementierung gibt nicht vor, dass der native Codec die Offsets eigenständig ermittelt. Die Bildausgabe (Pixel/Alpha/Farbprofile) bleibt bis zur separaten plattformübergreifenden ABI-Validierung im bestehenden verwalteten Pfad.

Bestehende UUIDs, JPEG2000-Texturen, Inventare, Map-/Mesh-/Sculpt-Daten, Viewer-Pakete und Hypergrid-Protokolle werden nicht konvertiert. HTJ2K wird nicht eingeführt. Erfolgreiche Layerdecodes können nur in den gesonderten zeitlich begrenzten Cache mit SHA-256-Inhaltsfingerabdruck übernommen werden; die alten UUID-basierten Layercaches werden vom neuen Pfad weder gelesen noch beschrieben.

## Native Bibliothek bauen (nur Linux-Staging)

```bash
# Ubuntu/Debian, Version mit OpenJPEG >= 2.5 verwenden
sudo apt-get install -y build-essential pkg-config libopenjp2-7-dev
bash addon-modules/OGLImageCodec/build.sh

# Ausgabe je nach Architektur:
# bin/lib64/libogl-openjpeg2-x86_64.so
# bin/lib64/libogl-openjpeg2-arm64.so
```

Die native Bibliothek wird **nicht** vom normalen G1-Serverbuild erstellt und nicht mit dem Produktionsserver verteilt. OpenJPEG-2.x-Runtime (`libopenjp2.so.7`) ist auf dem Zielsystem erforderlich. Ein Austausch von `OpenMetaverse.dll`, globale `LD_LIBRARY_PATH`-Workarounds oder System-Symlinks sind nicht erforderlich. Die dynamische Native-Bibliothek wird nur unter Linux x64/ARM64 gesucht; Windows nutzt vorerst den vorhandenen Kompatibilitäts-Fallback.

Die Datei kann für Tests alternativ über `OGL_IMAGE_CODEC_NATIVE_DIR` in einem absoluten Verzeichnis bereitgestellt werden. Nach Installation ist ein Neustart notwendig, weil die native Verfügbarkeit beim ersten Zugriff einmalig ermittelt wird.

## Nur im Staging aktivieren

```ini
[Startup]
OglImageCodecEnabled = true
```

**Standard: `false`.** `UseCSJ2K` steuert den ursprünglichen Decoder, solange `OglImageCodecEnabled=false` ist. Ein aktiver neuer Codec ohne native Bibliothek ist **kein** Nachweis einer erfolgreichen OpenJPEG2-Migration: Logmeldung zur Native-Verfügbarkeit kontrollieren. `main` und produktive INI-Dateien nicht automatisch ändern.

## CI und nachgewiesene Tests

`.github/workflows/gen11-net10.yml` generiert eine deterministische 64×64-PGM-Datei und kodiert sie mit `opj_compress` als J2C und JP2. `opj_dump` verifiziert die Dateien. Beide Formate durchlaufen den verbindlichen Hybrid-Regressionstest:

```bash
dotnet run -c Release --project tools/ci/OglImageCodecRegression/OglImageCodecRegression.csproj
# Mit gebautem Native-Adapter und vorhandenem Testasset:
OGL_IMAGE_CODEC_NATIVE_DIR="$PWD/bin/lib64" dotnet run -c Release \
  --project tools/ci/OglImageCodecRegression/OglImageCodecRegression.csproj -- \
  --hybrid /pfad/zu/test.j2k
```

Geprüft werden Provider-Fallback, dekodierte Layerzahl und -Bytepositionen, Input-Limits, Null-/Korrupt-/Trunkierungsfälle, Rückgabeverträge sowie die Kompilierung unter .NET 10. Das Testmaterial ist synthetisch und deckt **keine** echten Legacy-NexVerse-Assets oder alle JPEG2000-Progressionen ab.

Die alte `OpenMetaverse.Imaging.OpenJPEG.DecodeLayerBoundaries`-Implementierung schlägt bei diesem gültigen J2C-Test weiterhin fehl und wird separat unter `continue-on-error` dokumentiert; diese Diagnose ist **kein** bestandenes Release-Gate. Der neue OpenJPEG2-Hybridtest ist dagegen verbindlich.

## Verbleibende Freigabegates

1. Reale, anonymisierte NexVerse-J2C/JP2-Assets einschließlich beschädigter Bestände; Layer-/Pixelparität zu Firestorm und TrixieStorm nachweisen.
2. Native-ABI, Build und Runtime auf Linux ARM64 sowie Windows x64 verifizieren; neue native Pixeldecodierung und Farb-/Alpha-Pfade erst dann freigeben.
3. Fuzzing, Speicher-/CPU-Profil, P95-Latenzen, Parallelität, Timeout- und Ressourcenbegrenzungen über die derzeitigen Eingabe-/Bildgrößenlimits hinaus.
4. Unmittelbare `OpenJPEG.DecodeToImage`-Aufrufe in WorldMap, GetTexture, DynamicTexture, Warp3D/LegacyMap, Meshing und World API separat migrieren.
5. Migrations- und Rollbacktests inklusive Login, Teleport, Karten, Texturen, Sculpt/Mesh, Inventar, Hypergrid, Datenbank und AssetService.

Tracking: [GitHub Issue #377](https://github.com/opengenesislink/opengenesislinkserver/issues/377). Kein automatischer Merge nach `main`, keine Änderung produktiver Dienste.
