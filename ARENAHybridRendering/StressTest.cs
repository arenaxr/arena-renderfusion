using UnityEngine;
using ArenaUnity;

namespace ArenaUnity.HybridRendering.StressTest
{
    public class StressTest : MonoBehaviour {
#pragma warning disable 0649
        [SerializeField, Tooltip("Number of objects to spawn")]
        private int N = 500000;
#pragma warning restore 0649

        public void Start()
        {
            for (int i = 0; i < N; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Sphere{i}";
                sphere.GetComponent<Renderer>().material.color = new Color(255, 255, 0);
                sphere.transform.position = new Vector3(
                                                Random.Range(-250.0f, 250.0f),
                                                Random.Range(-250.0f, 250.0f),
                                                Random.Range(-250.0f, 250.0f)
                                            );
                sphere.transform.rotation = ArenaUnity.ToUnityRotationQuat(Quaternion.Euler(
                                                0,
                                                Random.Range(0.0f, Mathf.PI),
                                                0
                                            ));
                sphere.transform.localScale = new Vector3(
                                                Random.Range(0.0f, 1.0f),
                                                Random.Range(0.0f, 1.0f),
                                                Random.Range(0.0f, 1.0f)
                                            );
            }
        }
    }
}
