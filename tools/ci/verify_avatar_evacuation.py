#!/usr/bin/env python3
"""Regression guard: graceful region restart and all-server shutdown routing."""
from pathlib import Path
r = Path(__file__).resolve().parents[2]
def load(name):
    return (r / name).read_text(encoding="utf-8-sig")
evac=load("OpenSim/Region/Framework/Scenes/NexVerseAvatarEvacuation.cs")
scene=load("OpenSim/Region/Framework/Scenes/Scene.cs")
server=load("OpenSim/Region/Application/OpenSimBase.cs")
managed=load("NexVerse/RegionModules/NodeAgent/NexVerseManagedRegionHostPlugin.cs")
project=load("OpenSim/Region/Framework/OpenSim.Region.Framework.csproj")
config=load("bin/OpenSim.ini")
checks={
    "in-process scene lifecycle":
        ('NexVerseAvatarEvacuation.Evacuate(this' in scene and
        scene.index('NexVerseAvatarEvacuation.Evacuate(this') <
        scene.index('Close();',scene.index('public void RestartNow()')) and
        'Cancelled restart of' in scene),
    "process-wide before shutdown":
        ('NexVerseAvatarEvacuation.EvacuateInstance(' in server and
         server.index('NexVerseAvatarEvacuation.EvacuateInstance(') <
         server.index('SceneManager.Close();',server.index('protected override void ShutdownSpecific()'))),
    "every local region excluded":
        ('scenes.Where(s => s != null).Select(s => s.RegionInfo.RegionID)' in evac and
         'excluded.Contains(region.RegionID)' in evac),
    "genuine online regions only":
        ('scene.GridService.GetOnlineRegions(' in evac and
         'scopeID' not in evac and
         'region.RegionHandle != scene.RegionInfo.RegionHandle' in evac),
    "closest deterministic destination":
        ('.OrderBy(region =>' in evac and '.ThenBy(region => region.RegionID.ToString())' in evac),
    "real viewer-only root relocation":
        ('!sp.IsChildAgent && !sp.IsNPC' in evac and
         'transfer.Teleport(sp, destination.RegionHandle' in evac),
    "native access policies preserved":
        ('IEntityTransferModule' in evac and 'TPFlags.ViaLocation' in evac and
         'DoTeleport(' not in evac),
    "wait for actual avatar exit":
        ('!HasHumanRootAgents(scene)' in evac and
         'Thread.Sleep(250)' in evac and 'Environment.TickCount64' in evac),
    "managed stop and restart":
        (managed.count('NexVerseAvatarEvacuation.Evacuate(')==2 and
         managed.count('region_evacuation_incomplete')==2 and
         managed.count('region_not_nexverse_managed')>=2),
    "no silent success when missing target":
        ('if (targets.Count == 0)' in evac and
         'return false;' in evac and 'return left == 0;' in evac),
    "compiled and enabled":
        ('Scenes\\NexVerseAvatarEvacuation.cs' in project and
         '[NexVerseAvatarEvacuation]' in config and 'Enabled = true' in config and
         'WaitSeconds = 25' in config),
}
for title,ok in checks.items():
    if not ok:
        raise AssertionError("AVATAR_EVACUATION_REGRESSION: "+title)
    print("PASS:", title)
print("NEXVERSE_AVATAR_EVACUATION_CONTRACT_PASS",len(checks))
