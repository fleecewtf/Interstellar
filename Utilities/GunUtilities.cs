using GorillaLocomotion;
using System.Collections.Generic;
using Interstellar.Classes.Menu;
using UnityEngine;
using UnityEngine.InputSystem;
using static Interstellar.Menu.Main;
using Object = UnityEngine.Object;

namespace Interstellar.Utilities
{
    public static class GunUtilities
    {
        public static bool gunLocked;
        public static VRRig lockTarget;

        private static GameObject gunPointer;
        private static LineRenderer gunLine;
        private static Renderer pointerRenderer;
        private static bool renderedThisFrame;
        private static int renderedFrame = -1;
        private static int raycastFrame = -1;
        private static RaycastHit frameHit;
        private static Vector3 frameLineStart;
        private static Vector3 frameAimOrigin;
        private static readonly RaycastHit[] raycastHits = new RaycastHit[128];
        private static readonly Dictionary<VRRig, Collider> bodyColliders = new Dictionary<VRRig, Collider>();
        private static readonly List<Collider> bodyColliderCandidates = new List<Collider>();
        private static readonly HashSet<VRRig> aimCandidates = new HashSet<VRRig>();
        private static readonly List<VRRig> staleBodyRigs = new List<VRRig>();
        private static Collider lockedBody;
        private static Vector3 lockedLocalPoint;
        private const float BodyPadding = 0.035f;

        public static bool GetGunInput(bool isShooting)
        {
            if (!TryCreateAimRay(out _, out _))
                return false;

            if (!isShooting)
                return true;

            return IsFirePressed(shouldBePC, Application.isFocused, inTextInput || Interstellar.Menu.UI.HasKeyboardFocus, menu != null,
                Mouse.current != null && Mouse.current.leftButton.isPressed,
                ControllerInputPoller.instance != null ? ControllerInputPoller.instance.rightControllerIndexFloat : 0f);
        }

        private static bool IsFirePressed(bool pc, bool focused, bool textInput, bool menuOpen, bool mousePressed, float triggerAmount) =>
            !textInput && (pc ? focused && !menuOpen && mousePressed : triggerAmount > 0.5f);

        private static Camera AimCamera()
        {
            if (TPC != null && TPC.isActiveAndEnabled)
                return TPC;

            Camera mainCamera = Camera.main;
            return mainCamera != null && mainCamera.isActiveAndEnabled ? mainCamera : null;
        }

        private static bool TryCreateAimRay(out Ray ray, out Vector3 lineStart)
        {
            ray = default;
            lineStart = default;

            if (shouldBePC)
            {
                Camera camera = AimCamera();
                if (camera == null)
                    return false;

                Rect viewport = new Rect(camera.rect.x * Screen.width, camera.rect.y * Screen.height,
                    camera.rect.width * Screen.width, camera.rect.height * Screen.height);
                Vector2 position = !DesktopMouseLook.IsCaptured && Mouse.current != null ? Mouse.current.position.ReadValue() : viewport.center;
                Vector3 cursor = new Vector3(Mathf.Clamp01((position.x - viewport.xMin) / Mathf.Max(1f, viewport.width)),
                    Mathf.Clamp01((position.y - viewport.yMin) / Mathf.Max(1f, viewport.height)), 0f);
                ray = camera.ViewportPointToRay(cursor);

                Transform hand = GorillaTagger.Instance != null ? GorillaTagger.Instance.rightHandTransform : null;
                lineStart = hand != null ? hand.position : ray.GetPoint(Mathf.Max(0.15f, camera.nearClipPlane));
                return ray.direction.sqrMagnitude > 0.000001f;
            }

            VRRig rig = GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null;
            if (rig != null && rig.enabled && TryCreateStraightIndexRay(rig.rightIndex, out ray))
            {
                lineStart = ray.origin;
                return true;
            }
            if (rig != null && !rig.enabled && TryCreateTrackedIndexRay(rig, out ray))
            {
                lineStart = ray.origin;
                return true;
            }

            if (GorillaTagger.Instance == null || GorillaTagger.Instance.rightHandTransform == null || GTPlayer.Instance == null)
                return false;

            var handPose = ControllerUtilities.GetTrueRightHand();
            ray = new Ray(handPose.position, handPose.forward.normalized);
            lineStart = handPose.position;
            return ray.direction.sqrMagnitude > 0.000001f;
        }

