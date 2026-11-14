using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ArenaUnity.RenderFusion.Tests
{
    public class HdrpDepthTests
    {
        [Test]
        public void VolumeScriptResolvesToRuntimeType()
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Packages/io.conix.arena.renderfusion-hdrp/Runtime/RGBDepthHDRP.cs");
            Assert.That(script, Is.Not.Null);
            Assert.That(script.GetClass(), Is.EqualTo(typeof(RGBDepthHDRP)));
        }

        [Test]
        public void PackingShaderCompiles()
        {
            var shader = Shader.Find("Hidden/RGBDepthHDRP");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var target = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                // Vulkan batch mode has no backbuffer to bind this shader pass against.
                RenderTexture.active = target;
                Assert.That(material.SetPass(0), Is.True);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void NonClientCameraKeepsOriginalColorAcrossWholeFrame()
        {
            var root = new GameObject("ordinary-camera");
            var source = new RenderTexture(64, 32, 0, RenderTextureFormat.ARGB32);
            var target = new RenderTexture(64, 32, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(64, 32);
            var effect = ScriptableObject.CreateInstance<RGBDepthHDRP>();
            var command = new CommandBuffer();
            RenderPipelineAsset previousPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset previousQualityPipeline = QualitySettings.renderPipeline;
            HDRenderPipelineAsset temporaryPipeline = null;
            bool initializedBlitter = false;
            RTHandle sourceHandle = null;
            RTHandle targetHandle = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                // Full HDRP projects already initialize the blitter. Also support running
                // this component test in isolation, without replacing a live pipeline's resources.
                if (Blitter.GetBlitMaterial(TextureDimension.Tex2D) == null)
                {
                    temporaryPipeline = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                    GraphicsSettings.defaultRenderPipeline = temporaryPipeline;
                    QualitySettings.renderPipeline = null;
                    Blitter.Initialize(Shader.Find("Hidden/HDRP/Blit"), Shader.Find("Hidden/HDRP/BlitColorAndDepth"));
                    initializedBlitter = true;
                }
                source.Create();
                target.Create();
                sourceHandle = RTHandles.Alloc(source);
                targetHandle = RTHandles.Alloc(target);
                RenderTexture.active = source;
                GL.Clear(true, true, Color.red);
                effect.Setup();
                var camera = root.AddComponent<Camera>();
                effect.Render(command, HDCamera.GetOrCreate(camera), sourceHandle, targetHandle);
                Graphics.ExecuteCommandBuffer(command);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 64, 32), 0, 0);
                pixels.Apply();
                Assert.That(pixels.GetPixel(16, 20).r, Is.GreaterThan(0.9f));
                Assert.That(pixels.GetPixel(48, 20).r, Is.GreaterThan(0.9f));
                Assert.That(pixels.GetPixel(48, 20).g, Is.LessThan(0.1f));
            }
            finally
            {
                RenderTexture.active = previous;
                effect.Cleanup();
                command.Dispose();
                sourceHandle?.Release();
                targetHandle?.Release();
                Object.DestroyImmediate(effect);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                if (initializedBlitter)
                    Blitter.Cleanup();
                if (temporaryPipeline != null)
                {
                    GraphicsSettings.defaultRenderPipeline = previousPipeline;
                    QualitySettings.renderPipeline = previousQualityPipeline;
                    Object.DestroyImmediate(temporaryPipeline);
                }
            }
        }
    }
}
