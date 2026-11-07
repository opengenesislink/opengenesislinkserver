# OGL KeyframeMotion: migration from BinaryFormatter to OGKF v1

## Safety and scope
OpenGenesisLINK Generation 1 on .NET 10 writes a bounded typed OGKF v1
record instead of BinaryFormatter. Historic BinaryFormatter payloads are
preserved byte-for-byte but intentionally remain inactive until migration.
Never turn on unsafe .NET BinaryFormatter deserialization in the simulator.

The offline `KeyframeLegacyConverter` uses Microsoft's data-only NRBF
reader; it does **not** instantiate arbitrary serialized types. It accepts
only the historical OpenGenesisLINK/OpenSim KeyframeMotion shape and writes
a new file using `FileMode.CreateNew`. It does not connect to a database,
replace objects, read passwords or change any in-world data.

Before migration, **stop the affected region**, back up the region database
and any OAR exports. Keep the original BLOB and record UUID available for
rollback. Never perform automatic mass updates on an untested production DB.

## Detect legacy records (read-only)
MariaDB/MySQL:
```sql
SELECT UUID, OCTET_LENGTH(KeyframeMotion) AS bytes,
       HEX(SUBSTRING(KeyframeMotion, 1, 5)) AS header
FROM prims
WHERE KeyframeMotion IS NOT NULL
  AND OCTET_LENGTH(KeyframeMotion) > 0;
```

SQLite:
```sql
SELECT UUID, length(KeyframeMotion) AS bytes,
       hex(substr(KeyframeMotion, 1, 5)) AS header
FROM prims
WHERE KeyframeMotion IS NOT NULL AND length(KeyframeMotion) > 0;
```

Legacy values generally start with `0001000000`; OGKF starts with
`4F474B4601`. A header alone does not prove a valid record.

## Convert an exported single BLOB (no database writes)
```bash
dotnet run --project tools/migration/KeyframeLegacyConverter/KeyframeLegacyConverter.csproj \
  -c Release -- legacy-keyframe.bin converted-keyframe.ogkf
```
The command rejects existing output files, malformed payloads,
non-finite float components, unknown types and oversized arrays. The
converter then validates the result using the actual .NET 10
`KeyframeMotion.FromData` implementation before saving.

Do not import converted bytes into live tables until they have passed
a staging restore and object-motion verification. Compare each object's
original blob hash and UUID in a transaction; use an explicit rollback
plan. Historical schema variants may require additional decoding rules;
failed records are not overwritten.

## CI
`OGL .NET 10 Persistence and Shutdown Gate` generates a synthetic
legacy record using isolated .NET 8 test code, converts it without
BinaryFormatter in .NET 10 and checks OGKF restore. This is not a
substitute for testing a backed-up real-world movement record.

## Batch migration from a read-only MariaDB / MySQL export

Export the UUID and HEX blob columns using a **read-only database account**
against a **stopped region / consistent backup**, keeping the original
database and the exported file for rollback:

```sql
SELECT UUID, HEX(KeyframeMotion)
FROM prims
WHERE KeyframeMotion IS NOT NULL
  AND OCTET_LENGTH(KeyframeMotion) >= 9
  AND HEX(SUBSTRING(KeyframeMotion, 1, 5)) = '0001000000';
```

Store the two-column result as UTF-8 **tab-delimited text without headers**
(`uuid<TAB>blobhex`), for example `legacy-keyframes.tsv`. Never paste
database credentials into command logs or commit the export to GitHub.

```bash
dotnet run --project tools/migration/KeyframeLegacyConverter/KeyframeLegacyConverter.csproj \
  -c Release -- --batch-hex legacy-keyframes.tsv converted-keyframes.tsv
```

The batch converter processes at most 100,000 rows, rejects malformed,
duplicate or unsupported rows, checks **each result** against the actual
.NET 10 keyframe reader and writes a three-column report:
`UUID<TAB>SHA256(original BLOB)<TAB>HEX(new OGKF BLOB)`.

The batch report is created atomically only after **every row passes**;
it does not connect to SQL, issue UPDATE statements, replace existing
files, or modify live region data. The SHA-256 is a verification guard for
an eventual operator-reviewed per-row update on a **staging restore**.

Do not apply updates based on UUID alone. Check the original per-object
BLOB digest in the same transaction and verify the number of updated rows.
Not every historical BinaryFormatter variant is necessarily supported.
