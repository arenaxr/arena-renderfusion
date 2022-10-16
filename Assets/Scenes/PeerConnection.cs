using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using ArenaUnity.HybridRendering.Signaling;
using Unity.Profiling;

namespace ArenaUnity.HybridRendering
{
    public class PeerConnection : IDisposable
    {
        static readonly Vector2Int videoSize = new Vector2Int(1920, 1080);

        static readonly float s_defaultFrameRate = 60;

        static readonly uint s_defaultMinBitrate = 0;
        static readonly uint s_defaultMaxBitrate = 5000;

        static readonly string[] excludeCodecMimeType = { "video/red", "video/ulpfec", "video/rtx", "video/flexfec-03" };

        private string id;
        private string m_clientId;

        private ISignaling m_signaler;

        private CameraStream camStream;
        // private VideoStreamTrack track;

        public RTCPeerConnection pc;

        private List<RTCRtpSender> pcSenders;
        private RTCDataChannel remoteDataChannel;
        private MediaStream sourceStream;

        private GameObject gobj;

        private float m_frameRate = s_defaultFrameRate;

        private uint m_minBitrate = s_defaultMinBitrate;
        private uint m_maxBitrate = s_defaultMaxBitrate;

        private ProfilerRecorder mainThreadTimeRecorder;

        private readonly Func<IEnumerator, Coroutine> _startCoroutine;

        public string Id { get { return id; } }

        public PeerConnection(RTCPeerConnection peer, string clientId, ISignaling signaler, Func<IEnumerator, Coroutine> startCoroutine)
        {
            m_signaler = signaler;
            m_clientId = clientId;
            _startCoroutine = startCoroutine;

            id = System.Guid.NewGuid().ToString();
            Debug.Log($"New Peer: (ID: {id})");

            pc = peer;
            pc.OnIceCandidate = OnIceCandidate;
            pc.OnDataChannel = OnDataChannel;

            pcSenders = new List<RTCRtpSender>();

            sourceStream = new MediaStream();

            gobj = new GameObject(id);
            gobj.transform.gameObject.AddComponent<Camera>();
            camStream = gobj.AddComponent<CameraStream>();

            // track = camStream.GetTrack();
            AddSender();

            mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        }

        ~PeerConnection()
        {
            Dispose();
        }

        public void Dispose()
        {
            // track.Dispose();

            pc.OnTrack = null;
            pc.OnDataChannel = null;
            pc.OnIceCandidate = null;
            pc.OnNegotiationNeeded = null;
            pc.OnConnectionStateChange = null;
            pc.OnIceConnectionChange = null;
            pc.OnIceGatheringStateChange = null;
            pc.Dispose();
            pc = null;

            UnityEngine.Object.Destroy(gobj);

            mainThreadTimeRecorder.Dispose();

            Debug.Log($"Peer (ID: {id}) killed");
            GC.SuppressFinalize(this);
        }

        private static double GetRecorderFrameAverage(ProfilerRecorder recorder)
        {
            var samplesCount = recorder.Capacity;
            if (samplesCount == 0)
                return 0;

            double r = 0;
            unsafe
            {
                var samples = stackalloc ProfilerRecorderSample[samplesCount];
                recorder.CopyTo(samples, samplesCount);
                for (var i = 0; i < samplesCount; ++i)
                    r += samples[i].Value;
                r /= samplesCount;
            }

            return r;
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
                    minBitrate = (ulong?)m_minBitrate * 1000,
                    maxBitrate = (ulong?)m_maxBitrate * 1000,
                    maxFramerate = (uint?)m_frameRate
                    // scaleResolutionDownBy = m_scaleResolutionDown
                }
            };

