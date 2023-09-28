using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ArenaUnity;

namespace ArenaUnity.RenderFusion
{
    internal class RGBDepthPass : ScriptableRenderPass
    {
        public FilterMode filterMode { get; set; }
        public RGBDepthFeature.Settings settings;

        private string m_profilerTag;

        private Material m_material = null;

        public RGBDepthPass(string tag, Material material)
        {
            m_profilerTag = tag;
            m_material = material;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.camera.cameraType != CameraType.Game)
                return;

            if (m_material == null)
                return;

            var clientCamera = cameraData.camera.gameObject.GetComponent<ClientCamera>();
            if (clientCamera == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(m_profilerTag);

            int hasDualCameras = (clientCamera.IsDualCamera) ? 1 : 0;
            int frameID = clientCamera.FrameID;

            m_material.SetInt("_DualCameras", hasDualCameras);
            m_material.SetInt("_FrameID", frameID);

            Blit(cmd, ref renderingData, m_material);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
