using GorillaLocomotion;
using Interstellar.Managers;
using Interstellar.Utilities;
using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static Interstellar.Menu.Main;

namespace Interstellar.Mods
{

    public static class CameraTools
    {
        private enum OverrideMode { None, Free, Spectate }

        private static readonly string[] ViewNames = { "Standard", "First Person", "Third Person" };
        private static readonly int[] FovValues = { 0, 70, 80, 90, 100, 110 };
        private static readonly RaycastHit[] CameraHits = new RaycastHit[32];
        private static readonly List<Camera> DesktopCameras = new List<Camera>();
        private static OverrideMode mode;
        private static int viewIndex;
        private static int fovIndex;
        private static Camera ownedCamera;
        private static Transform savedParent;
        private static Vector3 savedLocalPosition;
        private static Quaternion savedLocalRotation;
        private static float savedFov;
        private static float savedNearClip;
        private static bool savedEnabled;
        private static CinemachineBrain brain;
        private static bool savedBrainEnabled;
        private static bool brainPaused;
        private static bool transformOwned;
        private static bool renderSubscribed;
        private static bool poseReady;
        private static Vector3 cameraPosition;
        private static Quaternion cameraRotation;
        private static Vector3 freePosition;
        private static Quaternion freeRotation;
        private static bool freeInitialized;
        private static bool ownsMouse;
        private static Mouse capturedMouse;
        private static float pitch;
        private static float yaw;
        private static VRRig spectateTarget;
        private static int spectateActor = -1;
        private static string spectateUserId;
        private static bool previousSpectateFire;
        private static bool sessionInitialized;
        private static int sceneHandle;
        private static GTPlayer player;
        private static NetworkSystem network;
        private static bool inRoom;
        private static string roomName;

        public static int ViewIndex => viewIndex;
        public static int FovIndex => fovIndex;
        public static string ViewLabel => ViewNames[viewIndex];
        public static string FovLabel => fovIndex == 0 ? "Default" : FovValues[fovIndex] + "°";
        public static bool FreeCameraActive => mode == OverrideMode.Free;
        public static bool SpectateGunActive => mode == OverrideMode.Spectate;

        public static void CycleView() => SetView((viewIndex + 1) % ViewNames.Length);
        public static void CycleFov() => SetFovIndex((fovIndex + 1) % FovValues.Length);

        public static void SetView(int value)
        {
            EnsureSession();
            StopOverride();
            viewIndex = Mathf.Clamp(value, 0, ViewNames.Length - 1);
            ClearCameraToggles();
            RefreshLabels();
            if (viewIndex == 0 && fovIndex == 0) RestoreCamera();
        }

        public static void SetFovIndex(int value)
        {
            EnsureSession();
            fovIndex = Mathf.Clamp(value, 0, FovValues.Length - 1);
            RefreshLabels();
            if (fovIndex == 0 && viewIndex == 0 && mode == OverrideMode.None) RestoreCamera();
        }

        public static void EnableFreeCamera()
        {
            EnsureSession();
            StopOverride();
            viewIndex = 0;
            mode = OverrideMode.Free;
            freeInitialized = false;
            RefreshLabels();
        }

        public static void DisableFreeCamera()
        {
            if (mode == OverrideMode.Free) StopOverride();
        }

        public static void EnableSpectateGun()
        {
            EnsureSession();
            StopOverride();
            viewIndex = 0;
            mode = OverrideMode.Spectate;
            RefreshLabels();

            previousSpectateFire = RawFirePressed();
        }

        public static void DisableSpectateGun()
        {
            if (mode == OverrideMode.Spectate) StopOverride();
        }

        public static void FreeCamera()
        {
            if (!FreeCameraActive) return;
            Tick();
            if (!FreeCameraActive || !FeatureInput.CanUseGameInput)
            {
                ReleaseMouse();
                return;
            }
            if (!InitializeFreePose()) return;

            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f);
            Vector3 movement;
            if (!shouldBePC)
            {
                Transform head = LocalHead();
                if (head != null) freeRotation = head.rotation;
                movement = new Vector3(Deadzone(leftJoystick.x), Deadzone(rightJoystick.y), Deadzone(leftJoystick.y));
            }
            else
            {
                UpdateMouseLook();
                movement = new Vector3(
                    (KeyPressed(Key.D) ? 1f : 0f) - (KeyPressed(Key.A) ? 1f : 0f),
                    (KeyPressed(Key.Space) ? 1f : 0f) - (KeyPressed(Key.LeftCtrl) ? 1f : 0f),
                    (KeyPressed(Key.W) ? 1f : 0f) - (KeyPressed(Key.S) ? 1f : 0f));
            }

