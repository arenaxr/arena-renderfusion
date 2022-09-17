using System;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ArenaUnity.HybridRendering
{
    public class CameraStream
    {
        static Vector2Int videoSize = new Vector2Int(1280, 720);

        private Camera cam;
        private GameObject gobj;

        private string m_id;

        private RenderTexture renderTarget;

        public CameraStream(string id)
        {
            m_id = id;

            gobj = new GameObject(id);
            cam = gobj.transform.gameObject.AddComponent<Camera>();
            cam.fieldOfView = 80f; // match arena
            cam.nearClipPlane = 0.1f; // match arena
            cam.farClipPlane = 10000f; // match arena
            // cam.backgroundColor = Color.clear;

            gobj.AddComponent<CameraDepth>();

            renderTarget = CreateRenderTexture(2 * videoSize.x, videoSize.y);
            gobj.GetComponent<CameraDepth>().renderTarget = renderTarget;
        }

        ~CameraStream()
        {
            Dispose();
        }

        private RenderTexture CreateRenderTexture(int width, int height) {
            var format = WebRTC.GetSupportedRenderTextureFormat(SystemInfo.graphicsDeviceType);
            return new RenderTexture(width, height, 0, format);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(gobj);
            cam = null;
        }

        public VideoStreamTrack GetTrack() {
            // return cam.CaptureStreamTrack(2 * videoSize.x, videoSize.y, 0);
            return new VideoStreamTrack(renderTarget, true);
        }

        public void updatePosition(float x, float y, float z) {
            if (cam) {
                cam.transform.position = new Vector3(x, y, z);
            }
        }

        public void updateRotation(float x_, float y_, float z_, float w_) {
            if (cam) {
                cam.transform.localRotation = new Quaternion(x_, y_, z_, w_);
            }
        }
    }
}
