# G1 Modul-Refactoring – NodeAgent / Performance

`NexVerseNodeAgentModule` ist fuer NexBus, Regionskommandos,
Live-Update-Befehle, Event-/Chat-Relay und Telemetrie verantwortlich.

Der erste nichtfunktionale Schnitt trennt den read-only
Performance-Herzschlag in `OglNodePerformanceHeartbeat` aus:

- In der NodeAgent-Mainklasse verbleiben Signierung, Uebertragung,
  Fehlerbehandlung, Retry und alle Freigabe-/Auth-Schalter.
- Im Helfer liegen ausschliesslich: Regionsfenster erfassen,
  `OglPerformanceSampler.Capture`, optionale Flotsam-Statistiken
  und Eintrag in das bestehende Metrik-Dictionary.
- Keine neuen Listener, Metriknamen, Signaturen, DB-Tabellen oder APIs.
- Der vorhandene Regen-/LunaTexture-/Wetter-Herzschlag bleibt unveraendert.

Weitere Refactorings sollten jeweils getrennt erfolgen: Node Control /
Updater, World-API-Fachrouten und Admin-Weboberflaeche. Die grosse
`NexApiDocsPage.cs` darf nicht ohne umfassende Anpassung der
Quellvertragstests gesplittet werden, da viele CI-Gates ihre Markup-
IDs direkt dort lesen.

Abnahme: .NET10 Linux/Windows/ARM64, NexVerse-CI, und Laufzeittest
einer 42-Regionen-Node mit unveraenderten signierten Heartbeats.
