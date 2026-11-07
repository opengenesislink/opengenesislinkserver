// SPDX-License-Identifier: MPL-2.0

using System;

namespace NexVerse.Core.Economy
{
    /// <summary>
    /// In-process, trusted signal raised only after a successful *local*
    /// authenticated ROBUST viewer login. Hypergrid travel, region teleports,
    /// failed authentication, ToS rejection and account creation never fire it.
    ///
    /// This event does not accept untrusted HTTP traffic. The listener is the
    /// authoritative economy connector in the same ROBUST process.
    /// </summary>
    public static class NexResidentLoginEvents
    {
        public static event Action<Guid, string> SuccessfulLocalLogin;

        public static void NotifySuccessfulLocalLogin(Guid residentId, string displayName)
        {
            if (residentId == Guid.Empty)
                throw new ArgumentException("Local resident is required.", nameof(residentId));
            SuccessfulLocalLogin?.Invoke(residentId, displayName);
        }
    }
}
