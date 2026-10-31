#!/usr/bin/env python3
"""Configure OpenGenesisLINK Gen1 IAR self-service for internal UGREEN WebDAV.

This tool changes only the specified local live INI files. It does not edit Git,
trigger server restarts, or attempt to discover credentials. It preserves a
timestamped copy of each modified INI file beside the original.
"""
import argparse
import getpass
import os
from pathlib import Path
import pwd
import re
import secrets
import stat
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from urllib.parse import urlsplit

SECTION_RE = re.compile(r"^\s*\[([^\]\r\n]+)\]\s*(?:[;#].*)?$")
KEY_RE = re.compile(r"^(\s*)([A-Za-z0-9_.-]+)(\s*=\s*)(.*?)(\r?\n)?$")


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--robust", type=Path, default=Path("/opt/robust/bin/Robust.HG.ini"),
                        help="Actual loaded ROBUST ini; omit with --only-sim")
    parser.add_argument("--sim", type=Path, default=Path("/opt/robust/bin/OpenSim.ini"),
                        help="Actual loaded simulator ini; omit with --only-robust")
    target = parser.add_mutually_exclusive_group()
    target.add_argument("--only-robust", action="store_true")
    target.add_argument("--only-sim", action="store_true")
    parser.add_argument("--url", required=True, help="Existing dedicated WebDAV archive folder URL")
    parser.add_argument("--username", default="ogl-iar", help="Restricted NAS WebDAV account")
    parser.add_argument("--runtime-user", default="root", help="Unix user running ROBUST and/or simulator")
    parser.add_argument("--secret", type=Path,
                        default=Path("/etc/opengenesislink/iar-webdav.pass"),
                        help="Separate password file, not a repository file")
    parser.add_argument("--ca-cert", type=Path, default=None,
                        help="Optional public NexVerse CA certificate to install into system trust")
    parser.add_argument("--restart-unit", action="append", default=[],
                        help="Explicit systemd service to restart after installation (repeatable)")
    return parser.parse_args()


def replace_section(text, section_name, replacements):
    lines = text.splitlines(keepends=True)
    indices = [i for i, line in enumerate(lines)
               if (match := SECTION_RE.match(line.rstrip('\r\n')))
               and match.group(1).strip().lower() == section_name.lower()]
    if len(indices) != 1:
        raise ValueError(f"Expected exactly one [{section_name}] section; found {len(indices)}")
    start = indices[0]
    end = next((i for i in range(start + 1, len(lines))
                if SECTION_RE.match(lines[i].rstrip('\r\n'))), len(lines))
    found = set()
    newline = '\r\n' if '\r\n' in text else '\n'
    for i in range(start + 1, end):
        match = KEY_RE.match(lines[i])
        if match and (key := match.group(2).lower()) in replacements:
            if key in found:
                raise ValueError(f"Duplicate key {key} in [{section_name}]; refusing ambiguous update")
            found.add(key)
            lines[i] = f"{match.group(1)}{match.group(2)}{match.group(3)}{replacements[key]}{newline}"
    missing = [f"    {key} = {value}{newline}" for key, value in replacements.items()
               if key not in found]
    lines[end:end] = missing
    return ''.join(lines)


def atomically_replace(path, data, original_stat):
    fd, temporary = tempfile.mkstemp(prefix=".ogl-iar-", dir=str(path.parent))
    try:
        with os.fdopen(fd, 'wb') as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(temporary, stat.S_IMODE(original_stat.st_mode))
        os.chown(temporary, original_stat.st_uid, original_stat.st_gid)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def create_secret(path, service_user):
    if not path.is_absolute() or path.is_symlink() or path.is_dir():
        raise ValueError("Secret path must be absolute, non-symlink, and a regular file")
    account = pwd.getpwnam(service_user)
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    if path.parent.is_symlink():
        raise ValueError("Secret directory must not be a symlink")
    os.chmod(path.parent, 0o700)
    if path.exists():
        if not path.is_file():
            raise ValueError("Existing secret is not a regular file")
        if path.stat().st_uid != account.pw_uid:
            raise ValueError("Existing secret owner does not match --runtime-user")
        if (stat.S_IMODE(path.stat().st_mode) & 0o077) != 0:
            raise ValueError("Existing secret is not owner-only (0600)")
        print(f"Reusing existing protected WebDAV secret: {path}")
        return
    if not sys.stdin.isatty():
        raise ValueError("A terminal is required for the initial NAS password entry")
    first = getpass.getpass("WebDAV NAS password (not saved in Git): ")
    second = getpass.getpass("Confirm WebDAV NAS password: ")
    if not first or first != second or any(c in first for c in '\r\n\x00'):
        raise ValueError("Password missing, contains invalid characters, or confirmation differs")
    # Use a new, owner-only file; never pass credentials in shell argv/history.
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, 'O_NOFOLLOW', 0), 0o600)
    try:
        with os.fdopen(fd, 'wb') as stream:
            os.fchown(stream.fileno(), account.pw_uid, account.pw_gid)
            os.fchmod(stream.fileno(), 0o600)
            stream.write(first.encode('utf-8'))
            stream.flush()
            os.fsync(stream.fileno())
    except BaseException:
        path.unlink(missing_ok=True)
        raise
    print(f"Saved WebDAV secret at {path} (0600, owner {service_user})")


