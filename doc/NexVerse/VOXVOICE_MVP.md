# VoxVoice MVP – externer WebRTC-Dienst fuer OpenGenesisLINK

Status: **separater Entwicklungs-Branch / NICHT produktiv abgenommen**. Diese Integration ist ein Opt-in ueber `[VoxVoice]` und ersetzt OGLVoice auf konfigurierten Simulatoren auf Ebene der Firestorm-CAPS. Das alte OGLVoice-Modul und die vorhandene Go-Pion/LiveKit-Medienengine bleiben aus Migrationsgruenden im Repository. Keine bestehenden Produktionssysteme werden automatisch umgeschaltet.

## Umfang

- Go-Prozess nutzt die bereits vorhandene Firestorm-WebRTC-Media-Bridge auf Basis von Pion und LiveKit; **kein SIP-Daemon, keine Viewer-Erweiterung, kein zweiter WebRTC-Stack**.
- `https://voice.opengenesislink.de/`: Registrierung mit E-Mail-Adresse, SMTP-Versand von zufaellig erzeugten `vox_user` / `vox_password`, Login, persoenliches Dashboard.
- Signierte SIM-Registrierung bei `RegionLoaded`: `POST /internal/voxvoice/v1/regions` mit Region-UUID und HMAC/Nonce/Time. Eine Region erscheint daher auch ohne aktiven Sprecher im Dashboard.
- Verbundene Avatare erscheinen nach ihrem ersten erfolgreichen Voice-Offer mit UUID. Eine gruen/rote Statusanzeige ist an den tatsaechlichen Pion-Verbindungsstatus gebunden, nicht an den Avatar-Online-Status; ein zusaetzliches `Spricht`-Label zeigt von LiveKit beobachtete Sprecherereignisse. Bei einem alleinigen Teilnehmer kann VAD unbekannt bleiben.
- Kein Avatarpasswort und keine Viewer-Weitergabe von VoxVoice-Zugangsdaten. Nur der Regionsserver signiert Requests.
- Konten und bekannte Regionen/Avatar-UUIDs werden in einer atomar ersetzten lokalen JSON-Datei (0600) persistiert. Registrierung und Dashboard sind fuer **eine einzelne Go-Instanz** vorgesehen, nicht fuer mehrere Media-Nodes mit gemeinsamem Accountstore.
- Portal-Betrieb und bisheriger OGLVoice-Betrieb koennen beim Deployment getrennt aktiviert werden.

## OpenSim.ini

```ini
[VoxVoice]
    enabled = true
    vox_server = voice.opengenesislink.de
    vox_sip_uri = voice.opengenesislink.de
    vox_user = <vom-Portal-zugewiesener-benutzer>
    vox_password = <vom-Portal-zugewiesenes-passwort>

[WebRtcVoice]
    Enabled = false

[VivoxVoice]
    enabled = false
```

`vox_sip_uri` wird ausschliesslich aus Kompatibilitaetsgruenden akzeptiert und **nicht fuer SIP verwendet**. Audio laeuft ueber WebRTC (ICE, DTLS/SRTP und SCTP). Der `vox_server` wird als HTTPS-Ziel genutzt. Der C#-Adapter leitet intern `SHA256(vox_password)` als 64-stelligen lowercase-Hexwert ab und verwendet ihn als HMAC-Schluessel; die Go-Anwendung speichert nur diesen abgeleiteten Wert. Die INI ist wegen des maschinellen Passworts auf `0600` zu setzen bzw. gegen andere Systembenutzer zu schuetzen. Zugangsdaten niemals in Git einchecken.

Wegen gemeldeter ICE-Probleme mit **Firestorm 7.2.4.80712** sollte im Simulator auf Staging zusaetzlich Folgendes geprueft werden:

```ini
[Startup]
    StunServers = stun.l.google.com:19302, stun.cloudflare.com:3478
```

Beim Einsatz von Firestorm 7.2.5.81788 ist derselbe Initialisierungsweg separat zu testen. Die WebRTC-Unterstuetzung des Viewers allein ist kein Nachweis der Funktion der eigenen Media-Bridge.

## Go-Konfiguration

Der bestehende Go-Service `services/oglvoice-media` wird erweitert. Er erfordert weiterhin eine vorhandene LiveKit-Instanz und alle bisherigen Medienabhängigkeiten (Pion/libopus). Er ist **nicht** ein reiner Go-Medienserver ohne LiveKit.

