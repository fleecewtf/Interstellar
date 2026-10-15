using GorillaLocomotion;
using HarmonyLib;
using Interstellar.Managers;
using Interstellar.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Interstellar.Menu.Main;
using Random = UnityEngine.Random;

namespace Interstellar.Mods
{
    public static class Rig
    {
        [Flags]
        private enum PoseOwner
        {
            None = 0,
            Ghost = 1,
            Invis = 2,
            Jumpscare = 4,
            Orbit = 8,
            Chase = 16,
            External = 32
        }

        private sealed class TransformState
        {
            private readonly Transform target;
            private readonly Vector3 position;
            private readonly Quaternion rotation;

            public TransformState(Transform value)
            {
                target = value;
                if (value == null)
                    return;
                position = value.position;
                rotation = value.rotation;
            }

            public void Restore()
            {
                if (target != null)
                    target.SetPositionAndRotation(position, rotation);
            }
        }

        private sealed class PoseState
        {
            internal readonly VRRig rig;
            private readonly bool enabled;
            private readonly TransformState body;
            private readonly TransformState head;
            private readonly TransformState left;
            private readonly TransformState right;
            private readonly TransformState constraint;

            public PoseState(VRRig value)
            {
                rig = value;
                enabled = value.enabled;
                body = new TransformState(value.transform);
                head = new TransformState(value.head?.rigTarget);
                left = new TransformState(value.leftHand?.rigTarget);
                right = new TransformState(value.rightHand?.rigTarget);
                constraint = new TransformState(value.headConstraint);
            }

            public void RestorePose()
            {
                body.Restore();
                head.Restore();
                left.Restore();
                right.Restore();
                constraint.Restore();
            }

            public void Restore()
            {
                if (rig == null)
                    return;
                RestorePose();
                rig.enabled = enabled;
            }
        }

        private static PoseOwner owners;
        private static PoseState originalPose;
        private static PoseState ghostPose;
        private static readonly Dictionary<PoseOwner, PoseState> gunPoses = new Dictionary<PoseOwner, PoseState>();
        private static readonly Dictionary<string, PoseState> externalPoses = new Dictionary<string, PoseState>(StringComparer.Ordinal);
        private static readonly List<string> externalOrder = new List<string>();
        private static PoseOwner latestGunOwner;
        private static bool ghostPressed;
        private static bool invisPressed;
        private static bool spinRig;
        private static bool upsideDown;
        private static bool spinHead;
        private static bool staring;
        private static VRRig headRig;
        private static Vector3 originalHeadOffset;
        private static Quaternion originalHeadConstraint;
        private static PublicRigLifecycle lifecycle;

        internal static void EnsureLifecycle()
        {
            if (lifecycle != null || CoroutineManager.instance == null)
                return;
            lifecycle = CoroutineManager.instance.gameObject.AddComponent<PublicRigLifecycle>();
        }

        private static VRRig LocalRig() => GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null;

        private static bool HasRoom() => NetworkSystem.Instance != null && NetworkSystem.Instance.InRoom;

        private static bool ValidTarget(VRRig target)
        {
            if (!HasRoom() || target == null || target.isLocal || target == LocalRig() || !target.gameObject.activeInHierarchy || target.Creator == null)
                return false;
            NetPlayer current = NetworkSystem.Instance.GetPlayer(target.Creator.ActorNumber);
            return current != null && !current.IsLocal && current.UserId == target.Creator.UserId;
        }

        private static bool Acquire(PoseOwner owner)
        {
            VRRig rig = LocalRig();
            if (rig == null)
                return false;
            if (originalPose != null && originalPose.rig != rig)
                ReleaseAllPoses();
            originalPose ??= new PoseState(rig);
            owners |= owner;
            rig.enabled = false;
            return true;
        }

        private static void Release(PoseOwner owner)
        {
            owners &= ~owner;
            gunPoses.Remove(owner);
            if (owners == PoseOwner.None)
                ReleaseAllPoses();
            else
                ApplyHeldPose();
        }

