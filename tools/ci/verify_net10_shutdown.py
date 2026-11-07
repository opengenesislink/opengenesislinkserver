#!/usr/bin/env python3
"""Safety invariants for G1 shutdown and .NET10 persistence."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
scenes = (root / "OpenSim/Region/Framework/Scenes/SceneManager.cs").read_text()
base = (root / "OpenSim/Region/Application/OpenSimBase.cs").read_text()
udp = (root / "OpenSim/Region/ClientStack/Linden/UDP/UnackedPacketCollection.cs").read_text()
keyframes = (root / "OpenSim/Region/Framework/Scenes/KeyframeMotion.cs").read_text()
assert "new BinaryFormatter(" not in keyframes
assert "OglLooksLikeLegacyBinaryFormatter" in keyframes
assert "m_oglLegacyPayload" in keyframes
assert "OglReadCount" in keyframes
assert "closeThread.Join(TimeSpan.FromSeconds(regionCloseTimeoutSeconds))" in scenes
assert "IsBackground = true" in scenes
assert "scene.Close();" in scenes
assert "plugin.Dispose()" in base
assert "SceneManager.Close(regionCloseTimeoutSeconds)" in base
background = (root / "OpenSim/Region/Application/OpenSimBackground.cs").read_text()
shutdown = background.split("public override void Shutdown()", 1)[1]
assert shutdown.index("base.Shutdown();") < shutdown.index("WorldHasComeToAnEnd.Set();")
assert "m_pendingAdds = null;" not in udp
assert "m_pendingAcknowledgements = null;" not in udp
assert "m_pendingRemoves = null;" not in udp
print("PASS: shutdown isolation, byte-only keyframe migration and ACK queue lifetime")