        private static bool TryCreateStraightIndexRay(VRMapIndex index, out Ray ray)
        {
            ray = default;
            if (index == null || index.fingerBone1 == null || index.fingerBone1.parent == null) return false;
            return TryCreateIndexRay(index, index.fingerBone1.parent.localToWorldMatrix, out ray);
        }

        private static bool TryCreateTrackedIndexRay(VRRig rig, out Ray ray)
        {
            ray = default;
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.rightHandTransform == null || GTPlayer.Instance == null
                || rig.rightHand == null || rig.rightIndex == null || rig.rightIndex.fingerBone1 == null) return false;
            GorillaIK ik = rig.GetComponent<GorillaIK>();
            Transform wrist = ik != null ? ik.rightHand : null;
            if (wrist == null) return false;
            Matrix4x4 palmLocal = Matrix4x4.identity;
            Transform current = rig.rightIndex.fingerBone1.parent;
            while (current != wrist)
            {
                if (current == null) return false;
                palmLocal = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * palmLocal;
                current = current.parent;
            }
            float scale = GTPlayer.Instance.scale;
            scale = !float.IsNaN(scale) && !float.IsInfinity(scale) && scale > 0f ? Mathf.Max(0.01f, scale) : 1f;
            var handPose = ControllerUtilities.GetTrueRightHand();
            var handState = GTPlayer.Instance.RightHand;
            Quaternion controllerRotation = handPose.rotation * Quaternion.Inverse(handState.handRotOffset);
            Vector3 controllerPosition = handPose.position - controllerRotation * (handState.handOffset * scale);
            Quaternion wristRotation = controllerRotation * Quaternion.Euler(rig.rightHand.trackingRotationOffset);
            Vector3 wristPosition = controllerPosition + wristRotation * (rig.rightHand.trackingPositionOffset * scale);
            Matrix4x4 wristPose = Matrix4x4.TRS(wristPosition, wristRotation, wrist.localScale * scale);
            return TryCreateIndexRay(rig.rightIndex, wristPose * palmLocal, out ray);
        }

        private static bool TryCreateIndexRay(VRMapIndex index, Matrix4x4 parentPose, out Ray ray)
        {
            ray = default;
            if (index == null || index.fingerBone1 == null || index.fingerBone2 == null || index.fingerBone3 == null || index.fingerBone1.parent == null || index.fingerBone2.parent != index.fingerBone1 || index.fingerBone3.parent != index.fingerBone2) return false;
            Transform first = index.fingerBone1;
            Transform second = index.fingerBone2;
            Transform third = index.fingerBone3;
            Matrix4x4 firstPose = parentPose * Matrix4x4.TRS(first.localPosition, Quaternion.Euler(index.startingAngle1), first.localScale);
            Matrix4x4 secondPose = firstPose * Matrix4x4.TRS(second.localPosition, Quaternion.Euler(index.startingAngle2), second.localScale);
            Matrix4x4 thirdPose = secondPose * Matrix4x4.TRS(third.localPosition, Quaternion.Euler(index.startingAngle3), third.localScale);
            Vector3 lastJoint = thirdPose.MultiplyPoint3x4(Vector3.zero);
            Vector3 localTip = third.localPosition * 0.4f;
            for (int indexChild = 0; indexChild < third.childCount; indexChild++)
            {
                Transform child = third.GetChild(indexChild);
                if (child.name.IndexOf("end", System.StringComparison.OrdinalIgnoreCase) < 0 && child.name.IndexOf("tip", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!(child.localPosition.sqrMagnitude > 0.000001f)) continue;
                localTip = child.localPosition;
                break;
            }
            Vector3 direction = thirdPose.MultiplyVector(localTip);
            Vector3 origin = lastJoint + direction;
            if (!(direction.sqrMagnitude > 0.000001f) || !FinitePosition(direction) || !FinitePosition(origin)) return false;
            ray = new Ray(origin, direction.normalized);
            return true;
        }

        private static bool IgnoreHit(Collider collider)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                return true;

            Transform target = collider.transform;
            if ((gunPointer != null && target.IsChildOf(gunPointer.transform)) ||
                (gunLine != null && target.IsChildOf(gunLine.transform)) ||
                (menu != null && target.IsChildOf(menu.transform)) ||
                collider.GetComponentInParent<ButtonCollider>() != null)
                return true;

            if (GTPlayer.Instance != null && target.IsChildOf(GTPlayer.Instance.transform))
                return true;

            VRRig rig = collider.GetComponentInParent<VRRig>();
            if (rig == null && collider.gameObject.layer == LayerMask.NameToLayer("Gorilla Trigger"))
                return true;

            return rig != null && (rig.isLocal || (GorillaTagger.Instance != null && rig == GorillaTagger.Instance.offlineVRRig));
        }

