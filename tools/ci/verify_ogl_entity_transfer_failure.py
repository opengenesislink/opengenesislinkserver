#!/usr/bin/env python3
"""Verify a disconnect cannot turn a failed CreateAgent into a hidden NRE."""
from pathlib import Path
s = (Path(__file__).resolve().parents[2] /
     "OpenSim/Region/CoreModules/Framework/EntityTransfer/EntityTransferModule.cs").read_text()
assert "private void SendTeleportFailureIfConnected(ScenePresence sp" in s
assert "client == null || sp.IsDeleted || !client.IsActive" in s
assert 'Original reason: {2}' in s
assert "m_log.WarnFormat(" in s
assert s.count("SendTeleportFailureIfConnected(sp, reason, finalDestination.RegionName);") == 2
assert s.count("CreateAgent rejected teleport of") == 2
assert "SendTeleportFailureIfConnected(sp, \"Internal error\", finalDestination.RegionName);" in s
assert "sp.IsInTransit = false;" in s
print("PASS: V1/V2 failed-agent creation preserves refusal reason and ignores disconnected viewer")
