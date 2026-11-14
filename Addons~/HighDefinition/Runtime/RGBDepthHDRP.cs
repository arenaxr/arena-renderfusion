using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

namespace ArenaUnity.RenderFusion
{
    [Serializable, VolumeComponentMenu("Post-processing/Custom/RGBDepthHDRP")]
    public sealed class RGBDepthHDRP : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        private Material m_Material;

        public bool IsActive() => m_Material != null;

        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        public override void Setup()
        {
            CoreUtils.Destroy(m_Material);
            m_Material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/RGBDepthHDRP"));
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            if (m_Material == null)
                return;

            var clientCamera = camera.camera.gameObject.GetComponent<ClientCamera>();
            if (clientCamera == null)
            {
                HDUtils.BlitCameraTexture(cmd, source, destination);
                return;
            }
            m_Material.SetInteger("_FrameID", clientCamera.FrameID);
            m_Material.SetTexture("_MainTex", source);

            HDUtils.DrawFullScreen(cmd, m_Material, destination);
        }

        public override void Cleanup() {
            CoreUtils.Destroy(m_Material);
            m_Material = null;
        }
    }
}
