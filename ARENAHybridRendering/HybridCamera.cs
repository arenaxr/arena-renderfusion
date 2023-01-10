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
    internal class WaitForCreateTrack : CustomYieldInstruction
    {
        public MediaStreamTrack Track { get { return m_track; } }

        MediaStreamTrack m_track;

        bool m_keepWaiting = true;

        public override bool keepWaiting { get { return m_keepWaiting; } }

        public WaitForCreateTrack() { }

        public void Done(MediaStreamTrack track)
        {
            m_track = track;
            m_keepWaiting = false;
        }
    }

    [RequireComponent(typeof(Camera))]
    public class HybridCamera : MonoBehaviour
    {
        static readonly Vector2Int videoSize = new Vector2Int(1280, 720);

        static readonly int s_defaultDepth = 16;

        public Camera m_camera;

        private Material m_material;

        private RenderTexture m_renderTexture;

        private Camera setCameraParams() {
            var cam = GetComponent<Camera>();
            cam.fieldOfView = 80f; // match arena
            cam.nearClipPlane = 0.1f; // match arena
            cam.farClipPlane = 10000f; // match arena
            cam.backgroundColor = Color.clear;
            cam.depthTextureMode = DepthTextureMode.Depth;
            return cam;
        }

        private void Awake()
        {
            if (Shader.Find("Hidden/RGBDepthShader") != null)
                m_material = new Material(Shader.Find("Hidden/RGBDepthShader"));
            else
                Debug.LogError("Cannot find required shader Hidden/RGBDepthShader!");

            m_camera = setCameraParams();
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

        internal WaitForCreateTrack CreateTrack(int screenWidth, int screenHeight)
        {
            CreateRenderTexture(screenWidth, screenHeight);
            var instruction = new WaitForCreateTrack();
            instruction.Done(new VideoStreamTrack(m_renderTexture));
            return instruction;
        }

        internal void SetRenderTextureOther(RenderTexture otherRendertexture)
        {
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
