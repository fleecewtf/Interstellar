using UnityEngine;

namespace Interstellar.Classes.Mods
{
    public static class PlatformGeometry
    {
        public static Vector3 Size(float scale) => new Vector3(0.3f, 0.025f, 0.4f) * scale;

        public static Quaternion Rotation(Vector3 handForward, Vector3 supportUp)
        {
            Vector3 up = supportUp.sqrMagnitude > 0.000001f ? supportUp.normalized : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(handForward, up);
            if (forward.sqrMagnitude < 0.000001f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (forward.sqrMagnitude < 0.000001f)
                forward = Vector3.ProjectOnPlane(Vector3.right, up);
            return Quaternion.LookRotation(forward.normalized, up);
        }

        public static Vector3 Center(Vector3 handPosition, Quaternion rotation, Vector3 dimensions,
            float handRadius, float clearance) => handPosition - rotation * Vector3.up
                * (handRadius + dimensions.y * 0.5f + clearance);
    }
}
