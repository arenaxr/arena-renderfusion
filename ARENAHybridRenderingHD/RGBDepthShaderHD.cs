using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;
using ArenaUnity;

namespace ArenaUnity.HybridRendering
{
    [Serializable, VolumeComponentMenu("Post-processing/Custom/RGBDepthShaderHD")]
    public sealed class RGBDepthShaderHD : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        private Material m_Material;

        private int m_HasDualCameras = 0;
        private int m_FrameID = 0;

        public bool IsActive() => m_Material != null;

        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        public override void Setup()
        {
            if (Shader.Find("Hidden/RGBDepthShaderHD") != null)
                m_Material = new Material(Shader.Find("Hidden/RGBDepthShaderHD"));
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            if (m_Material == null)
                return;

            var hybridCamera = camera.camera.gameObject.GetComponent<HybridCamera>();
            if (hybridCamera)
            {
                m_HasDualCameras = (hybridCamera.IsDualCamera) ? 1 : 0;
                m_FrameID = hybridCamera.FrameID;
            }
            m_Material.SetTexture("_MainTex", source);
            m_Material.SetInt("_DualCameras", m_HasDualCameras);
            m_Material.SetInt("_FrameID", m_FrameID);
            Debug.Log(m_FrameID);

            HDUtils.DrawFullScreen(cmd, m_Material, destination);
        }

        public override void Cleanup() => CoreUtils.Destroy(m_Material);
    }
}
