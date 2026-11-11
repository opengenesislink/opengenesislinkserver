// SPDX-License-Identifier: MPL-2.0
// Integration test: real MariaDB, real OglProfileFeedStore, concurrent writes
// and the SQL contract of account deletion; isolated synthetic local avatars.
using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using NexVerse.Server.Api;
using OpenMetaverse;

string connection = Environment.GetEnvironmentVariable("NEX_FEED_MARIADB");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("NEX_FEED_MARIADB is required.");

using var bootstrap = new MySqlConnection(connection);
bootstrap.Open();
// CI only. Reuse the ephemeral test schema, but never replace a preexisting
// UserAccounts table belonging to another test suite.
bool createdSyntheticUsers = Convert.ToInt32(Scalar(bootstrap, null,
    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='UserAccounts'")) == 0;
Exec(bootstrap, null, @"CREATE TABLE IF NOT EXISTS `UserAccounts` (
    `PrincipalID` CHAR(36) NOT NULL PRIMARY KEY
) ENGINE=InnoDB DEFAULT CHARSET=ascii");

UUID alice = UUID.Random();
UUID bob = UUID.Random();
Exec(bootstrap, null, "INSERT INTO `UserAccounts` (`PrincipalID`) VALUES (@id)",
    ("@id", alice.ToString()));
Exec(bootstrap, null, "INSERT INTO `UserAccounts` (`PrincipalID`) VALUES (@id)",
    ("@id", bob.ToString()));

OglProfileFeedStore store = new OglProfileFeedStore(connection, 180);
try
{
    Check(store.CreatePost(alice, "Alice first", out UUID post),
        "own post failed");
    Check(store.AddComment(post, bob, "Bob comment", out _),
        "comment failed");
    UUID moderator = UUID.Random();
    Check(store.AddComment(post, bob, "Moderation test", out UUID moderationComment),
        "moderation fixture failed");
    Check(!store.DeleteComment(alice, post, moderationComment, moderator),
        "unprivileged visitor deleted another avatar's comment");
    Check(store.DeleteComment(alice, post, moderationComment, moderator, true),
        "explicitly authorized moderation failed");
    Check(store.Like(post, bob, true), "like failed");
    Check(store.Follow(alice, bob, true), "follow failed");
    Check(store.ListPosts(alice).Count == 1, "own post not visible");
    Check(store.ListPosts(alice)[0].Likes == 1 &&
          store.ListPosts(alice)[0].Comments == 1 &&
          store.Followers(alice) == 1, "interactions missing");

    // Lock Bob's account in the delete transaction, then race a new post
    // from another independent connection. Once deleted, the second writer
    // cannot create a ghost row for the missing account.
    using (var delete = new MySqlConnection(connection))
    {
        delete.Open();
        using var tx = delete.BeginTransaction();
        Check(Scalar(delete, tx, @"SELECT 1 FROM `UserAccounts`
            WHERE `PrincipalID`=@id FOR UPDATE", ("@id", bob.ToString())) != null,
            "Bob account missing");

        var race = Task.Run(() => store.CreatePost(bob, "racing post", out _));
        await Task.Delay(200);
        PurgeAvatar(delete, tx, bob);
        tx.Commit();
        Check(!await race.WaitAsync(TimeSpan.FromSeconds(15)),
            "deleted avatar was allowed to post");
    }

    Check(store.ListComments(post).Count == 0,
        "deleted avatar comment survived");
    Check(store.ListPosts(alice)[0].Likes == 0,
        "deleted avatar like survived");
    Check(store.Followers(alice) == 0,
        "deleted avatar follow survived");
    Check(!store.CreatePost(bob, "ghost", out _),
        "deleted avatar wrote another post");
    Check(!store.Follow(alice, bob, true),
        "deleted avatar followed another resident");

    // Retention is enforced by read even if the periodic sweep is delayed.
    long expired = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 181L * 86400;
    Exec(bootstrap, null,
        "UPDATE ogl_profile_feed_posts SET created_at=@expired WHERE id=@id",
        ("@expired", expired), ("@id", post.ToString()));
    Check(store.ListPosts(alice).Count == 0,
        "old content exposed past retention");
    store.PruneExpired();
    Check(Scalar(bootstrap, null,
        "SELECT COUNT(*) FROM ogl_profile_feed_posts WHERE id=@id",
        ("@id", post.ToString())).ToString() == "0",
        "retention purge did not physically delete post");

    // Current posts remain valid. Deleting their owner cascades correctly.
    Check(store.CreatePost(alice, "Second post", out UUID second),
        "second post failed");
    using (var delete = new MySqlConnection(connection))
    {
        delete.Open();
        using var tx = delete.BeginTransaction();
        Check(Scalar(delete, tx, @"SELECT 1 FROM `UserAccounts`
            WHERE `PrincipalID`=@id FOR UPDATE", ("@id", alice.ToString())) != null,
            "Alice account missing");
        PurgeAvatar(delete, tx, alice);
        tx.Commit();
    }
    Check(!store.IsPostOwned(alice, second),
        "deleted account's content survived");
    Check(!store.CreatePost(alice, "ghost", out _),
        "deleted owner could post");
    Console.WriteLine("OGL Profile Feed MariaDB deletion, lock and retention: PASS");
}
finally
{
    // Idempotent teardown for unexpected test failures on an ephemeral CI DB.
    Exec(bootstrap, null, "DELETE FROM ogl_profile_feed_comments WHERE author IN (@a,@b)",
        ("@a", alice.ToString()), ("@b", bob.ToString()));
    Exec(bootstrap, null, "DELETE FROM ogl_profile_feed_likes WHERE actor IN (@a,@b)",
        ("@a", alice.ToString()), ("@b", bob.ToString()));
    Exec(bootstrap, null, "DELETE FROM ogl_profile_feed_follows WHERE owner IN (@a,@b) OR follower IN (@a,@b)",
        ("@a", alice.ToString()), ("@b", bob.ToString()));
    Exec(bootstrap, null, "DELETE FROM ogl_profile_feed_posts WHERE owner IN (@a,@b)",
        ("@a", alice.ToString()), ("@b", bob.ToString()));
    Exec(bootstrap, null, "DELETE FROM `UserAccounts` WHERE `PrincipalID` IN (@a,@b)",
        ("@a", alice.ToString()), ("@b", bob.ToString()));
    if (createdSyntheticUsers)
        Exec(bootstrap, null, "DROP TABLE `UserAccounts`");
}

static void PurgeAvatar(MySqlConnection db, MySqlTransaction tx, UUID id)
{
    // All writes share the same transaction, mirroring the production
    // PurgeLocalProfileFeed and account row deletion contract.
    string p = id.ToString();
    Exec(db, tx, "DELETE FROM ogl_profile_feed_comments WHERE author=@id", ("@id", p));
    Exec(db, tx, "DELETE FROM ogl_profile_feed_likes WHERE actor=@id", ("@id", p));
    Exec(db, tx, "DELETE FROM ogl_profile_feed_follows WHERE owner=@id OR follower=@id", ("@id", p));
    Exec(db, tx, "DELETE FROM ogl_profile_feed_posts WHERE owner=@id OR author=@id", ("@id", p));
    Exec(db, tx, "DELETE FROM `UserAccounts` WHERE `PrincipalID`=@id", ("@id", p));
}

static object Scalar(MySqlConnection db, MySqlTransaction tx, string sql,
    params (string key, object value)[] parameters)
{
    using var cmd = db.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = sql;
    foreach (var parameter in parameters)
        cmd.Parameters.AddWithValue(parameter.key, parameter.value);
    return cmd.ExecuteScalar();
}

static void Exec(MySqlConnection db, MySqlTransaction tx, string sql,
    params (string key, object value)[] parameters)
{
    using var cmd = db.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = sql;
    foreach (var parameter in parameters)
        cmd.Parameters.AddWithValue(parameter.key, parameter.value);
    cmd.ExecuteNonQuery();
}

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException("OGL Feed MariaDB: " + message);
}
