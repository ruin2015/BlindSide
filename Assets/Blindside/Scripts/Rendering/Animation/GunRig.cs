// The gun in a humanoid figure's hands, applied after the animation has posed the bones (UnitAnimator.LateUpdate):
//   - the right hand keeps the pistol grip (the gun is its child, placed by UnitView.SetWeapon);
//   - the left hand is pulled onto the foregrip / handguard (two-bone IK), in the hand rotation the aim clip uses;
//   - aiming, the gun is shouldered — the butt in the right shoulder pocket, the barrel along the aim — the right arm
//     is solved to the grip, the chest blades and the head drops onto the stock (a short recoil kick per round).
// No allocations per frame; runs on game time like the animator.
using UnityEngine;

namespace Blindside.Rendering
{
    public sealed class GunRig
    {
        /// <summary>Everything a figure needs to hold one gun: points in the gun's own (unscaled) space, the left hand's
        /// pose from the profile, and the figure's size.</summary>
        public struct Setup
        {
            public Vector3 foregripLocal, sightLocal, buttLocal, forwardLocal;   // sight: the stock's top at the rear
            public Quaternion leftRotInBarrel;
            public float leftRoll;                        // 0 = under the handguard … 90 = round a vertical grip
            public Vector3 leftPalm;
            public bool hasLeftPose;
            public Vector3 leftFinger, leftThumb;         // left-hand space, from the rest pose: toward the fingers / the thumb
            public bool hasHandAxes;
            public Vector3 shoulderPocket, eyeOffset;     // x right, y up, z forward (metres for a 1.7 m figure)
            public float eyeDrop, headPitch, headRoll, figure;   // figure: model height / 1.7 m
            public float blade, lean;                     // degrees the chest turns (left shoulder forward) / leans forward when aiming
            public Vector3 gripLocal;                     // the right hand's point (the left hand slides no further back)
            public HandRest hands;                        // rest-pose fingers / forearms (null: the clip's fingers, old elbows)
        }

        readonly Transform gun, rUpper, rLower, rHand, lUpper, lLower, lHand, head, chest, neck;
        readonly Transform[] fingers = new Transform[30];   // HandRest's order
        readonly Setup s;
        readonly float leftReach, rightForearmLen, leftForearmLen;
        float kick;

        GunRig(Transform gun, Animator a, Setup s)
        {
            this.gun = gun; this.s = s;
            rUpper = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rLower = a.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rHand = a.GetBoneTransform(HumanBodyBones.RightHand);
            lUpper = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lLower = a.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            lHand = a.GetBoneTransform(HumanBodyBones.LeftHand);
            head = a.GetBoneTransform(HumanBodyBones.Head);
            chest = a.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = a.GetBoneTransform(HumanBodyBones.Chest);
            neck = a.GetBoneTransform(HumanBodyBones.Neck);
            if (neck == null) neck = head;
            if (lUpper != null && lLower != null && lHand != null)
            {
                leftForearmLen = Vector3.Distance(lLower.position, lHand.position);
                leftReach = Vector3.Distance(lUpper.position, lLower.position) + leftForearmLen;
            }
            if (rLower != null && rHand != null) rightForearmLen = Vector3.Distance(rLower.position, rHand.position);
            for (int i = 0; i < 15; i++)
            {
                fingers[i] = a.GetBoneTransform((HumanBodyBones)((int)HumanBodyBones.LeftThumbProximal + i));
                fingers[15 + i] = a.GetBoneTransform((HumanBodyBones)((int)HumanBodyBones.RightThumbProximal + i));
            }
        }

        // The fingers of one hand (side 0 left, 1 right) closed round what it holds: curls blended a → b by t, over the
        // clip's fingers by `weight`.
        void Curl(int side, float[] a, float[] b, float t, float weight)
        {
            var h = s.hands;
            if (h == null || weight <= .001f) return;
            for (int i = 0; i < 15; i++)
            {
                int k = side * 15 + i;
                var bone = fingers[k];
                if (bone == null || !h.has[k]) continue;
                var want = h.rest[k] * Quaternion.AngleAxis(Mathf.Lerp(a[i], b[i], t), h.axis[k]);
                bone.localRotation = weight >= .999f ? want : Quaternion.Slerp(bone.localRotation, want, weight);
            }
        }

