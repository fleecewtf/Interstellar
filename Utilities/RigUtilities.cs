namespace Interstellar.Utilities
{
    public class RigUtilities
    {
        public static VRRig GetVRRigFromPlayer(NetPlayer p) =>
            GorillaGameManager.StaticFindRigForPlayer(p);

        public static NetPlayer GetPlayerFromVRRig(VRRig p) =>
            p == null ? null : p.Creator ?? NetworkSystem.Instance.GetPlayer(NetworkSystem.Instance.GetOwningPlayerID(p.rigSerializer.gameObject));

    }
}
