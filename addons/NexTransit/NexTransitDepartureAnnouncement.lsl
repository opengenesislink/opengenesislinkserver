// NexTransit - OPT-IN local text announcement for scheduled departures.
// llSay(0, ...) has limited local chat radius. No TTS or audio files.
// Default is disabled to avoid unsolicited repeated messages.
integer CHANNEL = -835422;
integer ANNOUNCE_IN_CHAT = FALSE;
integer ANNOUNCE_WHEN_MINUTES = 1;
integer COOLDOWN_SECONDS = 600;
string STOP_NAME = "Stadt-NexVerse";
string gLastAnnouncement = "";
integer gLastAnnouncementTime = 0;

default
{
    state_entry() { llListen(CHANNEL, "NexTransit", NULL_KEY, ""); }
    listen(integer channel, string name, key id, string message)
    {
        if (!ANNOUNCE_IN_CHAT || channel != CHANNEL ||
            name != "NexTransit" || id != NULL_KEY) return;
        list f = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(f) != 4 ||
            llList2String(f, 0) != "NEXTRANSIT" ||
            llList2String(f, 1) != "1") return;
        list rows = llParseStringKeepNulls(llList2String(f, 3), [";"], []);
        integer i;
        for (i = 0; i < llGetListLength(rows); i++)
        {
            list r = llParseStringKeepNulls(llList2String(rows, i), [","], []);
            if (llGetListLength(r) != 4) return;
            integer minutes = llList2Integer(r, 3);
            if (minutes == ANNOUNCE_WHEN_MINUTES)
            {
                string signature = llList2String(f, 2) + "|" +
                    llList2String(r, 0) + "|" + llList2String(r, 2);
                if (signature != gLastAnnouncement &&
                    llGetUnixTime() - gLastAnnouncementTime >= COOLDOWN_SECONDS)
                {
                    gLastAnnouncement = signature;
                    gLastAnnouncementTime = llGetUnixTime();
                    llSay(0, "Naechste Planabfahrt " + STOP_NAME + ": Linie " +
                        llList2String(r, 0) + " Richtung " + llList2String(r, 1) +
                        ", Abfahrt " + llList2String(r, 2) + " Uhr.");
                }
                return;
            }
        }
    }
    on_rez(integer param) { llResetScript(); }
}
