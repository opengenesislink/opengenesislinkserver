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
