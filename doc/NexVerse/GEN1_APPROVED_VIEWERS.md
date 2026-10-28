# OpenGenesisLINK Generation 1 – Offizielle Viewer-Zulassung

**Status:** Entwicklung (technischer Build 0.9.4.0 Dev), nach PR-Merge noch kein automatischer Live-Rollout.

## Zugelassene Viewer

- **Firestorm Viewer** – `Firestorm` und Firestorm-Channel wie `Firestorm-Releasex64`
- **OpenGenesisLINK Viewer** – `OpenGenesisLINK` und der projektinterne Name `OGL Viewer`

Andere selbst gemeldete Viewernamen – einschließlich **Darkstorm**, **Hydrastorm**, Alchemy, Singularity, Cool VL und unbekannter Clients – sind nicht zugelassen.

Der Zugang ist an **drei Stellen** serverseitig geprüft:
1. lokaler ROBUST LoginService vor erfolgreicher Anmeldung;
2. ROBUST GatekeeperService vor Hypergrid-Präsenz und Weitergabe an Zielregion;
3. Simulator Scene beim Eintritt, Direkttransport und Region Crossing.

Dafür nutzt Generation 1 die gemeinsame Implementierung `OpenSim.Framework.OglApprovedViewerPolicy`. Sie erfordert einen passenden offiziellen Namen am Anfang des Viewer-Kanals oder der Viewer-Version, prüft widersprüchliche Namensangaben und verwirft ausdrücklich die bekannten Copybot-Kennungen, selbst wenn ein erlaubter Name zusätzlich vorkommt. Numerische Versionsstrings mit offiziell angegebenem Viewer-Channel werden akzeptiert; komplett fehlende/unbekannte Kennungen nicht.

## Konfiguration

Aktive Profile und Vorlagen:

```ini
[AccessControl]
OGLApprovedViewersOnly = true
```

Das gilt sowohl für ROBUST (LoginService **und** GatekeeperService) als auch für jeden Simulator (`OpenSim.ini`). Nach einem Update müssen tatsächlich geladene Konfigurationen auf allen Nodes geprüft und die betroffenen Dienste neu gestartet werden. Für Tests kann die Option in einer isolierten Umgebung auf `false` gesetzt werden.

Bisherige `AllowedClients`/`DeniedClients`-Regeln werden weiterhin angewandt; diese dürfen die zwei offiziellen Viewer nicht zusätzlich ungewollt ausschließen. Die bestehende OGL Viewer Advisor-Empfehlung ist unabhängig davon und bleibt deaktiviert.

## Hypergrid und Marketplace bleiben offen

```ini
[HGInventoryAccessModule]
OutboundPermission = true

[HGAssetService]
DisallowAllExport = false
```

Berechtigte ausländische Gäste mit Firestorm/OpenGenesisLINK können Gegenstände kaufen, in ihr eigenes Inventar übertragen, heimreisen und sie im Rahmen der üblichen Objektberechtigungen aufstellen. Die HG-Asset-Endpunkte bleiben für autorisierte Transaktionen offen. Die Begrenzung großer Asset-Existenzabfragen aus PR #330 bleibt erhalten.

## Sicherheitsgrenze (verpflichtend klar kommunizieren)

**Viewer-Kennung ist keine vertrauenswürdige Identität.** Viewer senden Channel und Version selbst; ein modifizierter Darkstorm/Hydrastorm-Client kann sich als Firestorm tarnen. Diese Allowlist sperrt nur nicht freigegebene **gemeldete** Viewer und ihre Sitzungen. Sie stellt keine kryptografische Viewer-Attestierung dar und kann clientseitiges Exportieren empfangener Meshes/Texturen nicht zu 100 % verhindern.

Ebenso ist ein öffentlicher Hypergrid-Asset-HTTP-Endpunkt nicht automatisch einer Viewer-Sitzung zugeordnet: Direkte HTTP-Aufrufe auf Import/Export-Assetrouten können **nicht allein über die Login-Zulassung** abgesichert werden. Eine vollständige Sitzungs-/Asset-Berechtigung benötigt ein zusätzliches protokollkompatibles Authentisierungsdesign, das HG-Handel nicht unterbricht.

## Tests vor Produktivbetrieb

- Anmelden lokal: Firestorm und OpenGenesisLINK (auch interne Aliaskennung) erfolgreich.
- Ablehnen lokal: Darkstorm, Hydrastorm, nicht freigegebene/fehlende Kennungen und bekannte Spoof-Kombinationen.
- Gatekeeper: Firestorm-Gast darf hinein, Darkstorm/Hydrastorm sowie unbekannte Gäste werden vor Präsenzanlage abgelehnt.
- Regionen: Zugelassene lokale/HG-Avatare können teleportieren und Regionen wechseln; sonstige werden abgewiesen.
- Erhalt der Kaufkette: HG-Gast kauft Mesh/Kleidung, nimmt sie heim und kann sie dort rezzen/tragen. Reguläre Imports bleiben möglich.
- Live- und Negativtests auf Staging vor Produktion, inklusive Netztopologie und Konfigurationsvererbung.
- Keine pauschale HG-Exportsperre, kein verfrühter Stable-Rollout.

**Verifikation:** `tools/ci/verify_gen1_approved_viewer_access.py` und `tools/ci/OglApprovedViewerRegression`, zusätzlich zur normalen NexVerse CI.
