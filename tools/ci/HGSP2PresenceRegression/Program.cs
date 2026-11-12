using System;
using MySql.Data.MySqlClient;
using OpenSim.Data;
using OpenSim.Data.MySQL;
using OpenMetaverse;

string connection = Environment.GetEnvironmentVariable("OGL_HGSP2_CI_MYSQL") ??
    throw new InvalidOperationException("Only an isolated CI database is permitted.");
if (!connection.Contains("Database=hgsp2_presence_ci", StringComparison.OrdinalIgnoreCase) ||
    !connection.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
    !connection.Contains("Port=19498", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("HGSP2 Presence test requires isolated local database 127.0.0.1:19498/hgsp2_presence_ci.");

static void Check(bool ok, string why) {
    if (!ok) throw new InvalidOperationException(why);
}

using (var conn = new MySqlConnection(connection)) {
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS `GridUser` (
        `UserID` VARCHAR(255) NOT NULL PRIMARY KEY, `Online` CHAR(5) NOT NULL DEFAULT 'false');
      CREATE TABLE IF NOT EXISTS `UserAccounts` (
        `PrincipalID` CHAR(36) NOT NULL PRIMARY KEY);";
    cmd.ExecuteNonQuery();
}

// Existing OpenSim provider migrates/creates Presence in this disposable DB.
var store = new MySQLPresenceData(connection, "Presence");
var entries = new Dictionary<string, (string User, string Session)>();
void Add(string label, bool gridOnline, bool foreign, bool local, bool zeroRegion, int ageHours,
    bool extraOnlineForSameAvatar = false)
{
    string user = Guid.NewGuid().ToString("D");
    string session = Guid.NewGuid().ToString("D");
    string region = zeroRegion ? UUID.Zero.ToString() : Guid.NewGuid().ToString("D");
    entries[label] = (user, session);
    using var conn = new MySqlConnection(connection);
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"INSERT INTO `Presence`
        (`UserID`,`RegionID`,`SessionID`,`SecureSessionID`,`LastSeen`)
        VALUES (?User,?Region,?Session,?Secure,DATE_SUB(NOW(), INTERVAL ?Hours HOUR))";
    cmd.Parameters.AddWithValue("?User", user);
    cmd.Parameters.AddWithValue("?Region", region);
    cmd.Parameters.AddWithValue("?Session", session);
    cmd.Parameters.AddWithValue("?Secure", Guid.NewGuid().ToString("D"));
    cmd.Parameters.AddWithValue("?Hours", ageHours);
    Check(cmd.ExecuteNonQuery() == 1, label + ": unable to seed Presence");
    if (foreign)
    {
        cmd.Parameters.Clear();
        cmd.CommandText = "INSERT INTO `GridUser` (`UserID`,`Online`) VALUES (?User,?Online)";
        cmd.Parameters.AddWithValue("?User", user + ";https://origin.example:8002;Visitor Test");
        cmd.Parameters.AddWithValue("?Online", gridOnline ? "true" : "false");
        Check(cmd.ExecuteNonQuery() == 1, "Unable to seed foreign GridUser");
        if (extraOnlineForSameAvatar)
        {
            cmd.Parameters.Clear();
            cmd.CommandText = "INSERT INTO `GridUser` (`UserID`,`Online`) VALUES (?User,'true')";
            cmd.Parameters.AddWithValue("?User", user + ";https://other.example:8002;Visitor Test");
            Check(cmd.ExecuteNonQuery() == 1, "Unable to seed second active GridUser");
        }
    }
    if (local)
    {
        cmd.Parameters.Clear();
        cmd.CommandText = "INSERT INTO `UserAccounts` (`PrincipalID`) VALUES (?Principal)";
        cmd.Parameters.AddWithValue("?Principal", user);
        Check(cmd.ExecuteNonQuery() == 1, "Unable to seed local collision");
    }
}

Add("foreign_offline_old", false, true, false, false, 100);
Add("foreign_incomplete_old", true, true, false, true, 110);
Add("foreign_active_long_visit", true, true, false, false, 200);
Add("foreign_offline_recent", false, true, false, false, 1);
Add("local_same_uuid_collision", false, true, true, false, 300);
Add("not_verified_foreign", false, false, false, false, 500);
Add("foreign_other_grid_online", false, true, false, false, 250, true);

bool RejectBadAge = false;
try { store.CleanupConfirmedOrphans(1, 40); }
catch (ArgumentOutOfRangeException) { RejectBadAge = true; }
Check(RejectBadAge, "unsafe cleanup age was accepted");
List<HGStalePresence> first = store.CleanupConfirmedOrphans(72, 1);
Check(first.Count == 1, "cleanup must respect batch size");
List<HGStalePresence> second = store.CleanupConfirmedOrphans(72, 25);
Check(second.Count == 1, "only second confirmed orphan may be removed");
HashSet<string> deleted = first.Concat(second).Select(x => x.SessionID.ToString()).ToHashSet();
Check(deleted.SetEquals(new [] {
    entries["foreign_offline_old"].Session,
    entries["foreign_incomplete_old"].Session,
}), "wrong sessions were deleted");
Check(store.CleanupConfirmedOrphans(72, 25).Count == 0, "cleanup is not idempotent");

using (var conn = new MySqlConnection(connection)) {
    conn.Open();
    foreach (var entry in entries)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM `Presence` WHERE `SessionID`=?Session";
        cmd.Parameters.AddWithValue("?Session", entry.Value.Session);
        long remaining = Convert.ToInt64(cmd.ExecuteScalar());
        Check(remaining == (deleted.Contains(entry.Value.Session) ? 0L : 1L),
            entry.Key + ": unsafe removal or orphan was not cleaned");
    }
}
Console.WriteLine("HGSP2 isolated MariaDB Presence cleanup PASS: stale guest removed; "+
    "online, recent, local, unclassified and duplicate-active identities preserved.");
