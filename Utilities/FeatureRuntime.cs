using System;
using Interstellar.Managers;
using Interstellar.Mods;
using Interstellar.Mods.GameModes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Interstellar.Utilities
{
    [DefaultExecutionOrder(30000)]
    public sealed class FeatureRuntime : MonoBehaviour
    {
        private NetworkSystem observedNetwork;
        private string observedRoom;
        private bool initialized;
        private float nextError;
        private Action updateFeatures;
        private static readonly Action updatePlayerVisuals = PlayerVisuals.LateUpdate;
        private static readonly Action updatePaintbrawl = Paintbrawl.LateUpdate;
        private static readonly Action updateCameraTools = CameraTools.LateUpdate;

        private void OnEnable() => SceneManager.activeSceneChanged += SceneChanged;
        private void SceneChanged(Scene previous, Scene next) => ResetTransient();

        private void Update()
        {
            Run(updateFeatures ??= UpdateFeatures);
        }

        private void UpdateFeatures()
        {
            NetworkSystem network = NetworkSystem.Instance;
            string room = Room.IsConnected(network) ? network.RoomName : null;
            if (initialized && (observedNetwork != network || !string.Equals(observedRoom, room, StringComparison.Ordinal)))
                ResetTransient();
            initialized = true;
            observedNetwork = network;
            observedRoom = room;
            Traversal.ObserveContext();
            LocalTools.Tick();
            Fullbright.Tick();
        }

        private void LateUpdate()
        {
            Run(updatePlayerVisuals);
            Run(updatePaintbrawl);
            Run(updateCameraTools);
        }

        private void Run(Action update)
        {
            try { update(); }
            catch (Exception exception)
            {
                if (Time.unscaledTime < nextError) return;
                nextError = Time.unscaledTime + 5f;
                LogManager.LogError("Feature update failed: " + exception);
            }
        }

        public static void ResetTransient()
        {
            SafeCleanup(Traversal.ResetTransient);
            SafeCleanup(PlayerVisuals.ClearRuntime);
            SafeCleanup(Paintbrawl.ClearRuntime);
            SafeCleanup(LocalTools.ResetTransient);
        }

        public static void Shutdown()
        {
            SafeCleanup(Traversal.Cleanup);
            SafeCleanup(PlayerVisuals.Shutdown);
            SafeCleanup(Paintbrawl.Shutdown);
            SafeCleanup(CameraTools.Shutdown);
            SafeCleanup(LocalTools.Shutdown);
            SafeCleanup(Fullbright.Disable);
            SafeCleanup(JoinSettings.Shutdown);
        }

        private static void SafeCleanup(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception exception) { LogManager.LogError("Feature cleanup failed: " + exception); }
        }

        private void OnDisable() { SceneManager.activeSceneChanged -= SceneChanged; Shutdown(); }
        private void OnDestroy() => Shutdown();
    }
}
