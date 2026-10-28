#!/usr/bin/env python3
"""Static checks for opt-in wolfvoice integration and Radegast-ng policy."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
def read(p):
    return (root / p).read_text(encoding="utf-8-sig")

policy = read("OpenSim/Framework/OglApprovedViewerPolicy.cs")
tests = read("tools/ci/OglApprovedViewerRegression/Program.cs")
verify = read("tools/ci/verify_gen1_approved_viewer_access.py")
for item in ('"Firestorm"', '"OpenGenesisLINK"', '"OGL Viewer"',
             '"Radegast-ng"', '"Radegast"'):
    assert item in policy
assert "Check(\"Radegast-ng\", \"\", true);" in tests
assert "Check(\"2.3.0\", \"Radegast\", true);" in tests
assert "Radegast-ng" in verify

install = read("tools/voice/install-wolfvoice-region-addon.sh")
configure = read("tools/voice/configure-wolfvoice-region.sh")
doc = read("doc/NexVerse/GEN1_WOLFVOICE_COMPATIBILITY.md")

assert '7f08ce453fc5938beea12c452348d46b3ed1088e' in install
assert "intelligentwolf/os-webrtc-janus.git" in install
assert "git clone --no-checkout" in install
assert 'git -C "$ADDON_DIR" checkout --detach "$SOURCE_COMMIT"' in install
assert 'git -C "$ADDON_DIR" status --porcelain' in install
assert './runprebuild.sh' in install
assert "dotnet build --configuration Release OpenSim.sln" in install
for dll in ("WebRtcVoice.dll", "WebRtcVoiceRegionModule.dll", "WebRtcVoiceServiceModule.dll"):
    assert dll in install and dll in configure, dll

for marker in (
    "[OGLVoiceViewer]", "Enabled = false",
    "[WebRtcVoice]", "Enabled = true",
    "WebRtcVoice.dll:WebRtcVoiceServiceConnector",
    "RpcKeyFile = $RPC_KEY", "CredKeyFile = $CRED_KEY",
    "MessageDetails = false", "PositionFeed = true",
    "AcceptLegacyCallCredentials = false",
    "[VivoxVoice]", "enabled = false",
    "if [[ -e \"$TARGET\" ]]; then",
):
    assert marker in configure, marker

assert "wolfvoice" in doc and "os-webrtc-janus" in doc
assert "OGLVoiceViewer" in doc
assert "/addon-modules/os-webrtc-janus/" in read(".gitignore")
assert "OglVoiceFirestormCapsModule" in read("NexVerse/RegionModules/Voice/OglVoiceFirestormCapsModule.cs")
print("Wolfvoice optional integration + Radegast-ng aliases: source pin, CAPS separation, key checks OK")
