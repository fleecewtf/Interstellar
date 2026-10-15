using GorillaGameModes;
using Interstellar.Utilities;
using Photon.Pun;
using UnityEngine;

namespace Interstellar.Mods.GameModes
{
    public sealed class Guardian : MonoBehaviour
    {
        private static TappableGuardianIdol[] idols;
        private static float refreshIdolsAfter;
        private static float tapAfter;
        private static float flingAllAfter;
        private static float orbitAllAfter;
        private static float protectorAfter;
        private static float flingGunAfter;
        private static float orbitGunAfter;
        private static float freezeGunAfter;
        private static TappableGuardianIdol poseIdol;
        private static float poseStart;

        private static GorillaGuardianManager Manager => NetworkSystem.Instance != null && NetworkSystem.Instance.InRoom
            ? GameMode.ActiveGameMode as GorillaGuardianManager : null;

        private static bool IsGuardian => Manager != null && NetworkSystem.Instance.LocalPlayer != null
            && Manager.IsPlayerGuardian(NetworkSystem.Instance.LocalPlayer);

        private static TappableGuardianIdol[] GetIdols()
        {
            if (idols == null || Time.time >= refreshIdolsAfter)
            {
                idols = Object.FindObjectsByType<TappableGuardianIdol>(FindObjectsSortMode.None);
                refreshIdolsAfter = Time.time + 5f;
            }
            return idols;
        }

        public static void AlwaysGuardian()
        {
            GorillaGuardianManager manager = Manager;
            NetPlayer local = NetworkSystem.Instance != null ? NetworkSystem.Instance.LocalPlayer : null;
            if (manager == null || local == null || manager.IsPlayerGuardian(local)) { DisableAlwaysGuardian(); return; }
            if (NetworkSystem.Instance.IsMasterClient)
            {
                DisableAlwaysGuardian();
                foreach (GorillaGuardianZoneManager zone in GorillaGuardianZoneManager.zoneManagers)
                    if (zone != null && zone.isActiveAndEnabled && zone.IsZoneValid()) zone.SetGuardian(local);
                return;
            }
            TappableGuardianIdol candidate = null;
            foreach (TappableGuardianIdol idol in GetIdols())
                if (idol != null && idol.isActiveAndEnabled && !idol.isChangingPositions && idol.tappableId != 0
                    && idol.zoneManager != null && idol.zoneManager.isActiveAndEnabled && idol.zoneManager.IsZoneValid()
                    && idol.manager != null && idol.manager.photonView != null) { candidate = idol; break; }
            if (candidate == null) { DisableAlwaysGuardian(); return; }
            if (poseIdol != candidate) { poseIdol = candidate; poseStart = Time.time; }
            Rig.SetExternalPose("AlwaysGuardian", rig =>
            {
                rig.transform.position = candidate.transform.position;
                if (rig.leftHand.rigTarget != null) rig.leftHand.rigTarget.position = candidate.transform.position;
                if (rig.rightHand.rigTarget != null) rig.rightHand.rigTarget.position = candidate.transform.position;
            });
            if (Time.time < poseStart + 0.15f || Time.time < tapAfter) return;
            tapAfter = Time.time + 0.2f;
            candidate.manager.photonView.RPC("SendOnTapRPC", RpcTarget.All, candidate.tappableId, Random.Range(0.2f, 0.4f));
        }

        public static void DisableAlwaysGuardian()
        {
            Rig.ReleaseExternalPose("AlwaysGuardian");
            poseIdol = null;
        }

        private static bool ValidTarget(VRRig rig)
        {
            if (rig == null || rig.isLocal || !rig.gameObject.activeInHierarchy || rig.Creator == null || rig.netView == null || NetworkSystem.Instance == null || !NetworkSystem.Instance.InRoom) return false;
            NetPlayer current = NetworkSystem.Instance.GetPlayer(rig.Creator.ActorNumber);
            return current != null && !string.IsNullOrEmpty(current.UserId) && current.UserId == rig.Creator.UserId && !current.IsLocal;
        }

