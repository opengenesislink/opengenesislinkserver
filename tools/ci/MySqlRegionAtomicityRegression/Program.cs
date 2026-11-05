using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Data.MySQL;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

// End-to-end rollback test for the production MySQLSimulationData.StoreObject
// method against a disposable, local MariaDB schema. Never use production data.
string password = Environment.GetEnvironmentVariable("OGL_MYSQL_REGION_TX_CI_PASSWORD")
    ?? throw new InvalidOperationException("OGL_MYSQL_REGION_TX_CI_PASSWORD is required");
string connectionString = new MySqlConnectionStringBuilder
{
    Server = "127.0.0.1",
    Port = 3306,
    Database = "ogl_region_atomic_ci",
    UserID = "root",
    Password = password
}.ConnectionString;

var db = new MySQLSimulationData(connectionString);
using var connection = new MySqlConnection(connectionString);
connection.Open();

const string triggerName = "ogl_ci_reject_primshapes";
var rootId = UUID.Random();
var regionId = UUID.Random();
var obj = new SceneObjectGroup(
    UUID.Random(), new Vector3(128f, 128f, 25f), PrimitiveBaseShape.CreateBox());
obj.RootPart.UUID = rootId;
obj.RootPart.Name = "OGL CI Atomic Prim";
obj.RootPart.Description = "Test-only object; intentionally fail shape store";

void Sql(string sql)
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = sql;
    cmd.ExecuteNonQuery();
}

long Count(string table)
{
    // Table is one of two fixed, internal test identifiers.
    if (table != "prims" && table != "primshapes")
        throw new ArgumentException("Unexpected test table", nameof(table));
    using var cmd = connection.CreateCommand();
    cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE UUID = @uuid";
    cmd.Parameters.AddWithValue("@uuid", rootId.ToString());
    return Convert.ToInt64(cmd.ExecuteScalar());
}

