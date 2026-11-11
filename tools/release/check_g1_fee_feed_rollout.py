#!/usr/bin/env python3
"""Read-only Generation 1 NV$ fee + OGL Feed pre-deployment config verifier.

Usage:
  python3 tools/release/check_g1_fee_feed_rollout.py --sim bin/OpenSim.ini
  python3 tools/release/check_g1_fee_feed_rollout.py --robust bin/Robust.HG.ini
  python3 tools/release/check_g1_fee_feed_rollout.py --sim ... --robust ...

No network calls, migrations, secrets output or changes to server files.
"""
import argparse
import configparser
import sys
import uuid
from pathlib import Path
from urllib.parse import urlparse


def parse(path: str) -> configparser.ConfigParser:
    data = configparser.ConfigParser(interpolation=None, strict=True)
    data.optionxform = str
    data.read_string(Path(path).read_text(encoding="utf-8").expandtabs(4), source=path)
    return data


def value(config: configparser.ConfigParser, section: str, key: str, default=""):
    return config.get(section, key, fallback=default).strip().strip('"')


def flag(config, section: str, key: str):
    return value(config, section, key, "false").casefold() in ("true", "yes", "1")


def valid_https(url: str) -> bool:
    parsed = urlparse(url)
    return parsed.scheme == "https" and bool(parsed.hostname) and not parsed.username


def inspect_sim(sim, ok, warn, fail):
    if not sim.has_section("NexEconomyViewer"):
        fail("Simulator lacks [NexEconomyViewer].")
        return
    if not flag(sim, "NexEconomyViewer", "Enabled"):
        warn("NV$ viewer adapter is disabled in this simulator configuration.")
        return
    url = value(sim, "NexEconomyViewer", "WorldApiBaseUrl")
    if not valid_https(url):
        fail("[NexEconomyViewer] WorldApiBaseUrl must be an HTTPS URL.")
    else:
        ok("NV$ uses an HTTPS World API endpoint.")
    raw = value(sim, "NexEconomyViewer", "FeeWalletId")
    charges = [
        value(sim, "NexEconomyViewer", "UploadCharge", "0"),
        value(sim, "NexEconomyViewer", "GroupCreationCharge", "0"),
    ]
    positive_fee = False
    for raw_charge in charges:
        try:
            n = int(raw_charge)
            if n < 0:
                fail("Negative configured NV$ system charge.")
            positive_fee = positive_fee or n > 0
        except ValueError:
            fail("Invalid configured NV$ system charge.")
    try:
        wallet = uuid.UUID(raw)
        if wallet.int == 0:
            raise ValueError("zero")
        ok("Nonzero NV$ fee wallet configured (provisioning must be checked in ledger).")
    except (ValueError, AttributeError, TypeError):
        if positive_fee:
            fail("Positive NV$ system charges but FeeWalletId is missing/invalid.")
        else:
            warn("FeeWalletId absent. Positive-priced Classifieds will be rejected until provisioned.")


def inspect_robust(config, ok, warn, fail):
    if not config.has_section("NexVerseWorldApi"):
        fail("ROBUST lacks [NexVerseWorldApi].")
        return
    if not flag(config, "NexVerseWorldApi", "EnableProfileFeed"):
        ok("OGL Profile Feed opt-in disabled (safe staging default).")
        return
    provider = value(config, "UserAccountService", "StorageProvider",
                     value(config, "DatabaseService", "StorageProvider"))
    connection = value(config, "UserAccountService", "ConnectionString",
                       value(config, "DatabaseService", "ConnectionString"))
    profiles = value(config, "UserProfilesService", "StorageProvider",
                     value(config, "DatabaseService", "StorageProvider"))
    pconnection = value(config, "UserProfilesService", "ConnectionString",
                        value(config, "DatabaseService", "ConnectionString"))
    if not provider or "mysql" not in provider.casefold() or (
            provider.casefold() != profiles.casefold() or connection != pconnection or
            not connection):
        fail("Feed MUST use the exact same MySQL/MariaDB provider and connection as UserAccountService.")
    else:
        ok("Feed and account deletion share a MySQL/MariaDB connection.")
    if not flag(config, "UserProfilesService", "Enabled"):
        fail("UserProfilesService must be enabled for feed.")
    try:
        days = int(value(config, "NexVerseWorldApi", "ProfileFeedRetentionDays", "180"))
        if days < 7 or days > 3650:
            fail("ProfileFeedRetentionDays must be between 7 and 3650.")
        else:
            ok(f"Feed retention set to {days} days.")
    except ValueError:
        fail("ProfileFeedRetentionDays must be an integer.")
    grid_url = value(config, "GridInfoService", "web_profile_url")
    if not grid_url or not valid_https(grid_url.replace("[AGENT_NAME]", "Example.Resident")):
        warn("[GridInfoService] web_profile_url is not an HTTPS feed URL; Viewer feed may stay empty.")
    else:
        ok("Viewer web_profile_url uses HTTPS.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sim", help="effective simulator OpenSim.ini")
    parser.add_argument("--robust", help="effective ROBUST/HG ini")
    args = parser.parse_args()
    if not (args.sim or args.robust):
        parser.error("Provide --sim and/or --robust.")
    errors = []
    def ok(message):
        print("OK:", message)
    def warn(message):
        print("WARN:", message)
    def fail(message):
        print("ERROR:", message)
        errors.append(message)

    for path, label, callback in (
        (args.sim, "Simulator", inspect_sim),
        (args.robust, "ROBUST", inspect_robust),
    ):
        if not path:
            continue
        try:
            cfg = parse(path)
            print(f"CHECK: {label} (configuration values/secrets are not printed)")
            callback(cfg, ok, warn, fail)
        except (OSError, UnicodeError, configparser.Error) as error:
            fail(f"{label} configuration cannot be read or parsed: {type(error).__name__}")
    print("RESULT:", "FAIL (no files changed)" if errors else "PASS (warnings still require review)")
    return 2 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
