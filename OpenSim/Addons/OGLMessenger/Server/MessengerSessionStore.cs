using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;

namespace OpenSim.Addons.OGLMessenger.Server;

public sealed record MessengerSession(
    Guid AvatarId, string AccessToken, string RefreshToken,
    long AccessExpiresAt, long ExpiresAt, string CsrfToken);

public enum SendReservationStatus { Started, Completed, Pending, Conflict }
public sealed record SendReservation(SendReservationStatus Status, int HttpStatus, string? Response);

/// <summary>
/// SQLite persistent, single-node session and idempotency store. The browser
/// keeps a random opaque handle; only SHA-256(handle) goes into the database.
/// OAuth access and refresh tokens are encrypted using persisted Data Protection
/// keys. Do not share this SQLite database across unrelated server nodes.
/// </summary>
public sealed class MessengerSessionStore
{
    private readonly string _connectionString;
    private readonly IDataProtector _protector;

    public MessengerSessionStore(MessengerOptions options, IDataProtectionProvider protection)
    {
        string path = Path.GetFullPath(options.DatabasePath);
        string databaseDirectory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(databaseDirectory);
        // The SQLite database and its WAL can contain encrypted tokens,
        // personal IDs and IM idempotency metadata. Keep its parent private.
        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(databaseDirectory, UnixFileMode.UserRead |
                UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            DefaultTimeout = 15
        }.ToString();
        _protector = protection.CreateProtector("OpenGenesisLINK.OGLMessenger.Session.v1");
        using var db = Open();
        using var sql = db.CreateCommand();
        sql.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS messenger_sessions (
                session_hash TEXT PRIMARY KEY,
                avatar_id TEXT NOT NULL,
                access_token TEXT NOT NULL,
                refresh_token TEXT NOT NULL,
                access_expires INTEGER NOT NULL,
                session_expires INTEGER NOT NULL,
                csrf_token TEXT NOT NULL,
                created INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_messenger_sessions_avatar
              ON messenger_sessions(avatar_id);
            CREATE TABLE IF NOT EXISTS messenger_sends (
                avatar_id TEXT NOT NULL,
                client_id TEXT NOT NULL,
                recipient_id TEXT NOT NULL,
                body_hash TEXT NOT NULL,
                state TEXT NOT NULL,
                response_status INTEGER,
                response_json TEXT,
                created INTEGER NOT NULL,
                PRIMARY KEY (avatar_id, client_id)
            );
            """;
        sql.ExecuteNonQuery();
        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(_connectionString);
        db.Open();
        return db;
    }

    public static string RandomSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Digest(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public string Create(Guid avatarId, OAuthTokens tokens, int sessionDays)
    {
        if (avatarId == Guid.Empty || string.IsNullOrWhiteSpace(tokens.RefreshToken))
            throw new InvalidOperationException("offline_access refresh token is required.");

        string cookie = RandomSecret();
        using var db = Open();
        using var sql = db.CreateCommand();
        sql.CommandText = """
            INSERT INTO messenger_sessions
              (session_hash, avatar_id, access_token, refresh_token,
               access_expires, session_expires, csrf_token, created)
            VALUES (@hash, @id, @access, @refresh, @accessExp,
                    @sessionExp, @csrf, @created)
            """;
        sql.Parameters.AddWithValue("@hash", Digest(cookie));
        sql.Parameters.AddWithValue("@id", avatarId.ToString("D"));
        sql.Parameters.AddWithValue("@access", _protector.Protect(tokens.AccessToken));
        sql.Parameters.AddWithValue("@refresh", _protector.Protect(tokens.RefreshToken));
        sql.Parameters.AddWithValue("@accessExp", Now + tokens.ExpiresIn);
        sql.Parameters.AddWithValue("@sessionExp", Now + 86400L * sessionDays);
        sql.Parameters.AddWithValue("@csrf", RandomSecret());
        sql.Parameters.AddWithValue("@created", Now);
        sql.ExecuteNonQuery();
        return cookie;
    }

    public MessengerSession? Find(string? cookie)
    {
        if (string.IsNullOrEmpty(cookie) || cookie.Length > 128)
            return null;
        using var db = Open();
        using var sql = db.CreateCommand();
        sql.CommandText = """
            SELECT avatar_id, access_token, refresh_token, access_expires,
                   session_expires, csrf_token FROM messenger_sessions
            WHERE session_hash=@hash AND session_expires > @now
            """;
        sql.Parameters.AddWithValue("@hash", Digest(cookie));
        sql.Parameters.AddWithValue("@now", Now);
        using var reader = sql.ExecuteReader();
        if (!reader.Read())
            return null;
        try
        {
            return new MessengerSession(
                Guid.Parse(reader.GetString(0)),
                _protector.Unprotect(reader.GetString(1)),
                _protector.Unprotect(reader.GetString(2)),
                reader.GetInt64(3), reader.GetInt64(4),
                reader.GetString(5));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    public void UpdateTokens(string cookie, OAuthTokens tokens)
    {
        using var db = Open();
        using var sql = db.CreateCommand();
        sql.CommandText = """
            UPDATE messenger_sessions SET access_token=@access,
              refresh_token=@refresh, access_expires=@exp
            WHERE session_hash=@hash AND session_expires>@now
            """;
        sql.Parameters.AddWithValue("@access", _protector.Protect(tokens.AccessToken));
        sql.Parameters.AddWithValue("@refresh", _protector.Protect(tokens.RefreshToken ??
            throw new InvalidOperationException("Refresh rotation did not return a new token.")));
        sql.Parameters.AddWithValue("@exp", Now + tokens.ExpiresIn);
        sql.Parameters.AddWithValue("@hash", Digest(cookie));
        sql.Parameters.AddWithValue("@now", Now);
        if (sql.ExecuteNonQuery() != 1)
            throw new UnauthorizedAccessException("Session was revoked.");
    }

    public void Revoke(string? cookie)
    {
        if (string.IsNullOrEmpty(cookie) || cookie.Length > 128)
            return;
        using var db = Open();
        using var sql = db.CreateCommand();
        sql.CommandText = "DELETE FROM messenger_sessions WHERE session_hash=@hash";
        sql.Parameters.AddWithValue("@hash", Digest(cookie));
        sql.ExecuteNonQuery();
    }

    public SendReservation ReserveSend(Guid avatar, Guid clientId,
        Guid recipient, string body)
    {
        if (avatar == Guid.Empty || clientId == Guid.Empty || recipient == Guid.Empty)
            throw new ArgumentException("Missing avatar, client or recipient UUID.");

        string hash = Digest(body);
        using var db = Open();
        using var tx = db.BeginTransaction();
        using var insert = db.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT OR IGNORE INTO messenger_sends
             (avatar_id, client_id, recipient_id, body_hash, state, created)
            VALUES (@avatar,@client,@recipient,@body,'pending',@now)
            """;
        insert.Parameters.AddWithValue("@avatar", avatar.ToString("D"));
        insert.Parameters.AddWithValue("@client", clientId.ToString("D"));
        insert.Parameters.AddWithValue("@recipient", recipient.ToString("D"));
        insert.Parameters.AddWithValue("@body", hash);
        insert.Parameters.AddWithValue("@now", Now);
        bool inserted = insert.ExecuteNonQuery() == 1;
        using var existing = db.CreateCommand();
        existing.Transaction = tx;
        existing.CommandText = """
            SELECT recipient_id,body_hash,state,response_status,response_json
            FROM messenger_sends WHERE avatar_id=@avatar AND client_id=@client
            """;
        existing.Parameters.AddWithValue("@avatar", avatar.ToString("D"));
        existing.Parameters.AddWithValue("@client", clientId.ToString("D"));
        using var reader = existing.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("Message reservation unavailable.");
        SendReservation result;
        if (inserted)
            result = new SendReservation(SendReservationStatus.Started, 0, null);
        else if (reader.GetString(0) != recipient.ToString("D") ||
                 !CryptographicOperations.FixedTimeEquals(
                     Convert.FromHexString(reader.GetString(1)),
                     Convert.FromHexString(hash)))
            result = new SendReservation(SendReservationStatus.Conflict, 409, null);
        else if (reader.GetString(2) == "complete" && !reader.IsDBNull(3))
            result = new SendReservation(SendReservationStatus.Completed,
                reader.GetInt32(3), reader.IsDBNull(4) ? "" : reader.GetString(4));
        else
            result = new SendReservation(SendReservationStatus.Pending, 409, null);
        reader.Close();
        tx.Commit();
        return result;
    }

    public void CompleteSend(Guid avatar, Guid clientId, int status, string response)
    {
        using var db = Open();
        using var sql = db.CreateCommand();
        sql.CommandText = """
            UPDATE messenger_sends SET state='complete',response_status=@status,
              response_json=@response
            WHERE avatar_id=@avatar AND client_id=@client AND state='pending'
            """;
        sql.Parameters.AddWithValue("@avatar", avatar.ToString("D"));
        sql.Parameters.AddWithValue("@client", clientId.ToString("D"));
        sql.Parameters.AddWithValue("@status", status);
        sql.Parameters.AddWithValue("@response", response.Length > 8192 ?
            response[..8192] : response);
        sql.ExecuteNonQuery();
    }
}