        private static int GunLayerMask()
        {
            int playerTriggerLayer = LayerMask.NameToLayer("Gorilla Trigger");
            return NoInvisLayerMask() | (playerTriggerLayer >= 0 ? 1 << playerTriggerLayer : 0);
        }

        private static RaycastHit RaycastAim(Ray ray)
        {
            int count = Physics.RaycastNonAlloc(ray, raycastHits, 512f, GunLayerMask(), QueryTriggerInteraction.Collide);
            RaycastHit[] hits = count == raycastHits.Length
                ? Physics.RaycastAll(ray, 512f, GunLayerMask(), QueryTriggerInteraction.Collide)
                : raycastHits;

            if (hits != raycastHits)
                count = hits.Length;

            RaycastHit nearest = default;
            float nearestDistance = float.MaxValue;
            aimCandidates.Clear();
            for (int i = 0; i < count; i++)
            {
                if (IgnoreHit(hits[i].collider))
                    continue;

                VRRig rig = hits[i].collider.GetComponentInParent<VRRig>();
                if (rig != null) { aimCandidates.Add(rig); continue; }
                if (hits[i].distance >= nearestDistance) continue;

                nearest = hits[i];
                nearestDistance = hits[i].distance;
            }

            if (nearest.collider == null)
                nearest.point = ray.GetPoint(512f);

            foreach (VRRig rig in VRRigCache.ActiveRigs)
                if (rig != null && !rig.isLocal && rig.gameObject.activeInHierarchy) aimCandidates.Add(rig);
            float candidateDistance = Mathf.Min(512f, nearestDistance);
            foreach (VRRig rig in aimCandidates)
            {
                Collider body = FindBodyCollider(rig);
                if (body == null || IgnoreHit(body) || !TryGetBodyAim(ray, body, EffectiveRigScale(rig), candidateDistance, out RaycastHit bodyHit)) continue;
                float distance = Vector3.Distance(ray.origin, bodyHit.point);
                if (distance >= candidateDistance || !SurfaceVisible(ray.origin, bodyHit.point, rig)) continue;
                candidateDistance = distance;
                nearest = bodyHit;
            }
            staleBodyRigs.Clear();
            foreach (var cached in bodyColliders)
                if (cached.Key == null || !cached.Key.gameObject.activeInHierarchy || !aimCandidates.Contains(cached.Key)) staleBodyRigs.Add(cached.Key);
            foreach (VRRig stale in staleBodyRigs) bodyColliders.Remove(stale);

            return nearest;
        }

        private static float EffectiveRigScale(VRRig rig)
        {
            Vector3 scale = rig.transform.lossyScale;
            float value = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f ? value : 1f;
        }

