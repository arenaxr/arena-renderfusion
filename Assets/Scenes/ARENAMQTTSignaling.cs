using System;
using System.Text;
using System.Threading;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using ArenaUnity;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace ArenaUnity.CloudRendering.Signaling
{
    public class ARENAMQTTSignaling : ArenaMqttClient, ISignaling
    {
        private SynchronizationContext m_mainThreadContext;

        private string m_clientId;

        private string SERVER_OFFER_TOPIC_PREFIX = "realm/g/a/cloud_rendering_test/server/offer";
        private string SERVER_ANSWER_TOPIC_PREFIX = "realm/g/a/cloud_rendering_test/server/answer";
        private string SERVER_CANDIDATE_TOPIC_PREFIX = "realm/g/a/cloud_rendering_test/server/candidate";

        private string CLIENT_CONNECT_TOPIC_PREFIX = "realm/g/a/cloud_rendering_test/client/connect";
        private string CLIENT_DISCONNECT_TOPIC_PREFIX = "realm/g/a/cloud_rendering_test/client/disconnect";
        private string CLIENT_OFFER_TOPIC_PREFIX = "realm/g/a/cloud_rendering_test/client/offer";
        private string CLIENT_ANSWER_TOPIC_PREFIX = "realm/g/a/cloud_rendering_test/client/answer";
        private string CLIENT_CANDIDATE_TOPIC_PREFIX = "realm/g/a/cloud_rendering_test/client/candidate";

        private string SERVER_OFFER_TOPIC;
        private string SERVER_ANSWER_TOPIC;
        private string SERVER_CANDIDATE_TOPIC;

        public string Url { get { return "arena"; } }

        public ARENAMQTTSignaling(SynchronizationContext mainThreadContext)
        {
            m_clientId = "cloud-" + Guid.NewGuid().ToString();

            m_mainThreadContext = mainThreadContext;

            hostAddress = "mqtt.arenaxr.org";
            authType = Auth.Anonymous;

            SERVER_OFFER_TOPIC = $"{SERVER_OFFER_TOPIC_PREFIX}/{m_clientId}";
            SERVER_ANSWER_TOPIC = $"{SERVER_ANSWER_TOPIC_PREFIX}/{m_clientId}";
            SERVER_CANDIDATE_TOPIC = $"{SERVER_CANDIDATE_TOPIC_PREFIX}/{m_clientId}";
        }

        ~ARENAMQTTSignaling()
        {
            CloseConnection();
        }

        public void ConnectArena()
        {
            StartCoroutine(SigninScene("example", "public", "realm", false));
        }

        public event OnClientConnectHandler OnClientConnect;
        public event OnClientDisconnectHandler OnClientDisconnect;
        public event OnStartHandler OnStart;
        public event OnOfferHandler OnOffer;
        public event OnAnswerHandler OnAnswer;
        public event OnIceCandidateHandler OnIceCandidate;

        public void OpenConnection()
        {
            Debug.Log($"MQTT opening connection to ARENA");
            ConnectArena();
            if (mqttClientConnected)
            {
                Debug.Log($"Permissions: {permissions}");

                // Subscribe(new string[] { "$SYS/#" });
                Subscribe(new string[] { $"{CLIENT_CONNECT_TOPIC_PREFIX}/#" });
                Subscribe(new string[] { $"{CLIENT_DISCONNECT_TOPIC_PREFIX}/#" });
                Subscribe(new string[] { $"{CLIENT_OFFER_TOPIC_PREFIX}/#" });
                Subscribe(new string[] { $"{CLIENT_ANSWER_TOPIC_PREFIX}/#" });
                Subscribe(new string[] { $"{CLIENT_CANDIDATE_TOPIC_PREFIX}/#" });

                OnConnected();
            }
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

        protected override void OnConnected()
        {
            Debug.Log("MQTT connected!");
            m_mainThreadContext.Post(d => OnStart?.Invoke(this), null);
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
            }
            catch (Exception ex)
            {
                Debug.LogError("MQTT Failed to parse message: " + ex);
            }
        }
    }
}
