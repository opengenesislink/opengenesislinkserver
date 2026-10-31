# OGL IAR WebDAV deployment (Generation 1)

Use the existing UGREEN WebDAV share over `10.78.0.4:5006` and WireGuard.
The live endpoint must have a valid HTTPS certificate with IP SAN `10.78.0.4`.
The NexVerse private CA must be trusted **on each host running the transport**.
Do **not** store passwords, CA private keys, or machine-specific private paths in Git.

This is a **local deployment step**, not a GitHub config switch. It must run on
ROBUST and on each simulator host. The self-service is disabled by default in
source-controlled examples, until an operator deploys valid NAS credentials.

From an up-to-date OpenGenesisLINK checkout on the server:

```bash
sudo python3 tools/deploy/configure_iar_webdav.py \
  --url 'https://10.78.0.4:5006/<YOUR_EXISTING_WEBDAV_SHARE>/IAR-Backups/' \
  --robust /opt/robust/bin/Robust.HG.ini \
  --sim /opt/robust/bin/OpenSim.ini \
  --username ogl-iar --runtime-user root
```

The installer prompts for the restricted NAS account password once without
exposing it in the shell history. It creates `/etc/opengenesislink/iar-webdav.pass`
with mode `0600` and the configured runtime-user ownership, plus a private
staging directory. It edits only the two named **live** INI sections, preserving
all unrelated settings and writing timestamped `.bak` copies first. The
existing administrative IAR `StorageRoot` is not modified. Restart ROBUST and
the affected simulator processes using your actual service names after deployment.
For known systemd services, repeat `--restart-unit name.service` to restart
automatically as part of the deployment.

On a simulator hosted on a different machine, copy/install the **public**
`nexverse-ca.crt` into system trust, then run the same installer with `--only-sim`
and `--sim /path/to/loaded/OpenSim.ini`. Do not copy any CA private keys. The
simulator requires its own local protected NAS password file. Use
`--runtime-user` to specify the OS service account that needs read access;
a `0600` file for `root` is intentionally not readable by other accounts.

The WebDAV backend supports authenticated resident IAR export/import with
temporary local staging and remote NAS archive storage, 2 GiB default maximum,
SHA-256 validation and bounded HTTP transfers. It does not replace the older
administrative `/api/v1/iar/*` storage behavior. The NAS archive folder must
already exist, must be accessible to `ogl-iar`, and should have separate backup
retention independent of ROBUST cleanup. This installer does not create a
public WebDAV endpoint and does not disable TLS verification.