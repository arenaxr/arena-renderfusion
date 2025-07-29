using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using ArenaUnity.RenderFusion.Signaling;
using ArenaUnity.Schemas;

namespace ArenaUnity.RenderFusion
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
            get { return !m_clientCameraLeft.gameObject.activeSelf; }
        }

        private List<ClientPose> clientPoses = new List<ClientPose>();

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
            gobj.transform.parent = gameObject.transform; // new game object the child of this one
            gobj.transform.gameObject.AddComponent<Camera>();
            return gobj.transform.gameObject.AddComponent<ClientCamera>();
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
            bool isVRMode = clientStatus.isVRMode;
            bool isARMode = clientStatus.isARMode;
            bool hasDualCameras = clientStatus.hasDualCameras;

            m_clientCameraRight.gameObject.SetActive(hasDualCameras);
            if (!hasDualCameras)
            {
                m_clientCameraLeft.transform.localPosition = Vector3.zero;
                m_clientCameraRight.transform.localPosition = Vector3.zero;

                m_clientCameraLeft.setCameraParams();
                m_clientCameraRight.setCameraParams();

                if (!GraphicsSettings.renderPipelineAsset)
                {
                    m_clientCameraLeft.SetRenderTextureOther(null);
                }
                else
                {
                    m_clientCameraLeft.GetComponent<Camera>().rect = new Rect(new Vector2(0f, 0f), new Vector2(1f, 1f));
                    m_clientCameraRight.GetComponent<Camera>().targetTexture = null;

                    m_clientCameraLeft.GetComponent<ClientCamera>().IsDualCamera = false;
                    m_clientCameraRight.GetComponent<ClientCamera>().IsDualCamera = false;
                }
            }
            else
            {
                float ipd = clientStatus.ipd;
                var leftProj = clientStatus.leftProj;
                var rightProj = clientStatus.rightProj;

                m_clientCameraLeft.transform.localPosition = new Vector3(-ipd/2f, 0f, 0f);
                m_clientCameraRight.transform.localPosition = new Vector3(ipd/2f, 0f, 0f);

                if (leftProj != null && leftProj.Length > 0) m_clientCameraLeft.setCameraProjMatrix(leftProj);
                if (rightProj != null && rightProj.Length > 0) m_clientCameraRight.setCameraProjMatrix(rightProj);

                if (!GraphicsSettings.renderPipelineAsset)
                {
                    if (m_rightEyeTargetTexture)
                        m_clientCameraLeft.SetRenderTextureOther(m_rightEyeTargetTexture);
                }
                else
                {
                    m_clientCameraLeft.GetComponent<Camera>().rect = new Rect(new Vector2(0f, 0f), new Vector2(0.5f, 1f));
                    m_clientCameraLeft.GetComponent<ClientCamera>().IsDualCamera = true;

                    m_clientCameraRight.GetComponent<Camera>().targetTexture = m_renderTexture;
                    m_clientCameraRight.GetComponent<Camera>().rect = new Rect(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
                    m_clientCameraRight.GetComponent<ClientCamera>().IsDualCamera = true;
                }
            }
        }

        internal void CreateTrack(int width, int height, int screenWidth, int screenHeight)
        {
            StartCoroutine(CreateTrackCoroutine(width, height, screenWidth, screenHeight));
        }

        private IEnumerator CreateTrackCoroutine(int width, int height, int screenWidth, int screenHeight)
        {
            m_renderTexture = m_clientCameraLeft.CreateRenderTexture(width, height, screenWidth, screenHeight);
            WaitForCreateTrack op = CreateTrackInternal(m_renderTexture, width, height, screenWidth, screenHeight);

            if (op.Track == null)
                yield return op;

            // remove the current track, if it exists
            if (m_track != null)
            {
                m_track.Dispose();
                m_track = null;
            }
            m_track = op.Track;

            if (!GraphicsSettings.renderPipelineAsset)
                m_rightEyeTargetTexture = m_clientCameraRight.CreateRenderTexture(width, height, screenWidth, screenHeight);
        }

        internal WaitForCreateTrack CreateTrackInternal(RenderTexture rt, int width, int height, int screenWidth, int screenHeight)
        {
            var instruction = new WaitForCreateTrack();
            instruction.Done(new VideoStreamTrack(rt));
            return instruction;
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
            // Retrieves the 8 double values from the bytes array and copies into an array of doubles
            // 8 bytes per double * 8 values = 64 bytes
            const int cameraPoseNumElems = 17; // 16 elem pose matrix + 1 frameID
            double[] elems = new double[cameraPoseNumElems];
            Buffer.BlockCopy(bytes, 0, elems, 0, 8*cameraPoseNumElems);

            float[] floats = elems.Select(d => (float)d).ToArray();

            Matrix4x4 transformMatrix = new Matrix4x4();

            for (int i = 0; i < cameraPoseNumElems-1; i++) {
                int row = i % 4;
                int col = i / 4;
                transformMatrix[row, col] = floats[i];
            }

            int frameID = Convert.ToInt32(floats[cameraPoseNumElems-1]);
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

            var clientPose = new ClientPose(frameID, position, rotation, scale);
            clientPoses.Add(clientPose);
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
