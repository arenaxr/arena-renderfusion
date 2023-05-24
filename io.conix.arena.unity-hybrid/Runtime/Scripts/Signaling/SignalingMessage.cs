using System;
using UnityEngine;


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
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        public bool isMac = true;
#else
        public bool isMac = false;
#endif
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

    // [System.Serializable]
    // public class ClientPose
    // {
    //     public int id;
    //     public double x;
    //     public double y;
    //     public double z;
    //     public double x_;
    //     public double y_;
    //     public double z_;
    //     public double w_;
    //     public long ts;

    //     public ClientPose(int id, double x, double y, double z, double x_, double y_, double z_, double w_) {
    //         this.id = id;
    //         this.x = x;
    //         this.y = y;
    //         this.z = z;
    //         this.x_ = x_;
    //         this.y_ = y_;
    //         this.z_ = z_;
    //         this.w_ = w_;
    //     }
    // }

    [System.Serializable]
    public class ClientPose
    {
        public int id;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public ClientPose(int id, Vector3 position, Quaternion rotation, Vector3 scale) {
            this.id = id;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }

    [System.Serializable]
    public class ClientStatus
    {
        public bool isVRMode;
        public bool isARMode;
        public bool hasDualCameras;
        public float ipd;
        public float[] leftProj;
        public float[] rightProj;
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
