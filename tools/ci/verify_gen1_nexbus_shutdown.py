#!/usr/bin/env python3
"""Regression guard for NexVerse NodeAgent close/inbound HTTP race."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
src = (root / "NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs").read_text(encoding="utf-8-sig")
start = src.index("        private void HandleInbound(")
end = src.index("        private static void WriteInbound(", start)
handler = src[start:end]
close = src.split("        public void Close()",1)[1].split("        public IDisposable Subscribe(",1)[0]

assert "private int m_Stopping;" in src
assert "Interlocked.Exchange(ref m_Stopping, 1)" in close
assert close.index("Interlocked.Exchange(ref m_Stopping, 1)") < close.index("m_Bus?.Dispose()")
assert 'Volatile.Read(ref m_Stopping) != 0 || !m_Enabled' in handler
assert 'DistributedNexEventBus bus = m_Bus;' in handler
assert 'Volatile.Read(ref m_Stopping) != 0 || bus == null' in handler
assert "bool accepted = bus.Receive(nexEvent);" in handler
assert "catch (ObjectDisposedException)" in handler
assert "HttpStatusCode.ServiceUnavailable" in handler
assert 'new { error = "node_shutting_down" }' in handler
receive_tail = handler.split("bool accepted = bus.Receive(nexEvent);", 1)[1]
assert receive_tail.index("catch (ObjectDisposedException)") < receive_tail.index("catch (Exception e)")
assert "NexBusProtocol.Verify(" in handler
assert handler.index("NexBusProtocol.Verify(") < handler.index("bus.Receive(nexEvent)")
assert 'HttpStatusCode.Unauthorized' in handler

print("NodeAgent shutdown: early 503, safe in-flight Receive, HMAC preserved")
