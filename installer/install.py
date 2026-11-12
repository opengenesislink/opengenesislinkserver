#!/usr/bin/env python3
"""Interactive first-time installer for OpenGenesisLINK Generation 1 (Linux x64).

Never downloads main/dev builds, never touches an existing installation.
The release package is required to contain the generic config/runtime files.
"""
import getpass
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import secrets
import shutil
import stat
import subprocess
import sys
import tempfile
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile

REPO = "opengenesislink/opengenesislinkserver"
TAG = "v0.9.4.0"
ZIP = "OpenGenesisLINK-Generation1-0.9.4.0-Linux-x64.zip"
ROOT = Path("/opt/opengenesislink")
ETC = Path("/etc/opengenesislink")
USER = "opengenesislink"
UA = {"User-Agent": "OpenGenesisLINK-Gen1-Installer/1.0", "Accept": "application/vnd.github+json"}
REQUIRED = (
    "OpenSim.dll", "Robust.dll", "OpenSim.runtimeconfig.json",
    "Robust.runtimeconfig.json", "OpenSimDefaults.ini",
    "OpenSim.ini.example", "Robust.HG.ini.example",
    "config-include/GridHypergrid.ini",
    "config-include/GridCommon.ini.example",
    "config-include/FlotsamCache.ini",
    "config-include/osslDefaultEnable.ini",
)


def abort(message):
    raise RuntimeError(message)


def command(*args, input=None, check=True):
    print("+", " ".join(map(str, args)))
    return subprocess.run([str(x) for x in args], input=input, text=True, check=check)


def ask(label, default=None, validator=None, secret=False):
    suffix = f" [{default}]" if default is not None and not secret else ""
    while True:
        value = (getpass.getpass(f"{label}{suffix}: ") if secret
                 else input(f"{label}{suffix}: ")).strip()
        if not value and default is not None:
            value = default
        if value and (validator is None or validator(value)):
            return value
        print("Ungueltige Eingabe. Bitte erneut versuchen.")


def yes(label, default=False):
    mark = "J/n" if default else "j/N"
    value = input(f"{label} [{mark}]: ").strip().lower()
    return default if not value else value in ("j", "ja", "y", "yes")


def safe_name(value):
    return re.fullmatch(r"[A-Za-z0-9_]{2,48}", value) is not None


def safe_host(value):
    return (len(value) <= 253 and
            re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._:-]*", value) is not None and
            ".." not in value and not value.startswith("-"))


def safe_label(value):
    return len(value) <= 80 and not re.search(r'[\r\n"=;]', value)


def safe_port(value):
    return value.isdecimal() and 1024 <= int(value) <= 65535


def put_ini(path, section, key, value):
    """Edit an effective INI key while preserving unrelated keys and comments."""
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    start = next((i for i, line in enumerate(lines)
                  if re.fullmatch(r"\s*\[" + re.escape(section) + r"\]\s*", line, re.I)), None)
    if start is None:
        lines.extend(["", f"[{section}]", f"    {key} = {value}"])
    else:
        end = next((i for i in range(start + 1, len(lines))
                    if re.match(r"^\s*\[", lines[i])), len(lines))
        key_re = re.compile(r"^\s*" + re.escape(key) + r"\s*=", re.I)
        matching = [i for i in range(start + 1, end) if key_re.match(lines[i])]
        if matching:
            lines[matching[0]] = f"    {key} = {value}"
            for i in reversed(matching[1:]):
                lines.pop(i)
        else:
            lines.insert(end, f"    {key} = {value}")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def remove_ini(path, section, key):
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    section_seen = False
    result = []
    for line in lines:
        header = re.match(r"^\s*\[([^\]]+)\]", line)
        if header:
            section_seen = header.group(1).lower() == section.lower()
        if section_seen and re.match(r"^\s*" + re.escape(key) + r"\s*=", line, re.I):
            continue
        result.append(line)
    path.write_text("\n".join(result) + "\n", encoding="utf-8")


