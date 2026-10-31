// SPDX-License-Identifier: MPL-2.0
// R620: NPC direct-IM opt-in archive in the EXISTING OpenGenesisLINK MariaDB.
// Dedicated tables only; no local SQLite files and no citizen IM table reuse.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using MySql.Data.MySqlClient;

namespace NexVerse.Server.Api
{
    internal sealed class NexNpcChatStore
    {
        private readonly Func<DbConnection> m_Connect;
        private readonly int m_RetentionDays;
        private const int MaxPerOwner = 10000;

        internal NexNpcChatStore(string provider, string connectionString, int retentionDays)
        {
            if (string.IsNullOrWhiteSpace(provider) ||
                (provider.IndexOf("MySQL", StringComparison.OrdinalIgnoreCase) < 0 &&
                 provider.IndexOf("MariaDB", StringComparison.OrdinalIgnoreCase) < 0))
                throw new NotSupportedException("NPC IM archive requires existing MariaDB/MySQL grid storage.");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Grid [DatabaseService] MariaDB connection is required.");
            m_Connect = () => new MySqlConnection(connectionString);
            m_RetentionDays = Math.Clamp(retentionDays, 1, 90);

            using DbConnection db = Open();
            Exec(db, null, @"CREATE TABLE IF NOT EXISTS npc_chat_consent (
                owner CHAR(36) NOT NULL PRIMARY KEY,
                enabled TINYINT(1) NOT NULL DEFAULT 0,
                updated_at BIGINT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            Exec(db, null, @"CREATE TABLE IF NOT EXISTS npc_chat_messages (
                seq BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
                event_id CHAR(36) NOT NULL,
                owner CHAR(36) NOT NULL,
                npc_id CHAR(36) NOT NULL,
                sender VARCHAR(8) NOT NULL,
                body TEXT NOT NULL,
                accepted_at BIGINT NOT NULL,
                UNIQUE KEY uq_npc_chat_owner_event (owner,event_id),
                KEY ix_npc_chat_owner_npc_seq (owner,npc_id,seq),
                KEY ix_npc_chat_ttl (accepted_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
        }

        private DbConnection Open()
        {
            DbConnection db = m_Connect();
            db.Open();
            return db;
        }
        private static string Id(Guid id) => id.ToString("D");
        private static DbCommand Command(DbConnection db, DbTransaction tx, string sql,
            params (string Name, object Value)[] parameters)
        {
            DbCommand command = db.CreateCommand();
            command.Transaction = tx;
            command.CommandText = sql;
            foreach ((string name, object value) in parameters)
            {
                DbParameter p = command.CreateParameter();
                p.ParameterName = name;
                p.Value = value ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            return command;
        }
        private static int Exec(DbConnection db, DbTransaction tx, string sql,
            params (string Name, object Value)[] parameters)
        {
            using DbCommand command = Command(db, tx, sql, parameters);
            return command.ExecuteNonQuery();
        }
        private void Purge(DbConnection db, DbTransaction tx = null)
        {
            Exec(db, tx, "DELETE FROM npc_chat_messages WHERE accepted_at < @cutoff",
                ("@cutoff", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - m_RetentionDays * 86400L));
        }
        private static bool Enabled(DbConnection db, DbTransaction tx, Guid owner)
        {
            using DbCommand cmd = Command(db, tx,
                "SELECT enabled FROM npc_chat_consent WHERE owner=@owner",
                ("@owner", Id(owner)));
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) == 1;
        }
        internal bool IsEnabled(Guid owner)
        {
            using DbConnection db = Open();
            return Enabled(db, null, owner);
        }
        internal void SetEnabled(Guid owner, bool enabled)
        {
            if (owner == Guid.Empty) throw new ArgumentException("Owner required.");
            using DbConnection db = Open();
            using DbTransaction tx = db.BeginTransaction();
            Exec(db, tx, @"INSERT INTO npc_chat_consent(owner,enabled,updated_at)
                VALUES(@owner,@enabled,@now)
                ON DUPLICATE KEY UPDATE enabled=VALUES(enabled),updated_at=VALUES(updated_at)",
                ("@owner", Id(owner)), ("@enabled", enabled ? 1 : 0),
                ("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            if (!enabled)
                DeleteOwner(db, tx, owner);
            tx.Commit();
        }
        private static void DeleteOwner(DbConnection db, DbTransaction tx, Guid owner)
        {
            Exec(db, tx, "DELETE FROM npc_chat_messages WHERE owner=@owner",
                ("@owner", Id(owner)));
        }
        internal void DeleteHistory(Guid owner)
        {
            if (owner == Guid.Empty) throw new ArgumentException("Owner required.");
            using DbConnection db = Open();
            using DbTransaction tx = db.BeginTransaction();
            DeleteOwner(db, tx, owner);
            tx.Commit();
        }
        // Caller MUST also verify a fresh signed NPC owner snapshot. The
        // INSERT ... SELECT enforces consent atomically at database level.
        internal bool Append(Guid eventID, Guid owner, Guid npc, string sender, string text)
        {
            if (eventID == Guid.Empty || owner == Guid.Empty || npc == Guid.Empty ||
                (sender != "npc" && sender != "owner") ||
                string.IsNullOrWhiteSpace(text) ||
                Encoding.UTF8.GetByteCount(text) > 4096)
                return false;

            using DbConnection db = Open();
            using DbTransaction tx = db.BeginTransaction();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int inserted = Exec(db, tx, @"INSERT IGNORE INTO npc_chat_messages
                (event_id,owner,npc_id,sender,body,accepted_at)
                SELECT @event,@owner,@npc,@sender,@body,@now
                FROM npc_chat_consent WHERE owner=@owner AND enabled=1",
                ("@event",Id(eventID)),("@owner",Id(owner)),("@npc",Id(npc)),
                ("@sender",sender),("@body",text),("@now",now));
            // A duplicate event remains successfully acknowledged, but
            // an owner without consent must never be acknowledged as saved.
            bool consent = Enabled(db, tx, owner);
            Purge(db, tx);

            // MySQL 1093: do not DELETE using a SELECT from the same table;
            // resolve the 10,000-row cutoff first, like NexCitizenImMySqlStore.
            object cutoff;
            using (DbCommand limit = Command(db, tx,
                "SELECT seq FROM npc_chat_messages WHERE owner=@owner " +
                "ORDER BY seq DESC LIMIT 1 OFFSET 9999",
                ("@owner",Id(owner))))
                cutoff = limit.ExecuteScalar();
            if (cutoff != null && cutoff != DBNull.Value)
                Exec(db, tx, "DELETE FROM npc_chat_messages WHERE owner=@owner AND seq<@seq",
                    ("@owner",Id(owner)),("@seq",Convert.ToInt64(cutoff)));
            tx.Commit();
            return inserted > 0 || consent;
        }
        internal (List<object> Messages, string NextCursor) Read(Guid owner, Guid npc, long after, int limit)
        {
            if (owner == Guid.Empty || npc == Guid.Empty ||
                after < 0 || limit < 1 || limit > 500)
                throw new ArgumentException("Invalid NPC chat pagination.");
            using DbConnection db = Open();
            Purge(db);
            var entries = new List<object>();
            long lastSeq = 0;
            bool more = false;
            using DbCommand cmd = Command(db, null,
                @"SELECT seq,event_id,sender,body,accepted_at
                    FROM npc_chat_messages WHERE owner=@owner AND npc_id=@npc AND seq>@after
                    ORDER BY seq ASC LIMIT @limit",
                ("@owner",Id(owner)),("@npc",Id(npc)),("@after",after),("@limit",limit+1));
            using DbDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (entries.Count == limit) { more = true; break; }
                lastSeq = Convert.ToInt64(reader.GetValue(0));
                entries.Add(new
                {
                    message_id = reader.GetString(1),
                    npc_id = Id(npc),
                    sender = reader.GetString(2),
                    text = reader.GetString(3),
                    sent_at = DateTimeOffset.FromUnixTimeSeconds(
                        Convert.ToInt64(reader.GetValue(4))).UtcDateTime.ToString("O")
                });
            }
            return (entries, more ? lastSeq.ToString() : "");
        }
        internal int RetentionDays => m_RetentionDays;
    }
}
