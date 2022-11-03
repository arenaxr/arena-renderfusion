using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ArenaUnity.HybridRendering.Signaling;

namespace ArenaUnity.HybridRendering
{
    internal static class RTCRtpSenderExtension
    {
        public static RTCError SetFrameRate(this RTCRtpSender sender, uint framerate)
        {
            if (sender.Track.Kind != TrackKind.Video)
                throw new ArgumentException();

            RTCRtpSendParameters parameters = sender.GetParameters();
            foreach (var encoding in parameters.encodings)
            {
                encoding.maxFramerate = framerate;
            }
            return sender.SetParameters(parameters);
        }

        public static RTCError SetScaleResolutionDown(this RTCRtpSender sender, double? scaleFactor)
        {
            if (sender.Track.Kind != TrackKind.Video)
                throw new ArgumentException();

            RTCRtpSendParameters parameters = sender.GetParameters();
            foreach (var encoding in parameters.encodings)
            {
                encoding.scaleResolutionDownBy = scaleFactor;
            }
            return sender.SetParameters(parameters);
        }

        public static RTCError SetBitrate(this RTCRtpSender sender, uint? minBitrate, uint? maxBitrate)
        {
            RTCRtpSendParameters parameters = sender.GetParameters();

            foreach (var encoding in parameters.encodings)
            {
                encoding.minBitrate = minBitrate * 1000;
                encoding.maxBitrate = maxBitrate * 1000;
            }
            return sender.SetParameters(parameters);
        }
    }

    [RequireComponent(typeof(Camera))]
    public class CameraStream : MonoBehaviour
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

        static readonly Vector2Int videoSize = new Vector2Int(1280, 720);

        static readonly float s_defaultFrameRate = 60;
        static readonly float s_defaultScaleFactor = 1f;
        static readonly uint s_defaultMinBitrate = 100;
        static readonly uint s_defaultMaxBitrate = 100000;

        static readonly int s_defaultDepth = 16;

        private float m_FrameRate = s_defaultFrameRate;
        private float m_ScaleFactor = s_defaultScaleFactor;
        private uint m_MinBitrate = s_defaultMinBitrate;
        private uint m_MaxBitrate = s_defaultMaxBitrate;

        private Camera m_camera;

        private Material m_material;
        private RenderTexture m_renderTexture;

        private MediaStreamTrack m_track;

        private Dictionary<string, RTCRtpTransceiver> m_transceivers = new Dictionary<string, RTCRtpTransceiver>();

        public IReadOnlyDictionary<string, RTCRtpTransceiver> Transceivers => m_transceivers;

        public float frameRate
        {
            get { return m_FrameRate; }
        }

        public uint minBitrate
        {
            get { return m_MinBitrate; }
        }

        public uint maxBitrate
        {
            get { return m_MaxBitrate; }
        }

        public float scaleResolutionDown
        {
            get { return m_ScaleFactor; }
        }

        public MediaStreamTrack Track => m_track;

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

        private void OnDestroy()
        {
            m_track?.Dispose();
            m_track = null;

            if (m_renderTexture == null)
                return;
            m_camera.targetTexture = null;
            m_renderTexture.Release();
            Destroy(m_renderTexture);
            m_renderTexture = null;

            m_camera = null;
        }

        internal WaitForCreateTrack CreateTrack()
        {
            int width = videoSize.x;
            int height = videoSize.y;

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
                m_renderTexture = new RenderTexture(width, height, s_defaultDepth, format)
                {
                    antiAliasing = 2
                };
                m_renderTexture.Create();
                m_camera.targetTexture = m_renderTexture;
            }

            var instruction = new WaitForCreateTrack();
            instruction.Done(new VideoStreamTrack(m_renderTexture));
            return instruction;
        }

        internal void SetTrack(MediaStreamTrack newTrack)
        {
            m_track = newTrack;
        }

        public void SetFrameRate(float frameRate)
        {
            if (frameRate < 0)
                throw new ArgumentOutOfRangeException("frameRate", frameRate, "The parameter must be greater than zero.");
            m_FrameRate = frameRate;
            foreach (var transceiver in Transceivers.Values)
            {
                RTCError error = transceiver.Sender.SetFrameRate((uint)m_FrameRate);
                if (error.errorType != RTCErrorType.None)
                    throw new InvalidOperationException($"Set framerate is failed. {error.message}");
            }
        }

        public void SetBitrate(uint minBitrate, uint maxBitrate)
        {
            if (minBitrate > maxBitrate)
                throw new ArgumentException("The maxBitrate must be greater than minBitrate.", "maxBitrate");
            m_MinBitrate = minBitrate;
            m_MaxBitrate = maxBitrate;
            foreach (var transceiver in Transceivers.Values)
            {
                RTCError error = transceiver.Sender.SetBitrate(m_MinBitrate, m_MaxBitrate);
                if (error.errorType != RTCErrorType.None)
                    throw new InvalidOperationException($"Set codec is failed. {error.message}");
            }
        }

        public void SetScaleResolutionDown(float scaleFactor)
        {
            if (scaleFactor < 1.0f)
                throw new ArgumentOutOfRangeException("scaleFactor", scaleFactor, "The parameter must be greater than 1.0f. Scaleup is not allowed.");

            m_ScaleFactor = scaleFactor;
            foreach (var transceiver in Transceivers.Values)
            {
                double? value = Mathf.Approximately(m_ScaleFactor, 1) ? (double?)null : m_ScaleFactor;
                RTCError error = transceiver.Sender.SetScaleResolutionDown(value);
                if (error.errorType != RTCErrorType.None)
                    throw new InvalidOperationException($"Set codec is failed. {error.message}");
            }
        }

        public void SetTransceiver(string pcId, RTCRtpTransceiver transceiver)
        {
            if (transceiver == null)
            {
                m_transceivers.Remove(pcId);
                if (!m_transceivers.Any())
                {
                    m_track.Dispose();
                    m_track = null;
                }
            }
            else
            {
                m_transceivers.Add(pcId, transceiver);
            }
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

        // private void OnRenderImage(RenderTexture source, RenderTexture destination)
        // {
        //     if (m_camera != Camera.main)
        //     {
        //         Graphics.Blit(source, m_renderTexture, m_material);
        //     }
        //     else
        //     {
        //         Graphics.Blit(source, destination, m_material);
        //     }
        // }
    }
}