        // Where the elbow goes for the hand at (pos, rot) to sit on a straight wrist (the forearm along the hand), pulled
        // a little toward `fallback` (a plain elbow-down) so it never swings somewhere odd.
        static Vector3 StraightWristElbow(Vector3 pos, Quaternion rot, Vector3 forearmInHand, float forearmLen, Vector3 fallback)
        {
            var ideal = pos - (rot * forearmInHand) * forearmLen;
            return Vector3.Lerp(ideal, fallback, .3f);
        }

        /// <summary>A rig for `gun` (a child of the right hand) on a humanoid; null when either is missing.</summary>
        public static GunRig Bind(Animator a, Transform gun, Setup s)
        {
            if (a == null || gun == null || a.avatar == null || !a.avatar.isHuman) return null;
            var r = new GunRig(gun, a, s);
            if (r.rUpper == null || r.rLower == null || r.rHand == null || r.lUpper == null || r.lLower == null || r.lHand == null || r.head == null) return null;
            if (gun.parent != r.rHand) return null;
            return r;
        }

        /// <summary>One round's recoil.</summary>
        public void Kick() => kick = 1f;

        /// <summary>Hit: the gun knocks down and back off the shoulder for a moment (the hands stay on it).</summary>
        public void Jolt() => jolt = 1f;
        float jolt;

        /// <summary>0..1, set by the animator each frame: how far the head turns with the bladed chest instead of staying where the
        /// clip put it. 1 while a hit reaction plays — that clip twists the upper body away, and holding the head there turned the
        /// face off to the side while the chest was brought back to the aim.</summary>
        public float FaceFollow { get; set; }

        /// <summary>`ads` 0..1: raised to the eye; `left` 0..1: left hand on the foregrip; `aimYaw`: world yaw of the aim;
        /// `hold` 0..1: the right hand's fingers round the pistol grip (0 while the body falls).</summary>
        public void Solve(float ads, float left, float aimYaw, float dt, float hold = 1f)
        {
            if (gun == null) return;
            kick = Mathf.MoveTowards(kick, 0f, dt * 7f);
            jolt = Mathf.MoveTowards(jolt, 0f, dt * 3.5f);
            float f = s.figure;

            if (ads > .001f)
            {
                var fwd = Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                // bladed, leaning stance: the chest turns the left shoulder toward the target and leans over the hips; the
                // neck keeps the face where it was (the cheek weld below drops it onto the stock)
                if (chest != null)
                {
                    // `blade` is the shoulder line's angle to square-on, whatever the clip does (the aim clip already turns
                    // the body a long way): measure it, turn the chest the difference
                    var line = rUpper.position - lUpper.position; line.y = 0f;
                    float now = line.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(right, line, Vector3.up) : 0f;
                    float yawFix = Mathf.DeltaAngle(now, s.blade) * ads;
                    var turn = Quaternion.AngleAxis(s.lean * ads, right) * Quaternion.AngleAxis(yawFix, Vector3.up);
                    var neckRot = neck != null ? neck.rotation : Quaternion.identity;
                    chest.rotation = turn * chest.rotation;
                    if (neck != null && neck != chest) neck.rotation = FaceFollow > .001f ? Quaternion.Slerp(neckRot, neck.rotation, FaceFollow) : neckRot;
                }
                float gs = gun.lossyScale.x;
                var aimRot = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(s.forwardLocal, Vector3.up));
                // the butt in the shoulder pocket (just inside and in front of the right shoulder joint)
                var butt = rUpper.position + (right * s.shoulderPocket.x + Vector3.up * s.shoulderPocket.y + fwd * s.shoulderPocket.z) * f;
                float k = kick * kick;
                if (k > 0f)
                {
                    aimRot = Quaternion.AngleAxis(-5f * k, right) * aimRot;   // muzzle up
                    butt -= fwd * (.03f * f * k);                               // into the shoulder
                }
                float j = jolt * jolt * (3f - 2f * jolt);
                if (j > 0f)
                {
                    aimRot = Quaternion.AngleAxis(9f * j, right) * aimRot;    // muzzle knocked down
                    butt += (-fwd * .04f - Vector3.up * .03f) * (f * j);       // off the shoulder, back
                }
                var aimPos = butt - aimRot * (s.buttLocal * gs);

                // cheek weld: the head drops (and leans in) until the eye is just over the top of the stock
                if (neck != null)
                {
                    var stockTop = aimPos + aimRot * (s.sightLocal * gs);
                    var eye = head.position + (right * s.eyeOffset.x + Vector3.up * s.eyeOffset.y + fwd * s.eyeOffset.z) * f;
                    float drop = eye.y - (stockTop.y + s.eyeDrop * f);
                    float lever = Mathf.Max(.05f * f, Vector3.Distance(neck.position, eye));
                    float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(drop / lever, -1f, 1f)) * Mathf.Rad2Deg, 0f, s.headPitch);
                    neck.rotation = Quaternion.AngleAxis(pitch * ads, right) * Quaternion.AngleAxis(-s.headRoll * ads, fwd) * neck.rotation;
                }

