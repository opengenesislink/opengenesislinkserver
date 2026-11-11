# OpenGenesisLINK Generation 1 - NV$ Classified Fee Preflight

Status: Fix-Branch, Staging-/Build- und Betreiberabnahme erforderlich.

## Ursache
Beim Neuanlegen von Classifieds fuehrte `UserProfileModule.ClassifiedInfoUpdate`
den Aufruf `classified_update` gegen den Profilservice **vor** der
nachgelagerten `ApplyCharge`-Buchung aus. Mit leerem
`[NexEconomyViewer] FeeWalletId` schlug die Belastung danach mit
`InvalidOperationException: NV$ fee wallet is not configured.` fehl.
Der Eintrag konnte bereits gespeichert sein. Dies ist ein Problem der
Reihenfolge und der fehlenden Vorabvalidierung, nicht der RPC-Job-Engine.

## Aenderung
- Optionales Framework-Interface `IMoneyChargePreflight`. Bestehende andere
  `IMoneyModule`-Implementierungen bleiben unveraendert.
- `NexVerseMoneyModule`: bei positiven Gebuehren fehlende/ungueltige
  Zielwallet, deaktivierten Dienst, ungueltigen Zahler oder unzureichendes
  Guthaben **vor** dem `classified_update` erkennen. Negative Preise
  werden abgewiesen; Nullgebuehr folgt dem bisherigen Verhalten.
- Die Pruefung `AmountCovered` bleibt fuer Landkaeufe, Objektzahlungen und
  Transfers unveraendert; eine fehlende Systemgebuehrenwallet blockiert
  keine sonstigen NV$-Peer-to-Peer-Buchungen.
- Unerwartete Ledgerfehler *nach* dem Speichern loesen keine ungefangene
  Job-Exception mehr aus. Der Viewer erhaelt eine Warnung, das Serverlog
  enthaelt Classified- und Agent-UUID zur manuellen Abgleichung. Ein
  unklarer Debit wird niemals blind mit neuer Transaktions-ID wiederholt.

## Betrieb
```ini
[NexEconomyViewer]
Enabled = true
WorldApiBaseUrl = "https://world.stadt-nexverse.de"
ApiKey = "${Environment|NEXVERSE_ECONOMY_API_KEY}"
; Die UUID muss zu einem tatsaechlich eingerichteten, separaten
; empfangsbereiten Service-/Systemwallet im zentralen NV$-Ledger gehoeren.
FeeWalletId = "<SYSTEM_FEE_WALLET_UUID>"
```

**Nicht** die Avatar-UUID eines beliebigen Bewohners, nicht UUID.Zero,
kein zufaelliger Platzhalter und nicht den privaten MasterBank-Payout-Account
ohne ausdrueckliche Berechtigung verwenden. Das Ledger muss Ueberweisungen
an diese Wallet erlauben. Konfiguration pro Simulator pruefen; Aenderungen
werden erst nach dem jeweiligen Neustart aktiv.

Vor erneuter Aktivierung alle zwischenzeitlich erstellten Classifieds
auf erfolgreich gebuchte NV$-Gebuehren abgleichen. Unbezahlte Eintraege
manuell sperren oder nach Betreiberentscheidung nachberechnen. Keine
automatische Rueckbuchung auf Verdacht.

## Abnahme
1. Fehlende FeeWalletId, positiver Classified-Preis: Viewer-Warnung,
   **kein** `classified_update`, keine unbezahlte neue Anzeige.
2. Ungueltige negative Preisangabe: Ablehnung ohne Datenmutation.
3. Gueltige Wallet, Deckung vorhanden: genau eine Ledger-Buchung,
   Anzeige und Saldo korrekt.
4. Fehlendes Guthaben: keine Anzeige und keine Buchung.
5. Peer-to-Peer-Transfer/Landkauf bei nicht konfigurierter FeeWalletId:
   fuer dieses Szenario keine Regression der `AmountCovered`-Logik.
6. Simulierter World-API-Timeout **nach** `classified_update`:
   kontrollierte Meldung, Log mit IDs und manueller Ledger-Abgleich.
7. Keine automatischen Mehrfachabbuchungen nach erneuten Viewer-Anfragen.

## Verbleibendes Architektur-Risiko
Profil-RPC und Ledgerbuchung sind keine gemeinsame Datenbanktransaktion.
Die Vorabpruefung beseitigt die *bekannte, deterministische*
Fehlerursache, aber keine Verbindungsabbrueche zwischen Speichern und
Belasten. Fuer echte atomare Zahlungspflicht braucht es einen zentralen
`pending -> paid/published`-Workflow mit langlebigem
Idempotency-Key, Reconciliation und reversibler Reservierung. Bis dahin
ist eine globale "exactly once"-Garantie nicht behauptet.
