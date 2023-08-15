using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using ArenaUnity;

namespace ArenaUnity.RenderFusion.Signaling
{
    public class ARENAMQTTSignaling : ISignaling
    {
        private static readonly string TOPIC_PREFIX = "realm/g/a";

        private readonly string SERVER_OFFER_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/server/offer";
        private readonly string SERVER_ANSWER_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/server/answer";
        private readonly string SERVER_CANDIDATE_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/server/candidate";
        private readonly string SERVER_HEALTH_CHECK_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/server/health";
        private readonly string SERVER_STATS_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/server/stats";
        private readonly string SERVER_CONNECT_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/server/connect";

        private readonly string CLIENT_CONNECT_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/client/connect";
        private readonly string CLIENT_DISCONNECT_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/client/disconnect";
        private readonly string CLIENT_OFFER_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/client/offer";
        private readonly string CLIENT_ANSWER_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/client/answer";
        private readonly string CLIENT_CANDIDATE_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/client/candidate";
        private readonly string CLIENT_HEALTH_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/client/health";
        // private readonly string CLIENT_STATS_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/client/stats";
        private readonly string HAL_CONNECT_TOPIC_PREFIX = $"{TOPIC_PREFIX}/hybrid_rendering/HAL/connect";

        private string SERVER_OFFER_TOPIC;
        private string SERVER_ANSWER_TOPIC;
        private string SERVER_CANDIDATE_TOPIC;
        private string SERVER_HEALTH_CHECK_TOPIC;
        private string SERVER_STATS_TOPIC;
        private string SERVER_CONNECT_TOPIC;

        private string[] m_subbedTopics;

        private SynchronizationContext m_mainThreadContext;

        private string m_clientId;

        private string m_halID;
        private bool m_halStatus;

        public string Url { get { return ArenaClientScene.Instance.sceneUrl; } }

        public ARENAMQTTSignaling(SynchronizationContext mainThreadContext) {
            var scene = ArenaClientScene.Instance;

            m_clientId = "cloud-" + Guid.NewGuid().ToString();

            SERVER_OFFER_TOPIC = $"{SERVER_OFFER_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}";
            SERVER_ANSWER_TOPIC = $"{SERVER_ANSWER_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}";
            SERVER_CANDIDATE_TOPIC = $"{SERVER_CANDIDATE_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}";
            SERVER_HEALTH_CHECK_TOPIC = $"{SERVER_HEALTH_CHECK_PREFIX}/{scene.namespaceName}/{scene.sceneName}";
            SERVER_STATS_TOPIC = $"{SERVER_STATS_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}";
            SERVER_CONNECT_TOPIC = $"{SERVER_CONNECT_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}";

            m_subbedTopics = new string[] {
                $"{CLIENT_CONNECT_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}/#",
                $"{CLIENT_DISCONNECT_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}/#",
                $"{CLIENT_OFFER_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}/#",
                $"{CLIENT_ANSWER_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}/#",
                $"{CLIENT_CANDIDATE_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}/#",
                $"{CLIENT_HEALTH_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}/#",
                // $"{CLIENT_STATS_TOPIC_PREFIX}/{scene.namespaceName}/{scene.sceneName}/#",
                $"{HAL_CONNECT_TOPIC_PREFIX}/{m_halID}/#",
            };

            for (int i = 0; i < m_subbedTopics.Length; i++) {
                scene.Subscribe(new string[] { m_subbedTopics[i] });
            }

            scene.OnMessageCallback += ProcessMessage;

            m_mainThreadContext = mainThreadContext;
            m_mainThreadContext.Post(d => OnStart?.Invoke(this), null);
        }

        public event OnClientConnectHandler OnClientConnect;
        public event OnClientDisconnectHandler OnClientDisconnect;
        public event OnStartHandler OnStart;
        public event OnOfferHandler OnOffer;
        public event OnAnswerHandler OnAnswer;
        public event OnIceCandidateHandler OnIceCandidate;
        public event OnClientHealthCheckHandler OnClientHealthCheck;
        public event OnHALConnectHandler OnHALConnect;

        public void OpenConnection()
        {
        }

        public void CloseConnection()
        {
        }

        private void Publish(string topic, string msg)
        {
            var scene = ArenaClientScene.Instance;
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(msg);
            scene.Publish(topic, payload);
        }

        public void SendConnect()
        {
            RoutedMessage<string> routedMessage = new RoutedMessage<string>
            {
                type = "connect",
                source = "server",
                id = m_clientId,
                data = ""
            };
            Publish(SERVER_CONNECT_TOPIC, JsonUtility.ToJson(routedMessage));
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
            var scene = ArenaClientScene.Instance;
            RoutedMessage<string> healthCheck = new RoutedMessage<string>
            {
                //Change id to what other senders are using
                type = "health",
                source = "server",
                id = id,
                data = $"{scene.namespaceName}/{scene.sceneName}"
            };
            Publish(SERVER_HEALTH_CHECK_TOPIC, JsonUtility.ToJson(healthCheck));
        }

        public void UpdateHALInfo(string id, bool halStatus)
        {
            m_halID = id;
            m_halStatus = halStatus;
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

        protected void ProcessMessage(string topic, byte[] msg)
        {
            if ( !m_subbedTopics.Any(s => topic.Contains( s.Substring(0,s.Length-2) )) ) return;

            try
            {
                var content = Encoding.UTF8.GetString(msg);

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
                else if(routedMessage.type == "HAL")
                {
                    var routedMessageConnectData = JsonUtility.FromJson<RoutedMessage<ConnectData>>(content);
                    m_mainThreadContext.Post(d => OnHALConnect?.Invoke(this, routedMessageConnectData.data), null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("MQTT Failed to parse message: " + ex);
            }
        }
    }
}
