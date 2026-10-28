// NEXTRAFFIC 0.1 / Stadt-NexVerse
// Linked signal prims must be named RED, YELLOW, GREEN.
// One script in the root; AXIS A or B for opposite street directions.
// Decorations only. Do not use as a real-world safety controller.
integer CHANNEL = -835421;
string JUNCTION = "Hauptstrasse";
string AXIS = "A";
integer LAST_SIGNAL;
string CURRENT = "RED";
integer REMAINING;

showSignal()
{
    integer i;
    for (i = 1; i <= llGetNumberOfPrims(); i++)
    {
        string name = llToUpper(llGetLinkName(i));
        vector tint;
        if (name == "RED") tint = <1.0, 0.0, 0.0>;
        else if (name == "YELLOW") tint = <1.0, 0.74, 0.0>;
        else if (name == "GREEN") tint = <0.0, 1.0, 0.15>;
        else jump nextLamp;

        integer active = (name == CURRENT);
        llSetLinkPrimitiveParamsFast(i, [
            PRIM_COLOR, ALL_SIDES, tint * (active ? 1.0 : 0.08), 1.0,
            PRIM_FULLBRIGHT, ALL_SIDES, active
        ]);
@nextLamp;
    }
    llSetText("NexTraffic " + JUNCTION + " [" + AXIS + "]\n" +
        CURRENT + "  " + (string)REMAINING + "s",
        <1.0, 1.0, 1.0>, 1.0);
}

default
{
    state_entry()
    {
        CURRENT = "RED";
        REMAINING = 0;
        showSignal();
        llListen(CHANNEL, "NexTraffic", NULL_KEY, "");
        llSetTimerEvent(5.0);
    }

    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != "NexTraffic" || id != NULL_KEY)
            return;
        list f = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(f) != 7 ||
            llList2String(f, 0) != "NEXTRAFFIC" ||
            llList2String(f, 1) != "1" ||
            llList2String(f, 2) != JUNCTION) return;

        string phase = llList2String(f, AXIS == "B" ? 4 : 3);
        if (phase != "GREEN" && phase != "YELLOW" && phase != "RED")
            return;
        integer remaining = llList2Integer(f, 5);
        integer serverTime = llList2Integer(f, 6);
        if (remaining < 1 || remaining > 120 ||
            llAbs(llGetUnixTime() - serverTime) > 15) return;

        LAST_SIGNAL = llGetUnixTime();
        REMAINING = remaining;
        if (CURRENT != phase)
        {
            CURRENT = phase;
            showSignal();
        }
    }

    timer()
    {
        // Fail closed: no traffic module -> every signal red.
        if (LAST_SIGNAL == 0 || llGetUnixTime() - LAST_SIGNAL > 20)
        {
            if (CURRENT != "RED")
            {
                CURRENT = "RED";
                REMAINING = 0;
                showSignal();
            }
        }
    }

    on_rez(integer code) { llResetScript(); }
}
