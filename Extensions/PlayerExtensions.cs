using Interstellar.Utilities;

namespace Interstellar.Extensions
{
    public static class PlayerExtensions
    {


        public static VRRig VRRig(this NetPlayer self) =>
            RigUtilities.GetVRRigFromPlayer(self);


    }
}
