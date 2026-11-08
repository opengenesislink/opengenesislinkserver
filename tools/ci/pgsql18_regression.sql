-- PostgreSQL 18 SQL-level regression smoke test for G1 profile and estate fixes.
-- This does not replace C# data-service integration tests or migration testing.
BEGIN;

CREATE TEMP TABLE classifieds (classifieduuid uuid PRIMARY KEY, name text NOT NULL);
CREATE TEMP TABLE userpicks (pickuuid uuid PRIMARY KEY, name text NOT NULL);
CREATE TEMP TABLE usernotes (useruuid uuid NOT NULL, targetuuid uuid NOT NULL, notes text NOT NULL, UNIQUE (useruuid, targetuuid));
CREATE TEMP TABLE usersettings (useruuid uuid PRIMARY KEY, imviaemail boolean NOT NULL, visible boolean NOT NULL, email varchar(254) NOT NULL);
CREATE TEMP TABLE userdata ("UserId" uuid NOT NULL, "TagId" text NOT NULL, "DataKey" text, "DataVal" text, PRIMARY KEY ("UserId", "TagId"));
CREATE TEMP TABLE estateban ("EstateID" integer NOT NULL, "bannedUUID" uuid NOT NULL, "banningUUID" uuid, "banTime" integer);
CREATE TEMP TABLE estate_managers ("EstateID" integer NOT NULL, uuid uuid NOT NULL);

INSERT INTO classifieds VALUES ('00000000-0000-0000-0000-000000000001', 'first'), ('00000000-0000-0000-0000-000000000002', 'second');
WITH upsert AS (
    UPDATE classifieds SET name = 'updated' WHERE classifieduuid = '00000000-0000-0000-0000-000000000001' RETURNING *
) INSERT INTO classifieds (classifieduuid, name)
  SELECT '00000000-0000-0000-0000-000000000001', 'updated'
  WHERE NOT EXISTS (SELECT * FROM upsert);
INSERT INTO userpicks VALUES ('00000000-0000-0000-0000-000000000001', 'first'), ('00000000-0000-0000-0000-000000000002', 'second');
WITH upsert AS (
    UPDATE userpicks SET name = 'updated' WHERE pickuuid = '00000000-0000-0000-0000-000000000001' RETURNING *
) INSERT INTO userpicks (pickuuid, name)
  SELECT '00000000-0000-0000-0000-000000000001', 'updated'
  WHERE NOT EXISTS (SELECT * FROM upsert);

INSERT INTO usernotes VALUES ('00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000003', 'old');
INSERT INTO usernotes VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000003', 'untouched');
INSERT INTO usernotes (notes, useruuid, targetuuid)
VALUES ('updated', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000003')
ON CONFLICT (useruuid, targetuuid) DO UPDATE SET notes = EXCLUDED.notes;

INSERT INTO usersettings (useruuid, imviaemail, visible, email)
VALUES ('00000000-0000-0000-0000-000000000001', false, false, '')
ON CONFLICT (useruuid) DO NOTHING;
INSERT INTO usersettings (useruuid, imviaemail, visible, email)
VALUES ('00000000-0000-0000-0000-000000000001', false, false, '')
ON CONFLICT (useruuid) DO NOTHING;
INSERT INTO userdata ("UserId", "TagId", "DataKey", "DataVal")
VALUES ('00000000-0000-0000-0000-000000000001', 'test', '', '')
ON CONFLICT ("UserId", "TagId") DO NOTHING;
INSERT INTO userdata ("UserId", "TagId", "DataKey", "DataVal")
VALUES ('00000000-0000-0000-0000-000000000001', 'test', '', '')
ON CONFLICT ("UserId", "TagId") DO NOTHING;

INSERT INTO estateban ("EstateID", "bannedUUID")
VALUES (7, '00000000-0000-0000-0000-000000000001');
INSERT INTO estate_managers ("EstateID", uuid)
VALUES (7, '00000000-0000-0000-0000-000000000001');
DO $tests$
BEGIN
    -- Catching a failing insert rolls back the deletion in the same subtransaction.
    BEGIN
        DELETE FROM estateban WHERE "EstateID" = 7;
        INSERT INTO estateban ("EstateID", "bannedUUID") VALUES (7, NULL);
    EXCEPTION WHEN not_null_violation THEN
        NULL;
    END;
    BEGIN
        DELETE FROM estate_managers WHERE "EstateID" = 7;
        INSERT INTO estate_managers ("EstateID", uuid) VALUES (7, NULL);
    EXCEPTION WHEN not_null_violation THEN
        NULL;
    END;
    IF (SELECT name FROM classifieds WHERE classifieduuid = '00000000-0000-0000-0000-000000000002') <> 'second'
       OR (SELECT name FROM userpicks WHERE pickuuid = '00000000-0000-0000-0000-000000000002') <> 'second'
       OR (SELECT notes FROM usernotes WHERE useruuid = '00000000-0000-0000-0000-000000000002') <> 'untouched'
       OR (SELECT notes FROM usernotes WHERE useruuid = '00000000-0000-0000-0000-000000000001') <> 'updated'
       OR (SELECT COUNT(*) FROM usersettings) <> 1
       OR (SELECT COUNT(*) FROM userdata) <> 1
       OR (SELECT COUNT(*) FROM estateban) <> 1
       OR (SELECT COUNT(*) FROM estate_managers) <> 1 THEN
        RAISE EXCEPTION 'PostgreSQL 18 regression smoke check failed';
    END IF;
END
$tests$;

ROLLBACK;