Geschuetzte systemd EnvironmentFile, z. B. `/etc/voxvoice/voxvoice.env`:

```env
OGLVOICE_MEDIA_BIND=127.0.0.1:19098
OGLVOICE_LIVEKIT_URL=wss://LIVEKIT_HOST
OGLVOICE_LIVEKIT_API_KEY=CHANGE_ME
OGLVOICE_LIVEKIT_API_SECRET=CHANGE_ME_MIN_32_BYTES
OGLVOICE_WEBRTC_PORT_MIN=40000
OGLVOICE_WEBRTC_PORT_MAX=40999
OGLVOICE_WEBRTC_STUN_URLS=stun:stun.l.google.com:19302,stun:stun.cloudflare.com:3478

VOXVOICE_PUBLIC_URL=https://voice.opengenesislink.de
VOXVOICE_ACCOUNTS_FILE=/var/lib/voxvoice/accounts.json
VOXVOICE_SMTP_ADDRESS=mail.example.net:587
VOXVOICE_SMTP_FROM=noreply@voice.opengenesislink.de
VOXVOICE_SMTP_USER=CHANGE_ME
VOXVOICE_SMTP_PASSWORD=CHANGE_ME
```

Bei Verwendung **nur** registrierter VoxVoice-Accounts darf `OGLVOICE_MEDIA_ACL_JSON` leer bleiben. Andernfalls bleibt die bestehende statische OGLVoice-ACL aktiv. Ohne `VOXVOICE_ACCOUNTS_FILE` ist das Portal vollstaendig deaktiviert.

Der HTTPS-Reverse-Proxy leitet das Portal und beide maschinellen Routen an `127.0.0.1:19098` weiter:

- `/internal/webrtc/v1/exchange`: signierte SDP-/ICE-Requests
- `/internal/voxvoice/v1/regions`: signierte Region-UUID-Anmeldung

Port 443/TCP am Proxy, der konfigurierte Media-Portbereich (Beispiel 40000–40999/UDP) am Pion-Medienhost sowie LiveKit-/TURN-Routen muessen erreichbar sein. Keine HTTP-Weiterleitung der internen Dienste ohne TLS, keine ungefilterten API-Secrets an Browser. STUN allein ersetzt TURN nicht bei restriktiven NAT-/Firewall-Umgebungen.

## Grenzen und erforderliche Abnahme

1. Die Registrierung verlangt SMTP mit STARTTLS. Es wird zunaechst kein E-Mail-Bestaetigungslink versendet; vor einem groesseren oeffentlichen Rollout sind Verifizierung, Anti-Abuse und Passwortrotation zu ergaenzen. Das an E-Mail verschickte Passwort ist in der Mailbox weiterhin vertraulich aufzubewahren.
2. Das Dashboard zeigt verbundene Voice-Avatare, nicht jeden online anwesenden Avatar. Die `Spricht`-Anzeige basiert auf LiveKit-Remote-VAD und kann bei nur einem Teilnehmer fehlen. Fuer vollstaendige VAD-Anzeige braucht es eine direkte serverseitige VAD-Messung je eingehender Audiospur.
3. Bestehende OGLVoice-/WolfVoice-CAPS duerfen nicht parallel auf demselben Simulator aktiviert werden; das ist in der Region-Konfiguration zu vermeiden.
4. Ein E2E-Test mit echten Firestorm-Builds 7.2.4.80712 und 7.2.5.81788 steht aus: SDP, ICE, Audio in beide Richtungen, mehrere Sprecher, Voice-Kugeln, Hypergrid, Reset Voice, Teleport/Logout und Reconnect.
5. Erst nach erfolgreichem E2E-Test und CI-Build kann VoxVoice als OGLVoice-Ablösung fuer produktive Regionen aktiviert werden.

## Entwicklung und Tests

```bash
cd services/oglvoice-media
go test -tags nolibopusfile ./...
go build -tags nolibopusfile -trimpath -o voxvoice .
```

Beispiel eines **nicht produktiven** Starts erfolgt mit der vollstaendigen EnvironmentFile unter einem eigenen Systembenutzer. Fuer die Produktionsfreigabe zusaetzlich Race-Tests, Auth-Replay-Tests, SMTP-Tests und reale Viewer-Tests durchfuehren.
