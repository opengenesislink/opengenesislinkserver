-- OpenGenesisLINK G1 / OGL Profile Feed v1
-- Run ONLY on the same MariaDB database as [UserAccountService].
-- Back up and test restore before applying. Never DROP existing data.
-- Idempotent bootstrap. These DDL statements mirror OglProfileFeedStore.

CREATE TABLE IF NOT EXISTS ogl_profile_feed_posts (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    owner CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    author CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    body VARCHAR(1500) NOT NULL,
    created_at BIGINT NOT NULL,
    INDEX ix_ogl_feed_owner (owner, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS ogl_profile_feed_comments (
    id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    post_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    author CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    body VARCHAR(800) NOT NULL,
    created_at BIGINT NOT NULL,
    INDEX ix_ogl_feed_comments_post (post_id, created_at),
    INDEX ix_ogl_feed_comments_author (author, created_at),
    CONSTRAINT fk_ogl_feed_comment_post FOREIGN KEY (post_id)
        REFERENCES ogl_profile_feed_posts(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS ogl_profile_feed_likes (
    post_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    actor CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    created_at BIGINT NOT NULL,
    PRIMARY KEY (post_id, actor),
    CONSTRAINT fk_ogl_feed_like_post FOREIGN KEY (post_id)
        REFERENCES ogl_profile_feed_posts(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS ogl_profile_feed_follows (
    owner CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    follower CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    created_at BIGINT NOT NULL,
    PRIMARY KEY (owner, follower),
    INDEX ix_ogl_feed_follower (follower)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Read-only postflight check. All four must exist, all ENGINE=InnoDB.
SELECT table_name, engine FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_name IN ('ogl_profile_feed_posts', 'ogl_profile_feed_comments',
                     'ogl_profile_feed_likes', 'ogl_profile_feed_follows')
ORDER BY table_name;
