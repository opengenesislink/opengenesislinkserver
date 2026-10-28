// STADT NEXVERSE - OGLWeather 0.1
// In ein unsichtbares, nicht-physikalisches Prim pro Wetterbereich.
// Beispiel: 20-30 m Hoehe, mehrfach verteilt fuer 256m Regionen.
// Niederschlag ist LOKAL um jeden Emitter (Viewer-Partikel-Limit).
// Alle Regionen eines Simulatorprozesses empfangen dasselbe Wetter.
// Kein Scriptzugriff auf Fremdregionen, kein Terraforming.
integer CHANNEL = -835311;
string SOURCE = "OGLWeather";
string gCondition = "sun";
integer gIntensity;
integer gLastUpdate;

particles()
{
    if (gCondition != "rain" && gCondition != "snow")
    {
        llParticleSystem([]);
        return;
    }

    integer snow = gCondition == "snow";
    vector color = snow ? <0.95, 0.97, 1.0> : <0.60, 0.75, 0.95>;
    vector startScale = snow ? <0.16, 0.16, 0.0> : <0.035, 0.42, 0.0>;
    vector endScale = snow ? <0.10, 0.10, 0.0> : <0.02, 0.25, 0.0>;
    vector gravity = snow ? <0.3, 0.1, -0.9> : <0.1, 0.0, -5.5>;
    float age = snow ? 7.0 : 5.0;
    integer count = 1 + gIntensity / 18;
    if (count > 6) count = 6;
    if (count < 1) count = 1;

    llParticleSystem([
        PSYS_PART_FLAGS,
            PSYS_PART_INTERP_COLOR_MASK | PSYS_PART_INTERP_SCALE_MASK,
        PSYS_SRC_PATTERN, PSYS_SRC_PATTERN_EXPLODE,
        PSYS_PART_START_COLOR, color,
        PSYS_PART_END_COLOR, color,
        PSYS_PART_START_ALPHA, 0.85,
        PSYS_PART_END_ALPHA, 0.0,
        PSYS_PART_START_SCALE, startScale,
        PSYS_PART_END_SCALE, endScale,
        PSYS_PART_MAX_AGE, age,
        PSYS_SRC_BURST_RATE, 0.18,
        PSYS_SRC_BURST_PART_COUNT, count,
        PSYS_SRC_BURST_RADIUS, 8.0,
        PSYS_SRC_BURST_SPEED_MIN, snow ? 0.1 : 0.4,
        PSYS_SRC_BURST_SPEED_MAX, snow ? 0.9 : 1.6,
        PSYS_SRC_ACCEL, gravity,
        PSYS_SRC_MAX_AGE, 0.0
    ]);
}

default
{
    state_entry()
    {
        llParticleSystem([]);
        // Unsichtbarer Emitter-Prim; Partikel bleiben sichtbar.
        llSetAlpha(0.0, ALL_SIDES);
        llListen(CHANNEL, SOURCE, NULL_KEY, "");
        llSetTimerEvent(60.0);
    }

    listen(integer channel, string name, key id, string message)
    {
        // Nur authentisch serverseitig eingespeiste Region-Nachrichten.
        if (channel != CHANNEL || name != SOURCE || id != NULL_KEY) return;
        list p = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(p) != 8 ||
            llList2String(p, 0) != "OGLWX" ||
            llList2String(p, 1) != "1" ||
            llList2String(p, 2) != "NOW") return;

        string next = llList2String(p, 4);
        if (next != "sun" && next != "cloud" &&
            next != "rain" && next != "snow") return;

        integer intensity = llList2Integer(p, 6);
        if (intensity < 0 || intensity > 100) return;
        gLastUpdate = llGetUnixTime();

        // Nicht bei jeder 60-Sekunden-Nachricht Partikel neu starten.
        if (next != gCondition || intensity != gIntensity)
        {
            gCondition = next;
            gIntensity = intensity;
            particles();
        }
    }

    timer()
    {
        // Wettermodul offline -> niemals endlos regnen/schneien.
        if (gLastUpdate != 0 && llGetUnixTime() - gLastUpdate > 240)
        {
            gLastUpdate = 0;
            gCondition = "sun";
            gIntensity = 0;
            llParticleSystem([]);
        }
    }

    on_rez(integer p) { llResetScript(); }
}
