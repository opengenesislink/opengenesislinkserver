# OpenGenesisLINK Generation 1 – gridweite LSL-Objektkommunikation

**Neu:** `llGridSay(integer channel, string message);` ist eine OpenGenesisLINK-eigene LSL-Erweiterung für Kommunikation zwischen Objekten in **allen laufenden Regionen des gleichen Grids**, unabhängig vom Estate und der Entfernung. Auch über getrennte Simulatorprozesse hinweg kann die Nachricht per NexBus übertragen werden. Dies ist keine Standard-LSL-Funktion und funktioniert nicht auf unveränderten OpenSim-/Second-Life-Servern.

Das bestehende `llEstateSay` (gleicher Estate) und `llRegionSayTo` (gezielte Objekt-UUID) bleiben unverändert.

## Verwendung

```lsl
integer CHANNEL = -9342801;

default
{
    state_entry()
    {
        llListen(CHANNEL, "", NULL_KEY, "");
    }

    touch_start(integer total_number)
    {
        llGridSay(CHANNEL, "Beep");
    }

    listen(integer channel, string name, key sender, string message)
    {
        if (message == "Beep")
            llSetColor(<1.0, 0.0, 0.0>, ALL_SIDES);
    }
}
```

Die vollständige Würfel-Demovorlage mit 0,5-Sekunden-Rückstellung liegt in `tools/diagnostics/ogl_grid_beep_cubes.lsl`. Dieses Script kommt in jeden Würfel. Für zuverlässiges Feedback leuchtet der Sender beim Touch auch sofort lokal.

## Architektur

- **Gleicher Simulatorprozess:** Der NexVerse NodeAgent liefert über `IWorldComm.DeliverMessage(ChatTypeEnum.Region, ...)` in jede geladene Region, ohne Estate-Filter.
- **Andere Simulatorprozesse:** Der NodeAgent veröffentlicht `grid.script.message.requested` auf dem bestehenden, HMAC-gesicherten NexBus. Empfangende Nodes liefern über `IWorldComm` an ihre lokalen Regionen. Die Quellnode wird übersprungen, damit keine Rückschleife entsteht.
- **Empfänger:** Jedes Skript im selben Grid, das den benutzten Kanal abhört, kann den Inhalt empfangen. Ein negativer Kanal ist kein Geheimnis und keine authentisierte Senderidentität.
- **Grenzen:** kein Kanal `0`, kein `DEBUG_CHANNEL`; maximale Nachricht 1023 Zeichen/Bytes im bestehenden LSL-Payloadpfad; pro Quellobjekt bestehendes Ratenlimit `ObjectMessagesPerSecond`. Alte oder wiederholte NexBus-Ereignisse werden durch Ablaufkontrolle und bestehende Event-ID-Deduplizierung verworfen. Die Zustellung ist best effort, nicht garantiert; offline Regionen bekommen keine gespeicherte Nachricht.
- **Senden darf nur:** Objekt, das nachweislich dem **Estate Owner oder einem eingetragenen Estate Manager der Quellregion** gehört. Ein normales Einwohnerobjekt darf keine globalen Signale auslösen. Die Quellnode prüft live Objekt, Besitzer und Estate-Berechtigung, bevor lokal oder extern ausgeliefert wird. Die authentifizierte Gegenseite vertraut nur registrierten NexBus-Simulatoren, nicht Viewer-Daten.
- **Hypergrid:** Die Funktion kommuniziert innerhalb der selben OpenGenesisLINK-NexBus-Gridverwaltung, **nicht automatisch in fremde Hypergrid-Systeme**.

## Konfiguration

Alle Simulatoren auf denen Würfel stehen müssen die Funktion laden. Für eine echte standortübergreifende Verbindung braucht jeder NodeAgent eine eindeutige `NodeId`, eine funktionierende `PeerUrl` sowie den korrekt gesetzten `NEXVERSE_NEXBUS_SHARED_KEY`. Der bestehende HMAC-Hub muss die Ereignisse verteilen; eingehende NexBus-Routen dürfen niemals ungeschützt ins Internet gelegt werden.

```ini
[NexVerseNodeAgent]
Enabled = true
GridScriptMessaging = true
ObjectMessagesPerSecond = 20
ObjectMessageMaxAgeSeconds = 30
```

`GridScriptMessaging` ist in `OpenSimDefaults.ini` sicherheitshalber **false**; in den Stadt-NexVerse-Aktivprofilen und Beispielen **true**. Neue GitHub-Konfigurationen werden auf existierende Installationen nicht automatisch angewandt. Nur aktivieren, wenn die verwendeten Sims und der NexBus zu derselben Gridverwaltung gehören.

## Abnahme im Staging

1. Zwei Würfel in **derselben Region**: einmal pro Klick rot aufleuchten und wieder weiß werden.
2. Zwei Würfel auf **verschiedenen Regionen desselben Simulatorprozesses**, **verschiedene Estate-IDs**.
3. Zwei Würfel auf **verschiedenen Simulatorservern**, verschiedene Estate-IDs, tatsächlicher NexBus/HMAC-Pfad.
4. Nicht berechtigter Einwohner-Würfel muss Senden verweigert bekommen. Listener dürfen Empfang weiter nutzen.
5. Negativtest mit Kanal 0, Debug-Kanal, zu langen Nachrichten, Spam, ungültigen Signaturen, Replay, abgeschaltetem NodeAgent und nicht zustellbarem Peer.
6. Vorher bestehende `llEstateSay`/LSL-`llRegionSayTo` Tests, normale Hypergrid-Käufe, Region-Crossing und Build dürfen nicht regressieren.

Dies ist ein Grid-Broadcast im laufenden Simulatorbetrieb, **keine Hypergrid-Föderationsfunktion und keine dauerhafte Message-Queue**.
