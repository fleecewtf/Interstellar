using UnityEngine;
using UnityEngine.Rendering;

namespace Interstellar.Utilities
{
    [DefaultExecutionOrder(11000)]
    public sealed class GunVisualUpdater : MonoBehaviour
    {
        private bool subscribed;

        private void OnEnable()
        {
            if (subscribed) return;
            Camera.onPreCull += PrepareCamera;
            RenderPipelineManager.beginCameraRendering += PreparePipelineCamera;
            subscribed = true;
        }

        private void LateUpdate() => GunUtilities.RefreshVisuals();

        private void PrepareCamera(Camera camera)
        {
            if (camera != null && camera.cameraType == CameraType.Game)
                GunUtilities.RefreshVisuals();
        }

        private void PreparePipelineCamera(ScriptableRenderContext context, Camera camera) => PrepareCamera(camera);

        private void Release()
        {
            if (!subscribed) return;
            Camera.onPreCull -= PrepareCamera;
            RenderPipelineManager.beginCameraRendering -= PreparePipelineCamera;
            subscribed = false;
            GunUtilities.DisposeVisuals();
        }

        private void OnDisable() => Release();

        private void OnDestroy() => Release();
    }
}
