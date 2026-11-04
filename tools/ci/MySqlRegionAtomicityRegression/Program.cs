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
        return Convert.ToString(cmd.ExecuteScalar());
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
}
finally
{
    Sql($"DROP TRIGGER IF EXISTS {triggerName}");
    Sql("DROP TRIGGER IF EXISTS ogl_ci_reject_primitems");
    // Only test-generated UUIDs in the disposable CI database.
    using var cleanup = connection.CreateCommand();
    cleanup.CommandText = "DELETE FROM primitems WHERE primID=@uuid; " +
                          "DELETE FROM primshapes WHERE UUID=@uuid; " +
                          "DELETE FROM prims WHERE UUID=@uuid";
    cleanup.Parameters.AddWithValue("@uuid", rootId.ToString());
    cleanup.ExecuteNonQuery();
}