try
{
    Sql($"DROP TRIGGER IF EXISTS {triggerName}");
    // A successful prim REPLACE followed by a failing shape REPLACE must
    // leave neither row behind if both commands belong to one transaction.
    Sql($"CREATE TRIGGER {triggerName} BEFORE INSERT ON primshapes " +
        "FOR EACH ROW SIGNAL SQLSTATE '45000' " +
        "SET MESSAGE_TEXT = 'OGL intentional CI primshape failure'");

    bool failedAsExpected = false;
    try
    {
        db.StoreObject(obj, regionId);
    }
    catch (MySqlException)
    {
        failedAsExpected = true;
    }

    if (!failedAsExpected)
        throw new Exception("StoreObject did not surface the injected SQL error");
    if (Count("prims") != 0 || Count("primshapes") != 0)
        throw new Exception("StoreObject exposed a partial object after SQL failure");

    Sql($"DROP TRIGGER IF EXISTS {triggerName}");
    db.StoreObject(obj, regionId);
    if (Count("prims") != 1 || Count("primshapes") != 1)
        throw new Exception("StoreObject did not commit both rows on successful retry");

    Console.WriteLine("PASS: MySQL StoreObject rollback and retry");

    // A failed inventory replacement must retain all previously stored items.
    UUID ownerId = UUID.Random();
    TaskInventoryItem originalItem = new TaskInventoryItem
    {
        ItemID = UUID.Random(),
        AssetID = UUID.Random(),
        ParentPartID = rootId,
        ParentID = rootId,
        OwnerID = ownerId,
        LastOwnerID = ownerId,
        CreatorID = ownerId,
        GroupID = UUID.Zero,
        Name = "Before failed inventory replacement",
        Description = "CI-only original inventory entry",
        Type = (int)AssetType.Notecard,
        InvType = (int)InventoryType.Notecard
    };
    db.StorePrimInventory(rootId, new[] { originalItem });

    string ReadStoredItemName()
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM primitems WHERE primID = @id";
        cmd.Parameters.AddWithValue("@id", rootId.ToString());
        object value = cmd.ExecuteScalar();
        return value == null || value is DBNull ? null : Convert.ToString(value);
    }

    if (ReadStoredItemName() != originalItem.Name)
        throw new Exception("Baseline prim inventory was not stored");

    const string inventoryTrigger = "ogl_ci_reject_primitems";
    Sql($"DROP TRIGGER IF EXISTS {inventoryTrigger}");
    Sql($"CREATE TRIGGER {inventoryTrigger} BEFORE INSERT ON primitems " +
        "FOR EACH ROW SIGNAL SQLSTATE '45000' " +
        "SET MESSAGE_TEXT = 'OGL intentional CI primitem failure'");

    TaskInventoryItem replacementItem = new TaskInventoryItem
    {
        ItemID = UUID.Random(),
        AssetID = UUID.Random(),
        ParentPartID = rootId,
        ParentID = rootId,
        OwnerID = ownerId,
        LastOwnerID = ownerId,
        CreatorID = ownerId,
        GroupID = UUID.Zero,
        Name = "After successful replacement",
        Description = "CI-only replacement inventory entry",
        Type = (int)AssetType.Notecard,
        InvType = (int)InventoryType.Notecard
    };
    bool inventoryFailure = false;
    try
    {
        db.StorePrimInventory(rootId, new[] { replacementItem });
    }
    catch (MySqlException)
    {
        inventoryFailure = true;
    }

    if (!inventoryFailure || ReadStoredItemName() != originalItem.Name)
        throw new Exception("Failed inventory replace discarded the previous item");

    Sql($"DROP TRIGGER IF EXISTS {inventoryTrigger}");
    db.StorePrimInventory(rootId, new[] { replacementItem });
    if (ReadStoredItemName() != replacementItem.Name)
        throw new Exception("Successful inventory replacement was not persisted");

    db.StorePrimInventory(rootId, Array.Empty<TaskInventoryItem>());
    if (ReadStoredItemName() != null)
        throw new Exception("Empty inventory replacement did not commit deletion");


    Console.WriteLine("PASS: MySQL StorePrimInventory rollback, retry and empty inventory");

    // Both terrain writers are asynchronous. A non-transactional CI-only
    // marker proves the failing INSERT trigger actually ran; a fixed delay
    // alone could produce false positives when the background job never runs.
    const string markerTable = "ogl_ci_terrain_attempts";
    Sql($"CREATE TABLE IF NOT EXISTS {markerTable} (kind VARCHAR(20) NOT NULL) ENGINE=MyISAM");

    void VerifyTerrainRollback(string table, string trigger, string kind)
    {
        if (table != "terrain" && table != "bakedterrain")
            throw new ArgumentException("Unexpected terrain table", nameof(table));

        UUID rid = UUID.Random();
        byte[] original = new byte[] { 0x51, 0x52, 0x53, 0x54 };
        Sql($"INSERT INTO {table} (RegionUUID, Revision, Heightfield) " +
            $"VALUES ('{rid}', 11, X'51525354')");
        Sql($"DROP TRIGGER IF EXISTS {trigger}");

        (int revision, byte[] blob, long rows) ReadTerrain()
        {
            using var query = connection.CreateCommand();
            query.CommandText = $"SELECT Revision, Heightfield FROM {table} WHERE RegionUUID=@id";
            query.Parameters.AddWithValue("@id", rid.ToString());
            using var reader = query.ExecuteReader();
            if (!reader.Read())
                return (0, null, 0);
            int rev = reader.GetInt32(0);
            byte[] bytes = (byte[])reader.GetValue(1);
            long count = 1;
            while (reader.Read())
                count++;
            return (rev, bytes, count);
        }

        long MarkerCount()
        {
            using var check = connection.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM {markerTable} WHERE kind=@kind";
            check.Parameters.AddWithValue("@kind", kind);
            return Convert.ToInt64(check.ExecuteScalar());
        }

        void Store(TerrainData terrain)
        {
            if (table == "terrain") db.StoreTerrain(terrain, rid);
            else db.StoreBakedTerrain(terrain, rid);
        }

        var updatedHeights = new double[256, 256];
        updatedHeights[0, 0] = 33.25;
        TerrainData update = new TerrainData(updatedHeights);

        try
        {
            Sql($"CREATE TRIGGER {trigger} BEFORE INSERT ON {table} " +
                "FOR EACH ROW BEGIN " +
                $"INSERT INTO {markerTable} (kind) VALUES ('{kind}'); " +
                "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'OGL intentional terrain write failure'; END");

            Store(update);
            DateTime deadline = DateTime.UtcNow.AddSeconds(20);
            while (MarkerCount() == 0 && DateTime.UtcNow < deadline)
                System.Threading.Thread.Sleep(50);
            if (MarkerCount() == 0)
                throw new Exception($"Async {table} INSERT failure was never exercised");

            // The terrain worker and GetObjectIDs serialize on the same
            // m_dbLock. This ensures the failed transaction has completed
            // its rollback before validating the stored baseline.
            db.GetObjectIDs(rid);

            // Wait for the worker's transaction to finish; committed changes
            // must never expose a missing or partially updated terrain row.
            deadline = DateTime.UtcNow.AddSeconds(20);
            bool confirmedRollback = false;
            while (DateTime.UtcNow < deadline)
            {
                var row = ReadTerrain();
                if (row.rows == 1 && row.revision == 11 && row.blob.SequenceEqual(original))
                {
                    confirmedRollback = true;
                    break;
                }
                System.Threading.Thread.Sleep(50);
            }
            if (!confirmedRollback)
                throw new Exception($"Failed {table} replacement removed original terrain data");

            Sql($"DROP TRIGGER IF EXISTS {trigger}");
            Store(update);
            deadline = DateTime.UtcNow.AddSeconds(20);
            bool storedNewTerrain = false;
            while (DateTime.UtcNow < deadline)
            {
                var row = ReadTerrain();
                if (row.rows == 1 && row.revision == (int)DBTerrainRevision.Variable2DGzip
                    && !row.blob.SequenceEqual(original))
                {
                    storedNewTerrain = true;
                    break;
                }
                System.Threading.Thread.Sleep(50);
            }
            if (!storedNewTerrain)
                throw new Exception($"Valid {table} retry did not replace terrain data");

            Console.WriteLine($"PASS: MySQL {table} async rollback and retry");
        }
        finally
        {
            Sql($"DROP TRIGGER IF EXISTS {trigger}");
            Sql($"DELETE FROM {table} WHERE RegionUUID='{rid}'");
        }
    }

    VerifyTerrainRollback("terrain", "ogl_ci_reject_terrain", "terrain");
    VerifyTerrainRollback("bakedterrain", "ogl_ci_reject_bakedterrain", "bakedterrain");

}
finally
{
    Sql($"DROP TRIGGER IF EXISTS {triggerName}");
    Sql("DROP TRIGGER IF EXISTS ogl_ci_reject_primitems");
    Sql("DROP TRIGGER IF EXISTS ogl_ci_reject_terrain");
    Sql("DROP TRIGGER IF EXISTS ogl_ci_reject_bakedterrain");
    Sql("DROP TABLE IF EXISTS ogl_ci_terrain_attempts");
    // Only test-generated UUIDs in the disposable CI database.
    using var cleanup = connection.CreateCommand();
    cleanup.CommandText = "DELETE FROM primitems WHERE primID=@uuid; " +
                          "DELETE FROM primshapes WHERE UUID=@uuid; " +
                          "DELETE FROM prims WHERE UUID=@uuid";
    cleanup.Parameters.AddWithValue("@uuid", rootId.ToString());
    cleanup.ExecuteNonQuery();
}
