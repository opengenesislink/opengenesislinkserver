# OpenGenesisLINK Generation 1: wolfvoice 0.2.0 (optionaler Backend-Modus)

## Status und Abgrenzung

wolfvoice ist ein **separater Rust-Medienserver**, nicht einfach eine austauschbare OGLVoice-URL. Seine Firestorm-CAPS kommen zwingend aus dem **gehärteten os-webrtc-janus-Regionsaddon**. wolfvoice spricht dessen JSON-RPC, SDP, DTLS/SRTP und LL-WebRTC-Datachannel; der vorhandene OGLVoice/LiveKit-Media-Exchange in NexVerse benutzt dagegen einen anderen Authentisierungs- und Signalisierungsvertrag. **Nicht einfach MediaGatewayUrl auf wolfvoice zeigen.**

Diese Integration liefert ein **reproduzierbares opt-in Modul-Build**, ein **sicherheitsgeprüftes Aktivierungs-Skript für einen Simulator** und Konfliktvermeidung für die Viewer-CAPS. Nichts wird automatisch auf produktiven Servern installiert, gestartet oder umgeschaltet. Die Quelltexte von wolfvoice und os-webrtc-janus werden **nicht** ungeprüft in den NexVerse-Quellbaum kopiert oder als Docker-Container gestartet.

