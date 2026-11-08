#!/usr/bin/env python3
"""Validate G1 simulator/ROBUST configuration contracts, documented defaults and secure fallbacks.

No network, database, SMTP or secret access is needed for this regression gate.
"""
from configparser import ConfigParser
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FILES = (
    "bin/OpenSim.ini",
    "bin/OpenSim.ini.example",
    "bin/Robust.HG.ini",
    "bin/Robust.HG.ini.example",
    "bin/OpenSimDefaults.ini",
)


def text(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def parse(path: str) -> ConfigParser:
    config = ConfigParser(interpolation=None, strict=True)
    config.optionxform = str
    # Nini's OpenSimDefaults.ini contains legacy tab-indented keys.
    # ConfigParser treats following four-space keys as multiline values unless
    # their indentation is normalized. The actual INI is left untouched.
    config.read_string(text(path).expandtabs(4), source=path)
    return config


c = {path: parse(path) for path in FILES}
sim = c["bin/OpenSim.ini"]
sim_example = c["bin/OpenSim.ini.example"]
defaults = c["bin/OpenSimDefaults.ini"]
robust = c["bin/Robust.HG.ini"]
robust_example = c["bin/Robust.HG.ini.example"]

# A section-only stub is not a feature implementation. Every declared block
# in the two operator profiles and their install templates must have values.
for path in FILES[:4]:
    source = c[path]
    for name in source.sections():
        assert source.options(name), f"{path}: empty [{name}] section"

# An operator-specific profile must explicitly cover every supported key
# offered in its matching example; higher-level inherited defaults are a
# separate layer and must NOT be copied wholesale into either profile.
for active, example in ((sim, sim_example), (robust, robust_example)):
    for section in example.sections():
        assert active.has_section(section), section
        for option in example.options(section):
            assert active.has_option(section, option), (section, option)

expected_sim = {
    "Estates": ("EstateCommunicationsHandler", "Port"),
    "SMTP": ("enabled",),
    "ClientStack.LindenUDP": (
        "scene_throttle_max_bps", "TextureSendLimit", "AckTimeout", "PausedAckTimeout"),
    "Chat": ("enabled", "whisper_distance", "say_distance", "shout_distance"),
    "EntityTransfer": ("AllowAvatarCrossing", "DisableObjectTransfer",
                       "LevelHGTeleport", "DisableInterRegionTeleportCancellation",
                       "LandingPointBehavior"),
    "Wind": ("enabled", "wind_update_rate", "wind_plugin", "strength"),
    "Materials": ("enable_materials", "MaxMaterialsPerTransaction"),
    "DataSnapshot": ("index_sims", "data_exposure", "gridname",
                     "default_snapshot_period", "snapshot_cache_directory"),
    "YEngine": ("Enabled", "UseSourceHashCode", "MinTimerInterval"),
    "MediaOnAPrim": ("Enabled",),
    "Terrain": ("InitialTerrain", "SendTerrainUpdatesByViewDistance",
                "OglTerrainBrushStrengthMultiplier"),
    "LandManagement": ("LimitParcelLayerUpdateDistance",
                       "ParcelLayerViewDistance", "ShowParcelBansLines"),
    "XBakes": ("URL",),
    "RegionReady": ("enabled", "channel_notify", "login_disable"),
    "EstateManagement": ("AllowRegionRestartFromClient",),
    "Inventory": ("CoalesceMultipleObjectsToInventory",),
    "Attachments": ("Enabled",),
    "Mesh": ("AllowMeshUpload",),
    "PacketPool": ("RecycleBaseUDPPackets",),
    "Monitoring": ("Enabled",),
    "Statistics": ("NumberOfFrames",),
    "JsonStore": ("Enabled", "EnableObjectStore", "MaxStringSpace"),
    "OGLShutdown": ("RegionCloseTimeoutSeconds",),
}
for section, keys in expected_sim.items():
    for opt in keys:
        assert sim.has_option(section, opt), f"OpenSim.ini missing [{section}] {opt}"
        assert sim_example.has_option(section, opt), (
            f"OpenSim.ini.example missing [{section}] {opt}")

# Defaults must be preserved unless an already running production override
# intentionally disagrees (e.g. the city's Landscape/Grid profiles).
for section in (
    "SMTP", "Chat", "EntityTransfer", "Wind", "YEngine", "MediaOnAPrim",
    "LandManagement", "RegionReady", "EstateManagement", "Inventory",
    "Attachments", "Mesh", "PacketPool", "Monitoring", "Statistics",
    "JsonStore", "OGLShutdown"):
    for key in defaults.options(section):
        if sim.has_option(section, key):
            # Legacy OpenSimDefaults.ini uses semicolon inline comments for
            # some settings. Normalize those while comparing actual values.
            effective = lambda value: value.partition(";")[0].strip().strip('"').lower()
            assert effective(sim.get(section, key)) == effective(
                defaults.get(section, key)
            ), f"Unexpected default override: [{section}] {key}"

assert sim.get("Startup", "physics") == "BulletSim"
assert sim.get("Startup", "meshing") == "Meshmerizer"
assert sim.getboolean("EntityTransfer", "AllowAvatarCrossing")
assert not sim.getboolean("EntityTransfer", "DisableObjectTransfer")
assert sim.getboolean("Mesh", "AllowMeshUpload")
assert sim.getboolean("YEngine", "Enabled")
assert sim.getboolean("OGLMeshLandImpact", "Enabled")
assert not defaults.getboolean("OGLMeshLandImpact", "Enabled")
assert not sim.getboolean("SMTP", "enabled")  # Separate from OfflineIMEmail.
assert sim.getboolean("OfflineIMEmail", "Enabled")
assert sim.get("OfflineIMEmail", "SMTPHost").strip('"') == "web01.nexvortex.de"
assert sim.getint("OfflineIMEmail", "SMTPPort") == 587
assert sim.getboolean("OfflineIMEmail", "UseStartTls")
assert sim.get("XBakes", "URL").strip('"') == ""  # External bake disabled.
assert not sim.getboolean("DataSnapshot", "index_sims")
assert not sim.getboolean("JsonStore", "Enabled")
assert sim.getboolean("NexVerseNodeAgent", "LogForwardingEnabled")
assert sim.get("Architecture", "Include-Architecture").strip('"') == (
    "config-include/GridHypergrid.ini")
assert sim_example.get("Architecture", "Include-Architecture").strip('"') == (
    "config-include/Standalone.ini")

required_robust = {
    "NexGroups": ("Enabled", "EnableGridWideLand"),
    "OGLVoice": ("Enabled", "Mode", "ServiceUrl", "TenantId",
                 "IncludeHypergridGuests", "EnableSessionAuthority",
                 "AllowedNodes", "EnableFirestormGateway", "MediaGatewayUrl"),
    "NexVerseWorldApi": ("PasswordRecoveryEnabled",
                         "AdminDocsTrustedProxyAddresses",
                         "DatabaseMaintenanceCommandTimeoutSeconds"),
    "Hypergrid": ("HomeURI", "GatekeeperURI"),
}
for section, keys in required_robust.items():
    for key in keys:
        assert robust.has_option(section, key), f"Robust.HG.ini [{section}] {key}"
        assert robust_example.has_option(section, key), (
            f"Robust.HG.ini.example [{section}] {key}")

assert robust.getboolean("NexGroups", "Enabled")
assert robust.getboolean("NexGroups", "EnableGridWideLand")
assert not robust.getboolean("OGLVoice", "Enabled")
assert not robust.getboolean("OGLVoice", "EnableSessionAuthority")
assert not robust.getboolean("OGLVoice", "EnableFirestormGateway")
assert robust.get("OGLVoice", "AllowedNodes").strip('"') == ""
assert not robust.getboolean("NexVerseWorldApi", "PasswordRecoveryEnabled")
assert robust.get("NexVerseWorldApi", "AdminDocsTrustedProxyAddresses").strip('"') == ""
assert robust.getint("NexVerseWorldApi", "DatabaseMaintenanceCommandTimeoutSeconds") == 300
assert robust.getboolean("OGLFeatureControl", "Enabled")
assert sim.getboolean("OGLFeatureControl", "Enabled")
assert robust.getboolean("NexBus", "Enabled")
assert sim.getboolean("NexVerseNodeAgent", "Enabled")
assert robust.get("NexBus", "SharedKey") == sim.get("NexVerseNodeAgent", "SharedKey")
assert robust.get("OglIarSelfService", "StorageBackend").strip('"') == "WebDAV"
assert sim.get("OpenGenesisLINKIAR", "StorageBackend").strip('"') == "WebDAV"

# Code-backed parser contract: these names are not decorative INI keys.
code_contracts = (
    ("OpenSim/Region/CoreModules/World/Estate/EstateModule.cs",
     ("EstateCommunicationsHandler", 'GetInt("Port"', 'GetString("Token"')),
    ("OpenSim/Region/CoreModules/Avatar/BakedTextures/XBakesModule.cs",
     ('Configs["XBakes"]', 'GetString("URL"')),
    ("OpenSim/Region/OptionalModules/Materials/MaterialsModule.cs",
     ('GetBoolean("enable_materials"', 'GetInt("MaxMaterialsPerTransaction"')),
    ("NexVerse/Server/Api/NexVerseWorldApiConnector.cs",
     ('GetBoolean("EnableGridWideLand"', 'GetBoolean("EnableSessionAuthority"',
      'GetInt("DatabaseMaintenanceCommandTimeoutSeconds"')),
    ("NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs",
     ('GetBoolean("LogForwardingEnabled"',)),
)
for path, needles in code_contracts:
    code = text(path)
    for needle in needles:
        assert needle in code, f"Unsupported key contract {needle} in {path}"

# Critical operator warning text must remain visible in the editable INIs.
for path in ("bin/OpenSim.ini", "bin/Robust.HG.ini"):
    contents = text(path)
    assert contents.count("KRITISCH") >= 5, path
    assert "G1_CONFIG_AUDIT.md" in contents, path

print("OpenGenesisLINK G1: simulator/ROBUST/Hypergrid contract and INI coverage: PASS")
