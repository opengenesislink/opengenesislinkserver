# OpenGenesisLINK Generation 1.1 – OGL Image Codec Engine (G11-02)

**Status (04.11.2026):** Opt-in Hybrid-Decoder wurde mit nativen JPEG2000-Smoketests auf **Linux x64, Linux ARM64 und Windows x64** erfolgreich gegen synthetische J2C-/JP2-Dateien geprüft. Die Runner verwendeten OpenJPEG 2.5.0 (ARM64) bzw. OpenJPEG 2.5.4 (Linux x64/Windows). **Keine Produktionsfreigabe**: echte NexVerse-Assets, Pixel- und Viewer-Interoperabilität sowie Performance/Last müssen noch abgenommen werden. Branch: `GEN1.1NET10`.

## Architektur und Verantwortlichkeiten

- `OglImageCodecEngine.cs`: begrenzte Eingaben (max. 32 MiB), typisierte Fehler, unabhängiger primärer/fallback Codec, strikte Prüfung von Layergrenzen, keine erdachten Layerwerte.
- `OglOpenJpeg2CodecProvider.cs`: Hybrid aus **OpenJPEG 2.x** (vollständige native Codestream-Decodierung und Metadatenvalidierung) und **CSJ2K** (Ermittlung wirklicher JPEG2000-Layerpositionen). Bei Abweichung der Layerzahl oder ungültigen Byte-Offsets verwirft der Provider den Decode. JP2-Boxen werden mit Längenprüfung gelesen und der darin enthaltene J2C-Codestream an den Layerparser übergeben. Vorläufig nur JP2-Dateien mit letzter `jp2c`-Box.
- `addon-modules/OGLImageCodec/native/ogl_openjpeg2.c`: kleiner Native-ABI-Adapter über die OpenJPEG-2.x-C-API. Keine Zeiger auf native Speicherobjekte werden an .NET zurückgegeben; der Decoder akzeptiert nur einen einzelnen LRCP-Tile mit höchstens 1024 Layern, 5 Komponenten, 8192 Pixeln pro Achse und 32 Mio. Pixeln. Beschädigte oder nicht unterstützte Codestreams liefern einen Fehler.
- `J2KDecoderModule.cs`: schaltet den experimentellen Pfad mittels Konfiguration hinzu; bei fehlender nativer Bibliothek wird eine Warnung protokolliert und der CSJ2K-Kompatibilitätspfad bleibt erreichbar.
- `OglCsJ2KCodecProvider`: Kompatibilitäts-Fallback; der bisherige produktive G1-Pfad bleibt unverändert.

Wichtig: Die öffentliche OpenJPEG-2.x-Codestream-Index-API stellt beim Decodieren keine nutzbaren Paketpositionen bereit. **OpenJPEG validiert deshalb, CSJ2K berechnet die Layergrenzen.** Die Implementierung gibt nicht vor, dass der native Codec die Offsets eigenständig ermittelt. Die Bildausgabe (Pixel/Alpha/Farbprofile) bleibt bis zur separaten plattformübergreifenden ABI-Validierung im bestehenden verwalteten Pfad.

Bestehende UUIDs, JPEG2000-Texturen, Inventare, Map-/Mesh-/Sculpt-Daten, Viewer-Pakete und Hypergrid-Protokolle werden nicht konvertiert. HTJ2K wird nicht eingeführt. Erfolgreiche Layerdecodes können nur in den gesonderten zeitlich begrenzten Cache mit SHA-256-Inhaltsfingerabdruck übernommen werden; die alten UUID-basierten Layercaches werden vom neuen Pfad weder gelesen noch beschrieben.

## Native Bibliothek bauen (Linux- und Windows-Staging)

```bash
# Ubuntu/Debian, Version mit OpenJPEG >= 2.5 verwenden
sudo apt-get install -y build-essential pkg-config libopenjp2-7-dev
bash addon-modules/OGLImageCodec/build.sh

# Ausgabe je nach Architektur:
# bin/lib64/libogl-openjpeg2-x86_64.so
# bin/lib64/libogl-openjpeg2-arm64.so
```

Windows x64: Visual Studio/MSVC, CMake, PowerShell 7 und vcpkg werden benötigt. Der Windows-Build bindet `openjpeg:x64-windows-static` mit passender statischer MSVC-Runtime ein; der neue Adapter benötigt deshalb auf Windows keine separat ausgelieferte `libopenjp2.dll`.

```powershell
./addon-modules/OGLImageCodec/build-windows.ps1
# Ausgabe: bin/lib64/ogl-openjpeg2-x86_64.dll
```