def fetch_bytes(url):
    if urllib.parse.urlsplit(url).scheme != "https":
        abort("Nur HTTPS-Downloads sind erlaubt.")
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=90) as r:
        return r.read()


def fetch_release(work):
    url = f"https://api.github.com/repos/{REPO}/releases/tags/{TAG}"
    try:
        release = json.loads(fetch_bytes(url))
    except urllib.error.HTTPError as e:
        if e.code == 404:
            abort("Das offizielle Stable-Release v0.9.4.0 ist noch nicht auf GitHub "
                  "veroeffentlicht. Keine Entwicklungsversion wird installiert.")
        raise
    if release.get("draft") or release.get("prerelease") or release.get("tag_name") != TAG:
        abort("Release-Status ist nicht Stable.")
    assets = {a["name"]: a for a in release.get("assets", [])}
    if ZIP not in assets or ZIP + ".sha256" not in assets:
        abort("Stable-Release hat kein Linux-x64 ZIP mit SHA256-Datei.")
    checksums = fetch_bytes(assets[ZIP + ".sha256"]["browser_download_url"]).decode()
    match = re.search(r"(?im)^([a-f0-9]{64})\s+\*?" + re.escape(ZIP) + r"\s*$", checksums)
    if not match:
        abort("Ungueltige SHA256-Datei des offiziellen Releases.")
    package = work / ZIP
    print(f"\nLade offizielles Stable-Release {TAG} von GitHub ...")
    req = urllib.request.Request(assets[ZIP]["browser_download_url"], headers=UA)
    sha = hashlib.sha256()
    with urllib.request.urlopen(req, timeout=180) as src, package.open("wb") as out:
        while True:
            chunk = src.read(1024 * 1024)
            if not chunk:
                break
            sha.update(chunk)
            out.write(chunk)
    if sha.hexdigest().lower() != match.group(1).lower():
        abort("SHA256-Fehler: Download wird NICHT installiert.")
    asset_digest = assets[ZIP].get("digest", "")
    if asset_digest and asset_digest != "sha256:" + sha.hexdigest():
        abort("GitHub-Asset-Digest stimmt nicht mit dem Download ueberein.")
    return package


def unpack_verified(package, work):
    dest = work / "unpacked"
    dest.mkdir()
    total = 0
    with zipfile.ZipFile(package) as z:
        for entry in z.infolist():
            pp = PurePosixPath(entry.filename)
            mode = (entry.external_attr >> 16)
            if (entry.filename.startswith("/") or ".." in pp.parts or
                "\\" in entry.filename or stat.S_ISLNK(mode)):
                abort("Unsicherer ZIP-Pfad (Traversal/Symlink): " + entry.filename)
            total += entry.file_size
            if total > 2_000_000_000:
                abort("Release-ZIP enthaelt zu viele unkomprimierte Daten.")
        z.extractall(dest)
    pkg = dest / "OpenGenesisLINK-Generation1"
    manifest = json.loads((pkg / "BUILD-MANIFEST.json").read_text())
    if (manifest.get("channel") != "generation1-stable" or
        manifest.get("stable_release") is not True or
        manifest.get("version") != "0.9.4.0" or
        manifest.get("runtime") != ".NET 10" or
        manifest.get("platform") != "linux-x64" or
        not re.fullmatch(r"[0-9a-f]{40}", manifest.get("source_commit", ""))):
        abort("Release-Manifest nicht fuer Generation 1 Stable Linux-x64 freigegeben.")
    for filename in REQUIRED:
        if not (pkg / "bin" / filename).is_file():
            abort("Fehlende Release-Datei: bin/" + filename +
                  ". Die Release-Pipeline muss die generischen Konfigurationen mitliefern.")
    for target in ("OpenSim", "Robust"):
        runtime = json.loads((pkg / "bin" / (target + ".runtimeconfig.json")).read_text())
        if runtime["runtimeOptions"].get("tfm") != "net10.0":
            abort(target + " ist kein .NET-10-Build.")
    return pkg


