// NexTransit - static bus destination sign using the configured schedule.
// It DOES NOT steer, rez, teleport or represent a live physical bus.
integer CHANNEL = -835422;
string LINE = "101";
integer gLast;
string gVisible = "";

refresh()
{
    if (gLast == 0 || llGetUnixTime() - gLast > 150)
        llSetText("Linie " + LINE + "\nFahrplan offline",
            <1.0, 0.5, 0.1>, 1.0);
    else
        llSetText(gVisible, <1.0, 0.85, 0.2>, 1.0);
}

default
{
    state_entry()
    {
        llListen(CHANNEL, "NexTransit", NULL_KEY, "");
        llSetTimerEvent(60.0);
        refresh();
    }
    listen(integer ch, string name, key id, string message)
    {
        if (ch != CHANNEL || name != "NexTransit" || id != NULL_KEY) return;
        if (llStringLength(message) > 960) return;
        list fields = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(fields) != 4 ||
            llList2String(fields, 0) != "NEXTRANSIT" ||
            llList2String(fields, 1) != "1") return;
        list rows = llParseStringKeepNulls(llList2String(fields, 3), [";"], []);
        integer i;
        string found = "";
        for (i = 0; i < llGetListLength(rows); i++)
        {
            list cols = llParseStringKeepNulls(llList2String(rows, i), [","], []);
            if (llGetListLength(cols) != 4) return;
            if (llList2String(cols, 0) == LINE && found == "")
                found = "Linie " + LINE + "\nRichtung " +
                    llList2String(cols, 1) + "\n" +
                    llList2String(cols, 2) + " / " +
                    llList2String(cols, 3) + " min";
        }
        gLast = llGetUnixTime();
        if (found == "") found = "Linie " + LINE + "\nKeine Planabfahrt";
        gVisible = found;
        refresh();
    }
    timer() { refresh(); }
    on_rez(integer param) { llResetScript(); }
}
