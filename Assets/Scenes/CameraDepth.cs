using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArenaUnity.CloudRendering
{
    [RequireComponent(typeof(Camera))]
    public class CameraDepth : MonoBehaviour
    {
        private Camera cam = null;
        private Material mat = null;
        public RenderTexture renderTarget = null;

        // Start is called before the first frame update
        void Start()
        {
            cam = GetComponent<Camera>();
            cam.depthTextureMode = DepthTextureMode.Depth;

            mat = new Material(Shader.Find("Hidden/DepthShader"));
        }

        // Update is called once per frame
        void Update()
        {

        }

        // private void OnPreRender()
        // {
        //     Shader.SetGlobalMatrix(Shader.PropertyToID("UNITY_MATRIX_IV"), Cam.cameraToWorldMatrix);
        // }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (renderTarget) {
                Graphics.Blit(source, renderTarget, mat);
            }
            else {
                Graphics.Blit(source, destination, mat);
            }
        }
    }
}