def preflight():
    if os.geteuid() != 0:
        abort("Bitte als root ausfuehren: sudo bash <(wget -qO- https://install.opengenesislink.de)")
    if os.uname().machine != "x86_64":
        abort("Dieser Installer unterstuetzt derzeit nur Linux x86_64.")
    if not Path("/run/systemd/system").exists():
        abort("systemd ist erforderlich.")
    values = {}
    for raw in Path("/etc/os-release").read_text().splitlines():
        if "=" in raw:
            k, v = raw.split("=", 1)
            values[k] = v.strip('"')
    os_id, version = values.get("ID"), values.get("VERSION_ID")
    if (os_id == "debian" and version not in ("12", "13")) or (
        os_id == "ubuntu" and version not in ("22.04", "24.04", "26.04")) or (
        os_id not in ("debian", "ubuntu")):
        abort("Unterstuetzt: Debian 12/13 sowie Ubuntu 22.04/24.04/26.04.")
    if ROOT.exists() or any(Path("/etc/systemd/system", name).exists()
                            for name in ("opengenesislink-robust.service",
                                         "opengenesislink-simulator.service")):
        abort("Bereits installierte Daten/Dienste gefunden. Der Erstinstaller ueberschreibt nichts.")
    return os_id, version


def collect():
    print("\nOpenGenesisLINK Generation 1 - Einrichtungsassistent\n")
    print("1) ROBUST (Grid-/Login-/Hypergrid-Dienste)")
    print("2) Simulator (Regionenserver)")
    print("3) ROBUST + Simulator auf diesem Server [empfohlen zum Einstieg]")
    mode = ask("Installationsart", "3", lambda v: v in ("1", "2", "3"))
    robust = mode in ("1", "3")
    simulator = mode in ("2", "3")
    grid = ask("Grid-Name", "Mein OpenGenesisLINK Grid", safe_label)
    nick = ask("Kurzname (nur Buchstaben/Zahlen/_)", "MeinGrid", safe_name)
    public_host = ask("Oeffentlicher ROBUST-Hostname oder IP", "grid.example.org", safe_host)
    public_port = int(ask("Oeffentlicher ROBUST-Port", "8002", safe_port))
    public_url = f"http://{public_host}:{public_port}"
    if simulator and not robust:
        private_url = ask("Interne ROBUST-Adresse inkl. Port (nur vertrauenswuerdiges Netz)",
                          "http://10.0.0.10:8003",
                          lambda v: valid_private_url(v))
    else:
        private_url = "http://127.0.0.1:8003"
    region_host = ""
    region_name = ""
    region_port = 9000
    if simulator:
        region_name = ask("Name der ersten Region", "Welcome", safe_label)
        region_host = ask("Oeffentlicher Regions-Hostname oder IP", public_host, safe_host)
        region_port = int(ask("Regionsport (UDP, auch Simulator-HTTP)", "9000", safe_port))
    local_db = yes("Lokale MariaDB automatisch installieren und einrichten?", True)
    if local_db:
        dbhost, dbname, dbuser = "127.0.0.1", "opengenesislink", "ogl"
        dbpass = secrets.token_urlsafe(27)
    else:
        print("Die Datenbank muss bereits existieren; TLS fuer Remote-DB wird erzwungen.")
        dbhost = ask("MariaDB-Hostname/IP", validator=safe_host)
        dbname = ask("Datenbankname", "opengenesislink", safe_name)
        dbuser = ask("Datenbankbenutzer", "ogl", safe_name)
        dbpass = ask("Datenbankpasswort (ohne ;, Anfuehrungszeichen oder Leerzeichen)",
                     validator=lambda v: len(v) >= 12 and re.fullmatch(r"[A-Za-z0-9._@%+!~#-]+", v) is not None,
                     secret=True)
    print("\nKonfigurationsuebersicht:")
    print(f"  Rollen: {mode}  | Grid: {grid} | Login/HG: {public_url}")
    if simulator:
        print(f"  Region: {region_name} @ {region_host}:{region_port}/UDP")
    print(f"  Datenbank: {dbhost}/{dbname} (Benutzer: {dbuser})")
    print("  Ziel: /opt/opengenesislink (neue Installation, keine Updates)")
    if not yes("Installation verbindlich starten?"):
        abort("Auf Wunsch abgebrochen.")
    return locals()


