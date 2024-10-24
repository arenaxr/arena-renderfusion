using System.Threading;
using Unity.WebRTC;

namespace ArenaUnity.RenderFusion.Signaling
{
    public delegate void OnClientConnectHandler(ISignaling signaler, ConnectData data);
    public delegate void OnClientDisconnectHandler(ISignaling signaler, string id);
    public delegate void OnStartHandler(ISignaling signaler);
    public delegate void OnOfferHandler(ISignaling signaler, SDPData offer);
    public delegate void OnAnswerHandler(ISignaling signaler, SDPData answer);
    public delegate void OnIceCandidateHandler(ISignaling signaler, CandidateData e);
    public delegate void OnClientHealthCheckHandler(ISignaling signaler, string id);
    public delegate void OnHALConnectHandler(ISignaling signaler, ConnectData data);

    public interface ISignaling
    {
        event OnClientConnectHandler OnClientConnect;
        event OnClientDisconnectHandler OnClientDisconnect;
        event OnStartHandler OnStart;
        event OnOfferHandler OnOffer;
        event OnAnswerHandler OnAnswer;
        event OnIceCandidateHandler OnIceCandidate;
        event OnClientHealthCheckHandler OnClientHealthCheck;
        event OnHALConnectHandler OnHALConnect;

        string Url { get; }

        void OpenConnection();
        void CloseConnection();
        void SendConnect();
        void SendOffer(string id, string toUid, RTCSessionDescription offer);
        void SendAnswer(string id, string toUid, RTCSessionDescription answer);
        void SendCandidate(string id, string toUid, RTCIceCandidate candidate);
        void SendHealthCheck(string id);
        void SendStats(string stats, string toUid);
        void UpdateHALInfo(string id, bool halStatus);
    }
}