                var pos = Vector3.Lerp(gun.position, aimPos, ads);
                var rot = Quaternion.Slerp(gun.rotation, aimRot, ads);
                // the hand that makes the gun land there (the gun is the hand's child)
                var handRot = rot * Quaternion.Inverse(gun.localRotation);
                var handPos = pos - handRot * (gun.localPosition * rHand.lossyScale.x);
                var elbow = rUpper.position + (right * .25f - Vector3.up * .3f - fwd * .05f) * f;   // out to the side, down
                if (s.hands != null) elbow = StraightWristElbow(handPos, handRot, s.hands.rightForearm, rightForearmLen, elbow);
                TwoBone(rUpper, rLower, rHand, handPos, handRot, 1f, elbow, ads);
            }
            Curl(1, HandRest.PistolGrip, HandRest.PistolGrip, 0f, hold);

            if (left > .001f)
            {
                var barrel = gun.rotation * Quaternion.LookRotation(s.forwardLocal, Vector3.up);
                var fwd = barrel * Vector3.forward;
                Quaternion lRot;
                var grip = gun.TransformPoint(s.foregripLocal);
                if (s.hasHandAxes)
                {
                    // the left hand set by its own axes: under the handguard (k 0) the palm faces up, the fingers wrap across
                    // to the right and the thumb lies along the barrel; round a vertical grip (k 1) the palm faces right,
                    // the fingers wrap forward and the thumb is on top. The palm sits just off the surface.
                    var bRight = barrel * Vector3.right; var bUp = barrel * Vector3.up;
                    float k = Mathf.Clamp01(s.leftRoll / 90f);
                    // under a handguard the fingers run forward-right at about 40° (a straight wrist from an arm that comes
                    // from below-left), not straight across
                    var finger = Vector3.Slerp((bRight + fwd * .85f + bUp * .2f).normalized, (fwd + bRight * .75f).normalized, k);
                    var thumb = Vector3.Slerp((fwd + bUp * .2f).normalized, (bUp + fwd * .25f).normalized, k);
                    lRot = Quaternion.LookRotation(finger, thumb) * Quaternion.Inverse(Quaternion.LookRotation(s.leftFinger, s.leftThumb));
                    grip -= Vector3.Slerp(bUp, bRight, k) * (.025f * f);
                }
                else lRot = s.hasLeftPose ? barrel * Quaternion.AngleAxis(s.leftRoll, Vector3.forward) * s.leftRotInBarrel : lHand.rotation;
                var target = grip - lRot * (s.leftPalm * lHand.lossyScale.x);
                // out of reach: the hand slides back along the handguard (never past the halfway point to the grip)
                float maxSlide = .5f * Vector3.Dot(gun.TransformPoint(s.foregripLocal) - gun.TransformPoint(s.gripLocal), fwd);
                float slid = 0f, limit = leftReach * .98f;
                for (int i = 0; i < 4 && limit > 0f; i++)
                {
                    float over = Vector3.Distance(target, lUpper.position) - limit;
                    if (over <= .002f || slid >= maxSlide) break;
                    float step = Mathf.Min(over, maxSlide - slid);
                    target -= fwd * step; slid += step;
                }
                var right = Vector3.Cross(Vector3.up, fwd).normalized;
                var elbow = lUpper.position + (-Vector3.up * .35f + right * .05f + fwd * .1f) * f;   // under the gun
                float hintW = ads;
                if (s.hands != null) { elbow = StraightWristElbow(target, lRot, s.hands.leftForearm, leftForearmLen, elbow); hintW = 1f; }
                TwoBone(lUpper, lLower, lHand, target, lRot, left, elbow, hintW);
                // a vertical grip is wrapped by every finger; a handguard is cupped
                Curl(0, HandRest.Handguard, HandRest.VerticalGrip, Mathf.Clamp01((s.leftRoll - 20f) / 50f), left);
            }
        }

        // Two-bone IK (root → mid → tip), after Unity's Animation Rigging AnimationRuntimeUtils.SolveTwoBoneIK: bend the
        // middle joint to the reach, swing the root onto the target, then turn the chain about root→tip toward the hint.
        internal static void TwoBone(Transform root, Transform mid, Transform tip, Vector3 targetPos, Quaternion targetRot, float weight, Vector3 hint, float hintWeight)
        {
            var a = root.position; var b = mid.position; var c = tip.position;
            var relBefore = Quaternion.Inverse(mid.rotation) * tip.rotation;   // the clip's wrist, for the twist share below
            var t = Vector3.Lerp(c, targetPos, weight);
            var tRot = Quaternion.Slerp(tip.rotation, targetRot, weight);
            Vector3 ab = b - a, bc = c - b, ac = c - a, at = t - a;
            float abLen = ab.magnitude, bcLen = bc.magnitude;
            float oldAngle = TriangleAngle(ac.magnitude, abLen, bcLen);
            float newAngle = TriangleAngle(at.magnitude, abLen, bcLen);
            var axis = Vector3.Cross(ab, bc);
            if (axis.sqrMagnitude < 1e-8f)
            {
                axis = Vector3.Cross(hint - a, bc);
                if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(at, bc);
                if (axis.sqrMagnitude < 1e-8f) axis = Vector3.up;
            }
            axis.Normalize();
            float half = .5f * (oldAngle - newAngle);
            float sin = Mathf.Sin(half);
            mid.rotation = new Quaternion(axis.x * sin, axis.y * sin, axis.z * sin, Mathf.Cos(half)) * mid.rotation;
            ac = tip.position - a;
            root.rotation = Quaternion.FromToRotation(ac, at) * root.rotation;
            if (hintWeight > 0f)
            {
                b = mid.position; c = tip.position;
                ab = b - a; ac = c - a;
                float acSq = ac.sqrMagnitude;
                if (acSq > 0f)
                {
                    var acN = ac / Mathf.Sqrt(acSq);
                    var ah = hint - a;
                    var abProj = ab - acN * Vector3.Dot(ab, acN);
                    var ahProj = ah - acN * Vector3.Dot(ah, acN);
                    float reach = abLen + bcLen;
                    if (abProj.sqrMagnitude > reach * reach * .001f && ahProj.sqrMagnitude > 0f)
                        root.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(abProj, ahProj), hintWeight) * root.rotation;
                }
            }
            tip.rotation = tRot;

            // These rigs have no forearm twist bones: a wrist turned about the forearm pinches the skin. Give the forearm half
            // of the new twist (it turns about its own axis, so the hand stays put) and keep the hand's world rotation.
            var axis2 = mid.InverseTransformDirection(tip.position - mid.position);
            if (axis2.sqrMagnitude > 1e-8f)
            {
                axis2.Normalize();
                var delta = (Quaternion.Inverse(mid.rotation) * tRot) * Quaternion.Inverse(relBefore);
                var v = new Vector3(delta.x, delta.y, delta.z);
                var p = axis2 * Vector3.Dot(v, axis2);
                var twist = new Quaternion(p.x, p.y, p.z, delta.w);
                float n = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
                if (n > 1e-6f)
                {
                    twist = new Quaternion(twist.x / n, twist.y / n, twist.z / n, twist.w / n);
                    mid.rotation = mid.rotation * Quaternion.Slerp(Quaternion.identity, twist, .5f);
                    tip.rotation = tRot;
                }
            }
        }

        static float TriangleAngle(float aLen, float aLen1, float aLen2)
        {
            float c = Mathf.Clamp((aLen1 * aLen1 + aLen2 * aLen2 - aLen * aLen) / (aLen1 * aLen2) / 2f, -1f, 1f);
            return Mathf.Acos(c);
        }
    }
}