        private static void ReleaseAllPoses()
        {
            owners = PoseOwner.None;
            originalPose?.Restore();
            originalPose = null;
            ghostPose = null;
            gunPoses.Clear();
            externalPoses.Clear();
            externalOrder.Clear();
            latestGunOwner = PoseOwner.None;
        }

        private static void ApplyHeldPose()
        {
            VRRig rig = LocalRig();
            if (rig == null || originalPose == null || originalPose.rig != rig)
                return;
            rig.enabled = false;
            if ((owners & PoseOwner.External) != 0 && externalOrder.Count > 0)
                externalPoses[externalOrder[externalOrder.Count - 1]].RestorePose();
            else if ((owners & PoseOwner.Invis) != 0)
                rig.transform.position = new Vector3(999f, 999f, 999f);
            else if ((owners & latestGunOwner) != 0 && gunPoses.TryGetValue(latestGunOwner, out PoseState currentGun))
                currentGun.RestorePose();
            else if ((owners & PoseOwner.Chase) != 0 && gunPoses.TryGetValue(PoseOwner.Chase, out PoseState chase))
                chase.RestorePose();
            else if ((owners & PoseOwner.Orbit) != 0 && gunPoses.TryGetValue(PoseOwner.Orbit, out PoseState orbit))
                orbit.RestorePose();
            else if ((owners & PoseOwner.Jumpscare) != 0 && gunPoses.TryGetValue(PoseOwner.Jumpscare, out PoseState jumpscare))
                jumpscare.RestorePose();
            else if ((owners & PoseOwner.Ghost) != 0)
                ghostPose?.RestorePose();
        }

        public static void SetExternalPose(string owner, Action<VRRig> action)
        {
            if (string.IsNullOrEmpty(owner) || action == null)
                return;
            EnsureLifecycle();
            if (!Acquire(PoseOwner.External))
                return;
            PoseState before = new PoseState(LocalRig());
            try
            {
                action(LocalRig());
                externalPoses[owner] = new PoseState(LocalRig());
                externalOrder.Remove(owner);
                externalOrder.Add(owner);
            }
            catch
            {
                before.Restore();
                if (externalPoses.Count == 0)
                    Release(PoseOwner.External);
                throw;
            }
            ApplyHeldPose();
        }

        public static void ReleaseExternalPose(string owner)
        {
            if (string.IsNullOrEmpty(owner))
                return;
            externalPoses.Remove(owner);
            externalOrder.Remove(owner);
            if (externalPoses.Count == 0)
                Release(PoseOwner.External);
            else
                ApplyHeldPose();
        }

        private static void CommitGunPose(PoseOwner owner)
        {
            gunPoses[owner] = new PoseState(LocalRig());
            latestGunOwner = owner;
            ApplyHeldPose();
        }

        public static void Ghost()
        {
            EnsureLifecycle();
            bool pressed = rightSecondary;
            if (pressed && !ghostPressed)
            {
                if ((owners & PoseOwner.Ghost) != 0)
                    Release(PoseOwner.Ghost);
                else if (LocalRig() != null)
                {
                    ghostPose = new PoseState(LocalRig());
                    Acquire(PoseOwner.Ghost);
                }
            }
            ghostPressed = pressed;
            ApplyHeldPose();
        }

        public static void DisableGhost()
        {
            ghostPressed = false;
            Release(PoseOwner.Ghost);
        }

        public static void Invis()
        {
            EnsureLifecycle();
            bool pressed = rightPrimary;
            if (pressed && !invisPressed)
            {
                if ((owners & PoseOwner.Invis) != 0)
                    Release(PoseOwner.Invis);
                else
                    Acquire(PoseOwner.Invis);
            }
            invisPressed = pressed;
            ApplyHeldPose();
        }

        public static void DisableInvis()
        {
            invisPressed = false;
            Release(PoseOwner.Invis);
        }

