// A humanoid's hands as they are in the rest pose, read once (UnitView.CalibrateGrip, before the model is ever posed):
// each finger joint's rest rotation and the axis it curls about (toward the palm), and the forearm's direction in each
// hand's space (a straight wrist). GunRig uses it to close the fingers round what they hold — a pistol grip, a vertical
// grip, a handguard — instead of the clip's fingers, and to send the elbow where the wrist stays straight.
// Shared read-only by a figure and its battle-stage copy (same rig, same bone axes).
using UnityEngine;

namespace Blindside.Rendering
{
    public sealed class HandRest
    {
        // [side * 15 + finger * 3 + joint]: side 0 left / 1 right; finger 0 thumb, 1 index, 2 middle, 3 ring, 4 little;
        // joint 0 proximal, 1 intermediate, 2 distal (HumanBodyBones' own order)
        public readonly Quaternion[] rest = new Quaternion[30];
        public readonly Vector3[] axis = new Vector3[30];
        public readonly bool[] has = new bool[30];
        public Vector3 leftForearm, rightForearm;   // hand space: elbow → wrist with a straight wrist

        /// <summary>Reads the hands of `a`'s model, which must still be in its rest pose. Null without a humanoid hand.</summary>
        public static HandRest Read(Animator a)
        {
            if (a == null || a.avatar == null || !a.avatar.isHuman) return null;
            var r = new HandRest();
            for (int side = 0; side < 2; side++)
            {
                var hand = a.GetBoneTransform(side == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var lower = a.GetBoneTransform(side == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var middle = a.GetBoneTransform(side == 0 ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                var thumb = a.GetBoneTransform(side == 0 ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
                if (hand == null || lower == null) return null;
                var forearm = hand.InverseTransformDirection(hand.position - lower.position).normalized;
                if (side == 0) r.leftForearm = forearm; else r.rightForearm = forearm;
                if (middle == null || thumb == null) continue;
                // the palm faces the side the fingers close to: from the finger line and the thumb's side of the hand
                var f = (middle.position - hand.position).normalized;
                var t = thumb.position - hand.position; t -= f * Vector3.Dot(t, f);
                var palm = Vector3.Cross(f, t).normalized * (side == 0 ? -1f : 1f);
                int first = side == 0 ? (int)HumanBodyBones.LeftThumbProximal : (int)HumanBodyBones.RightThumbProximal;
                for (int i = 0; i < 15; i++)
                {
                    var bone = a.GetBoneTransform((HumanBodyBones)(first + i));
                    if (bone == null) continue;
                    int joint = i % 3;
                    // the bone's direction: to the next joint (the distal one: from the joint before it)
                    Vector3 dir;
                    var nextB = joint < 2 ? a.GetBoneTransform((HumanBodyBones)(first + i + 1)) : null;
                    var prevB = joint > 0 ? a.GetBoneTransform((HumanBodyBones)(first + i - 1)) : null;
                    if (nextB != null) dir = nextB.position - bone.position;
                    else if (prevB != null) dir = bone.position - prevB.position;
                    else continue;
                    var ax = Vector3.Cross(dir.normalized, palm);
                    if (ax.sqrMagnitude < 1e-6f) continue;
                    int k = side * 15 + i;
                    r.rest[k] = bone.localRotation;
                    r.axis[k] = bone.InverseTransformDirection(ax.normalized);
                    r.has[k] = true;
                }
            }
            return r;
        }

        // ---- grips: curl per finger and joint, degrees (thumb, index, middle, ring, little × proximal, intermediate, distal)
        /// <summary>Right hand on a pistol grip: three fingers wrapped, the index finger along the trigger guard.</summary>
        public static readonly float[] PistolGrip = { 22f, 28f, 20f,  28f, 32f, 14f,  72f, 85f, 45f,  76f, 85f, 45f,  80f, 82f, 45f };
        /// <summary>Left hand under a handguard: the fingers cupped round its side, the thumb along it.</summary>
        public static readonly float[] Handguard = { 10f, 14f, 10f,  38f, 42f, 22f,  42f, 46f, 25f,  46f, 46f, 25f,  50f, 46f, 25f };
        /// <summary>Left hand round a vertical grip: every finger wrapped.</summary>
        public static readonly float[] VerticalGrip = { 28f, 30f, 25f,  66f, 76f, 40f,  72f, 82f, 45f,  76f, 82f, 45f,  80f, 80f, 45f };
    }
}
