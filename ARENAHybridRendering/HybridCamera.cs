using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using ArenaUnity.HybridRendering.Signaling;

namespace ArenaUnity.HybridRendering
{
    [RequireComponent(typeof(Camera))]
    public class HybridCamera : MonoBehaviour
    {
        static readonly Vector2Int videoSize = new Vector2Int(1280, 720);

        static readonly int s_defaultDepth = 16;

        private Camera m_camera;

        private Material m_material;

        private RenderTexture m_renderTexture;

        public bool isDualCamera;

        public void setCameraParams() {
            var cam = GetComponent<Camera>();
            cam.fieldOfView = 80f; // match arena
            cam.nearClipPlane = 0.1f; // match arena
            cam.farClipPlane = 10000f; // match arena
        }

        public void setCameraProj(float[] proj) {
            var cam = GetComponent<Camera>();
            float x = proj[0];
            float a = proj[1];
            float y = proj[2];
            float b = proj[3];
            float c = proj[4];
            float d = proj[5];
            float e = proj[6];

            Matrix4x4 p = new Matrix4x4();
            p[0, 0] = x;
            p[0, 1] = 0;
            p[0, 2] = a;
            p[0, 3] = 0;
            p[1, 0] = 0;
            p[1, 1] = y;
            p[1, 2] = b;
            p[1, 3] = 0;
            p[2, 0] = 0;
            p[2, 1] = 0;
            p[2, 2] = c;
            p[2, 3] = d;
            p[3, 0] = 0;
            p[3, 1] = 0;
            p[3, 2] = e;
            p[3, 3] = 0;
            cam.projectionMatrix = p;
        }

        private void Awake()
        {
            if (!GraphicsSettings.renderPipelineAsset)
            {
                if (Shader.Find("Hidden/RGBDepthShader") != null)
                    m_material = new Material(Shader.Find("Hidden/RGBDepthShader"));
                else
                    Debug.LogError("Cannot find required shader Hidden/RGBDepthShader!");
            }

            m_camera = GetComponent<Camera>();
            m_camera.backgroundColor = Color.clear;
            m_camera.depthTextureMode = DepthTextureMode.Depth;
        }

        private void OnDestroy()
        {
            if (m_renderTexture == null)
                return;
            m_camera.targetTexture = null;
            m_renderTexture.Release();
            Destroy(m_renderTexture);
            m_renderTexture = null;
        }

        internal RenderTexture CreateRenderTexture(int screenWidth, int screenHeight)
        {
            RenderTexture renderTexture;

            // int width = 2 * screenWidth;
            // int height = screenHeight;

            int width = 2 * videoSize.x;
            int height = (int)(videoSize.x * ((float)screenHeight / (float)screenWidth));

            // int width = 2 * videoSize.x;
            // int height = videoSize.y;

            if (m_camera.targetTexture != null)
            {
                renderTexture = m_camera.targetTexture;
                RenderTextureFormat supportFormat = WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType);
                GraphicsFormat graphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(supportFormat, RenderTextureReadWrite.Default);
                GraphicsFormat compatibleFormat = SystemInfo.GetCompatibleFormat(graphicsFormat, FormatUsage.Render);
                GraphicsFormat format = graphicsFormat == compatibleFormat ? graphicsFormat : compatibleFormat;

                if (renderTexture.graphicsFormat != format)
                {
                    Debug.LogWarning(
                        $"This color format:{renderTexture.graphicsFormat} not support in unity.webrtc. Change to supported color format:{format}.");
                    renderTexture.Release();
                    renderTexture.graphicsFormat = format;
                    renderTexture.Create();
                }

                m_camera.targetTexture = renderTexture;
            }
            else
            {
                RenderTextureFormat format = WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType);
                renderTexture = new RenderTexture(width, height, s_defaultDepth, format)
                {
                    antiAliasing = 1
                };
                renderTexture.Create();
                m_camera.targetTexture = renderTexture;
                m_camera.aspect = (float)(width / 2) / (float)height;
            }

            m_renderTexture = renderTexture;

            return renderTexture;
        }

        internal void SetRenderTextureOther(RenderTexture otherRendertexture)
        {
            if (GraphicsSettings.renderPipelineAsset) return;

            if (otherRendertexture != null)
            {
                m_material.SetInteger("_HasRightEyeTex", 1);
                m_material.SetTexture("_RightEyeTex", otherRendertexture);
            }
            else {
                m_material.SetInteger("_HasRightEyeTex", 0);
                m_material.SetTexture("_RightEyeTex", null);
            }
        }

        // private void OnPreRender()
        // {
        //     Shader.SetGlobalMatrix(Shader.PropertyToID("UNITY_MATRIX_IV"), Cam.cameraToWorldMatrix);
        // }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
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
