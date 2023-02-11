using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ArenaUnity;

namespace ArenaUnity.HybridRendering
{
    internal class RGBDepthRendererFeature : ScriptableRendererFeature
    {
        [Range(0, 1)]
        public int m_HasDualCameras = 0;

        private Shader m_Shader;

        private Material m_Material;

        private RGBDepthPass m_RenderPass;

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
                var hybridCamera = cameraData.camera.gameObject.GetComponent<HybridCamera>();
                if (hybridCamera)
                    m_HasDualCameras = (hybridCamera.isDualCamera) ? 1 : 0;

                // Calling ConfigureInput with the ScriptableRenderPassInput.Color argument
                // ensures that the opaque texture is available to the Render Pass.
                m_RenderPass.ConfigureInput(ScriptableRenderPassInput.Color);
                m_RenderPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                m_RenderPass.SetTarget(renderer.cameraColorTargetHandle, m_HasDualCameras);
            }
        }

        public override void Create()
        {
            m_Shader = Shader.Find("Hidden/RGBDepthShaderURP");
            m_Material = CoreUtils.CreateEngineMaterial(m_Shader);
            m_RenderPass = new RGBDepthPass(m_Material);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_Material);
        }
    }
}
