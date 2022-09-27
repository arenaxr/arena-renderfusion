using Unity.WebRTC;
using System.Threading;

namespace ArenaUnity.HybridRendering.Signaling
{
    public delegate void OnClientConnectHandler(ISignaling signaler, string id);
    public delegate void OnClientDisconnectHandler(ISignaling signaler, string id);
    public delegate void OnStartHandler(ISignaling signaler);
    public delegate void OnOfferHandler(ISignaling signaler, SDPData offer);
    public delegate void OnAnswerHandler(ISignaling signaler, SDPData answer);
    public delegate void OnIceCandidateHandler(ISignaling signaler, CandidateData e);
    public delegate void OnRemoteObjectStatusUpdateHandler(ISignaling signaler, string objectId, bool remoteRendered);

    public interface ISignaling
    {
        event OnClientConnectHandler OnClientConnect;
        event OnClientDisconnectHandler OnClientDisconnect;
        event OnStartHandler OnStart;
        event OnOfferHandler OnOffer;
        event OnAnswerHandler OnAnswer;
        event OnIceCandidateHandler OnIceCandidate;
        event OnRemoteObjectStatusUpdateHandler OnRemoteObjectStatusUpdate;

        string Url { get; }

        void SetSyncContext(SynchronizationContext mainThreadContext);
        void OpenConnection();
        void CloseConnection();
        void SendOffer(string id, RTCSessionDescription offer);
        void SendAnswer(string id, RTCSessionDescription answer);
        void SendCandidate(string id, RTCIceCandidate candidate);
        void BroadcastHealthCheck(string id);
        void SendStats(string stats);
    }
}
