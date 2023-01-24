using System;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;
using ArenaUnity;
using ArenaUnity.HybridRendering.Signaling;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ArenaUnity.HybridRendering
{
    [RequireComponent(typeof(ArenaClientScene))]
    public sealed class ARENAHybridRendering : MonoBehaviour
    {
#pragma warning disable 0649
        [SerializeField, Tooltip("Maximum clients allowed (-1 if unlimited).")]
        public int maxClients = -1;

        [SerializeField, Tooltip("Maximum missed heartbeats before removal of a client.")]
        public int maxMissedHeartbeats = 10;

        [SerializeField, Tooltip("Enable dynamic scene partitioning (using remote-render).")]
        public bool remoteRender = true;

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
        internal List<string> deadPeerIds = new List<string>();

        internal System.Threading.Timer timer;

        private void Start()
        {
            if (!runOnStart)
                return;

#if !UNITY_EDITOR
            StartCoroutine(ArenaClientScene.Instance.ConnectArena());
#endif
            // Debug.Log(ArenaClientScene.Instance.namespaceName);
            // Debug.Log(ArenaClientScene.Instance.sceneName);
            StartCoroutine(SetupSignaling());
        }

        private void OnDestroy()
        {
            timer.Dispose();
        }

        private IEnumerator SetupSignaling()
        {
            yield return new WaitUntil(() => ArenaClientScene.Instance.mqttClientConnected);

            GameObject gobj = new GameObject("Arena MQTT Signaler");
            signaler = gobj.AddComponent(typeof(ARENAMQTTSignaling)) as ARENAMQTTSignaling;
            signaler.SetSyncContext(SynchronizationContext.Current);

            // signaler = new ARENAMQTTSignaling(SynchronizationContext.Current);
            signaler.OnStart += OnSignalerStart;
            signaler.OnClientConnect += OnClientConnect;
            signaler.OnClientDisconnect += OnClientDisconnect;
            signaler.OnOffer += OnOffer;
            signaler.OnAnswer += OnAnswer;
            signaler.OnIceCandidate += OnIceCandidate;
            signaler.OnClientHealthCheck += OnClientHealthCheck;
            signaler.OnRemoteObjectStatusUpdate += OnRemoteObjectStatusUpdate;
            signaler.OpenConnection();

            // sets up heartbeats to send to client every second
            TimerCallback timercallback = new TimerCallback(HandleHealthCheck);
            timer = new Timer(timercallback, signaler as object, 1000, 1000);
        }

        private void OnSignalerStart(ISignaling signaler)
        {
            if (remoteRender)
                removeNonRemoteRenderedObjs();

            StartCoroutine(WebRTC.Update());
            Debug.Log("Hybrid Rendering Server Started!");
        }

        private void removeNonRemoteRenderedObjs() {
            foreach (var aobj in FindObjectsOfType<ArenaObject>(true))
            {
                JToken data = JToken.Parse(aobj.jsonData);
                var remoteRenderToken = data["remote-render"];
                if (remoteRenderToken != null)
                {
                    bool remoteRendered = remoteRenderToken["enabled"].Value<bool>();
                    aobj.gameObject.SetActive(remoteRendered);
                    // aobj.gameObject.GetComponent<Renderer>().enabled = remoteRendered;
                }
                else if (aobj.gameObject.activeSelf)
                {
                    aobj.gameObject.SetActive(false);
                    // aobj.gameObject.GetComponent<Renderer>().enabled = false;
                }
            }
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
            PeerConnection peer;
            if (clientPeerDict.TryGetValue(id, out peer))
            {
                clientPeerDict.Remove(id);
                peer.Dispose();
                Debug.Log($"[RemovePeerConnection] There are now {clientPeerDict.Count} clients connected.");
            }
            else
                Debug.LogWarning($"Peer {id} not found in dictionary.");
        }

        private void OnClientConnect(ISignaling signaler, ConnectData data)
        {
            if (maxClients != -1 && clientPeerDict.Count >= maxClients)
            {
                Debug.LogWarning($"[OnClientConnect] Only a maximum of {maxClients} are allowed to connect.");
                return;
            }

            PeerConnection peer;
            if (!clientPeerDict.TryGetValue(data.id, out peer))
            {
                peer = CreatePeerConnection(data);
                peer.AddSender();
                StartCoroutine(peer.GetStats(1.0f));
            }
            else
            {
                peer.peer.Close();
                clientPeerDict.Remove(data.id);
            }
        }

        private void OnClientDisconnect(ISignaling signaler, string id)
        {
            RemovePeerConnection(id);
        }

        private void OnOffer(ISignaling signaler, SDPData offer)
        {
            // Debug.Log("got offer.");

            PeerConnection peer;
            if (clientPeerDict.TryGetValue(offer.id, out peer))
                StartCoroutine(peer.CreateAndSendAnswerCoroutine(offer));
            else
                Debug.LogWarning($"Peer {offer.id} not found in dictionary.");
        }

        private void OnAnswer(ISignaling signaler, SDPData answer)
        {
            // Debug.Log("got answer.");

            PeerConnection peer;
            if (clientPeerDict.TryGetValue(answer.id, out peer))
                StartCoroutine(peer.SetRemoteDescriptionCoroutine(RTCSdpType.Answer, answer));
            else
                Debug.LogWarning($"Peer {answer.id} not found in dictionary.");
        }

        private void OnIceCandidate(ISignaling signaler, CandidateData data)
        {
            PeerConnection peer;
            if (clientPeerDict.TryGetValue(data.id, out peer))
                peer.AddIceCandidate(data);
            else
                Debug.LogWarning($"Peer {data.id} not found in dictionary.");
        }

        private void OnClientHealthCheck(ISignaling signaler, string id) {
            PeerConnection peer;
            if (clientPeerDict.TryGetValue(id, out peer))
                peer.missedHeartbeats = 0;
            else
                Debug.LogWarning($"Peer {id} not found in dictionary.");
        }

        private void OnRemoteObjectStatusUpdate(ISignaling signaler, string objectId, bool remoteRendered)
        {
            if (!remoteRender)
                return;

            foreach (var aobj in FindObjectsOfType<ArenaObject>(true))
            {
                if (aobj.name != objectId)
                    continue;

                // Debug.Log($"[OnRemoteObjectStatusUpdate] {objectId} - {remoteRendered}");
                aobj.gameObject.SetActive(remoteRendered);
                // aobj.gameObject.GetComponent<Renderer>().enabled = remoteRendered;
            }
        }

        private void HandleHealthCheck(object signalerObj)
        {
            ISignaling signaler = (ISignaling)signalerObj;
            foreach (var item in clientPeerDict)
            {
                var id = item.Key;
                var peer = item.Value;

                signaler.SendHealthCheck(peer.Id);

                if (peer.missedHeartbeats >= maxMissedHeartbeats)
                    deadPeerIds.Add(id);

                peer.missedHeartbeats++;
            }
        }

        private void Update()
        {
            foreach (var deadPeerId in deadPeerIds)
            {
                RemovePeerConnection(deadPeerId);
            }
            deadPeerIds.Clear();
        }
    }
}
