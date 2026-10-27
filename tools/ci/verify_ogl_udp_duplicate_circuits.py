#!/usr/bin/env python3
"""Guard against double-UseCircuitCode races and null-circuit logins.

The real repro is two authorized handshakes for the same agent/circuit
from distinct UDP source ports at the same instant, followed by a
CloseAgent/removal during Scene.AddNewAgent. This is a source-level
regression guard; live viewer/NAT/concurrency verification is separate.
"""
from pathlib import Path

root=Path(__file__).resolve().parents[2]
scene=(root/"OpenSim/Region/Framework/Scenes/Scene.cs").read_text(encoding="utf-8")
udp=(root/"OpenSim/Region/ClientStack/Linden/UDP/LLUDPServer.cs").read_text(encoding="utf-8")

new_agent=scene.split("public override ISceneAgent AddNewAgent(",1)[1].split(
    "public string GetAgentHomeURI(",1)[0]
handle=udp.split("protected void HandleUseCircuitCode(object o)",1)[1].split(
    "Send an ack immediately",1)[0]
add_client=udp.split("protected virtual IClientAPI AddClient(",1)[1].split(
    "Deactivates the client",1)[0]

# Scene must refuse stale circuits before it tries lock(null), and
# must balance the user-logging-in counter for that rejection.
lookup="m_authenticateHandler.GetAgentCircuitData(client.CircuitCode)"
guard="if (aCircuit is null)"
lock="lock (aCircuit)"
assert new_agent.index(lookup)<new_agent.index(guard)<new_agent.index(lock)
reject=new_agent[new_agent.index(guard):new_agent.index(lock)]
assert "StatsReporter.UpdateUsersLoggingIn(false)" in reject
assert "return null;" in reject

# Circuit authorization and a second lookup are not atomic: a first
# handshake may close the connection before the second packet gets here.
lookup="m_circuitManager.GetAgentCircuitData(uccp.CircuitCode.Code)"
ip="aCircuit.IPAddress"
start="client = AddClient("
assert handle.index(lookup)<handle.index(guard)<handle.index(ip)<handle.index(start)
reject=handle[handle.index(guard):handle.index(ip)]
assert 'm_pendingCache.Remove(endPoint)' in reject and "return;" in reject

# Keep old active client when a duplicate comes from another source
# port of the same authorized session. Only a new/different session
# may go through Scene.CloseAgent.
for term in ("client.CircuitCode == circuitCode","client.SessionId == sessionID",
             "SecureSessionId.Equals(sessionInfo.LoginInfo.SecureSession)"):
    assert term in add_client,term
dupe="if (client.RemoteEndPoint.Equals(remoteEndPoint))"
assert dupe in add_client
before_close=add_client[add_client.index(dupe):add_client.index("Scene.CloseAgent(agentID, true)")]
assert "return client;" in before_close
assert "different UDP endpoint" in before_close
assert "return null;" in before_close

# A circuit removed after the first handle lookup may still disappear
# before Scene.AddNewAgent. Never advertise an unregistered UDP client.
client_start=add_client.split("client.Start();",1)[1].split("return client;",1)[0]
assert "client.SceneAgent is null" in client_start
assert "client.Close(false, true)" in client_start
assert "return null;" in client_start

print("OGL duplicate UDP circuit guards: source regression PASS")
