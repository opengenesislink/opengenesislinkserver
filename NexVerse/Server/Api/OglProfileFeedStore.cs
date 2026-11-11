// SPDX-License-Identifier: MPL-2.0
// Persistent, local-grid profile activity. No OpenSim inventory data is copied.
using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using OpenMetaverse;

namespace NexVerse.Server.Api
{
    internal sealed class OglProfileFeedStore
    {
        internal sealed class Post
        {
            public string Id { get; init; }
            public string AuthorId { get; init; }
            public string Text { get; init; }
            public long CreatedAt { get; init; }
            public int Likes { get; init; }
            public int Comments { get; init; }
        }

        internal sealed class Comment
        {
            public string Id { get; init; }
            public string AuthorId { get; init; }
            public string Text { get; init; }
            public long CreatedAt { get; init; }
        }

        private readonly string m_Connection;

        internal OglProfileFeedStore(string connection)
        {
            if (string.IsNullOrWhiteSpace(connection))
                throw new ArgumentException("Existing ROBUST MariaDB connection is required.");
            m_Connection = connection;
            using var db = Open();
            Execute(db, @"CREATE TABLE IF NOT EXISTS ogl_profile_feed_posts(
                id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
                owner CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                author CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                body VARCHAR(1500) NOT NULL, created_at BIGINT NOT NULL,
                INDEX ix_ogl_feed_owner (owner,created_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            Execute(db, @"CREATE TABLE IF NOT EXISTS ogl_profile_feed_comments(
                id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
                post_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                author CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                body VARCHAR(800) NOT NULL, created_at BIGINT NOT NULL,
                INDEX ix_ogl_feed_comments_post(post_id,created_at),
                INDEX ix_ogl_feed_comments_author(author,created_at),
                CONSTRAINT fk_ogl_feed_comment_post FOREIGN KEY(post_id)
                    REFERENCES ogl_profile_feed_posts(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            Execute(db, @"CREATE TABLE IF NOT EXISTS ogl_profile_feed_likes(
                post_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                actor CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                created_at BIGINT NOT NULL,
                PRIMARY KEY (post_id,actor),
                CONSTRAINT fk_ogl_feed_like_post FOREIGN KEY(post_id)
                    REFERENCES ogl_profile_feed_posts(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            Execute(db, @"CREATE TABLE IF NOT EXISTS ogl_profile_feed_follows(
                owner CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                follower CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                created_at BIGINT NOT NULL,
                PRIMARY KEY(owner,follower),
                INDEX ix_ogl_feed_follower(follower)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
        }

        private MySqlConnection Open()
        {
            var db = new MySqlConnection(m_Connection);
            db.Open();
            return db;
        }

        private static void Execute(MySqlConnection db, string sql)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static void Add(MySqlCommand c, string name, object value)
            => c.Parameters.AddWithValue(name, value);
        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        internal List<Post> ListPosts(UUID owner, int limit = 30)
        {
            var posts = new List<Post>();
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"SELECT p.id,p.author,p.body,p.created_at,
                (SELECT COUNT(*) FROM ogl_profile_feed_likes l WHERE l.post_id=p.id),
                (SELECT COUNT(*) FROM ogl_profile_feed_comments c WHERE c.post_id=p.id)
                FROM ogl_profile_feed_posts p WHERE p.owner=@owner
                ORDER BY p.created_at DESC,p.id DESC LIMIT @count";
            Add(cmd,"@owner",owner.ToString());
            Add(cmd,"@count",Math.Clamp(limit,1,50));
            using var rd = cmd.ExecuteReader();
            while(rd.Read())
                posts.Add(new Post { Id=rd.GetString(0), AuthorId=rd.GetString(1),
                    Text=rd.GetString(2), CreatedAt=rd.GetInt64(3),
                    Likes=Convert.ToInt32(rd.GetValue(4)), Comments=Convert.ToInt32(rd.GetValue(5)) });
            return posts;
        }

        internal List<Comment> ListComments(UUID post, int limit = 30)
        {
            var result = new List<Comment>();
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"SELECT id,author,body,created_at
                FROM ogl_profile_feed_comments WHERE post_id=@post
                ORDER BY created_at ASC,id ASC LIMIT @count";
            Add(cmd,"@post",post.ToString());
            Add(cmd,"@count",Math.Clamp(limit,1,50));
            using var rd=cmd.ExecuteReader();
            while(rd.Read())
                result.Add(new Comment { Id=rd.GetString(0), AuthorId=rd.GetString(1),
                    Text=rd.GetString(2), CreatedAt=rd.GetInt64(3) });
            return result;
        }

        internal bool IsPostOwned(UUID owner, UUID post)
        {
            using var db=Open();
            using var cmd=db.CreateCommand();
            cmd.CommandText="SELECT COUNT(*) FROM ogl_profile_feed_posts WHERE owner=@owner AND id=@post";
            Add(cmd,"@owner",owner.ToString());
            Add(cmd,"@post",post.ToString());
            return Convert.ToInt32(cmd.ExecuteScalar())==1;
        }

        internal bool CreatePost(UUID owner, string text, out UUID id)
        {
            id=UUID.Random();
            using var db=Open();
            using var cmd=db.CreateCommand();
            // Per-avatar 10 minute throttle and a daily cap.
            cmd.CommandText=@"INSERT INTO ogl_profile_feed_posts(id,owner,author,body,created_at)
                SELECT @id,@owner,@owner,@body,@now WHERE
                (SELECT COUNT(*) FROM ogl_profile_feed_posts WHERE owner=@owner AND created_at>=@minute)<3
                AND (SELECT COUNT(*) FROM ogl_profile_feed_posts WHERE owner=@owner AND created_at>=@day)<30";
            Add(cmd,"@id",id.ToString());
            Add(cmd,"@owner",owner.ToString());
            Add(cmd,"@body",text);
            Add(cmd,"@now",Now);
            Add(cmd,"@minute",Now-600);
            Add(cmd,"@day",Now-86400);
            return cmd.ExecuteNonQuery()==1;
        }

        internal bool DeletePost(UUID owner, UUID post)
        {
            using var db=Open();
            using var cmd=db.CreateCommand();
            cmd.CommandText="DELETE FROM ogl_profile_feed_posts WHERE id=@id AND owner=@owner";
            Add(cmd,"@id",post.ToString());Add(cmd,"@owner",owner.ToString());
            return cmd.ExecuteNonQuery()==1;
        }

        internal bool AddComment(UUID post, UUID actor, string text, out UUID id)
        {
            id=UUID.Random();
            using var db=Open();
            using var cmd=db.CreateCommand();
            cmd.CommandText=@"INSERT INTO ogl_profile_feed_comments(id,post_id,author,body,created_at)
                SELECT @id,@post,@actor,@body,@now WHERE
                (SELECT COUNT(*) FROM ogl_profile_feed_comments
                  WHERE author=@actor AND created_at>=@minute)<10
                AND EXISTS (SELECT 1 FROM ogl_profile_feed_posts WHERE id=@post)";
            Add(cmd,"@id",id.ToString());Add(cmd,"@post",post.ToString());
            Add(cmd,"@actor",actor.ToString());Add(cmd,"@body",text);
            Add(cmd,"@now",Now);Add(cmd,"@minute",Now-60);
            return cmd.ExecuteNonQuery()==1;
        }

        internal bool DeleteComment(UUID owner, UUID post, UUID comment, UUID actor)
        {
            using var db=Open();
            using var cmd=db.CreateCommand();
            cmd.CommandText=@"DELETE c FROM ogl_profile_feed_comments c
                INNER JOIN ogl_profile_feed_posts p ON p.id=c.post_id
                WHERE p.owner=@owner AND c.post_id=@post AND c.id=@comment
                AND (c.author=@actor OR p.owner=@actor)";
            Add(cmd,"@owner",owner.ToString());Add(cmd,"@post",post.ToString());
            Add(cmd,"@comment",comment.ToString());Add(cmd,"@actor",actor.ToString());
            return cmd.ExecuteNonQuery()==1;
        }

        internal void Like(UUID post, UUID actor, bool enabled)
        {
            using var db=Open();
            using var cmd=db.CreateCommand();
            cmd.CommandText=enabled
                ? @"INSERT IGNORE INTO ogl_profile_feed_likes(post_id,actor,created_at)
                    SELECT id,@actor,@now FROM ogl_profile_feed_posts WHERE id=@post"
                : "DELETE FROM ogl_profile_feed_likes WHERE post_id=@post AND actor=@actor";
            Add(cmd,"@post",post.ToString());Add(cmd,"@actor",actor.ToString());
            Add(cmd,"@now",Now);cmd.ExecuteNonQuery();
        }

        internal void Follow(UUID owner, UUID actor, bool enabled)
        {
            using var db=Open();
            using var cmd=db.CreateCommand();
            cmd.CommandText=enabled
                ? @"INSERT IGNORE INTO ogl_profile_feed_follows(owner,follower,created_at)
                    VALUES(@owner,@actor,@now)"
                : "DELETE FROM ogl_profile_feed_follows WHERE owner=@owner AND follower=@actor";
            Add(cmd,"@owner",owner.ToString());Add(cmd,"@actor",actor.ToString());
            Add(cmd,"@now",Now);cmd.ExecuteNonQuery();
        }

        internal int Followers(UUID owner)
        {
            using var db=Open();
            using var cmd=db.CreateCommand();
            cmd.CommandText="SELECT COUNT(*) FROM ogl_profile_feed_follows WHERE owner=@owner";
            Add(cmd,"@owner",owner.ToString());
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}
