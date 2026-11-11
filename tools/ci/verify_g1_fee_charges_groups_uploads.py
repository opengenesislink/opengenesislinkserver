#!/usr/bin/env python3
"""Regression contracts for optional fee preflight of uploads and groups."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
money = (root/"NexVerse/RegionModules/Economy/NexVerseMoneyModule.cs").read_text()
modern = (root/"OpenSim/Addons/Groups/GroupsModule.cs").read_text()
legacy = (root/"OpenSim/Region/OptionalModules/Avatar/XmlRpcGroups/GroupsModule.cs").read_text()
interface = (root/"OpenSim/Framework/IMoneyModule.cs").read_text()
assert "public interface IMoneyChargePreflight" in interface

upload = money.split("public bool UploadCovered(", 1)[1].split("public bool AmountCovered(", 1)[0]
assert "TryPreflightCharge(agentID, amount, out _)" in upload
# Standard transfers and land purchases must not inherit fee-wallet restrictions.
funds = money.split("public bool AmountCovered(", 1)[1].split("public bool TryPreflightCharge(", 1)[0]
assert "GetBalance(agentID)" in funds
assert "m_FeeWalletId.IsZero()" not in funds

for label, source in (("new Groups",modern),("XmlRpcGroups",legacy)):
    creation = source.split("public UUID CreateGroup(",1)[1].split("public GroupNoticeData[] GroupNoticesListRequest(",1)[0]
    joining = source.split("public void JoinGroupRequest(",1)[1].split("public void LeaveGroupRequest(",1)[0]
    for name, block, call in (("creation",creation,"CreateGroup("),
                               ("join",joining,"AddAgentToGroup(")):
        assert "IMoneyChargePreflight preflight" in block, (label,name)
        assert "preflight.TryPreflightCharge(" in block, (label,name)
        assert "catch (Exception feeError)" in block, (label,name)
        assert block.index("preflight.TryPreflightCharge(") < block.index("m_groupData."+call), (label,name)
        assert block.index("m_groupData."+call) < block.index("money.ApplyCharge("), (label,name)
    assert "ledger reconciliation required" in creation or "reconcile ledger" in creation
    assert "ledger reconciliation required" in joining or "reconcile ledger" in joining

assert "if (groupRecord == null)" in legacy
print("G1 group and upload fee preflight contracts: PASS")