            return init;
        }

        private void AddSender()
        {
            _startCoroutine(AddSenderCoroutine());
        }

        private IEnumerator AddSenderCoroutine()
        {
            var op = camStream.CreateTrack(2 * videoSize.x, videoSize.y);
            if (op.Track == null)
                yield return op;

            // senderBase.SetTrack(op.Track);

            sourceStream.AddTrack(op.Track);

            foreach (var track in sourceStream.GetTracks())
            {
                var pcSender = pc.AddTrack(track, sourceStream);
                pcSenders.Add(pcSender);
            }

            var capabilities = RTCRtpSender.GetCapabilities(TrackKind.Video);
            var codecs = capabilities.codecs.Where(codec => !excludeCodecMimeType.Contains(codec.mimeType)).ToArray();
            // // var codecs = capabilities.codecs.Where(codec => codec.mimeType == "video/H264").ToArray();
            // foreach (var transceiver in pc.GetTransceivers())
            // {
            //     if (pcSenders.Contains(transceiver.Sender))
            //     {
            //         transceiver.SetCodecPreferences(codecs);
            //     }
            // }

            // RTCRtpTransceiverInit init = GetTransceiverInit();
            // var transceiver = pc.AddTransceiver(op.Track, init);
            // transceiver.SetCodecPreferences(codecs);
        }

        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            // Debug.Log($"pc ICE candidate:\n {candidate.Candidate}");
            m_signaler.SendCandidate(id, candidate);
        }

        private void OnDataChannel(RTCDataChannel channel)
        {
            remoteDataChannel = channel;
            remoteDataChannel.OnMessage = onDataChannelMessage;
        }

        public IEnumerator StartNegotiationCoroutine()
        {
            // Debug.Log($"[{m_clientId}] creating offer.");

            var op = pc.CreateOffer();
            yield return op;

            if (!op.IsError)
            {
                if (pc.SignalingState != RTCSignalingState.Stable)
                {
                    Debug.LogError("pc signaling state is not stable.");
                    yield break;
                }

                RTCSessionDescription desc = op.Desc;
                var op1 = pc.SetLocalDescription(ref desc);
                yield return op1;

                if (!op1.IsError)
                {
                    // Debug.Log($"[{m_clientId}] sent offer.");
                    m_signaler.SendOffer(id, pc.LocalDescription);
                }

                SetBitrate(s_defaultMinBitrate, s_defaultMaxBitrate);
                SetFrameRate(s_defaultFrameRate);
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

            var opRemoteDesc = pc.SetRemoteDescription(ref description);
            yield return opRemoteDesc;

            if (opRemoteDesc.IsError)
            {
                Debug.LogError($"pc {opRemoteDesc.Error.message}");
                yield break;
            }

            var op = pc.CreateAnswer();
            yield return op;

            if (!op.IsError)
            {
                RTCSessionDescription desc = op.Desc;
                var op1 = pc.SetLocalDescription(ref desc);
                yield return op1;

                if (!op1.IsError)
                {
                    // Debug.Log($"[{m_clientId}] sent answer.");
                    m_signaler.SendAnswer(id, pc.LocalDescription);
                }
            }
            else
            {
                CreateDescriptionError(op.Error);
            }
        }

        private static void CreateDescriptionError(RTCError error)
        {
            Debug.LogError($"Error Detail Type: {error.message}");
        }

        public IEnumerator SetRemoteDescriptionCoroutine(RTCSdpType type, SDPData sdpData)
        {
            RTCSessionDescription description;
            description.type = type;
            description.sdp = sdpData.sdp;

            var opRemoteDesc = pc.SetRemoteDescription(ref description);
            yield return opRemoteDesc;
        }

        public void AddIceCandidate(CandidateData data)
        {
            RTCIceCandidateInit option = new RTCIceCandidateInit
            {
                candidate = data.candidate, sdpMid = data.sdpMid, sdpMLineIndex = data.sdpMLineIndex
            };

            pc.AddIceCandidate(new RTCIceCandidate(option));
        }

        private void onDataChannelMessage(byte[] bytes)
        {
            string pos = System.Text.Encoding.UTF8.GetString(bytes);
            var clientPose = JsonUtility.FromJson<ClientPose>(pos);
            camStream.UpdatePose(clientPose);
        }

        public void SetFrameRate(float frameRate)
        {
            if (frameRate < 0)
                throw new ArgumentOutOfRangeException("frameRate", frameRate, "The parameter must be greater than zero.");
            m_frameRate = frameRate;
            foreach (var transceiver in pc.GetTransceivers())
            {
                if (pcSenders.Contains(transceiver.Sender))
                {
                    RTCError error = transceiver.Sender.SetFrameRate((uint)m_frameRate);
                    if (error.errorType != RTCErrorType.None)
                        throw new InvalidOperationException($"Set framerate is failed. {error.message}");
                }
            }
        }

        public void SetBitrate(uint minBitrate, uint maxBitrate)
        {
            if (minBitrate > maxBitrate)
                throw new ArgumentException("The maxBitrate must be greater than minBitrate.", "maxBitrate");
            m_minBitrate = minBitrate;
            m_maxBitrate = maxBitrate;
            foreach (var transceiver in pc.GetTransceivers())
            {
                if (pcSenders.Contains(transceiver.Sender))
                {
                    RTCError error = transceiver.Sender.SetBitrate(m_minBitrate, m_maxBitrate);
                    if (error.errorType != RTCErrorType.None)
                        throw new InvalidOperationException($"Set codec is failed. {error.message}");
                }
            }
        }

        public IEnumerator GetStatsInterval(float interval = 1.0f)
        {
            while (true)
            {
                yield return new WaitForSeconds(interval);

                if (pc == null)
                {
                    yield break;
                }

                // WebRTC stats
                var statsOperation = pc.GetStats();
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
                m_signaler.SendStats(text);
                //Debug.Log(statsOperation);
            }
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
}
