#!/usr/bin/env python3
"""Owner-selection flags are not a substitute for granted friend edit rights."""
from pathlib import Path
src = (Path(__file__).resolve().parents[2] /
       "OpenSim/Region/CoreModules/World/Permissions/PermissionsModule.cs").read_text()
method = src.split("public uint GenerateClientFlags(SceneObjectPart task", 1)[1]
owner = method.split("// owner", 1)[1].split("// if not god or owner", 1)[0]
friends = method.split("// Friends granted object-edit rights are editors, not owners.", 1)[1]
friends = friends.split("// group owned or shared ?", 1)[0]
assert "returnMask |= EXTRAOWNERMASK;" in owner, "real owner identity was changed"
assert "IsFriendWithPerms(sp.UUID, task.OwnerID)" in friends
assert "ApplyObjectModifyMasks(grpEffectiveOwnerPerms, objflags, unlocked)" in friends
assert "returnMask |= (uint)PrimFlags.ObjectAnyOwner;" in friends
assert "returnMask |= EXTRAOWNERMASK;" not in friends
assert "PrimFlags.ObjectYouOwner" not in friends.split("if (notGroupdOwned", 1)[1]
assert "returnMask |= (uint)PrimFlags.ObjectOwnerModify;" in friends
assert "if (m_bypassPermissions)" in method, "bypass code unintentionally changed"
print("PASS: granted friend edit rights cannot claim ObjectYouOwner")
