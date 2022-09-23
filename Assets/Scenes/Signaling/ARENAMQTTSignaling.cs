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

        private string SERVER_OFFER_TOPIC_PREFIX = "realm/g/a/cloud_rendering/server/offer";
        private string SERVER_ANSWER_TOPIC_PREFIX = "realm/g/a/cloud_rendering/server/answer";
        private string SERVER_CANDIDATE_TOPIC_PREFIX = "realm/g/a/cloud_rendering/server/candidate";
        private string SERVER_HEALTH_CHECK = "realm/g/a/cloud_rendering/server/health";

        private string CLIENT_CONNECT_TOPIC_PREFIX = "realm/g/a/cloud_rendering/client/connect";
        private string CLIENT_DISCONNECT_TOPIC_PREFIX = "realm/g/a/cloud_rendering/client/disconnect";
        private string CLIENT_OFFER_TOPIC_PREFIX = "realm/g/a/cloud_rendering/client/offer";
        private string CLIENT_ANSWER_TOPIC_PREFIX = "realm/g/a/cloud_rendering/client/answer";
        private string CLIENT_CANDIDATE_TOPIC_PREFIX = "realm/g/a/cloud_rendering/client/candidate";
        private string CLIENT_STATS_TOPIC_PREFIX = "realm/g/a/cloud_rendering/client/stats";

        private string UPDATE_REMOTE_STATUS_TOPIC_PREFIX = "realm/g/a/cloud_rendering/client/remote";

        private string SERVER_OFFER_TOPIC;
        private string SERVER_ANSWER_TOPIC;
        private string SERVER_CANDIDATE_TOPIC;

        public string Url { get { return "arena"; } }

        protected override void Awake()
        {
            m_clientId = "cloud-" + Guid.NewGuid().ToString();

            hostAddress = "arena-dev1.conix.io";
            authType = Auth.Manual;

            SERVER_OFFER_TOPIC = $"{SERVER_OFFER_TOPIC_PREFIX}/{m_clientId}";
            SERVER_ANSWER_TOPIC = $"{SERVER_ANSWER_TOPIC_PREFIX}/{m_clientId}";
            SERVER_CANDIDATE_TOPIC = $"{SERVER_CANDIDATE_TOPIC_PREFIX}/{m_clientId}";

            base.Awake();
            name = "ARENA MQTT Signaler (Starting...)";
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
        public event OnRemoteObjectStatusUpdateHandler OnRemoteObjectStatusUpdate;

        public void ConnectArena()
        {
            name = "ARENA MQTT Signaler (Connecting...)";
            StartCoroutine(Signin());
        }

        public void OpenConnection()
        {
            Debug.Log($"MQTT opening connection to ARENA");
            ConnectArena();
        }

        protected override void OnConnected()
        {
            // Subscribe(new string[] { "$SYS/#" });
            Subscribe(new string[] { $"{CLIENT_CONNECT_TOPIC_PREFIX}/#" });
            Subscribe(new string[] { $"{CLIENT_DISCONNECT_TOPIC_PREFIX}/#" });
            Subscribe(new string[] { $"{CLIENT_OFFER_TOPIC_PREFIX}/#" });
            Subscribe(new string[] { $"{CLIENT_ANSWER_TOPIC_PREFIX}/#" });
            Subscribe(new string[] { $"{CLIENT_CANDIDATE_TOPIC_PREFIX}/#" });
            Subscribe(new string[] { $"{CLIENT_STATS_TOPIC_PREFIX}/#" });
            Subscribe(new string[] { $"{UPDATE_REMOTE_STATUS_TOPIC_PREFIX}/#" });

            Debug.Log("MQTT connected!");
            name = "ARENA MQTT Signaler (Connected)";
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

        public void BroadcastHealthCheck(string id){
            RoutedMessage<String> healthCheck = new RoutedMessage<String>
            {
                //Change id to what other senders are using
                type = "health",
                source = "server",
                id = id,
                data = "somemessage"
            };
            Publish(SERVER_HEALTH_CHECK,JsonUtility.ToJson(healthCheck));
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

                if (routedMessage.type == "connect")
                {
                    m_mainThreadContext.Post(d => OnClientConnect?.Invoke(this, routedMessage.id), null);
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
