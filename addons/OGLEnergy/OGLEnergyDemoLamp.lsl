// STADT NEXVERSE | OGLEnergy optional blackout demonstration
// Install ONLY in a dedicated demo lamp's light-emitting prim.
// Does not change NexSmartLight, existing objects or your streetlights.
// During NORMAL and WARN the demo lamp is powered; OUTAGE turns it off.
integer CHANNEL = -835431;
integer STALE_AFTER = 240;
integer POWERED = FALSE;
integer LAST_UPDATE = 0;
vector LIGHT_COLOR = <1.0, 0.91, 0.70>;

setPowered(integer on)
{
    if (POWERED == on) return;
    POWERED = on;
    float glow = 0.0;
    if (on) glow = 0.08;
    llSetLinkPrimitiveParamsFast(LINK_THIS, [
        PRIM_POINT_LIGHT, on, LIGHT_COLOR, 0.90, 12.0, 0.6,
        PRIM_FULLBRIGHT, ALL_SIDES, on,
        PRIM_GLOW, ALL_SIDES, glow
    ]);
}

default
{
    state_entry()
    {
        POWERED = TRUE;
        setPowered(FALSE); // No power until a valid server broadcast.
        llListen(CHANNEL, "OGLEnergy", NULL_KEY, "");
        llSetTimerEvent(30.0);
    }
    listen(integer channel, string name, key id, string wire)
    {
        if (channel != CHANNEL || name != "OGLEnergy" ||
            id != NULL_KEY || llStringLength(wire) > 200) return;
        list f = llParseStringKeepNulls(wire, ["|"], []);
        if (llGetListLength(f) != 12 ||
            llList2String(f, 0) != "OGLENERGY" ||
            llList2String(f, 1) != "1") return;

        string status = llList2String(f, 3);
        if (status != "NORMAL" && status != "WARN" &&
            status != "OUTAGE") return;
        integer supply = llList2Integer(f, 4);
        integer demand = llList2Integer(f, 5);
        if (supply < 0 || supply > 400000 ||
            demand < 1 || demand > 150000) return;
        // Check against the exact documented grid status thresholds.
        if (status == "NORMAL" && supply < demand) return;
        if (status == "WARN" &&
            (supply >= demand || supply * 4 < demand * 3)) return;
        if (status == "OUTAGE" && supply * 4 >= demand * 3) return;

        LAST_UPDATE = llGetUnixTime();
        setPowered(status != "OUTAGE");
    }
    timer()
    {
        if (LAST_UPDATE == 0 ||
            llGetUnixTime() - LAST_UPDATE > STALE_AFTER)
            setPowered(FALSE);
    }
    on_rez(integer start) { llResetScript(); }
}
