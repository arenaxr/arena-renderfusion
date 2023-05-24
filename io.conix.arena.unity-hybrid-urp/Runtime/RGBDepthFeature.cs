using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ArenaUnity;

namespace ArenaUnity.HybridRendering
{
    internal class RGBDepthFeature : ScriptableRendererFeature
    {
        internal class RGBDepthPass : ScriptableRenderPass
        {
            private ProfilingSampler m_profilingSampler = new ProfilingSampler("RGBDepth");
            private Material m_material;
            private RTHandle m_cameraColorTarget;
            private int m_hasDualCameras;
            private int m_frameID;

            public RGBDepthPass(Material material)
            {
                m_material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public void SetTarget(RTHandle colorHandle)
            {
                m_cameraColorTarget = colorHandle;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ConfigureTarget(m_cameraColorTarget);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cameraData = renderingData.cameraData;
                if (cameraData.camera.cameraType != CameraType.Game)
                    return;

                if (m_material == null)
                    return;

                if (m_cameraColorTarget == null)
                    return;

                var hybridCamera = cameraData.camera.gameObject.GetComponent<HybridCamera>();
                if (hybridCamera == null)
                    return;

                m_hasDualCameras = (hybridCamera.IsDualCamera) ? 1 : 0;
                m_frameID = hybridCamera.FrameID;

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, m_profilingSampler))
                {
                    m_material.SetInt("_DualCameras", m_hasDualCameras);
                    m_material.SetInt("_FrameID", m_frameID);
                    Blitter.BlitCameraTexture(cmd, m_cameraColorTarget, m_cameraColorTarget, m_material, 0);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }
        }

        [Range(0, 1)]
        private int m_hasDualCameras = 0;
        private int m_frameID = 0;

        private Material m_material;

        private RGBDepthPass m_RenderPass;

        public override void Create()
        {
            if (Shader.Find("Hidden/RGBDepthShaderURP") != null)
            {
                 m_material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/RGBDepthShaderURP"));
                 m_RenderPass = new RGBDepthPass(m_material);
            }
            else
                throw new InvalidOperationException("Cannot find required shader Hidden/RGBDepthShaderURP!");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game)
                renderer.EnqueuePass(m_RenderPass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType == CameraType.Game)
            {
                // Calling ConfigureInput with the ScriptableRenderPassInput.Color argument
                // ensures that the opaque texture is available to the Render Pass.
                m_RenderPass.ConfigureInput(ScriptableRenderPassInput.Color);
                m_RenderPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                m_RenderPass.SetTarget(renderer.cameraColorTargetHandle);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_material);
        }
    }
}
