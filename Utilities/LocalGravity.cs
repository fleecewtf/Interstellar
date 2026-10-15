using GorillaLocomotion;
using GorillaTag.Gravity;
using Interstellar.Mods;
using UnityEngine;
using UnityEngine.XR;
using static Interstellar.Menu.Main;

namespace Interstellar.Utilities
{
    internal static class LocalGravity
    {
        private static bool lowGravityEnabled;
        private static bool wallWalkEnabled;
        private static GTPlayer wallOwner;
        private static bool wallContact;
        private static Vector3 wallNormal;

        internal static void EnableLowGravity() => lowGravityEnabled = true;
        internal static void DisableLowGravity() => lowGravityEnabled = false;
        internal static void EnableWallWalk() => wallWalkEnabled = true;

        internal static void DisableWallWalk()
        {
            wallWalkEnabled = false;
            ClearWallContact();
        }

        internal static void ClearWallContact()
        {
            wallOwner = null;
            wallContact = false;
            wallNormal = Vector3.zero;
        }

        internal static void AdjustGravity(MonkeGravityController controller, Vector3 force, ForceMode forceMode)
        {
            if ((!lowGravityEnabled && !wallWalkEnabled) || controller == null || controller != GTPlayerTransform.Instance)
                return;

            GTPlayer player = GTPlayer.Instance;
            Rigidbody body = controller.TargetRigidBody;
            if (player == null || GorillaTagger.Instance == null || body == null ||
                body != GorillaTagger.Instance.rigidbody || body.isKinematic || !player.isActiveAndEnabled ||
                player.inOverlay || player.disableMovement || player.isClimbing || player.GravityOverrideCount > 0 ||
                GTPlayerTransform.IgnoreGravityForce || (!XRSettings.isDeviceActive && !Application.isFocused) ||
                CameraTools.FreeCameraActive || Flying())
            {
                ClearWallContact();
                return;
            }

            Vector3 appliedForce = force * controller.GravityMultiplier;
            if (!Finite(appliedForce) || !Finite(player.scale) || player.scale <= 0f)
            {
                ClearWallContact();
                return;
            }

            if (TryWallNormal(player, out Vector3 normal))
            {
                body.AddForce(-appliedForce, forceMode);
                body.AddForce(-normal * (5f * player.scale), ForceMode.Acceleration);
            }
            else if (lowGravityEnabled)
                body.AddForce(-appliedForce * 0.68f, forceMode);
        }

        private static bool Flying() =>
            Interstellar.Menu.Buttons.GetIndex("WASD Fly")?.enabled == true ||
            (Interstellar.Menu.Buttons.GetIndex("Fly")?.enabled == true && rightPrimary) ||
            (Interstellar.Menu.Buttons.GetIndex("Joystick Fly")?.enabled == true &&
                (Mathf.Abs(leftJoystick.x) > 0.3f || Mathf.Abs(leftJoystick.y) > 0.3f));

        private static bool TryWallNormal(GTPlayer player, out Vector3 normal)
        {
            normal = Vector3.zero;
            if (!wallWalkEnabled || !rightGrab || !FeatureInput.CanUseGameInput)
            {
                ClearWallContact();
                return false;
            }

            if (wallOwner != player)
            {
                ClearWallContact();
                wallOwner = player;
            }

            bool left = player.IsHandTouching(true);
            if (left || player.IsHandTouching(false))
            {
                RaycastHit hit = player.GetTouchHitInfo(left);
                if (hit.collider != null && Finite(hit.normal) && hit.normal.sqrMagnitude > 0.000001f)
                {
                    wallNormal = hit.normal.normalized;
                    wallContact = true;
                }
            }

            normal = wallNormal;
            return wallContact;
        }

        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
