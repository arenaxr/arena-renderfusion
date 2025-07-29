using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using ArenaUnity.RenderFusion.Signaling;
using Unity.Profiling;

namespace ArenaUnity.RenderFusion
{
    internal class PeerConnection : IDisposable
    {
        static readonly string[] excludeCodecMimeType = { "video/red", "video/ulpfec", "video/rtx" };

        private readonly string CLIENT_INPUT_DC_LABEL = "client-input";
        private readonly string CLIENT_STATUS_DC_LABEL = "client-status";

        private string m_id;
        private string m_clientId;

        private int m_screenWidth;
        private int m_screenHeight;

        // Used by RenderFusion
        public int missedHeartbeats = 0;

        private ISignaling m_signaler;

        private CameraStream camStream;

        private RTCPeerConnection _peer;

        private RTCDataChannel clientInputDataChannel;
        private RTCDataChannel clientStatusDataChannel;

        private MediaStream sourceStream;

        private GameObject gobj;

        private ProfilerRecorder mainThreadTimeRecorder;

        private readonly Func<IEnumerator, Coroutine> _startCoroutine;

        public string Id { get { return m_id; } }
        public RTCPeerConnection peer => _peer;

        public PeerConnection(RTCPeerConnection peer, ConnectData data, ISignaling signaler, Func<IEnumerator, Coroutine> startCoroutine)
        {
            m_signaler = signaler;
            m_clientId = data.id;
            m_screenWidth = data.screenWidth;
            m_screenHeight = data.screenHeight;
            _startCoroutine = startCoroutine;

            m_id = System.Guid.NewGuid().ToString();
            Debug.Log($"New Peer: (ID: {m_clientId}) - {data.deviceType}");

            _peer = peer;
            _peer.OnNegotiationNeeded = () => StartCoroutine(OnNegotiationNeeded());
            _peer.OnIceCandidate = candidate => m_signaler.SendCandidate(m_id, m_clientId, candidate);
            _peer.OnDataChannel = OnDataChannel;

            sourceStream = new MediaStream();

            gobj = new GameObject($"rf_client_{m_clientId}");
            camStream = gobj.AddComponent<CameraStream>();

            mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        }

        ~PeerConnection()
        {
            Dispose();
        }

        private void StartCoroutine(IEnumerator enumerator)
        {
            _startCoroutine(enumerator);
        }

        public void Dispose()
        {
            _peer.OnDataChannel = null;
            _peer.OnIceCandidate = null;
            _peer.OnNegotiationNeeded = null;
            _peer.OnConnectionStateChange = null;
            _peer.OnIceConnectionChange = null;
            _peer.OnIceGatheringStateChange = null;
            _peer.Dispose();
            _peer = null;

            mainThreadTimeRecorder.Dispose();

            UnityEngine.Object.Destroy(gobj);

            Debug.Log($"Peer (cID: {m_clientId}, ID: {m_id}) killed");

            GC.SuppressFinalize(this);
        }

        private RTCRtpTransceiverInit GetTransceiverInit()
        {
            RTCRtpTransceiverInit init = new RTCRtpTransceiverInit()
            {
                direction = RTCRtpTransceiverDirection.SendOnly,
            };
            init.sendEncodings = new RTCRtpEncodingParameters[]
            {
                new RTCRtpEncodingParameters()
                {
                    active = true,
                    minBitrate = (ulong?)camStream.MinBitrate * 1000,
                    maxBitrate = (ulong?)camStream.MaxBitrate * 1000,
                    maxFramerate = (uint?)camStream.FrameRate,
                    scaleResolutionDownBy = camStream.ScaleResolutionDown
                }
            };

            return init;
        }

