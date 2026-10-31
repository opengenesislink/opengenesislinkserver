// SPDX-License-Identifier: MPL-2.0
// R619: owner-consented direct NPC IM archive; not a general chat logger.
using System;
using System.Collections.Generic;
using System.IO;
using Mono.Data.Sqlite;

namespace NexVerse.Server.Api
{
    internal sealed class NexNpcChatStore
    {
        private readonly object m_Guard = new object();
        private readonly string m_Connection;
        private readonly int m_RetentionDays;
        private const int MaxPerOwner = 10000;

        internal NexNpcChatStore(string path, int retentionDays)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A dedicated NPC chat database is required.", nameof(path));
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            m_Connection = "Data Source=" + full + ";Version=3;Pooling=False;";
            m_RetentionDays = Math.Clamp(retentionDays, 1, 90);
            lock (m_Guard)
            {
                using var db = Open();
                Exec(db, @"CREATE TABLE IF NOT EXISTS npc_chat_consent (
                    owner TEXT PRIMARY KEY NOT NULL, enabled INTEGER NOT NULL,
                    updated_at INTEGER NOT NULL)");
                Exec(db, @"CREATE TABLE IF NOT EXISTS npc_chat_messages (
                    seq INTEGER PRIMARY KEY AUTOINCREMENT,
                    event_id TEXT NOT NULL,
                    owner TEXT NOT NULL,
                    npc_id TEXT NOT NULL,
                    sender TEXT NOT NULL CHECK(sender IN ('npc','owner')),
                    text TEXT NOT NULL,
                    accepted_at INTEGER NOT NULL,
                    UNIQUE(owner,event_id))");
                Exec(db, "CREATE INDEX IF NOT EXISTS ix_npc_chat_owner_npc_seq ON npc_chat_messages(owner,npc_id,seq)");
                Exec(db, "CREATE INDEX IF NOT EXISTS ix_npc_chat_ttl ON npc_chat_messages(accepted_at)");
            }
        }

        private SqliteConnection Open()
        {
            var db = new SqliteConnection(m_Connection);
            db.Open();
            return db;
        }
        private static void Exec(SqliteConnection db, string sql)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        private static void Param(SqliteCommand cmd, string key, object value)
        {
            cmd.Parameters.AddWithValue(key, value);
        }
        private void Purge(SqliteConnection db, SqliteTransaction tx = null)
        {
            using var cmd = db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM npc_chat_messages WHERE accepted_at < @cutoff";
            Param(cmd, "@cutoff", DateTimeOffset.UtcNow.AddDays(-m_RetentionDays).ToUnixTimeSeconds());
            cmd.ExecuteNonQuery();
        }
        private static bool Enabled(SqliteConnection db, Guid owner)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT enabled FROM npc_chat_consent WHERE owner=@owner";
            Param(cmd, "@owner", owner.ToString("D"));
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) == 1;
        }
        internal bool IsEnabled(Guid owner)
        {
            lock (m_Guard)
            {
                using var db = Open();
                return Enabled(db, owner);
            }
        }
        internal void SetEnabled(Guid owner, bool enabled)
        {
            if (owner == Guid.Empty) throw new ArgumentException("Owner required.");
            lock (m_Guard)
            {
                using var db = Open();
                using var tx = db.BeginTransaction();
                using (var cmd = db.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT INTO npc_chat_consent(owner,enabled,updated_at)
                        VALUES(@owner,@enabled,@time)
                        ON CONFLICT(owner) DO UPDATE SET enabled=excluded.enabled,updated_at=excluded.updated_at";
                    Param(cmd,"@owner",owner.ToString("D"));
                    Param(cmd,"@enabled",enabled ? 1 : 0);
                    Param(cmd,"@time",DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    cmd.ExecuteNonQuery();
                }
                if (!enabled) DeleteOwner(db, tx, owner);
                tx.Commit();
            }
        }
        private static void DeleteOwner(SqliteConnection db, SqliteTransaction tx, Guid owner)
        {
            using var cmd = db.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM npc_chat_messages WHERE owner=@owner";
            Param(cmd,"@owner",owner.ToString("D"));
            cmd.ExecuteNonQuery();
        }
        internal void DeleteHistory(Guid owner)
        {
            lock (m_Guard)
            {
                using var db = Open();
                using var tx = db.BeginTransaction();
                DeleteOwner(db,tx,owner);
                tx.Commit();
            }
        }

        // Only the HMAC-verified ingress may call this, after resolving the
        // owner and NPC against a fresh complete simulator snapshot.
        internal bool Append(Guid eventID, Guid owner, Guid npc, string sender, string text)
        {
            if (eventID == Guid.Empty || owner == Guid.Empty || npc == Guid.Empty ||
                (sender != "owner" && sender != "npc") ||
                string.IsNullOrWhiteSpace(text) ||
                System.Text.Encoding.UTF8.GetByteCount(text) > 4096)
                return false;
            lock (m_Guard)
            {
                using var db = Open();
                using var tx = db.BeginTransaction();
                Purge(db,tx);
                if (!Enabled(db, owner)) return false;
                using (var cmd = db.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"INSERT OR IGNORE INTO npc_chat_messages
                        (event_id,owner,npc_id,sender,text,accepted_at)
                        VALUES(@event,@owner,@npc,@sender,@text,@time)";
                    Param(cmd,"@event",eventID.ToString("D"));
                    Param(cmd,"@owner",owner.ToString("D"));
                    Param(cmd,"@npc",npc.ToString("D"));
                    Param(cmd,"@sender",sender);
                    Param(cmd,"@text",text);
                    Param(cmd,"@time",DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = db.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"DELETE FROM npc_chat_messages
                        WHERE owner=@owner AND seq NOT IN
                        (SELECT seq FROM npc_chat_messages WHERE owner=@owner
                        ORDER BY seq DESC LIMIT @limit)";
                    Param(cmd,"@owner",owner.ToString("D"));
                    Param(cmd,"@limit",MaxPerOwner);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
                return true;
            }
        }

        internal (List<object> Messages, string NextCursor) Read(Guid owner, Guid npc, long after, int limit)
        {
            if (after < 0 || limit < 1 || limit > 500)
                throw new ArgumentException("Invalid NPC chat pagination.");
            lock (m_Guard)
            {
                using var db = Open();
                Purge(db);
                var items = new List<object>();
                var ids = new List<long>();
                using var cmd = db.CreateCommand();
                cmd.CommandText = @"SELECT seq,event_id,sender,text,accepted_at
                    FROM npc_chat_messages WHERE owner=@owner AND npc_id=@npc AND seq>@after
                    ORDER BY seq ASC LIMIT @limit";
                Param(cmd,"@owner",owner.ToString("D"));
                Param(cmd,"@npc",npc.ToString("D"));
                Param(cmd,"@after",after);
                Param(cmd,"@limit",limit+1);
                using var reader = cmd.ExecuteReader();
                bool more = false;
                while (reader.Read())
                {
                    if (items.Count == limit) { more = true; break; }
                    long seq=reader.GetInt64(0);
                    ids.Add(seq);
                    items.Add(new
                    {
                        message_id=reader.GetString(1),
                        npc_id=npc.ToString("D"),
                        sender=reader.GetString(2),
                        text=reader.GetString(3),
                        sent_at=DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4))
                            .UtcDateTime.ToString("O")
                    });
                }
                return (items, more && ids.Count>0 ? ids[ids.Count-1].ToString() : "");
            }
        }
        internal int RetentionDays => m_RetentionDays;
    }
}
