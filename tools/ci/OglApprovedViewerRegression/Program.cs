using System;
using OpenSim.Framework;

internal static class Program
{
    private static int m_Count;

    private static void Check(string viewer, string channel, bool expected)
    {
        bool actual = OglApprovedViewerPolicy.IsAllowed(viewer, channel);
        if (actual != expected)
            throw new Exception("Viewer admission regression: viewer='" + viewer +
                                "', channel='" + channel + "', expected=" + expected +
                                ", actual=" + actual);
        m_Count++;
    }

    public static void Main()
    {
        // Firestorm version/channel reporting used by local and HG login.
        Check("Firestorm-Release 7.1.0", "Firestorm-Releasex64", true);
        Check("7.1.0.12345", "Firestorm-Releasex64", true);
        Check("Firestorm-Release HG CI", "Firestorm-Releasex64", true);
        Check("Firestorm", "", true);
        Check("OpenGenesisLINK Viewer 1.0", "OpenGenesisLINK-Release", true);
        Check("1.0.0.1", "OpenGenesisLINK", true);
        // Exact login wire values from the OpenGenesisLINK Viewer v0.4.0.0.
        Check("v0.4.0.0", "OpenGenesisLINK Viewer", true);
        Check("V0.4.0.0", "OpenGenesisLINK Viewer", true);
        Check("v0.4.0.0", "OpenGenesisLINK-Release", true);
        Check("v0.4.0.0", "OGL Viewer", true);
        Check("v0.4.0.0", "Firestorm-Releasex64", true);
        // A version prefix must NOT authorize an unknown/disallowed viewer.
        Check("v0.4.0.0", "UnknownViewer", false);
        Check("v0.4.0.0", "Darkstorm", false);
        Check("v0.4.0.0", "Hydrastorm", false);
        Check("v0.4.0.0-beta", "OpenGenesisLINK Viewer", false);
        Check("vv0.4.0.0", "OpenGenesisLINK Viewer", false);
        Check("v", "OpenGenesisLINK Viewer", false);
        Check("v0.4.0.0-evil", "OpenGenesisLINK Viewer", false);

        Check("OGL Viewer 1.0", "OGL Viewer", true);
        Check("1.0.0", "OGL Viewer-Release", true);
        Check("OpenGenesisLINK-Release", null, true);

        // Radegast-ng reports its channel in different forms across builds.
        Check("Radegast-ng 2.3", "Radegast-ng", true);
        Check("Radegast-ng", "", true);
        Check("2.3.0", "Radegast-ng", true);
        Check("Radegast 2.3", "Radegast", true);
        Check("2.3.0", "Radegast", true);
        Check("Radegast-ng-malice", "Hydrastorm", false);

        // Explicit WolfViewer-Release exception: preserve the existing
        // anti-spoofing and exact family-boundary checks.
        Check("WolfViewer-Release 1.0", "WolfViewer-Release", true);
        Check("1.0.0.1", "WolfViewer-Release", true);
        Check("WolfViewer-Release", "", true);
        Check("wolfviewer-release 2.0", "WOLFVIEWER-RELEASE", true);
        Check("WolfViewer-ReleaseCandidate", "", false);
        Check("WolfViewer", "WolfViewer", false);
        Check("WolfViewer-Release", "UnknownViewer", false);
        Check("WolfViewer-Release Copybot", "WolfViewer-Release", false);
        Check("WolfViewer-Release", "Hydrastorm", false);

        // Frivolin Viewer is an approved Generation 1 viewer family.
        // Accept numeric versions, exact names and channel suffixes only.
        Check("Frivolin Viewer", "", true);
        Check("Frivolin Viewer 1.0", "Frivolin Viewer", true);
        Check("1.0.0", "Frivolin Viewer", true);
        Check("v0.2.0", "Frivolin Viewer-Release", true);
        Check("frivolin viewer 1.0", "FRIVOLIN VIEWER", true);
        Check("Frivolin Viewer-Release", null, true);
        Check("Frivolin ViewerFake", "", false);
        Check("Frivolin ViewerCopybot", "Frivolin Viewer", false);
        Check("Frivolin Viewer", "UnknownViewer", false);
        Check("Frivolin Viewer", "Darkstorm", false);
        Check("Darkstorm Frivolin Viewer", "Frivolin Viewer", false);
        Check("Frivolin Viewer Copybot", "Frivolin Viewer", false);
        Check("Frivolin Viewer", "Hydrastorm", false);
        Check("Frivolin", "Frivolin", false);

        // TrixieStorm Viewer is our official in-house OpenGenesisLINK viewer.
        // Accept release/beta channel suffixes and numeric viewer versions,
        // but do not widen admission to similarly named/unknown clients.
        Check("TrixieStorm Viewer", "", true);
        Check("TrixieStorm Viewer 0.4.0", "TrixieStorm Viewer-Release", true);
        Check("0.4.0", "TrixieStorm Viewer", true);
        Check("v0.4.0.0", "TrixieStorm Viewer-Release", true);
        Check("TRIXIESTORM VIEWER-Release", "trixiestorm viewer", true);
        Check("TrixieStorm Viewer_Dev", null, true);
        Check("TrixieStorm ViewerFake", "", false);
        Check("TrixieStorm", "TrixieStorm", false);
        Check("TrixieStorm ViewerCopybot", "TrixieStorm Viewer", false);
        Check("TrixieStorm Viewer", "UnknownViewer", false);
        Check("UnknownViewer", "TrixieStorm Viewer", false);
        Check("TrixieStorm Viewer", "Hydrastorm", false);
        Check("Darkstorm TrixieStorm Viewer", "TrixieStorm Viewer", false);
        Check("TrixieStorm Viewer Copybot", "TrixieStorm Viewer", false);
        Check("TrixieStorm ViewerFake-Release", "", false);

        // Denials: explicit known Copybot labels and every other viewer family.
        Check("Darkstorm 2.0", "Darkstorm", false);
        Check("Hydrastorm 1.0", "Hydrastorm", false);
        Check("DarkStorm-Release", "Firestorm-Releasex64", false);
        Check("7.1.0", "Hydrastorm", false);
        Check("Firestorm-Release Darkstorm", "Firestorm-Releasex64", false);
        Check("Firestorm-copybot", "Firestorm-Releasex64", false);
        Check("FirestormFake", "", false);
        Check("Hydrastorm", "Firestorm-Releasex64", false);
        Check("Cool VL Viewer 1.0", "Cool VL Viewer", false);
        Check("Singularity 1.0", "Singularity", false);
        Check("Alchemy 1.0", "Alchemy", false);
        Check("Unknown", "Unknown", false);
        Check("", "", false);
        Check(null, null, false);
        Check("Firestorm-Release", "OtherViewer", false);
        Check("NotFirestorm 7", "NotFirestorm", false);

        Console.WriteLine("OGL approved viewer admission: " + m_Count + " regression cases passed");
    }
}
