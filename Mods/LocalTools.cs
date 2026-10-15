using System.Collections.Generic;
using System.Linq;
using GorillaNetworking;
using Interstellar.Managers;
using Interstellar.Utilities;
using UnityEngine;

namespace Interstellar.Mods
{
    public static class LocalTools
    {
        private static bool mutePressed;
        private static bool focusPressed;
        private static VRRig focusedRig;
        private static string focusedUserId;
        private sealed class VoiceVolume
        {
            internal VRRig Rig;
            internal string UserId;
            internal float Original, Applied;
            internal bool Matches => Rig != null && Rig.Creator != null && Rig.Creator.UserId == UserId;
        }
        private static readonly Dictionary<AudioSource, VoiceVolume> originalVolumes = new Dictionary<AudioSource, VoiceVolume>();
        private static readonly HashSet<AudioSource> seenSources = new HashSet<AudioSource>();
        private static readonly List<AudioSource> staleSources = new List<AudioSource>();
        private static PhotonNetworkController afkOwner;
        private static bool originalAfk;
        private static bool antiAfk;

        public static void EnableMuteGun() => mutePressed = FeatureInput.GunFireHeld;
        public static void EnableFocusVoice() => focusPressed = FeatureInput.GunFireHeld;

        public static void MuteGun()
        {
            if (!FeatureInput.CanUseGameInput || !GunUtilities.GetGunInput(false))
            {
                mutePressed = FeatureInput.GunFireHeld;
                return;
            }
            RaycastHit hit = GunUtilities.RenderGun();
            bool pressed = GunUtilities.GetGunInput(true);
            if (pressed && !mutePressed)
            {
                VRRig rig = GunUtilities.GetGunTarget(hit);
                NetPlayer player = rig == null || rig.isLocal ? null : RigUtilities.GetPlayerFromVRRig(rig);
                if (player != null)
                {
                    foreach (GorillaPlayerScoreboardLine line in GorillaScoreboardTotalUpdater.allScoreboardLines)
                    {
                        if (line == null || line.linePlayer == null || line.linePlayer.UserId != player.UserId || line.muteButton == null) continue;
                        bool muted = !line.muteButton.isOn;
                        line.muteButton.isOn = muted;
                        line.PressButton(muted, GorillaPlayerLineButton.ButtonType.Mute);
                        NotificationManager.SendNotification(muted ? "Player muted." : "Player unmuted.");

                        break;
                    }
                }
            }
            mutePressed = pressed;
        }

        public static void DisableMuteGun() => mutePressed = false;

        public static void FocusVoice()
        {
            if (FeatureInput.CanUseGameInput && GunUtilities.GetGunInput(false))
            {
                RaycastHit hit = GunUtilities.RenderGun();
                bool pressed = GunUtilities.GetGunInput(true);
                if (pressed && !focusPressed)
                {
                    VRRig target = GunUtilities.GetGunTarget(hit);
                    if (target != null && !target.isLocal && target.Creator != null)
                    {
                        focusedRig = target;
                        focusedUserId = target.Creator.UserId;
                    }
                }
                focusPressed = pressed;
            }
            else focusPressed = FeatureInput.GunFireHeld;

            if (!Room.IsConnected(NetworkSystem.Instance) || focusedRig == null || focusedRig.Creator == null ||
                focusedRig.Creator.UserId != focusedUserId || !VRRigCache.ActiveRigs.Contains(focusedRig))
            {
                RestoreVolumes();
                focusedRig = null;
                focusedUserId = null;
                return;
            }
            seenSources.Clear();
            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (rig == null || rig.isLocal || rig.voiceAudio == null || rig.Creator == null) continue;
                AudioSource source = rig.voiceAudio;
                seenSources.Add(source);
                if (!originalVolumes.TryGetValue(source, out VoiceVolume state) || state.Rig != rig || state.UserId != rig.Creator.UserId)
                {
                    state = new VoiceVolume { Rig = rig, UserId = rig.Creator.UserId, Original = source.volume, Applied = source.volume };
                    originalVolumes[source] = state;
                }
                else if (!Mathf.Approximately(source.volume, state.Applied)) state.Original = source.volume;
                state.Applied = rig == focusedRig ? state.Original : state.Original * 0.2f;
                source.volume = state.Applied;
            }
            staleSources.Clear();
            foreach (var entry in originalVolumes)
                if (entry.Key == null || !seenSources.Contains(entry.Key)) staleSources.Add(entry.Key);
            foreach (AudioSource source in staleSources)
            {
                RestoreVolume(source, originalVolumes[source]);
                originalVolumes.Remove(source);
            }
        }

        private static void RestoreVolume(AudioSource source, VoiceVolume state)
        {
            if (source != null && state.Matches && Mathf.Approximately(source.volume, state.Applied)) source.volume = state.Original;
        }

        private static void RestoreVolumes()
        {
            foreach (KeyValuePair<AudioSource, VoiceVolume> entry in originalVolumes) RestoreVolume(entry.Key, entry.Value);
            originalVolumes.Clear();
        }

        public static void DisableFocusVoice()
        {
            RestoreVolumes();
            focusedRig = null;
            focusedUserId = null;
            focusPressed = false;
        }

        public static void RestartMicrophone()
        {
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.myRecorder == null)
            {
                NotificationManager.SendNotification("The microphone recorder is not available yet.");
                return;
            }
            GorillaTagger.Instance.myRecorder.RestartRecording(true);
            NotificationManager.SendNotification("Microphone restarted.");
        }

        public static void EnableAntiAfk() { antiAfk = true; Tick(); }
        public static void DisableAntiAfk()
        {
            antiAfk = false;
            if (afkOwner != null) afkOwner.disableAFKKick = originalAfk;
            afkOwner = null;
        }

        internal static void Tick()
        {
            if (!antiAfk) return;
            PhotonNetworkController current = PhotonNetworkController.Instance;
            if (current != afkOwner)
            {
                if (afkOwner != null) afkOwner.disableAFKKick = originalAfk;
                afkOwner = current;
                if (current != null) originalAfk = current.disableAFKKick;
            }
            if (afkOwner != null) afkOwner.disableAFKKick = true;
        }

        public static void ResetTransient() { DisableFocusVoice(); DisableMuteGun(); }
        public static void Shutdown() { ResetTransient(); DisableAntiAfk(); }
    }
}
