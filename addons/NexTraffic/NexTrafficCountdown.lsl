// NexTraffic - countdown for one junction axis (LSL, no OSSL)
// Put into a countdown sign prim. Matches NexTrafficModel.Wire.
// JUNCTION and AXIS must match the signal scripts next to it.
integer CHANNEL = -835421;
string JUNCTION = "Hauptstrasse";
string AXIS = "A"; // "A" or "B"
integer gServerTime = 0;
integer gRemaining = 0;
integer gReceived = 0;
string gPhase = "RED";
string gShown = "";

render()
{
    integer seconds = gRemaining - (llGetUnixTime() - gServerTime);
    if (seconds < 0) seconds = 0;
    string mode = "ROT";
    vector color = <1.0, 0.25, 0.25>;
    if (gPhase == "GREEN")
    {
        mode = "GRUEN";
        color = <0.3, 1.0, 0.4>;
    }
    else if (gPhase == "YELLOW")
    {
        mode = "GELB";
        color = <1.0, 0.8, 0.2>;
    }
    if (gReceived == 0 || llGetUnixTime() - gReceived > 15)
    {
        mode = "KEINE DATEN - ROT";
        color = <1.0, 0.2, 0.2>;
        seconds = 0;
    }
    string textValue = JUNCTION + " / " + AXIS + "\n" + mode + "\n" + (string)seconds + " s";
    if (textValue != gShown)
    {
        gShown = textValue;
        llSetText(textValue, color, 1.0);
    }
}

default
{
    state_entry()
    {
        llListen(CHANNEL, "NexTraffic", NULL_KEY, "");
        llSetTimerEvent(1.0);
        render();
    }

    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != "NexTraffic" || id != NULL_KEY) return;
        list fields = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(fields) != 7 ||
            llList2String(fields, 0) != "NEXTRAFFIC" ||
            llList2String(fields, 1) != "1" ||
            llList2String(fields, 2) != JUNCTION) return;
        integer column = 3;
        if (AXIS == "B") column = 4;
        string phase = llList2String(fields, column);
        if (phase != "GREEN" && phase != "RED" && phase != "YELLOW") return;
        integer remaining = llList2Integer(fields, 5);
        integer stamp = llList2Integer(fields, 6);
        if (remaining < 1 || remaining > 120 ||
            llAbs(llGetUnixTime() - stamp) > 15) return;
        gReceived = llGetUnixTime();
        gServerTime = stamp;
        gRemaining = remaining;
        gPhase = phase;
        render();
    }

    timer() { render(); }
    on_rez(integer param) { llResetScript(); }
}
