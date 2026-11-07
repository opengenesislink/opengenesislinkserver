#!/usr/bin/env python3
"""Ensure .NET 10 YEngine and generic helpers never activate BinaryFormatter."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
y = (ROOT / "OpenSim/Region/ScriptEngine/YEngine/XMRInstAbstract.cs").read_text()
codec = (ROOT / "OpenSim/Region/ScriptEngine/YEngine/ScriptStateExceptionCodec.cs").read_text()
util = (ROOT / "OpenSim/Framework/Util.cs").read_text()
ctor = (ROOT / "OpenSim/Region/ScriptEngine/YEngine/XMRInstCtor.cs").read_text()
capture = (ROOT / "OpenSim/Region/ScriptEngine/YEngine/XMRInstCapture.cs").read_text()
for name, src in [("YEngine", y), ("Codec", codec)]:
    assert "new BinaryFormatter(" not in src, name
    assert "new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter" not in src, name
    assert "formatter.Deserialize(" not in src and "bformatter.Deserialize(" not in src, name
assert "case Ser.SYSERIAL:\n                case Ser.THROWNEX:\n                    throw new InvalidDataException" in y
assert "SYSERIALSAFE" in y and "THROWNEXSAFE" in y
assert "ScriptStateExceptionCodec.Write(" in y
assert "ScriptStateExceptionCodec.Read(" in y
assert "Type.GetType(str, true)" not in y
assert "MaxMigrationDepth" in y and "MaxMigrationNodes" in y
assert "new BinaryFormatter(" not in util
assert "throw new NotSupportedException(" in util.split(
    "public static void SerializeToFile(", 1)[1].split("public static string Compress", 1)[0]
assert "File.Delete(m_StateFileName)" not in ctor.split("private void LoadInitialState()", 1)[1].split(
    "private static EventParams", 1)[0]
assert "ReadPersistedStateXmlFile(m_StateFileName)" in ctor
assert "WriteStateFileAtomically(m_StateFileName, scriptStateN.OuterXml)" in capture
assert "DtdProcessing = DtdProcessing.Prohibit" in capture
assert "File.Move(temporary, statePath, overwrite: true)" in capture
print("PASS: no BinaryFormatter activation in YEngine or Util, legacy state preserved")
