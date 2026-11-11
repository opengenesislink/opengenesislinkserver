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

## Protokoll

Testumgebung: PENDING
Viewer und Version: PENDING
MariaDB/FSAssets: PENDING
Nachweise/Log-Referenzen: PENDING
Bekannte Einschraenkungen: PENDING
Rollback-Test und Ergebnis: PENDING

**Freigabeprozess:** Erst nach dokumentierter Abnahme dieses Dokument in einem PR auf den exakt geprueften Commitstand aktualisieren. Das Abnahmeprotokoll referenziert den letzten tatsaechlich im Staging getesteten CODE-Commit. Danach duerfen bis zum finalen HEAD ausschliesslich Aenderungen an diesem Abnahmeprotokoll erfolgen, damit die Staging-Abnahme weiterhin fuer exakt denselben Code gilt. Der finale HEAD muss erneut alle CI-Gates bestehen. Der Freigabe-Workflow prueft die Commit-Abstammung und den vollstaendigen Diff; der Selbstverweis auf den SHA des Abnahmedokuments wird bewusst vermieden. Die Repository-Variable `OGL_GEN1_STABLE_RELEASE_APPROVED=true` ist eine zweite, explizite Betreiberfreigabe; GitHub Environment `generation1-stable` sollte zusaetzlich erforderliche Reviewer haben.
