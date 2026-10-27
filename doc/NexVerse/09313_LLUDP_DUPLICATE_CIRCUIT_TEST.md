# 0.9.3.13 Dev — LLUDP duplicate UseCircuitCode / missing AgentCircuitData

**Status: Fix im PR, Live-Abnahme ausdrücklich offen.**

## Reproduzierbarer Vorfall (älterer Betreiber-Log)

Die hochgeladene `OpenSim.log` enthält am **27.10.2026, 10:48:38** mit
einem **OpenGenesisLINK 0.9.3.12**-Build (letzter Startup `debdcce`)
zwei nahezu zeitgleiche `UseCircuitCode`-Anfragen für **denselben
Radegast-ng-Avatar und denselben Circuit-Code**, aber zwei verschiedene
Quell-UDP-Ports. Der erste Client wird aufgebaut, kurz darauf geschlossen,
und sein Child-Agent sowie CAPS werden gelöscht; der zweite Versuch
endet mit `System.Threading.Monitor.ReliableEnter(Object obj,...)` in
`Scene.AddNewAgent`. Das historische Log enthält keine Evidenz
für ein entsprechendes 0.9.3.13 Dev-Testergebnis.

## Codeursache

1. `LLUDPServer.AddClient` schließt ein bestehendes Client-Objekt bei
   gleichem Agenten/Circuit/Session und abweichendem Quellport, obwohl
   der andere Handshake derselben Session angehört.
2. Die Schließung kann `AgentCircuitData` entfernen, während der zweite
   Verbindungsaufbau fortgesetzt wird. `Scene.AddNewAgent` versuchte
   dann `lock (aCircuit)` auf `null`.
3. Auch `LLUDPServer.HandleUseCircuitCode` griff auf
   `aCircuit.IPAddress` **vor** dem fehlenden Null-Check zu.

## Gezielte Schutzmaßnahmen

- Gleicher Agent, Circuit, Session und SecureSession mit **gleicher**
  IP/UDP-Port-Kombination: alten, registrierten Client wiederverwenden.
- Identische autorisierte Session mit **anderem** UDP-Quellport:
  den konkurrierenden Handshake ablehnen, **ohne den existierenden
  Client zu schließen**.
- Andere/neue Session: bisheriger Reconnect-/Cleanup-Pfad bleibt erhalten.
- Bereits entfernter Circuit: frühzeitig mit Warnung abweisen,
  Pending-UDP-Cache für den betreffenden Endpoint bereinigen.
- Zwischenzeitlich verlorener Circuit beim `Scene.AddNewAgent`:
  keine Null-Lock-Exception, Login-Zähler korrekt reduzieren.
  Die im Handshake bereits erzeugte, aber nicht registrierte
  Client-Instanz wird ohne Region-Handshake geschlossen.

## Pflichtprüfung auf RC-Build, außerhalb der Quellcode-/CI-Regression

1. Firestorm 7.2.4: Login, Logout, sofortiges Re-Login, Teleport, HG-
   Teleport und Regionsgrenze; jeweils beobachten, ob Agent und CAPS
   bestehen bleiben und Login-Zähler normalisieren.
2. Radegast-ng: **denselben authentifizierten Circuit** nahezu zeitgleich
   mit zwei UDP-Quellports verwenden; erster Client darf nicht gelöscht
   werden, zweiter Handshake muss kontrolliert abgewiesen werden.
3. Legitimer neuer Circuit/Session nach Logout muss sich anmelden können;
   parallele voneinander unabhängige Avatare bleiben uneingeschränkt.
4. Verspäteter/ungültiger Circuit darf weder Login noch Crash auslösen.
5. Mindestens 30 Minuten Last-/Reconnect-Betrieb: Logs prüfen,
   keine neue `UseCircuitCode ... failed`-/`ReliableEnter`-Exception,
   keine verlorenen Child-Agents oder unaufgeräumten Sessions.

**Akzeptanzstatus:** C04 und V03 bleiben bis zur echten Betreiberprüfung
offen. GitHub CI und statische Guard-Tests beweisen nur Kompilierbarkeit
und Quellcodebedingungen, keine reale NAT-/Viewer-End-to-End-Funktion.

**Achtung:** Der fix verhindert, dass der alternative UDP-Port den
bestehenden aktiven Client verdrängt. Ein legitimer NAT-Rebind benötigt
weiterhin einen vollständigen Neuanmeldeprozess oder eine separate
durchgehend authentifizierte Endpoint-Migrationslogik. Diese
Erweiterung ist nicht Bestandteil dieses Sicherheitsfixes.
