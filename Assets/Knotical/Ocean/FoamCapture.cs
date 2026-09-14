using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Knotical
{
    public class FoamCapture : MonoBehaviour
    {
        public const string LayerName = "FoamCapture";

        private static readonly int CaptureId = Shader.PropertyToID("_FoamCapture");
        private static readonly int ParamsId = Shader.PropertyToID("_FoamCaptureParams");
        private static readonly int ShiftId = Shader.PropertyToID("_Shift");
        private static readonly int DecayId = Shader.PropertyToID("_Decay");

        [SerializeField, Range(128, 2048)] private int resolution = 1024;
        [SerializeField, Range(64f, 2048f)] private float extent = 256f;
        [SerializeField, Range(0.5f, 30f)] private float fadeSeconds = 9f;
        [SerializeField, Range(0f, 2f)] private float strength = 1f;
        [SerializeField] private Transform follow;

        private RenderTexture front;
        private RenderTexture back;
        private Camera captureCamera;
        private Material decay;
        private Vector2 centre;
        private bool centred;

        public static int Layer => LayerMask.NameToLayer(LayerName);

        private void OnEnable()
        {
            front = CreateTarget();
            back = CreateTarget();
            decay = new Material(Shader.Find("Knotical/FoamDecay"));
            CreateCamera();
            centred = false;
        }

        private void OnDisable()
        {
            Shader.SetGlobalTexture(CaptureId, Texture2D.blackTexture);
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
            if (captureCamera != null) Destroy(captureCamera.gameObject);
            if (front != null) front.Release();
            if (back != null) back.Release();
            if (decay != null) Destroy(decay);
        }

        private void LateUpdate()
        {
            Transform target = follow;
            Camera main = Camera.main;
            if (main != null && Layer >= 0) main.cullingMask &= ~(1 << Layer);
            if (target == null && main != null) target = main.transform;
            if (target == null) return;

            float texel = 2f * extent / resolution;
            var wanted = new Vector2(Mathf.Round(target.position.x / texel) * texel, Mathf.Round(target.position.z / texel) * texel);
            Vector2 shift = centred ? (wanted - centre) / (2f * extent) : Vector2.zero;
            centre = wanted;
            centred = true;

            decay.SetVector(ShiftId, shift);
            decay.SetFloat(DecayId, Mathf.Exp(-Time.deltaTime / fadeSeconds));
            Graphics.Blit(front, back, decay);
            (front, back) = (back, front);

            captureCamera.targetTexture = front;
            captureCamera.orthographicSize = extent;
            captureCamera.transform.position = new Vector3(centre.x, Ocean.SeaLevel + 100f, centre.y);

            Shader.SetGlobalTexture(CaptureId, front);
            Shader.SetGlobalVector(ParamsId, new Vector4(centre.x, centre.y, extent, strength));
        }

        private RenderTexture CreateTarget()
        {
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
            var rt = new RenderTexture(resolution, resolution, 16, format, RenderTextureReadWrite.Linear)
            {
                name = "FoamCapture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            rt.Create();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
            return rt;
        }

        private void CreateCamera()
        {
            int layer = Layer;
            if (layer < 0)
            {
                Debug.LogError($"FoamCapture: add a layer named {LayerName} in Tags and Layers.");
            }

            var go = new GameObject("FoamCaptureCamera");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            captureCamera = go.AddComponent<Camera>();
            captureCamera.orthographic = true;
            captureCamera.orthographicSize = extent;
            captureCamera.nearClipPlane = 1f;
            captureCamera.farClipPlane = 200f;
            captureCamera.cullingMask = layer >= 0 ? 1 << layer : 0;
            captureCamera.clearFlags = CameraClearFlags.Nothing;
            captureCamera.depth = -10f;
            captureCamera.allowHDR = false;
            captureCamera.allowMSAA = false;
            captureCamera.useOcclusionCulling = false;
            captureCamera.targetTexture = front;

            UniversalAdditionalCameraData data = captureCamera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.antialiasing = AntialiasingMode.None;
            data.volumeLayerMask = 0;
        }
    }
}
