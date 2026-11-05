#!/usr/bin/env python3
"""Receive buffers are freed on pre-handoff errors, never twice after handoff."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
code = (root / "OpenSim/Region/ClientStack/Linden/UDP/OpenSimUDPBase.cs").read_text()
recv = code.split("private async void AsyncBeginReceive()", 1)[1].split(
    "public void SyncSend(", 1
)[0]
assert "bool handedOff = false;" in recv
assert "handedOff = true;" in recv
assert recv.index("handedOff = true;") < recv.index("PacketReceived(buf);")
assert "finally" in recv
assert "if (!handedOff)" in recv.split("finally", 1)[1]
assert "FreeUDPBuffer(buf);" in recv.split("finally", 1)[1]
assert recv.count("FreeUDPBuffer(buf);") == 1, "Do not double-return any receive buffer"
assert "catch (OperationCanceledException)" in recv
print("PASS: UDP receive buffers are returned on pre-handoff errors or cancellation")
