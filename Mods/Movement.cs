using GorillaLocomotion;
using Interstellar.Classes.Menu;
using Interstellar.Classes.Mods;
using Interstellar.Managers;
using Interstellar.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using static Interstellar.Menu.Main;
using Object = UnityEngine.Object;

namespace Interstellar.Mods
{
    public static class Movement
    {
        public static float _flySpeed = 10f;

        public static float FlySpeed
        {
            get => _flySpeed * (scaleWithPlayer ? GTPlayer.Instance.scale : 1f);
            set => _flySpeed = value;
        }

        public static Vector3 lastPosition = Vector3.zero;
        private static bool wasdMenuPaused;
        private static bool wasdLookActive;
        private static float wasdPitch;
        private static float wasdYaw;
        private static Transform wasdLookTransform;
        private static Mouse wasdLookMouse;

        private static void ResetWASDLook()
        {
            DesktopMouseLook.Release();
            wasdLookActive = false;
            wasdLookTransform = null;
            wasdLookMouse = null;
            wasdPitch = 0f;
            wasdYaw = 0f;
        }

        private static void UpdateWASDLook(Transform parentTransform, Vector2 keyboardDelta)
        {
            if (parentTransform == null || XRSettings.isDeviceActive || !Application.isFocused)
            {
                ResetWASDLook();
                return;
            }

            Mouse mouse = Mouse.current;
            bool mouseLook = mouse != null && mouse.rightButton.isPressed;
            if (!mouseLook)
                ResetWASDLook();
            else if (wasdLookActive && (wasdLookTransform != parentTransform || wasdLookMouse != mouse || !DesktopMouseLook.IsCaptured))
                ResetWASDLook();

            bool startingLook = mouseLook && !wasdLookActive;
            if (!wasdLookActive)
            {
                Vector3 angles = parentTransform.eulerAngles;
                wasdPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), -89f, 89f);
                wasdYaw = Mathf.DeltaAngle(0f, angles.y);
                if (mouseLook)
                {
                    DesktopMouseLook.Capture();
                    wasdLookTransform = parentTransform;
                    wasdLookMouse = mouse;
                    wasdLookActive = true;
                }
            }

            wasdPitch += keyboardDelta.y;
            wasdYaw += keyboardDelta.x;
            if (mouseLook && !startingLook)
            {
                Vector2 delta = DesktopMouseLook.ReadDelta(mouse);
                if (!float.IsNaN(delta.x) && !float.IsInfinity(delta.x) &&
                    !float.IsNaN(delta.y) && !float.IsInfinity(delta.y))
                {
                    wasdPitch -= delta.y * DesktopMouseLook.DegreesPerPixel;
                    wasdYaw += delta.x * DesktopMouseLook.DegreesPerPixel;
                }
            }