        private static VRRig GunTarget(PoseOwner owner)
        {
            EnsureLifecycle();
            RaycastHit hit = GunUtilities.RenderGun();
            VRRig target = null;
            if (HasRoom() && GunUtilities.HandleLock(hit))
                target = GunUtilities.lockTarget;
            if (!ValidTarget(target) || (owners & PoseOwner.Invis) != 0 || !Acquire(owner))
            {
                Release(owner);
                return null;
            }
            return target;
        }

        private static Vector3 HeadPosition(VRRig rig) => rig.headMesh != null ? rig.headMesh.transform.position : rig.transform.position + Vector3.up * 0.4f;

        private static void SetHands(VRRig rig, Quaternion rotation, float spread)
        {
            if (rig.head?.rigTarget != null)
                rig.head.rigTarget.rotation = rotation;
            if (rig.leftHand?.rigTarget != null)
                rig.leftHand.rigTarget.SetPositionAndRotation(rig.transform.position - rig.transform.right * spread, rotation);
            if (rig.rightHand?.rigTarget != null)
                rig.rightHand.rigTarget.SetPositionAndRotation(rig.transform.position + rig.transform.right * spread, rotation);
        }

        public static void JumpscareGun()
        {
            VRRig target = GunTarget(PoseOwner.Jumpscare);
            if (target == null)
                return;
            VRRig rig = LocalRig();
            Transform targetHead = target.headMesh != null ? target.headMesh.transform : target.transform;
            Vector3 head = HeadPosition(target);
            rig.transform.position = head + targetHead.forward * Random.Range(0.1f, 0.5f);
            rig.transform.LookAt(head);
            Quaternion look = rig.transform.rotation;
            SetHands(rig, look, 0.2f);
            if (rig.leftHand?.rigTarget != null)
                rig.leftHand.rigTarget.SetPositionAndRotation(head + targetHead.right * 0.2f, look * Quaternion.Euler(0f, 180f, 0f));
            if (rig.rightHand?.rigTarget != null)
                rig.rightHand.rigTarget.SetPositionAndRotation(head - targetHead.right * 0.2f, look * Quaternion.Euler(0f, 180f, 0f));
            CommitGunPose(PoseOwner.Jumpscare);
        }

        public static void DisableJumpscareGun() => Release(PoseOwner.Jumpscare);

        public static void OrbitPlayerGun()
        {
            VRRig target = GunTarget(PoseOwner.Orbit);
            if (target == null)
                return;
            VRRig rig = LocalRig();
            float angle = Time.time * 3f;
            rig.transform.position = target.transform.position + new Vector3(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle));
            rig.transform.LookAt(target.transform.position);
            SetHands(rig, rig.transform.rotation, 1f);
            CommitGunPose(PoseOwner.Orbit);
        }

        public static void DisableOrbitPlayerGun() => Release(PoseOwner.Orbit);

        public static void ChasePlayerGun()
        {
            VRRig target = GunTarget(PoseOwner.Chase);
            if (target == null)
                return;
            VRRig rig = LocalRig();
            Vector3 behind = target.transform.position - target.transform.forward * 0.5f + Vector3.up * 0.1f;
            rig.transform.position = ChasePosition(rig.transform.position, behind, Time.deltaTime);
            rig.transform.LookAt(HeadPosition(target));
            SetHands(rig, rig.transform.rotation, 0.2f);
            CommitGunPose(PoseOwner.Chase);
        }

        private static Vector3 ChasePosition(Vector3 from, Vector3 target, float deltaTime) =>
            Vector3.MoveTowards(from, target, Mathf.Max(0f, deltaTime) * 8f);

        public static void DisableChasePlayerGun() => Release(PoseOwner.Chase);

        public static void SpinRig()
        {
            EnsureLifecycle();
            spinRig = true;
        }

        public static void DisableSpinRig() => spinRig = false;

        public static void UpsideDownRig()
        {
            EnsureLifecycle();
            upsideDown = true;
        }

        public static void DisableUpsideDownRig() => upsideDown = false;