def valid_private_url(value):
    try:
        p = urllib.parse.urlsplit(value)
        return (p.scheme in ("http", "https") and p.hostname and
                safe_host(p.hostname) and 1 <= p.port <= 65535 and
                not p.username and not p.password and not p.path and
                not p.query and not p.fragment)
    except ValueError:
        return False


def packages(os_id, version, local_db):
    command("apt-get", "update")
    deps = ["ca-certificates", "screen", "openssl", "libgdiplus", "libopenjp2-7"]
    if local_db:
        deps.append("mariadb-server")
    command("apt-get", "install", "-y", *deps)
    runtime_ok = False
    if shutil.which("dotnet"):
        runtime_ok = "Microsoft.NETCore.App 10." in subprocess.run(
            ["dotnet", "--list-runtimes"], text=True, capture_output=True).stdout
    if not runtime_ok:
        if os_id == "debian":
            with tempfile.TemporaryDirectory() as td:
                pkg = Path(td) / "packages-microsoft-prod.deb"
                pkg.write_bytes(fetch_bytes(
                    f"https://packages.microsoft.com/config/debian/{version}/packages-microsoft-prod.deb"))
                command("dpkg", "-i", pkg)
            command("apt-get", "update")
        command("apt-get", "install", "-y", "dotnet-runtime-10.0")
    if not shutil.which("dotnet") or "Microsoft.NETCore.App 10." not in subprocess.run(
        ["dotnet", "--list-runtimes"], text=True, capture_output=True).stdout:
        abort("Keine funktionsfaehige .NET-10-Runtime gefunden.")
    if local_db:
        command("systemctl", "enable", "--now", "mariadb")


def db_setup(cfg):
    if not cfg["local_db"]:
        return
    # Generated password is urlsafe and single-quote-free; all identifiers are fixed.
    pw = cfg["dbpass"]
    sql = (
        "CREATE DATABASE `opengenesislink` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;\n"
        f"CREATE USER IF NOT EXISTS 'ogl'@'localhost' IDENTIFIED BY '{pw}';\n"
        f"CREATE USER 'ogl'@'127.0.0.1' IDENTIFIED BY '{pw}';\n"
        "GRANT ALL PRIVILEGES ON `opengenesislink`.* TO 'ogl'@'localhost';\n"
        "GRANT ALL PRIVILEGES ON `opengenesislink`.* TO 'ogl'@'127.0.0.1';\n"
        "FLUSH PRIVILEGES;\n"
    )
    # Never reuse or modify an existing grid database/account by accident.
    guard = ("SELECT COUNT(*) FROM information_schema.SCHEMATA "
             "WHERE SCHEMA_NAME='opengenesislink';")
    result = subprocess.run(["mariadb", "-N", "-B", "-u", "root", "-e", guard],
                            check=True, capture_output=True, text=True)
    if result.stdout.strip() != "0":
        abort("Lokale DB opengenesislink existiert bereits. "
              "Bitte vorhandene Daten manuell sichern/konfigurieren.")
    guard_user = "SELECT COUNT(*) FROM mysql.user WHERE User='ogl';"
    result = subprocess.run(["mariadb", "-N", "-B", "-u", "root", "-e", guard_user],
                            check=True, capture_output=True, text=True)
    if result.stdout.strip() != "0":
        abort("MariaDB-Benutzer ogl existiert bereits. Abbruch ohne Passwortaenderung.")
    command("mariadb", "-u", "root", input=sql)


def write_env(role, cfg):
    ETC.mkdir(mode=0o700, parents=True, exist_ok=True)
    path = ETC / (role + ".env")
    lines = [
        "NEXVERSE_DB_PASSWORD=" + cfg["dbpass"],
        "NEXVERSE_NATIVE_TOKEN_SIGNING_KEY=" + secrets.token_urlsafe(48),
    ]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    os.chmod(path, 0o600)


