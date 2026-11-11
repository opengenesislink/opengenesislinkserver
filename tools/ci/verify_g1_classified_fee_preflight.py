#!/usr/bin/env python3
"""Source regression for paid-classified preflight and non-atomic payment handling."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
contract = (root / "OpenSim/Framework/IMoneyModule.cs").read_text()
money = (root / "NexVerse/RegionModules/Economy/NexVerseMoneyModule.cs").read_text()
profile = (root / "OpenSim/Region/CoreModules/Avatar/UserProfiles/UserProfileModule.cs").read_text()

assert "public interface IMoneyChargePreflight" in contract
assert "TryPreflightCharge(UUID agentID, int amount, out string reason)" in contract
assert "IMoneyChargePreflight," in money
preflight = money.split("public bool TryPreflightCharge(", 1)[1].split("public void ApplyCharge(", 1)[0]
for required in ("if (amount < 0)", "if (amount == 0)",
                 "m_FeeWalletId.IsZero()", "AmountCovered(agentID, amount)"):
    assert required in preflight, required

classified = profile.split("public void ClassifiedInfoUpdate(", 1)[1].split(
    "public void ClassifiedDelete(", 1)[0]
for required in ("if (queryclassifiedPrice < 0)",
                 "money is IMoneyChargePreflight preflight",
                 "preflight.TryPreflightCharge(",
                 '"classified_update"',
                 "catch (Exception feeException)",
                 "ledger/manual reconciliation required"):
    assert required in classified, required
assert classified.index("preflight.TryPreflightCharge(") < classified.index('"classified_update"')
assert classified.index('"classified_update"') < classified.index("money.ApplyCharge(")
assert "Never retry an ambiguous debit" in classified
assert "NoMoney" not in classified
print("G1 classified fee preflight contract OK")
