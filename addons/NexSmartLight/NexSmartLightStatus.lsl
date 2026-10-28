// NexSmartLight - diagnostics/status sign; never overrides lamp objects.
integer CHANNEL = -835423;
integer gLast = 0;
string gStatus = "OFF";
string gClock = "";

render()
{
    if (gLast == 0 || llGetUnixTime() - gLast > 300)
    {
        llSetText("NexSmartLight\nKeine Verbindung",
            <1.0, 0.35, 0.2>, 1.0);
        return;
    }
    vector tint = <0.7, 0.85, 1.0>;
    if (gStatus == "ON") tint = <1.0, 0.9, 0.5>;
    llSetText("NexSmartLight\n" + gStatus + "\n" + gClock,
        tint, 1.0);
}

default
{
    state_entry()
    {
        llListen(CHANNEL, "NexSmartLight", NULL_KEY, "");
        llSetTimerEvent(60.0);
        render();
    }
    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != "NexSmartLight" || id != NULL_KEY)
            return;
        list v = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(v) != 4 ||
            llList2String(v, 0) != "NEXLIGHT" ||
            llList2String(v, 1) != "1") return;
        string status = llList2String(v, 2);
        if (status != "ON" && status != "OFF") return;
        gStatus = status;
        gClock = llList2String(v, 3);
        gLast = llGetUnixTime();
        render();
    }
    timer() { render(); }
    on_rez(integer param) { llResetScript(); }
}
