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
assert "try\n            {\n                if (m_timerStopped)" in keyframes
assert "finally\n            {\n                Monitor.Exit(m_frames);" in keyframes
assert "closeThread.Join(TimeSpan.FromSeconds(regionCloseTimeoutSeconds))" in scenes
assert "IsBackground = true" in scenes
assert "scene.Close();" in scenes
assert "plugin.Dispose()" in base
assert "SceneManager.Close(regionCloseTimeoutSeconds)" in base
assert "if (!SceneManager.Close(regionCloseTimeoutSeconds))" in base
assert "ShutdownExitCode = 1;" in base
assert "return failed == 0;" in scenes
server = (root / "OpenSim/Framework/Servers/BaseOpenSimServer.cs").read_text()
assert "Environment.Exit(ShutdownExitCode)" in server
host = (root / "OpenSim/Framework/Servers/ServerBase.cs").read_text()
app = (root / "OpenSim/Region/Application/Application.cs").read_text()
assert "public bool ShutdownRequested" in host
assert "while (!m_sim.ShutdownRequested)" in app
background = (root / "OpenSim/Region/Application/OpenSimBackground.cs").read_text()
shutdown = background.split("public override void Shutdown()", 1)[1]
assert shutdown.index("base.Shutdown();") < shutdown.index("WorldHasComeToAnEnd.Set();")
assert "m_pendingAdds = null;" not in udp
assert "m_pendingAcknowledgements = null;" not in udp
assert "m_pendingRemoves = null;" not in udp
converter = (root / "tools/migration/KeyframeLegacyConverter/Program.cs").read_text()
assert "NrbfDecoder.DecodeClassRecord(" in converter
assert "new BinaryFormatter(" not in converter
assert "FileMode.CreateNew" in converter
assert "KeyframeMotion.FromData(null, converted)" in converter
assert "ConvertBatch(" in converter
assert "SHA256.HashData(original)" in converter
assert "File.Move(temporary, output)" in converter
print("PASS: shutdown isolation, byte-only keyframe migration and ACK queue lifetime")