        private static Collider FindBodyCollider(VRRig rig)
        {
            if (rig == null || rig.isLocal || !rig.gameObject.activeInHierarchy) return null;
            if (bodyColliders.TryGetValue(rig, out Collider cached) && cached != null && cached.name == "BodyTrigger" && cached.enabled && cached.gameObject.activeInHierarchy && cached.GetComponentInParent<VRRig>() == rig) return cached;
            Collider fallback = rig.GetComponent<Collider>();
            try
            {
                rig.GetComponentsInChildren(false, bodyColliderCandidates);
                foreach (Collider collider in bodyColliderCandidates)
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.GetComponentInParent<VRRig>() != rig) continue;
                    if (collider.name == "BodyTrigger") { bodyColliders[rig] = collider; return collider; }
                    if (fallback == null) fallback = collider;
                }
                if (fallback != null) bodyColliders[rig] = fallback;
                return fallback;
            }
            finally { bodyColliderCandidates.Clear(); }
        }

        private static bool TryGetBodyAim(Ray ray, Collider collider, float scale, float maximumDistance, out RaycastHit hit)
        {
            hit = default;
            if (RaycastBodySurface(ray, collider, maximumDistance, out hit)) return true;
            Vector3 lossy = collider.transform.lossyScale;
            Vector3 axes = new Vector3(Mathf.Max(0.0001f, Mathf.Abs(lossy.x)), Mathf.Max(0.0001f, Mathf.Abs(lossy.y)), Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));
            Vector3 center;
            Vector3 extents;
            if (collider is BoxCollider box) { center = box.center; extents = box.size * 0.5f; }
            else if (collider is SphereCollider sphere)
            {
                center = sphere.center;
                float radius = sphere.radius * Mathf.Max(axes.x, axes.y, axes.z);
                extents = new Vector3(radius / axes.x, radius / axes.y, radius / axes.z);
            }
            else if (collider is CapsuleCollider capsule)
            {
                center = capsule.center;
                float radius = capsule.radius * Mathf.Max(axes[(capsule.direction + 1) % 3], axes[(capsule.direction + 2) % 3]);
                extents = new Vector3(radius / axes.x, radius / axes.y, radius / axes.z);
                extents[capsule.direction] = Mathf.Max(extents[capsule.direction], capsule.height * 0.5f);
            }
            else return collider.Raycast(ray, out hit, maximumDistance);
            scale = !float.IsNaN(scale) && !float.IsInfinity(scale) && scale > 0f ? scale : 1f;
            Vector3 padding = new Vector3(BodyPadding * scale / axes.x, BodyPadding * scale / axes.y, BodyPadding * scale / axes.z);
            if (!IntersectBodyBox(collider.transform.InverseTransformPoint(ray.origin), collider.transform.InverseTransformVector(ray.direction), center, extents + padding, maximumDistance, out _)) return false;
            Vector3 worldCenter = collider.transform.TransformPoint(center);
            float along = Mathf.Clamp(Vector3.Dot(worldCenter - ray.origin, ray.direction), 0f, maximumDistance);
            Vector3 projection = ray.GetPoint(along);
            Vector3 surface = collider.ClosestPoint(projection);
            if ((surface - projection).sqrMagnitude > BodyPadding * scale * BodyPadding * scale + 0.00000001f) return false;
            surface = Vector3.MoveTowards(surface, worldCenter, 0.001f * scale);
            Vector3 direction = surface - ray.origin;
            if (direction.sqrMagnitude <= 0.00000001f || Vector3.Dot(direction, ray.direction) < 0f) return false;
            Ray surfaceRay = new Ray(ray.origin, direction.normalized);
            return collider.Raycast(surfaceRay, out hit, maximumDistance);
        }

        private static bool RaycastBodySurface(Ray ray, Collider collider, float maximumDistance, out RaycastHit hit)
        {
            if (collider.Raycast(ray, out hit, maximumDistance)) return true;
            if (!(collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider) || (collider.ClosestPoint(ray.origin) - ray.origin).sqrMagnitude > 0.00000001f) return false;
            Vector3 reverseOrigin = ray.GetPoint(maximumDistance);
            if ((collider.ClosestPoint(reverseOrigin) - reverseOrigin).sqrMagnitude <= 0.00000001f || !collider.Raycast(new Ray(reverseOrigin, -ray.direction), out hit, maximumDistance)) return false;
            hit.distance = Vector3.Distance(ray.origin, hit.point);
            return hit.distance <= maximumDistance;
        }

        private static bool IntersectBodyBox(Vector3 origin, Vector3 direction, Vector3 center, Vector3 extents, float maximumDistance, out float distance)
        {
            float near = 0f;
            float far = maximumDistance;
            for (int axis = 0; axis < 3; axis++)
            {
                float coordinate = origin[axis] - center[axis];
                if (Mathf.Abs(direction[axis]) < 0.000001f)
                {
                    if (Mathf.Abs(coordinate) > extents[axis]) { distance = 0f; return false; }
                    continue;
                }
                float first = (-extents[axis] - coordinate) / direction[axis];
                float second = (extents[axis] - coordinate) / direction[axis];
                near = Mathf.Max(near, Mathf.Min(first, second));
                far = Mathf.Min(far, Mathf.Max(first, second));
                if (near > far) { distance = 0f; return false; }
            }
            distance = near;
            return far >= 0f;
        }

        private static bool SurfaceVisible(Vector3 origin, Vector3 surface, VRRig target)
        {
            Vector3 direction = surface - origin;
            float distance = direction.magnitude;
            int count = Physics.RaycastNonAlloc(new Ray(origin, direction / Mathf.Max(0.0001f, distance)), raycastHits, distance, GunLayerMask(), QueryTriggerInteraction.Collide);
            RaycastHit[] hits = count == raycastHits.Length ? Physics.RaycastAll(origin, direction, distance, GunLayerMask(), QueryTriggerInteraction.Collide) : raycastHits;
            if (hits != raycastHits) count = hits.Length;
            for (int index = 0; index < count; index++)
            {
                Collider collider = hits[index].collider;
                if (IgnoreHit(collider)) continue;
                VRRig rig = collider.GetComponentInParent<VRRig>();
                if (rig == target) continue;
                if (rig != null && collider != FindBodyCollider(rig)) continue;
                return false;
            }
            return true;
        }

        public static RaycastHit RenderGun()
        {
            if (!TryCreateAimRay(out Ray ray, out Vector3 lineStart))
                return default;

            renderedThisFrame = true;
            renderedFrame = Time.frameCount;
            if (!GetGunInput(true))
                ReleaseLock();

            if (raycastFrame != Time.frameCount)
            {
                raycastFrame = Time.frameCount;
                frameLineStart = lineStart;
                frameAimOrigin = ray.origin;
                frameHit = RaycastAim(ray);
            }

            Vector3 end = frameHit.point;
            if (gunLocked && lockTarget != null)
            {
                if (GetGunTarget(frameHit) == lockTarget) RememberLockSurface(frameHit);
                if (TryGetLockedEnd(shouldBePC ? frameAimOrigin : frameLineStart, out Vector3 lockedEnd)) end = lockedEnd;
                else ReleaseLock();
            }
            DrawVisuals(frameLineStart, end);
            return frameHit;
        }

        internal static void RefreshVisuals()
        {
            if (renderedFrame != Time.frameCount || gunLine == null || gunPointer == null)
            {
                HideVisuals();
                return;
            }
            if (!shouldBePC && GorillaIKMgr.Instance != null)
                GorillaIKMgr.Instance.jobXformHandle.Complete();
            if (!TryCreateAimRay(out Ray ray, out Vector3 lineStart))
            {
                HideVisuals();
                return;
            }
            RaycastHit hit = RaycastAim(ray);
            Vector3 end = hit.point;
            if (gunLocked && lockTarget != null)
            {
                if (TryGetLockedEnd(shouldBePC ? ray.origin : lineStart, out Vector3 lockedEnd)) end = lockedEnd;
                else ReleaseLock();
            }
            DrawVisuals(lineStart, end);
        }

        private static void DrawVisuals(Vector3 lineStart, Vector3 end)
        {
            if (!FinitePosition(lineStart) || !FinitePosition(end))
            {
                HideVisuals();
                return;
            }
            float scale = scaleWithPlayer && GTPlayer.Instance != null ? GTPlayer.Instance.scale : 1f;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) scale = 1f;
            scale = Mathf.Max(0.01f, scale);
            Color color = GetGunInput(true) ? ModelPalette.Enabled : ModelPalette.DisabledHint;

            if (gunPointer == null)
            {
                gunPointer = Interstellar.Patches.Menu.ShaderFix.CreatePrimitive(PrimitiveType.Sphere);
                gunPointer.name = "Interstellar_GunPointer";
                gunPointer.layer = 2;
                Collider collider = gunPointer.GetComponent<Collider>();
                collider.enabled = false;
                Object.Destroy(collider);
                pointerRenderer = gunPointer.GetComponent<Renderer>();
                pointerRenderer.material.shader = Shader.Find("GUI/Text Shader");
            }

            float pointerSize = 0.1f * scale;
            if (shouldBePC)
            {
                Camera camera = AimCamera();
                if (camera != null)
                    pointerSize = Mathf.Min(pointerSize, Vector3.Distance(camera.transform.position, end) * 0.035f);
            }

            gunPointer.SetActive(true);
            gunPointer.transform.localScale = Vector3.one * Mathf.Max(0.0001f, pointerSize);
            gunPointer.transform.position = end;
            pointerRenderer.material.color = color;

            if (gunLine == null)
            {
                GameObject lineObject = new GameObject("Interstellar_GunLine");
                lineObject.layer = 2;
                gunLine = lineObject.AddComponent<LineRenderer>();
                gunLine.material = new Material(Shader.Find("GUI/Text Shader"));
                gunLine.positionCount = 2;
                gunLine.useWorldSpace = true;
                gunLine.numCapVertices = 4;
            }

            gunLine.gameObject.SetActive(true);
            gunLine.startWidth = gunLine.endWidth = 0.015f * scale;
            gunLine.startColor = gunLine.endColor = color;
            gunLine.SetPosition(0, lineStart);
            gunLine.SetPosition(1, end);
        }

        private static bool FinitePosition(Vector3 position) => !float.IsNaN(position.sqrMagnitude) && !float.IsInfinity(position.sqrMagnitude);

        private static void HideVisuals()
        {
            if (gunPointer != null) gunPointer.SetActive(false);
            if (gunLine != null) gunLine.gameObject.SetActive(false);
        }

        internal static void DisposeVisuals()
        {
            HideVisuals();
            if (pointerRenderer != null) Object.Destroy(pointerRenderer.material);
            if (gunLine != null)
            {
                Object.Destroy(gunLine.material);
                Object.Destroy(gunLine.gameObject);
            }
            if (gunPointer != null) Object.Destroy(gunPointer);
            gunPointer = null;
            gunLine = null;
            pointerRenderer = null;
            renderedThisFrame = false;
            renderedFrame = -1;
            raycastFrame = -1;
            ReleaseLock();
            bodyColliders.Clear();
            aimCandidates.Clear();
            staleBodyRigs.Clear();
        }

        private static void RememberLockSurface(RaycastHit hit)
        {
            if (hit.collider == null || hit.collider.GetComponentInParent<VRRig>() != lockTarget) return;
            lockedBody = hit.collider;
            lockedLocalPoint = lockedBody.transform.InverseTransformPoint(hit.point);
        }

        private static bool TryGetLockedEnd(Vector3 origin, out Vector3 end)
        {
            end = default;
            Collider body = FindBodyCollider(lockTarget);
            if (body == null || IgnoreHit(body)) return false;
            Vector3 destination = lockedBody == body ? body.transform.TransformPoint(lockedLocalPoint) : body.bounds.center;
            destination = Vector3.MoveTowards(destination, body.bounds.center, 0.001f * EffectiveRigScale(lockTarget));
            Vector3 direction = destination - origin;
            if (!(direction.sqrMagnitude > 0.00000001f) || !RaycastBodySurface(new Ray(origin, direction.normalized), body, 512f, out RaycastHit hit) || !SurfaceVisible(origin, hit.point, lockTarget)) return false;
            end = hit.point;
            return true;
        }

        public static VRRig GetGunTarget(RaycastHit hit)
        {
            if (hit.collider == null || IgnoreHit(hit.collider))
                return null;

            VRRig rig = hit.collider.GetComponentInParent<VRRig>();
            return rig != null && !rig.isLocal ? rig : null;
        }

        public static bool HandleLock(RaycastHit hit)
        {
            if (!GetGunInput(true))
            {
                ReleaseLock();
                return false;
            }

            VRRig target = GetGunTarget(hit);
            if (target != null)
            {
                gunLocked = true;
                lockTarget = target;
                RememberLockSurface(hit);
            }

            if (lockTarget == null || lockTarget.isLocal || !lockTarget.gameObject.activeInHierarchy)
                ReleaseLock();

            if (gunLocked && TryGetLockedEnd(shouldBePC ? frameAimOrigin : frameLineStart, out Vector3 end))
            {
                if (gunLine != null && gunPointer != null)
                {
                    gunLine.SetPosition(1, end);
                    gunPointer.transform.position = end;
                }
            }
            else ReleaseLock();

            return gunLocked && lockTarget != null;
        }

        public static void ReleaseLock()
        {
            gunLocked = false;
            lockTarget = null;
            lockedBody = null;
            lockedLocalPoint = default;
        }

        public static void CleanUp()
        {
            if (!renderedThisFrame)
            {
                if (gunPointer != null)
                    gunPointer.SetActive(false);

                if (gunLine != null)
                    gunLine.gameObject.SetActive(false);

                ReleaseLock();
                bodyColliders.Clear();
                aimCandidates.Clear();
            }
            else if (!GetGunInput(true))
                ReleaseLock();

            renderedThisFrame = false;
        }
    }
}
