using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArenaUnity.HybridRendering.Misc
{
    public class ScreenShot : MonoBehaviour
    {
        public KeyCode screenShotButton = KeyCode.S;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(screenShotButton))
            {
                ScreenCapture.CaptureScreenshot("~/Desktop/screenshot.png");
                Debug.Log("A screenshot was taken!");
            }
        }
    }
}
