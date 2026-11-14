using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ArenaUnity.RenderFusion.Misc
{
    public class PlayerController : MonoBehaviour
    {
        float speed = 5.0f;
        // Start is called before the first frame update
        void Start()
        {
            speed = 5.0f;
        }

        // Update is called once per frame
        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            bool forward = keyboard.wKey.isPressed;
            bool back = keyboard.sKey.isPressed;
            bool left = keyboard.aKey.isPressed;
            bool right = keyboard.dKey.isPressed;
#else
            bool forward = Input.GetKey(KeyCode.W);
            bool back = Input.GetKey(KeyCode.S);
            bool left = Input.GetKey(KeyCode.A);
            bool right = Input.GetKey(KeyCode.D);
#endif
            if (forward)
            {
                transform.Translate(Vector3.forward * Time.deltaTime * speed);
            }
            if (back)
            {
                transform.Translate(-1 * Vector3.forward * Time.deltaTime * speed);
            }
            if (left)
            {
                transform.Rotate(0, -1, 0);
            }
            if (right)
            {
                transform.Rotate(0, 1, 0);
            }
        }
    }
}
