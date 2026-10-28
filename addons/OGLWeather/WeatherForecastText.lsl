// OGLWeather - five-day hovertext forecast, no OSSL texture permissions.
// A display prim is optional; NOT needed for rainfall/snowfall.
integer CHANNEL = -835311;
list gDays = ["", "", "", "", ""];
integer gLast = 0;

string title(string code)
{
    if (code == "sun") return "Sonnig";
    if (code == "cloud") return "Bewoelkt";
    if (code == "rain") return "Regen";
    if (code == "snow") return "Schnee";
    return "n/a";
}

render()
{
    if (gLast == 0 || llGetUnixTime() - gLast > 900)
    {
        llSetText("OGLWeather\nVorhersage wird geladen...",
            <1.0, 0.7, 0.3>, 1.0);
        return;
    }
    string result = "STADT NEXVERSE\nWetter-Vorhersage\n";
    integer i;
    for (i = 0; i < 5; i++)
    {
        string line = llList2String(gDays, i);
        if (line != "")
        {
            list row = llParseStringKeepNulls(line, ["|"], []);
            result += llList2String(row, 4) + "  " +
                title(llList2String(row, 5)) + "  " +
                llList2String(row, 6) + "/" + llList2String(row, 7) + " C\n";
        }
    }
    llSetText(result, <0.85, 0.95, 1.0>, 1.0);
}

default
{
    state_entry()
    {
        llListen(CHANNEL, "OGLWeather", NULL_KEY, "");
        llSetTimerEvent(60.0);
        render();
    }
    listen(integer ch, string name, key id, string message)
    {
        if (ch != CHANNEL || name != "OGLWeather" || id != NULL_KEY) return;
        list p = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(p) != 9 || llList2String(p, 0) != "OGLWX" ||
            llList2String(p, 1) != "1" || llList2String(p, 2) != "DAY") return;
        integer slot = llList2Integer(p, 3);
        if (slot < 0 || slot >= 5) return;
        string condition = llList2String(p, 5);
        if (condition != "sun" && condition != "cloud" &&
            condition != "rain" && condition != "snow") return;

        if (slot == 0 && llList2String(gDays, 0) != "")
        {
            list old = llParseStringKeepNulls(llList2String(gDays, 0), ["|"], []);
            if (llList2String(old, 4) != llList2String(p, 4))
                gDays = ["", "", "", "", ""];
        }
        gDays = llListReplaceList(gDays, [message], slot, slot);
        gLast = llGetUnixTime();
        render();
    }
    timer() { render(); }
    on_rez(integer param) { llResetScript(); }
}
