#!/usr/bin/env python3
"""Guard smooth dynamic-texture refresh while preserving existing OSSL API and render path."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
def read(p):
    return (root / p).read_text(encoding="utf-8-sig")

module = read("OpenSim/Region/CoreModules/Scripting/DynamicTexture/DynamicTextureModule.cs")
retired = read("OpenSim/Region/CoreModules/Scripting/DynamicTexture/DynamicTextureRetirementQueue.cs")
proj = read("OpenSim/Region/CoreModules/OpenSim.Region.CoreModules.csproj")
defaults = read("bin/OpenSimDefaults.ini")
script_api = read("OpenSim/Region/ScriptEngine/Shared/Api/Implementation/OSSL_Api.cs")
ci = read(".github/workflows/nexjast-ci.yml")

for marker in (
    '"DynamicTexturePreviousRetentionSeconds", 0',
    '"DynamicTexturePreviousRetentionLimit", 256',
    'updater.RetirePreviousTexture = RetirePreviousTexture;',
    'RetirePreviousTexture(iac, oldID);',
    'm_RetirementTimer = new System.Threading.Timer(',
    'm_RetiredTextureQueue?.ExpireDue()',
    'pending?.Flush();',
    'iac.Cache(asset);',
    'UUID oldID = UpdatePart(part, asset.FullID);',
    'if (oldAsset.Temporary)',
):
    assert marker in module, "missing handoff guard: " + marker
assert module.index('iac.Cache(asset);') < module.index('UUID oldID = UpdatePart(part, asset.FullID);')

for marker in (
    'Queue<RetiredAsset>',
    'm_MaxPending',
    'DateTime.UtcNow',
    'CollectDue(now, false, due)',
    'm_Queue.Count >= m_MaxPending',
    'm_Queue.Dequeue()',
    'public void Flush()',
):
    assert marker in retired, "missing bounded queue guard: " + marker

assert 'Scripting\\DynamicTexture\\DynamicTextureRetirementQueue.cs' in proj
assert 'DynamicTexturePreviousRetentionSeconds = 0' in defaults
for config in ("bin/OpenSim.ini", "bin/OpenSim.ini.example"):
    body = read(config)
    assert 'DynamicTexturePreviousRetentionSeconds = 30' in body
    assert 'DynamicTexturePreviousRetentionLimit = 256' in body

assert 'public string osSetDynamicTextureDataFace(' in script_api
assert 'false, 3, 255, face' in script_api, "legacy OSSL API flags must remain unchanged"
assert "Regression-test dynamic texture retirement handoff" in ci
print("Generation 1 dynamic textures: bounded old-asset retention + legacy render path OK")
