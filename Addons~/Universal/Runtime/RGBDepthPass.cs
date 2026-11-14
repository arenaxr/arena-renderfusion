using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ArenaUnity.RenderFusion
{
    internal class RGBDepthPass : ScriptableRenderPass
    {
        // the shader samples the camera textures under their compatibility-mode global names
        private static readonly int k_CameraColorTexture = Shader.PropertyToID("_CameraColorTexture");
        private static readonly int k_CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int k_FrameID = Shader.PropertyToID("_FrameID");

        private string m_profilerTag;
        private string m_destinationName;

        private Material m_material = null;

        private class PassData
        {
            internal TextureHandle source;
            internal TextureHandle depth;
            internal Material material;
        }

        public RGBDepthPass(string tag, Material material)
        {
            m_profilerTag = tag;
            m_destinationName = $"{tag}Target";
            m_material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_material == null)
                return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.camera.cameraType != CameraType.Game)
                return;

            var clientCamera = cameraData.camera.gameObject.GetComponent<ClientCamera>();
            if (clientCamera == null)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError($"{m_profilerTag}: cannot run when the active target is the backbuffer. " +
                               "Use a renderPassEvent before rendering completes.");
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid())
                return;

            // SetInt stores a float internally and loses low frame-ID bits above 2^24.
            m_material.SetInteger(k_FrameID, clientCamera.FrameID);

            // a pass cannot read and write the same texture, so blit into a copy and swap it in.
            // RGBDepthFeature sets requiresIntermediateTexture, so the source is always a
            // graph-created texture with a valid descriptor here, never the imported backbuffer.
            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = m_destinationName;
            destinationDesc.clearBuffer = false;
            // a full screen blit gains nothing from multisampling, and it would force a resolve
            destinationDesc.msaaSamples = MSAASamples.None;
            destinationDesc.bindTextureMS = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_profilerTag, out var passData))
            {
                passData.material = m_material;
                passData.source = source;
                passData.depth = resourceData.cameraDepthTexture;

                builder.UseTexture(passData.source, AccessFlags.Read);
                if (passData.depth.IsValid())
                    builder.UseTexture(passData.depth, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0);
                // required to bind the camera textures as globals below, as URP's own passes do
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(k_CameraColorTexture, data.source);
                    if (data.depth.IsValid())
                        context.cmd.SetGlobalTexture(k_CameraDepthTexture, data.depth);

                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
                });
            }

            resourceData.cameraColor = destination;
        }
    }
}
