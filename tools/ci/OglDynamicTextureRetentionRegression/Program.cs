using System;
using System.Collections.Generic;
using OpenSim.Region.CoreModules.Scripting.DynamicTexture;

internal static class Program
{
    private static int checks;

    private static void Check(bool success, string reason)
    {
        checks++;
        if (!success)
            throw new InvalidOperationException("Dynamic texture retention: " + reason);
    }

    private static void Main()
    {
        DateTime now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        List<string> expired = new List<string>();
        List<Exception> errors = new List<Exception>();

        DynamicTextureRetirementQueue queue = new DynamicTextureRetirementQueue(
            30, 2, () => now, errors.Add);

        queue.Retire(() => expired.Add("A"));
        Check(queue.PendingCount == 1 && expired.Count == 0, "first asset retained");
        now = now.AddSeconds(10);
        queue.Retire(() => expired.Add("B"));
        now = now.AddSeconds(10);
        queue.Retire(() => expired.Add("C")); // bounded eviction of oldest A
        Check(queue.PendingCount == 2, "queue capped at two");
        Check(expired.Count == 1 && expired[0] == "A", "oldest freed on overflow");
        now = now.AddSeconds(19);
        queue.ExpireDue();
        Check(expired.Count == 1, "old B not expired before deadline");
        now = now.AddSeconds(1);
        queue.ExpireDue();
        Check(expired.Count == 2 && expired[1] == "B", "B expired at deadline");
        now = now.AddSeconds(10);
        queue.ExpireDue();
        Check(expired.Count == 3 && expired[2] == "C", "C expired at deadline");
        Check(queue.PendingCount == 0, "queue drained");

        DynamicTextureRetirementQueue noGrace =
            new DynamicTextureRetirementQueue(0, 2, () => now, errors.Add);
        noGrace.Retire(() => expired.Add("D"));
        Check(expired.Count == 4 && expired[3] == "D", "legacy immediate expiry");

        DynamicTextureRetirementQueue draining =
            new DynamicTextureRetirementQueue(120, 2, () => now, errors.Add);
        draining.Retire(() => expired.Add("E"));
        draining.Flush();
        Check(expired.Count == 5 && expired[4] == "E", "shutdown flushes pending assets");

        DynamicTextureRetirementQueue resilient =
            new DynamicTextureRetirementQueue(0, 1, () => now, errors.Add);
        resilient.Retire(() => throw new InvalidOperationException("fake cache failure"));
        resilient.Retire(() => expired.Add("F"));
        Check(errors.Count == 1, "cache expiration exception reported");
        Check(expired.Count == 6 && expired[5] == "F", "subsequent callbacks remain usable");

        Console.WriteLine("OGL dynamic texture retirement: PASS (" + checks + " checks)");
    }
}
