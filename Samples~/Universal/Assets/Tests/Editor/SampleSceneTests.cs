using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArenaUnity.RenderFusion.Tests
{
    public class SampleSceneTests
    {
        // Optional command-line entry for a live browser smoke test. Normal use
        // still consists of opening MyScene in Unity and entering Play mode.
        public static void StartLiveScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MyScene.unity", OpenSceneMode.Single);
            // The unchanged ARENA MQTT client yields WaitForEndOfFrame, so this
            // live check needs a graphical Editor with its Game view available.
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }

        [Test]
        public void SampleSceneLoadsServerAndMainCamera()
        {
            const string scenePath = "Assets/Scenes/MyScene.unity";
            Scene previousActive = SceneManager.GetActiveScene();
            bool wasAlreadyLoaded = SceneManager.GetSceneByPath(scenePath).isLoaded;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                Assert.That(scene.isLoaded, Is.True, $"Could not load {scenePath}.");
                GameObject[] roots = scene.GetRootGameObjects();
                Assert.That(roots.Select(root => root.name), Is.EquivalentTo(new[]
                {
                    "ARENARenderFusionURP", "Main Camera", "Directional Light", "Global Volume"
                }), "The sample must deserialize all four scene roots, including its server prefab.");

                RenderFusion[] servers = roots.SelectMany(root => root.GetComponentsInChildren<RenderFusion>(true)).ToArray();
                Assert.That(servers, Has.Length.EqualTo(1), "The sample must contain one bound RenderFusion component.");
                GameObject server = servers[0].gameObject;
                Component[] components = server.GetComponents<Component>();
                string componentTypes = string.Join(", ", components.Select(component =>
                    component == null ? "<missing script>" : component.GetType().FullName));
                Assert.That(components.Any(component => component != null &&
                    component.GetType().FullName == "ArenaUnity.ArenaClientScene"), Is.True,
                    $"The server prefab must retain its bound ARENA scene component. Components: {componentTypes}.");
                Assert.That(PrefabUtility.GetPrefabInstanceStatus(server), Is.EqualTo(PrefabInstanceStatus.Connected));

                Camera[] cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
                Assert.That(cameras.Any(camera => camera.CompareTag("MainCamera") && camera.enabled &&
                    camera.gameObject.activeInHierarchy), Is.True, "The sample must have an active main camera.");
            }
            finally
            {
                if (!wasAlreadyLoaded && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
            }
        }
    }
}
