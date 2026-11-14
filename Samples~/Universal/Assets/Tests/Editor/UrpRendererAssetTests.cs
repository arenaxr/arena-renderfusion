using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace ArenaUnity.RenderFusion.Tests
{
    public class UrpRendererAssetTests
    {
        [Test]
        public void RecreatingFeatureDoesNotAccumulateMaterials()
        {
            var feature = ScriptableObject.CreateInstance<RGBDepthFeature>();
            Shader shader = Shader.Find("Hidden/RGBDepthURP");
            Assert.That(shader, Is.Not.Null);
            try
            {
                feature.Create();
                int originalCount = Resources.FindObjectsOfTypeAll<Material>().Count(material => material.shader == shader);
                feature.Create();
                feature.Create();
                int currentCount = Resources.FindObjectsOfTypeAll<Material>().Count(material => material.shader == shader);
                Assert.That(currentCount, Is.EqualTo(originalCount), "Renderer recreation must release its previous material.");
            }
            finally
            {
                Object.DestroyImmediate(feature);
            }
        }

        [UnityTest]
        public IEnumerator TenMeterGeometryMatchesBrowserDepthRange()
        {
            var root = new GameObject("render-fusion-depth-range-test");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var target = new RenderTexture(100, 64, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(100, 64, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                root.AddComponent<ClientCamera>();
                var camera = root.GetComponent<Camera>();
                camera.enabled = false;
                camera.allowHDR = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                cube.layer = 30;
                camera.cullingMask = 1 << cube.layer;
                // The cube's front face is exactly ten metres from the camera.
                cube.transform.position = new Vector3(0, 0, 10.5f);
                cube.transform.localScale = new Vector3(100, 100, 1);
                material.SetColor("_BaseColor", Color.red);
                cube.GetComponent<Renderer>().sharedMaterial = material;
                target.Create();
                yield return null;

                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                foreach (var clipPlanes in new[]
                {
                    new Vector2(0.005f, 10000f),
                    new Vector2(0.1f, 1000f),
                    new Vector2(1f, 100f)
                })
                {
                    camera.nearClipPlane = clipPlanes.x;
                    camera.farClipPlane = clipPlanes.y;
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 100, 64), 0, 0);
                    pixels.Apply();

                    // The unchanged browser decodes metres / 200, independently of
                    // the renderer's clip planes: a ten-metre surface must encode 0.05.
                    Assert.That(pixels.GetPixel(75, 32).r, Is.EqualTo(0.05f).Within(0.015f),
                        $"Ten-metre depth changed with near={clipPlanes.x}, far={clipPlanes.y}.");

                    camera.cullingMask = 0;
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 100, 64), 0, 0);
                    pixels.Apply();
                    // The browser also uses white depth as its empty-background
                    // sentinel, even when the camera's far plane is less than 200 m.
                    Assert.That(pixels.GetPixel(75, 32).r, Is.EqualTo(1f).Within(0.015f),
                        $"Empty background lost its far-depth sentinel at near={clipPlanes.x}, far={clipPlanes.y}.");
                    camera.cullingMask = 1 << cube.layer;
                }
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(material);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        [UnityTest]
        public IEnumerator RenderGraphPacksColorDepthAndFrameIdOnlyForClientCameras()
        {
            var root = new GameObject("render-fusion-render-graph-test");
            var target = new RenderTexture(100, 64, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(100, 64, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                var camera = root.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 0;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.red;
                camera.allowHDR = false;
                target.Create();
                // Let the editor initialize the active render pipeline before submitting a request.
                yield return null;

                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 100, 64), 0, 0);
                pixels.Apply();
                Assert.That(pixels.GetPixel(75, 32).g, Is.LessThan(0.1f), "Ordinary cameras must remain unpacked.");

                var client = root.AddComponent<ClientCamera>();
                camera.backgroundColor = Color.red; // ClientCamera.Awake sets a transparent background.
                client.FrameID = 0x40000021;
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 100, 64), 0, 0);
                pixels.Apply();

                Assert.That(pixels.GetPixel(25, 32).r, Is.GreaterThan(0.9f), "Left half must contain camera color.");
                Assert.That(pixels.GetPixel(25, 32).g, Is.LessThan(0.1f));
                Assert.That(pixels.GetPixel(75, 32).g, Is.GreaterThan(0.9f), "Right half must contain far-plane depth.");
                // ReadPixels is bottom-left based; ARENA decodes the video at UV y=1.
                // x=69 encodes bit 5 (set); x=70 encodes bit 6 (clear).
                Assert.That(pixels.GetPixel(69, 63).g, Is.GreaterThan(0.9f),
                    "ARENA must find frame-ID bit 5 at the video top on every graphics backend.");
                Assert.That(pixels.GetPixel(70, 63).g, Is.LessThan(0.1f),
                    "ARENA must find frame-ID bit 6 at the video top on every graphics backend.");
                Assert.That(pixels.GetPixel(70, 0).g, Is.GreaterThan(0.9f),
                    "The opposite edge must retain its depth, not a misplaced frame-ID stripe.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(root);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void RgbDepthFeatureScriptResolvesToRuntimeType()
        {
            const string scriptPath = "Packages/io.conix.arena.renderfusion-urp/Runtime/RGBDepthFeature.cs";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);


            Assert.That(script, Is.Not.Null, $"Could not load {scriptPath}.");
            Assert.That(script.GetClass(), Is.EqualTo(typeof(RGBDepthFeature)),
                $"{scriptPath} does not resolve to {typeof(RGBDepthFeature).FullName}.");
        }

        [TestCase("Assets/Settings/URP-Balanced-Renderer.asset")]
        [TestCase("Assets/Settings/URP-HighFidelity-Renderer.asset")]
        [TestCase("Assets/Settings/URP-Performant-Renderer.asset")]
        public void RendererFeaturesAreLinked(string assetPath)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(assetPath);

            Assert.That(rendererData, Is.Not.Null, $"Could not load {assetPath}.");
            Assert.That(rendererData.rendererFeatures, Has.None.Null,
                $"{assetPath} contains a missing renderer feature.");
            Assert.That(rendererData.rendererFeatures, Has.Some.InstanceOf<RGBDepthFeature>(),
                $"{assetPath} does not contain the RenderFusion RGB/depth feature.");
        }
    }
}
