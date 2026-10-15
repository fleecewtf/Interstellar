using System.Collections.Generic;
using GorillaGameModes;
using GorillaTag.Gravity;
using Interstellar.Managers;
using Interstellar.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Interstellar.Mods.GameModes
{
    public static class Paintbrawl
    {
        private sealed class Path
        {
            internal LineRenderer Line;
            internal readonly Vector3[] Points = new Vector3[33];
            internal int Frame;
        }

        private const int MaximumPaths = 32;
        private static bool aimbot;
        private static bool trajectories;
        private static GameObject holder;
        private static Material material;
        private static Path localPreview;
        private static readonly Dictionary<int, Path> paths = new Dictionary<int, Path>();
        private static readonly Stack<Path> sparePaths = new Stack<Path>();
        private static readonly List<int> expired = new List<int>();
        private static readonly RaycastHit[] hits = new RaycastHit[32];
        private static float refreshAfter;
        private static float restartAfter;
        private static int frame;
        private static int shown;

        private static GorillaPaintbrawlManager Manager => NetworkSystem.Instance != null && NetworkSystem.Instance.InRoom
            ? GameMode.ActiveGameMode as GorillaPaintbrawlManager : null;

        public static void EnableAimbot() => aimbot = true;
        public static void DisableAimbot() => aimbot = false;
        public static void EnableProjectileTrajectories() { trajectories = true; refreshAfter = 0f; }
        public static void DisableProjectileTrajectories() { trajectories = false; ClearRuntime(); }

        internal static void AssistLaunch(Slingshot slingshot, ref Vector3 velocity)
        {
            GorillaPaintbrawlManager manager = Manager;
            VRRig local = GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null;


            if (!aimbot || !FeatureInput.CanUseGameInput || manager == null || local == null || slingshot == null
                || local.projectileWeapon != slingshot || slingshot.dummyProjectile == null
                || manager.currentState != GorillaPaintbrawlManager.PaintbrawlState.GameRunning
                || !PaintbrawlMath.Finite(velocity) || manager.GetPlayerLives(NetworkSystem.Instance.LocalPlayer) <= 0) return;
            Transform view = GorillaTagger.Instance.headCollider != null ? GorillaTagger.Instance.headCollider.transform : local.transform;
            Vector3 origin = slingshot.GetLaunchPosition();
            VRRig best = null;
            float bestScore = float.MaxValue;
            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (!PlayerVisuals.CurrentRemoteRig(rig) || !manager.LocalCanHit(NetworkSystem.Instance.LocalPlayer, rig.Creator)) continue;
                Vector3 target = AimPoint(rig);
                Vector3 offset = target - view.position;
                float distance = offset.magnitude;
                if (distance < 0.15f || distance > 50f) continue;
                float angle = Vector3.Angle(view.forward, offset);
                if (angle > 55f || Obstructed(origin, target, local, rig)) continue;
                float score = angle + distance * 0.12f;
                if (score >= bestScore) continue;
                best = rig;
                bestScore = score;
            }
            if (best == null) return;
            Vector3 targetVelocity = PaintbrawlMath.Finite(best.remoteVelocity) ? Vector3.ClampMagnitude(best.remoteVelocity, 35f) : Vector3.zero;
            Vector3 gravity = ShotGravity(slingshot);
            if (PaintbrawlMath.TryIntercept(origin, AimPoint(best), targetVelocity, gravity, velocity.magnitude, out Vector3 assisted))
                velocity = assisted;
        }

        private static Vector3 AimPoint(VRRig rig) => rig.headMesh != null ? rig.headMesh.transform.position : rig.transform.position;

        private static bool Obstructed(Vector3 origin, Vector3 target, VRRig owner, VRRig targetRig)
        {
            Vector3 delta = target - origin;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, hits, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return true;
            for (int index = 0; index < count; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null || collider.GetComponentInParent<VRRig>() == owner || collider.GetComponentInParent<VRRig>() == targetRig) continue;
                if (collider.GetComponentInParent<SlingshotProjectile>() != null) continue;
                return true;
            }
            return false;
        }

        public static void RestartRound()
        {
            GorillaPaintbrawlManager manager = Manager;
            if (manager == null) { Notify("Join a Paintbrawl room first."); return; }
            if (!NetworkSystem.Instance.IsMasterClient) { Notify("You must be master client to restart this round."); return; }
            if (Time.unscaledTime < restartAfter) return;
            restartAfter = Time.unscaledTime + 2f;
            if (RoomSystem.PlayersInRoom.Count < manager.playerMin) { Notify("The room needs more players to start a round."); return; }
            if (manager.coroutineRunning || manager.currentState == GorillaPaintbrawlManager.PaintbrawlState.StartCountdown
                || manager.currentState == GorillaPaintbrawlManager.PaintbrawlState.CountingDownToStart
                || manager.currentState == GorillaPaintbrawlManager.PaintbrawlState.GameStart)
            {
                Notify("The round is already starting.");
                return;
            }
            manager.playerHitTimes.Clear();
            manager.playerStunTimes.Clear();
            manager.Transition(GorillaPaintbrawlManager.PaintbrawlState.StartCountdown);
            Notify("Paintbrawl round restarting.");
        }

        private static void Notify(string text) => NotificationManager.SendNotification(text);

        public static void LateUpdate()
        {
            GorillaPaintbrawlManager manager = Manager;
            if (!trajectories || manager == null || GorillaTagger.Instance == null || GorillaTagger.Instance.offlineVRRig == null)
            {
                if (holder != null || paths.Count != 0) ClearRuntime();
                return;
            }
            EnsureResources();
            shown = 0;
            VRRig local = GorillaTagger.Instance.offlineVRRig;

            if (localPreview != null) localPreview.Line.enabled = false;
            DrawHeldShot(local.projectileWeapon as Slingshot, local);
            if (Time.unscaledTime < refreshAfter) return;
            refreshAfter = Time.unscaledTime + 0.05f;
            frame++;
            foreach (VRRig rig in VRRigCache.ActiveRigs)
                if (PlayerVisuals.CurrentRemoteRig(rig)) DrawHeldShot(rig.projectileWeapon as Slingshot, rig);
            foreach (SlingshotProjectile projectile in SlingshotProjectileManager.allsP)
            {
                if (shown >= MaximumPaths) break;
                if (projectile == null || !projectile.gameObject.activeInHierarchy || !projectile.particleLaunched
                    || projectile.projectileRigidbody == null || projectile.projectileRigidbody.isKinematic) continue;
                if ((projectile.transform.position - local.transform.position).sqrMagnitude > 2500f) continue;
                DrawPath(projectile.GetInstanceID(), projectile.transform.position, projectile.projectileRigidbody.linearVelocity,
                    ProjectileGravity(projectile), ProjectileOwner(projectile), projectile, false);
            }
            expired.Clear();
            foreach (KeyValuePair<int, Path> entry in paths)
                if (entry.Value.Frame != frame) expired.Add(entry.Key);
            foreach (int key in expired)
            {
                Path path = paths[key];
                path.Line.enabled = false;
                sparePaths.Push(path);
                paths.Remove(key);
            }
        }

        private static VRRig ProjectileOwner(SlingshotProjectile projectile)
        {
            NetPlayer owner = projectile.projectileOwner;
            if (owner == null) return null;
            if (owner.IsLocal) return GorillaTagger.Instance.offlineVRRig;
            foreach (VRRig rig in VRRigCache.ActiveRigs)
                if (PlayerVisuals.CurrentRemoteRig(rig) && rig.Creator.ActorNumber == owner.ActorNumber
                    && rig.Creator.UserId == owner.UserId) return rig;
            return null;
        }

        private static void DrawHeldShot(Slingshot slingshot, VRRig rig)
        {
            if (shown >= MaximumPaths || slingshot == null || !slingshot.InDrawingState() || slingshot.dummyProjectile == null
                || slingshot.center == null || slingshot.centerOrigin == null) return;
            Vector3 velocity;
            if (rig.isLocal) velocity = slingshot.GetLaunchVelocity();
            else
            {

                float scale = Mathf.Max(0.01f, Mathf.Abs(slingshot.transform.lossyScale.x));
                Vector3 stretch = (slingshot.centerOrigin.position - slingshot.center.position) / scale;
                velocity = Mathf.Min(slingshot.springConstant * slingshot.maxDraw, stretch.magnitude * slingshot.springConstant)
                    * stretch.normalized * scale + rig.remoteVelocity;
            }
            DrawPath(slingshot.GetInstanceID(), slingshot.GetLaunchPosition(), velocity, ShotGravity(slingshot),
                rig, slingshot.dummyProjectile.GetComponent<SlingshotProjectile>(), rig.isLocal);
        }

        private static Vector3 ShotGravity(Slingshot slingshot)
        {
            SlingshotProjectile template = slingshot.projectilePrefab != null ? slingshot.projectilePrefab.GetComponent<SlingshotProjectile>() : null;
            float multiplier = template != null ? template.gravityMultiplier : 1f;
            float scale = Mathf.Clamp(Mathf.Abs(slingshot.transform.lossyScale.x), 0.01f, 1f);
            GravityInfo defaults = MonkeGravityManager.DefaultGravityInfo;
            Vector3 acceleration = defaults.gravityUpDirection * defaults.gravityStrength;
            SlingshotProjectile held = slingshot.dummyProjectile != null ? slingshot.dummyProjectile.GetComponent<SlingshotProjectile>() : null;
            if (held != null && held.gravityController != null && held.gravityController.GravityZonesCount > 0)
            {
                Vector3 position = slingshot.GetLaunchPosition();
                acceleration = held.gravityController.ProcessGravityZones(in position).Item1;
            }
            return acceleration * multiplier * scale;
        }

        private static Vector3 ProjectileGravity(SlingshotProjectile projectile)
        {
            MonkeGravityController gravity = projectile.gravityController;
            GravityInfo defaults = MonkeGravityManager.DefaultGravityInfo;
            Vector3 acceleration = defaults.gravityUpDirection * defaults.gravityStrength;
            if (gravity != null)
            {
                if (gravity.GravityZonesCount > 0)
                {
                    Vector3 position = projectile.transform.position;
                    acceleration = gravity.ProcessGravityZones(in position).Item1;
                }
                acceleration *= gravity.GravityMultiplier;
            }
            if (projectile.forceComponent != null && projectile.forceComponent.enabled && projectile.projectileRigidbody != null)
                acceleration += (projectile.forceComponent.force + projectile.transform.TransformDirection(projectile.forceComponent.relativeForce))
                    / Mathf.Max(0.001f, projectile.projectileRigidbody.mass);
            return acceleration;
        }

        private static void DrawPath(int key, Vector3 origin, Vector3 velocity, Vector3 acceleration,
            VRRig owner, SlingshotProjectile projectile, bool local)
        {
            if (!PaintbrawlMath.Finite(origin) || !PaintbrawlMath.Finite(velocity) || !PaintbrawlMath.Finite(acceleration)) return;
            Path path;
            if (local)
            {
                if (localPreview == null) localPreview = CreatePath();
                path = localPreview;
            }
            else if (!paths.TryGetValue(key, out path))
            {
                path = sparePaths.Count != 0 ? sparePaths.Pop() : CreatePath();
                paths.Add(key, path);
            }
            path.Frame = frame;
            shown++;
            path.Line.enabled = true;
            path.Line.startColor = path.Line.endColor = local ? Color.white : new Color(0.72f, 0.72f, 0.72f, 0.72f);
            path.Line.startWidth = path.Line.endWidth = local ? 0.012f : 0.007f;
            path.Points[0] = origin;
            int pointCount = 1;
            Vector3 position = origin;
            float damping = projectile != null && projectile.projectileRigidbody != null ? projectile.projectileRigidbody.linearDamping : 0f;
            for (int step = 1; step < path.Points.Length; step++)
            {
                const float duration = 0.065f;
                Vector3 next = position + velocity * duration + acceleration * (duration * duration * 0.5f);
                if (NearestCollision(position, next, owner, projectile, out Vector3 impact))
                {
                    path.Points[pointCount++] = impact;
                    break;
                }
                path.Points[pointCount++] = next;
                position = next;
                velocity = (velocity + acceleration * duration) * Mathf.Clamp01(1f - damping * duration);
            }
            path.Line.positionCount = pointCount;
            for (int point = 0; point < pointCount; point++) path.Line.SetPosition(point, path.Points[point]);
        }

        private static bool NearestCollision(Vector3 start, Vector3 end, VRRig owner, SlingshotProjectile projectile, out Vector3 point)
        {
            point = end;
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < 0.000001f) return false;
            int count = Physics.RaycastNonAlloc(start, delta.normalized, hits, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null) continue;
                if (owner != null && collider.GetComponentInParent<VRRig>() == owner) continue;
                if (projectile != null && collider.GetComponentInParent<SlingshotProjectile>() == projectile) continue;
                if (hits[index].distance >= nearest) continue;
                nearest = hits[index].distance;
                point = hits[index].point;
            }
            return nearest != float.MaxValue;
        }

        private static Path CreatePath()
        {
            GameObject item = new GameObject("Interstellar_ProjectilePath");
            item.transform.SetParent(holder.transform, false);
            LineRenderer line = item.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return new Path { Line = line };
        }

        private static void EnsureResources()
        {
            if (holder != null) return;
            holder = new GameObject("Interstellar_ProjectileTrajectories");
            Object.DontDestroyOnLoad(holder);
            material = PlayerVisuals.CreateMaterial(Color.white, false);
        }

        public static void ClearRuntime()
        {
            if (holder != null) Object.Destroy(holder);
            if (material != null) Object.Destroy(material);
            holder = null;
            material = null;
            localPreview = null;
            paths.Clear();
            sparePaths.Clear();
            expired.Clear();
            refreshAfter = 0f;
        }

        public static void Shutdown()
        {
            aimbot = trajectories = false;
            restartAfter = 0f;
            ClearRuntime();
        }
    }
}
