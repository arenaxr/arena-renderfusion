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

    // [RequireComponent(typeof(Camera))]
    public class CameraStream : MonoBehaviour
    {
        static readonly float s_defaultFrameRate = 60;
        static readonly float s_defaultScaleFactor = 1.0f;
        static readonly uint s_defaultMinBitrate = 100;
        static readonly uint s_defaultMaxBitrate = 100000;

        private float m_FrameRate = s_defaultFrameRate;
        private float m_ScaleFactor = s_defaultScaleFactor;
        private uint m_MinBitrate = s_defaultMinBitrate;
        private uint m_MaxBitrate = s_defaultMaxBitrate;

        private HybridCamera m_hybridCameraLeft;
        private HybridCamera m_hybridCameraRight;

        private RenderTexture m_rightEyeTargetTexture;

        private MediaStreamTrack m_track;

        public bool hasDualCameras
        {
            get { return !m_hybridCameraLeft.gameObject.activeSelf; }
        }

        private List<ClientPose> clientPoses = new List<ClientPose>();

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

        private HybridCamera addCamera(string identifier)
        {
            var gobj = new GameObject($"camera-{identifier}");
            gobj.transform.parent = gameObject.transform; // new game object the child of this one
            gobj.transform.gameObject.AddComponent<Camera>();
            return gobj.transform.gameObject.AddComponent<HybridCamera>();
        }

        private void Awake()
        {
            m_hybridCameraLeft = addCamera("left");
            m_hybridCameraRight = addCamera("right");
            // disable stereo cameras at first
            m_hybridCameraRight.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            m_track?.Dispose();
            m_track = null;
        }

        public void SetHasDualCameras(bool active, float ipd=0.67f)
        {
            m_hybridCameraRight.gameObject.SetActive(active);
            if (!active)
            {
                m_hybridCameraLeft.transform.localPosition = ArenaUnity.ToUnityPosition(Vector3.zero);
                m_hybridCameraLeft.SetRenderTextureOther(null);
            }
            else if (m_rightEyeTargetTexture)
            {
                m_hybridCameraLeft.transform.localPosition = ArenaUnity.ToUnityPosition(new Vector3(-ipd/2f, 0f, 0f));
                m_hybridCameraRight.transform.localPosition = ArenaUnity.ToUnityPosition(new Vector3(ipd/2f, 0f, 0f));
                m_hybridCameraLeft.SetRenderTextureOther(m_rightEyeTargetTexture);
            }
        }

        internal void CreateTrack(int screenWidth, int screenHeight)
        {
            StartCoroutine(CreateTrackCoroutine(screenWidth, screenHeight));
        }

        private IEnumerator CreateTrackCoroutine(int screenWidth, int screenHeight)
        {
            WaitForCreateTrack op = m_hybridCameraLeft.CreateTrack(screenWidth, screenHeight);

            if (op.Track == null)
                yield return op;

            // remove the current track, if it exists
            if (m_track != null)
            {
                m_track.Dispose();
                m_track = null;
            }
            m_track = op.Track;

            m_rightEyeTargetTexture = m_hybridCameraRight.CreateRenderTexture(screenWidth, screenHeight);
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
                    m_track?.Dispose();
                    m_track = null;
                }
            }
            else
            {
                // if there already exists a transceiver, just remove it
                RTCRtpTransceiver trans;
                if (m_transceivers.TryGetValue(pcId, out trans))
                {
                    m_transceivers.Remove(pcId);
                }
                m_transceivers.Add(pcId, transceiver);
            }
        }

        public void OnInputMessage(byte[] bytes)
        {
            string poseMsg = System.Text.Encoding.UTF8.GetString(bytes);
            var clientPose = JsonUtility.FromJson<ClientPose>(poseMsg);

            clientPoses.Add(clientPose);
        }

        private void updatePose(ClientPose clientPose)
        {
            gameObject.transform.position = ArenaUnity.ToUnityPosition(new Vector3(
                clientPose.x,
                clientPose.y,
                clientPose.z
            ));
            gameObject.transform.localRotation = ArenaUnity.ToUnityRotationQuat(new Quaternion(
                clientPose.x_,
                clientPose.y_,
                clientPose.z_,
                clientPose.w_
            ));
        }

        private void Update()
        {
            foreach (var clientPose in clientPoses)
            {
                // System.DateTime epochStart = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
                // long currTime = (long)(System.DateTime.UtcNow - epochStart).TotalMilliseconds;
                // Debug.Log($"{currTime} {clientPose.ts} {currTime - clientPose.ts}");
                updatePose(clientPose);
            }
            clientPoses.Clear();
        }
    }
}
