using System;
using System.Collections.Generic;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Services.InventoryService.Tests
{
    [TestFixture]
    public class XInventoryOperationResultTests : OpenSimTestCase
    {
        private static IInventoryService CreateInventoryService()
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("InventoryService");
            config.Configs["InventoryService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");
            return ServerUtils.LoadPlugin<IInventoryService>(
                "OpenSim.Services.InventoryService.dll:XInventoryService", new Object[] { config });
        }

        [Test]
        public void TestMoveItemsPropagatesBackendFailure()
        {
            UUID owner = UUID.Random();
            UUID itemID = UUID.Random();
            UUID destination = UUID.Random();
            IInventoryService inventory = CreateInventoryService();
            InventoryItemBase item = new InventoryItemBase(itemID, owner)
            {
                CreatorIdentification = owner.ToString(),
                AssetID = UUID.Random(),
                Folder = UUID.Random(),
                Name = "Inventory move regression",
                InvType = (int)InventoryType.Object,
                AssetType = (int)AssetType.Object
            };

            // A missing backend item must never be reported as moved.
            item.Folder = destination;
            Assert.That(inventory.MoveItems(owner, new List<InventoryItemBase> { item }), Is.False);

            // Valid items still move normally.
            item.Folder = UUID.Random();
            Assert.That(inventory.AddItem(item), Is.True);
            item.Folder = destination;
            Assert.That(inventory.MoveItems(owner, new List<InventoryItemBase> { item }), Is.True);
            Assert.That(inventory.GetItem(owner, itemID).Folder, Is.EqualTo(destination));
        }
    }
}
