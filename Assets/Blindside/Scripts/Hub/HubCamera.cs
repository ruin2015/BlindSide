// The HQ's third-person camera: behind and above the player, orbiting with the mouse's movement (the cursor is locked while walking)
// or the right stick, zooming with the wheel; pulled in front of a wall that would hide the player. Holds still while a panel is open.
// No allocation per frame.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blindside.Hub
{
    public class HubCamera : MonoBehaviour
    {
        public Transform target;
        public float height = 1.5f;
        public float distance = 4.2f, minDistance = 2f, maxDistance = 7f;
        public float yaw = 0f, pitch = 18f, minPitch = -5f, maxPitch = 60f;
        public float mouseSpeed = .18f, stickSpeed = 140f, follow = 12f;
        [Tooltip("What blocks the view (walls). Everything but the player's layer by default.")]
        public LayerMask blockers = ~0;

        Vector3 focus;
        bool placed;

        void LateUpdate()
        {
            if (target == null) return;
            if (!HubDirector.InputBlocked)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    // the cursor is locked while walking (HubDirector): every mouse move turns the view
                    if (Cursor.lockState == CursorLockMode.Locked)
                    {
                        var d = mouse.delta.ReadValue();
                        yaw += d.x * mouseSpeed; pitch -= d.y * mouseSpeed;
                    }
                    float wheel = mouse.scroll.ReadValue().y;
                    if (wheel != 0f) distance = Mathf.Clamp(distance - Mathf.Sign(wheel) * .4f, minDistance, maxDistance);
                }
                var pad = Gamepad.current;
                if (pad != null)
                {
                    var s = pad.rightStick.ReadValue();
                    yaw += s.x * stickSpeed * Time.unscaledDeltaTime; pitch -= s.y * stickSpeed * .6f * Time.unscaledDeltaTime;
                }
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            Vector3 want = target.position + Vector3.up * height;
            focus = placed ? Vector3.Lerp(focus, want, 1f - Mathf.Exp(-follow * Time.unscaledDeltaTime)) : want;
            placed = true;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rot * Vector3.back;
            float dist = distance;
            if (Physics.SphereCast(focus, .25f, back, out var hit, distance, blockers, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target))
                dist = Mathf.Max(.6f, hit.distance - .1f);
            transform.SetPositionAndRotation(focus + back * dist, rot);
        }
    }
}
