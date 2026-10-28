// SPDX-License-Identifier: MPL-2.0

using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    /// <summary>
    /// Optional grid-wide, cross-estate script-message dispatcher.
    /// The router is responsible for permission checks, local delivery,
    /// cross-node transport authentication, and anti-flood controls.
    /// </summary>
    public interface IGridScriptMessageRouter
    {
        bool TryBroadcastGridMessage(
            UUID sourceRegionId,
            UUID sourceObjectId,
            UUID sourceOwnerId,
            string sourceName,
            int channel,
            string message);
    }
}
