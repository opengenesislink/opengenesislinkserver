// ================================================================
// STADT NEXVERSE | OGLEnergy Grid Live Display
// 1024 x 512 VectorRender directly on a prim face
// Requires enabled OGLEnergy region module and OSSL VectorRender.
// Shows a SIMULATED virtual grid. No physical power or real billing.
// ================================================================
integer CHANNEL = -835431;
integer DISPLAY_FACE = 3;
integer STALE_AFTER = 240;
string gCondition = "";
integer gSupply;
integer gDemand;
integer gSolar;
integer gWind;
integer gHydro;
integer gBase;
integer gUtilization;
integer gReserve;
integer gUpdated;
string gRendered = "";

string rect(string d, integer x, integer y, integer w, integer h, string color)
{
    d = osSetPenColor(d, color);
    d = osMovePen(d, x, y);
    return osDrawFilledRectangle(d, w, h);
}
string textAt(string d, integer x, integer y, integer size,
    string color, string value)
{
    d = osSetPenColor(d, color);
    d = osSetFontSize(d, size);
    d = osMovePen(d, x, y);
    return osDrawText(d, value);
}
string statusText(string status)
{
    if (status == "NORMAL") return "NETZ STABIL";
    if (status == "WARN") return "NETZ UNTER LAST";
    return "STROMMANGEL";
}
integer minInt(integer a, integer b)
{
    if (a < b) return a;
    return b;
}
string bar(string d, integer x, integer y, integer value,
    integer maximum, string color)
{
    d = rect(d, x, y, 420, 13, "FF243B50");
    if (maximum <= 0) return d;
    integer pixels = value * 420 / maximum;
    pixels = minInt(420, pixels);
    if (pixels > 0) d = rect(d, x, y, pixels, 13, color);
    return d;
}
render()
{
    integer online = (gUpdated != 0 &&
        llGetUnixTime() - gUpdated <= STALE_AFTER);
    string signature = "OFFLINE";
    if (online)
        signature = gCondition + "|" + (string)gSupply + "|" +
            (string)gDemand + "|" + (string)gSolar + "|" +
            (string)gWind + "|" + (string)gHydro + "|" +
            (string)gBase + "|" + (string)gUtilization;
    if (signature == gRendered) return;

    string d = "";
    d = osSetFontName(d, "Arial");
    d = rect(d, 0, 0, 1024, 512, "FF081323");
    d = rect(d, 0, 0, 1024, 7, "FF33CCEA");
    d = rect(d, 0, 8, 1024, 75, "FF102238");
    d = textAt(d, 28, 24, 34, "FFFFFFFF", "STADT NEXVERSE");
    d = textAt(d, 765, 29, 22, "FF56D6F3", "Energiezentrale");
    d = rect(d, 28, 103, 470, 292, "FF132841");
    d = rect(d, 516, 103, 478, 292, "FF112238");
    d = rect(d, 28, 103, 7, 292, "FF39C8E9");
    d = textAt(d, 53, 121, 20, "FF8ABFCF", "STROMNETZ");

    if (!online)
    {
        d = textAt(d, 65, 200, 36, "FFFFFFFF", "Keine Netzdaten");
        d = textAt(d, 65, 266, 23, "FFFFBA7C", "Simulator Verbindung pruefen");
        d = textAt(d, 551, 215, 33, "FF9DC8DC", "Wird geladen ...");
    }
    else
    {
        string accent = "FF65DDBB";
        if (gCondition == "WARN") accent = "FFFFD16B";
        if (gCondition == "OUTAGE") accent = "FFFF7777";
        d = textAt(d, 55, 161, 43, accent, statusText(gCondition));
        d = textAt(d, 56, 229, 30, "FFFFFFFF",
            (string)gSupply + " kW");
        d = textAt(d, 57, 274, 20, "FF89ABC1", "ERZEUGUNG");
        d = textAt(d, 56, 312, 28, "FFFFFFFF",
            (string)gDemand + " kW");
        d = textAt(d, 57, 351, 20, "FF89ABC1", "VERBRAUCH");
        d = textAt(d, 547, 122, 21, "FF84BAD4", "STROMQUELLEN");
        d = textAt(d, 547, 174, 22, "FFFFD16B",
            "SOLAR   " + (string)gSolar + " kW");
        d = bar(d, 548, 207, gSolar, gSupply, "FFFFD16B");
        d = textAt(d, 547, 232, 22, "FF69CEF1",
            "WIND    " + (string)gWind + " kW");
        d = bar(d, 548, 265, gWind, gSupply, "FF69CEF1");
        d = textAt(d, 547, 288, 22, "FF90DDC2",
            "WASSER  " + (string)gHydro + " kW");
        d = bar(d, 548, 319, gHydro, gSupply, "FF90DDC2");
        d = textAt(d, 547, 343, 20, "FFB9C4D3",
            "GRUNDLAST   " + (string)gBase + " kW");
        d = bar(d, 548, 375, gBase, gSupply, "FFB9C4D3");
    }

    string region = llDumpList2String(
        llParseString2List(llGetRegionName(), [";"], []), " ");
    if (llStringLength(region) > 22)
        region = llGetSubString(region, 0, 21);
    d = textAt(d, 30, 421, 20, "FFCEE1EF", "REGION  " + region);
    d = textAt(d, 30, 461, 18, "FF7D9AAF",
        "OGLEnergy | virtuelles Stromnetz, keine reale Messung");
    if (online)
    {
        string reserve = (string)gReserve + " kW";
        d = textAt(d, 755, 423, 20, "FFCEE1EF", "RESERVE");
        d = textAt(d, 755, 458, 21, "FF56D6F3", reserve);
    }
    else
        d = textAt(d, 756, 460, 18, "FFFFBA7C", "OFFLINE");

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
        llListen(CHANNEL, "OGLEnergy", NULL_KEY, "");
        llSetTimerEvent(30.0);
        render();
    }
    listen(integer ch, string name, key id, string wire)
    {
        if (ch != CHANNEL || name != "OGLEnergy" || id != NULL_KEY) return;
        if (llStringLength(wire) > 200) return;
        list f = llParseStringKeepNulls(wire, ["|"], []);
        if (llGetListLength(f) != 12 ||
            llList2String(f, 0) != "OGLENERGY" ||
            llList2String(f, 1) != "1") return;
        string status = llList2String(f, 3);
        if (status != "NORMAL" && status != "WARN" &&
            status != "OUTAGE") return;
        integer supply = llList2Integer(f, 4);
        integer demand = llList2Integer(f, 5);
        integer solar = llList2Integer(f, 6);
        integer wind = llList2Integer(f, 7);
        integer hydro = llList2Integer(f, 8);
        integer base = llList2Integer(f, 9);
        integer util = llList2Integer(f, 10);
        integer reserve = llList2Integer(f, 11);
        if (supply < 0 || supply > 400000 ||
            demand < 1 || demand > 150000 ||
            solar < 0 || wind < 0 || hydro < 0 || base < 0 ||
            solar > 100000 || wind > 100000 ||
            hydro > 100000 || base > 100000 ||
            util < 0 || util > 999 ||
            supply != solar + wind + hydro + base ||
            reserve != supply - demand) return;
        gCondition = status;
        gSupply = supply;
        gDemand = demand;
        gSolar = solar;
        gWind = wind;
        gHydro = hydro;
        gBase = base;
        gUtilization = util;
        gReserve = reserve;
        gUpdated = llGetUnixTime();
        render();
    }
    timer() { render(); }
    on_rez(integer start) { llResetScript(); }
}