        private static bool CaptureHead()
        {
            VRRig rig = LocalRig();
            if (rig == null || rig.head == null)
                return false;
            if (headRig != rig)
            {
                RestoreHead();
                headRig = rig;
                originalHeadOffset = rig.head.trackingRotationOffset;
                originalHeadConstraint = rig.headConstraint != null ? rig.headConstraint.rotation : Quaternion.identity;
            }
            return true;
        }

        public static void SpinHead()
        {
            EnsureLifecycle();
            if (!CaptureHead())
                return;
            spinHead = true;
            headRig.head.trackingRotationOffset.y = Mathf.Repeat(headRig.head.trackingRotationOffset.y + 8f, 360f);
        }

        public static void DisableSpinHead()
        {
            spinHead = false;
            if (headRig != null)
                headRig.head.trackingRotationOffset = originalHeadOffset;
            if (!staring)
                RestoreHead();
        }

        public static void StareAtClosest()
        {
            EnsureLifecycle();
            if (!CaptureHead())
                return;
            staring = true;
            if (!HasRoom() || headRig.headConstraint == null)
                return;
            VRRig closest = null;
            float distance = float.MaxValue;
            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (!ValidTarget(rig))
                    continue;
                float candidate = (rig.transform.position - headRig.transform.position).sqrMagnitude;
                if (candidate >= distance)
                    continue;
                closest = rig;
                distance = candidate;
            }
            if (closest != null)
                headRig.headConstraint.LookAt(closest.transform.position + Vector3.up * 0.4f);
        }

        public static void DisableStareAtClosest()
        {
            staring = false;
            if (headRig != null && headRig.headConstraint != null)
                headRig.headConstraint.rotation = originalHeadConstraint;
            if (!spinHead)
                RestoreHead();
        }

        private static void RestoreHead()
        {
            if (headRig != null)
            {
                if (headRig.head != null)
                    headRig.head.trackingRotationOffset = originalHeadOffset;
                if (headRig.headConstraint != null)
                    headRig.headConstraint.rotation = originalHeadConstraint;
            }
            headRig = null;
        }

        internal static void AfterRigTick(VRRig rig)
        {
            if (rig == null || rig != LocalRig())
                return;
            Movement.ApplyJumpLimit();
            if (!rig.enabled)
            {
                ApplyHeldPose();
                return;
            }
            Vector3 pivot = rig.bodyTransform != null ? rig.bodyTransform.position : rig.transform.position;
            if (spinRig)
                rig.transform.RotateAround(pivot, Vector3.up, (Time.time * 360f) % 360f);
            if (upsideDown)
                rig.transform.RotateAround(pivot, rig.transform.forward, 180f);
        }

        public static void Cleanup()
        {
            ReleaseAllPoses();
            RestoreHead();
            ghostPressed = invisPressed = spinRig = upsideDown = spinHead = staring = false;
        }
    }

    [HarmonyPatch(typeof(VRRig), nameof(VRRig.PostTick))]
    internal static class PublicRigPosePatch
    {
        private static void Postfix(VRRig __instance) => Rig.AfterRigTick(__instance);
    }

    internal sealed class PublicRigLifecycle : MonoBehaviour
    {
        private NetworkSystem network;
        private VRRig rig;
        private string room;
        private bool inRoom;
        private bool initialized;

        private void Update()
        {
            NetworkSystem currentNetwork = NetworkSystem.Instance;
            VRRig currentRig = GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null;
            bool currentInRoom = currentNetwork != null && currentNetwork.InRoom;
            string currentRoom = currentInRoom ? currentNetwork.RoomName : null;
            if (initialized && (currentNetwork != network || currentRig != rig || currentInRoom != inRoom || currentRoom != room))
            {
                Rig.Cleanup();
                Movement.ResetTransient();
            }
            network = currentNetwork;
            rig = currentRig;
            inRoom = currentInRoom;
            room = currentRoom;
            initialized = true;
        }

        private void OnDisable()
        {
            Rig.Cleanup();
            Movement.Cleanup();
            initialized = false;
        }
    }
}
