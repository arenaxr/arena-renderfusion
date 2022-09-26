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
    public class PeerConnection
    {
        private string id;
        private string m_clientId;

        private ISignaling m_signaler;

        private CameraStream camStream;
        private VideoStreamTrack track;

        public RTCPeerConnection pc;

        private List<RTCRtpSender> pcSenders;
        private RTCDataChannel remoteDataChannel;

        private ProfilerRecorder mainThreadTimeRecorder;

        private readonly string[] excludeCodecMimeType = { "video/red", "video/ulpfec", "video/rtx" };

        public string Id { get { return id; } }

        public PeerConnection(RTCPeerConnection peer, string clientId, ISignaling signaler)
        {
            m_signaler = signaler;
            m_clientId = clientId;

            id = System.Guid.NewGuid().ToString();
            Debug.Log($"New Peer: (ID: {id})");

            pc = peer;
            pc.OnIceCandidate = OnIceCandidate;
            pc.OnDataChannel = OnDataChannel;

            pcSenders = new List<RTCRtpSender>();

            camStream = new CameraStream(id);

            track = camStream.GetTrack();
            AddTracks(track);

            mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        }

        ~PeerConnection()
        {
            Dispose();
        }

        public void Dispose()
        {
            Debug.Log($"Peer (ID: {id}) killed");
            track.Dispose();
            camStream.Dispose();
            pc.OnTrack = null;
            pc.OnDataChannel = null;
            pc.OnIceCandidate = null;
            pc.OnNegotiationNeeded = null;
            pc.OnConnectionStateChange = null;
            pc.OnIceConnectionChange = null;
            pc.OnIceGatheringStateChange = null;
            pc.Dispose();
            pc = null;
            mainThreadTimeRecorder.Dispose();
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

        private void AddTracks(MediaStreamTrack track)
        {
            pcSenders.Add(pc.AddTrack(track));

            var capabilities = RTCRtpSender.GetCapabilities(TrackKind.Video);
            var codecs = capabilities.codecs.Where(codec => !excludeCodecMimeType.Contains(codec.mimeType)).ToArray();
            foreach (var transceiver in pc.GetTransceivers())
            {
                if (pcSenders.Contains(transceiver.Sender))
                {
                    transceiver.SetCodecPreferences(codecs);
                }
            }
        }

        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            // Debug.Log($"pc ICE candidate:\n {candidate.Candidate}");
            m_signaler.SendCandidate(id, candidate);
        }

        private void OnDataChannel(RTCDataChannel channel) {
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

                UpdateBandwidth();
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

            camStream.updatePosition(clientPose.x, clientPose.y, clientPose.z);

            camStream.updateRotation(
                -clientPose.x_,
                -clientPose.y_,
                clientPose.z_,
                clientPose.w_
            );
        }

        public IEnumerator GetStatsInterval(float interval = 1.0f)
        {
            while (true)
            {
                yield return new WaitForSeconds(interval);

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

        private void UpdateBandwidth()
        {
            ulong? bandwidth = 100; // Bandwidth in Kbps
            RTCRtpSender sender = pc.GetSenders().First();
            RTCRtpSendParameters parameters = sender.GetParameters();

            parameters.encodings[0].maxBitrate = bandwidth * 1000;
            parameters.encodings[0].minBitrate = bandwidth * 1000;

            RTCError error = sender.SetParameters(parameters);
            if (error.errorType != RTCErrorType.None)
            {
                Debug.LogErrorFormat("Bandwidth update failed. RTCRtpSender.SetParameters failed {0}", error.errorType);
            }
        }
    }
}
