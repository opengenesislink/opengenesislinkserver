// STADT NEXVERSE - OGLWeather 0.1
// Frontseite eines Prims: aktuelle Wetterlage + 5-Tage-Vorhersage.
// osSetDynamicTextureDataFace (OpenSimulator/OSSL) erforderlich.
// Neue Bild-UUID nur erzeugen, wenn sich etwas am Wetterbild aendert.
integer CHANNEL = -835311;
integer DISPLAY_FACE = 3;
string SOURCE = "OGLWeather";

string gCurrent = "";
list gDays = ["", "", "", "", ""];
string gRendered = "";
integer gLastReceived = 0;

string label(string code)
{
    if (code == "sun") return "Sonnig";
    if (code == "cloud") return "Bewoelkt";
    if (code == "rain") return "Regen";
    if (code == "snow") return "Schnee";
    return "Unbekannt";
}

draw()
{
    if (gCurrent == "") return;
    string signature = gCurrent + llDumpList2String(gDays, ";");
    if (signature == gRendered) return;

    list now = llParseStringKeepNulls(gCurrent, ["|"], []);
    string d = "";
    d = osSetPenColor(d, "black");
    d = osMovePen(d, 0, 0);
    d = osDrawFilledRectangle(d, 1024, 512);
    d = osSetFontName(d, "Arial");

    d = osSetPenColor(d, "yellow");
    d = osSetFontSize(d, 40);
    d = osMovePen(d, 30, 15);
    d = osDrawText(d, "STADT NEXVERSE");

    d = osSetPenColor(d, "white");
    d = osSetFontSize(d, 24);
    d = osMovePen(d, 32, 74);
    d = osDrawText(d, "WETTER  |  " + llGetRegionName());

    d = osSetPenColor(d, "yellow");
    d = osSetFontSize(d, 25);
    d = osMovePen(d, 35, 121);
    d = osDrawText(d, "JETZT: " + label(llList2String(now, 4)));

    d = osSetPenColor(d, "white");
    d = osSetFontSize(d, 26);
    d = osMovePen(d, 460, 121);
    d = osDrawText(d,
        llList2String(now, 5) + " C    Wind " +
        llList2String(now, 7) + " km/h");

    d = osSetPenColor(d, "gray");
    d = osMovePen(d, 30, 160);
    d = osDrawFilledRectangle(d, 964, 2);

    d = osSetPenColor(d, "yellow");
    d = osSetFontSize(d, 22);
    d = osMovePen(d, 35, 170);
    d = osDrawText(d, "5-TAGE-VORHERSAGE   MIN / MAX");

    integer i;
    for (i = 0; i < 5; i++)
    {
        string row = llList2String(gDays, i);
        integer y = 213 + i * 49;
        d = osSetPenColor(d, "white");
        d = osSetFontSize(d, 23);
        d = osMovePen(d, 35, y);
        if (row != "")
        {
            list p = llParseStringKeepNulls(row, ["|"], []);
            string date = llList2String(p, 4);
            d = osDrawText(d, date);
            d = osMovePen(d, 310, y);
            d = osDrawText(d, label(llList2String(p, 5)));
            d = osMovePen(d, 670, y);
            d = osDrawText(d,
                llList2String(p, 6) + " / " +
                llList2String(p, 7) + " C");
            d = osMovePen(d, 844, y);
            d = osDrawText(d, llList2String(p, 8) + "h nass");
        }
        else
        {
            d = osDrawText(d, "Vorhersage wird geladen ...");
        }
    }

    d = osSetPenColor(d, "gray");
    d = osSetFontSize(d, 17);
    d = osMovePen(d, 36, 481);
    d = osDrawText(d, "OGLWeather - simuliertes, verbindliches Grid-Wetter");

    // Kein Weisssetzen/Texture-Reset vor dem naechsten Render.
    osSetDynamicTextureDataFace("", "vector", d,
        "width:1024,height:512,alpha:255,bgcolour:black", 0, DISPLAY_FACE);
    gRendered = signature;
}

default
{
    state_entry()
    {
        llSetLinkPrimitiveParamsFast(LINK_THIS,
            [PRIM_FULLBRIGHT, DISPLAY_FACE, TRUE]);
        llListen(CHANNEL, SOURCE, NULL_KEY, "");
        llSetTimerEvent(60.0);
        // Vorhandene Face-Textur bewusst nicht loeschen.
    }

    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != SOURCE || id != NULL_KEY) return;
        list p = llParseStringKeepNulls(message, ["|"], []);
        if (llList2String(p, 0) != "OGLWX" ||
            llList2String(p, 1) != "1") return;
        string kind = llList2String(p, 2);
        if (kind == "NOW" && llGetListLength(p) == 8)
        {
            string condition = llList2String(p, 4);
            if (condition != "sun" && condition != "cloud" &&
                condition != "rain" && condition != "snow") return;
            gCurrent = message;
        }
        else if (kind == "DAY" && llGetListLength(p) == 9)
        {
            integer index = llList2Integer(p, 3);
            if (index < 0 || index > 4) return;
            gDays = llListReplaceList(gDays, [message], index, index);
        }
        else return;

        gLastReceived = llGetUnixTime();
        draw();
    }

    timer()
    {
        // Warnung anzeigen, wenn die Modul-Daten verschwunden sind.
        if (gLastReceived != 0 && llGetUnixTime() - gLastReceived > 900)
        {
            llSetText("OGLWeather: Daten veraltet / Simulator-Modul offline",
                <1.0, 0.65, 0.3>, 1.0);
        }
        else llSetText("", <1.0, 1.0, 1.0>, 0.0);
    }

    on_rez(integer p) { llResetScript(); }
}
