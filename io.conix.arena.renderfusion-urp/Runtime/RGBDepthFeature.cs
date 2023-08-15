using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ArenaUnity;

namespace ArenaUnity.RenderFusion
{
    internal class RGBDepthFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            public int materialPassIndex = -1;
        }

        public Settings settings = new Settings();
        private Material m_material = null;
        private RGBDepthPass m_renderPass;

        public override void Create()
        {
            if (Shader.Find("Hidden/RGBDepthShaderURP") != null)
            {
                m_material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/RGBDepthShaderURP"));
                m_renderPass = new RGBDepthPass(name, m_material);
            }
            else
                throw new InvalidOperationException("Cannot find required shader Hidden/RGBDepthShaderURP!");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game) {
                m_renderPass.renderPassEvent = settings.renderPassEvent;
                m_renderPass.settings = settings;
                renderer.EnqueuePass(m_renderPass);
            }
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType == CameraType.Game)
            {
                // Calling ConfigureInput with the ScriptableRenderPassInput.Color argument
                // ensures that the opaque texture is available to the Render Pass.
                m_renderPass.ConfigureInput(ScriptableRenderPassInput.Color);
                m_renderPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_material);
        }
    }
}