        private static bool BeginAction(ref float actionAfter)
        {
            if (!IsGuardian || Time.time < actionAfter) return false;
            actionAfter = Time.time + 0.2f;
            return true;
        }

        private static void MoveTarget(VRRig rig, Vector3 velocity)
        {
            if (!ValidTarget(rig) || !IsGuardian) return;
            rig.netView.SendRPC("GrabbedByPlayer", rig.Creator, new object[] { true, false, false });
            rig.netView.SendRPC("DroppedByPlayer", rig.Creator, new object[] { velocity });
        }

        private static Vector3 FlingVelocity() => new Vector3(Random.Range(-20f, 20f), Random.Range(-20f, 20f), Random.Range(-20f, 20f));

        private static Vector3 OrbitVelocity(VRRig target)
        {
            Vector3 center = GorillaTagger.Instance.headCollider.transform.position;
            Vector3 position = center + new Vector3(Mathf.Cos(Time.frameCount / 30f), 1f, Mathf.Sin(Time.frameCount / 30f));
            return (position - target.transform.position).normalized * 20f;
        }

        private static void Gun(bool orbit, bool freeze, ref float actionAfter)
        {
            if (!GunUtilities.GetGunInput(false)) return;
            RaycastHit hit = GunUtilities.RenderGun();
            if (!GunUtilities.HandleLock(hit) || !ValidTarget(GunUtilities.lockTarget)) return;
            if (orbit && (GorillaTagger.Instance == null || GorillaTagger.Instance.headCollider == null)) return;
            if (!BeginAction(ref actionAfter)) return;
            VRRig target = GunUtilities.lockTarget;
            Vector3 velocity = freeze ? Vector3.down : orbit ? OrbitVelocity(target) : FlingVelocity();
            MoveTarget(target, velocity);
        }

        public static void FlingGun() => Gun(false, false, ref flingGunAfter);
        public static void OrbitGun() => Gun(true, false, ref orbitGunAfter);
        public static void FreezeGun() => Gun(false, true, ref freezeGunAfter);

        public static void FlingAll()
        {
            if (!BeginAction(ref flingAllAfter)) return;
            foreach (VRRig target in VRRigCache.ActiveRigs) MoveTarget(target, FlingVelocity());
        }

        public static void OrbitAll()
        {
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.headCollider == null || !BeginAction(ref orbitAllAfter)) return;
            foreach (VRRig target in VRRigCache.ActiveRigs)
                if (ValidTarget(target)) MoveTarget(target, OrbitVelocity(target));
        }

        public static void Protector()
        {
            if (!BeginAction(ref protectorAfter)) return;
            foreach (VRRig target in VRRigCache.ActiveRigs)
            {
                if (!ValidTarget(target)) continue;
                foreach (TappableGuardianIdol idol in GetIdols())
                    if (idol != null && idol.isActiveAndEnabled && !idol.isChangingPositions && idol.zoneManager != null
                        && idol.zoneManager.isActiveAndEnabled && idol.zoneManager.IsZoneValid()
                        && Vector3.Distance(target.transform.position, idol.transform.position) < 3.5f)
                    {
                        MoveTarget(target, new Vector3(Random.Range(-99f, 99f), Random.Range(-99f, 99f), Random.Range(-99f, 99f)));
                        break;
                    }
            }
        }

        private void Update()
        {
            if (Manager != null) return;
            DisableAlwaysGuardian();
            idols = null;
            tapAfter = flingAllAfter = orbitAllAfter = protectorAfter = flingGunAfter = orbitGunAfter = freezeGunAfter = 0f;
        }

        private void OnDestroy()
        {
            DisableAlwaysGuardian();
            idols = null;
            tapAfter = flingAllAfter = orbitAllAfter = protectorAfter = flingGunAfter = orbitGunAfter = freezeGunAfter = 0f;
        }
    }
}
