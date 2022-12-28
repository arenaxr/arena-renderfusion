using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

[Serializable, VolumeComponentMenu("Post-processing/Custom/DepthShaderHD")]
public sealed class DepthShaderHD : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    Material m_material;

    public bool IsActive() => m_material != null;

    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    public override void Setup()
    {
        if (Shader.Find("Hidden/Shader/DepthShaderHD") != null)
            m_material = new Material(Shader.Find("Hidden/Shader/DepthShaderHD"));
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (m_material == null)
            return;

        m_material.SetTexture("_MainTex", source);

        HDUtils.DrawFullScreen(cmd, m_material, destination);
    }

    public override void Cleanup() => CoreUtils.Destroy(m_material);
}
