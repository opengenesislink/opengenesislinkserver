using System;
using System.Reflection;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.World.Permissions;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Friend object flag regression: " + message);
    }

    // Provide exactly the right the permissions module normally queries from
    // the scene's friends service; no network/account state is needed.
    public class FriendRightsProxy : DispatchProxy
    {
        public int Rights;
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.Name == "GetRightsGrantedByFriend")
                return Rights;
            throw new InvalidOperationException("Unexpected friend API call: " + targetMethod.Name);
        }
    }

    public static int Main()
    {
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        try
        {
            Run();
            Console.WriteLine("PASS: actual GenerateClientFlags distinguishes friend edit from ownership");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static void Run()
    {
        IniConfigSource config = new IniConfigSource();
        config.AddConfig("Startup").Set("serverside_object_permissions", true);
        TestScene scene = new SceneHelpers().SetupScene("Friend flag CI", UUID.Random(), 1300, 1300, config);
        DefaultPermissionsModule permissions = new DefaultPermissionsModule();
        SceneHelpers.SetupSceneModules(scene, config, permissions);

        UUID owner = UUID.Random();
        UUID friend = UUID.Random();
        SceneObjectGroup foreign = SceneHelpers.AddSceneObject(scene, "friend-owned prim", owner);
        SceneObjectPart prim = foreign.RootPart;

        ScenePresence friendPresence = SceneHelpers.AddScenePresence(scene, friend);
        ScenePresence ownerPresence = SceneHelpers.AddScenePresence(scene, owner);
        Check(!friendPresence.IsGod, "friend must not be in god mode");
        Check(prim.OwnerID == owner && prim.GroupID != owner,
            "test prim must be owned by avatar, not by group");

        IFriendsModule friendService = DispatchProxy.Create<IFriendsModule, FriendRightsProxy>();
        var rights = (FriendRightsProxy)friendService;
        FieldInfo field = typeof(DefaultPermissionsModule).GetField(
            "m_friendsModule", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(field != null, "friends module injection point");
        field.SetValue(permissions, friendService);

        const PrimFlags ownerFlag = PrimFlags.ObjectYouOwner;
        const PrimFlags anyOwnerFlag = PrimFlags.ObjectAnyOwner;
        const PrimFlags modFlag = PrimFlags.ObjectModify;
        const PrimFlags moveFlag = PrimFlags.ObjectMove;
        const PrimFlags copyFlag = PrimFlags.ObjectCopy;
        const PrimFlags ownerModifyFlag = PrimFlags.ObjectOwnerModify;
        uint current = (uint)(ownerFlag | anyOwnerFlag | modFlag | moveFlag |
                             copyFlag | PrimFlags.ObjectTransfer | ownerModifyFlag);
        uint GetFlags(ScenePresence user) =>
            permissions.GenerateClientFlags(prim, user, current);

        rights.Rights = (int)FriendRights.CanModifyObjects;
        uint friendFlags = GetFlags(friendPresence);
        Check((friendFlags & (uint)ownerFlag) == 0,
            "granted friend must NOT be flagged as owner");
        Check((friendFlags & (uint)anyOwnerFlag) != 0,
            "existing owner should still be advertised");
        uint actualOwnerFlags = GetFlags(ownerPresence);
        Check((actualOwnerFlags & (uint)ownerFlag) != 0,
            "real owner must remain flagged as owner");
        Check((actualOwnerFlags & (uint)anyOwnerFlag) != 0,
            "real owner must retain ObjectAnyOwner");

        // When movement and modification are allowed in the prim's masks,
        // friendship must continue granting them (the fix affects identity
        // only, NOT the existing permissions granted to a friend).
        uint expectedCapabilityBits = actualOwnerFlags &
            (uint)(modFlag | moveFlag | copyFlag | ownerModifyFlag);
        Check((friendFlags & expectedCapabilityBits) == expectedCapabilityBits,
            "friend must retain owner's permitted edit/move/copy capabilities");

        rights.Rights = 0;
        uint unrelatedFlags = GetFlags(friendPresence);
        Check((unrelatedFlags & (uint)ownerFlag) == 0,
            "friend without granted edit rights must not claim ownership");
        Check((GetFlags(ownerPresence) & (uint)ownerFlag) != 0,
            "revoking friendship rights must not revoke real owner's identity");
    }
}
