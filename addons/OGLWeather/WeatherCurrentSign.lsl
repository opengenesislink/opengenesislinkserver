// OGLWeather - compact, standard LSL weather sign with temperature.
// Not a weather emitter; rain/snow is managed by server-side C#.
integer CHANNEL = -835311;
integer LAST = 0;
string gStatus = "";
integer gTemperature;
integer gWind;
integer gIntensity;
string gForecastTime;

string weatherName(string code)
{
    if (code == "sun") return "Sonnig";
    if (code == "cloud") return "Bewoelkt";
    if (code == "rain") return "Regen";
    if (code == "snow") return "Schnee";
    return "Unbekannt";
}
render()
{
    if (LAST == 0 || llGetUnixTime() - LAST > 240)
    {
        llSetText("OGLWeather\nWetterdienst nicht erreichbar",
            <1.0, 0.4, 0.25>, 1.0);
        return;
    }
    vector tint = <0.65, 0.9, 1.0>;
    if (gStatus == "sun") tint = <1.0, 0.9, 0.4>;
    if (gStatus == "snow") tint = <0.85, 0.95, 1.0>;
    string textValue = "STADT NEXVERSE\n" + weatherName(gStatus) +
        "  " + (string)gTemperature + " C\n" +
        "Wind " + (string)gWind + " km/h";
    if (gStatus == "rain" || gStatus == "snow")
        textValue += " | " + (string)gIntensity + "%";
    llSetText(textValue, tint, 1.0);
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
        list f = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(f) != 8 || llList2String(f, 0) != "OGLWX" ||
            llList2String(f, 1) != "1" || llList2String(f, 2) != "NOW")
            return;
        string status = llList2String(f, 4);
        if (status != "sun" && status != "cloud" &&
            status != "rain" && status != "snow") return;
        integer intensity = llList2Integer(f, 6);
        integer wind = llList2Integer(f, 7);
        if (intensity < 0 || intensity > 100 || wind < 0 || wind > 150)
            return;
        gStatus = status;
        gTemperature = llList2Integer(f, 5);
        gIntensity = intensity;
        gWind = wind;
        gForecastTime = llList2String(f, 3);
        LAST = llGetUnixTime();
        render();
    }
    timer() { render(); }
    on_rez(integer param) { llResetScript(); }
}