        private string GetProfileFromFmtp(string fmtp)
        {
            if (string.IsNullOrEmpty(fmtp)) return "unknown";

            var parts = fmtp.Split(';');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("profile-level-id="))
                {
                    return trimmed.Substring("profile-level-id=".Length);
                }
            }
            return "unknown";
        }

        public void AddSender(int width, int height, bool dualCameraMode = false)
        {
            ClientStatus clientStatus = new ClientStatus
            {
                hasDualCameras = dualCameraMode,
            };
            camStream.UpdateStateWithStatus(clientStatus);
            camStream.CreateTrack(width, height, m_screenWidth, m_screenHeight);

            RTCRtpTransceiverInit init = GetTransceiverInit();
            var transceiver = _peer.AddTransceiver(camStream.Track, init);

            var capabilities = RTCRtpSender.GetCapabilities(TrackKind.Video);
            var codecs = capabilities.codecs.Where(codec => !excludeCodecMimeType.Contains(codec.mimeType));
            // var codecs = capabilities.codecs.Where(codec => codec.mimeType == "video/H264");
            var codecStrings = codecs.Select(c =>
            {
                var profile = GetProfileFromFmtp(c.sdpFmtpLine);
                return string.IsNullOrEmpty(profile) || profile == "unknown"
                    ? c.mimeType
                    : c.mimeType + " (profile: " + profile + ")";
            });
            Debug.Log("Available video codecs: " + string.Join(", ", codecStrings));

            var error = transceiver.SetCodecPreferences(codecs.ToArray());
            if (error != RTCErrorType.None)
            {
                Debug.LogError($"Failed to set codec preferences: {error}");
                return;
            }

            camStream.SetTransceiver(m_id, transceiver);
        }

        private void OnDataChannel(RTCDataChannel channel)
        {
            if (channel.Label == CLIENT_INPUT_DC_LABEL)
            {
                clientInputDataChannel = channel;
                clientInputDataChannel.OnMessage = bytes => camStream.OnInputMessage(bytes);
            }
            else if (channel.Label == CLIENT_STATUS_DC_LABEL)
            {
                clientStatusDataChannel = channel;
                clientStatusDataChannel.OnMessage = bytes => OnClientStatusChange(bytes);
            }
        }

        private void OnClientStatusChange(byte[] bytes)
        {
            string statusMsg = System.Text.Encoding.UTF8.GetString(bytes);
            var clientStatus = JsonUtility.FromJson<ClientStatus>(statusMsg);
            camStream.UpdateStateWithStatus(clientStatus);
            // Debug.Log(statusMsg);
        }

        public IEnumerator OnNegotiationNeeded()
        {
            // Debug.Log($"[{m_clientId}] creating offer.");
            var op = _peer.CreateOffer();
            yield return op;

            if (!op.IsError)
            {
                if (_peer.SignalingState != RTCSignalingState.Stable)
                {
                    Debug.LogError("pc signaling state is not stable.");
                    yield break;
                }

                RTCSessionDescription desc = op.Desc;
                var op1 = _peer.SetLocalDescription(ref desc);
                yield return op1;

                if (!op1.IsError)
                {
                    // Debug.Log($"[{m_clientId}] sent offer.");
                    m_signaler.SendOffer(m_id, m_clientId, _peer.LocalDescription);
                }
            }
            else
            {
                CreateDescriptionError(op.Error);
            }
        }

        public IEnumerator CreateAndSendAnswerCoroutine(SDPData offer)
        {
            // Debug.Log($"[{m_clientId}] creating answer.");
            RTCSessionDescription description;
            description.type = RTCSdpType.Offer;
            description.sdp = offer.sdp;

            var opRemoteDesc = _peer.SetRemoteDescription(ref description);
            yield return opRemoteDesc;

            if (opRemoteDesc.IsError)
            {
                Debug.LogError($"pc {opRemoteDesc.Error.message}");
                yield break;
            }

            var op = _peer.CreateAnswer();
            yield return op;

            if (!op.IsError)
            {
                RTCSessionDescription desc = op.Desc;
                var op1 = _peer.SetLocalDescription(ref desc);
                yield return op1;

                if (!op1.IsError)
                {
                    // Debug.Log($"[{m_clientId}] sent answer.");
                    m_signaler.SendAnswer(m_id, m_clientId, _peer.LocalDescription);
                }
            }
            else
            {
                CreateDescriptionError(op.Error);
            }
        }

        private static void CreateDescriptionError(RTCError error)
        {
            Debug.LogError($"[CreateDescriptionError]: {error.message}");
        }

        public IEnumerator SetRemoteDescriptionCoroutine(RTCSdpType type, SDPData sdpData)
        {
            RTCSessionDescription description;
            description.type = type;
            description.sdp = sdpData.sdp;

            var opRemoteDesc = _peer.SetRemoteDescription(ref description);
            yield return opRemoteDesc;
        }

        public void AddIceCandidate(CandidateData data)
        {
            RTCIceCandidateInit option = new RTCIceCandidateInit
            {
                candidate = data.candidate, sdpMid = data.sdpMid, sdpMLineIndex = data.sdpMLineIndex
            };

            _peer.AddIceCandidate(new RTCIceCandidate(option));
        }

        private static double GetRecorderFrameAverage(ProfilerRecorder recorder)
        {
            var samplesCount = recorder.Capacity;
            if (samplesCount == 0)
                return 0;

            double r = 0;
            // unsafe
            // {
            //     var samples = stackalloc ProfilerRecorderSample[samplesCount];
            //     recorder.CopyTo(samples, samplesCount);
            //     for (var i = 0; i < samplesCount; ++i)
            //         r += samples[i].Value;
            //     r /= samplesCount;
            // }

            return r;
        }

        public IEnumerator GetStats(float interval = 1.0f)
        {
            while (true)
            {
                yield return new WaitForSeconds(interval);

                if (_peer == null)
                {
                    yield break;
                }

                // WebRTC stats
                var statsOperation = _peer.GetStats();
                // Unity main thread time
                var frameTime = GetRecorderFrameAverage(mainThreadTimeRecorder) * (1e-9f);
                yield return statsOperation;

                var stats = statsOperation.Value.Stats;
                string text = "";

                text += $"[Renderer Time]\nframeTime={frameTime}\n";
                foreach (var stat in stats.Values)
                {
                    if ((stat is RTCOutboundRTPStreamStats) ||
                        (stat is RTCTransportStats) ||
                        (stat is RTCVideoSourceStats) ||
                        (stat is RTCRemoteInboundRtpStreamStats))
                    {
                        text += System.String.Format("[{0}]\n", stat.GetType().AssemblyQualifiedName);
                        text += $"timestamp={stat.Timestamp}\n";
                        text += stat.Dict.Aggregate(string.Empty, (str, next) =>
                                    str + next.Key + "=" + (next.Value == null ? string.Empty : next.Value.ToString()) + "\n");
                        ;
                    }
                }
                m_signaler.SendStats(text, m_clientId);
                // Debug.Log(statsOperation);
            }
        }
    }
}
