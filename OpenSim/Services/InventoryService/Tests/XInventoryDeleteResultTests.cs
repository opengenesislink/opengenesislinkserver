using System;
using System.Collections.Generic;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;

namespace OpenSim.Services.InventoryService.Tests
{
    [TestFixture]
    public class XInventoryDeleteResultTests
    {
        private sealed class TestService : XInventoryService
        {
            public TestService(IConfigSource config) : base(config) { }
            public IXInventoryData Database
            {
                get { return m_Database; }
                set { m_Database = value; }
            }
        }

        // Delegating mock: simulate the database rejecting a delete while
        // allowing subsequent reads to verify the row is still present.
        private sealed class RefusingDeletes : IXInventoryData
        {
            private readonly IXInventoryData m_inner;
            public bool RefuseItems;
            public bool RefuseFolders;

            public RefusingDeletes(IXInventoryData inner) { m_inner = inner; }
            public XInventoryFolder[] GetFolder(string field, string val) => m_inner.GetFolder(field, val);
            public XInventoryFolder[] GetFolders(string[] fields, string[] vals) => m_inner.GetFolders(fields, vals);
            public XInventoryItem[] GetItems(string[] fields, string[] vals) => m_inner.GetItems(fields, vals);
            public bool StoreFolder(XInventoryFolder folder) => m_inner.StoreFolder(folder);
            public bool StoreItem(XInventoryItem item) => m_inner.StoreItem(item);
            public bool DeleteFolders(string field, string val) => !RefuseFolders && m_inner.DeleteFolders(field, val);
            public bool DeleteFolders(string[] fields, string[] vals) => !RefuseFolders && m_inner.DeleteFolders(fields, vals);
            public bool DeleteItems(string field, string val) => !RefuseItems && m_inner.DeleteItems(field, val);
            public bool DeleteItems(string[] fields, string[] vals) => !RefuseItems && m_inner.DeleteItems(fields, vals);
            public bool MoveItem(string id, string newParent) => m_inner.MoveItem(id, newParent);
            public bool MoveFolder(string id, string newParent) => m_inner.MoveFolder(id, newParent);
            public XInventoryItem[] GetActiveGestures(UUID principalID) => m_inner.GetActiveGestures(principalID);
            public int GetAssetPermissions(UUID principalID, UUID assetID) => m_inner.GetAssetPermissions(principalID, assetID);
        }

        private static TestService NewService()
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("InventoryService").Set("StorageProvider", "OpenSim.Tests.Common.dll");
            return new TestService(config);
        }

        private static InventoryItemBase NewItem(UUID owner, UUID folderID)
        {
            return new InventoryItemBase(UUID.Random(), owner)
            {
                CreatorIdentification = owner.ToString(),
                AssetID = UUID.Random(),
                Name = "Delete failure regression",
                Folder = folderID,
                InvType = (int)InventoryType.Object,
                AssetType = (int)AssetType.Object
            };
        }

        [Test]
        public void TestDeleteItemReportsFailureAndAllowsIdempotentDelete()
        {
            TestService service = NewService();
            UUID owner = UUID.Random();
            InventoryItemBase item = NewItem(owner, UUID.Random());
            Assert.That(service.AddItem(item), Is.True);

            var refuse = new RefusingDeletes(service.Database) { RefuseItems = true };
            service.Database = refuse;

            Assert.That(service.DeleteItems(owner, new List<UUID> { item.ID }), Is.False);
            Assert.That(service.GetItem(owner, item.ID), Is.Not.Null);

            refuse.RefuseItems = false;
            Assert.That(service.DeleteItems(owner, new List<UUID> { item.ID }), Is.True);
            Assert.That(service.DeleteItems(owner, new List<UUID> { item.ID }), Is.True);
            Assert.That(service.GetItem(owner, item.ID), Is.Null);
        }

        [Test]
        public void TestFailedFolderPurgeDoesNotDeleteFolder()
        {
            TestService service = NewService();
            UUID owner = UUID.Random();
            Assert.That(service.CreateUserInventory(owner), Is.True);

            InventoryFolderBase trash = service.GetFolderForType(owner, FolderType.Trash);
            Assert.That(trash, Is.Not.Null);

            InventoryFolderBase child = new InventoryFolderBase
            {
                ID = UUID.Random(), ParentID = trash.ID, Owner = owner,
                Name = "CI delete test", Type = (short)FolderType.None, Version = 1
            };
            Assert.That(service.AddFolder(child), Is.True);

            InventoryItemBase item = NewItem(owner, child.ID);
            Assert.That(service.AddItem(item), Is.True);
            var refuse = new RefusingDeletes(service.Database) { RefuseItems = true };
            service.Database = refuse;

            Assert.That(service.DeleteFolders(owner, new List<UUID> { child.ID }), Is.False);
            Assert.That(service.GetFolder(owner, child.ID), Is.Not.Null);
            Assert.That(service.GetItem(owner, item.ID), Is.Not.Null);

            refuse.RefuseItems = false;
            refuse.RefuseFolders = true;
            Assert.That(service.DeleteFolders(owner, new List<UUID> { child.ID }), Is.False);
            Assert.That(service.GetFolder(owner, child.ID), Is.Not.Null);

            refuse.RefuseFolders = false;
            Assert.That(service.DeleteFolders(owner, new List<UUID> { child.ID }), Is.True);
            Assert.That(service.GetFolder(owner, child.ID), Is.Null);
        }
    }
}
