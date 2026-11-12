using UnityEngine;
using UnityEngine.InputSystem;

namespace Harness
{
    /// <summary>
    /// Stand-in "game" for `hx selftest --unity`. Written like ordinary game code: plain Input System actions,
    /// no harness references in its logic — the harness drives it with virtual devices and reads it with probes.
    /// </summary>
    public class SelfTestMover : MonoBehaviour
    {
        public float speed = 5f;
        public float CurrentSpeed { get; private set; }
        InputAction move;

        void OnEnable()
        {
            move = new InputAction("move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            move.Enable();
        }

        void OnDisable() => move?.Disable();

        void Update()
        {
            var v = move.ReadValue<Vector2>();
            var vel = new Vector3(v.x, 0, v.y) * speed;
            CurrentSpeed = vel.magnitude;
            transform.position += vel * Time.deltaTime;
        }
    }
}
