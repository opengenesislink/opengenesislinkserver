#!/usr/bin/env python3
"""ACK packet batching must not allocate a packet after the final full batch."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = (root / "OpenSim/Region/ClientStack/Linden/UDP/LLUDPServer.cs").read_text()
method = source.split("public void SendAcks(LLUDPClient udpClient)", 1)[1].split(
    "static private readonly byte[] StartPingCheckHeader", 1
)[0]

assert "UDPPacketBuffer buf = null;" in method
loop = method.split("while (udpClient.PendingAcks.Dequeue(out uint ack))", 1)[1]
assert "if (count == 0)" in loop, "Allocate a packet only when an ACK arrives"
assert "buf = GetNewUDPBuffer(udpClient.RemoteEndPoint);" in loop.split(
    "Utils.UIntToBytes(ack, data, pos)", 1
)[0]
flush = loop.split("if (count == 255)", 1)[1].split("if (count > 0)", 1)[0]
assert "SendUDPPacket(udpClient, buf, ThrottleOutPacketType.Unknown);" in flush
assert "GetNewUDPBuffer" not in flush, "Do not allocate a packet after sending the last full ACK batch"
assert "count = 0;" in flush
assert "data[10] = (byte)count;" in loop
print("PASS: ACK batching uses lazy buffer allocation for exact 255-item multiples")