def conn_string(cfg):
    tls = "None" if cfg["local_db"] else "Required"
    return (f'"Data Source={cfg["dbhost"]};Database={cfg["dbname"]};'
            f'User ID={cfg["dbuser"]};Password=${{Environment|NEXVERSE_DB_PASSWORD}};'
            f'Old Guids=true;SslMode={tls};"')


def keypair(robust_bin, sim_bin):
    if robust_bin:
        folder = robust_bin / "config-include"
        private = folder / "ogl-feature-private.pem"
        public = folder / "ogl-feature-public.pem"
        command("openssl", "ecparam", "-name", "prime256v1", "-genkey", "-noout", "-out", private)
        command("openssl", "pkey", "-in", private, "-pubout", "-out", public)
        os.chmod(private, 0o600)
        if sim_bin:
            shutil.copy2(public, sim_bin / "config-include" / public.name)


def configure_robust(folder, cfg):
    ini = folder / "Robust.HG.ini"
    shutil.copyfile(folder / "Robust.HG.ini.example", ini)
    p = urllib.parse.urlsplit(cfg["private_url"])
    updates = [
        ("Const", "BaseHostname", f'"{cfg["public_host"]}"'),
        ("Const", "BaseURL", f'"http://{cfg["public_host"]}"'),
        ("Const", "PublicPort", f'"{cfg["public_port"]}"'),
        ("Const", "PrivatePort", f'"{p.port}"'),
        ("DatabaseService", "StorageProvider", '"OpenSim.Data.MySQL.dll"'),
        ("DatabaseService", "ConnectionString", conn_string(cfg)),
        ("GridInfoService", "gridname", f'"{cfg["grid"]}"'),
        ("GridInfoService", "gridnick", f'"{cfg["nick"]}"'),
        ("LoginService", "WelcomeMessage", f'"Willkommen in {cfg["grid"]}!"'),
        ("LoginService", "SearchURL", '""'),
        ("LoginService", "DestinationGuide", '""'),
        ("NexBus", "Enabled", "false"),
        ("NexEconomy", "Enabled", "false"),
        ("OfflineIMMailRelay", "Enabled", "false"),
        ("OglIarSelfService", "Enabled", "false"),
        ("OGLVoice", "Enabled", "false"),
        ("OGLFeatureControl", "Enabled", "true"),
    ]
    for sec, key, value in updates:
        put_ini(ini, sec, key, value)
    put_ini(ini, "Environment", "NEXVERSE_DB_PASSWORD", '""')


