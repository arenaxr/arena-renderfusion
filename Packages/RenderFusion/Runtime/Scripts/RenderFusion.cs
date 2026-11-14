using System.Linq;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;
using ArenaUnity;
using ArenaUnity.RenderFusion.Signaling;

namespace ArenaUnity.RenderFusion
{
    [RequireComponent(typeof(ArenaClientScene))]
    public sealed class RenderFusion : MonoBehaviour
    {
#pragma warning disable 0649
        [SerializeField, Tooltip("Maximum clients allowed (-1 if unlimited).")]
        public int maxClients = -1;

        [SerializeField, Tooltip("Maximum missed heartbeats before removal of a client.")]
        public int maxMissedHeartbeats = 3;

        [SerializeField, Tooltip("Amount of seconds to poll for new clients.")]
        public int discoveryInterval = 1;

        [SerializeField, Tooltip("Video resolution (width will be internally doubled to account for depth frame).")]
        public Vector2Int videoResolution = new Vector2Int(1920, 1080);

        [SerializeField, Tooltip("Automatically started when called Start method.")]
        public bool runOnStart = true;

        [SerializeField, Tooltip("Array to set custom STUN/TURN servers.")]
        private RTCIceServer[] iceServers = new RTCIceServer[]
        {
            new RTCIceServer() {urls = new string[] {"stun:stun.l.google.com:19302"}}
        };

#pragma warning restore 0649

        internal ISignaling signaler;
        internal Dictionary<string, PeerConnection> clientPeerDict = new Dictionary<string, PeerConnection>();

        private int timerCounter = 0;

        private void Awake()
        {
            // remote rendering publishes on the scene's 'r' topics, which the ARENA auth API
            // only grants when the token is requested with render rights. This must run before
            // ArenaClientScene connects, and GetComponent avoids depending on Awake ordering
            // between the two components (Instance is assigned in ArenaClientScene.Awake).
            GetComponent<ArenaClientScene>().requestRemoteRenderRights = true;
        }

        private void Start()
        {
            if (!runOnStart)
                return;

            var scene = ArenaClientScene.Instance;

            scene.requestRemoteRenderRights = true;

#if !UNITY_EDITOR
            StartCoroutine(scene.ConnectArena());
#endif
            StartCoroutine(SetupSignaling());
        }

        private IEnumerator SetupSignaling()
        {
            var scene = ArenaClientScene.Instance;

            yield return new WaitUntil(() => scene.mqttClientConnected);

            signaler = new ARENAMQTTSignaling(SynchronizationContext.Current);
            signaler.OnStart += OnSignalerStart;
            signaler.OnClientConnect += OnClientConnect;
            signaler.OnClientDisconnect += OnClientDisconnect;
            signaler.OnOffer += OnOffer;
            signaler.OnAnswer += OnAnswer;
            signaler.OnIceCandidate += OnIceCandidate;
            signaler.OnClientHealthCheck += OnClientHealthCheck;

            signaler.OpenConnection();

            signaler.SendConnect();

            // heartbeats run as a coroutine so the peer collections are only ever
            // touched on the main thread, alongside the signaling callbacks
            StartCoroutine(HeartbeatLoop());
        }

        private void OnSignalerStart(ISignaling signaler)
        {
            var scene = ArenaClientScene.Instance;

            StartCoroutine(WebRTC.Update());

            Debug.Log($"RenderFusion server started! Please visit <a href=\"https://{scene.hostAddress}/{scene.namespaceName}/{scene.sceneName}\">https://{scene.hostAddress}/{scene.namespaceName}/{scene.sceneName}</a>");
        }

        private PeerConnection CreatePeerConnection(ConnectData data)
        {
            RTCConfiguration conf = new RTCConfiguration { iceServers = iceServers };
            var pc = new RTCPeerConnection(ref conf);
            PeerConnection peer = new PeerConnection(pc, data, signaler, StartCoroutine);
            clientPeerDict.Add(data.id, peer);
            Debug.Log($"[OnClientConnect] There are now {clientPeerDict.Count} clients connected.");
            return peer;
        }

