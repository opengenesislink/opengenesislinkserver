// SPDX-License-Identifier: MPL-2.0
// Durable, server-owned publication registry; never scans or publishes
// arbitrary FSAsset/OpenSim inventory contents.
using System;
using MySql.Data.MySqlClient;
using OpenMetaverse;

namespace NexVerse.Server.Api
{
    internal sealed class NexMediaCatalogStore
    {
        internal readonly struct MediaRecord
        {
            internal readonly UUID Asset;
            internal readonly UUID Owner;
            internal readonly bool Public;
            internal readonly string Mime;

            internal MediaRecord(UUID asset, UUID owner, bool isPublic, string mime)
            {
                Asset = asset;
                Owner = owner;
                Public = isPublic;
                Mime = mime;
            }
        }

        private readonly string m_Connection;

        internal NexMediaCatalogStore(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Existing ROBUST MariaDB connection required.");
            m_Connection = connectionString;
            using MySqlConnection db = Open();
            using MySqlCommand schema = db.CreateCommand();
            schema.CommandText = @"CREATE TABLE IF NOT EXISTS ogl_media_uploads (
                asset_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
                owner_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                mime VARCHAR(24) NOT NULL,
                is_public TINYINT(1) NOT NULL DEFAULT 0,
                created_at BIGINT NOT NULL,
                INDEX ix_ogl_media_owner (owner_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
            schema.ExecuteNonQuery();
            schema.CommandText = @"CREATE TABLE IF NOT EXISTS ogl_media_marketplace (
                listing_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
                asset_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                owner_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                updated_at BIGINT NOT NULL,
                INDEX ix_ogl_media_listing_asset (asset_id),
                CONSTRAINT fk_ogl_media_uploaded_asset FOREIGN KEY(asset_id)
                    REFERENCES ogl_media_uploads(asset_id) ON DELETE RESTRICT
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
            schema.ExecuteNonQuery();
        }

        private MySqlConnection Open()
        {
            var db = new MySqlConnection(m_Connection);
            db.Open();
            return db;
        }

        internal void RecordUpload(UUID assetId, UUID ownerId, string mime)
        {
            if (assetId.IsZero() || ownerId.IsZero() ||
                (mime != "image/png" && mime != "image/jpeg"))
                throw new ArgumentException("Invalid owned media upload.");
            using MySqlConnection db = Open();
            using MySqlCommand command = db.CreateCommand();
            command.CommandText = @"INSERT INTO ogl_media_uploads
                (asset_id,owner_id,mime,is_public,created_at)
                VALUES(@asset,@owner,@mime,0,@now)";
            command.Parameters.AddWithValue("@asset", assetId.ToString());
            command.Parameters.AddWithValue("@owner", ownerId.ToString());
            command.Parameters.AddWithValue("@mime", mime);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            command.ExecuteNonQuery();
        }

        internal MediaRecord? FindUpload(UUID assetId)
        {
            if (assetId.IsZero()) return null;
            using MySqlConnection db = Open();
            using MySqlCommand command = db.CreateCommand();
            command.CommandText = @"SELECT owner_id,is_public,mime FROM ogl_media_uploads
                                    WHERE asset_id=@asset LIMIT 1";
            command.Parameters.AddWithValue("@asset", assetId.ToString());
            using MySqlDataReader reader = command.ExecuteReader();
            if (!reader.Read() || !UUID.TryParse(reader.GetString(0), out UUID owner))
                return null;
            return new MediaRecord(assetId, owner, reader.GetBoolean(1), reader.GetString(2));
        }

        internal bool ChangeVisibility(UUID assetId, UUID ownerId, bool publicAccess)
        {
            if (assetId.IsZero() || ownerId.IsZero()) return false;
            using MySqlConnection db = Open();
            using MySqlCommand command = db.CreateCommand();
            command.CommandText = @"UPDATE ogl_media_uploads SET is_public=@visible
                WHERE asset_id=@asset AND owner_id=@owner";
            command.Parameters.AddWithValue("@asset", assetId.ToString());
            command.Parameters.AddWithValue("@owner", ownerId.ToString());
            command.Parameters.AddWithValue("@visible", publicAccess);
            return command.ExecuteNonQuery() == 1;
        }

        internal bool PublishListing(UUID listingId, UUID assetId, UUID ownerId)
        {
            if (listingId.IsZero() || assetId.IsZero() || ownerId.IsZero()) return false;
            using MySqlConnection db = Open();
            using MySqlTransaction tx = db.BeginTransaction();
            using (MySqlCommand check = db.CreateCommand())
            {
                check.Transaction = tx;
                check.CommandText = @"SELECT COUNT(*) FROM ogl_media_uploads
                    WHERE asset_id=@asset AND owner_id=@owner FOR UPDATE";
                check.Parameters.AddWithValue("@asset", assetId.ToString());
                check.Parameters.AddWithValue("@owner", ownerId.ToString());
                if (Convert.ToInt32(check.ExecuteScalar()) != 1)
                    return false;
            }
            using (MySqlCommand cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO ogl_media_marketplace
                    (listing_id,asset_id,owner_id,updated_at)
                    VALUES(@listing,@asset,@owner,@now)
                    ON DUPLICATE KEY UPDATE asset_id=VALUES(asset_id),
                      owner_id=VALUES(owner_id),updated_at=VALUES(updated_at)";
                cmd.Parameters.AddWithValue("@listing", listingId.ToString());
                cmd.Parameters.AddWithValue("@asset", assetId.ToString());
                cmd.Parameters.AddWithValue("@owner", ownerId.ToString());
                cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return true;
        }

        internal UUID? FindListingImage(UUID listingId)
        {
            if (listingId.IsZero()) return null;
            using MySqlConnection db = Open();
            using MySqlCommand cmd = db.CreateCommand();
            cmd.CommandText = @"SELECT m.asset_id FROM ogl_media_marketplace m
                INNER JOIN ogl_media_uploads u ON m.asset_id=u.asset_id
                WHERE m.listing_id=@listing LIMIT 1";
            cmd.Parameters.AddWithValue("@listing", listingId.ToString());
            object result = cmd.ExecuteScalar();
            return result != null && UUID.TryParse(Convert.ToString(result), out UUID id)
                && !id.IsZero() ? id : null;
        }

        internal bool UnpublishListing(UUID listingId)
        {
            if (listingId.IsZero()) return false;
            using MySqlConnection db = Open();
            using MySqlCommand cmd = db.CreateCommand();
            cmd.CommandText = @"DELETE FROM ogl_media_marketplace WHERE listing_id=@listing";
            cmd.Parameters.AddWithValue("@listing", listingId.ToString());
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
