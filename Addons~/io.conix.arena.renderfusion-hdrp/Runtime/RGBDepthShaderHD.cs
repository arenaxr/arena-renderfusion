using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;
using ArenaUnity;

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
            if (Shader.Find("Hidden/RGBDepthHDRP") != null)
                m_Material = new Material(Shader.Find("Hidden/RGBDepthHDRP"));
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            if (m_Material == null)
                return;

            var clientCamera = camera.camera.gameObject.GetComponent<ClientCamera>();
            if (clientCamera)
            {
                int hasDualCameras = (clientCamera.IsDualCamera) ? 1 : 0;
                int frameID = clientCamera.FrameID;

                m_Material.SetInt("_HasStereoCameras", hasDualCameras);
                m_Material.SetInt("_FrameID", frameID);
            }
            m_Material.SetTexture("_MainTex", source);

            HDUtils.DrawFullScreen(cmd, m_Material, destination);
        }

        public override void Cleanup() {
            CoreUtils.Destroy(m_Material);
        }
    }
}