def configure_simulator(folder, cfg):
    ini = folder / "OpenSim.ini"
    shutil.copyfile(folder / "OpenSim.ini.example", ini)
    common = folder / "config-include" / "GridCommon.ini"
    shutil.copyfile(folder / "config-include" / "GridCommon.ini.example", common)
    # Declare placeholder keys so OpenSim/Nini can expand the process environment
    # without persisting any secrets in INI files.
    for name in (
        "NEXVERSE_DB_PASSWORD", "NEXVERSE_NODE_ID", "NEXVERSE_ECONOMY_API_KEY",
        "NEXVERSE_GROUP_LAND_API_KEY", "NEXVERSE_EXPERIENCES_API_KEY",
        "NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY",
        "NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY",
        "NEXVERSE_NEXBUS_SHARED_KEY", "NEXVERSE_NEXBUS_PEER_URL",
        "NEXVERSE_OFFLINE_IM_SMTP_PASSWORD", "NEXVERSE_IM_RELAY_SIGNING_KEY",
        "NEXVERSE_IM_RELAY_IMAP_PASSWORD", "NEX_PORTAL_IM_SECRET",
    ):
        put_ini(common, "Environment", name, '""')
    remove_ini(common, "DatabaseService", "Include-Storage")
    p = urllib.parse.urlsplit(cfg["private_url"])
    for sec, key, value in (
        ("DatabaseService", "StorageProvider", '"OpenSim.Data.MySQL.dll"'),
        ("DatabaseService", "ConnectionString", conn_string(cfg)),
    ):
        put_ini(common, sec, key, value)
    common_vals = [
        ("Const", "BaseHostname", f'"{cfg["public_host"]}"'),
        ("Const", "BaseURL", f'"http://{cfg["public_host"]}"'),
        ("Const", "PublicPort", f'"{cfg["public_port"]}"'),
        ("Const", "PrivURL", f'"{p.scheme}://{p.hostname}"'),
        ("Const", "PrivatePort", f'"{p.port}"'),
        ("Architecture", "Include-Architecture", '"config-include/GridHypergrid.ini"'),
        ("Network", "http_listener_port", str(cfg["region_port"])),
        ("NexVerseNodeAgent", "Enabled", "false"),
        ("NexEconomyViewer", "Enabled", "false"),
        ("NexDiscoveryViewer", "Enabled", "false"),
        ("OfflineIMEmail", "Enabled", "false"),
        ("OpenGenesisLINKIAR", "Enabled", "false"),
        ("SimulatorFeatures", "SearchServerURI", '""'),
        ("SimulatorFeatures", "DestinationGuideURI", '""'),
        ("Economy", "economymodule", '""'),
        ("OGLFeatureControl", "Enabled", "true" if cfg["robust"] else "false"),
    ]
    for sec, key, value in common_vals:
        put_ini(ini, sec, key, value)
    for sec, key, value in [
        ("Hypergrid", "HomeURI", f'"{cfg["public_url"]}"'),
        ("Hypergrid", "GatekeeperURI", f'"{cfg["public_url"]}"'),
    ]:
        put_ini(common, sec, key, value)
    # All GridCommon service URLs use Const/PublicPort or Const/PrivURL/PrivatePort.
    region_file = folder / "Regions" / "Regions.ini"
    if region_file.exists():
        abort("Regions.ini existiert bereits; Erstinstaller ueberschreibt nichts.")
    region_file.parent.mkdir(parents=True, exist_ok=True)
    region_file.write_text(
        f'[{cfg["region_name"]}]\n'
        f'RegionUUID = {uuid.uuid4()}\n'
        'Location = 1000,1000\n'
        'SizeX = 256\nSizeY = 256\n'
        'InternalAddress = 0.0.0.0\n'
        f'InternalPort = {cfg["region_port"]}\n'
        'AllowAlternatePorts = False\n'
        f'ExternalHostName = {cfg["region_host"]}\n'
        '\n', encoding="utf-8")


def create_systemd(role, extra_after=""):
    service = "opengenesislink-" + role
    screen_name = "OpenGenesisLINK-Robust" if role == "robust" else "OpenGenesisLINK"
    dll = "Robust.dll" if role == "robust" else "OpenSim.dll"
    args = " -inifile=Robust.HG.ini" if role == "robust" else ""
    unit = (f"[Unit]\nDescription=OpenGenesisLINK Generation 1 {role}\n"
            "Wants=network-online.target\nAfter=network-online.target" + extra_after + "\n\n"
            "[Service]\nType=simple\nUser=opengenesislink\nGroup=opengenesislink\n"
            "Environment=HOME=/var/lib/opengenesislink\n"
            f"EnvironmentFile={ETC / (role + '.env')}\n"
            f"WorkingDirectory={ROOT / role / 'bin'}\n"
            f"ExecStart=/usr/bin/screen -D -m -S {screen_name} /usr/bin/dotnet {dll}{args}\n"
            f"ExecStop=/usr/local/libexec/ogl-screen-stop {screen_name}\n"
            "Restart=on-failure\nRestartSec=15\nTimeoutStopSec=240\n"
            "NoNewPrivileges=true\nUMask=0077\n\n"
            "[Install]\nWantedBy=multi-user.target\n")
    path = Path("/etc/systemd/system") / (service + ".service")
    path.write_text(unit, encoding="utf-8")


