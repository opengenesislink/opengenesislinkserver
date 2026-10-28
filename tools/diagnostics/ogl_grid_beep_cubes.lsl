// OpenGenesisLINK Generation 1 – gridweite Würfel-Kommunikation
// Das gleiche Skript in jeden Würfel legen. Alle Würfel leuchten rot auf,
// unabhängig von Region/Entfernung/Estate – innerhalb eines OpenGenesisLINK-Grids.
//
// Voraussetzung auf allen Simulatoren:
// [NexVerseNodeAgent] Enabled = true; GridScriptMessaging = true
// Für mehrere Simulatornodes muss der NexBus-HMAC-Transport funktionieren.
// Der AUSLÖSENDE Würfel muss einem Estate Owner oder Manager gehören.
// llGridSay ist eine OpenGenesisLINK-Servererweiterung (nicht Standard-LSL).

integer CHANNEL = -9342801;
string SIGNAL = "Beep";
float FLASH_SECONDS = 0.5;

flash()
{
    llSetColor(<1.0, 0.0, 0.0>, ALL_SIDES);
    llSetTimerEvent(FLASH_SECONDS);
}

default
{
    state_entry()
    {
        llSetColor(<1.0, 1.0, 1.0>, ALL_SIDES);
        llSetTimerEvent(0.0);
        llListen(CHANNEL, "", NULL_KEY, "");
    }

    touch_start(integer total_number)
    {
        // Sofortiges Feedback am auslösenden Würfel.
        flash();
        // Alle anderen (auch auf fremden Estates im selben Grid) benachrichtigen.
        llGridSay(CHANNEL, SIGNAL);
    }

    listen(integer channel, string name, key id, string message)
    {
        if (channel == CHANNEL && message == SIGNAL)
            flash();
    }

    timer()
    {
        llSetColor(<1.0, 1.0, 1.0>, ALL_SIDES);
        llSetTimerEvent(0.0);
    }

    on_rez(integer start_param)
    {
        llResetScript();
    }
}
