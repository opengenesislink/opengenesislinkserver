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
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Grid Server
    /// </summary>
    public class MySQLPresenceData : MySQLGenericTableHandler<PresenceData>,
            IPresenceData, IHGForeignPresenceMaintenance
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MySQLPresenceData(string connectionString, string realm) :
                base(connectionString, realm, "Presence")
        {
        }

        public PresenceData Get(UUID sessionID)
        {
            PresenceData[] ret = Get("SessionID",
                    sessionID.ToString());

            if (ret.Length == 0)
                return null;

            return ret[0];
        }

        public void LogoutRegionAgents(UUID regionID)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText = String.Format("delete from {0} where `RegionID`=?RegionID", m_Realm);

                cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());

                ExecuteNonQuery(cmd);
            }
        }

        public bool ReportAgent(UUID sessionID, UUID regionID)
        {
            PresenceData[] pd = Get("SessionID", sessionID.ToString());
            if (pd.Length == 0)
                return false;

            if (regionID.IsZero())
                return false;

            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText = String.Format("update {0} set RegionID=?RegionID, LastSeen=NOW() where `SessionID`=?SessionID", m_Realm);

                cmd.Parameters.AddWithValue("?SessionID", sessionID.ToString());
                cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());

                if (ExecuteNonQuery(cmd) == 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Cleans only provably foreign, old and disconnected sessions.
        /// LastSeen is NOT a regular heartbeat in classic OpenSim: an aged
        /// record with Online=true and a nonzero RegionID is deliberately
        /// left untouched even if it is many days old. Even Online=false
        /// alone is insufficient: a recorded, later logout must also exist.
        /// Never issue an
        /// unrestricted DELETE FROM Presence.
        /// </summary>
        public List<HGStalePresence> CleanupConfirmedOrphans(int minimumAgeHours, int maxRows)
        {
            if (minimumAgeHours < 24 || minimumAgeHours > 24 * 365 ||
                maxRows < 1 || maxRows > 500)
                throw new ArgumentOutOfRangeException(nameof(minimumAgeHours),
                    "HG foreign presence cleanup requires 24..8760 hours and 1..500 rows.");

            // Table names cannot be parameterized. Reject unknown characters
            // instead of interpolating arbitrary deployment configuration.
            if (String.IsNullOrEmpty(m_Realm))
                throw new InvalidOperationException("Presence table is not configured.");
            foreach (char c in m_Realm)
                if (!Char.IsLetterOrDigit(c) && c != '_')
                    throw new InvalidOperationException("Invalid Presence table identifier.");

            string table = "`" + m_Realm + "`";
            const string foreign = @"EXISTS (
                SELECT 1 FROM `GridUser` AS foreignUser
                WHERE foreignUser.`UserID` LIKE CONCAT(p.`UserID`, ';%')
            )";
            const string noLocal = @"NOT EXISTS (
                SELECT 1 FROM `UserAccounts` AS localUser
                WHERE localUser.`PrincipalID` = p.`UserID`
            )";
            const string noOnlineForeign = @"NOT EXISTS (
                SELECT 1 FROM `GridUser` AS onlineUser
                WHERE onlineUser.`UserID` LIKE CONCAT(p.`UserID`, ';%')
                  AND LOWER(TRIM(onlineUser.`Online`)) = 'true'
            )";
            const string recordedForeignLogout = @"EXISTS (
                SELECT 1 FROM `GridUser` AS logoutUser
                WHERE logoutUser.`UserID` LIKE CONCAT(p.`UserID`, ';%')
                  AND LOWER(TRIM(logoutUser.`Online`)) = 'false'
                  AND CAST(logoutUser.`Logout` AS UNSIGNED) >
                      CAST(logoutUser.`Login` AS UNSIGNED)
                  AND CAST(logoutUser.`Logout` AS UNSIGNED) >= UNIX_TIMESTAMP(p.`LastSeen`)
            )";
            string predicate = $@"
                p.`LastSeen` < DATE_SUB(NOW(), INTERVAL ?MinAge HOUR)
                AND {foreign}
                AND {noLocal}
                AND (p.`RegionID` = ?EmptyRegion
                     OR ({noOnlineForeign} AND {recordedForeignLogout}))";

            var candidates = new List<(string User, UUID Session, UUID Region, DateTime Seen)>();
            // Use DB wall-clock for both read and conditional delete: the
            // timestamp field is maintained by the existing Presence schema.
            using (var connection = new MySqlConnection(m_connectionString))
            {
                connection.Open();
                using (var query = new MySqlCommand(
                    $@"SELECT p.`UserID`, p.`SessionID`, p.`RegionID`, p.`LastSeen`
                       FROM {table} AS p
                       WHERE {predicate}
                       ORDER BY p.`LastSeen` ASC LIMIT ?MaxRows", connection))
                {
                    query.Parameters.AddWithValue("?MinAge", minimumAgeHours);
                    query.Parameters.AddWithValue("?EmptyRegion", UUID.Zero.ToString());
                    query.Parameters.AddWithValue("?MaxRows", maxRows);
                    using (MySqlDataReader reader = query.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!UUID.TryParse(reader.GetString(1), out UUID session) ||
                                session == UUID.Zero ||
                                !UUID.TryParse(reader.GetString(2), out UUID region))
                                continue;
                            candidates.Add((reader.GetString(0), session, region,
                                reader.GetDateTime(3)));
                        }
                    }
                }

                var removed = new List<HGStalePresence>();
                foreach (var candidate in candidates)
                {
                    // Compare-and-delete: a fresh login, ReportAgent heartbeat,
                    // region switch or concurrent logout must abort deletion.
                    // Repeat the foreign/local/online predicates under the
                    // delete statement rather than trusting the earlier scan.
                    // Keep the p alias in correlated subqueries and DELETE.
                    using (var delete = new MySqlCommand(
                        $@"DELETE p FROM {table} AS p
                           WHERE p.`UserID` = ?UserID
                             AND p.`SessionID` = ?SessionID
                             AND p.`RegionID` = ?RegionID
                             AND p.`LastSeen` = ?LastSeen
                             AND {predicate}", connection))
                    {
                        delete.Parameters.AddWithValue("?UserID", candidate.User);
                        delete.Parameters.AddWithValue("?SessionID", candidate.Session.ToString());
                        delete.Parameters.AddWithValue("?RegionID", candidate.Region.ToString());
                        delete.Parameters.AddWithValue("?LastSeen", candidate.Seen);
                        delete.Parameters.AddWithValue("?MinAge", minimumAgeHours);
                        delete.Parameters.AddWithValue("?EmptyRegion", UUID.Zero.ToString());
                        if (delete.ExecuteNonQuery() == 1)
                            removed.Add(new HGStalePresence
                            {
                                UserID = candidate.User,
                                SessionID = candidate.Session
                            });
                    }
                }
                return removed;
            }
        }

        public int CountUnverifiedOnlineGuests(int minimumAgeHours)
        {
            if (minimumAgeHours < 24 || minimumAgeHours > 8760)
                throw new ArgumentOutOfRangeException(nameof(minimumAgeHours));
            foreach (char c in m_Realm)
                if (!Char.IsLetterOrDigit(c) && c != '_')
                    throw new InvalidOperationException("Invalid Presence table identifier.");

            // This is an AUDIT, not authorization to delete. Classic
            // LastSeen can remain unchanged during a long legitimate visit.
            using var db = new MySqlConnection(m_connectionString);
            db.Open();
            using var cmd = new MySqlCommand($@"
                SELECT COUNT(*) FROM `{m_Realm}` AS p
                WHERE p.`LastSeen` < DATE_SUB(NOW(), INTERVAL ?Age HOUR)
                  AND p.`RegionID` <> ?Empty
                  AND EXISTS (
                    SELECT 1 FROM `GridUser` AS g
                    WHERE g.`UserID` LIKE CONCAT(p.`UserID`, ';%')
                      AND LOWER(TRIM(g.`Online`)) = 'true')
                  AND NOT EXISTS (
                    SELECT 1 FROM `UserAccounts` AS a
                    WHERE a.`PrincipalID` = p.`UserID`)", db);
            cmd.Parameters.AddWithValue("?Age", minimumAgeHours);
            cmd.Parameters.AddWithValue("?Empty", UUID.Zero.ToString());
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool VerifyAgent(UUID agentId, UUID secureSessionID)
        {
            PresenceData[] ret = Get("SecureSessionID",
                    secureSessionID.ToString());

            if (ret.Length == 0)
                return false;

            if(ret[0].UserID != agentId.ToString())
                return false;

            return true;
        }
    }
}