Quellen / feste Revisionsstände:
- [wolfvoice](https://github.com/intelligentwolf/wolfvoice), main-Commit `f13ce3c462fd9524e2b3a89f897f7a034357c03d`, Cargo version 0.2.0, Apache-2.0.
- [gehärtetes Regionsaddon](https://github.com/intelligentwolf/os-webrtc-janus/tree/chatsession-p2p-session-id-and-fast-fail), Commit `7f08ce453fc5938beea12c452348d46b3ed1088e`, zwingend für authentifizierten Wolfvoice-Betrieb. Externe Lizenz beachten.

## Installation auf einer isolierten Generation-1-Entwicklungsregion

1. Auf einem **separaten Voice-Host** wolfvoice 0.2.0 aus geprüftem Quellstand bereitstellen, TLS-Zertifikat für öffentliche DNS-Adresse, extern UDP **40000–40999**, JSON-RPC TCP **9443 nur für Regionshosts**; **nie** RPC-Schlüssel an Viewer ausgeben. Vor Ausführung das externe `setup.sh` des Projekts prüfen.
2. **Zwei unabhängige Schlüssel** serverseitig erzeugen: `rpc_key` (Relay-Authentisierung) und `cred_key` (Tickets). Auf Voice-Host `/etc/wolfvoice/`, auf jedem Regionenhost z. B. `/etc/opensim/voice_rpc_key` und `/etc/opensim/voice_cred_key` für die OpenSim-Serviceidentität nur lesbar bereitstellen (Dateimodus 0400). Nicht in GitHub, INI, Log, Issue oder Chat veröffentlichen.
3. Im NexVerse-Quellbaum (mit vollständiger Quelle, `dotnet`, `git` und `bin/prebuild.dll`):
   ```bash
   bash tools/voice/install-wolfvoice-region-addon.sh fetch
   bash tools/voice/install-wolfvoice-region-addon.sh build
   ```
   Das Skript benutzt `addon-modules/os-webrtc-janus` und den **festen** gehärteten Revision-Stand. Ein bereits abweichender oder lokal modifizierter Checkout wird **nicht** überschrieben. Das standardmäßige `prebuild.xml` in NexVerse schließt `addon-modules/*/prebuild*.xml` automatisch ein. Erforderliche Ausgabedateien: `bin/WebRtcVoice.dll`, `bin/WebRtcVoiceServiceModule.dll`, `bin/WebRtcVoiceRegionModule.dll`.
4. Nach Bereitstellung von Voice-Host und Schlüsseln für den gewählten **Staging-Simulatorprozess**:
   ```bash
   bash tools/voice/configure-wolfvoice-region.sh https://voice.example.org:9443
   ```
   `voice.example.org` **durch den eigenen TLS-Host ersetzen**. Das Skript verweigert den Platzhalter und fehlende DLLs/Schlüssel; es schreibt ausschließlich die neue `bin/config/wolfvoice.ini` und überschreibt keine vorhandene Datei. Dabei wird `[OGLVoiceViewer] Enabled=false` gesetzt, um konkurrierende Firestorm-CAPS zu verhindern. `[WebRtcVoice] Enabled=true` schaltet die gehärtete Regionserweiterung ein. Legacy Vivox wird abgeschaltet. Nach Prüfungen **diesen** Simulator geordnet neu starten.
5. Estate und Parzelle **Allow Voice Chat** aktivieren. Mit Firestorm **7.1.10 oder neuer** (vorzugsweise aktueller Build) auf Staging testen: lokale Voice-Sitzung, Teilnehmerliste, positionsabhängiges Hören, Sprach-Mute, Regionwechsel, P2P-Text-IM und Gruppenruf. Die WebRTC-IM-CAPS des gehärteten Moduls sind wichtig, weil sonst P2P-Textnachrichten in kompatiblen Viewern blockieren können.

Ein Simulatorprozess kann mehrere Regionen hosten; in dem Fall wirkt `[WebRtcVoice]` auf alle Regionen im Prozess. Das Ziel ist ein bewusstes **entweder Wolfvoice oder OGLVoice** je Simulatorprozess, kein paralleler Betrieb zweier CAPS-Anbieter für denselben Viewer.

## Konfiguration nach dem Setup-Skript

```ini
[OGLVoiceViewer]
    Enabled = false

[WebRtcVoice]
    Enabled = true
    SpatialVoiceService = WebRtcVoice.dll:WebRtcVoiceServiceConnector
    NonSpatialVoiceService = WebRtcVoice.dll:WebRtcVoiceServiceConnector
    WebRtcVoiceServerURI = https://MEIN-VOICE-HOST:9443
    RpcKeyFile = /etc/opensim/voice_rpc_key
    CredKeyFile = /etc/opensim/voice_cred_key
    PositionFeed = true
    MessageDetails = false
    AcceptLegacyCallCredentials = false

[VivoxVoice]
    enabled = false
```

Diese Dateien müssen nach einem Update auf dem **laufenden** Simulator landen. Die beiden Schlüssel dürfen ausschließlich vom Regionsprozess lesbar sein. Die bestehende ROBUST-Verwaltung wird **nicht** auf wolfvoice umkonfiguriert.

## Betrieb, Status und Sicherheitsgrenzen

- Der Voice-Host antwortet ohne Relay-Schlüssel auf `https://MEIN-VOICE-HOST:9443/` lediglich mit `ok`. Die Regionshosts senden Authentisierungsheader zum eigentlichen Voice-Service; die Schlüssel gehören niemals in öffentliche Frontends.
- Bei Störung von wolfvoice bleibt der Simulator erreichbar, aber WebRTC-Sprachverbindungen scheitern. Bei IPv4/IPv6, NAT und Firewalls müssen die UDP-Medienports erreichbar sein.
- **Radegast-ng** ist durch die neue Viewer-Zulassung für **Login/Chat** erlaubt. Ob genau diese Radegast-ng-Version selbst den LL-WebRTC-Webclient beherrscht, ist getrennt zu testen; allein eine Viewer-Zulassung aktiviert kein Mikrofon.
- Das separate OGLVoice/LiveKit-Backend bleibt unangetastet; Wolfvoice ist **kein LiveKit-Ersatz über eine OGLVoice-URL**.
- End-to-end-Voice mit echten Firestorm-Clients, externem Rust-Medienserver und Netztransport ist **nicht durch den .NET-Gesamtbuild bewiesen** und benötigt eine Staging-Abnahme auf einem echten Regionshost.

## Rücknahme

`bin/config/wolfvoice.ini` im betroffenen Staging-Simulator entfernen bzw. `[WebRtcVoice] Enabled=false` setzen. Danach für die vorherige OGLVoice-Konfiguration `[OGLVoiceViewer] Enabled=true` oder bisherigen Default wiederherstellen und den Simulator neu starten. Die externen, lokal nicht versionierten Addon-Dateien und Schlüssel **nicht blind löschen**, wenn andere Regionen sie verwenden.
