using System;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ArenaUnity.HybridRendering.Signaling;

namespace ArenaUnity.HybridRendering
{
    [RequireComponent(typeof(Camera))]
    public class CameraStream : MonoBehaviour
    {
        private Camera m_camera;
        private Material m_material;
        public RenderTexture m_renderTexture;

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

        private void Awake()
        {
            m_camera = GetComponent<Camera>();
            m_camera.fieldOfView = 80f; // match arena
            m_camera.nearClipPlane = 0.1f; // match arena
            m_camera.farClipPlane = 10000f; // match arena
            // m_camera.backgroundColor = Color.clear;
            m_camera.depthTextureMode = DepthTextureMode.Depth;

            m_material = new Material(Shader.Find("Hidden/DepthShader"));
        }

        // Update is called once per frame
        private void Update()
        {

        }

        private void OnDestroy()
        {
            if (m_renderTexture == null)
                return;
            m_camera.targetTexture = null;
            m_renderTexture.Release();
            Destroy(m_renderTexture);
            m_renderTexture = null;
            m_camera = null;
        }

        internal WaitForCreateTrack CreateTrack(int width, int height)
        {
            if (m_camera.targetTexture != null)
            {
                m_renderTexture = m_camera.targetTexture;
                RenderTextureFormat supportFormat = WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType);
                GraphicsFormat graphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(supportFormat, RenderTextureReadWrite.Default);
                GraphicsFormat compatibleFormat = SystemInfo.GetCompatibleFormat(graphicsFormat, FormatUsage.Render);
                GraphicsFormat format = graphicsFormat == compatibleFormat ? graphicsFormat : compatibleFormat;

                if (m_renderTexture.graphicsFormat != format)
                {
                    Debug.LogWarning(
                        $"This color format:{m_renderTexture.graphicsFormat} not support in unity.webrtc. Change to supported color format:{format}.");
                    m_renderTexture.Release();
                    m_renderTexture.graphicsFormat = format;
                    m_renderTexture.Create();
                }

                m_camera.targetTexture = m_renderTexture;
            }
            else
            {
                RenderTextureFormat format = WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType);
                m_renderTexture = new RenderTexture(width, height, 16, format)
                {
                    antiAliasing = 1
                };
                m_renderTexture.Create();
                m_camera.targetTexture = m_renderTexture;
            }

            var instruction = new WaitForCreateTrack();
            instruction.Done(new VideoStreamTrack(m_renderTexture));
            return instruction;
        }

        public void UpdatePose(ClientPose clientPose)
        {
            // System.DateTime epochStart = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            // long currTime = (long)(System.DateTime.UtcNow - epochStart).TotalMilliseconds;
            // Debug.Log($"{currTime} {clientPose.ts} {currTime - clientPose.ts}");

            if (m_camera == null) return;

            m_camera.transform.position = ArenaUnity.ToUnityPosition(new Vector3(clientPose.x, clientPose.y, clientPose.z));
            m_camera.transform.localRotation = ArenaUnity.ToUnityRotationQuat(new Quaternion(
                clientPose.x_,
                clientPose.y_,
                clientPose.z_,
                clientPose.w_
            ));
        }

        // private void OnPreRender()
        // {
        //     Shader.SetGlobalMatrix(Shader.PropertyToID("UNITY_MATRIX_IV"), Cam.cameraToWorldMatrix);
        // }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // if (m_camera != Camera.main)
            // {
            //     Graphics.Blit(source, renderTexture, m_material);
            // }
            // else
            // {
                Graphics.Blit(source, destination, m_material);
            // }
        }
    }
}
