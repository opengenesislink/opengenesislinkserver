# OpenGenesisLINK Generation 1 - Linux Easy Installer

First-time installer for **OpenGenesisLINK Generation 1 Stable 0.9.4.0**, Linux
**x86_64**, **.NET 10**; Debian 12/13 and Ubuntu 22.04/24.04/26.04.
The server is downloaded as a verified **release ZIP from GitHub**, never from
an unreviewed development branch. The bootstrap is the reviewed Shell file
in this directory.

## Setup for beginners

On a new server with root access, GNU wget and systemd:

~~~bash
bash <(wget -qO- https://install.opengenesislink.de)
~~~

Run from a root shell (for example sudo -i beforehand). An interactive
terminal is required. The URL above works **only after** the domain has been
configured and the Stable ZIP / SHA256 artifacts have been published.

The installer offers:

1. ROBUST: Grid, login and Hypergrid services.
2. Simulator: Region server connecting to an existing ROBUST.
3. ROBUST and simulator on one host (recommended for the first deployment).

It asks for the grid name, public ROBUST host/port, the region name and public
region host/port, Estate name and owner avatar name, plus local MariaDB or
existing MariaDB access. Local MariaDB
uses an automatically generated password stored only in root-protected service
environment files. No existing grid directory, database, DB user, INI file
or service is silently overwritten.

### Paths and controls

- /opt/opengenesislink/robust/bin and /opt/opengenesislink/simulator/bin
- /etc/opengenesislink/{robust,simulator}.env (root only, mode 0600)
- opengenesislink-robust.service (screen -S OpenGenesisLINK-Robust)
- opengenesislink-simulator.service (screen -S OpenGenesisLINK)
- Dedicated unprivileged service account opengenesislink.

Systemd uses screen -D -m in the foreground (no forking), so the screen
session lifetime follows the .NET process. Stop handling sends quit to
the interactive console. Do not wrap screen -dmS in a Type=simple service:
systemd would lose track of the application.

~~~bash
systemctl status opengenesislink-robust
systemctl status opengenesislink-simulator

sudo -u opengenesislink screen -r OpenGenesisLINK-Robust
sudo -u opengenesislink screen -r OpenGenesisLINK
# CTRL+A, then D to detach without stopping the application

systemctl stop opengenesislink-simulator
systemctl restart opengenesislink-robust
~~~

The first simulator region is stored under bin/Regions/Regions.ini. The
installer configures the first Estate and its owner name. For a new grid,
create the owner account in the ROBUST console when prompted (command:
create user). The simulator only starts after confirming that account was
created. For an existing remote ROBUST, create that account there before
starting the simulator. Estate setup and avatar creation are two separate
operations; a running ROBUST process alone does not provide an owner avatar.

### Configuration and security

The installer uses the upstream generic .example files, not the
Stadt-NexVerse operator configuration. It generates:

- Robust.HG.ini, private MariaDB connection settings, grid info and login.
- OpenSim.ini, with GridHypergrid architecture instead of standalone.
- config-include/GridCommon.ini with all grid service URLs and MariaDB,
  config-include/GridHypergrid.ini, FlotsamCache and OSSL includes.
- ES256 private feature key only on ROBUST, public feature key on the local
  simulator. On a simulator-only host the feature module stays disabled until
  the administrator transfers the authentic public key securely.
- First region with unique UUID, listening UDP port and public DNS/IP.

Integration modules requiring independent external services (such as economy,
node messaging, offline mail or NAS-based archive storage) remain disabled
until their operator credentials, endpoints and trust are established. This
avoids silently connecting a new grid to Stadt-NexVerse production services.

The script does not register DNS, configure an internet firewall, obtain TLS
certificates or open ports; these are infrastructure-level operations.
Allow public ROBUST (TCP 8002 by default) and region TCP/UDP 9000 by default.
Keep private ROBUST (TCP 8003 by default) behind a firewall or VPN. Use HTTPS
for public services in production. Remote database connections require TLS.

**First boot is not an acceptance test:** verify login, inventory,
teleports, asset service, region persistence and Hypergrid connectivity.
The local DB setup declines an existing database and user to prevent
unintended modification.

## Publishing https://install.opengenesislink.de

This repository does NOT manage DNS or the actual HTTPS host. Point the
domain to your server and serve installer/bootstrap.sh as GET / with TLS.
Example nginx snippet inside an existing SSL virtual host:

~~~nginx
server_name install.opengenesislink.de;

location = / {
    default_type text/plain;
    alias /var/www/opengenesislink-installer/bootstrap.sh;
}
~~~

Copy a reviewed and pinned-commit version of bootstrap.sh to that path.
The bootstrap downloads installer/install.py from the official main branch
over HTTPS; for immutable deployment pin the raw URL to a reviewed commit SHA.
Provide HTTPS certificates before advertising the command and verify the
served response using curl -fsSL https://install.opengenesislink.de.

## Stable release coupling

The immutable Stable tag is v0.9.4.0. Expected release assets:

- OpenGenesisLINK-Generation1-0.9.4.0-Linux-x64.zip
- OpenGenesisLINK-Generation1-0.9.4.0-Linux-x64.zip.sha256

The installer verifies release metadata, checksum and package manifest.
It fails closed if the Stable release or generic runtime INIs do not exist.
The tools/release/ogl_gen1_snapshot_zip.py allowlist includes static INIs
but excludes production INIs, databases and credentials.

The rolling gen1-updates channel is not used for fresh Stable installs.
A separate configuration-preserving updater can be added in the future.

Offline checks:

~~~bash
bash -n installer/bootstrap.sh
python3 -m py_compile installer/install.py
python3 -m unittest discover -s installer -p 'test_*.py' -v
python3 tools/release/ogl_gen1_snapshot_zip.py --self-test
~~~
