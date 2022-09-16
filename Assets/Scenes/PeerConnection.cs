using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using ArenaUnity.HybridRendering.Signaling;

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
    }
}
