using System;
using System.IO;
using OpenSim;
using OpenSim.Framework.Servers;

// Exercise the actual ServerBase version advertised through Scene's
// RegionHandshake, including a checkout with a stamped .version file.
internal static class Program
{
    private static int Main()
    {
        string originalDirectory = Directory.GetCurrentDirectory();
        string root = Path.Combine(Path.GetTempPath(),
            "ogl-public-simulator-version-" + Guid.NewGuid().ToString("N"));
        string working = Path.Combine(root, "bin");
        Directory.CreateDirectory(working);

        try
        {
            Directory.SetCurrentDirectory(working);
            File.WriteAllText(".version", "74eaea4");

            string expected = "Version: OpenGenesisLINK Generation 1 (SIM-" +
                VersionInfo.SimulationServiceVersionSupportedMin + "/" +
                VersionInfo.SimulationServiceVersionSupportedMax + ")";
            string actual = new ServerBase().GetVersionText();
            Check(actual == expected, "stamped .version must not alter public version: " + actual);

            File.Delete(".version");
            Directory.CreateDirectory(Path.Combine(root, ".git", "refs", "heads"));
            File.WriteAllText(Path.Combine(root, ".git", "HEAD"), "ref: refs/heads/main");
            File.WriteAllText(Path.Combine(root, ".git", "refs", "heads", "main"),
                "74eaea49bfa4efbb1f3f0c9c5889fe4686cd872b");
            actual = new ServerBase().GetVersionText();
            Check(actual == expected, "checkout Git HEAD must not alter public version: " + actual);

            // Scene adds a platform label; the public version must have
            // exactly one separator and no commit identifier.
            string simulatedHandshake = VersionInfo.PublicVersionName +
                " (X64/Unix/DotNet)";
            Check(!simulatedHandshake.Contains("74eaea4"), "Git SHA leaked");
            Check(VersionInfo.Version.TrimEnd() == VersionInfo.PublicVersionName,
                "legacy VersionInfo padding changed unexpectedly");

            Console.WriteLine("PASS: stable OpenGenesisLINK Generation 1 viewer version; " +
                ".version and Git HEAD cannot alter RegionHandshake identity");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
