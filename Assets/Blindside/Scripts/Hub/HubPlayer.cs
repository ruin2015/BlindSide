// The player at the HQ (FLARE, third person): WASD / the left stick moves relative to the camera, Shift runs; the figure turns to
// where it walks. A CharacterController keeps it on the floor and out of the walls. Frozen while a station's panel is open
// (HubDirector.InputBlocked). No allocation per frame.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blindside.Hub
{
    [RequireComponent(typeof(CharacterController))]
    public class HubPlayer : MonoBehaviour
    {
        [Tooltip("m/s: walking, and running (Shift held while moving).")]
        public float walkSpeed = 1.7f, runSpeed = 4.2f;
        public float turnSpeed = 720f, gravity = 20f;
        [Tooltip("The camera whose yaw the movement follows (empty: Camera.main).")]
        public Transform view;
        public HubFigure figure;

        CharacterController cc;
        float fall;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (figure == null) figure = GetComponentInChildren<HubFigure>();
            if (figure != null) { figure.walkSpeed = walkSpeed; figure.runSpeed = runSpeed; }   // the blend follows this player's speeds
        }

        void Update()
        {
            if (view == null && Camera.main != null) view = Camera.main.transform;
            Vector2 move = Vector2.zero;
            bool run = false;
            if (!HubDirector.InputBlocked)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                    run = kb.leftShiftKey.isPressed;
                }
                var pad = Gamepad.current;
                if (pad != null) { move += pad.leftStick.ReadValue(); run |= pad.leftStickButton.isPressed; }
            }
            if (move.sqrMagnitude > 1f) move.Normalize();

            Vector3 fwd = Vector3.forward, right = Vector3.right;
            if (view != null)
            {
                fwd = view.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) fwd = view.up;
                fwd.y = 0f; fwd.Normalize();
                right = new Vector3(fwd.z, 0f, -fwd.x);
            }
            Vector3 dir = fwd * move.y + right * move.x;
            float speed = (run ? runSpeed : walkSpeed) * dir.magnitude;
            if (dir.sqrMagnitude > 1e-4f)
            {
                var want = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
            }

            fall = cc.isGrounded ? -1f : fall - gravity * Time.deltaTime;
            Vector3 v = (dir.sqrMagnitude > 1e-4f ? dir.normalized * speed : Vector3.zero) + Vector3.up * fall;
            cc.Move(v * Time.deltaTime);
            if (figure != null) figure.Speed = speed;
        }
    }
}
