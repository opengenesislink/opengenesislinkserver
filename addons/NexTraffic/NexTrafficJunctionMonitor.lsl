// NexTraffic - non-interfering diagnostic monitor for one junction
// Shows both street directions. Does not control signals/vehicles.
integer CHANNEL = -835421;
string JUNCTION = "Hauptstrasse";
integer gLast;
string gA = "RED";
string gB = "RED";
integer gUntil;

show()
{
    if (gLast == 0 || llGetUnixTime() - gLast > 20)
    {
        llSetText("NexTraffic " + JUNCTION + "\nOFFLINE / STOP",
            <1.0, 0.25, 0.25>, 1.0);
        return;
    }
    integer remaining = gUntil - llGetUnixTime();
    if (remaining < 0) remaining = 0;
    vector tint = <0.8, 1.0, 0.8>;
    if (gA != "RED" && gB != "RED") tint = <1.0, 0.25, 0.25>;
    llSetText("NexTraffic " + JUNCTION + "\nA: " + gA +
        " | B: " + gB + "\n" + (string)remaining + " s",
        tint, 1.0);
}

default
{
    state_entry()
    {
        llListen(CHANNEL, "NexTraffic", NULL_KEY, "");
        llSetTimerEvent(1.0);
        show();
    }

    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != "NexTraffic" || id != NULL_KEY) return;
        list v = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(v) != 7 || llList2String(v, 0) != "NEXTRAFFIC" ||
            llList2String(v, 1) != "1" || llList2String(v, 2) != JUNCTION) return;
        string a = llList2String(v, 3);
        string b = llList2String(v, 4);
        if (a != "RED" && a != "GREEN" && a != "YELLOW") return;
        if (b != "RED" && b != "GREEN" && b != "YELLOW") return;
        // Reject inconsistent messages instead of displaying false safety.
        if (a != "RED" && b != "RED") return;
        integer seconds = llList2Integer(v, 5);
        integer stamp = llList2Integer(v, 6);
        if (seconds < 1 || seconds > 120 || llAbs(llGetUnixTime() - stamp) > 15)
            return;
        gA = a;
        gB = b;
        gLast = llGetUnixTime();
        gUntil = stamp + seconds;
        show();
    }

    timer() { show(); }
    on_rez(integer param) { llResetScript(); }
}
