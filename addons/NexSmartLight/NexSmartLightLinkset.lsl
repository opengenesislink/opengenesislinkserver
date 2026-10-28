// NexSmartLight - controls all lamp parts named LIGHT_* in one linked object.
// Only named lamp prims are affected; avoid changing the rest of the model.
// Requires the server's NexSmartLight module; no 30s polling.
integer CHANNEL = -835423;
string PREFIX = "LIGHT_";
integer SERVER_TIMEOUT = 300;
integer FALLBACK_TO_SIM_SUN = FALSE;
integer gLast = 0;
integer gMode = -1;
vector LIGHT_COLOR = <1.0, 0.88, 0.68>;
float LIGHT_INTENSITY = 0.95;
float LIGHT_RADIUS = 13.0;
float LIGHT_FALLOFF = 0.5;

switchLights(integer on)
{
    if (on == gMode) return;
    gMode = on;
    integer i;
    integer total = llGetNumberOfPrims();
    for (i = 1; i <= total; i++)
    {
        string name = llToUpper(llGetLinkName(i));
        if (llSubStringIndex(name, PREFIX) == 0)
        {
            float glow = 0.0;
            if (on) glow = 0.06;
            llSetLinkPrimitiveParamsFast(i, [
                PRIM_POINT_LIGHT, on, LIGHT_COLOR, LIGHT_INTENSITY,
                    LIGHT_RADIUS, LIGHT_FALLOFF,
                PRIM_FULLBRIGHT, ALL_SIDES, on,
                PRIM_GLOW, ALL_SIDES, glow
            ]);
        }
    }
}

default
{
    state_entry()
    {
        llListen(CHANNEL, "NexSmartLight", NULL_KEY, "");
        switchLights(FALSE);
        llSetTimerEvent(120.0);
    }
    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != "NexSmartLight" || id != NULL_KEY)
            return;
        list f = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(f) != 4 ||
            llList2String(f, 0) != "NEXLIGHT" ||
            llList2String(f, 1) != "1") return;
        string mode = llList2String(f, 2);
        if (mode != "ON" && mode != "OFF") return;
        gLast = llGetUnixTime();
        switchLights(mode == "ON");
    }
    timer()
    {
        if (gLast != 0 && llGetUnixTime() - gLast <= SERVER_TIMEOUT) return;
        if (FALLBACK_TO_SIM_SUN) switchLights(llGetSunDirection().z < -0.05);
        else switchLights(FALSE);
    }
    changed(integer code)
    {
        if (code & CHANGED_LINK) { gMode = -1; switchLights(FALSE); }
    }
    on_rez(integer param) { llResetScript(); }
}