            movement = Vector3.ClampMagnitude(movement, 1f);
            float speed = 5f * PlayerScale();
            if (shouldBePC && KeyPressed(Key.LeftShift)) speed *= 2f;
            if (shouldBePC && KeyPressed(Key.LeftAlt)) speed *= 0.25f;
            freePosition += (freeRotation * new Vector3(movement.x, 0f, movement.z) + Vector3.up * movement.y) * (speed * dt);
        }

        public static void SpectateGun()
        {
            if (!SpectateGunActive) return;
            Tick();
            bool fire = GunUtilities.GetGunInput(true);
            if (!SpectateGunActive || !FeatureInput.CanUseGameInput)
            {
                ClearSpectateTarget();
                previousSpectateFire = RawFirePressed();
                return;
            }
            if (!fire || !ValidSelectedTarget()) ClearSpectateTarget();
            if (spectateTarget == null && GunUtilities.GetGunInput(false))
            {
                RaycastHit hit = GunUtilities.RenderGun();
                if (fire && !previousSpectateFire)
                {
                    VRRig candidate = GunUtilities.GetGunTarget(hit);
                    if (ValidTarget(candidate))
                    {
                        spectateTarget = candidate;
                        spectateActor = candidate.Creator.ActorNumber;
                        spectateUserId = candidate.Creator.UserId;
                    }
                }
            }
            previousSpectateFire = fire;
        }

        public static void Tick()
        {
            int currentScene = SceneManager.GetActiveScene().handle;
            NetworkSystem currentNetwork = NetworkSystem.Instance;
            bool currentInRoom = currentNetwork != null && currentNetwork.InRoom;
            string currentRoom = currentInRoom ? currentNetwork.RoomName : null;
            GTPlayer currentPlayer = GTPlayer.Instance;
            if (sessionInitialized && (sceneHandle != currentScene || player != currentPlayer ||
                network != currentNetwork || inRoom != currentInRoom || !string.Equals(roomName, currentRoom, StringComparison.Ordinal)))
                Cleanup();
            sceneHandle = currentScene;
            player = currentPlayer;
            network = currentNetwork;
            inRoom = currentInRoom;
            roomName = currentRoom;
            sessionInitialized = true;
        }


        public static void LateUpdate()
        {
            Tick();
            poseReady = false;
            bool inputAllowed = FeatureInput.CanUseGameInput;
            if (!inputAllowed || player == null || LocalHead() == null)
            {
                ReleaseMouse();
                ClearSpectateTarget();
                RestoreCamera();
                return;
            }
            if (SpectateGunActive && (!ValidSelectedTarget() || !GunUtilities.GetGunInput(true))) ClearSpectateTarget();
            bool hasSpectate = SpectateGunActive && spectateTarget != null;
            bool hasPose = FreeCameraActive || hasSpectate || (mode == OverrideMode.None && viewIndex != 0);
            if (!hasPose && fovIndex == 0)
            {
                RestoreCamera();
                return;
            }
            Camera desktop = FindDesktopCamera();
            if (desktop == null)
            {
                RestoreCamera();
                return;
            }
            CaptureCamera(desktop);
            PauseBrain(hasPose);
            if (hasPose)
            {
                if (FreeCameraActive)
                {
                    if (!InitializeFreePose()) return;
                    cameraPosition = freePosition;
                    cameraRotation = freeRotation;
                }
                else if (hasSpectate)
                {
                    Transform targetHead = spectateTarget.headMesh.transform;
                    cameraPosition = targetHead.TransformPoint(new Vector3(0f, 0.08f, 0.16f));
                    cameraRotation = targetHead.rotation;
                }
                else
                {
                    Transform head = LocalHead();
                    cameraRotation = head.rotation;
                    cameraPosition = viewIndex == 1
                        ? head.position + head.forward * (0.12f * PlayerScale())
                        : ThirdPersonPosition(head);
                }
                poseReady = Finite(cameraPosition);
            }
            ApplyCamera();
        }

        public static void Cleanup() => ResetState(true);

        private static void ResetState(bool refreshMenu)
        {
            StopOverride();
            viewIndex = 0;
            fovIndex = 0;
            RestoreCamera();
            sessionInitialized = false;
            bool togglesChanged = ClearCameraToggles();
            RefreshLabels();
            if (refreshMenu && togglesChanged && menu != null) ReloadMenu();
        }

