// NEXTRANSIT 0.1 – Stadt-NexVerse
// Place in any prim; DISPLAY_FACE 3 is +Y on a standard cube.
// No inventaried scripts or route data needed in the prim.
// Server broadcasts full timetable in one validated message.
integer CHANNEL = -835422;
integer DISPLAY_FACE = 3;
string LAST_WIRE = "";
integer LAST_RECV;

drawBoard(string wire)
{
    if (wire == LAST_WIRE) return;
    list p = llParseStringKeepNulls(wire, ["|"], []);
    if (llGetListLength(p) != 4 ||
        llList2String(p, 0) != "NEXTRANSIT" ||
        llList2String(p, 1) != "1") return;
    string rawRoutes = llList2String(p, 3);
    list routes = [];
    if (rawRoutes != "") routes = llParseStringKeepNulls(rawRoutes, [";"], []);
    string d = "";
    d = osSetPenColor(d, "black");
    d = osMovePen(d, 0, 0);
    d = osDrawFilledRectangle(d, 1024, 512);
    d = osSetFontName(d, "Arial");
    d = osSetPenColor(d, "yellow");
    d = osSetFontSize(d, 38);
    d = osMovePen(d, 32, 20);
    d = osDrawText(d, "STADT NEXVERSE");
    d = osSetPenColor(d, "white");
    d = osSetFontSize(d, 22);
    d = osMovePen(d, 35, 73);
    d = osDrawText(d, "NEXTRANSIT  |  " + llGetRegionName());
    d = osSetPenColor(d, "yellow");
    d = osSetFontSize(d, 22);
    d = osMovePen(d, 35, 127);
    d = osDrawText(d, "LINIE");
    d = osMovePen(d, 180, 127);
    d = osDrawText(d, "ZIEL");
    d = osMovePen(d, 725, 127);
    d = osDrawText(d, "ZEIT");
    d = osMovePen(d, 870, 127);
    d = osDrawText(d, "IN");
    integer n = llGetListLength(routes);
    if (n > 5) n = 5;
    integer i;
    for (i = 0; i < n; i++)
    {
        list row = llParseStringKeepNulls(llList2String(routes, i), [","], []);
        if (llGetListLength(row) != 4) return;
        integer y = 173 + 58 * i;
        d = osSetPenColor(d, "yellow");
        d = osSetFontSize(d, 26);
        d = osMovePen(d, 38, y);
        d = osDrawText(d, llList2String(row, 0));
        d = osSetPenColor(d, "white");
        d = osMovePen(d, 177, y);
        d = osDrawText(d, llList2String(row, 1));
        d = osMovePen(d, 720, y);
        d = osDrawText(d, llList2String(row, 2));
        d = osSetPenColor(d, "yellow");
        d = osMovePen(d, 872, y);
        integer remaining = llList2Integer(row, 3);
        if (remaining == 0) d = osDrawText(d, "JETZT");
        else d = osDrawText(d, (string)remaining + "m");
    }
    d = osSetPenColor(d, "gray");
    d = osSetFontSize(d, 16);
    d = osMovePen(d, 34, 482);
    d = osDrawText(d, "NEXTRANSIT  |  Planabfahrten  |  " + llList2String(p, 2));
    // Retain the previous texture until the new image is ready.
    osSetDynamicTextureDataFace("", "vector", d,
        "width:1024,height:512,alpha:255,bgcolour:black",
        0, DISPLAY_FACE);
    LAST_WIRE = wire;
}

default
{
    state_entry()
    {
        llSetLinkPrimitiveParamsFast(LINK_THIS,
            [PRIM_FULLBRIGHT, DISPLAY_FACE, TRUE]);
        llListen(CHANNEL, "NexTransit", NULL_KEY, "");
        llSetTimerEvent(120.0);
    }

    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != "NexTransit" || id != NULL_KEY)
            return;
        if (llStringLength(message) > 960) return;
        list p = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(p) != 4 ||
            llList2String(p, 0) != "NEXTRANSIT" ||
            llList2String(p, 1) != "1") return;
        // Validate the WHOLE message before updating the offline watchdog.
        // Untrusted or malformed rows must never replace the last good sign.
        string payload = llList2String(p, 3);
        list rows = [];
        if (payload != "") rows = llParseStringKeepNulls(payload, [";"], []);
        if (llGetListLength(rows) > 10) return;
        integer i;
        for (i = 0; i < llGetListLength(rows); i++)
        {
            list row = llParseStringKeepNulls(llList2String(rows, i), [","], []);
            if (llGetListLength(row) != 4) return;
            integer wait = llList2Integer(row, 3);
            if (wait < 0 || wait > 1440) return;
        }
        LAST_RECV = llGetUnixTime();
        drawBoard(message);
    }

    timer()
    {
        if (LAST_RECV != 0 && llGetUnixTime() - LAST_RECV > 300)
            llSetText("NexTransit: Fahrplan-Daten veraltet",
                <1.0, 0.5, 0.2>, 1.0);
        else
            llSetText("", <1.0, 1.0, 1.0>, 0.0);
    }

    on_rez(integer code) { llResetScript(); }
}
