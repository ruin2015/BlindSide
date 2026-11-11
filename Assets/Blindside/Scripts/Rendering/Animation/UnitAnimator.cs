// Plays a UnitAnimationProfile on a unit's model through the Playables API (no Animator Controller asset):
//   layer 0  full body : idle ↔ legs (forward only, or up to 8 directions blended by where the legs go relative to the aim)
//   layer 1  upper body: the aim pose (selected / in combat / just fired or hit) or, moving without aiming, the
//                        move-upper-body clip; fire / reload / block / upper-body hit / upper-body ability over it
//                        (AvatarMask from the profile, or built from the rig)
//   layer 2  full body : dodge, death, full-body hit, full-body ability
// The owner (UnitView) sets AimYaw — the unit's facing, where the upper body looks and aims. The move direction comes from
// the model's own motion. With directional leg clips the body keeps facing the aim and the legs blend the two clips around
// the move direction; without them the hips turn toward the move and the spine twists back toward the aim (LateUpdate,
// after the animation has posed the bones), up to profile.maxSpineTwist, walking backward past profile.backpedalAngle.
// Everything runs on game time, so a hit stop (timeScale 0) freezes the pose too. Never draws UnityEngine.Random (the
// combat rolls share it and these hooks run inside an engagement).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Blindside.Rendering
{
    [DefaultExecutionOrder(10)]   // after UnitView.Update, which hands over this frame's AimYaw
    public class UnitAnimator : MonoBehaviour
    {
        public UnitAnimationProfile Profile { get; private set; }
        public Animator Animator { get; private set; }

        /// <summary>World yaw (degrees) the upper body aims along.</summary>
        public float AimYaw { get; set; }
        /// <summary>Hold the aim pose regardless of combat (the battle stage).</summary>
        public bool ForceAim { get; set; }
        /// <summary>The owner says the unit is in combat (seen, shooting, being shot at) — aim while true.</summary>
        public bool CombatReady { get; set; }
        /// <summary>The player has this unit selected (aims when the profile says so).</summary>
        public bool Selected { get; set; }
        /// <summary>Fires once the death clip has played out (+ a short beat).</summary>
        public System.Action DeathFinished;
        /// <summary>The held gun (UnitView.SetWeapon): hands on it and raised to the eye while aiming, after the pose.</summary>
        public GunRig Gun { get; set; }

        public bool Dead => dead;

        Transform body;                     // turned to the legs' direction each frame
        PlayableGraph graph;
        AnimationLayerMixerPlayable layers;
        AnimationMixerPlayable baseMix, upperMix;
        // base inputs: 0 idle, 1..8 the leg directions (profile.Directions order: 0°, 45°, … 315°)
        readonly AnimationClipPlayable[] baseClips = new AnimationClipPlayable[9];
        readonly float[] dirAngle = new float[9];
        // base input 9: the aim clip on the whole body — standing still while aiming, the legs hold the aiming stance too
        // (no idle mixed in under the shouldered gun)
        AnimationClipPlayable aimBase;
        const int AimBase = 9;
        // upper inputs: 0 aim, 1 action, 2 move-upper-body
        AnimationClipPlayable aimClip, moveUpperClip, upperAction, fullAction;
        readonly List<Transform> spine = new List<Transform>();

        const int Idle = 0, Fwd = 1, Back = 5;

        Vector3 lastPos, vel;
        bool hasLast, hasBody, dead, deathReported, backpedal, directional;
        float bodyYaw, moveW, aimW, aimSel, upperW, fullW, adsW, leftW, standAimW, hitW;
        float aimUntil, upperEnd = -1f, fullEnd = -1f, deathAt = -1f;
        bool fullHold;

        /// <summary>Builds the graph. `body` is the transform that turns with the legs (the model's root under the view).</summary>
        public void Init(Animator animator, UnitAnimationProfile profile, Transform body)
        {
            Profile = profile; Animator = animator; this.body = body;
            if (animator == null || profile == null) { enabled = false; return; }
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            directional = profile.HasDirectionalWalk;

            graph = PlayableGraph.Create("UnitAnimator_" + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Unit", animator);

            var idleClip = profile.idle != null ? profile.idle : profile.aim;
            baseMix = AnimationMixerPlayable.Create(graph, baseClips.Length + 1);
            Add(baseMix, 0, idleClip, ref baseClips[0]);
            var dirs = profile.Directions;
            for (int i = 0; i < dirs.Length; i++) { dirAngle[i + 1] = dirs[i].angle; Add(baseMix, i + 1, dirs[i].clip, ref baseClips[i + 1]); }
            Add(baseMix, AimBase, profile.aim, ref aimBase);

            upperMix = AnimationMixerPlayable.Create(graph, 3);
            Add(upperMix, 0, profile.aim != null ? profile.aim : idleClip, ref aimClip);
            Add(upperMix, 2, profile.moveUpperBody, ref moveUpperClip);

            layers = AnimationLayerMixerPlayable.Create(graph, 3);
            graph.Connect(baseMix, 0, layers, 0);
            graph.Connect(upperMix, 0, layers, 1);
            layers.SetInputWeight(0, 1f);
            var mask = profile.upperBodyMask != null ? profile.upperBodyMask : BuildUpperMask(animator, profile);
            if (mask != null) layers.SetLayerMaskFromAvatarMask(1, mask);

            output.SetSourcePlayable(layers);
            FindSpine(animator, profile);
            FindLegs(animator);
            ApplyWeights();
            graph.Play();
        }

        void Add(Playable mixer, int port, AnimationClip clip, ref AnimationClipPlayable slot)
        {
            if (clip == null) return;
            slot = AnimationClipPlayable.Create(graph, clip);
            graph.Connect(slot, 0, mixer, port);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        /// <summary>Holds the figure still in the pose it has now (the armoury's turned-back gadget pose): the graph stops and this
        /// stops running, so nothing writes the bones until it is released.</summary>
        public void SetFrozen(bool on)
        {
            if (graph.IsValid()) { if (on) graph.Stop(); else graph.Play(); }
            enabled = !on;
        }

        // ================================================================
        //  Actions
        // ================================================================
        public void PlayFire()
        {
            if (dead || Profile == null) return;
            if (Profile.fire != null) PlayUpper(Profile.fire, Profile.fireSpeed, true);
            Gun?.Kick();
            AimNow();
        }
        public void PlayReload() { if (!dead && Profile != null && Profile.reload != null) PlayUpper(Profile.reload, 1f, true); }

        public void PlayHit()
        {
            if (dead || Profile == null || Profile.hit == null) return;
            if (Profile.hitUpperBodyOnly) PlayUpper(Profile.hit, Profile.hitSpeed, false); else PlayFull(Profile.hit, Profile.hitSpeed, false, false);
            Gun?.Jolt();
            AimNow();
        }

        /// <summary>The shield stopped a round (`blocked` true): the block clip. A miss (`blocked` false) plays nothing — the
        /// round flies wide, nobody dodges.</summary>
        public void PlayDodge(bool blocked)
        {
            if (dead || Profile == null || !blocked) return;
            if (Profile.block != null) PlayUpper(Profile.block, Profile.blockSpeed, false);
            AimNow();
        }

        public void PlayAbility(string key)
        {
            if (dead || Profile == null) return;
            var a = Profile.FindAbility(key);
            if (a == null) return;
            if (a.fullBody) PlayFull(a.clip, a.speed, false, true); else PlayUpper(a.clip, a.speed, true);
        }

        /// <summary>Plays the death clip (held on its last frame). False when there is none — the caller fades the figure itself.</summary>
        public bool PlayDeath()
        {
            if (dead) return Profile != null && Profile.death != null;
            dead = true;
            if (Profile == null || Profile.death == null) return false;
            PlayFull(Profile.death, 1f, true, true);
            deathAt = Time.time + Profile.death.length + .4f;
            return true;
        }

        /// <summary>Seconds until the running dodge reaches its peak (the body furthest out of the line of fire); −1 when no
        /// dodge is running or it is already past the peak.</summary>
        public float DodgePeakIn
        {
            get
            {
                if (Profile == null || Profile.dodge == null || !fullAction.IsValid() || fullAction.GetAnimationClip() != Profile.dodge || Time.time >= fullEnd) return -1f;
                float left = Profile.dodgePeakTime - (float)fullAction.GetTime();
                return left < 0f ? -1f : left / Mathf.Max(.01f, Profile.dodgeSpeed);
            }
        }

        void AimNow() { if (Profile != null) aimUntil = Mathf.Max(aimUntil, Time.time + Profile.aimHoldSeconds); }

        // `restart`: a clip already playing starts over (each round fired); false: a reaction already running carries on
        // (a burst of misses doesn't restart the dodge every round).
        void PlayUpper(AnimationClip clip, float speed, bool restart)
        {
            if (!graph.IsValid() || clip == null) return;
            if (!restart && Running(upperAction, clip, upperEnd)) return;
            Replace(ref upperAction, clip, upperMix, 1, speed);
            upperEnd = Time.time + clip.length / Mathf.Max(.01f, speed);
        }

        void PlayFull(AnimationClip clip, float speed, bool hold, bool restart, float startTime = 0f)
        {
            if (!graph.IsValid() || clip == null) return;
            if (!restart && Running(fullAction, clip, fullEnd)) return;
            Replace(ref fullAction, clip, layers, 2, speed);
            startTime = Mathf.Clamp(startTime, 0f, clip.length);
            if (startTime > 0f) fullAction.SetTime(startTime);
            fullHold = hold;
            fullEnd = Time.time + (clip.length - startTime) / Mathf.Max(.01f, speed);
        }

        static bool Running(AnimationClipPlayable p, AnimationClip clip, float end) => p.IsValid() && p.GetAnimationClip() == clip && Time.time < end;

        void Replace(ref AnimationClipPlayable slot, AnimationClip clip, Playable mixer, int port, float speed)
        {
            if (slot.IsValid())
            {
                if (slot.GetAnimationClip() == clip) { slot.SetTime(0); slot.SetSpeed(speed); return; }
                graph.Disconnect(mixer, port);
                slot.Destroy();
            }
            slot = AnimationClipPlayable.Create(graph, clip);
            slot.SetSpeed(speed);
            graph.Connect(slot, 0, mixer, port);
        }

        // ================================================================
        //  Per frame
        // ================================================================
        void Update()
        {
            if (!graph.IsValid()) return;
            // undo last frame's twist before the animation runs: a spine bone no clip animates would otherwise keep turning
            if (spinePre != null) for (int i = 0; i < spine.Count; i++) if (spine[i] != null) spine[i].localRotation = spinePre[i];
            float dt = Time.deltaTime, now = Time.time;
            var pos = body != null ? body.position : transform.position;
            if (hasLast && dt > 0f)
            {
                var step = pos - lastPos; step.y = 0f;
                // a jump (deployment, a scripted move) is not walking
                var v = step.magnitude > 1.5f ? Vector3.zero : step / dt;
                vel = Vector3.Lerp(vel, v, 1f - Mathf.Exp(-dt * 10f));
            }
            lastPos = pos; hasLast = true;

            float speed = vel.magnitude;
            bool moving = !dead && speed > .25f;
            bool aiming = !dead && (ForceAim || CombatReady || (Selected && Profile.aimWhenSelected) || now < aimUntil || now < upperEnd);
            bool upperOn = aiming || (moving && (moveUpperClip.IsValid() || aimClip.IsValid()));
            float k = Profile.crossFade > 0f ? 1f - Mathf.Exp(-dt / (Profile.crossFade * .5f)) : 1f;
            moveW = Mathf.Lerp(moveW, moving ? 1f : 0f, k);
            standAimW = Mathf.Lerp(standAimW, aiming ? 1f : 0f, k);
            aimW = Mathf.Lerp(aimW, upperOn ? 1f : 0f, k);
            aimSel = Mathf.Lerp(aimSel, aiming || !moveUpperClip.IsValid() ? 1f : 0f, k);
            upperW = Mathf.Lerp(upperW, now < upperEnd ? 1f : 0f, k);
            bool fullOn = fullAction.IsValid() && (fullHold || now < fullEnd);
            fullW = Mathf.Lerp(fullW, fullOn ? 1f : 0f, k);

            // the gun: up at the eye while aiming (firing included, not while reloading / reacting / dodging); the left
            // hand on the foregrip except while it reloads, uses an ability or the body falls
            var upClip = upperAction.IsValid() && now < upperEnd ? upperAction.GetAnimationClip() : null;
            var fullClip = fullOn ? fullAction.GetAnimationClip() : null;
            bool firing = upClip != null && upClip == Profile.fire;
            // being hit (or blocking) keeps the gun shouldered and both hands on it: the clip moves the body, the gun only
            // jolts (GunRig.Jolt) — letting the clip's arms take the gun made it jump out of the hands for a moment
            bool reacting = upClip == Profile.hit || upClip == Profile.block;
            bool adsOn = aiming && (fullClip == null || fullClip == Profile.hit) && (upClip == null || firing || reacting);
            bool leftOn = !dead && (upClip == null || firing || reacting)
                          && (fullClip == null || fullClip == Profile.dodge || fullClip == Profile.hit);
            adsW = Mathf.Lerp(adsW, adsOn ? 1f : 0f, k);
            leftW = Mathf.Lerp(leftW, leftOn ? 1f : 0f, k);
            hitW = Mathf.Lerp(hitW, upClip == Profile.hit || fullClip == Profile.hit ? 1f : 0f, k);

            float moveYaw = speed > .05f ? Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg : bodyYaw;
            float targetBody = AimYaw;
            backpedal = false;
            if (!directional && moving && !dead)
            {
                float rel = Mathf.Abs(Mathf.DeltaAngle(moveYaw, AimYaw));
                backpedal = rel > Profile.backpedalAngle && baseClips[Back].IsValid();
                targetBody = backpedal ? moveYaw + 180f : moveYaw;
            }
            if (!hasBody) { bodyYaw = targetBody; hasBody = true; }
            if (!dead) bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, targetBody, 540f * dt);
            if (body != null) body.rotation = Quaternion.Euler(0f, bodyYaw, 0f);

            // leg playback speed follows the real speed (no foot sliding)
            float rate = Mathf.Clamp(speed / Profile.walkClipSpeed, .5f, 2.5f);
            for (int i = 1; i < baseClips.Length; i++) if (baseClips[i].IsValid()) baseClips[i].SetSpeed(rate);
            if (moveUpperClip.IsValid()) moveUpperClip.SetSpeed(rate);

            WeighBase(Mathf.DeltaAngle(bodyYaw, moveYaw));
            ApplyWeights();
            for (int i = 0; i < baseClips.Length; i++) Loop(baseClips[i]);
            Loop(aimClip); Loop(moveUpperClip); Loop(aimBase);
            if (fullAction.IsValid() && fullHold && fullAction.GetTime() >= fullAction.GetAnimationClip().length) fullAction.SetSpeed(0);

            if (dead && !deathReported && deathAt > 0f && now >= deathAt) { deathReported = true; DeathFinished?.Invoke(); }
        }

        readonly float[] baseW = new float[9];

        void WeighBase(float relToBody)
        {
            for (int i = 0; i < baseW.Length; i++) baseW[i] = 0f;
            baseW[Idle] = 1f - moveW;
            if (directional)
            {
                // the two available clips on either side of the move direction (clockwise from forward), by angle
                float a = Mathf.Repeat(relToBody, 360f);
                int lo = -1, hi = -1; float loGap = 999f, hiGap = 999f;
                for (int i = 1; i < baseClips.Length; i++)
                {
                    if (!baseClips[i].IsValid()) continue;
                    float below = Mathf.Repeat(a - dirAngle[i], 360f), above = Mathf.Repeat(dirAngle[i] - a, 360f);
                    if (below < loGap) { loGap = below; lo = i; }
                    if (above < hiGap) { hiGap = above; hi = i; }
                }
                if (lo < 0) { baseW[Idle] = 1f; return; }
                float span = loGap + hiGap;
                float t = span > 1e-3f ? loGap / span : 0f;
                baseW[lo] += moveW * (1f - t);
                baseW[hi] += moveW * t;
            }
            else
            {
                int walk = backpedal ? Back : Fwd;
                if (!baseClips[walk].IsValid()) walk = baseClips[Fwd].IsValid() ? Fwd : Idle;
                baseW[walk] += moveW;
            }
        }

        void ApplyWeights()
        {
            if (!graph.IsValid()) return;
            // standing still and aiming: the idle's share goes to the aim clip
            float still = baseW[Idle], aimStill = aimBase.IsValid() ? still * standAimW : 0f;
            for (int i = 0; i < baseW.Length; i++) baseMix.SetInputWeight(i, baseClips[i].IsValid() ? (i == Idle ? still - aimStill : baseW[i]) : 0f);
            baseMix.SetInputWeight(AimBase, aimStill);
            float rest = 1f - upperW;
            upperMix.SetInputWeight(0, aimClip.IsValid() ? rest * aimSel : 0f);
            upperMix.SetInputWeight(2, moveUpperClip.IsValid() ? rest * (1f - aimSel) : 0f);
            upperMix.SetInputWeight(1, upperAction.IsValid() ? upperW : 0f);
            layers.SetInputWeight(1, Mathf.Max(aimW, upperW));
            layers.SetInputWeight(2, fullAction.IsValid() ? fullW : 0f);
        }

        static void Loop(AnimationClipPlayable p)
        {
            if (!p.IsValid()) return;
            float len = p.GetAnimationClip().length;
            if (len > 0f && p.GetTime() > len) p.SetTime(p.GetTime() % len);
        }

        Quaternion[] spinePre;

        // The upper body back toward the aim after the animation has posed it (only when the legs are turned away from it).
        void LateUpdate()
        {
            if (!graph.IsValid()) return;
            PinLegs();
            TwistSpine();
            WalkFeet();
            if (Gun != null) { Gun.FaceFollow = hitW; Gun.Solve(adsW, leftW, AimYaw, Time.deltaTime, dead ? 0f : 1f); }
        }

        // ---- standing legs: firing, being hit or blocking (and the combat stance that follows) must not move the lower body.
        // The fire / hit clips swing the hips and the legs, and the base layer would change its stance when aiming starts; so
        // while the unit stands and is engaged, the hips and legs keep the pose they had just before (cached each frame
        // while nothing runs), blended in and out over the cross-fade. Full-body abilities and death are left alone.
        Transform hips;
        readonly Quaternion[] pinLegRot = new Quaternion[8];
        Quaternion pinHipsRot;
        Vector3 pinHipsPos;
        bool hasPin;
        float pinW;

        void PinLegs()
        {
            if (!hasLegs || hips == null) return;
            float now = Time.time;
            var fullClip = fullAction.IsValid() && (fullHold || now < fullEnd) ? fullAction.GetAnimationClip() : null;
            bool engaged = now < upperEnd || fullClip == Profile.hit || now < aimUntil || CombatReady;
            bool pinned = !dead && moveW < .05f && engaged && (fullClip == null || fullClip == Profile.hit);
            float k = Profile.crossFade > 0f ? 1f - Mathf.Exp(-Time.deltaTime / (Profile.crossFade * .5f)) : 1f;
            pinW = Mathf.Lerp(pinW, pinned && hasPin ? 1f : 0f, k);
            if (!pinned && pinW < .01f)
            {
                // nothing running: remember this stance (standing only — a mid-stride pose is no stance)
                if (moveW < .05f && !dead && fullClip == null)
                {
                    pinHipsPos = hips.localPosition; pinHipsRot = hips.localRotation;
                    for (int i = 0; i < 8; i++) if (legBones[i] != null) pinLegRot[i] = legBones[i].localRotation;
                    hasPin = true;
                }
                return;
            }
            if (!hasPin) return;
            hips.localPosition = Vector3.Lerp(hips.localPosition, pinHipsPos, pinW);
            hips.localRotation = Quaternion.Slerp(hips.localRotation, pinHipsRot, pinW);
            for (int i = 0; i < 8; i++) if (legBones[i] != null) legBones[i].localRotation = Quaternion.Slerp(legBones[i].localRotation, pinLegRot[i], pinW);
        }

        // ---- walking feet: the retargeted walk clips splay the toes and swing the feet wide on these rigs. While moving,
        // each foot is drawn toward the line under the hips (two-bone IK on the leg) and the whole leg turns about
        // hip → ankle until the toes point along the body (± walkToeOut); turning about that line leaves the foot in place.
        readonly Transform[] legBones = new Transform[8];   // L upper, lower, foot, toes, R upper, lower, foot, toes
        // each foot's own sideways axis (foot space, from the rest pose): walking pitches the foot about it, so it stays
        // level and gives the foot's heading even when the toes point at the ground
        readonly Vector3[] footSide = new Vector3[2];
        bool hasLegs;

        void FindLegs(Animator a)
        {
            hasLegs = false;
            if (!a.isHuman) return;
            var ids = new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
                              HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes };
            for (int i = 0; i < ids.Length; i++) legBones[i] = a.GetBoneTransform(ids[i]);
            hips = a.GetBoneTransform(HumanBodyBones.Hips);
            hasLegs = legBones[0] != null && legBones[1] != null && legBones[2] != null && legBones[4] != null && legBones[5] != null && legBones[6] != null;
            // the model is still in its rest pose here (Init runs before the first frame): its right is each foot's side
            if (hasLegs)
            {
                footSide[0] = legBones[2].InverseTransformDirection(a.transform.right).normalized;
                footSide[1] = legBones[6].InverseTransformDirection(a.transform.right).normalized;
            }
        }

        /// <summary>A copy of a posed figure (the battle stage) isn't in its rest pose at Init: take the original's foot axes.</summary>
        public void CopyFootAxes(UnitAnimator from)
        {
            if (from == null || !from.hasLegs || !hasLegs) return;
            footSide[0] = from.footSide[0]; footSide[1] = from.footSide[1];
        }

        void WalkFeet()
        {
            if (!hasLegs || dead) return;
            float w = moveW * (1f - fullW);
            if (w < .01f) return;
            var fwd = Quaternion.Euler(0f, bodyYaw, 0f) * Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var center = (legBones[0].position + legBones[4].position) * .5f;
            Leg(0, -1f, center, fwd, right, w);
            Leg(4, 1f, center, fwd, right, w);
        }

        void Leg(int i, float side, Vector3 center, Vector3 fwd, Vector3 right, float w)
        {
            Transform upper = legBones[i], lower = legBones[i + 1], foot = legBones[i + 2];
            var sideLocal = footSide[i == 0 ? 0 : 1];
            var f = foot.position;
            float lateral = Vector3.Dot(f - center, right);
            var target = f - right * (lateral * Profile.walkFeetNarrow * w);
            GunRig.TwoBone(upper, lower, foot, target, foot.rotation, 1f, f, 0f);
            var axis = foot.position - upper.position;
            if (axis.sqrMagnitude < 1e-8f) return;
            axis.Normalize();
            // mid-stride the leg leans, so a turn about it moves the toes by a different yaw: a few measured steps
            float want = 0f;
            for (int k = 0; k < 3; k++)
            {
                var toeDir = Vector3.Cross(foot.TransformDirection(sideLocal), Vector3.up); toeDir.y = 0f;
                if (toeDir.sqrMagnitude < 1e-6f) return;
                float yaw = Vector3.SignedAngle(fwd, toeDir, Vector3.up);
                if (k == 0) want = Mathf.LerpAngle(yaw, side * Profile.walkToeOut, w);
                float turn = Mathf.DeltaAngle(yaw, want);
                if (Mathf.Abs(turn) < .5f) return;
                upper.rotation = Quaternion.AngleAxis(-turn, axis) * upper.rotation;
            }
        }

        void TwistSpine()
        {
            if (spine.Count == 0) return;
            if (spinePre == null) spinePre = new Quaternion[spine.Count];
            for (int i = 0; i < spine.Count; i++) if (spine[i] != null) spinePre[i] = spine[i].localRotation;
            if (dead || directional) return;
            float twist = Mathf.Clamp(Mathf.DeltaAngle(bodyYaw, AimYaw), -Profile.maxSpineTwist, Profile.maxSpineTwist) * aimW * aimSel;
            if (Mathf.Abs(twist) < .5f) return;
            float share = twist / spine.Count;
            foreach (var b in spine) if (b != null) b.rotation = Quaternion.AngleAxis(share, Vector3.up) * b.rotation;
        }

        // ================================================================
        //  Rig helpers
        // ================================================================
        void FindSpine(Animator a, UnitAnimationProfile p)
        {
            spine.Clear();
            if (a.isHuman)
            {
                foreach (var hb in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
                {
                    var t = a.GetBoneTransform(hb);
                    if (t != null) spine.Add(t);
                }
                return;
            }
            if (p.spineBones == null) return;
            foreach (var n in p.spineBones)
            {
                var t = FindDeep(a.transform, n);
                if (t != null) spine.Add(t);
            }
        }

        static AvatarMask BuildUpperMask(Animator a, UnitAnimationProfile p)
        {
            var mask = new AvatarMask();
            if (a.isHuman)
            {
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
                                             AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK })
                    mask.SetHumanoidBodyPartActive(part, true);
                return mask;
            }
            var upper = FindDeep(a.transform, p.upperBodyRootBone);
            if (upper == null) return null;   // no split: the upper layer then drives the whole body
            mask.AddTransformPath(a.transform, true);
            string upperPath = PathFrom(a.transform, upper);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                mask.SetTransformActive(i, path == upperPath || path.StartsWith(upperPath + "/"));
            }
            return mask;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        static string PathFrom(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c != root; c = c.parent) parts.Insert(0, c.name);
            return string.Join("/", parts);
        }
    }
}
