// NEXSMARTLIGHT 0.1 – automatic city streetlight
// Put in the lamp's light-emitting prim. No scanning every 30 seconds.
// Server broadcasts local civil time with DST; optional OS sun fallback.
integer CHANNEL = -835423;
integer USE_OS_SUN_FALLBACK = FALSE; // TRUE: use llGetSunDirection().z
integer ACTIVE = FALSE;
integer LAST_SERVER;
vector LIGHT_COLOR = <1.0, 0.86, 0.64>;
float LIGHT_INTENSITY = 0.95;
float LIGHT_RADIUS = 13.0;
float LIGHT_FALLOFF = 0.5;

updateLight(integer enable)
{
    if (ACTIVE == enable) return;
    ACTIVE = enable;
    float glow = 0.0;
    if (enable) glow = 0.07;
    llSetLinkPrimitiveParamsFast(LINK_THIS, [
        PRIM_POINT_LIGHT, enable, LIGHT_COLOR, LIGHT_INTENSITY,
            LIGHT_RADIUS, LIGHT_FALLOFF,
        PRIM_FULLBRIGHT, ALL_SIDES, enable,
        PRIM_GLOW, ALL_SIDES, glow
    ]);
}

default
{
    state_entry()
    {
        // Start safely unlit until the first trusted module update.
        ACTIVE = TRUE;
        updateLight(FALSE);
        llListen(CHANNEL, "NexSmartLight", NULL_KEY, "");
        llSetTimerEvent(120.0);
    }

    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != "NexSmartLight" || id != NULL_KEY)
            return;
        list fields = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(fields) != 4 ||
            llList2String(fields, 0) != "NEXLIGHT" ||
            llList2String(fields, 1) != "1") return;
        string mode = llList2String(fields, 2);
        if (mode != "ON" && mode != "OFF") return;
        LAST_SERVER = llGetUnixTime();
        updateLight(mode == "ON");
    }

    timer()
    {
        if (LAST_SERVER != 0 && llGetUnixTime() - LAST_SERVER <= 300)
            return;
        if (USE_OS_SUN_FALLBACK)
            updateLight(llGetSunDirection().z < -0.05);
        else
            updateLight(FALSE);
    }

    on_rez(integer code) { llResetScript(); }
}
