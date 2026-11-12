# OpenGenesisLINK Generation 1 (0.9.4.0) - Stable-Freigabeprotokoll

**Dieses Dokument ist keine Testbestaetigung.** Solange ein Feld PENDING ist, darf kein offizielles Stable-Release entstehen.

Status: PENDING
Gepruefter Code-Commit: PENDING
Pruefdatum: PENDING
Freigabe durch: PENDING

## Echte End-to-End-Abnahme (Staging, kein CI-Smoke-Test)

Vor einer Freigabe die folgenden Zeilen erst nach nachweislich bestandenem Test auf PASS setzen. Testumgebung, Viewer-Version, Datenbankstand, verwendete Testbenutzer und relevante Protokolle in diesem Dokument ergaenzen.

Login_Inventar: PENDING
Teleport_Hypergrid: PENDING
Mesh_Asset: PENDING
LSL_Animesh: PENDING
Feed_Datenloeschung: PENDING
DB_Backup_Restore_Rollback: PENDING

## Testumfang und Nachweise

- Login und Logout mit Firestorm und, soweit freigegeben, TrixieStorm; neue und vorhandene Avatare.
- Teleports intern und ueber Hypergrid, Rueckreise, Inventarberechtigungen sowie Offline-Zustellung.
- Mesh-/PBR-Upload einschliesslich LOD, J2C-/Textur-/Asset-Abruf, Rez, Relog und OAR/IAR.
- LSL/OSSL und Animesh: Objektanimation, Rotations-/Bindpose-Faelle, keine Datenverluste.
- MariaDB und FSAssets mit realistischer Staging-Kopie; Feed-Opt-in, OAuth, Loeschung, Retention und konkurrierende Schreibzugriffe. Falls Feed nicht abgenommen ist: in allen Release-Defaults deaktivieren und gesondert dokumentieren.
- Backup und Wiederherstellung von Datenbank, Konfiguration, Inventar und Assets; nachweislich getesteter Rueckweg auf zuvor eingesetzte G1-Version.
- Pruefung aller Netzwerk-/TLS-/World-API-/NV$-Berechtigungen; Sicherheitsgrenzen und optionale Dienste.
- Native .NET-10-Laufzeiten fuer Linux x64, Linux ARM64 und Windows x64 aus CI; Paketinhalt und SHA256 verifizieren.


## Betreiberbestaetigung zum Livebetrieb (12.11.2026)

Der Betreiber bestaetigt fuer die bereits laufende OpenGenesisLINK-Generation-1-Installation:

- **Viewer und Inworld:** Anmeldung, Mesh/PBR, Animesh und Hypergrid funktionieren im Livebetrieb nach Betreiberangabe tadellos.
- **Persistenz:** MariaDB und FSAssets sowie Backup/Restore/Rollback wurden vom Betreiber als funktionierend bestaetigt.
- **Freigabeabsicht:** Der Betreiber hat die offiziellen Release-Schutz- und Freigabeschritte grundsaetzlich bestaetigt.

**Nachweisart:** Betreibererklaerung am 12.11.2026, zusaetzlich zur dokumentierten
technischen Teilabnahme auf `PC-Bomlitz` und den GitHub-CI-Pruefungen.
Dies stellt keine vom Abnahmeagenten selbst ausgefuehrte Datenwiederherstellung
und keinen durch den Agenten selbst getesteten grafischen Viewer-Login dar.

**Tatsaechlich verifizierter technischer Teststand vor Umschaltung:**
`c28ad822ade426087851714bcc8a4a7922d645ae`, .NET10,
Linux Mint 22.3/de_DE.UTF-8: Release-Build, isoliertes ROBUST,
lokale OpenSim-Simulator-Login-/CAPS-/LSL-Tests, HomeAgent/Gatekeeper,
Mesh-LLSD und PBR; GitHub-CI Linux x64, ARM64, Windows x64 und MariaDB.
Details: [Issue #442](https://github.com/opengenesislink/opengenesislinkserver/issues/442).

**Noch zu belegen:**
- **Behoben/technisch nachgewiesen am 12.11.2026:** `main` ist durch
  das aktive [Ruleset #27082748](https://github.com/opengenesislink/opengenesislinkserver/rules/27082748)
  geschuetzt (PR + sechs CI-Checks, kein Force-Push/Delete);
  `v0.9.4.0` durch [Tag-Ruleset #27082762](https://github.com/opengenesislink/opengenesislinkserver/rules/27082762)
  gegen Loeschung/Mutation. GitHub-Environment `generation1-stable`
  hat den Required Reviewer `opengenesislink` und erlaubt nur
  geschuetzte Branches. Beide Actions-Variablen
  `OGL_GEN1_STABLE_RELEASE_APPROVED` und `OGL_GEN1_UPDATES_APPROVED`
  wurden initial auf `false` gesetzt.
- Die genaue Betreiberabnahme fuer Feed-Loeschung/Retention, externen
  HG-Rueckweg und die Datensicherungs-/Restore-/Rollback-Umgebung muss
  im abschliessenden Protokoll spezifiziert werden.
- Nach Freigabe der Release-Flavour-Aenderung den neuen finalen
  **Code-Commit-SHA** festhalten und dessen gruene CI-Gates pruefen.
- Erst dann in einem **separaten reinen Abnahmeprotokoll-Commit**
  `Status: APPROVED` und die technischen `PASS`-Kennzeichnungen
  freigeben; der `v0.9.4.0`-Tag bleibt bis dahin gesperrt.

## Release-Schutz fuer den optionalen Profile Feed

[Issue #435](https://github.com/opengenesislink/opengenesislinkserver/issues/435)
ist weiter offen. Die Konto-Loeschung/Retention darf deshalb nicht als
funktionierender aktiver Feed attestiert werden. Fuer die Stable-Quelle
sind `EnableProfileFeed = false` in `bin/Robust.HG.ini`,
`bin/Robust.HG.ini.example` und `bin/Robust.ini.example`
festgeschrieben; die NexVerse-CI prueft diesen Zustand. Das
Release-ZIP liefert keine laufenden Betreiber-INIs und schaltet
keinen Feed im vorhandenen produktiven Grid um. Betreiber mit
eigener Live-Konfiguration muessen diesen Schalter gesondert pruefen.

## Protokoll

Testumgebung: PENDING
Viewer und Version: PENDING
MariaDB/FSAssets: PENDING
Nachweise/Log-Referenzen: PENDING
Bekannte Einschraenkungen: PENDING
Rollback-Test und Ergebnis: PENDING

**Freigabeprozess:** Erst nach dokumentierter Abnahme dieses Dokument in einem PR auf den exakt geprueften Commitstand aktualisieren. Das Abnahmeprotokoll referenziert den letzten tatsaechlich im Staging getesteten CODE-Commit. Danach duerfen bis zum finalen HEAD ausschliesslich Aenderungen an diesem Abnahmeprotokoll erfolgen, damit die Staging-Abnahme weiterhin fuer exakt denselben Code gilt. Der finale HEAD muss erneut alle CI-Gates bestehen. Der Freigabe-Workflow prueft die Commit-Abstammung und den vollstaendigen Diff; der Selbstverweis auf den SHA des Abnahmedokuments wird bewusst vermieden. Die Repository-Variable `OGL_GEN1_STABLE_RELEASE_APPROVED=true` ist eine zweite, explizite Betreiberfreigabe; GitHub Environment `generation1-stable` sollte zusaetzlich erforderliche Reviewer haben.
