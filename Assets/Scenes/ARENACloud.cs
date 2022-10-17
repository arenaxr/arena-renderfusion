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
    public class ARENACloud : MonoBehaviour
    {
        private ISignaling signaler;
        private RTCConfiguration config = new RTCConfiguration{
                iceServers = new[] {
                    new RTCIceServer {
                        urls = new[] {"stun:stun.l.google.com:19302"}
                    }
                }
            };
        System.Threading.Timer timer;

        Dictionary<string, PeerConnection> clientPeerDict = new Dictionary<string, PeerConnection>();

        private void Awake()
        {
            WebRTC.Initialize();
        }

        private void OnDestroy()
        {
            WebRTC.Dispose();
            timer.Dispose();
            Debug.Log("Destroyed");
        }

        // Start is called before the first frame update
        private void Start()
        {
#if !UNITY_EDITOR
            StartCoroutine(ArenaClientScene.Instance.ConnectArena());
#endif
            StartCoroutine(SetupSignaling());
        }

        // Update is called once per frame
        private void Update()
        {

        }

        private IEnumerator SetupSignaling()
        {
            yield return new WaitUntil(() => ArenaClientScene.Instance.mqttClientConnected);

            GameObject gobj = new GameObject("Arena MQTT Signaler");
            signaler = gobj.AddComponent(typeof(ARENAMQTTSignaling)) as ARENAMQTTSignaling;
            signaler.SetSyncContext(SynchronizationContext.Current);

            // signaler = new ARENAMQTTSignaling(SynchronizationContext.Current);
            signaler.OnStart += OnSignalerStart;
            signaler.OnClientConnect += GotClientConnect;
            signaler.OnClientDisconnect += GotClientDisconnect;
            signaler.OnOffer += GotOffer;
            signaler.OnAnswer += GotAnswer;
            signaler.OnIceCandidate += GotIceCandidate;
            signaler.OnRemoteObjectStatusUpdate += GotRemoteObjectStatusUpdate;
            signaler.OpenConnection();

            // sets up heartbeats to send to client every second
            TimerCallback timercallback = new TimerCallback(BroadcastHealthCheck);
            timer = new Timer(timercallback, signaler as object, 1000, 1000);
        }

        private void OnSignalerStart(ISignaling signaler)
        {
            removeNonRemoteRenderedObjs();
            StartCoroutine(WebRTC.Update());
            Debug.Log("Hybrid Rendering Server Started!");
        }

        private void removeNonRemoteRenderedObjs() {
            foreach (var aobj in FindObjectsOfType<ArenaObject>(true))
            {
                JToken data = JToken.Parse(aobj.jsonData);
                var remoteRenderToken = data["remote-render"];
                // Debug.Log($"{aobj.name} - {remoteRenderToken}");
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

        private PeerConnection CreatePeerConnection(string id)
        {
            var pc = new RTCPeerConnection(ref config);
            PeerConnection peer = new PeerConnection(pc, id, signaler, StartCoroutine);
            clientPeerDict.Add(id, peer);
            return peer;
        }

        private void GotClientConnect(ISignaling signaler, string id)
        {
            PeerConnection peer;
            // Debug.Log(id);
            if (!clientPeerDict.TryGetValue(id, out peer))
            {
                peer = CreatePeerConnection(id);
                Debug.Log($"[Connect] There are now {clientPeerDict.Count} clients connected.");

                peer.AddSender();
                StartCoroutine(peer.GetStatsInterval(1.0f));
            }
            else
            {
                peer.pc.Close();
                clientPeerDict.Remove(id);
            }
        }

        private void GotClientDisconnect(ISignaling signaler, string id)
        {
            PeerConnection peer;
            if (clientPeerDict.TryGetValue(id, out peer))
            {
                clientPeerDict.Remove(id);
                peer.Dispose();
                Debug.Log($"[Disconnect] There are now {clientPeerDict.Count} clients connected.");
            }
            else
                Debug.LogWarning($"Peer {id} not found in dictionary.");
        }

        private void GotOffer(ISignaling signaler, SDPData offer)
        {
            // Debug.Log("got offer.");

            PeerConnection peer;
            if (clientPeerDict.TryGetValue(offer.id, out peer))
                StartCoroutine(peer.CreateAndSendAnswerCoroutine(offer));
            else
                Debug.LogWarning($"Peer {offer.id} not found in dictionary.");
        }

        private void GotAnswer(ISignaling signaler, SDPData answer)
        {
            // Debug.Log("got answer.");

            PeerConnection peer;
            if (clientPeerDict.TryGetValue(answer.id, out peer))
                StartCoroutine(peer.SetRemoteDescriptionCoroutine(RTCSdpType.Answer, answer));
            else
                Debug.LogWarning($"Peer {answer.id} not found in dictionary.");
        }

        private void GotIceCandidate(ISignaling signaler, CandidateData data)
        {
            PeerConnection peer;
            if (clientPeerDict.TryGetValue(data.id, out peer))
                peer.AddIceCandidate(data);
            else
                Debug.LogWarning($"Peer {data.id} not found in dictionary.");
        }

        private void GotRemoteObjectStatusUpdate(ISignaling signaler, string objectId, bool remoteRendered)
        {
            foreach (var aobj in FindObjectsOfType<ArenaObject>(true))
            {
                if (aobj.name != objectId)
                    continue;

                // Debug.Log($"[GotRemoteObjectStatusUpdate] {objectId} - {remoteRendered}");
                aobj.gameObject.SetActive(remoteRendered);
                // aobj.gameObject.GetComponent<Renderer>().enabled = remoteRendered;
            }
        }

        private void BroadcastHealthCheck(object signalerObj)
        {
            ISignaling signaler = (ISignaling)signalerObj;
            foreach(var item in clientPeerDict)
            {
                signaler.BroadcastHealthCheck(item.Value.Id);
            }
        }
    }
}
