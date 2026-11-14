using System;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Rendering;
using ArenaUnity.RenderFusion.Signaling;

namespace ArenaUnity.RenderFusion
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

    public class CameraStream : MonoBehaviour
    {
        static readonly float s_defaultFrameRate = 60;
        static readonly float s_defaultScaleFactor = 1.0f;
        static readonly uint s_defaultMinBitrate = 20000;
        static readonly uint s_defaultMaxBitrate = 100000;

        private float m_FrameRate = s_defaultFrameRate;
        private float m_ScaleFactor = s_defaultScaleFactor;
        private uint m_MinBitrate = s_defaultMinBitrate;
        private uint m_MaxBitrate = s_defaultMaxBitrate;

        private ClientCamera m_clientCameraLeft;
        private ClientCamera m_clientCameraRight;

        private RenderTexture m_renderTexture;
        private RenderTexture m_rightEyeTargetTexture;

        private MediaStreamTrack m_track;

        public bool hasDualCameras
        {
            get { return m_clientCameraRight != null && m_clientCameraRight.gameObject.activeSelf; }
        }

        // Input messages can arrive faster than Unity renders. Only the newest pose
        // should be applied, together with the frame ID the browser assigned to it.
        private ClientPose m_pendingPose;

        private Dictionary<string, RTCRtpTransceiver> m_transceivers = new Dictionary<string, RTCRtpTransceiver>();

        public IReadOnlyDictionary<string, RTCRtpTransceiver> Transceivers => m_transceivers;

        public float FrameRate
        {
            get { return m_FrameRate; }
        }

        public uint MinBitrate
        {
            get { return m_MinBitrate; }
        }

        public uint MaxBitrate
        {
            get { return m_MaxBitrate; }
        }

        public float ScaleResolutionDown
        {
            get { return m_ScaleFactor; }
        }

        public MediaStreamTrack Track => m_track;

        private ClientCamera AddCamera(string identifier)
        {
            var gobj = new GameObject($"camera-{identifier}");
            gobj.transform.SetParent(transform, false);
            return gobj.AddComponent<ClientCamera>();
        }

        private void Awake()
        {
            m_clientCameraLeft = AddCamera("left");
            m_clientCameraRight = AddCamera("right");
            // disable stereo cameras at first
            m_clientCameraRight.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            m_track?.Dispose();
            m_track = null;
        }

        public void UpdateStateWithStatus(ClientStatus clientStatus)
        {
            if (clientStatus == null)
                return;

            bool hasDualCameras = clientStatus.hasDualCameras;

            Matrix4x4 leftProjection = default, rightProjection = default;
            float leftNear = 0f, leftFar = 0f, rightNear = 0f, rightFar = 0f;
            if (hasDualCameras && (float.IsNaN(clientStatus.ipd) || float.IsInfinity(clientStatus.ipd) ||
                clientStatus.ipd < 0f ||
                !ClientCamera.TryCreateProjection(clientStatus.leftProj, out leftProjection, out leftNear, out leftFar) ||
                !ClientCamera.TryCreateProjection(clientStatus.rightProj, out rightProjection, out rightNear, out rightFar)))
                return;

            m_clientCameraRight.gameObject.SetActive(hasDualCameras);
            m_clientCameraLeft.IsDualCamera = hasDualCameras;
            m_clientCameraRight.IsDualCamera = hasDualCameras;
            if (!hasDualCameras)
            {
                m_clientCameraLeft.transform.localPosition = Vector3.zero;
                m_clientCameraRight.transform.localPosition = Vector3.zero;

                m_clientCameraLeft.setCameraParams();
                m_clientCameraRight.setCameraParams();
                m_clientCameraLeft.GetComponent<Camera>().rect = new Rect(0f, 0f, 1f, 1f);
                m_clientCameraRight.GetComponent<Camera>().rect = new Rect(0f, 0f, 1f, 1f);

                if (!GraphicsSettings.defaultRenderPipeline)
                {
                    m_clientCameraLeft.SetRenderTextureOther(null);
                }
                else
                {
                    m_clientCameraRight.GetComponent<Camera>().targetTexture = null;
                }
            }
            else
            {
                float ipd = clientStatus.ipd;
                m_clientCameraLeft.transform.localPosition = new Vector3(-ipd/2f, 0f, 0f);
                m_clientCameraRight.transform.localPosition = new Vector3(ipd/2f, 0f, 0f);

                m_clientCameraLeft.ApplyProjection(leftProjection, leftNear, leftFar);
                m_clientCameraRight.ApplyProjection(rightProjection, rightNear, rightFar);

                if (!GraphicsSettings.defaultRenderPipeline)
                {
                    if (m_rightEyeTargetTexture)
                        m_clientCameraLeft.SetRenderTextureOther(m_rightEyeTargetTexture);
                }
                else
                {
                    m_clientCameraLeft.GetComponent<Camera>().rect = new Rect(new Vector2(0f, 0f), new Vector2(0.5f, 1f));

                    m_clientCameraRight.GetComponent<Camera>().targetTexture = m_renderTexture;
                    m_clientCameraRight.GetComponent<Camera>().rect = new Rect(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
                }
            }
        }

        internal void CreateTrack(int width, int height, int screenWidth, int screenHeight)
        {
            // VideoStreamTrack creation is synchronous. A coroutine here concealed
            // the requirement that AddSender receives a completed track immediately.
            m_track?.Dispose();
            m_track = null;
            m_renderTexture = m_clientCameraLeft.CreateRenderTexture(width, height, screenWidth, screenHeight);
            m_track = new VideoStreamTrack(m_renderTexture);

            if (!GraphicsSettings.defaultRenderPipeline)
            {
                m_rightEyeTargetTexture = m_clientCameraRight.CreateRenderTexture(width, height, screenWidth, screenHeight);
                m_clientCameraLeft.SetRenderTextureOther(hasDualCameras ? m_rightEyeTargetTexture : null);
            }
            else if (hasDualCameras)
            {
                // Status may have enabled stereo before the shared target existed.
                m_clientCameraRight.GetComponent<Camera>().targetTexture = m_renderTexture;
            }
        }

        public void SetFrameRate(float frameRate)
        {
            if (float.IsNaN(frameRate) || float.IsInfinity(frameRate) || frameRate < 1f || (double)frameRate > uint.MaxValue)
                throw new ArgumentOutOfRangeException("frameRate", frameRate, "Frame rate must be finite and between 1 and UInt32.MaxValue.");
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
            if (float.IsNaN(scaleFactor) || float.IsInfinity(scaleFactor) || scaleFactor < 1.0f)
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
                if (m_transceivers.Count == 0)
                {
                    m_track?.Dispose();
                    m_track = null;
                }
            }
            else
            {
                m_transceivers[pcId] = transceiver;
            }
        }

        private const int cameraPoseNumElems = 17; // 16 elem pose matrix + 1 frameID
        private readonly double[] m_poseElems = new double[cameraPoseNumElems];

        public void OnInputMessage(byte[] bytes)
        {
            // The wire payload is 16 column-major matrix doubles and one frame ID.
            if (bytes == null || bytes.Length < 8 * cameraPoseNumElems)
            {
                Debug.LogWarning($"Ignoring malformed client pose message: {bytes?.Length ?? 0} bytes, expected at least {8 * cameraPoseNumElems}.");
                return;
            }

            // reused across messages: this runs on the main thread at the client frame rate
            Buffer.BlockCopy(bytes, 0, m_poseElems, 0, 8*cameraPoseNumElems);

            for (int i = 0; i < cameraPoseNumElems; i++)
            {
                if (double.IsNaN(m_poseElems[i]) || double.IsInfinity(m_poseElems[i]))
                    return;
            }

            double frameId = m_poseElems[cameraPoseNumElems - 1];
            if (frameId < int.MinValue || frameId > int.MaxValue)
                return;

            Matrix4x4 transformMatrix = new Matrix4x4();

            for (int i = 0; i < cameraPoseNumElems-1; i++) {
                int row = i % 4;
                int col = i / 4;
                transformMatrix[row, col] = (float)m_poseElems[i];
            }

            // Converting the ID through float loses integer precision after 2^24.
            int frameID = Convert.ToInt32(frameId);
            Vector3 position = transformMatrix.GetColumn(3);
            Quaternion rotation = Quaternion.LookRotation(
                                        transformMatrix.GetColumn(2),
                                        transformMatrix.GetColumn(1)
                                    );
            Vector3 scale = new Vector3(
                                transformMatrix.GetColumn(0).magnitude,
                                transformMatrix.GetColumn(1).magnitude,
                                transformMatrix.GetColumn(2).magnitude
                            );

            m_pendingPose = new ClientPose(frameID, position, rotation, scale);
        }

        private void updatePose(ClientPose clientPose)
        {
            m_clientCameraLeft.SetFrameID(clientPose.id);
            m_clientCameraRight.SetFrameID(clientPose.id);

            //Only takes floats so we convert at this point
            gameObject.transform.position = ArenaUnity.ToUnityPosition(clientPose.position);
            gameObject.transform.localRotation = ArenaUnity.ToUnityRotationQuat(clientPose.rotation);
        }

        private void Update()
        {
            if (m_pendingPose == null)
                return;

            updatePose(m_pendingPose);
            m_pendingPose = null;
        }
    }
}
