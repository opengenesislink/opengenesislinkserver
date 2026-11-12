# OpenGenesisLINK Generation 1 (0.9.4.0) - Stable-Freigabeprotokoll

**Betreiberfreigabe mit transparenter Nachweisart:** PASS fuer Live-Funktionen beruht auf ausdruecklicher Betreiberbestaetigung. Build, lokale Protokolltests, Asset-Paketierung und GitHub-CI wurden unabhaengig davon technisch geprueft. Externe Viewer- und Produktivdatenbank-Logs wurden dem Abnahmeagenten nicht vorgelegt.

Status: APPROVED
Gepruefter Code-Commit: bbc5c604e93742f773bd2227578235ff160df50a
Pruefdatum: 2026-11-12
Freigabe durch: Betreiber OpenGenesisLINK / Stadt NexVerse (ausdrueckliche Live-Betriebsbestaetigung im Chat, 2026-11-12)

## Live-Betreiberabnahme und davon getrennte lokale End-to-End-Smoketests

Die Live-Funktionen wurden vom Betreiber bestaetigt; fuer Code-SHA und Build sind eigenstaendige lokale/CI-Nachweise vorhanden. PASS bezeichnet in der Matrix die erteilte Betreiberabnahme, keine Behauptung eines unabhaengigen Live-Audits. Beim optionalen Profil-Feed bedeutet PASS ausschliesslich die vor Veroeffentlichung nachgewiesene Deaktivierung in den ausgelieferten Standardkonfigurationen.

Login_Inventar: PASS
Teleport_Hypergrid: PASS
Mesh_Asset: PASS
LSL_Animesh: PASS
Feed_Datenloeschung: PASS
DB_Backup_Restore_Rollback: PASS


## Abnahme-Matrix: Quelle und Aussagekraft

| Pruefbereich | Status | Nachweis |
| --- | --- | --- |
| Viewer-Login, Inventar, Rechte, Offline-IM, Profile | PASS | Betreiberbestaetigung Livebetrieb; lokale Login/CAPS/LSL-Tests sind getrennte Protokoll-Smokes. |
| Interne/externe Teleports und Hypergrid-Rueckweg | PASS | Betreiberbestaetigung Livebetrieb; getrennte lokale HomeAgent/Gatekeeper-Testprozesse auf Code-SHA erfolgreich. |
| Mesh/PBR, LOD, Upload-Fee, Assets und OAR/IAR | PASS | Betreiberbestaetigung Livebetrieb; Mesh/PBR-Regressionen mit deutschem Locale und GitHub CI bestanden. |
| LSL/OSSL, Animesh | PASS | Betreiberbestaetigung Livebetrieb; lokale LSL/RemoteData-Tests bestanden; #419 ist weiterhin eine separate Kompatibilitaetsuntersuchung. |
| Optionaler Profil-Feed / Konto-Loeschung | PASS (Default-OFF) | `EnableProfileFeed=false` in allen drei ausgelieferten ROBUST-Konfigurationen und per NexVerse-CI gesichert; **nicht** als funktionierender aktiver Feed freigegeben. |
| MariaDB, FSAssets, Backup/Restore/Rollback | PASS | Betreiberbestaetigung Livebetrieb; CI-MariaDB-Matrix, isolierte SQLite-Inventar- und Worker-Kontrakttests erfolgreich; keine unabh. Produktiv-Restore-Logs. |

**Betriebsregel:** Die im bestehenden Grid verwendeten aktiven INIs werden durch die Release-ZIPs nicht ersetzt. Auf vorhandenen Installationen muss `EnableProfileFeed=false` explizit kontrolliert werden, solange Issue #435 offen ist. Der Betreiber bleibt fuer echte Backups und Rueckweg vor Installation verantwortlich.


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
- Zur aktiven Feed-Loeschung/Retention sind noch keine vollstaendigen Live-Nachweise vorhanden; daher ist der Feed fuer dieses Stable **nicht freigegeben** und im ausgelieferten Default deaktiviert (Issue #435 bleibt offen). Der interne/externe Hypergrid-Betrieb sowie Backup/Restore/Rollback werden vom Betreiber bestaetigt; konkrete Live-Logdateien wurden nicht geteilt.
- Die Release-Flavour- und Sicherheits-Aenderungen wurden als PR #445 auf den exakten Code-Commit `bbc5c604e93742f773bd2227578235ff160df50a` gemergt. Der Tag darf erst nach vollstaendig gruenen CI-Gates auf dem **finalen rein dokumentarischen Freigabe-Commit** erzeugt werden.

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

Testumgebung: Stadt NexVerse laufender Livebetrieb (Betreiberbestaetigung); PC-Bomlitz Linux Mint 22.3 x86_64 de_DE.UTF-8 / .NET SDK 10.0.404 + lokale Simulator-/ROBUST-/HG-Smokes; GitHub CI Linux x64, ARM64, Windows x64, MariaDB
Viewer und Version: Firestorm/TrixieStorm laut Betreiber funktionierend; konkrete beim Live-Test benutzte Buildnummern wurden nicht separat angegeben. Firestorm-kompatibles Login-Protokoll im isolierten Simulator-Test nachgewiesen.
MariaDB/FSAssets: Laut Betreiber produktiv funktionsfaehig; GitHub NV$-/MariaDB-Matrix erfolgreich. Keine Live-DB-Bestaende vom Abnahmeagenten eingesehen.
Nachweise/Log-Referenzen: GitHub Issue #442; PR #444 Mesh/PBR Locale; PR #445 Release-Flavour / Feed-Defaults; lokale Logs unter /home/slmm/Projekte/OGL Test/acceptance-release-code-bbc5c604-20261112.log und acceptance-stable-candidate-package-bbc5.log; GitHub-CI-Check-Runs auf dem Code-SHA und finalem Dokumentations-SHA
Bekannte Einschraenkungen: Offene Animesh-Kompatibilitaetsuntersuchung #419, als laufendes Inworld-Problem vom Betreiber nicht fuer die Stable-Freigabe bestaetigt; optionaler Profil-Feed in Release-Defaults AUS bis #435 erledigt; bestehende Betreiber-INIs werden durch das ZIP NICHT geaendert. Live Viewer-/Restore-Logdateien nicht extern auditiert.
Rollback-Test und Ergebnis: Laut ausdruecklicher Betreiberbestaetigung im Livebetrieb funktionierend; genaue Zeitpunkte, Dump-IDs und Restore-Protokolle nicht geteilt. Die lokalen technischen Tests validieren lediglich Backup-/Restore-Worker-Vertraege, keinen Produktivrestore.

**Freigabeprozess:** Erst nach dokumentierter Abnahme dieses Dokument in einem PR auf den exakt geprueften Commitstand aktualisieren. Das Abnahmeprotokoll referenziert den letzten tatsaechlich im Staging getesteten CODE-Commit. Danach duerfen bis zum finalen HEAD ausschliesslich Aenderungen an diesem Abnahmeprotokoll erfolgen, damit die Staging-Abnahme weiterhin fuer exakt denselben Code gilt. Der finale HEAD muss erneut alle CI-Gates bestehen. Der Freigabe-Workflow prueft die Commit-Abstammung und den vollstaendigen Diff; der Selbstverweis auf den SHA des Abnahmedokuments wird bewusst vermieden. Die Repository-Variable `OGL_GEN1_STABLE_RELEASE_APPROVED=true` ist eine zweite, explizite Betreiberfreigabe; GitHub Environment `generation1-stable` sollte zusaetzlich erforderliche Reviewer haben.
