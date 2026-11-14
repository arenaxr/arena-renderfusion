using System;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArenaUnity.RenderFusion
{
    [RequireComponent(typeof(Camera))]
    public class ClientCamera : MonoBehaviour
    {
        static readonly int s_defaultDepth = 24;

        private Camera m_camera;

        private Material m_material;

        private RenderTexture m_renderTexture;

        public bool IsDualCamera;
        public int FrameID;

        public void setCameraParams()
        {
            var cam = GetComponent<Camera>();
            // Match ARENA's desktop camera. The 200 m video depth range is a
            // separate wire contract, independent of the camera's clip planes.
            cam.orthographic = false;
            cam.fieldOfView = 80f;
            cam.nearClipPlane = 0.005f;
            cam.farClipPlane = 10000f;
            // Changing FOV or clip fields does not clear an assigned XR matrix.
            // Restore automatic projection so aspect/viewport changes work again.
            cam.ResetProjectionMatrix();
        }

        internal const int ProjMatrixElems = 7;

        public void setCameraProjMatrix(float[] proj)
        {
            if (TryCreateProjection(proj, out Matrix4x4 projection, out float near, out float far))
                ApplyProjection(projection, near, far);
        }

        internal static bool TryCreateProjection(float[] proj, out Matrix4x4 projection, out float near, out float far)
        {
            projection = default;
            near = far = 0f;
            if (proj == null || proj.Length < ProjMatrixElems)
                return false;

            for (int i = 0; i < ProjMatrixElems; i++)
                if (float.IsNaN(proj[i]) || float.IsInfinity(proj[i]))
                    return false;

            float horizontalScale = proj[0] * 0.5f;
            if (horizontalScale <= 0f || proj[2] <= 0f || proj[6] >= 0f)
                return false;

            // OpenGL/WebXR maps the near/far planes to NDC -1/+1. Derive the
            // clip fields from the coefficients actually received, not guessed
            // device defaults. Double arithmetic avoids further cancellation;
            // it cannot recover precision already lost in an XR Float32 matrix.
            double c = proj[4], d = proj[5], e = proj[6];
            double nearDistance = d / (c + e);
            double farDistance = d / (c - e);
            if (double.IsNaN(nearDistance) || double.IsInfinity(nearDistance) ||
                double.IsNaN(farDistance) || double.IsInfinity(farDistance) ||
                nearDistance <= 0d || farDistance <= nearDistance)
                return false;

            near = (float)nearDistance;
            far = (float)farDistance;
            if (float.IsInfinity(near) || float.IsInfinity(far) || near <= 0f || far <= near)
                return false;

            // Each eye is rendered at twice its transmitted color width. Halve
            // both x scale and off-axis x offset before the middle-half crop;
            // preserve the supplied vertical and depth projection coefficients.
            projection[0, 0] = horizontalScale;
            projection[0, 2] = proj[1] * 0.5f;
            projection[1, 1] = proj[2];
            projection[1, 2] = proj[3];
            projection[2, 2] = proj[4];
            projection[2, 3] = proj[5];
            projection[3, 2] = proj[6];
            return true;
        }

        internal void ApplyProjection(Matrix4x4 projection, float near, float far)
        {
            var cam = GetComponent<Camera>();
            cam.orthographic = false;
            // Keep the intermediate clip interval valid when changing between
            // disjoint frusta, before installing their custom projection.
            if (near < cam.nearClipPlane)
            {
                cam.nearClipPlane = near;
                cam.farClipPlane = far;
            }
            else
            {
                cam.farClipPlane = far;
                cam.nearClipPlane = near;
            }
            cam.projectionMatrix = projection;
        }

        private void Awake()
        {
            m_camera = GetComponent<Camera>();
            setCameraParams();
            if (!GraphicsSettings.defaultRenderPipeline)
            {
                if (Shader.Find("Hidden/RGBDepth") != null)
                    m_material = new Material(Shader.Find("Hidden/RGBDepth"));
                else
                    throw new InvalidOperationException("Cannot find required shader Hidden/RGBDepth!");
            }

            m_camera.backgroundColor = Color.clear;
            m_camera.depthTextureMode = DepthTextureMode.Depth;
        }

        private void OnDestroy()
        {
            if (m_material != null)
            {
                DestroyOwnedResource(m_material);
                m_material = null;
            }

            ReleaseRenderTexture();
        }

        private void ReleaseRenderTexture()
        {
            if (m_renderTexture == null)
                return;

            // A stereo camera can borrow another camera's target. Detach only the
            // target owned here, never a replacement supplied by another component.
            if (m_camera != null && m_camera.targetTexture == m_renderTexture)
                m_camera.targetTexture = null;
            m_renderTexture.Release();
            DestroyOwnedResource(m_renderTexture);
            m_renderTexture = null;
        }

        private static void DestroyOwnedResource(UnityEngine.Object resource)
        {
            if (Application.isPlaying)
                Destroy(resource);
            else
                DestroyImmediate(resource);
        }

        internal RenderTexture CreateRenderTexture(int width, int height, int screenWidth, int screenHeight)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Stream dimensions must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Stream dimensions must be positive.");

            // A frame carries color and depth side by side. Clamp before doubling
            // to avoid overflowing integer arithmetic for invalid remote settings.
            width = 2 * Mathf.Min(width, 2048);
            height = Mathf.Min(height, 4096);
            RenderTextureFormat format = WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType);

            if (m_renderTexture == null || m_renderTexture.width != width ||
                m_renderTexture.height != height || m_renderTexture.format != format)
            {
                ReleaseRenderTexture();
                m_renderTexture = new RenderTexture(width, height, s_defaultDepth, format)
                {
                    antiAliasing = 2
                };
                m_renderTexture.Create();
            }

            // Never adopt camera.targetTexture: it may be an asset or a borrowed
            // stereo target. This component releases only its own allocation.
            m_camera.targetTexture = m_renderTexture;
            return m_renderTexture;
        }

        internal void SetRenderTextureOther(RenderTexture otherRendertexture)
        {
            if (GraphicsSettings.defaultRenderPipeline) return;

            if (otherRendertexture != null)
            {
                m_material.SetInteger("_HasRightEyeTex", 1);
                m_material.SetTexture("_RightEyeTex", otherRendertexture);
            }
            else
            {
                m_material.SetInteger("_HasRightEyeTex", 0);
                m_material.SetTexture("_RightEyeTex", null);
            }
        }

        internal void SetFrameID(int frameID)
        {
            FrameID = frameID;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (m_material != null)
            {
                m_material.SetInteger("_FrameID", FrameID);
            }

            if (m_camera != Camera.main)
            {
                Graphics.Blit(source, m_renderTexture, m_material);
            }
            else
            {
                Graphics.Blit(source, destination, m_material);
            }
        }
    }
}
