using System;
using System.Text;
using System.Threading;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using ArenaUnity;

namespace ArenaUnity.HybridRendering.Signaling
{
    public class ARENAMQTTSignaling : ArenaMqttClient, ISignaling
    {
        private SynchronizationContext m_mainThreadContext;

        private string m_clientId;

        private readonly string SERVER_OFFER_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/server/offer";
        private readonly string SERVER_ANSWER_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/server/answer";
        private readonly string SERVER_CANDIDATE_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/server/candidate";
        private readonly string SERVER_HEALTH_CHECK_PREFIX = "realm/g/a/hybrid_rendering/server/health";
        private readonly string SERVER_STATS_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/server/stats";
        private readonly string SERVER_CONNECT_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/server/connect";

        private readonly string CLIENT_CONNECT_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/client/connect";
        private readonly string CLIENT_DISCONNECT_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/client/disconnect";
        private readonly string CLIENT_OFFER_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/client/offer";
        private readonly string CLIENT_ANSWER_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/client/answer";
        private readonly string CLIENT_CANDIDATE_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/client/candidate";
        private readonly string CLIENT_HEALTH_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/client/health";
        // private readonly string CLIENT_STATS_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/client/stats";

        private readonly string UPDATE_REMOTE_STATUS_TOPIC_PREFIX = "realm/g/a/hybrid_rendering/client/remote";

        private string SERVER_OFFER_TOPIC;
        private string SERVER_ANSWER_TOPIC;
        private string SERVER_CANDIDATE_TOPIC;
        private string SERVER_HEALTH_CHECK_TOPIC;
        private string SERVER_STATS_TOPIC;
        private string SERVER_CONNECT_TOPIC;

        public string Url { get { return "arenaxr.org"; } }

        protected override void Awake()
        {
            m_clientId = "cloud-" + Guid.NewGuid().ToString();

            hostAddress = "arena-dev1.conix.io";
            authType = Auth.Manual;

            SERVER_OFFER_TOPIC = $"{SERVER_OFFER_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}";
            SERVER_ANSWER_TOPIC = $"{SERVER_ANSWER_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}";
            SERVER_CANDIDATE_TOPIC = $"{SERVER_CANDIDATE_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}";
            SERVER_HEALTH_CHECK_TOPIC = $"{SERVER_HEALTH_CHECK_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}";
            SERVER_STATS_TOPIC = $"{SERVER_STATS_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}";
            SERVER_CONNECT_TOPIC = $"{SERVER_CONNECT_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}";

            base.Awake();
            name = "Hybrid Rendering Signaler (Starting...)";
        }

        public void SetSyncContext(SynchronizationContext mainThreadContext) {
            m_mainThreadContext = mainThreadContext;
        }

        public event OnClientConnectHandler OnClientConnect;
        public event OnClientDisconnectHandler OnClientDisconnect;
        public event OnStartHandler OnStart;
        public event OnOfferHandler OnOffer;
        public event OnAnswerHandler OnAnswer;
        public event OnIceCandidateHandler OnIceCandidate;
        public event OnClientHealthCheckHandler OnClientHealthCheck;
        public event OnRemoteObjectStatusUpdateHandler OnRemoteObjectStatusUpdate;

        public void ConnectArena()
        {
            name = "Hybrid Rendering Signaler (Connecting...)";
            StartCoroutine(Signin());
        }

        public void OpenConnection()
        {
            Debug.Log($"MQTT opening connection to ARENA");
            ConnectArena();
        }

        protected override void OnConnected()
        {
            base.OnConnected();

            Subscribe(new string[] { $"{CLIENT_CONNECT_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}/#" });
            Subscribe(new string[] { $"{CLIENT_DISCONNECT_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}/#" });
            Subscribe(new string[] { $"{CLIENT_OFFER_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}/#" });
            Subscribe(new string[] { $"{CLIENT_ANSWER_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}/#" });
            Subscribe(new string[] { $"{CLIENT_CANDIDATE_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}/#" });
            Subscribe(new string[] { $"{CLIENT_HEALTH_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}/#" });
            // Subscribe(new string[] { $"{CLIENT_STATS_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}/#" });
            Subscribe(new string[] { $"{UPDATE_REMOTE_STATUS_TOPIC_PREFIX}/{ArenaClientScene.Instance.namespaceName}/{ArenaClientScene.Instance.sceneName}/#" });

            Debug.Log("Hybrid Rendering MQTT client connected!");
            name = "Hybrid Rendering Signaler (MQTT Connected)";
            m_mainThreadContext.Post(d => OnStart?.Invoke(this), null);
        }

        public void CloseConnection()
        {
            Disconnect();
        }

        private void Publish(string topic, string msg)
        {
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(msg);
            Publish(topic, payload);
        }

        public IEnumerator SendConnect()
        {
            while (true) {
                yield return new WaitForSeconds(5);

                RoutedMessage<string> routedMessage = new RoutedMessage<string>
                {
                    type = "connect",
                    source = "server",
                    id = m_clientId,
                    data = ""
                };
                Publish(SERVER_CONNECT_TOPIC, JsonUtility.ToJson(routedMessage));
            }
        }

