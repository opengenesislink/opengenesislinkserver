using OpenMetaverse;
using OpenSim.Data.MySQL;
using OpenSim.Data.PGSQL;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

// Run only on disposable CI databases; neither external nor production
// connection strings are accepted.
if (args.Length != 1 || (args[0] != "mysql" && args[0] != "pgsql"))
    throw new ArgumentException("Usage: RegionInventoryBatchRegression mysql|pgsql");

string dbProvider = args[0];
bool mysql = dbProvider == "mysql";
string secret = Environment.GetEnvironmentVariable("OGL_REGION_BATCH_CI_PASSWORD")
    ?? throw new InvalidOperationException("OGL_REGION_BATCH_CI_PASSWORD is required");
string connString = mysql
    ? $"Server=127.0.0.1;Port=3306;Database=ogl_region_batch_ci;Uid=root;Pwd={secret}"
    : $"Host=127.0.0.1;Port=5432;Database=ogl_region_batch_ci;Username=postgres;Password={secret}";

ISimulationDataStore db = mysql
    ? new MySQLSimulationData(connString)
    : new PGSQLSimulationData(connString);

static SceneObjectGroup NewObject(string name)
{
    SceneObjectPart part = new SceneObjectPart
    {
        UUID = UUID.Random(),
        Name = name,
        Description = name,
        Shape = PrimitiveBaseShape.Default
    };
    return new SceneObjectGroup(part);
}

static TaskInventoryItem NewItem(UUID parent, string name)
{
    UUID owner = UUID.Random();
    return new TaskInventoryItem
    {
        ItemID = UUID.Random(),
        AssetID = UUID.Random(),
        ParentPartID = parent,
        ParentID = parent,
        OwnerID = owner,
        LastOwnerID = owner,
        CreatorID = owner,
        GroupID = UUID.Zero,
        Type = (int)AssetType.Notecard,
        InvType = (int)InventoryType.Notecard,
        Name = name,
        Description = name
    };
}

UUID regionA = UUID.Random();
UUID regionB = UUID.Random();
SceneObjectGroup objA = NewObject("Batch Region A");
SceneObjectGroup objB = NewObject("Batch Region B");
TaskInventoryItem a1 = NewItem(objA.RootPart.UUID, "A1");
TaskInventoryItem a2 = NewItem(objA.RootPart.UUID, "A2");
TaskInventoryItem b1 = NewItem(objB.RootPart.UUID, "B1");
try
{
    db.StoreObject(objA, regionA);
    db.StoreObject(objB, regionB);
    db.StorePrimInventory(objA.RootPart.UUID, new[] { a1, a2 });
    db.StorePrimInventory(objB.RootPart.UUID, new[] { b1 });

    List<SceneObjectGroup> loadedA = db.LoadObjects(regionA);
    if (loadedA.Count != 1 || loadedA[0].RootPart.UUID != objA.RootPart.UUID)
        throw new Exception("Region A objects were not isolated");
    SceneObjectPart loadedPartA = loadedA[0].RootPart;
    if (loadedPartA.Inventory.GetInventoryItem(a1.ItemID) == null ||
        loadedPartA.Inventory.GetInventoryItem(a2.ItemID) == null ||
        loadedPartA.Inventory.GetInventoryItem(b1.ItemID) != null ||
        loadedPartA.Inventory.GetInventoryItems().Count != 2)
        throw new Exception("Region A inventory mismatch after batch load");

    List<SceneObjectGroup> loadedB = db.LoadObjects(regionB);
    if (loadedB.Count != 1 || loadedB[0].RootPart.UUID != objB.RootPart.UUID)
        throw new Exception("Region B objects were not isolated");
    SceneObjectPart loadedPartB = loadedB[0].RootPart;
    if (loadedPartB.Inventory.GetInventoryItem(b1.ItemID) == null ||
        loadedPartB.Inventory.GetInventoryItem(a1.ItemID) != null ||
        loadedPartB.Inventory.GetInventoryItems().Count != 1)
        throw new Exception("Region B inventory mismatch after batch load");

    Console.WriteLine($"PASS: {dbProvider} region-scoped batched task inventory roundtrip");
}
finally
{
    db.RemoveObject(objA.RootPart.UUID, regionA);
    db.RemoveObject(objB.RootPart.UUID, regionB);
    db.Dispose();
}