            wasdPitch = Mathf.Clamp(wasdPitch, -89f, 89f);
            wasdYaw = Mathf.DeltaAngle(0f, wasdYaw);
            if (mouseLook || keyboardDelta != Vector2.zero)
                parentTransform.rotation = Quaternion.Euler(wasdPitch, wasdYaw, 0f);
        }

        public static void EnableWASDFly()
        {
            ResetWASDLook();
            lastPosition = GorillaTagger.Instance != null && GorillaTagger.Instance.rigidbody != null
                ? GorillaTagger.Instance.rigidbody.transform.position : Vector3.zero;
            wasdMenuPaused = false;
            if (XRSettings.isDeviceActive && Time.time < timeMenuStarted + 1f)
                Toggle("WASD Fly");
        }

        public static void DisableWASDFly()
        {
            ResetWASDLook();
            wasdMenuPaused = false;
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.rigidbody != null)
                GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
            if (XRSettings.isDeviceActive || GTPlayer.Instance == null)
                return;
            Transform controller = GTPlayer.Instance.GetControllerTransform(false);
            if (controller != null && controller.parent != null)
                controller.parent.rotation = Quaternion.identity;
        }

        public static void WASDFly()
        {
            const bool stationary = true;
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.rigidbody == null || GTPlayer.Instance == null)
            {
                ResetWASDLook();
                return;
            }
            Rigidbody body = GorillaTagger.Instance.rigidbody;
            if (menu != null || inTextInput || CurrentPrompt != null || Interstellar.Menu.UI.HasKeyboardFocus || !Application.isFocused || CameraTools.FreeCameraActive)
            {
                if (!wasdMenuPaused)
                    lastPosition = body.transform.position;
                wasdMenuPaused = true;
                body.linearVelocity = Vector3.zero;
                body.transform.position = lastPosition;
                ResetWASDLook();
                return;
            }
            if (wasdMenuPaused)
            {
                wasdMenuPaused = false;
                lastPosition = body.transform.position;
            }

            bool W = UnityInput.GetKey(Key.W);
            bool A = UnityInput.GetKey(Key.A);
            bool S = UnityInput.GetKey(Key.S);
            bool D = UnityInput.GetKey(Key.D);
            bool Space = UnityInput.GetKey(Key.Space);
            bool Ctrl = UnityInput.GetKey(Key.LeftCtrl);
            bool Shift = UnityInput.GetKey(Key.LeftShift);
            bool Alt = UnityInput.GetKey(Key.LeftAlt);

            bool LeftArrow = UnityInput.GetKey(Key.LeftArrow);
            bool RightArrow = UnityInput.GetKey(Key.RightArrow);
            bool UpArrow = UnityInput.GetKey(Key.UpArrow);
            bool DownArrow = UnityInput.GetKey(Key.DownArrow);

            if (stationary || W || A || S || D || Space || Ctrl)
                GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;

            if (!menu)
            {
                Transform controller = GTPlayer.Instance.GetControllerTransform(false);
                Transform parentTransform = controller != null ? controller.parent : null;
                if (parentTransform == null)
                {
                    ResetWASDLook();
                    body.transform.position = lastPosition;
                    return;
                }

                float turnStep = 250f * Time.deltaTime;
                UpdateWASDLook(parentTransform, new Vector2(
                    ((RightArrow ? 1f : 0f) - (LeftArrow ? 1f : 0f)) * turnStep,
                    ((DownArrow ? 1f : 0f) - (UpArrow ? 1f : 0f)) * turnStep));

                float speed = FlySpeed;
                if (Shift)
                    speed *= 2f;
                else if (Alt)
                    speed /= 2;

                if (W)
                    body.transform.position += parentTransform.forward * (Time.deltaTime * speed);

                if (S)
                    body.transform.position += parentTransform.forward * (Time.deltaTime * -speed);

                if (A)
                    body.transform.position += parentTransform.right * (Time.deltaTime * -speed);

                if (D)
                    body.transform.position += parentTransform.right * (Time.deltaTime * speed);

                if (Space)
                    body.transform.position += new Vector3(0f, Time.deltaTime * speed, 0f);

                if (Ctrl)
                    body.transform.position += new Vector3(0f, Time.deltaTime * -speed, 0f);

                if (VRRig.LocalRig != null && GorillaTagger.Instance.headCollider != null)
                    VRRig.LocalRig.head.rigTarget.transform.rotation = GorillaTagger.Instance.headCollider.transform.rotation;
            }

            if (!W && !A && !S && !D && !Space && !Ctrl && stationary)
                body.transform.position = lastPosition;
            else
                lastPosition = body.transform.position;
        }

        public static void Fly()
        {
            if (!rightPrimary || CameraTools.FreeCameraActive)
                return;

            GTPlayer.Instance.transform.position += GorillaTagger.Instance.headCollider.transform.forward * (Time.deltaTime * FlySpeed);
            GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
        }

        public static void JoystickFly()
        {
            if (CameraTools.FreeCameraActive) return;
            Vector2 joystick = leftJoystick;

            if (Mathf.Abs(joystick.x) <= 0.3f && Mathf.Abs(joystick.y) <= 0.3f)
                return;

            Transform head = GorillaTagger.Instance.headCollider.transform;

            GTPlayer.Instance.transform.position +=
                head.forward * (Time.deltaTime * (joystick.y * FlySpeed)) +
                head.right * (Time.deltaTime * (joystick.x * FlySpeed));

            GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
        }

        private static GameObject leftPlatform;
        private static GameObject rightPlatform;

        private static void SetPlatformPosition(GameObject platform, bool left)
        {
            GTPlayer player = GTPlayer.Instance;
            GTPlayer.HandState hand = left ? player.LeftHand : player.RightHand;



            Vector3 position = hand.GetCurrentHandPosition();
            Quaternion handRotation = hand.controllerTransform.rotation * hand.handRotOffset;
            Quaternion rotation = PlatformGeometry.Rotation(handRotation * Vector3.forward,
                GTPlayerTransform.PhysicsUp);
            float handRadius = Mathf.Max(0.001f, player.minimumRaycastDistance) * player.scale;
            float clearance = 0.001f * player.scale;

            platform.transform.SetPositionAndRotation(PlatformGeometry.Center(position, rotation,
                platform.transform.localScale, handRadius, clearance), rotation);
        }

        private static GameObject CreatePlatform(bool left)
        {
            GameObject platform = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Cube);
            platform.name = left ? "Interstellar Left Platform" : "Interstellar Right Platform";
            platform.transform.localScale = PlatformGeometry.Size(scaleWithPlayer ? GTPlayer.Instance.scale : 1f);
            platform.GetComponent<BoxCollider>().isTrigger = false;

            platform.AddComponent<ColorChanger>().colors = backgroundColor;

            SetPlatformPosition(platform, left);

            return platform;
        }

        private static bool ProcessPlatform(bool left, bool grip)
        {
            GameObject platform = left ? leftPlatform : rightPlatform;

            if (grip && platform == null)
            {
                GameObject newPlatform = CreatePlatform(left);

                if (left)
                    leftPlatform = newPlatform;
                else
                    rightPlatform = newPlatform;
                return true;
            }
            else if (!grip && platform != null)
            {
                platform.GetComponent<BoxCollider>().enabled = false;
                Object.Destroy(platform);

                if (left)
                    leftPlatform = null;
                else
                    rightPlatform = null;
                return true;
            }
            return false;
        }

        public static void Platforms()
        {
            bool changed = ProcessPlatform(true, leftGrab);
            changed |= ProcessPlatform(false, rightGrab);



            if (changed) Physics.SyncTransforms();
        }

        public static void DisablePlatforms()
        {
            bool changed = ProcessPlatform(true, false);
            changed |= ProcessPlatform(false, false);
            if (changed) Physics.SyncTransforms();
        }

        private static readonly Dictionary<GorillaSurfaceOverride, Vector2> boostedSurfaces = new Dictionary<GorillaSurfaceOverride, Vector2>();
        private static readonly Dictionary<MeshCollider, bool> noclipColliders = new Dictionary<MeshCollider, bool>();
        private static bool uncapEnabled;
        private static GTPlayer uncapPlayer;
        private static float originalJumpLimit;
        private static float nextSurfaceScan;
        private static float nextColliderScan;
        private static bool pullInitialized;
        private static bool lastTouchLeft;
        private static bool lastTouchRight;

        public static void SpeedBoost()
        {
            Rig.EnsureLifecycle();
            if (Time.unscaledTime < nextSurfaceScan)
                return;
            nextSurfaceScan = Time.unscaledTime + 0.5f;
            foreach (GorillaSurfaceOverride surface in Resources.FindObjectsOfTypeAll<GorillaSurfaceOverride>())
            {
                if (surface == null || !surface.gameObject.scene.IsValid())
                    continue;
                if (!boostedSurfaces.ContainsKey(surface))
                    boostedSurfaces.Add(surface, new Vector2(surface.extraVelMultiplier, surface.extraVelMaxMultiplier));
                surface.extraVelMultiplier = 1.3f;
                surface.extraVelMaxMultiplier = 1.4f;
            }
        }

        public static void DisableSpeedBoost()
        {
            foreach (KeyValuePair<GorillaSurfaceOverride, Vector2> state in boostedSurfaces)
            {
                if (state.Key == null)
                    continue;
                state.Key.extraVelMultiplier = state.Value.x;
                state.Key.extraVelMaxMultiplier = state.Value.y;
            }
            boostedSurfaces.Clear();
            nextSurfaceScan = 0f;
        }

        public static void Noclip()
        {
            Rig.EnsureLifecycle();
            if (GTPlayer.Instance == null || rightTrigger <= 0.5f)
            {
                DisableNoclip();
                return;
            }
            if (Time.unscaledTime < nextColliderScan)
                return;
            nextColliderScan = Time.unscaledTime + 0.25f;
            foreach (MeshCollider collider in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
            {
                if (collider == null || !collider.gameObject.scene.IsValid() ||
                    collider.transform.IsChildOf(GTPlayer.Instance.transform) || collider.GetComponentInParent<VRRig>() != null)
                    continue;
                if (!noclipColliders.ContainsKey(collider))
                    noclipColliders.Add(collider, collider.enabled);
                collider.enabled = false;
            }
        }

        public static void DisableNoclip()
        {
            foreach (KeyValuePair<MeshCollider, bool> state in noclipColliders)
                if (state.Key != null)
                    state.Key.enabled = state.Value;
            noclipColliders.Clear();
            nextColliderScan = 0f;
        }

        public static void UncapMaxVelocity()
        {
            Rig.EnsureLifecycle();
            uncapEnabled = true;
            ApplyJumpLimit();
        }

        internal static void ApplyJumpLimit()
        {
            if (!uncapEnabled || GTPlayer.Instance == null)
                return;
            if (uncapPlayer != GTPlayer.Instance)
            {
                if (uncapPlayer != null)
                    uncapPlayer.maxJumpSpeed = originalJumpLimit;
                uncapPlayer = GTPlayer.Instance;
                originalJumpLimit = uncapPlayer.maxJumpSpeed;
            }
            uncapPlayer.maxJumpSpeed = float.MaxValue;
        }

        public static void DisableUncapMaxVelocity()
        {
            uncapEnabled = false;
            if (uncapPlayer != null)
                uncapPlayer.maxJumpSpeed = originalJumpLimit;
            uncapPlayer = null;
        }

        public static void EnableWallWalk()
        {
            Rig.EnsureLifecycle();
            LocalGravity.EnableWallWalk();
        }

        public static void DisableWallWalk()
        {
            LocalGravity.DisableWallWalk();
        }

        public static void PullMod()
        {
            Rig.EnsureLifecycle();
            GTPlayer player = GTPlayer.Instance;
            if (player == null)
                return;
            bool touchingLeft = player.IsHandTouching(true);
            bool touchingRight = player.IsHandTouching(false);
            if (pullInitialized && rightGrab && ((!touchingLeft && lastTouchLeft) || (!touchingRight && lastTouchRight)))
            {
                Rigidbody body = player.GetComponent<Rigidbody>();
                if (body != null)
                {
                    Vector3 velocity = body.linearVelocity;
                    player.transform.position += new Vector3(velocity.x * 1.5f, 0f, velocity.z * 1.5f);
                }
            }
            pullInitialized = true;
            lastTouchLeft = touchingLeft;
            lastTouchRight = touchingRight;
        }

        public static void DisablePullMod()
        {
            pullInitialized = false;
            lastTouchLeft = lastTouchRight = false;
        }

        internal static void ResetTransient()
        {
            DisableNoclip();
            LocalGravity.ClearWallContact();
            DisablePullMod();
        }

        public static void Cleanup()
        {
            DisableSpeedBoost();
            DisableUncapMaxVelocity();
            DisableWallWalk();
            ResetTransient();
            DisablePlatforms();
        }
    }
}
