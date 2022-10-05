using System;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ArenaUnity.HybridRendering.Signaling;

namespace ArenaUnity.HybridRendering
{
    [RequireComponent(typeof(Camera))]
    public class CameraStream : MonoBehaviour
    {
        static Vector2Int videoSize = new Vector2Int(1920, 1080);

        private Camera cam;
        private Material mat;
        public RenderTexture renderTarget;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.fieldOfView = 80f; // match arena
            cam.nearClipPlane = 0.1f; // match arena
            cam.farClipPlane = 10000f; // match arena
            // cam.backgroundColor = Color.clear;
            cam.depthTextureMode = DepthTextureMode.Depth;

            mat = new Material(Shader.Find("Hidden/DepthShader"));

            renderTarget = CreateRenderTexture(2 * videoSize.x, videoSize.y);
        }

        // Update is called once per frame
        private void Update()
        {

        }

        private void OnDestroy()
        {
            cam = null;
        }

        private RenderTexture CreateRenderTexture(int width, int height)
        {
            var format = WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType);
            return new RenderTexture(width, height, 0, format);
        }

        public VideoStreamTrack GetTrack()
        {
            // return cam.CaptureStreamTrack(2 * videoSize.x, videoSize.y, 0);
            return new VideoStreamTrack(renderTarget, true);
        }

        public void UpdatePose(ClientPose clientPose)
        {
            // System.DateTime epochStart = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            // long currTime = (long)(System.DateTime.UtcNow - epochStart).TotalMilliseconds;
            // Debug.Log($"{currTime} {clientPose.ts} {currTime - clientPose.ts}");

            cam.transform.position = ArenaUnity.ToUnityPosition(new Vector3(clientPose.x, clientPose.y, clientPose.z));
            cam.transform.localRotation = ArenaUnity.ToUnityRotationQuat(new Quaternion(
                clientPose.x_,
                clientPose.y_,
                clientPose.z_,
                clientPose.w_
            ));
        }

        // private void OnPreRender()
        // {
        //     Shader.SetGlobalMatrix(Shader.PropertyToID("UNITY_MATRIX_IV"), Cam.cameraToWorldMatrix);
        // }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (cam != Camera.main)
            {
                Graphics.Blit(source, renderTarget, mat);
            }
            else
            {
                Graphics.Blit(source, destination, mat);
            }
        }
    }
}
