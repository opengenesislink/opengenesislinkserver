#!/usr/bin/env python3
"""Guard PostgreSQL inventory IN-query code against value interpolation."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = (root / "OpenSim/Data/PGSQL/PGSQLGenericTableHandler.cs").read_text()
method = source.split("public virtual T[] Get(string field, string[] keys)", 1)[1].split(
    "public virtual T[] Get(string[] fields, string[] keys)", 1
)[0]
assert 'field.Replace("\\\"", "\\\"\\\"")' in method, "SQL identifier must be escaped"
assert 'sb.Append(\':\').Append(parameterName)' in method
assert "cmd.Parameters.Add(m_database.CreateParameter(parameterName, keys[i]" in method
assert "sb.Append(keys[i])" not in method, "Untrusted keys must not be interpolated"
assert "IN ('" not in method, "Query must use placeholders, not string literals"
print("PASS: PostgreSQL multi-key inventory query binds values")