def create_stop_helper():
    helper = Path("/usr/local/libexec/ogl-screen-stop")
    helper.parent.mkdir(parents=True, exist_ok=True)
    helper.write_text(
        '#!/bin/sh\nset -eu\n'
        'session="$1"\n'
        '/usr/bin/screen -S "$session" -p 0 -X stuff "$(printf "quit\\r")" || exit 0\n'
        'i=0\nwhile [ "$i" -lt 210 ]; do\n'
        '  if ! /usr/bin/screen -ls | grep -Fq "$session"; then exit 0; fi\n'
        '  i=$((i+1)); sleep 1\n'
        'done\nexit 1\n', encoding="utf-8")
    os.chmod(helper, 0o755)


def install_files(pkg, cfg):
    if shutil.which("id"):
        found = subprocess.run(["id", USER], capture_output=True)
        if found.returncode != 0:
            command("useradd", "--system", "--create-home", "--home-dir",
                    "/var/lib/opengenesislink", "--shell", "/usr/sbin/nologin", USER)
    ROOT.mkdir(parents=True, mode=0o755)
    rb = sm = None
    for role, enabled in (("robust", cfg["robust"]), ("simulator", cfg["simulator"])):
        if not enabled:
            continue
        target = ROOT / role
        shutil.copytree(pkg, target)
        folder = target / "bin"
        if role == "robust":
            rb = folder
            configure_robust(folder, cfg)
        else:
            sm = folder
            configure_simulator(folder, cfg)
        write_env(role, cfg)
    keypair(rb, sm)
    command("chown", "-R", f"{USER}:{USER}", ROOT)
    if rb:
        os.chmod(rb / "config-include" / "ogl-feature-private.pem", 0o600)
    create_stop_helper()
    if rb:
        create_systemd("robust")
    if sm:
        after = " opengenesislink-robust.service" if rb else ""
        create_systemd("simulator", after)
    command("systemctl", "daemon-reload")
    services = [r for r in ("robust", "simulator") if cfg[r]]
    for role in services:
        command("systemctl", "enable", "--now", "opengenesislink-" + role)
    print("\nInstallation ausgefuehrt. Dienstestatus:")
    for role in services:
        command("systemctl", "status", "--no-pager", "--lines=8",
                "opengenesislink-" + role, check=False)
    print("\nScreen-Konsole (CTRL+A, D zum Verlassen):")
    for role in services:
        screen = "OpenGenesisLINK-Robust" if role == "robust" else "OpenGenesisLINK"
        print(f"  sudo -u {USER} screen -r {screen}")
    print("Logs: journalctl -u opengenesislink-robust -f")
    print("      journalctl -u opengenesislink-simulator -f")
    print(f"Grid-Login: {cfg['public_url']}/")
    if cfg["simulator"]:
        print(f"Firewall: Region {cfg['region_port']}/UDP und {cfg['region_port']}/TCP")
    if cfg["robust"]:
        print(f"Firewall: ROBUST {cfg['public_port']}/TCP oeffentlich; "
              "8003/TCP nur im privaten Netz.")
    if cfg["simulator"] and not cfg["robust"]:
        print("ACHTUNG: Externer Simulator benoetigt den authentischen "
              "OGLFeatureControl-Public-Key vom ROBUST-Betreiber, bevor "
              "dieses Modul aktiviert werden kann.")
    print("DNS, Firewall, TLS/Reverse-Proxy, Nutzer/Estates und Online-Tests "
          "muessen vor produktivem Betrieb separat geprueft werden.")


def main():
    os_id, version = preflight()
    print("OpenGenesisLINK Generation 1 | Stable 0.9.4.0 | .NET 10\n")
    with tempfile.TemporaryDirectory(prefix="ogl-install-") as td:
        work = Path(td)
        pkg = unpack_verified(fetch_release(work), work)
        cfg = collect()
        packages(os_id, version, cfg["local_db"])
        db_setup(cfg)
        install_files(pkg, cfg)


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, urllib.error.URLError, subprocess.CalledProcessError,
            KeyError, ValueError, zipfile.BadZipFile) as exc:
        print("\nINSTALLATION ABGEBROCHEN:", exc, file=sys.stderr)
        sys.exit(1)
