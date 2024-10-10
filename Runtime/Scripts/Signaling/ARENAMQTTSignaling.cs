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
        private ArenaTopics subRenderServerTopic;

        private string[] m_subbedTopics;

        private SynchronizationContext m_mainThreadContext;

        private string m_clientId;

        private string m_halID;
        private bool m_halStatus;

        public string Url { get { return ArenaClientScene.Instance.sceneUrl; } }

        public ARENAMQTTSignaling(SynchronizationContext mainThreadContext) {
            var scene = ArenaClientScene.Instance;

            m_clientId = "cloud-" + Guid.NewGuid().ToString();
            subRenderServerTopic = new ArenaTopics(
                realm: scene.realm,
                name_space: scene.namespaceName,
                scenename: scene.sceneName,
                idtag: "-"
            );
            m_subbedTopics = new string[] {
                $"{subRenderServerTopic.SUB_SCENE_RENDER_PRIVATE}",
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

        private void Publish(string toUid, string msg)
        {
            var scene = ArenaClientScene.Instance;
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(msg);
            var pubRenderServerTopic = new ArenaTopics(
                realm: scene.realm,
                name_space: scene.namespaceName,
                scenename: scene.sceneName,
                idtag: "-",
                touid: toUid
            );
            var topic = (toUid == null) ? pubRenderServerTopic.PUB_SCENE_RENDER : pubRenderServerTopic.PUB_SCENE_RENDER_PRI_SERV;
            scene.Publish(topic, payload);
            Debug.Log($"MQTT Sent: {topic} {msg}");
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
            Publish(null, JsonUtility.ToJson(routedMessage));
        }

        public void SendOffer(string id, string toUid, RTCSessionDescription offer)
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

            Publish(toUid, JsonUtility.ToJson(routedMessage));
        }

        public void SendAnswer(string id, string toUid, RTCSessionDescription answer)
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

            Publish(toUid, JsonUtility.ToJson(routedMessage));
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
            Publish(null, JsonUtility.ToJson(healthCheck));
        }

        public void UpdateHALInfo(string id, bool halStatus)
        {
            m_halID = id;
            m_halStatus = halStatus;
        }

        public void SendCandidate(string id, string toUid, RTCIceCandidate candidate)
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

            Publish(toUid, JsonUtility.ToJson(routedMessage));
        }

        public void SendStats(string stats, string toUid)
        {
            Publish(toUid, stats);
        }

        protected void ProcessMessage(string topic, string content)
        {
            // filter messages based on expected payload format
            var topicSplit = topic.Split("/");
            if (topicSplit.Length <= 4 || topicSplit[4] != "r") return;

            Debug.Log($"MQTT Received: {topic} {content}");

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
