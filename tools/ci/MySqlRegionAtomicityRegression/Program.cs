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
}
finally
{
    Sql($"DROP TRIGGER IF EXISTS {triggerName}");
    // Only test-generated UUIDs in the disposable CI database.
    using var cleanup = connection.CreateCommand();
    cleanup.CommandText = "DELETE FROM primshapes WHERE UUID=@uuid; " +
                          "DELETE FROM prims WHERE UUID=@uuid";
    cleanup.Parameters.AddWithValue("@uuid", rootId.ToString());
    cleanup.ExecuteNonQuery();
}
