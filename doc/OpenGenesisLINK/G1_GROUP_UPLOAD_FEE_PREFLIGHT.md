# G1 NV$ Gebuehren - Gruppen, Mitgliedschaften und Uploads

Status: Quellcode-Fix mit CI- und Staging-Gates, kein Live-Rollout.

## Risiko vor der Korrektur

Der bekannte Fehler bei Classifieds (PR #432) betrifft auch
`NexVerseMoneyModule.ApplyCharge` und `ApplyUploadCharge`.
Bei unkonfigurierter `[NexEconomyViewer] FeeWalletId` schlugen positive
Gebuehren mit `NV$ fee wallet is not configured` fehl.

Gruppen (sowohl `OpenSim/Addons/Groups` als auch der alte
`XmlRpcGroups`-Adapter) prueften nur `AmountCovered`.
Eine gefuellte Nutzerwallet konnte somit genuegen, um einen
kostenpflichtigen Gruppenaufruf bis zur Gebuehrenbuchung durchzulassen.
Im alten XmlRpc-Gruppenadapter wurde bei Gruppenerstellung zu frueh
belastet, bei Gruppenbeitritt hingegen erst die Mitgliedschaft angelegt
und anschliessend das Guthaben geprueft.

## Fix

- `UploadCovered` verwendet nun die getrennte
  `IMoneyChargePreflight.TryPreflightCharge`-Pruefung fuer positive
  Uploadgebuehren. `AmountCovered` fuer normale Transfers bleibt
  unveraendert.
- Bei Neugruendung und kostenpflichtigem Beitritt wird die Systemgebuehr
  vor dem persistenten Anlegen der Gruppe/Mitgliedschaft vorvalidiert.
  Beide Gruppenmodule nutzen das optionale Interface; fremde
  `IMoneyModule` koennen weiterhin `AmountCovered` nutzen.
- Der alte XmlRpc-Adapter bucht die Gruppenerstellungsgebuehr erst nach
  erfolgreich erstellter Gruppe; bei Mitgliedschaften wird nicht mehr
  vor der Guthabenpruefung eingetragen.
- Eine spaete Buchungsausnahme laeuft nicht ungefangen in den
  Viewer-Request: UUIDs und Fehlergrund werden mit
  manuellem Ledger-Reconciliation-Hinweis protokolliert; der Viewer
  bekommt eine Warnung.

## Betrieb und Konfiguration

```ini
[NexEconomyViewer]
FeeWalletId = "<SYSTEM_FEE_WALLET_UUID>"
```

Die Zielwallet muss im NV$-Ledger als tatsaechlicher, berechtigter
Empfaenger eingerichtet sein. Die hier geaenderten Pfade
**generieren weder Wallet-UUIDs noch erlauben sie Gebuehrenfreiheit**
bei fehlender Konfiguration. Keine fremde Avatarwallet und nicht
`UUID.Zero` einsetzen. Der produktive Regionsprozess braucht nach
einem neuen Build einen kontrollierten Neustart.

## Abnahme und Restpunkte

1. `FeeWalletId` leer, UploadCharge/GroupCreationCharge oder
   MembershipFee positiv: Ablehnung *vor* dauerhafter Mutation.
2. Gueltige Wallet, ausreichendes Guthaben: erfolgreich und einmalig
   gebuchter Geldfluss sowie korrekte Gruppe/Inventar.
3. Keine Deckung, API timeout, falsches HG-Wallet, die gleiche Aktion
   zweimal: keine negativen Salden und keine Dopplungen.
4. Gratis-Gruppen und UploadCharge=0: weiterhin freigeschaltet.
5. Normale Residents- und Land-Transfers bei fehlender Gebuehrenwallet:
   nicht global blockiert.
6. Alte XmlRpc- und neuere Groups-Implementierung getrennt pruefen.

**Wichtige Grenze:** Die jeweiligen externen Gruppenservices und
das NV$-Ledger verwenden keine gemeinsame Datenbanktransaktion.
Die Vorabpruefung macht die bekannte Konfigurationsstörung vorhersehbar;
sie garantiert keine atomare `group/create + debit`- oder
`inventory/upload + debit`-Buchung. Unter Last oder nach API-Timeout
muessen Betreiber die Eintraege gegen das Ledger abgleichen. Ein
dauerhafter `pending -> confirmed`-Workflow mit persistenter
Idempotenz ist die Folgestufe, bevor eine allgemeine
Exactly-Once-Zahlungsgarantie behauptet werden darf.
