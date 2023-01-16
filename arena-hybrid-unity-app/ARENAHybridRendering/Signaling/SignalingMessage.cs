using System;

namespace ArenaUnity.HybridRendering.Signaling
{
    #pragma warning disable 0649
    [System.Serializable]
    public class RoutedMessage<T>
    {
        public string type;
        public string source;
        public string id;
        public int ts;
        public T data;
    }

    [System.Serializable]
    public class ConnectData
    {
        public string id;
        public string deviceType;
        public string namespacedScene;
        public int screenWidth;
        public int screenHeight;
    }

    [System.Serializable]
    public class SDPData
    {
        public string type;
        public string id;
        public string sdp;
    }

    [System.Serializable]
    public class CandidateData
    {
        public string id;
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
        // public int usernameFragment;
    }

    [System.Serializable]
    public class ClientPose
    {
        public float x;
        public float y;
        public float z;
        public float x_;
        public float y_;
        public float z_;
        public float w_;
        public long ts;
    }

    [System.Serializable]
    public class RemoteObjectStatusUpdate
    {
        public string object_id;
        public bool remoteRendered;
    }
    #pragma warning restore 0649
}
