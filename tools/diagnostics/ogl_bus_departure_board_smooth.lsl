// STADT NEXVERSE – BUS ABFAHRTSTAFEL (smooth-refresh edition)
// OpenGenesisLINK Generation 1, osSetDynamicTextureDataFace
// Installieren: [Textures] DynamicTexturePreviousRetentionSeconds = 30
// Die alte Anzeige bleibt auf dem Prim, bis der Renderer die neue liefert.
// Der Viewer lädt neue UUIDs dennoch asynchron; 100 % flackerfrei ist
// ohne eine viewer-seitige Umschaltbestätigung nicht garantiert.

integer DISPLAY_FACE = 3;
integer UTC_OFFSET = 1;  // Im Sommer manuell auf 2 stellen.

list ROUTEN = [
    "101", "Magdeburg",       10,  0,
    "120", "Hamburg",         20,  5,
    "130", "Freiburg Sued",   15, 10,
    "140", "Gummybear Island",30, 12,
    "150", "Jenseits",        20, 15
];

integer lastMinute = -1;
string lastDrawHash = "";

string zwei(integer n)
{
    if (n < 10) return "0" + (string)n;
    return (string)n;
}

string formatTime(integer minutes)
{
    minutes = (minutes + 1440) % 1440;
    return zwei(minutes / 60) + ":" + zwei(minutes % 60);
}

list berechneAbfahrten()
{
    integer localMinute = ((llGetUnixTime() / 60) + UTC_OFFSET * 60) % 1440;
    list abfahrten = [];

    integer i;
    for (i = 0; i < llGetListLength(ROUTEN); i += 4)
    {
        string linie = llList2String(ROUTEN, i);
        string ziel = llList2String(ROUTEN, i + 1);
        integer takt = llList2Integer(ROUTEN, i + 2);
        integer start = llList2Integer(ROUTEN, i + 3);

        if (takt > 0)
        {
            integer rest = (localMinute - start + 1440) % takt;
            integer warten = (takt - rest) % takt;
            integer abfahrt = (localMinute + warten) % 1440;

            abfahrten += [warten, linie, ziel, abfahrt];
        }
    }
    return llListSort(abfahrten, 4, TRUE);
}

anzeigeAktualisieren()
{
    integer minute = llGetUnixTime() / 60;

    // Gleiche Minute? Dann keine neue UUID generieren – auch bei Touch.
    if (minute == lastMinute) return;

    list daten = berechneAbfahrten();
    string d = "";

    d = osSetPenColor(d, "black");
    d = osMovePen(d, 0, 0);
    d = osDrawFilledRectangle(d, 1024, 512);
    d = osSetFontName(d, "Arial");

    d = osSetPenColor(d, "yellow");
    d = osSetFontSize(d, 39);
    d = osMovePen(d, 35, 20);
    d = osDrawText(d, "STADT NEXVERSE");

    d = osSetPenColor(d, "white");
    d = osSetFontSize(d, 21);
    d = osMovePen(d, 38, 76);
    d = osDrawText(d, "BUS ABFAHRTEN  |  " + llGetRegionName());

    d = osSetPenColor(d, "yellow");
    d = osSetFontSize(d, 23);
    d = osMovePen(d, 42, 122);
    d = osDrawText(d, "LINIE");
    d = osMovePen(d, 190, 122);
    d = osDrawText(d, "ZIELREGION");
    d = osMovePen(d, 700, 122);
    d = osDrawText(d, "ZEIT");
    d = osMovePen(d, 866, 122);
    d = osDrawText(d, "IN");

    d = osSetPenColor(d, "gray");
    d = osMovePen(d, 30, 155);
    d = osDrawFilledRectangle(d, 964, 2);

    integer i;
    integer zeilen = llGetListLength(daten) / 4;
    if (zeilen > 5) zeilen = 5;

    for (i = 0; i < zeilen; ++i)
    {
        integer index = i * 4;
        integer warten = llList2Integer(daten, index);
        string linie = llList2String(daten, index + 1);
        string ziel = llList2String(daten, index + 2);
        integer abfahrt = llList2Integer(daten, index + 3);
        integer y = 166 + i * 60;

        d = osSetPenColor(d, "yellow");
        d = osMovePen(d, 36, y);
        d = osDrawFilledRectangle(d, 110, 45);

        d = osSetPenColor(d, "black");
        d = osSetFontSize(d, 28);
        d = osMovePen(d, 49, y + 6);
        d = osDrawText(d, linie);

        d = osSetPenColor(d, "white");
        d = osSetFontSize(d, 29);
        d = osMovePen(d, 190, y + 7);
        d = osDrawText(d, ziel);

        d = osSetPenColor(d, "white");
        d = osSetFontSize(d, 27);
        d = osMovePen(d, 704, y + 9);
        d = osDrawText(d, formatTime(abfahrt));

        if (warten == 0)
        {
            d = osSetPenColor(d, "lime");
            d = osSetFontSize(d, 27);
            d = osMovePen(d, 858, y + 9);
            d = osDrawText(d, "JETZT");
        }
        else
        {
            d = osSetPenColor(d, "yellow");
            d = osSetFontSize(d, 27);
            d = osMovePen(d, 860, y + 9);
            d = osDrawText(d, (string)warten + " Min");
        }

        if (i < zeilen - 1)
        {
            d = osSetPenColor(d, "gray");
            d = osMovePen(d, 36, y + 53);
            d = osDrawFilledRectangle(d, 950, 1);
        }
    }

    d = osSetPenColor(d, "gray");
    d = osSetFontSize(d, 17);
    d = osMovePen(d, 38, 481);
    d = osDrawText(d, "STADT NEXVERSE VERKEHRSSYSTEM");

    // Gleicher Bildinhalt? Keine neue Asset-ID und kein Viewer-Reload.
    string nextHash = llMD5String(d, 0);
    if (nextHash != lastDrawHash)
    {
        osSetDynamicTextureDataFace(
            "",
            "vector",
            d,
            "width:1024,height:512,alpha:255,bgcolour:black",
            0,
            DISPLAY_FACE
        );
        lastDrawHash = nextHash;
    }
    lastMinute = minute;
}

default
{
    state_entry()
    {
        // Absichtlich kein llSetTexture/llSetColor: alte Anzeige nicht leeren.
        llSetLinkPrimitiveParamsFast(LINK_THIS,
            [PRIM_FULLBRIGHT, DISPLAY_FACE, TRUE]);

        anzeigeAktualisieren();
        llSetTimerEvent(10.0);
    }

    timer()
    {
        anzeigeAktualisieren();
    }

    touch_start(integer total_number)
    {
        if (llDetectedKey(0) == llGetOwner())
            anzeigeAktualisieren();
    }

    on_rez(integer start_param)
    {
        llResetScript();
    }
}
