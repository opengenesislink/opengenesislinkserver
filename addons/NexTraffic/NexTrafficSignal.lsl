// NexTraffic 0.2 - two-axis synchronized inworld traffic signal.
// Linkset: lamp prims MUST be named RED, YELLOW, GREEN.
// Put one script in each signal root; configure JUNCTION and AXIS.
// Uses server region chat. Visual signals only, NOT physical road safety.
integer CHANNEL = -835421;
string JUNCTION = "Hauptstrasse";
string AXIS = "A"; // "A" or "B"
string CURRENT = "RED";
integer LAST_SIGNAL = 0;
integer PHASE_END = 0;
integer LAST_CAPTION = -1;
string LAST_PHASE = "";

updateCaption()
{
    integer seconds = PHASE_END - llGetUnixTime();
    if (seconds < 0) seconds = 0;
    if (CURRENT == LAST_PHASE && seconds == LAST_CAPTION) return;
    LAST_PHASE = CURRENT;
    LAST_CAPTION = seconds;
    llSetText("NexTraffic " + JUNCTION + " [" + AXIS + "]\n" +
        CURRENT + "  " + (string)seconds + "s",
        <1.0, 1.0, 1.0>, 1.0);
}

setLamps()
{
    integer i;
    for (i = 1; i <= llGetNumberOfPrims(); i++)
    {
        string name = llToUpper(llGetLinkName(i));
        if (name == "RED" || name == "YELLOW" || name == "GREEN")
        {
            vector tint = <0.0, 0.0, 0.0>;
            if (name == "RED") tint = <1.0, 0.0, 0.0>;
            else if (name == "YELLOW") tint = <1.0, 0.74, 0.0>;
            else if (name == "GREEN") tint = <0.0, 1.0, 0.15>;
            integer active = (name == CURRENT);
            float brightness = 0.08;
            if (active) brightness = 1.0;
            llSetLinkPrimitiveParamsFast(i, [
                PRIM_COLOR, ALL_SIDES, tint * brightness, 1.0,
                PRIM_FULLBRIGHT, ALL_SIDES, active
            ]);
        }
    }
    updateCaption();
}

default
{
    state_entry()
    {
        CURRENT = "RED";
        PHASE_END = 0;
        setLamps();
        llListen(CHANNEL, "NexTraffic", NULL_KEY, "");
        llSetTimerEvent(1.0);
    }
    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != "NexTraffic" || id != NULL_KEY)
            return;
        if (AXIS != "A" && AXIS != "B") return;
        list f = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(f) != 7 ||
            llList2String(f, 0) != "NEXTRAFFIC" ||
            llList2String(f, 1) != "1" ||
            llList2String(f, 2) != JUNCTION) return;
        integer column = 3;
        if (AXIS == "B") column = 4;
        string phase = llList2String(f, column);
        string other = llList2String(f, 4);
        if (AXIS == "B") other = llList2String(f, 3);
        if (phase != "RED" && phase != "GREEN" && phase != "YELLOW")
            return;
        if (other != "RED" && other != "GREEN" && other != "YELLOW")
            return;
        // A vehicle direction can only move if the other is red.
        if (phase != "RED" && other != "RED") return;
        integer remaining = llList2Integer(f, 5);
        integer serverTime = llList2Integer(f, 6);
        if (remaining < 1 || remaining > 120 ||
            llAbs(llGetUnixTime() - serverTime) > 15) return;
        LAST_SIGNAL = llGetUnixTime();
        PHASE_END = serverTime + remaining;
        if (phase != CURRENT)
        {
            CURRENT = phase;
            setLamps();
        }
        else updateCaption();
    }
    timer()
    {
        // Fail visibly red if module stops or data no longer arrive.
        if (LAST_SIGNAL == 0 || llGetUnixTime() - LAST_SIGNAL > 20)
        {
            PHASE_END = 0;
            if (CURRENT != "RED")
            {
                CURRENT = "RED";
                setLamps();
            }
            else updateCaption();
        }
        else updateCaption();
    }
    changed(integer change)
    {
        if (change & CHANGED_LINK) setLamps();
    }
    on_rez(integer param) { llResetScript(); }
}
