using System;
using NexVerse.Core.Archives;

internal static class Program
{
    private static int count;
    private static void Check(bool ok, string name)
    {
        count++;
        if (!ok) throw new Exception("IAR self-service test failed: " + name);
    }
    public static void Main()
    {
        Guid region = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid job = Guid.Parse("22222222-2222-2222-2222-222222222222");
        string path = OglIarTransferPolicy.Resolve("/srv/ogl-iar", region, job);
        Check(path.EndsWith("11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222.iar"),
            "server-chosen opaque storage path");
        try { OglIarTransferPolicy.Resolve("/", region, job);
            throw new Exception("Root accepted"); }
        catch (ArgumentException) { count++; }
        long max = OglIarTransferPolicy.DefaultMaximumArchiveBytes;
        Check(max > 100L * 1024 * 1024, "uploads support >100 MiB");
        Check(OglIarTransferPolicy.AcceptChunk(0, 0, 4194304, 200000000, max),
            "first 4MiB chunk");
        Check(OglIarTransferPolicy.AcceptChunk(4194304, 4194304, 4194304,
            200000000, max), "second sequential chunk");
        Check(!OglIarTransferPolicy.AcceptChunk(4194304, 0, 4194304,
            200000000, max), "concurrent stale offset rejected");
        Check(!OglIarTransferPolicy.AcceptChunk(0, 0, 4194305,
            200000000, max), "oversized chunks rejected");
        Check(!OglIarTransferPolicy.AcceptChunk(0, 0, 0,
            200000000, max), "empty chunk rejected");
        Check(!OglIarTransferPolicy.AcceptChunk(0, 0, 1,
            max + 1, max), "archive quota enforced");
        Check(!OglIarTransferPolicy.AcceptChunk(199999999, 199999999, 2,
            200000000, max), "cannot exceed claimed archive size");

        DateTimeOffset now = new(2026, 10, 29, 18, 0, 0, TimeSpan.Zero);
        Check(OglIarTransferPolicy.EtaSeconds(100, 200,
            now.AddSeconds(-60), now) == 60, "actual upload ETA");
        Check(OglIarTransferPolicy.EtaSeconds(0, 200,
            now.AddMinutes(-20), now) == null, "no progress no fake ETA");
        Check(OglIarTransferPolicy.EtaSeconds(200, 200,
            now.AddMinutes(-20), now) == null, "finished upload no ETA");
        Check(OglIarTransferPolicy.EtaFromJob(50,
            now.AddMinutes(-2), now) == 120, "job ETA from observed progress");
        Check(OglIarTransferPolicy.EtaFromJob(0, null, now) == null,
            "no job progress no ETA");
        Console.WriteLine("OGL IAR self-service: " + count + " policy checks PASS");
    }
}