        public void SendOffer(string id, RTCSessionDescription offer)
        {
            RoutedMessage<SDPData> routedMessage = new RoutedMessage<SDPData>{
                type = "offer",
                source = "server",
                id = id,
                data = new SDPData{
                    type = "offer",
                    sdp = offer.sdp
                }
            };

            Publish(SERVER_OFFER_TOPIC, JsonUtility.ToJson(routedMessage));
        }

        public void SendAnswer(string id, RTCSessionDescription answer)
        {
            RoutedMessage<SDPData> routedMessage = new RoutedMessage<SDPData>
            {
                type = "answer",
                source = "server",
                id = id,
                data = new SDPData{
                    type = "answer",
                    sdp = answer.sdp
                }
            };

            Publish(SERVER_ANSWER_TOPIC, JsonUtility.ToJson(routedMessage));
        }

        public void SendHealthCheck(string id){
            RoutedMessage<string> healthCheck = new RoutedMessage<string>
            {
                //Change id to what other senders are using
                type = "health",
                source = "server",
                id = id,
                data = ""
            };
            Publish(SERVER_HEALTH_CHECK_TOPIC, JsonUtility.ToJson(healthCheck));
        }


        public void SendCandidate(string id, RTCIceCandidate candidate)
        {
            RoutedMessage<CandidateData> routedMessage = new RoutedMessage<CandidateData>
            {
                type = "ice",
                source = "server",
                id = id,
                data = new CandidateData{
                    candidate = candidate.Candidate,
                    sdpMid = candidate.SdpMid,
                    sdpMLineIndex = candidate.SdpMLineIndex.GetValueOrDefault(0)
                }
            };

            Publish(SERVER_CANDIDATE_TOPIC, JsonUtility.ToJson(routedMessage));
        }

        public void SendStats(string stats)
        {
            Publish(SERVER_STATS_TOPIC, stats);
        }


        protected override void ProcessMessage(byte[] msg)
        {
            // msg.Topic, msg.Message
            var content = Encoding.UTF8.GetString(msg);

            try
            {
                var routedMessage = JsonUtility.FromJson<RoutedMessage<string>>(content);
                // ignore other servers
                if (routedMessage.source == "server") return;

                // Debug.Log($"MQTT Received message: {content}");
                if (routedMessage.type == "connect-ack")
                {
                    var routedMessageConnectData = JsonUtility.FromJson<RoutedMessage<ConnectData>>(content);
                    m_mainThreadContext.Post(d => OnClientConnect?.Invoke(this, routedMessageConnectData.data), null);
                }     
                else if (routedMessage.type == "disconnect")
                {
                    m_mainThreadContext.Post(d => OnClientDisconnect?.Invoke(this, routedMessage.id), null);
                }
                else if (routedMessage.type == "offer")
                {
                    var routedMessageSDP = JsonUtility.FromJson<RoutedMessage<SDPData>>(content);
                    SDPData offer = new SDPData
                    {
                        type = "offer",
                        id = routedMessageSDP.id,
                        sdp = routedMessageSDP.data.sdp
                    };
                    m_mainThreadContext.Post(d => OnOffer?.Invoke(this, offer), null);
                }
                else if (routedMessage.type == "answer")
                {
                    var routedMessageSDP = JsonUtility.FromJson<RoutedMessage<SDPData>>(content);
                    SDPData answer = new SDPData
                    {
                        type = "answer",
                        id = routedMessageSDP.id,
                        sdp = routedMessageSDP.data.sdp
                    };
                    m_mainThreadContext.Post(d => OnAnswer?.Invoke(this, answer), null);
                }
                else if (routedMessage.type == "ice")
                {
                    var routedMessageICE = JsonUtility.FromJson<RoutedMessage<CandidateData>>(content);
                    if (routedMessageICE.data.candidate != "")
                    {
                        CandidateData candidate = new CandidateData
                        {
                            id = routedMessageICE.id,
                            candidate = routedMessageICE.data.candidate,
                            sdpMid = routedMessageICE.data.sdpMid,
                            sdpMLineIndex = routedMessageICE.data.sdpMLineIndex
                        };
                        m_mainThreadContext.Post(d => OnIceCandidate?.Invoke(this, candidate), null);
                    }
                }
                else if (routedMessage.type == "health")
                {
                    var routedMessageHealth = JsonUtility.FromJson<RoutedMessage<string>>(content);
                    m_mainThreadContext.Post(d => OnClientHealthCheck?.Invoke(this, routedMessageHealth.id), null);
                }
                else if (routedMessage.type == "stats")
                {
                    // Debug.Log("got stats");
                }
                else if (routedMessage.type == "remote-update")
                {
                    var routedMessageRemoteUpdate = JsonUtility.FromJson<RoutedMessage<RemoteObjectStatusUpdate>>(content);
                    RemoteObjectStatusUpdate remoteStatusUpdate = routedMessageRemoteUpdate.data;
                    m_mainThreadContext.Post(d => OnRemoteObjectStatusUpdate?.Invoke(this,
                        remoteStatusUpdate.object_id, remoteStatusUpdate.remoteRendered), null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("MQTT Failed to parse message: " + ex);
            }
        }
    }
}
