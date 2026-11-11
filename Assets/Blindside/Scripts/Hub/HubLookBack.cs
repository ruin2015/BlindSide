// The armoury's gadget pose (HubArmoryStage): the operator turns to look back and down at a camera low behind it, the turn shared down
// the body the way a person does it — the hips a little, the spine and chest more, the neck and head the most — then the head tips
// down onto the camera and the chest leans a touch toward it. Applied after the animation (and the gun rig) each frame, on the idle;
// `Weight` eases 0 → 1 (fast, then settling: HubArmoryStage.EaseOut) over TurnTime, unscaled, and back.
// Limits keep it human: the chest turns at most ChestMax off the hips, the head at most HeadMax off the chest, the head tips at most
// PitchMax. Nothing is allocated per frame.
using UnityEngine;

namespace Blindside.Hub
{
    [DefaultExecutionOrder(1000)]   // after UnitAnimator's LateUpdate (the pose and the gun)
    public class HubLookBack : MonoBehaviour
    {
        public const float TurnTime = .7f, ChestMax = 55f, HeadMax = 75f, PitchMax = 32f, Lean = 7f;
        // the yaw each bone takes, hips → head (sums to 1)
        // (the hips keep the legs' line: the feet stay planted, the figure's facing gives the rest)
        // (the torso about a third — its back stays toward the camera; the neck and head the rest, ~75° at the usual turn)
        static readonly float[] YawShare = { 0f, .1f, .12f, .1f, .28f, .4f };
        static readonly HumanBodyBones[] Chain =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
        };

        readonly Transform[] bones = new Transform[6];
        Transform target;     // what to look at (the camera)
        Transform body;       // the model's root: its forward is where the figure faces
        float weight, goal, from, clock = 99f;

        public float Weight => weight;

        /// <summary>Once turned (and the camera settled: HoldAfter), the figure is held still in the pose — the animation stops
        /// (`Hold(true)`: UnitAnimator.SetFrozen) and this stops posing; turning back releases it first.</summary>
        public System.Action<bool> Hold;
        public const float HoldAfter = 1f;
        bool held;
        public bool Held => held;

        /// <summary>Binds to `animator`'s humanoid bones; `look` is the camera it turns to.</summary>
        public void Bind(Animator animator, Transform look)
        {
            target = look;
            body = animator != null ? animator.transform : null;
            for (int i = 0; i < Chain.Length; i++) bones[i] = animator != null && animator.isHuman ? animator.GetBoneTransform(Chain[i]) : null;
        }

        /// <summary>Turns to the camera (true) or back to the plain idle, eased over TurnTime (instant: at once).</summary>
        public void Turn(bool on, bool instant = false)
        {
            if (held) { held = false; Hold?.Invoke(false); }   // moving again: the animation writes the bones again
            from = weight; goal = on ? 1f : 0f;
            clock = instant ? 99f : 0f;
            if (instant) weight = goal;
        }

        void OnDisable() { if (held) { held = false; Hold?.Invoke(false); } }

        void LateUpdate()
        {
            if (held) return;   // the pose stays as it was left
            if (clock < HoldAfter + 1f)
            {
                clock += Time.unscaledDeltaTime;
                weight = Mathf.Lerp(from, goal, HubArmoryStage.EaseOut(Mathf.Clamp01(clock / TurnTime)));
            }
            if (weight > .001f) Apply(weight);
            // turned all the way and the camera settled: stop here, still
            if (goal >= 1f && weight >= .999f && clock >= HoldAfter && Hold != null) { held = true; Hold(true); }
        }

        /// <summary>Poses the turn at `w` (0–1) over the current pose. Called each frame after the animation (and once by the previews,
        /// on a sampled pose).</summary>
        public void Apply(float w)
        {
            var head = bones[5];
            if (target == null || head == null || body == null) return;
            // the yaw to the camera, measured from where the figure faces (flat)
            var fwd = body.forward;
            fwd.y = 0f;
            var toCam = target.position - head.position;
            var flat = new Vector3(toCam.x, 0f, toCam.z);
            if (fwd.sqrMagnitude < 1e-6f || flat.sqrMagnitude < 1e-6f) return;
            float yaw = Vector3.SignedAngle(fwd.normalized, flat.normalized, Vector3.up);
            // human limits: whatever the chest and the head can't take, they don't
            yaw = Mathf.Clamp(yaw, -(ChestMax + HeadMax + 20f), ChestMax + HeadMax + 20f);
            float side = Mathf.Sign(yaw);
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null) continue;
                b.rotation = Quaternion.AngleAxis(yaw * YawShare[i] * w, Vector3.up) * b.rotation;
            }
            // the chest leans a touch toward the shoulder it looks over (a person drops that shoulder to see behind)
            var chest = bones[3] != null ? bones[3] : bones[2];
            if (chest != null)
            {
                var axis = Quaternion.AngleAxis(yaw * w, Vector3.up) * fwd.normalized;
                chest.rotation = Quaternion.AngleAxis(-side * Lean * w, axis) * chest.rotation;
            }
            // the head (and a little of the neck) tips down onto the camera
            var look = target.position - head.position;
            float pitch = Mathf.Clamp(Vector3.SignedAngle(new Vector3(look.x, 0f, look.z).normalized, look.normalized,
                                                          Vector3.Cross(Vector3.up, new Vector3(look.x, 0f, look.z)).normalized), -PitchMax, PitchMax);
            var tilt = Vector3.Cross(Vector3.up, new Vector3(look.x, 0f, look.z)).normalized;
            if (bones[4] != null) bones[4].rotation = Quaternion.AngleAxis(pitch * .35f * w, tilt) * bones[4].rotation;
            head.rotation = Quaternion.AngleAxis(pitch * .65f * w, tilt) * head.rotation;
        }
    }
}
