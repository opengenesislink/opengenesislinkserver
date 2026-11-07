#!/usr/bin/env python3
"""Static integration checks; functional ledger tests run in NexEconomyPolicyRegression."""
from pathlib import Path

core = Path("NexVerse/Core/Economy/NexEconomyService.cs").read_text(encoding="utf-8")
login = Path("OpenSim/Services/LLLoginService/LLLoginService.cs").read_text(encoding="utf-8")
signal = Path("NexVerse/Core/Economy/NexResidentLoginEvents.cs").read_text(encoding="utf-8")
connector = Path("NexVerse/Server/Api/NexEconomyConnector.cs").read_text(encoding="utf-8")
prod = Path("bin/Robust.HG.ini").read_text(encoding="utf-8")
tests = Path("tools/ci/NexEconomyPolicyRegression/Program.cs").read_text(encoding="utf-8")

for required in (
    "CreditResidentDailyLoginBonus(",
    "NexLedgerAppendStatus.Created",
    "m_Ledger.GetTransaction(transactionId)",
    "DailyLoginTransactionId(residentId, previousDay)",
    "new NexLedgerPosting(",
    "new NexLedgerTransaction(",
    '["streak_day"]',
    "streakDay == 7 ? 60L : 10L",
    "m_Ledger.Post(transaction)",
    "RequireNotClosed(resident.AccountId",
):
    assert required in core, f"Missing ledger guarantee: {required}"

assert "NexResidentLoginEvents.NotifySuccessfulLocalLogin(" in login
assert login.index("NexResidentLoginEvents.NotifySuccessfulLocalLogin(") > login.index("LLLoginResponse response = new(")
assert "SuccessfulLocalLogin?.Invoke(" in signal
assert "SuccessfulLocalLogin += HandleSuccessfulLocalLogin" in connector
assert "TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, m_DailyLoginRewardZone)" in connector
assert "DailyLoginRewardEnabled = true" in prod
assert 'DailyLoginRewardTimezone = "Europe/Berlin"' in prod
assert "RunDailyLoginRewardRegression(economy, label);" in tests
print("OGL G1 authenticated NV$ login bonus guards: OK")
