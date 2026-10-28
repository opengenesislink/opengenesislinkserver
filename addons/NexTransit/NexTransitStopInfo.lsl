// NexTransit - one-prim stop information and private touch response.
// No OSSL: standard LSL only; no teleport and no actual vehicle tracking.
integer CHANNEL = -835422;
string STOP_NAME = "Stadt-NexVerse";
integer gLast;
string gStatus = "Lade Fahrplan...";
list gDepartures = [];

render()
{
    if (gLast == 0 || llGetUnixTime() - gLast > 150)
    {
        llSetText(STOP_NAME + "\nFahrplan offline",
            <1.0, 0.45, 0.2>, 1.0);
        return;
    }
    string output = STOP_NAME + "\n";
    integer count = llGetListLength(gDepartures);
    if (count == 0) output += "Keine Planabfahrten";
    integer i;
    for (i = 0; i < count && i < 3; i++)
    {
        list row = llParseStringKeepNulls(llList2String(gDepartures, i), [","], []);
        if (llGetListLength(row) == 4)
            output += llList2String(row, 0) + " " +
                llList2String(row, 1) + " - " +
                llList2String(row, 3) + " min\n";
    }
    llSetText(output, <0.95, 0.95, 0.75>, 1.0);
}

default
{
    state_entry()
    {
        llListen(CHANNEL, "NexTransit", NULL_KEY, "");
        llSetTimerEvent(60.0);
        render();
    }
    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != "NexTransit" || id != NULL_KEY)
            return;
        if (llStringLength(message) > 960) return;
        list f = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(f) != 4 ||
            llList2String(f, 0) != "NEXTRANSIT" ||
            llList2String(f, 1) != "1") return;
        string data = llList2String(f, 3);
        list rows = [];
        if (data != "") rows = llParseStringKeepNulls(data, [";"], []);
        integer i;
        for (i = 0; i < llGetListLength(rows); i++)
        {
            list cols = llParseStringKeepNulls(llList2String(rows, i), [","], []);
            if (llGetListLength(cols) != 4) return;
            integer mins = llList2Integer(cols, 3);
            if (mins < 0 || mins > 1440) return;
        }
        gDepartures = rows;
        gStatus = llList2String(f, 2);
        gLast = llGetUnixTime();
        render();
    }
    touch_start(integer total)
    {
        key user = llDetectedKey(0);
        if (gLast == 0 || llGetUnixTime() - gLast > 150)
        {
            llInstantMessage(user, "NexTransit: Fahrplandaten nicht verfuegbar.");
            return;
        }
        string response = "NexTransit " + STOP_NAME + " (" + gStatus + ")\n";
        integer i;
        for (i = 0; i < llGetListLength(gDepartures); i++)
        {
            list cols = llParseStringKeepNulls(llList2String(gDepartures, i), [","], []);
            response += llList2String(cols, 0) + " Richtung " +
                llList2String(cols, 1) + " um " +
                llList2String(cols, 2) + " (" +
                llList2String(cols, 3) + " min)\n";
        }
        if (llGetListLength(gDepartures) == 0) response += "Keine Abfahrten";
        llInstantMessage(user, response);
    }
    timer()
    {
        // The published minutes may be one minute old; display them only
        // while recent and do not claim to track actual vehicle arrivals.
        render();
    }
    on_rez(integer param) { llResetScript(); }
}