        public static void Shutdown() => ResetState(false);

        public static void RefreshLabels()
        {
            var view = Interstellar.Menu.Buttons.GetIndex("Camera View");
            if (view != null) view.overlapText = "Camera View [" + ViewLabel + "]";
            var fov = Interstellar.Menu.Buttons.GetIndex("Camera FOV");
            if (fov != null) fov.overlapText = "Camera FOV [" + FovLabel + "]";
        }

        private static bool ClearCameraToggles()
        {
            bool changed = false;
            foreach (string name in new[] { "Free Camera", "Spectate Gun" })
            {
                var button = Interstellar.Menu.Buttons.GetIndex(name);
                if (button == null) continue;
                changed |= button.enabled;
                button.enabled = false;
                button.firstFrame = true;
            }
            return changed;
        }

        private static void EnsureSession()
        {
            Tick();
        }

        private static void StopOverride()
        {
            mode = OverrideMode.None;
            ClearSpectateTarget();
            previousSpectateFire = false;
            freeInitialized = false;
            poseReady = false;
            ReleaseMouse();
            RestoreCamera();
        }

        private static bool InitializeFreePose()
        {
            if (freeInitialized) return true;
            Camera camera = FindDesktopCamera();
            if (camera == null) return false;
            freePosition = camera.transform.position;
            freeRotation = camera.transform.rotation;
            Vector3 angles = freeRotation.eulerAngles;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), -89f, 89f);
            yaw = Mathf.DeltaAngle(0f, angles.y);
            freeInitialized = true;
            return true;
        }

        private static void UpdateMouseLook()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed)
            {
                ReleaseMouse();
                return;
            }
            if (ownsMouse && (capturedMouse != mouse || !DesktopMouseLook.IsCaptured)) ReleaseMouse();
            if (!ownsMouse)
            {

                if (DesktopMouseLook.IsCaptured) return;
                DesktopMouseLook.Capture();
                ownsMouse = DesktopMouseLook.IsCaptured;
                capturedMouse = ownsMouse ? mouse : null;
                return;
            }
            Vector2 delta = DesktopMouseLook.ReadDelta(mouse);
            if (!Finite(delta)) return;
            pitch = Mathf.Clamp(pitch - delta.y * DesktopMouseLook.DegreesPerPixel, -89f, 89f);
            yaw = Mathf.DeltaAngle(0f, yaw + delta.x * DesktopMouseLook.DegreesPerPixel);
            freeRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private static void ReleaseMouse()
        {
            if (ownsMouse) DesktopMouseLook.Release();
            ownsMouse = false;
            capturedMouse = null;
        }

        private static Camera FindDesktopCamera()
        {
            if (IsDesktopCamera(TPC)) return TPC;
            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger == null || tagger.thirdPersonCamera == null) return null;
            try
            {
                tagger.thirdPersonCamera.GetComponentsInChildren(true, DesktopCameras);
                foreach (Camera camera in DesktopCameras)
                    if (IsDesktopCamera(camera)) return camera;
                return null;
            }
            finally { DesktopCameras.Clear(); }
        }

        private static bool IsDesktopCamera(Camera camera)
        {
            if (camera == null || !camera.gameObject.activeInHierarchy || !camera.enabled ||
                camera.targetTexture != null || camera.stereoTargetEye != StereoTargetEyeMask.None) return false;
            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger == null || tagger.thirdPersonCamera == null ||
                !camera.transform.IsChildOf(tagger.thirdPersonCamera.transform)) return false;
            if (tagger.mainCamera != null && camera.gameObject == tagger.mainCamera) return false;
            return camera != GTFirstPersonCamera.camera;
        }

        private static void CaptureCamera(Camera camera)
        {
            if (ownedCamera == camera) return;
            RestoreCamera();
            ownedCamera = camera;
            savedParent = camera.transform.parent;
            savedLocalPosition = camera.transform.localPosition;
            savedLocalRotation = camera.transform.localRotation;
            savedFov = camera.fieldOfView;
            savedNearClip = camera.nearClipPlane;
            savedEnabled = camera.enabled;
            brain = camera.GetComponent<CinemachineBrain>();
            savedBrainEnabled = brain != null && brain.enabled;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            renderSubscribed = true;
        }

        private static void PauseBrain(bool pause)
        {
            if (pause && !transformOwned && ownedCamera != null)
            {
                savedLocalPosition = ownedCamera.transform.localPosition;
                savedLocalRotation = ownedCamera.transform.localRotation;
                transformOwned = true;
            }
            else if (!pause && transformOwned && ownedCamera != null)
            {
                if (ownedCamera.transform.parent == savedParent)
                {
                    ownedCamera.transform.localPosition = savedLocalPosition;
                    ownedCamera.transform.localRotation = savedLocalRotation;
                }
                transformOwned = false;
            }
            if (brain == null || brainPaused == pause) return;
            brain.enabled = pause ? false : savedBrainEnabled;
            brainPaused = pause;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == ownedCamera) ApplyCamera();
        }

        private static void ApplyCamera()
        {
            if (ownedCamera == null || !FeatureInput.CanUseGameInput) return;
            if (poseReady)
            {
                ownedCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                ownedCamera.nearClipPlane = Mathf.Min(savedNearClip, 0.035f * PlayerScale());
            }
            else ownedCamera.nearClipPlane = savedNearClip;
            ownedCamera.fieldOfView = fovIndex == 0 ? savedFov : FovValues[fovIndex];
        }

        private static void RestoreCamera()
        {
            if (renderSubscribed)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                renderSubscribed = false;
            }
            if (ownedCamera != null)
            {

                if (transformOwned && ownedCamera.transform.parent == savedParent)
                {
                    ownedCamera.transform.localPosition = savedLocalPosition;
                    ownedCamera.transform.localRotation = savedLocalRotation;
                }
                ownedCamera.fieldOfView = savedFov;
                ownedCamera.nearClipPlane = savedNearClip;
                ownedCamera.enabled = savedEnabled;
            }
            if (brain != null && brainPaused) brain.enabled = savedBrainEnabled;
            ownedCamera = null;
            brain = null;
            savedParent = null;
            brainPaused = false;
            transformOwned = false;
            poseReady = false;
        }

        private static Vector3 ThirdPersonPosition(Transform head)
        {
            float scale = PlayerScale();
            Vector3 anchor = head.position + Vector3.up * (0.15f * scale);
            Vector3 desired = anchor - head.forward * (1.8f * scale) + head.right * (0.3f * scale);
            Vector3 direction = desired - anchor;
            float distance = direction.magnitude;
            int count = Physics.SphereCastNonAlloc(anchor, 0.12f * scale, direction.normalized,
                CameraHits, distance, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = count == CameraHits.Length
                ? Physics.SphereCastAll(anchor, 0.12f * scale, direction.normalized, distance, ~0, QueryTriggerInteraction.Ignore)
                : CameraHits;
            if (hits != CameraHits) count = hits.Length;
            float safeDistance = distance;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || collider.GetComponentInParent<VRRig>() != null ||
                    collider.transform.IsChildOf(player.transform) ||
                    (menu != null && collider.transform.IsChildOf(menu.transform))) continue;
                safeDistance = Mathf.Min(safeDistance, Mathf.Max(0f, hits[i].distance - 0.04f * scale));
            }
            return anchor + direction.normalized * safeDistance;
        }

        private static Transform LocalHead() => GorillaTagger.Instance != null && GorillaTagger.Instance.headCollider != null
            ? GorillaTagger.Instance.headCollider.transform : null;
        private static void ClearSpectateTarget()
        {
            spectateTarget = null;
            spectateActor = -1;
            spectateUserId = null;
        }
        private static bool ValidSelectedTarget() => ValidTarget(spectateTarget)
            && spectateTarget.Creator.ActorNumber == spectateActor
            && string.Equals(spectateTarget.Creator.UserId, spectateUserId, StringComparison.Ordinal);
        private static bool ValidTarget(VRRig rig) => PlayerVisuals.CurrentRemoteRig(rig) && rig.headMesh != null;
        private static bool RawFirePressed() => shouldBePC ? Mouse.current != null && Mouse.current.leftButton.isPressed
            : ControllerInputPoller.instance != null && ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f;
        private static bool KeyPressed(Key key) => Keyboard.current != null && UnityInput.GetKey(key);
        private static float PlayerScale() => player != null && !float.IsNaN(player.scale) && !float.IsInfinity(player.scale)
            ? Mathf.Max(0.01f, player.scale) : 1f;
        private static float Deadzone(float value) => Mathf.Abs(value) < 0.15f ? 0f : value;
        private static bool Finite(Vector3 value) => !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
        private static bool Finite(Vector2 value) => !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
    }
}
