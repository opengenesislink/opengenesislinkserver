#!/usr/bin/env python3
"""Ensure .NET 10 YEngine and generic helpers never activate BinaryFormatter."""
from pathlib import Path
import json
import re

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
assert "finally\n            {\n                // Unsafe/unsupported legacy state" in capture
assert "suspendOnCheckRunHold = false;" in capture
engine = (ROOT / "OpenSim/Region/ScriptEngine/YEngine/XMREngine.cs").read_text()
assert "doc = XMRInstance.ParsePersistedStateXml(xml)" in engine
assert "XMRInstance.WriteStateFileAtomically(statePath, scriptStateN.OuterXml)" in engine
assert "using (FileStream ss = File.Create(statePath))" not in engine
assert "Convert.FromBase64String(snapshotN.InnerText)" in engine
assert "restoredScriptTime.HasValue" in ctor
assert "DtdProcessing = DtdProcessing.Prohibit" in capture
assert "File.Move(temporary, statePath, overwrite: true)" in capture
generator = (ROOT / "Prebuild/src/Core/Targets/VSGenericTarget.cs").read_text()
assert '<EnableUnsafeBinaryFormatterSerialization>false</EnableUnsafeBinaryFormatterSerialization>' in generator
assert '<EnableUnsafeBinaryFormatterSerialization>true</EnableUnsafeBinaryFormatterSerialization>' not in generator
retarget = (ROOT / "tools/gen11/retarget_net10.py").read_text()
assert "UNSAFE_TRUE" in retarget and "UNSAFE_FALSE" in retarget
assert 'converted = source.replace(OLD, NEW).replace(UNSAFE_TRUE, UNSAFE_FALSE)' in retarget
runtime_paths = [
    ROOT / "OpenSim/Server/runtimeconfig.template.json",
    ROOT / "OpenSim/Region/Application/runtimeconfig.template.json",
    *sorted((ROOT / "bin").glob("*.runtimeconfig.json")),
]
for runtime_path in runtime_paths:
    config = json.loads(runtime_path.read_text())
    # runtimeconfig.template.json uses configProperties at the document root.
    # Built runtimeconfig.json puts the same fields under runtimeOptions.
    enabled = config.get("runtimeOptions", config).get("configProperties", {}).get(
        "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", False)
    assert enabled is not True, f"Unsafe BinaryFormatter runtime opt-in: {runtime_path}"
root_targets = (ROOT / "Directory.Build.targets").read_text()
assert '<EnableUnsafeBinaryFormatterSerialization>false</EnableUnsafeBinaryFormatterSerialization>' in root_targets
assert "'$(TargetFramework)' == 'net10.0'" in root_targets
server_targets = (ROOT / "OpenSim/Server/Directory.Build.targets").read_text()
assert "../../Directory.Build.targets" in server_targets
# All in-tree runtime C# must be free of active BinaryFormatter constructors.
# KeyframeMotion is an explicitly tracked exception while PR #396 stays
# unmerged; its removal is tested independently in that PR.
active_construction = re.compile(
    r"\bnew\s+(?:System\.Runtime\.Serialization\.Formatters\.Binary\.)?BinaryFormatter\s*\(")
legacy_keyframe = ROOT / "OpenSim/Region/Framework/Scenes/KeyframeMotion.cs"
for codefile in (ROOT / "OpenSim").rglob("*.cs"):
    # Ignore legacy comments, but never ignore executable constructors.
    source_without_comments = "\n".join(
        line.split("//", 1)[0] for line in codefile.read_text(errors="replace").splitlines())
    if active_construction.search(source_without_comments) and codefile != legacy_keyframe:
        raise AssertionError(f"Active BinaryFormatter runtime constructor: {codefile}")
print("PASS: no BinaryFormatter activation, unsafe build opt-ins or runtime opt-ins; state preserved")
