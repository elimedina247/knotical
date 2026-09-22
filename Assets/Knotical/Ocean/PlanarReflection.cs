using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Knotical
{
    [DefaultExecutionOrder(200)]
    public class PlanarReflection : MonoBehaviour
    {
        private static readonly int TextureId = Shader.PropertyToID("_PlanarReflection");
        private static readonly int EnabledId = Shader.PropertyToID("_PlanarReflectionEnabled");

        [Range(0.1f, 1f)] public float Resolution = 0.5f;
        [Range(0f, 0.5f)] public float ClipOffset = 0.07f;
        public LayerMask Exclude = 1 << 4;
        public bool RenderShadows;
        public Camera Target;

        private Camera reflectionCamera;
        private RenderTexture texture;

        public RenderTexture Texture => texture;

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            Shader.SetGlobalFloat(EnabledId, 0f);
            if (reflectionCamera != null) Destroy(reflectionCamera.gameObject);
            if (texture != null) texture.Release();
            reflectionCamera = null;
            texture = null;
        }

        private void LateUpdate()
        {
            if (!Prepare()) return;
            reflectionCamera.enabled = true;
        }

        public void RenderNow()
        {
            if (!Prepare()) return;
            reflectionCamera.Render();
        }

        private bool Prepare()
        {
            if (Target == null) Target = Camera.main;
            if (Target == null) return false;

            EnsureCamera();
            EnsureTexture();

            float plane = Ocean.SeaLevel;
            var mirror = Matrix4x4.identity;
            mirror.m11 = -1f;
            mirror.m13 = 2f * plane;

            Transform t = Target.transform;
            Vector3 forward = t.forward;
            Vector3 up = t.up;
            forward.y = -forward.y;
            up.y = -up.y;
            reflectionCamera.transform.SetPositionAndRotation(mirror.MultiplyPoint(t.position), Quaternion.LookRotation(forward, up));

            reflectionCamera.fieldOfView = Target.fieldOfView;
            reflectionCamera.nearClipPlane = Target.nearClipPlane;
            reflectionCamera.farClipPlane = Target.farClipPlane;
            reflectionCamera.orthographic = Target.orthographic;
            reflectionCamera.orthographicSize = Target.orthographicSize;
            reflectionCamera.aspect = Target.aspect;
            reflectionCamera.cullingMask = Target.cullingMask & ~Exclude.value;
            reflectionCamera.depth = Target.depth - 1f;
            reflectionCamera.targetTexture = texture;

            Matrix4x4 view = Target.worldToCameraMatrix * mirror;
            reflectionCamera.worldToCameraMatrix = view;

            Vector3 offset = new Vector3(0f, plane, 0f) + Vector3.up * ClipOffset;
            Vector3 cameraPos = view.MultiplyPoint(offset);
            Vector3 cameraNormal = view.MultiplyVector(Vector3.up).normalized;
            var clip = new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPos, cameraNormal));
            reflectionCamera.projectionMatrix = Target.CalculateObliqueMatrix(clip);
            return true;
        }

        private void EnsureCamera()
        {
            if (reflectionCamera != null) return;

            var go = new GameObject("PlanarReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
            reflectionCamera = go.AddComponent<Camera>();
            reflectionCamera.enabled = false;
            reflectionCamera.clearFlags = CameraClearFlags.Skybox;
            reflectionCamera.allowMSAA = false;
            reflectionCamera.allowHDR = Target.allowHDR;
            reflectionCamera.useOcclusionCulling = false;

            UniversalAdditionalCameraData data = reflectionCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = RenderShadows;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.antialiasing = AntialiasingMode.None;
            data.allowXRRendering = false;
        }

        private void EnsureTexture()
        {
            int width = Mathf.Max(16, Mathf.RoundToInt(Target.pixelWidth * Resolution));
            int height = Mathf.Max(16, Mathf.RoundToInt(Target.pixelHeight * Resolution));
            if (texture != null && texture.width == width && texture.height == height) return;

            if (texture != null) texture.Release();
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.Default)
            {
                name = "PlanarReflection",
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.Create();
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == reflectionCamera)
            {
                GL.invertCulling = true;
                return;
            }

            bool active = cam == Target && texture != null;
            Shader.SetGlobalFloat(EnabledId, active ? 1f : 0f);
            if (active) Shader.SetGlobalTexture(TextureId, texture);
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == reflectionCamera) GL.invertCulling = false;
        }
    }
}