def install_ca(cert_path):
    if cert_path is None:
        return
    if not cert_path.is_file() or cert_path.is_symlink():
        raise ValueError("CA certificate not found or is symlink")
    dest = Path('/usr/local/share/ca-certificates/nexverse-webdav-ca.crt')
    if dest.resolve() != cert_path.resolve():
        raw = cert_path.read_bytes()
        if b'BEGIN CERTIFICATE' not in raw or b'PRIVATE KEY' in raw:
            raise ValueError("Expected public PEM CA certificate, not a private key")
        dest.write_bytes(raw)
        dest.chmod(0o644)
    subprocess.run(['update-ca-certificates'], check=True)


def main():
    args = parse_args()
    if os.geteuid() != 0:
        raise PermissionError("Run as root to protect the secret and preserve config ownership")
    uri = urlsplit(args.url)
    if (uri.scheme != 'https' or uri.hostname != '10.78.0.4' or uri.port != 5006
            or uri.username or uri.password or uri.query or uri.fragment
            or not uri.path.rstrip('/').endswith('/IAR-Backups')
            or any(c in args.url for c in '\"\r\n') or '..' in uri.path):
        raise ValueError("URL must point to the dedicated internal NAS HTTPS WebDAV IAR folder")
    if not args.username or ':' in args.username or any(ch in args.username for ch in '\r\n\"'):
        raise ValueError("Invalid WebDAV username")
    if not args.secret.is_absolute() or Path('/etc/opengenesislink') not in args.secret.resolve().parents:
        raise ValueError("Secret must be stored in /etc/opengenesislink, not inside the Git repository")
    chosen = []
    if not args.only_sim:
        chosen.append((args.robust, 'OglIarSelfService', {
            'Enabled': 'true', 'StorageBackend': '"WebDAV"',
            'NasHost': '"10.78.0.4"', 'WebDavBaseUrl': f'"{args.url}"',
            'WebDavUsername': f'"{args.username}"',
            'WebDavPasswordFile': f'"{args.secret}"',
            'TemporaryStorageRoot': '"/var/lib/opengenesislink/iar-staging"',
            'RetentionDays': '30', 'MaximumArchiveMiB': '2048',
        }))
    if not args.only_robust:
        chosen.append((args.sim, 'OpenGenesisLINKIAR', {
            'Enabled': 'true', 'StorageBackend': '"WebDAV"',
            'NasHost': '"10.78.0.4"', 'WebDavBaseUrl': f'"{args.url}"',
            'WebDavUsername': f'"{args.username}"',
            'WebDavPasswordFile': f'"{args.secret}"',
            'MaximumArchiveMiB': '2048',
        }))
    if len({path.resolve() for path, _, _ in chosen}) != len(chosen):
        raise ValueError("ROBUST and simulator INI files cannot be the same file")
    plans = []
    for path, section, options in chosen:
        if not path.is_file() or path.is_symlink():
            raise ValueError(f"Live INI file missing or symlink: {path}")
        before = path.read_bytes()
        updated = replace_section(before.decode('utf-8-sig'), section,
                                  {k.lower(): v for k, v in options.items()})
        bom = b'\xef\xbb\xbf' if before.startswith(b'\xef\xbb\xbf') else b''
        plans.append((path, before, bom + updated.encode('utf-8'), path.stat()))
    install_ca(args.ca_cert)
    create_secret(args.secret, args.runtime_user)
    account = pwd.getpwnam(args.runtime_user)
    stage = Path('/var/lib/opengenesislink/iar-staging')
    if not args.only_sim:
        if stage.is_symlink():
            raise ValueError("Temporary storage must not be a symlink")
        stage.mkdir(mode=0o700, parents=True, exist_ok=True)
        os.chmod(stage, 0o700)
        os.chown(stage, account.pw_uid, account.pw_gid)
    backups = []
    try:
        for path, before, after, source_stat in plans:
            stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
            backup = path.with_name(path.name + f'.before-webdav-{stamp}-{secrets.token_hex(3)}.bak')
            # Never overwrite a previous backup.
            with backup.open('xb') as stream:
                stream.write(before)
            os.chown(backup, source_stat.st_uid, source_stat.st_gid)
            os.chmod(backup, stat.S_IMODE(source_stat.st_mode))
            backups.append((path, before, source_stat, backup))
            atomically_replace(path, after, source_stat)
            print(f"Updated {path}; backup {backup}")
    except BaseException:
        for path, before, source_stat, _ in reversed(backups):
            atomically_replace(path, before, source_stat)
        raise
    print("WebDAV is now configured in the selected live INI files.")
    if args.restart_unit:
        for unit in args.restart_unit:
            if not re.fullmatch(r'[A-Za-z0-9@_.-]+\.service', unit):
                raise ValueError(f"Invalid systemd unit: {unit}")
        for unit in args.restart_unit:
            subprocess.run(['systemctl', 'restart', unit], check=True)
            print(f"Restarted {unit}")
    else:
        print("Services not restarted; restart your actual ROBUST and simulator processes to load the new settings.")
    print("On additional simulator hosts repeat with --only-sim and their live OpenSim.ini.")


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, PermissionError, subprocess.CalledProcessError) as error:
        print(f"IAR WebDAV deployment failed: {error}", file=sys.stderr)
        sys.exit(1)
