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
        // Linux mountinfo source must match EXACTLY the UGREEN NFS host.
        string nasMount = "35 24 0:44 / /mnt/ogl-iar rw,relatime - nfs4 10.78.0.4:/volume1/OGLIAR rw,vers=4.1\\n";
        Check(OglIarNasStorage.IsExpectedNfsMount(
            "/mnt/ogl-iar", "10.78.0.4", nasMount),
            "correct UGREEN NFSv4 mount accepted");
        Check(!OglIarNasStorage.IsExpectedNfsMount(
            "/mnt/ogl-iar", "10.78.0.5", nasMount),
            "wrong NAS host rejected");
        Check(!OglIarNasStorage.IsExpectedNfsMount(
            "/mnt/ogl-iar-extra", "10.78.0.4", nasMount),
            "incorrect mount point rejected");
        Check(!OglIarNasStorage.IsExpectedNfsMount(
            "/mnt/ogl-iar", "10.78.0.4",
            "35 24 0:44 / /mnt/ogl-iar rw - ext4 /dev/sda rw"),
            "local ext4 fallback is not acceptable");
        Check(!OglIarNasStorage.IsExpectedNfsMount(
            "/mnt/ogl-iar", "10.78.0.4",
            "35 24 0:44 / /mnt/ogl-iar rw - nfs4 10.78.0.40:/share rw"),
            "host-prefix spoofing rejected");
        Check(!OglIarNasStorage.IsExpectedNfsMount(
            "/mnt/ogl-iar", "10.78.0.4", ""),
            "missing/unmounted NAS rejected");

        DateTimeOffset time = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        Check(OglIarNasStorage.MaximumRetentionDays == 30, "30 day hard cap");
        Check(!OglIarNasStorage.IsExpired(time.AddDays(-29), time, 30),
            "day 29 retained");
        Check(OglIarNasStorage.IsExpired(time.AddDays(-30), time, 30),
            "day 30 expires");
        Check(OglIarNasStorage.IsExpired(time.AddDays(-31), time, 30),
            "older backups expire");
        try
        {
            OglIarNasStorage.IsExpired(time, time, 31);
            throw new Exception("31 day retention must be rejected");
        }
        catch (ArgumentOutOfRangeException) { count++; }
        Check(OglIarNasStorage.IsManagedIar("/mnt/ogl-iar", 
            path.Replace("/srv/ogl-iar", "/mnt/ogl-iar"), out Guid parsed) &&
            parsed == job, "only server-generated GUID archive paths");
        Check(!OglIarNasStorage.IsManagedIar("/mnt/ogl-iar",
            "/mnt/ogl-iar/other-folder/arbitrary.iar", out _),
            "unmanaged files cannot be purged");
        Console.WriteLine("OGL IAR self-service: " + count + " policy checks PASS");
    }
}
