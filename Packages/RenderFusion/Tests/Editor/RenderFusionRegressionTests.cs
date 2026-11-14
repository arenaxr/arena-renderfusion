using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using ArenaUnity.RenderFusion.Signaling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ArenaUnity.RenderFusion.Tests
{
    public class CameraStreamTests
    {
        private GameObject m_root;

        [SetUp]
        public void SetUp()
        {
            m_root = new GameObject("camera-stream-test");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_root);
        }

        [Test]
        public void HasDualCamerasReflectsRightCameraActivation()
        {
            var stream = m_root.AddComponent<CameraStream>();
            var left = new GameObject("camera-left").AddComponent<ClientCamera>();
            var right = new GameObject("camera-right").AddComponent<ClientCamera>();
            left.transform.SetParent(m_root.transform);
            right.transform.SetParent(m_root.transform);

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CameraStream).GetField("m_clientCameraLeft", flags).SetValue(stream, left);
            typeof(CameraStream).GetField("m_clientCameraRight", flags).SetValue(stream, right);

            right.gameObject.SetActive(false);
            Assert.That(stream.hasDualCameras, Is.False);

            right.gameObject.SetActive(true);
            Assert.That(stream.hasDualCameras, Is.True);

            right.gameObject.SetActive(false);
            Assert.That(stream.hasDualCameras, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(0.5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.MaxValue)]
        public void SetFrameRateRejectsUnencodableValues(float frameRate)
        {
            var stream = m_root.AddComponent<CameraStream>();

            Assert.Throws<ArgumentOutOfRangeException>(() => stream.SetFrameRate(frameRate));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0.5f)]
        public void SetScaleResolutionDownRejectsInvalidValues(float scale)
        {
            var stream = m_root.AddComponent<CameraStream>();
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.SetScaleResolutionDown(scale));
        }

        [Test]
        public void LatestPosePreservesFrameIdBeyondFloatPrecision()
        {
            var stream = m_root.AddComponent<CameraStream>();
            // Awake is not called for ordinary MonoBehaviours in EditMode tests.
            typeof(CameraStream).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(stream, null);
            double[] pose = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 2, 3, 4, 1, 16777217 };
            var bytes = new byte[pose.Length * sizeof(double)];
            Buffer.BlockCopy(pose, 0, bytes, 0, bytes.Length);
            stream.OnInputMessage(bytes);
            typeof(CameraStream).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(stream, null);

            foreach (var camera in m_root.GetComponentsInChildren<ClientCamera>(true))
                Assert.That(camera.FrameID, Is.EqualTo(16777217), "The browser matches frames using the exact integer ID.");
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.MaxValue)]
        public void MalformedFrameIdDoesNotEscapeInputCallback(double frameId)
        {
            var stream = m_root.AddComponent<CameraStream>();
            double[] pose = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 2, 3, 4, 1, frameId };
            var bytes = new byte[pose.Length * sizeof(double)];
            Buffer.BlockCopy(pose, 0, bytes, 0, bytes.Length);
            Assert.DoesNotThrow(() => stream.OnInputMessage(bytes));
        }

        [Test]
        public void StreamTextureDoesNotAdoptBorrowedCameraTarget()
        {
            var camera = m_root.AddComponent<Camera>();
            var client = m_root.AddComponent<ClientCamera>();
            typeof(ClientCamera).GetField("m_camera", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(client, camera);
            var borrowed = new RenderTexture(8, 8, 0);
            try
            {
                borrowed.Create();
                camera.targetTexture = borrowed;
                var owned = (RenderTexture)typeof(ClientCamera)
                    .GetMethod("CreateRenderTexture", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(client, new object[] { 32, 16, 32, 16 });

                Assert.That(owned, Is.Not.SameAs(borrowed), "A stream must allocate and own its target.");
                Assert.That(borrowed.width, Is.EqualTo(8), "A caller-owned texture must not be resized.");
                Assert.That(owned.width, Is.EqualTo(64));
                Assert.That(owned.height, Is.EqualTo(16));
            }
            finally
            {
                camera.targetTexture = null;
                borrowed.Release();
                Object.DestroyImmediate(borrowed);
            }
        }

        [Test]
        public void RecreatingStreamTextureUsesRequestedDimensions()
        {
            var camera = m_root.AddComponent<Camera>();
            var client = m_root.AddComponent<ClientCamera>();
            typeof(ClientCamera).GetField("m_camera", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(client, camera);
            MethodInfo create = typeof(ClientCamera)
                .GetMethod("CreateRenderTexture", BindingFlags.Instance | BindingFlags.NonPublic);
            var first = (RenderTexture)create.Invoke(client, new object[] { 16, 16, 16, 16 });
            var second = (RenderTexture)create.Invoke(client, new object[] { 32, 24, 32, 24 });

            Assert.That(second.width, Is.EqualTo(64));
            Assert.That(second.height, Is.EqualTo(24));
            Assert.That(first == null || !first.IsCreated(), Is.True, "The old owned target must be released.");
        }
    }

    public class ClientCameraProjectionTests
    {
        private GameObject m_root;
        private ClientCamera m_client;
        private Camera m_camera;

        // Exact binary-friendly OpenGL coefficients for near=1, far=9, with an
        // asymmetric horizontal/vertical frustum. Expected values do not depend
        // on the production projection builder.
        private static float[] AsymmetricProjection => new[] { 2.4f, 0.3f, 1.8f, -0.2f, -1.25f, -2.25f, -1f };

        [SetUp]
        public void SetUp()
        {
            m_root = new GameObject("client-camera-projection-test");
            m_camera = m_root.AddComponent<Camera>();
            m_client = m_root.AddComponent<ClientCamera>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_root);
        }

        [Test]
        public void FreshCameraUsesBrowserDesktopProjectionDefaults()
        {
            InvokeAwake(m_client);
            Assert.That(m_camera.fieldOfView, Is.EqualTo(80f));
            Assert.That(m_camera.nearClipPlane, Is.EqualTo(0.005f));
            Assert.That(m_camera.farClipPlane, Is.EqualTo(10000f));
        }

        [Test]
        public void StereoDepthCoefficientsUpdateCameraClipFields()
        {
            m_client.setCameraProjMatrix(AsymmetricProjection);
            Assert.That(m_camera.nearClipPlane, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(m_camera.farClipPlane, Is.EqualTo(9f).Within(0.000001f));
        }

        [Test]
        public void MiddleHalfCropPreservesAsymmetricEyeProjection()
        {
            m_client.setCameraProjMatrix(AsymmetricProjection);
            Matrix4x4 projection = m_camera.projectionMatrix;
            Vector4 clip = projection * new Vector4(0.5f, 0.25f, -3f, 1f);

            // The center-half UV crop doubles horizontal NDC, restoring the
            // browser eye's x scale AND its off-axis offset.
            Assert.That(2f * clip.x / clip.w, Is.EqualTo(0.1f).Within(0.000001f));
            Assert.That(clip.y / clip.w, Is.EqualTo(0.35f).Within(0.000001f));
            Assert.That(projection[2, 2], Is.EqualTo(-1.25f));
            Assert.That(projection[2, 3], Is.EqualTo(-2.25f));
            Assert.That(projection[3, 2], Is.EqualTo(-1f));
        }

        [Test]
        public void ReturningToMonoRestoresAutomaticProjectionAndBrowserClips()
        {
            m_client.setCameraProjMatrix(AsymmetricProjection);
            m_client.setCameraParams();

            Assert.That(m_camera.nearClipPlane, Is.EqualTo(0.005f));
            Assert.That(m_camera.farClipPlane, Is.EqualTo(10000f));
            Matrix4x4 expected = Matrix4x4.Perspective(80f, m_camera.aspect, 0.005f, 10000f);
            AssertProjectionEqual(m_camera.projectionMatrix, expected);

            // An automatic projection must respond to a subsequent aspect change;
            // writing a replacement custom matrix once would still leave it stuck.
            m_camera.aspect = 1.25f;
            expected = Matrix4x4.Perspective(80f, 1.25f, 0.005f, 10000f);
            AssertProjectionEqual(m_camera.projectionMatrix, expected);
        }

        private static IEnumerable InvalidProjections()
        {
            yield return new TestCaseData((object)null).SetName("NullProjectionLeavesCameraUnchanged");
            yield return new TestCaseData(new float[6]).SetName("ShortProjectionLeavesCameraUnchanged");
            foreach (int index in new[] { 0, 1, 2, 3, 4, 5, 6 })
            {
                float[] invalid = AsymmetricProjection;
                invalid[index] = float.NaN;
                yield return new TestCaseData(invalid).SetName($"NonfiniteProjectionCoefficient{index}LeavesCameraUnchanged");
            }
            yield return new TestCaseData(new[] { 0f, 0.3f, 1.8f, -0.2f, -1.25f, -2.25f, -1f })
                .SetName("SingularHorizontalProjectionLeavesCameraUnchanged");
            yield return new TestCaseData(new[] { 2.4f, 0.3f, -1f, -0.2f, -1.25f, -2.25f, -1f })
                .SetName("NegativeVerticalProjectionLeavesCameraUnchanged");
            yield return new TestCaseData(new[] { 2.4f, 0.3f, 1.8f, -0.2f, -1.25f, -2.25f, 0f })
                .SetName("NonPerspectiveProjectionLeavesCameraUnchanged");
            yield return new TestCaseData(new[] { 2.4f, 0.3f, 1.8f, -0.2f, -1f, -2.25f, -1f })
                .SetName("InfiniteFarProjectionLeavesCameraUnchanged");
            yield return new TestCaseData(new[] { 2.4f, 0.3f, 1.8f, -0.2f, -1.25f, 0f, -1f })
                .SetName("ZeroNearProjectionLeavesCameraUnchanged");
            yield return new TestCaseData(new[] { 2.4f, 0.3f, 1.8f, -0.2f, -0.5f, -2.25f, -1f })
                .SetName("UnorderedClipProjectionLeavesCameraUnchanged");
            yield return new TestCaseData(new[] { 2.4f, 0.3f, 1.8f, -0.2f, -1.0000001f, -float.MaxValue, -1f })
                .SetName("UnrepresentableFarProjectionLeavesCameraUnchanged");
        }

        [TestCaseSource(nameof(InvalidProjections))]
        public void InvalidProjectionDoesNotMutateCamera(float[] projection)
        {
            m_client.setCameraProjMatrix(AsymmetricProjection);
            Matrix4x4 before = m_camera.projectionMatrix;
            float nearBefore = m_camera.nearClipPlane;
            float farBefore = m_camera.farClipPlane;

            Assert.DoesNotThrow(() => m_client.setCameraProjMatrix(projection));
            Assert.That(m_camera.projectionMatrix, Is.EqualTo(before));
            Assert.That(m_camera.nearClipPlane, Is.EqualTo(nearBefore));
            Assert.That(m_camera.farClipPlane, Is.EqualTo(farBefore));
        }

        [Test]
        public void StereoToMonoResetsBothEyesAndSharedTargetState()
        {
            CameraStream stream = CreateStream();
            var left = GetEye(stream, "m_clientCameraLeft");
            var right = GetEye(stream, "m_clientCameraRight");
            var target = new RenderTexture(64, 32, 24);
            try
            {
                typeof(CameraStream).GetField("m_renderTexture", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(stream, target);
                left.GetComponent<Camera>().targetTexture = target;
                stream.UpdateStateWithStatus(new ClientStatus
                {
                    hasDualCameras = true, ipd = 0.064f,
                    leftProj = AsymmetricProjection, rightProj = AsymmetricProjection
                });
                Assert.That(stream.hasDualCameras, Is.True);
                Assert.That(left.transform.localPosition.x, Is.EqualTo(-0.032f));
                Assert.That(right.transform.localPosition.x, Is.EqualTo(0.032f));

                stream.UpdateStateWithStatus(new ClientStatus { hasDualCameras = false });
                Assert.That(stream.hasDualCameras, Is.False);
                Assert.That(left.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(right.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(left.IsDualCamera || right.IsDualCamera, Is.False);
                Assert.That(left.GetComponent<Camera>().rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                if (GraphicsSettings.defaultRenderPipeline != null)
                    Assert.That(right.GetComponent<Camera>().targetTexture, Is.Null);
                foreach (var eye in new[] { left, right })
                {
                    var camera = eye.GetComponent<Camera>();
                    AssertProjectionEqual(camera.projectionMatrix,
                        Matrix4x4.Perspective(80f, camera.aspect, 0.005f, 10000f));
                }
            }
            finally
            {
                left.GetComponent<Camera>().targetTexture = null;
                right.GetComponent<Camera>().targetTexture = null;
                Object.DestroyImmediate(target);
            }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-0.01f)]
        public void InvalidStereoIpdLeavesMonoStateUnchanged(float ipd)
        {
            AssertInvalidStereoLeavesMonoUnchanged(ipd, AsymmetricProjection);
        }

        [Test]
        public void InvalidRightProjectionDoesNotPartiallyEnableStereo()
        {
            AssertInvalidStereoLeavesMonoUnchanged(0.064f, new float[6]);
        }

        private void AssertInvalidStereoLeavesMonoUnchanged(float ipd, float[] rightProjection)
        {
            CameraStream stream = CreateStream();
            var left = GetEye(stream, "m_clientCameraLeft");
            var right = GetEye(stream, "m_clientCameraRight");
            stream.UpdateStateWithStatus(new ClientStatus { hasDualCameras = false });
            Matrix4x4 leftBefore = left.GetComponent<Camera>().projectionMatrix;
            Matrix4x4 rightBefore = right.GetComponent<Camera>().projectionMatrix;
            stream.UpdateStateWithStatus(new ClientStatus
            {
                hasDualCameras = true, ipd = ipd,
                leftProj = AsymmetricProjection, rightProj = rightProjection
            });

            Assert.That(stream.hasDualCameras, Is.False, "Reject malformed stereo data before enabling or moving either eye.");
            Assert.That(left.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(right.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(left.GetComponent<Camera>().projectionMatrix, Is.EqualTo(leftBefore));
            Assert.That(right.GetComponent<Camera>().projectionMatrix, Is.EqualTo(rightBefore));
        }

        private CameraStream CreateStream()
        {
            var stream = m_root.AddComponent<CameraStream>();
            InvokeAwake(stream);
            InvokeAwake(GetEye(stream, "m_clientCameraLeft"));
            InvokeAwake(GetEye(stream, "m_clientCameraRight"));
            return stream;
        }

        private static ClientCamera GetEye(CameraStream stream, string field)
        {
            return (ClientCamera)typeof(CameraStream).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(stream);
        }

        private static void InvokeAwake(MonoBehaviour component)
        {
            component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(component, null);
        }

        private static void AssertProjectionEqual(Matrix4x4 actual, Matrix4x4 expected)
        {
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    Assert.That(actual[row, column], Is.EqualTo(expected[row, column]).Within(0.000001f),
                        $"Projection coefficient ({row}, {column}) must match the active mono camera settings.");
        }
    }

    public class RenderFusionHeartbeatTests
    {
        [Test]
        public void HealthResponseResetsMissedHeartbeats()
        {
            var root = new GameObject("render-fusion-heartbeat-test");
            try
            {
                var renderFusion = root.AddComponent<RenderFusion>();
                const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type peerType = typeof(RenderFusion).Assembly.GetType("ArenaUnity.RenderFusion.PeerConnection");
                object peer = FormatterServices.GetUninitializedObject(peerType);
                peerType.GetField("missedHeartbeats", instanceFlags).SetValue(peer, 3);

                var peers = (IDictionary)typeof(RenderFusion).GetField("clientPeerDict", instanceFlags).GetValue(renderFusion);
                peers.Add("client-1", peer);

                typeof(RenderFusion).GetMethod("OnClientHealthCheck", instanceFlags)
                    .Invoke(renderFusion, new object[] { null, "client-1" });

                Assert.That(peerType.GetField("missedHeartbeats", instanceFlags).GetValue(peer), Is.EqualTo(0));
                Assert.That(peers.Contains("client-1"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase("OnNegotiationNeeded")]
        [TestCase("CreateAndSendAnswerCoroutine")]
        [TestCase("SetRemoteDescriptionCoroutine")]
        public void SignalingCoroutinesIgnoreAlreadyDisposedPeers(string methodName)
        {
            Type peerType = typeof(RenderFusion).Assembly.GetType("ArenaUnity.RenderFusion.PeerConnection");
            object peer = FormatterServices.GetUninitializedObject(peerType);
            MethodInfo method = peerType.GetMethod(methodName);
            ParameterInfo[] parameters = method.GetParameters();
            var arguments = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                arguments[i] = Activator.CreateInstance(parameters[i].ParameterType);

            var coroutine = (IEnumerator)method.Invoke(peer, arguments);
            Assert.That(coroutine.MoveNext(), Is.False, "Late signaling callbacks must not access the disposed native peer.");
        }
    }

    public class FrameIdShaderTests
    {
        private const int Width = 100;
        private const int Height = 32;
        private const int FrameIdBitCount = 32;

        [Test]
        public void EncodesFrameIdWithDefinedModuloShiftForNonAlignedWidth()
        {
            Shader shader = Shader.Find("Hidden/RGBDepth");
            Assert.That(shader, Is.Not.Null);

            var material = new Material(shader);
            var source = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            var destination = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                source.Create();
                destination.Create();

                RenderTexture.active = source;
                GL.Clear(true, true, Color.black);

                const int frameId = 0x40000021; // bits 0, 5 and 30
                material.SetInteger("_FrameID", frameId);
                material.SetInteger("_HasRightEyeTex", 0);
                material.SetTexture("_CameraDepthTexture", Texture2D.whiteTexture);

                Graphics.Blit(source, destination, material);

                RenderTexture.active = destination;
                readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                readback.Apply();

                // Unity ReadPixels uses bottom-left image coordinates on every backend.
                // ARENA's decoder samples texture UV y=1, so inspect the VIDEO TOP,
                // not whichever edge SV_Position happens to start at on this GPU.
                foreach (int row in new[] { Height - 1, Height - 8 })
                {
                    // The wire format uses the absolute output x-coordinate modulo 32.
                    // The legacy strict lower bound leaves the first pixel untouched.
                    for (int x = Width - FrameIdBitCount + 1; x < Width; x++)
                    {
                        int bit = x & (FrameIdBitCount - 1);
                        bool expected = (frameId & (1 << bit)) != 0;
                        Color pixel = readback.GetPixel(x, row);
                        Assert.That(pixel.g > 0.5f, Is.EqualTo(expected),
                            $"Video-top frame ID green channel differs at ({x}, {row}) on {SystemInfo.graphicsDeviceType}.");
                        Assert.That(pixel.b > 0.5f, Is.EqualTo(expected),
                            $"Video-top frame ID blue channel differs at ({x}, {row}) on {SystemInfo.graphicsDeviceType}.");
                    }
                }

                bool bottomContainsFrameId = true;
                for (int x = Width - FrameIdBitCount + 1; x < Width; x++)
                {
                    bool expected = (frameId & (1 << (x & (FrameIdBitCount - 1)))) != 0;
                    Color pixel = readback.GetPixel(x, 0);
                    if ((pixel.g > 0.5f) != expected || (pixel.b > 0.5f) != expected)
                    {
                        bottomContainsFrameId = false;
                        break;
                    }
                }
                Assert.That(bottomContainsFrameId, Is.False, "Frame IDs belong at the video top, never at its bottom edge.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
                source.Release();
                destination.Release();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(destination);
                Object.DestroyImmediate(material);
            }
        }
    }
}