        private void RemovePeerConnection(string id)
        {
            if (clientPeerDict.TryGetValue(id, out PeerConnection peer))
            {
                clientPeerDict.Remove(id);
                peer.Dispose();
                Debug.Log($"[RemovePeerConnection] There are now {clientPeerDict.Count} clients connected.");
            }
        }
        private void OnDestroy()
        {
            if (signaler != null)
            {
                // callbacks already posted to the main thread would otherwise run against
                // a destroyed component, creating GameObjects and starting coroutines
                signaler.OnStart -= OnSignalerStart;
                signaler.OnClientConnect -= OnClientConnect;
                signaler.OnClientDisconnect -= OnClientDisconnect;
                signaler.OnOffer -= OnOffer;
                signaler.OnAnswer -= OnAnswer;
                signaler.OnIceCandidate -= OnIceCandidate;
                signaler.OnClientHealthCheck -= OnClientHealthCheck;
            }

            var keysToRemove = clientPeerDict.Keys.ToList();
            foreach (var id in keysToRemove)
            {
                RemovePeerConnection(id);
            }

            if (signaler != null)
                signaler.CloseConnection();
        }

        private void OnClientConnect(ISignaling signaler, ConnectData data)
        {
            bool reconnecting = clientPeerDict.ContainsKey(data.id);

            // a reconnecting client reuses its own slot, so the cap only gates new ones
            if (!reconnecting && maxClients != -1 && clientPeerDict.Count >= maxClients)
            {
                Debug.LogWarning($"[OnClientConnect] Only a maximum of {maxClients} are allowed to connect.");
                return;
            }

            if (reconnecting)
            {
                // tear the stale peer down completely (dropping it leaked its GameObject,
                // recorder and WebRTC handles) and then give the client a fresh one
                RemovePeerConnection(data.id);
            }

            PeerConnection peer = CreatePeerConnection(data);
            peer.AddSender(videoResolution.x, videoResolution.y);
            StartCoroutine(peer.GetStats(1.0f));
        }

        private void OnClientDisconnect(ISignaling signaler, string id)
        {
            RemovePeerConnection(id);
        }

        private void OnOffer(ISignaling signaler, SDPData offer)
        {
            // Debug.Log("got offer.");
            if (clientPeerDict.TryGetValue(offer.id, out PeerConnection peer))
                StartCoroutine(peer.CreateAndSendAnswerCoroutine(offer));
            else
                Debug.LogWarning($"Peer {offer.id} not found in dictionary.");
        }

        private void OnAnswer(ISignaling signaler, SDPData answer)
        {
            // Debug.Log("got answer.");
            if (clientPeerDict.TryGetValue(answer.id, out PeerConnection peer))
                StartCoroutine(peer.SetRemoteDescriptionCoroutine(RTCSdpType.Answer, answer));
            else
                Debug.LogWarning($"Peer {answer.id} not found in dictionary.");
        }

        private void OnIceCandidate(ISignaling signaler, CandidateData data)
        {
            if (clientPeerDict.TryGetValue(data.id, out PeerConnection peer))
                peer.AddIceCandidate(data);
            else
                Debug.LogWarning($"Peer {data.id} not found in dictionary.");
        }

        private void OnClientHealthCheck(ISignaling signaler, string id) {
            if (clientPeerDict.TryGetValue(id, out PeerConnection peer))
            {
                peer.missedHeartbeats = 0;
            }
            else
                Debug.LogWarning($"Peer {id} not found in dictionary.");
        }

        private IEnumerator HeartbeatLoop()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                SendHeartbeats();
            }
        }

        private void SendHeartbeats()
        {
            if (signaler == null)
                return;

            // discoveryInterval is author-set and would divide by zero at 0
            if (timerCounter % Mathf.Max(1, discoveryInterval) == 0)
            {
                signaler.SendConnect();
            }
            timerCounter++;

            // Reap from a snapshot on the same thread as signaling. There is no
            // deferred ID queue that can accidentally delete a replacement peer.
            foreach (var item in clientPeerDict.ToArray())
            {
                var id = item.Key;
                var peer = item.Value;

                if (peer.missedHeartbeats >= Mathf.Max(1, maxMissedHeartbeats))
                {
                    RemovePeerConnection(id);
                    continue;
                }

                peer.missedHeartbeats++;
                signaler.SendHealthCheck(peer.Id);
            }
        }
    }
}
