using System;
using NexVerse.Server.Api;

internal static class Program
{
    private static int s_Count;
    private static void Require(bool condition, string message)
    {
        s_Count++;
        if (!condition)
            throw new Exception("Relationship HG/last-online: " + message);
    }

    public static void Main()
    {
        const string local = "11111111-1111-4111-8111-111111111111";
        const string foreign = "22222222-2222-4222-8222-222222222222";
        Require(NexRelationshipPresenceModel.TryParseIdentity(
            local, out var localFriend) &&
            !localFriend.IsHypergrid &&
            localFriend.HomeGrid == null &&
            localFriend.Id.ToString() == local,
            "plain UUID local friend");
        Require(NexRelationshipPresenceModel.TryParseIdentity(
            foreign + ";http://hg.example.net:8002;Antonia Porta",
            out var foreignFriend) &&
            foreignFriend.IsHypergrid &&
            foreignFriend.HomeGrid == "http://hg.example.net:8002" &&
            foreignFriend.ForeignName == "Antonia Porta" &&
            foreignFriend.Id.ToString() == foreign,
            "full HG UUI and display name");
        Require(NexRelationshipPresenceModel.TryParseIdentity(
            foreign + ";https://example.net/opensim/;Trixi Back;TOP-SECRET",
            out var secretFriend) &&
            secretFriend.IsHypergrid &&
            secretFriend.HomeGrid == "https://example.net/opensim" &&
            secretFriend.ForeignName == "Trixi Back" &&
            !secretFriend.HomeGrid.Contains("SECRET", StringComparison.Ordinal) &&
            !secretFriend.ForeignName.Contains("SECRET", StringComparison.Ordinal),
            "HG federation secret stripped from every projected field");
        foreach (string invalid in new[]
        {
            "", "not-a-uuid",
            "00000000-0000-0000-0000-000000000000",
            foreign + ";http://hg.example.net; ",
            foreign + ";javascript:alert(1);Tester Guest",
            foreign + ";https://user:pass@example.net;Tester Guest",
            foreign + ";http://hg.example.net;Tester Guest;secret;extra",
            "bad-id;http://grid.example;Tester Guest"
        })
        {
            Require(!NexRelationshipPresenceModel.TryParseIdentity(
                invalid, out _), "reject malformed/unsafe UUI " + invalid);
        }

        DateTimeOffset now = new DateTimeOffset(
            2026, 10, 28, 21, 0, 0, TimeSpan.Zero);
        for (int minutes = 0; minutes < 61 * 24; minutes++)
        {
            DateTime logout = now.AddMinutes(-minutes).UtcDateTime;
            var a = NexRelationshipPresenceModel.LastLogout(
                logout, now, true, false);
            Require(a.LastOnlineMinutes == minutes,
                "accurate minute clock");
            Require(a.LastOnlineHours == minutes / 60,
                "accurate hour floor");
            Require(a.LastOnlineAtUtc != null && a.LastOnlineLabel != null,
                "valid logout yields timestamp and label");
        }
        Require(NexRelationshipPresenceModel.LastLogout(
            now.AddMinutes(-1).UtcDateTime, now, true, false)
            .LastOnlineLabel == "vor 1 Minute", "singular minute");
        Require(NexRelationshipPresenceModel.LastLogout(
            now.AddMinutes(-120).UtcDateTime, now, true, false)
            .LastOnlineLabel == "vor 2 Stunden", "plural hour");
        Require(NexRelationshipPresenceModel.LastLogout(
            now.AddDays(-1).UtcDateTime, now, true, false)
            .LastOnlineLabel == "vor 1 Tag", "singular day");
        foreach (DateTime date in new[]
        {
            default(DateTime), new DateTime(1970, 1, 1, 0, 0, 0,
                DateTimeKind.Utc),
            now.AddHours(1).UtcDateTime
        })
            Require(NexRelationshipPresenceModel.LastLogout(
                date, now, true, false).LastOnlineMinutes == null,
                "unknown/epoch/future date must remain null");
        Require(NexRelationshipPresenceModel.LastLogout(
            now.AddHours(-3).UtcDateTime, now, false, false)
            .LastOnlineMinutes == null, "friend without permission");
        Require(NexRelationshipPresenceModel.LastLogout(
            now.AddHours(-3).UtcDateTime, now, true, true)
            .LastOnlineMinutes == null, "online friend has no offline age");

        Console.WriteLine("Nex Relationship HG and last-online: " +
            s_Count + " checks PASS");
    }
}
