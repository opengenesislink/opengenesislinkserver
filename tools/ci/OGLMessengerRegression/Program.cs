using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using OpenSim.Addons.OGLMessenger.Server;

static void Check(bool ok, string message)
{
    if (!ok)
        throw new InvalidOperationException(message);
}

string root = Path.Combine(Path.GetTempPath(),
    "ogl-messenger-regression-" + Guid.NewGuid().ToString("N"));
string keys = Path.Combine(root, "keys");
Directory.CreateDirectory(keys);
try
{
    var config = new MessengerOptions
    {
        WorldApiBaseUrl = "https://world.example.invalid",
        PublicBaseUrl = "https://messenger.example.invalid",
        ClientId = "registered-pkce-public-client",
        DatabasePath = Path.Combine(root, "messenger.sqlite"),
        DataProtectionKeysPath = keys
    };
    Check(config.IsReady, "valid production HTTPS configuration refused");
    config.WorldApiBaseUrl = "http://world.example.invalid";
    Check(!config.IsReady, "insecure upstream was accepted");
    config.WorldApiBaseUrl = "https://world.example.invalid";

    var protection = DataProtectionProvider.Create(new DirectoryInfo(keys));
    var store = new MessengerSessionStore(config, protection);
    Guid avatar = Guid.NewGuid();
    Guid friend = Guid.NewGuid();
    Guid messageId = Guid.NewGuid();
    string cookie = store.Create(avatar,
        new OAuthTokens("access-secret-1", "refresh-secret-1", 600), 30);

    Check(cookie.Length >= 32, "cookie entropy too small");
    Check(store.IsMobileOnline(avatar), "new active PWA session must be mobile-online");
    Check(!store.IsMobileOnline(friend), "presence leaked to unrelated avatar");
    store.TouchPresence(cookie);
    Check(store.IsMobileOnline(avatar), "mobile heartbeat failed");
    Check(store.Find("invalid") == null, "invalid cookie accepted");
    MessengerSession? session = store.Find(cookie);
    Check(session?.AvatarId == avatar && session.AccessToken == "access-secret-1" &&
          session.RefreshToken == "refresh-secret-1", "encrypted token session failed");
    // Inspect SQLite ciphertext through a normal read connection: on Windows
    // SQLite may hold an open WAL/file handle, making raw File.ReadAllBytes
    // unsafe. This also verifies encryption without base64-alignment guesses.
    using (var conn = new SqliteConnection(
        "Data Source=" + config.DatabasePath + ";Pooling=False"))
    {
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT access_token,refresh_token FROM messenger_sessions LIMIT 1";
        using var row = cmd.ExecuteReader();
        Check(row.Read(), "session row missing from SQLite");
        Check(row.GetString(0) != "access-secret-1" &&
              !row.GetString(0).Contains("access-secret-1", StringComparison.Ordinal),
            "plaintext access token stored in SQLite");
        Check(row.GetString(1) != "refresh-secret-1" &&
              !row.GetString(1).Contains("refresh-secret-1", StringComparison.Ordinal),
            "plaintext refresh token stored in SQLite");
    }

    store.UpdateTokens(cookie, new OAuthTokens("access-2", "refresh-2", 3600));
    session = store.Find(cookie);
    Check(session?.AccessToken == "access-2" &&
          session.RefreshToken == "refresh-2", "refresh token rotation failed");

    SendReservation first = store.ReserveSend(avatar, messageId, friend, "hello");
    Check(first.Status == SendReservationStatus.Started, "first IM rejected");
    Check(store.ReserveSend(avatar, messageId, friend, "hello").Status ==
          SendReservationStatus.Pending, "duplicate send re-entered upstream");
    Check(store.ReserveSend(avatar, messageId, friend, "different").Status ==
          SendReservationStatus.Conflict, "reused ID with different content was accepted");
    Check(store.ReserveSend(avatar, messageId, Guid.NewGuid(), "hello").Status ==
          SendReservationStatus.Conflict, "reused ID with different recipient was accepted");
    store.CompleteSend(avatar, messageId, 202, "{\"status\":\"accepted\"}");
    SendReservation duplicate = store.ReserveSend(avatar, messageId, friend, "hello");
    Check(duplicate.Status == SendReservationStatus.Completed &&
          duplicate.HttpStatus == 202 && duplicate.Response!.Contains("accepted"),
          "idempotent accepted IM was not replayed safely");
    Check(store.ReserveSend(Guid.NewGuid(), messageId, friend, "hello").Status ==
          SendReservationStatus.Started, "idempotency keys leaked between senders");

    var reopened = new MessengerSessionStore(config,
        DataProtectionProvider.Create(new DirectoryInfo(keys)));
    Check(reopened.Find(cookie)?.RefreshToken == "refresh-2",
        "server restart lost persistent encrypted session");
    reopened.Revoke(cookie);
    Check(store.Find(cookie) == null, "logout did not revoke the device session");
    Check(!store.IsMobileOnline(avatar), "mobile presence remained online after logout");

    Console.WriteLine("PASS: Messenger encrypted cookies, session persistence, refresh rotation and exactly-once IM keys");
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(root, true);
}
