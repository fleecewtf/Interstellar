using GorillaGameModes;
using GorillaLocomotion;
using Interstellar.Utilities;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Interstellar.Mods.GameModes
{
    public sealed class Infection : MonoBehaviour
    {
        private static VRRig allTarget;
        private static VRRig gunTarget;
        private static VRRig ownedGunLock;
        private static float allPoseStart;
        private static float gunPoseStart;
        private static float reportAfter;
        private static GTPlayer freezePlayer;
        private static bool freezeOverridden;
        private static Photon.Realtime.Player tutorialPlayer;
        private static bool hadTutorialProperty;
        private static object tutorialProperty;
        private static bool hadTutorialPreference;
        private static string tutorialPreference;
        private static bool tutorialActive;

        private static GorillaTagManager Manager => NetworkSystem.Instance != null && NetworkSystem.Instance.InRoom
            ? GameMode.ActiveGameMode as GorillaTagManager : null;

        public static void TagAll()
        {
            GorillaTagManager manager = Manager;
            if (manager == null) { DisableTagAll(); return; }
            VRRig candidate = ValidTarget(allTarget) && !manager.IsInfected(allTarget.Creator) ? allTarget : null;
            if (candidate == null)
                foreach (VRRig rig in VRRigCache.ActiveRigs)
                    if (ValidTarget(rig) && !manager.IsInfected(rig.Creator)) { candidate = rig; break; }
            ApplyTag("InfectionAll", candidate, manager, ref allTarget, ref allPoseStart);
        }

        public static void TagGun()
        {
            if (!GunUtilities.GetGunInput(false)) { DisableTagGun(); return; }
            RaycastHit hit = GunUtilities.RenderGun();
            GorillaTagManager manager = Manager;
            if (manager == null || !GunUtilities.HandleLock(hit)) { DisableTagGun(); return; }
            ownedGunLock = GunUtilities.lockTarget;
            ApplyTag("InfectionGun", GunUtilities.lockTarget, manager, ref gunTarget, ref gunPoseStart);
        }

        private static bool ValidTarget(VRRig rig)
        {
            if (rig == null || rig.isLocal || !rig.gameObject.activeInHierarchy || rig.Creator == null || NetworkSystem.Instance == null || !NetworkSystem.Instance.InRoom) return false;
            NetPlayer current = NetworkSystem.Instance.GetPlayer(rig.Creator.ActorNumber);
            return current != null && !string.IsNullOrEmpty(current.UserId) && current.UserId == rig.Creator.UserId && !current.IsLocal;
        }

        private static void ApplyTag(string owner, VRRig target, GorillaTagManager manager, ref VRRig priorTarget, ref float poseStart)
        {
            NetPlayer local = NetworkSystem.Instance != null ? NetworkSystem.Instance.LocalPlayer : null;
            if (!ValidTarget(target) || local == null || manager.IsInfected(target.Creator))
            {
                Rig.ReleaseExternalPose(owner);
                priorTarget = null;
                return;
            }
            if (NetworkSystem.Instance.IsMasterClient)
            {
                Rig.ReleaseExternalPose(owner);
                GameModeUtilities.AddInfected(target.Creator);
                priorTarget = null;
                return;
            }
            if (!manager.IsInfected(local))
            {
                Rig.ReleaseExternalPose(owner);
                priorTarget = null;
                return;
            }
            if (priorTarget != target) { priorTarget = target; poseStart = Time.time; }
            Rig.SetExternalPose(owner, localRig =>
            {
                localRig.transform.position = target.transform.position;
                if (localRig.leftHand.rigTarget != null) localRig.leftHand.rigTarget.position = target.transform.position;
                if (localRig.rightHand.rigTarget != null) localRig.rightHand.rigTarget.position = target.transform.position;
            });
            if (Time.time < poseStart + 0.15f || Time.time < reportAfter) return;
            reportAfter = Time.time + 0.1f;
            GameMode.ReportTag(target.Creator);
        }

        public static void DisableTagAll()
        {
            Rig.ReleaseExternalPose("InfectionAll");
            allTarget = null;
        }

        public static void DisableTagGun()
        {
            Rig.ReleaseExternalPose("InfectionGun");
            gunTarget = null;
            if (ownedGunLock != null && GunUtilities.lockTarget == ownedGunLock) GunUtilities.ReleaseLock();
            ownedGunLock = null;
        }

        public static void NoTagFreeze()
        {
            if (Manager == null) { DisableNoTagFreeze(); return; }
            GorillaTagger tagger = GorillaTagger.Instance;
            GTPlayer player = GTPlayer.Instance;
            if (tagger == null || player == null || tagger.currentStatus != GorillaTagger.StatusEffect.Frozen || Time.time >= tagger.statusEndTime) return;
            if (freezePlayer != player) { freezePlayer = player; freezeOverridden = false; }
            freezeOverridden |= player.disableMovement;
            player.disableMovement = false;
        }

        public static void DisableNoTagFreeze()
        {
            GorillaTagger tagger = GorillaTagger.Instance;
            if (freezeOverridden && freezePlayer != null && freezePlayer == GTPlayer.Instance && tagger != null
                && tagger.currentStatus == GorillaTagger.StatusEffect.Frozen && Time.time < tagger.statusEndTime)
                freezePlayer.disableMovement = true;
            freezePlayer = null;
            freezeOverridden = false;
        }

        public static void EnableNoTagOnJoin()
        {
            if (tutorialActive) return;
            tutorialActive = true;
            hadTutorialPreference = PlayerPrefs.HasKey("didTutorial");
            tutorialPreference = PlayerPrefs.GetString("didTutorial", "");
            PlayerPrefs.SetString("didTutorial", "");
            NoTagOnJoin();
        }

        public static void NoTagOnJoin()
        {
            if (!tutorialActive) EnableNoTagOnJoin();
            Photon.Realtime.Player local = PhotonNetwork.LocalPlayer;
            if (local == null) return;
            if (tutorialPlayer == local)
            {
                if (!local.CustomProperties.ContainsKey("didTutorial") || !(local.CustomProperties["didTutorial"] is bool completed) || completed)
                    local.SetCustomProperties(new Hashtable { { "didTutorial", false } });
                return;
            }
            RestoreTutorialProperty();
            tutorialPlayer = local;
            hadTutorialProperty = local.CustomProperties.ContainsKey("didTutorial");
            tutorialProperty = hadTutorialProperty ? local.CustomProperties["didTutorial"] : null;
            local.SetCustomProperties(new Hashtable { { "didTutorial", false } });
        }

        private static void RestoreTutorialProperty()
        {
            if (tutorialPlayer != null && tutorialPlayer == PhotonNetwork.LocalPlayer)
                tutorialPlayer.SetCustomProperties(new Hashtable { { "didTutorial", hadTutorialProperty ? tutorialProperty : null } });
            tutorialPlayer = null;
            tutorialProperty = null;
        }

        public static void DisableNoTagOnJoin()
        {
            if (!tutorialActive) return;
            RestoreTutorialProperty();
            if (hadTutorialPreference) PlayerPrefs.SetString("didTutorial", tutorialPreference);
            else PlayerPrefs.DeleteKey("didTutorial");
            tutorialActive = false;
        }

        private void Update()
        {
            if (Manager != null) return;
            DisableTagAll();
            DisableTagGun();
            DisableNoTagFreeze();
        }

        private void OnDestroy()
        {
            DisableTagAll();
            DisableTagGun();
            DisableNoTagFreeze();
            DisableNoTagOnJoin();
        }
    }
}
