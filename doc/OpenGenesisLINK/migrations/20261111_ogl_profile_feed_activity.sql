-- OpenGenesisLINK G1, opt-in auto activity / Feed v2+
-- Existing UserAccountService and UserProfilesService must share a single
-- MariaDB database. Import after a verified backup, with ROBUST stopped
-- or Feed disabled during migration. CREATE IF NOT EXISTS preserves data.

CREATE TABLE IF NOT EXISTS ogl_profile_feed_activity_settings (
    owner CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    auto_events TINYINT(1) NOT NULL DEFAULT 0,
    auto_classifieds TINYINT(1) NOT NULL DEFAULT 0,
    updated_at BIGINT NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS ogl_profile_feed_activity_receipts (
    owner CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    activity_kind VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    reference_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    post_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    created_at BIGINT NOT NULL,
    PRIMARY KEY (owner, activity_kind, reference_id),
    INDEX ix_ogl_feed_activity_time (owner, created_at),
    CONSTRAINT fk_ogl_feed_activity_post FOREIGN KEY (post_id)
        REFERENCES ogl_profile_feed_posts(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Both tables should exist with InnoDB. Never drop them during rollback:
-- consent and receipt history are needed to avoid reposting.
SELECT table_name, engine FROM information_schema.tables
WHERE table_schema=DATABASE() AND table_name IN
    ('ogl_profile_feed_activity_settings',
     'ogl_profile_feed_activity_receipts')
ORDER BY table_name;
