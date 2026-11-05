// Every clip and setting a unit model's animation needs, filled in the Inspector (Create > Blindside > Unit Animation Profile).
// UnitAnimator plays it through the Playables API — no Animator Controller to author:
//   lower body / full body : idle, or the walk while moving (one forward walk is enough; with more directions — up to 8 —
//                            the legs blend the clips around the direction they move in while the body keeps facing the aim)
//   upper body (mask)      : aim while selected / in combat (else the move-upper-body clip while moving); fire, reload,
//                            block and upper-body ability moves play over it
//   full body override     : hit, dodge, death and full-body ability moves
// Moving while aiming: the upper body keeps to the unit's facing (where it looks/aims) and the lower body walks the way it
// moves. Without directional walk clips this is done by turning the hips toward the move and twisting the spine back to the aim.
// Only `idle` is required; any empty slot is simply skipped.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Rendering
{
    [CreateAssetMenu(fileName = "UnitAnimationProfile", menuName = "Blindside/Unit Animation Profile")]
    public class UnitAnimationProfile : ScriptableObject
    {
        [Serializable]
        public class AbilityClip
        {
            [Tooltip("The ability's key: hammer, launcher, thermite, stim, shield, shock, gas, ghost, mirror, cam (drone for the drone).")]
            public string abilityKey;
            public AnimationClip clip;
            [Tooltip("On: the whole body plays it (a swing, a throw). Off: the upper body only, the legs keep their idle/walk.")]
            public bool fullBody = true;
            [Min(.1f)] public float speed = 1f;
        }

        public static readonly string[] AbilityKeys = { "hammer", "launcher", "thermite", "stim", "shield", "shock", "gas", "ghost", "mirror", "cam", "drone" };

        [Header("Base")]
        [Tooltip("Standing, not in combat (full body). Required.")]
        public AnimationClip idle;
        [Tooltip("Aiming the weapon (upper body): while moving, and while standing in combat.")]
        public AnimationClip aim;

        [Tooltip("Aim while this unit is selected by the player.")]
        public bool aimWhenSelected = true;

        [Header("Moving")]
        [Tooltip("Upper body while moving and not aiming (e.g. a rifle run). Empty: the aim pose.")]
        public AnimationClip moveUpperBody;
        [Tooltip("Legs: walking forward. Played at walkClipSpeed metres per second, sped up / slowed down to the unit's real speed.")]
        public AnimationClip walkForward;
        [Tooltip("Optional directions. With any of them the body keeps facing the aim and the legs blend the two clips around " +
                 "the move direction (up to 8). With none, the hips turn to the move and the spine twists back to the aim.")]
        public AnimationClip walkForwardLeft;
        public AnimationClip walkForwardRight;
        public AnimationClip strafeLeft;
        public AnimationClip strafeRight;
        public AnimationClip walkBackward;
        public AnimationClip walkBackwardLeft;
        public AnimationClip walkBackwardRight;
        [Tooltip("How fast the walk clips move the character, m/s (at the model's in-game scale).")]
        [Min(.05f)] public float walkClipSpeed = 1.2f;

        [Header("Actions")]
        [Tooltip("One shot per round fired (upper body).")]
        public AnimationClip fire;
        [Tooltip("After a volley (upper body). Off if empty.")]
        public AnimationClip reload;
        [Tooltip("A damaging hit landed.")]
        public AnimationClip hit;
        [Tooltip("Hit plays on the upper body only (off: whole body).")]
        public bool hitUpperBodyOnly = true;
        [Tooltip("A round missed this unit (whole body).")]
        public AnimationClip dodge;
        [Tooltip("A round stopped by the shield (upper body). Empty: the dodge.")]
        public AnimationClip block;

        public enum Side { Right, Left }

        [Header("Action speeds")]
        [Range(.25f, 4f)] public float fireSpeed = 1f;
        [Range(.25f, 4f)] public float hitSpeed = 1f;
        [Range(.25f, 4f)] public float dodgeSpeed = 1f;
        [Range(.25f, 4f)] public float blockSpeed = 1f;
        [Tooltip("Seconds into the dodge clip it starts from (skips a slow wind-up).")]
        [Min(0f)] public float dodgeStartTime = 0f;
        [Tooltip("Seconds into the dodge clip where the body is furthest out of the line of fire — the missed round flies past then.")]
        [Min(0f)] public float dodgePeakTime = .5f;
        [Tooltip("Which way (the character's own right / left) the dodge takes the body; the missed round passes on the other side.")]
        public Side dodgeSide = Side.Right;

        /// <summary>Real seconds from the dodge starting to its peak.</summary>
        public float DodgeLead => Mathf.Max(0f, (dodgePeakTime - dodgeStartTime) / Mathf.Max(.01f, dodgeSpeed));
        [Tooltip("Dying (whole body; holds its last frame).")]
        public AnimationClip death;
        public List<AbilityClip> abilities = new List<AbilityClip>();

        [Header("Upper / lower body split")]
        [Tooltip("Optional. Empty: built automatically (Humanoid: head, arms, spine; Generic: everything under upperBodyRootBone).")]
        public AvatarMask upperBodyMask;
        [Tooltip("Generic rigs: the first bone of the upper body (e.g. Spine). Humanoid rigs ignore it.")]
        public string upperBodyRootBone = "Spine";
        [Tooltip("Generic rigs: the spine bones that twist the upper body toward the aim, bottom to top. Humanoid rigs use Spine/Chest/UpperChest.")]
        public string[] spineBones = { "Spine", "Spine1", "Spine2" };
        [Tooltip("How far the spine may twist from the legs toward the aim, degrees.")]
        [Range(0f, 120f)] public float maxSpineTwist = 90f;
        [Tooltip("Aim more than this behind the move direction: walk backward (with a walkBackward clip) instead of twisting.")]
        [Range(60f, 180f)] public float backpedalAngle = 115f;

        [Header("Model")]
        [Tooltip("The model is scaled so its height is this many metres (the game's figures are about 1 m).")]
        [Min(.1f)] public float modelHeight = 1f;
        [Tooltip("Name of the transform at the gun's muzzle in the model (tracers start there). Empty: the right hand.")]
        public string muzzleTransform = "Muzzle";

        [Header("Gun grip (Blindside > Animation > Calibrate Operator Grips)")]
        [Tooltip("Baked from the aim clip on this rig: how the right hand holds a rifle. Off: worked out at runtime.")]
        public bool gripCalibrated;
        [Tooltip("Right-hand space: the barrel direction (toward the left hand).")]
        public Vector3 gripBarrel;
        [Tooltip("Right-hand space: world up in the aim pose.")]
        public Vector3 gripUp;
        [Tooltip("Right-hand space: the palm point the grip sits in.")]
        public Vector3 gripPalm;
        [Tooltip("The left hand's rotation in the barrel's frame (forward = barrel, up = up) in the aim pose.")]
        public Quaternion gripLeftRot = Quaternion.identity;
        [Tooltip("Left-hand space: the palm point that goes on the foregrip.")]
        public Vector3 gripLeftPalm;

        [Header("Aiming down the sights (metres, for a 1.7 m figure)")]
        [Tooltip("Where the butt sits, from the right shoulder joint: x right (− = toward the chest), y up, z forward.")]
        public Vector3 adsShoulderPocket = new Vector3(.04f, -.01f, .05f);
        [Tooltip("The eye in front of / above the head bone.")]
        public Vector3 adsEyeOffset = new Vector3(.03f, .07f, .09f);
        [Tooltip("The head drops until the eye is this far above the top of the stock (cheek weld).")]
        public float adsEyeDrop = .035f;
        [Tooltip("At most this many degrees of head drop toward the stock.")]
        [Range(0f, 45f)] public float adsHeadPitch = 12f;
        [Tooltip("The head leans this many degrees toward the stock.")]
        [Range(0f, 30f)] public float adsHeadRoll = 7f;
        [Tooltip("Aiming, the shoulder line ends up this many degrees from square-on to the target, left shoulder forward (whatever the clip does; the face stays on the target).")]
        [Range(0f, 80f)] public float adsBlade = 30f;
        [Tooltip("Aiming, the chest leans this many degrees forward over the hips (an aggressive firing stance).")]
        [Range(0f, 30f)] public float adsLean = 0f;

        [Header("Walking feet (after the clip, while moving)")]
        [Tooltip("How much of each foot's sideways distance from the hips' centre is taken away while walking (0 = as the clip, 1 = on one line).")]
        [Range(0f, 1f)] public float walkFeetNarrow = .6f;
        [Tooltip("The toes point this many degrees outward from the body's forward while walking (the leg turns as a whole, knee with it).")]
        [Range(-10f, 40f)] public float walkToeOut = 6f;

        [Header("Timing")]
        [Tooltip("Seconds to fade from one pose to the next.")]
        [Range(0f, .5f)] public float crossFade = .15f;
        [Tooltip("Keep aiming this long after firing / being hit / an encounter.")]
        [Min(0f)] public float aimHoldSeconds = 4f;
        [Tooltip("Play the reload clip after every volley.")]
        public bool reloadAfterVolley = true;
        [Tooltip("After the death clip ends: keep the body lying there (off: fade out like before).")]
        public bool keepBodyAfterDeath = false;

        public AbilityClip FindAbility(string key)
        {
            if (string.IsNullOrEmpty(key) || abilities == null) return null;
            foreach (var a in abilities) if (a != null && a.clip != null && a.abilityKey == key) return a;
            return null;
        }

        /// <summary>Leg clips by direction relative to the aim, degrees clockwise from forward (null where empty).</summary>
        public (float angle, AnimationClip clip)[] Directions => new[]
        {
            (0f, walkForward), (45f, walkForwardRight), (90f, strafeRight), (135f, walkBackwardRight),
            (180f, walkBackward), (225f, walkBackwardLeft), (270f, strafeLeft), (315f, walkForwardLeft),
        };

        /// <summary>The leg clips cover every direction with no gap wider than 90° (e.g. the four, or all eight): the body then
        /// keeps facing the aim and the legs blend. Otherwise the spine twists.</summary>
        public bool HasDirectionalWalk
        {
            get
            {
                if (walkForward == null) return false;
                float last = 0f, maxGap = 0f;
                foreach (var d in Directions)
                {
                    if (d.clip == null || d.angle == 0f) continue;
                    maxGap = Mathf.Max(maxGap, d.angle - last); last = d.angle;
                }
                maxGap = Mathf.Max(maxGap, 360f - last);
                return maxGap <= 90f;
            }
        }
    }
}
