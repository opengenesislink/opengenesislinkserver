#!/usr/bin/env python3
"""Fail-closed, evidence-backed OpenGenesisLINK 0.9.3.13 Stable gate.

Runs after operator tests; does not execute tests and cannot confer approval.
Only accepts the 30 mandatory criteria from 09313_RELEASE_ACCEPTANCE.md.
"""
import argparse
import json
import re
import sys
from pathlib import Path

REQUIRED = (
    ["C%02d" % n for n in range(1, 6)]
    + ["A%02d" % n for n in range(1, 10)]
    + ["M%02d" % n for n in range(1, 8)]
    + ["V%02d" % n for n in range(1, 5)]
    + ["R%02d" % n for n in range(1, 6)]
)


def validate(data, expected_sha):
    problems = []
    if not isinstance(data, dict):
        return ["Abnahme-JSON muss ein Objekt sein."]
    if data.get("schema") != "ogl-09313-1" or data.get("release") != "0.9.3.13":
        problems.append("Falsche Schema-/Releasekennung.")
    meta = data.get("meta")
    if not isinstance(meta, dict):
        return problems + ["Metaangaben fehlen."]
    sha = str(meta.get("sha", "")).strip().lower()
    if not re.fullmatch(r"[a-f0-9]{40}", expected_sha.lower()):
        problems.append("Die erwartete Git-SHA ist ungueltig.")
    if sha != expected_sha.lower():
        problems.append("Abnahme-SHA weicht von der exakten Release-SHA ab.")
    if meta.get("approval") != "approved":
        problems.append("Ausdrueckliche Betreiberfreigabe fehlt.")
    for key in ("operator", "testEnv", "versions", "ci", "backup", "date"):
        val = meta.get(key, "")
        if not isinstance(val, str) or not val.strip():
            problems.append("Fehlende Betreiber-/Abnahmeangabe: " + key)
    gates = data.get("gates")
    if not isinstance(gates, dict):
        return problems + ["Einzelne Gate-Ergebnisse fehlen."]
    for gate_id in REQUIRED:
        gate = gates.get(gate_id)
        if not isinstance(gate, dict):
            problems.append(gate_id + ": nicht dokumentiert")
            continue
        if gate.get("status") != "PASS":
            problems.append(gate_id + ": nicht bestanden")
        evidence = gate.get("evidence", "")
        if not isinstance(evidence, str) or len(evidence.strip()) < 8:
            problems.append(gate_id + ": Beleg/Referenz fehlt")
    # Date, evidence links and operator signatures still need separate external
    # review. No automated validator can prove that live tests happened.
    return problems


def self_test():
    sample = {
        "schema": "ogl-09313-1", "release": "0.9.3.13",
        "meta": {
            "sha": "a" * 40, "approval": "approved",
            "operator": "Release operator", "testEnv": "isolated staging",
            "versions": "OGL 0.9.3.13 RC1, Firestorm 7.2.4",
            "ci": "https://github.com/example/actions/runs/1",
            "backup": "snapshot ID 123", "date": "2026-10-27"
        },
        "gates": {g: {"status": "PASS", "evidence": "Evidence URI / run log"} for g in REQUIRED}
    }
    assert not validate(sample, "a" * 40)
    sample["gates"]["A04"]["status"] = "PARTIAL"
    assert any("A04" in p for p in validate(sample, "a" * 40))
    sample["gates"]["A04"]["status"] = "PASS"
    sample["meta"]["sha"] = "b" * 40
    assert any("Abnahme-SHA" in p for p in validate(sample, "a" * 40))
    sample["meta"]["sha"] = "a" * 40
    sample["gates"]["V01"]["evidence"] = ""
    assert any("V01" in p for p in validate(sample, "a" * 40))
    sample["gates"]["V01"]["evidence"] = "Evidence URI / run log"
    sample["meta"]["approval"] = "pending"
    assert validate(sample, "a" * 40)
    print("09313 acceptance gate: positive/negative/fail-closed fixtures PASS")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", type=Path, help="Operator-exported JSON evidence bundle")
    parser.add_argument("--sha", help="Exact Git SHA for the release candidate")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return 0
    if not args.json or not args.sha:
        parser.error("--json and --sha must be provided")
    try:
        obj = json.loads(args.json.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        print("BLOCKED: Abnahme-JSON nicht lesbar: " + str(exc), file=sys.stderr)
        return 2
    errors = validate(obj, args.sha)
    if errors:
        print("BLOCKED: OpenGenesisLINK 0.9.3.13 Stable nicht freigegeben:", file=sys.stderr)
        for err in errors:
            print(" - " + err, file=sys.stderr)
        return 1
    print("Dokumentarische 30/30-Gates-Pruefung PASS auf", args.sha)
    print("Zusatzpruefung durch Betreiber: Nachweise, CI und Live-Tests verifizieren.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