Die neuen Native-Bibliotheken werden **nicht** vom normalen G1-Serverbuild erzeugt oder automatisch auf Produktivserver verteilt. Unter Linux muss die OpenJPEG-Runtime (`libopenjp2.so.7`) auf dem Zielsystem vorhanden sein. Ein Austausch von `OpenMetaverse.dll`, globale `LD_LIBRARY_PATH`-Workarounds oder System-Symlinks sind nicht erforderlich. Die `lib64`-Dateien für alle drei Plattformen stehen als befristete GitHub-Actions-Artefakte bereit; vor einem Staging-Rollout Release-Datei, Plattform und Native-Abhängigkeiten überprüfen.

Die Datei kann für Tests alternativ über `OGL_IMAGE_CODEC_NATIVE_DIR` in einem absoluten Verzeichnis bereitgestellt werden. Nach Installation ist ein Neustart notwendig, weil die native Verfügbarkeit beim ersten Zugriff einmalig ermittelt wird. Im Startlog muss die tatsächlich geladene native OpenJPEG2-Bibliothek bestätigt werden; eine Fallback-Meldung ist **kein** bestandener Native-Rollout.

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

Die CI prüft auf **Linux x64, Linux ARM64 und Windows x64** mit real erzeugten, aber synthetischen J2C-/JP2-Codestreams die native Bibliothek und die Layergrenzen. Zusätzlich laufen 20 Provider-/Fallback-Assertions, je **168 deterministische native Mutationstestfälle** sowie ein schreibgeschützter Korpustest mit zwei synthetischen Dateien. Die Windows-Matrix validiert die gleichen Dateien, die zuvor unter Linux erzeugt wurden. Diese Regressionen ersetzen **weder vollständiges Fuzzing noch Tests mit echten Legacy-NexVerse-Assets, Pixelvergleichen und Viewer-Szenarien**.

### Echte Assets im Staging prüfen (read-only)

```bash
# Native-Bibliothek muss zuvor für die jeweilige Zielplattform gebaut worden sein
export OGL_IMAGE_CODEC_NATIVE_DIR="$PWD/bin/lib64"
dotnet run -c Release --project tools/ci/OglImageCodecRegression/OglImageCodecRegression.csproj -- \
  --corpus /pfad/zu/read-only-testassets
```

Windows PowerShell: `$env:OGL_IMAGE_CODEC_NATIVE_DIR = (Resolve-Path "./bin/lib64").Path` setzen und denselben `dotnet run ... -- --corpus "D:\Staging\Texturen"`-Aufruf verwenden.

Akzeptiert werden `.j2k`, `.j2c` und `.jp2` (maximal 2.000 Dateien je Testlauf, maximal 32 MiB pro Datei). Der Test liest nur, schreibt keine Assets und zeigt in den Ergebnissen Hash-Präfixe statt Dateinamen. **Mindestens eine Datei muss vorhanden sein.** Ungültige bzw. nicht unterstützte Texturen führen zu einem Fehlerstatus. Für Releasefreigabe einen repräsentativen, berechtigten und anonymisierten Korpus testen; vertrauliche Inventardaten nicht in GitHub Actions hochladen.

Die alte `OpenMetaverse.Imaging.OpenJPEG.DecodeLayerBoundaries`-Implementierung schlägt bei diesem gültigen J2C-Test weiterhin fehl und wird separat unter `continue-on-error` dokumentiert; diese Diagnose ist **kein** bestandenes Release-Gate. Der neue OpenJPEG2-Hybridtest ist dagegen verbindlich.

## Verbleibende Freigabegates

1. Reale, anonymisierte NexVerse-J2C/JP2-Assets einschließlich beschädigter Bestände; Layer-/Pixelparität zu Firestorm und TrixieStorm nachweisen.
2. **Native-ABI-Basis unter Linux ARM64 und Windows x64 im CI bestätigt.** Offen: Zielserver-Runtime und Native-Paketierung außerhalb des CI, native Pixeldecodierung sowie Farb-/Alpha-Pfade.
3. Deterministische Mutations-Smokes bestanden; offen bleiben vollständiges Fuzzing, Speicher-/CPU-Profil, P95-Latenzen, Parallelität, Timeout- und Ressourcenbegrenzungen über die derzeitigen Eingabe-/Bildgrößenlimits hinaus.
4. Unmittelbare `OpenJPEG.DecodeToImage`-Aufrufe in WorldMap, GetTexture, DynamicTexture, Warp3D/LegacyMap, Meshing und World API separat migrieren.
5. Migrations- und Rollbacktests inklusive Login, Teleport, Karten, Texturen, Sculpt/Mesh, Inventar, Hypergrid, Datenbank und AssetService.

Tracking: [GitHub Issue #377](https://github.com/opengenesislink/opengenesislinkserver/issues/377). Kein automatischer Merge nach `main`, keine Änderung produktiver Dienste.
