/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Services.InventoryService.Tests
{
    /// <summary>
    /// Tests for the XInventoryService
    /// </summary>
    /// <remarks>
    /// TODO: Fill out more tests.
    /// </remarks>
    [TestFixture]
    public class XInventoryServiceTests : OpenSimTestCase
    {
        private IInventoryService CreateXInventoryService()
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("InventoryService");
            config.Configs["InventoryService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");

            return ServerUtils.LoadPlugin<IInventoryService>(
                "OpenSim.Services.InventoryService.dll:XInventoryService", new Object[] { config });
        }

        /// <summary>
        /// Tests add item operation.
        /// </summary>
        /// <remarks>
        /// TODO: Test all operations.
        /// </remarks>
        [Test]
        public void TestAddItem()
        {
            TestHelpers.InMethod();

            string creatorId = TestHelpers.ParseTail(0x1).ToString();
            UUID ownerId = TestHelpers.ParseTail(0x2);
            UUID itemId = TestHelpers.ParseTail(0x10);
            UUID assetId = TestHelpers.ParseTail(0x20);
            UUID folderId = TestHelpers.ParseTail(0x30);
            int invType = (int)InventoryType.Animation;
            int assetType = (int)AssetType.Animation;
            string itemName = "item1";

            IInventoryService xis = CreateXInventoryService();

            InventoryItemBase itemToStore
                = new InventoryItemBase(itemId, ownerId)
                {
                    CreatorIdentification = creatorId.ToString(),
                    AssetID = assetId,
                    Name = itemName,
                    Folder = folderId,
                    InvType = invType,
                    AssetType = assetType
                };

            Assert.That(xis.AddItem(itemToStore), Is.True);

            InventoryItemBase itemRetrieved = xis.GetItem(UUID.Zero, itemId);

            Assert.That(itemRetrieved, Is.Not.Null);
            Assert.That(itemRetrieved.CreatorId, Is.EqualTo(creatorId));
            Assert.That(itemRetrieved.Owner, Is.EqualTo(ownerId));
            Assert.That(itemRetrieved.AssetID, Is.EqualTo(assetId));
            Assert.That(itemRetrieved.Folder, Is.EqualTo(folderId));
            Assert.That(itemRetrieved.InvType, Is.EqualTo(invType));
            Assert.That(itemRetrieved.AssetType, Is.EqualTo(assetType));
            Assert.That(itemRetrieved.Name, Is.EqualTo(itemName));
        }

        [Test]
        public void TestUpdateItem()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string creatorId = TestHelpers.ParseTail(0x1).ToString();
            UUID ownerId = TestHelpers.ParseTail(0x2);
            UUID itemId = TestHelpers.ParseTail(0x10);
            UUID assetId = TestHelpers.ParseTail(0x20);
            UUID folderId = TestHelpers.ParseTail(0x30);
            int invType = (int)InventoryType.Animation;
            int assetType = (int)AssetType.Animation;
            string itemName = "item1";
            string itemName2 = "item2";

            IInventoryService xis = CreateXInventoryService();

            InventoryItemBase itemToStore
                = new InventoryItemBase(itemId, ownerId)
                {
                    CreatorIdentification = creatorId.ToString(),
                    AssetID = assetId,
                    Name = itemName,
                    Folder = folderId,
                    InvType = invType,
                    AssetType = assetType
                };

            Assert.That(xis.AddItem(itemToStore), Is.True);

            // Normal update
            itemToStore.Name = itemName2;

            Assert.That(xis.UpdateItem(itemToStore), Is.True);

            InventoryItemBase itemRetrieved = xis.GetItem(UUID.Zero, itemId);

            Assert.That(itemRetrieved, Is.Not.Null);
            Assert.That(itemRetrieved.Name, Is.EqualTo(itemName2));

            // Attempt to update properties that should never change
            string creatorId2 = TestHelpers.ParseTail(0x7).ToString();
            UUID ownerId2 = TestHelpers.ParseTail(0x8);
            UUID folderId2 = TestHelpers.ParseTail(0x70);
            int invType2 = (int)InventoryType.CallingCard;
            int assetType2 = (int)AssetType.CallingCard;
            string itemName3 = "item3";

            itemToStore.CreatorIdentification = creatorId2.ToString();
            itemToStore.Owner = ownerId2;
            itemToStore.Folder = folderId2;
            itemToStore.InvType = invType2;
            itemToStore.AssetType = assetType2;
            itemToStore.Name = itemName3;

            Assert.That(xis.UpdateItem(itemToStore), Is.True);

            itemRetrieved = xis.GetItem(itemRetrieved.Owner, itemRetrieved.ID);

            Assert.That(itemRetrieved, Is.Not.Null);
            Assert.That(itemRetrieved.CreatorId, Is.EqualTo(creatorId));
            Assert.That(itemRetrieved.Owner, Is.EqualTo(ownerId));
            Assert.That(itemRetrieved.AssetID, Is.EqualTo(assetId));
            Assert.That(itemRetrieved.Folder, Is.EqualTo(folderId));
            Assert.That(itemRetrieved.InvType, Is.EqualTo(invType));
            Assert.That(itemRetrieved.AssetType, Is.EqualTo(assetType));
            Assert.That(itemRetrieved.Name, Is.EqualTo(itemName3));
        }

        [Test]
        public void TestPurgeNestedLostAndFoundDescendant()
        {
            UUID owner = UUID.Random();
            IInventoryService inventory = CreateXInventoryService();
            Assert.That(inventory.CreateUserInventory(owner), Is.True);

            InventoryFolderBase lostAndFound = inventory.GetFolderForType(owner, FolderType.LostAndFound);
            Assert.That(lostAndFound, Is.Not.Null);

            InventoryFolderBase child = new InventoryFolderBase
            {
                ID = UUID.Random(),
                ParentID = lostAndFound.ID,
                Owner = owner,
                Name = "Recovered",
                Type = (short)FolderType.None,
                Version = 1
            };
            Assert.That(inventory.AddFolder(child), Is.True);

            InventoryFolderBase grandchild = new InventoryFolderBase
            {
                ID = UUID.Random(),
                ParentID = child.ID,
                Owner = owner,
                Name = "Recovered nested",
                Type = (short)FolderType.None,
                Version = 1
            };
            Assert.That(inventory.AddFolder(grandchild), Is.True);

            UUID itemId = UUID.Random();
            InventoryItemBase item = new InventoryItemBase(itemId, owner)
            {
                CreatorIdentification = owner.ToString(),
                AssetID = UUID.Random(),
                Name = "Recovered inventory item",
                Folder = grandchild.ID,
                InvType = (int)InventoryType.Object,
                AssetType = (int)AssetType.Object
            };
            Assert.That(inventory.AddItem(item), Is.True);
            Assert.That(inventory.GetItem(owner, itemId), Is.Not.Null);

            // The descendants must be treated as belonging to Lost And Found.
            Assert.That(inventory.PurgeFolder(grandchild), Is.True);
            Assert.That(inventory.GetItem(owner, itemId), Is.Null);

            // A regular folder outside Trash and Lost And Found remains protected.
            InventoryFolderBase regular = new InventoryFolderBase
            {
                ID = UUID.Random(),
                ParentID = lostAndFound.ParentID,
                Owner = owner,
                Name = "Protected",
                Type = (short)FolderType.None,
                Version = 1
            };
            Assert.That(inventory.AddFolder(regular), Is.True);
            Assert.That(inventory.PurgeFolder(regular), Is.False);
        }
    }
}
