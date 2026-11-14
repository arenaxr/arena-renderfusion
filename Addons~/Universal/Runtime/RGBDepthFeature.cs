using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ArenaUnity.RenderFusion
{
    /// <summary>
    /// Packs camera color and linearized depth into one frame for RenderFusion streaming.
    /// Must be public: Unity cannot bind a script asset to an internal renderer feature type,
    /// which leaves serialized sub-assets reporting as missing scripts.
    /// </summary>
    public class RGBDepthFeature : ScriptableRendererFeature
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
            // Renderer assets can recreate features after quality/settings changes.
            CoreUtils.Destroy(m_material);
            m_material = null;
            m_renderPass = null;
            var shader = Shader.Find("Hidden/RGBDepthURP");
            if (shader != null)
            {
                m_material = CoreUtils.CreateEngineMaterial(shader);
                m_renderPass = new RGBDepthPass(name, m_material);
            }
            else
                Debug.LogError("Cannot find required shader Hidden/RGBDepthURP! RGBDepthFeature is disabled.");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_renderPass == null)
                return; // Create() failed, e.g. the shader is missing

            if (renderingData.cameraData.cameraType == CameraType.Game &&
                renderingData.cameraData.camera.GetComponent<ClientCamera>() != null) {
                m_renderPass.renderPassEvent = settings.renderPassEvent;
                // the pass samples the active color target and swaps in its own copy, so it can
                // never render straight to the backbuffer. SetupRenderPasses was removed with URP
                // compatibility mode, so this is configured here instead.
                m_renderPass.requiresIntermediateTexture = true;
                m_renderPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                renderer.EnqueuePass(m_renderPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_material);
            m_material = null;
            m_renderPass = null;
        }
    }
}
