# Stadt NexVerse – Täglicher NV$-Loginbonus

## Regeln

Gültig für **registrierte lokale Bürger**, die sich über den ROBUST-Login
erfolgreich im Stadt-NexVerse-Grid anmelden. Keine Belohnung bei
fehlgeschlagenen Anmeldungen, TOS-Ablehnung, NPCs, Hypergrid-Gästen oder
Regionsteleports. Ohne Anmeldung erfolgt keine Gutschrift.

| Tag der ununterbrochenen Serie | Gutschrift |
|---|---:|
| 1–6 | 10 NV$ pro Tag |
| 7 | 60 NV$ = 10 NV$ normal + 50 NV$ Extra |
| 8 | Neuer Zyklus: 10 NV$ |

Die siebentägige Staffel **wiederholt sich**, wenn sich der Bürger
weiterhin täglich einloggt. Fehlt ein Berliner Kalendertag, beginnt die
Staffel am nächsten erfolgreichen Login wieder bei Tag 1. Es gibt keine
rückwirkenden Auszahlungen für verpasste Tage.

## Technische Integration

- Der lokale `LLLoginService` emittiert erst **nach** erfolgreicher
  Authentifizierung, TOS-Prüfung und Region-Launch ein vertrauenswürdiges
  In-Process-Ereignis. Passwort- und Anmeldeversuche erzeugen keine Rewards.
- `NexEconomyConnector` ist alleiniger Subscriber im ROBUST-Prozess und
  nutzt `NexEconomyService.CreditResidentDailyLoginBonus()`.
- Jeder Kalendertag je Avatar hat eine eindeutige, deterministische
  Transaktions-ID und zwei deterministische Buchungsposten.
  Der zentrale **Double-Entry-Ledger** erzwingt atomare Persistenz und
  verweigert doppelte / abweichende Buchungen – auch über mehrere ROBUST-Nodes.
- Das Journal enthält `program`, `resident_id`, `local_day` und
  `streak_day`. Der vorherige lokale Kalendertag wird mit einer
  einzelnen Buchungsabfrage bestimmt, ohne zusätzliche mutable Tabellen.
- Guthaben werden niemals direkt überschrieben oder als Zweitkonto geführt.
  Die Buchung erfolgt von einem separaten System-Ausgabekonto.
- Die Belohnung eines Tages wird sofort nach dem erfolgreichen Login
  synchron verarbeitet. Ein fehlgeschlagener Ledger-Zugriff wird
  protokolliert, verhindert aber den Viewer-Login nicht. Ein erneuter
  Login am selben Tag kann die fehlende Buchung nachholen.

## Produktionskonfiguration

```ini
[NexEconomy]
    Enabled = true
    DailyLoginRewardEnabled = true
    DailyLoginRewardTimezone = "Europe/Berlin"
```

In `bin/Robust.HG.ini` für Stadt NexVerse aktiviert; generische Beispiele
lassen die Funktion deaktiviert. Die Kalendertagsberechnung verwendet
`TimeZoneInfo` einschließlich Sommer-/Winterzeit, keine starre UTC+1.
Die eigentliche Journalbuchung besitzt einen stabilen UTC-Zeitstempel für
die lokale Kalender-ID. Rollback: `DailyLoginRewardEnabled = false`, dann
ROBUST neu starten. Bereits ausgezahlte Buchungen bleiben unverändert.

## Produktionsabnahme

1. Lokalen, noch nicht belohnten Testavatar erfolgreich anmelden: +10 NV$.
2. Abmelden und am selben Kalendertag erneut anmelden: +0 NV$.
3. Sieben aufeinanderfolgende Tage in isolierter Test-DB durchspielen;
   Tag 7 muss genau +60 NV$ ergeben, Tag 8 +10 NV$.
4. Einen Tag überspringen: nächste Anmeldung zahlt +10 NV$ (Tag 1).
5. Lokal- und HG-Login unterscheiden; nur lokaler Login löst die
   Gutschrift aus.
6. 100 parallele Anmeldeversuche am selben Tag dürfen in Summe nur
   genau eine Ledger-Transaktion je Avatar erzeugen.
7. Datum um die Berliner Sommer-/Winterzeit prüfen.
