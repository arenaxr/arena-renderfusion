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

        private List<ClientPose> poseQueue;

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

            poseQueue = new List<ClientPose>();

            Debug.Log("Started cam");
        }

        // Update is called once per frame
        private void Update()
        {
            if (poseQueue.Count > 0)
            {
                foreach (var clientPose in poseQueue)
                {
                    UpdatePosition(clientPose.x, clientPose.y, clientPose.z);
                    UpdateRotation(
                        -clientPose.x_,
                        -clientPose.y_,
                        clientPose.z_,
                        clientPose.w_
                    );
                }
            }
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

        public void AddPose(ClientPose pose)
        {
            poseQueue.Add(pose);
        }

        private void UpdatePosition(float x, float y, float z)
        {
            cam.transform.position = new Vector3(x, y, z);
        }

        private void UpdateRotation(float x_, float y_, float z_, float w_)
        {
            cam.transform.localRotation = new Quaternion(x_, y_, z_, w_);
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
