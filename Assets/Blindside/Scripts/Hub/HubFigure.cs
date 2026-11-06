// A figure at the HQ (the player's FLARE or an NPC): the HQ's own idle / walk / run clips (AnimationPack/Neutral Idle, Walking, Running —
// the HQ only; the battle keeps its profile clips), mixed by how fast it moves through a Playables graph of its own (the battle's
// UnitAnimator aims, fires and carries a gun — none of that here):
//   0 … walkSpeed         idle → walk
//   walkSpeed … runSpeed  walk → run (HubPlayer: Shift)
// Symmetric legs: the HQ's Running turns the left foot out (it lands ~58° out where the right lands 22°, and swings back off the other
// way). So for walk and run the left leg is not the clip's own: each stride clip has a mirrored take of itself (walkMirror / runMirror,
// imported with Mirror on — Blindside > Scenes > Set Up HQ Clips) played half a stride later, layered over it with a left-leg-only
// mask. A mirrored right step half a cycle on is exactly what the left leg of a symmetric gait does, so the left foot is the right
// foot's mirror at every moment of the stride — by construction, nothing measured.
// Knees locked: the walk / run splay the knees out and turn them with the stride; after the pose each thigh is turned about its hip →
// ankle line so its kneecap (from the rest pose) always faces a little inside straight ahead (the foot stays put and keeps its aim).
// Feet on the floor: two frames in, each clip is sampled over its cycle (16 poses) for its lowest toe; the model is shifted so that toe
// sits at toeHeight (the toe bone's height over the sole, measured by the builder in the bind pose) — per clip, blended by the weights.
// One stride: walk and run share a phase (both start on the same footfall), advanced at the blended step rate and written into both
// clips (and their mirrors, + half a cycle), so a walk ↔ run blend never mixes two different leg positions. Guns are never shown at the
// HQ (renderers under a gun-like node are off). Speed comes from the owner (HubPlayer) or stays 0 (an NPC idles). Nothing is allocated
// per frame.
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Blindside.Rendering;

namespace Blindside.Hub
{
    public class HubFigure : MonoBehaviour
    {
        [Tooltip("Fallback idle / walk when the HQ clips are empty.")]
        public UnitAnimationProfile profile;
        [Tooltip("The HQ's idle loop (empty: the profile's idle).")]
        public AnimationClip idle;
        [Tooltip("The HQ's walk loop (in place).")]
        public AnimationClip walk;
        [Tooltip("The HQ's run loop (in place).")]
        public AnimationClip run;
        [Tooltip("The walk loop mirrored (left ↔ right): the left leg is taken from it, half a stride on. Empty: the clip's own left leg.")]
        public AnimationClip walkMirror;
        [Tooltip("The run loop mirrored (left ↔ right): the left leg is taken from it, half a stride on. Empty: the clip's own left leg.")]
        public AnimationClip runMirror;
        [Tooltip("Ground speed (m/s) of a full walk / a full run (HubPlayer's walkSpeed / runSpeed).")]
        public float walkSpeed = 1.7f, runSpeed = 4.2f;
        [Tooltip("The speed (m/s) each clip's steps are made for: it plays at speed / this.")]
        public float walkClipSpeed = 1.6f, runClipSpeed = 4f;
        [Tooltip("The toe bone's height over the sole (m), from the bind pose (the builder measures it). Feet are grounded on it.")]
        public float toeHeight = .035f;
        [Tooltip("Walk / run: how firmly the knees are held facing their fixed direction (1 = locked: they never turn with the stride).")]
        [Range(0f, 1f)] public float kneeStraighten = 1f;
        [Tooltip("Where the knees aim: degrees inward from straight ahead (a little: knees drawn together).")]
        public float kneeInward = 5f;

        /// <summary>Ground speed now (m/s); the owner sets it every frame.</summary>
        public float Speed { get; set; }

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable idleP, walkP, runP, walkM, runM;   // walkM / runM: the mirrored takes (invalid when absent)
        float walkLen = 1f, runLen = 1f;
        float shown, phase;
        Transform model, toeL, toeR;
        Vector3 modelBase;
        readonly float[] lift = new float[3];
        readonly float[] weight = new float[3];
        static AvatarMask leftLeg;

        static readonly string[] GunWords = { "gun", "weapon", "rifle", "pistol", "wpn" };

        /// <summary>Only the left leg (and its foot IK goal): what the mirrored take overrides.</summary>
        static AvatarMask LeftLeg()
        {
            if (leftLeg != null) return leftLeg;
            leftLeg = new AvatarMask { name = "HubLeftLeg" };
            for (var p = AvatarMaskBodyPart.Root; p < AvatarMaskBodyPart.LastBodyPart; p++) leftLeg.SetHumanoidBodyPartActive(p, false);
            leftLeg.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
            leftLeg.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, true);
            return leftLeg;
        }

        void Start()
        {
            HideGuns(transform);
            var anim = GetComponentInChildren<Animator>();
            var idleClip = idle != null ? idle : (profile != null ? profile.idle : null);
            if (anim == null || idleClip == null) return;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // a culled Animator isn't posed by the foot sampling (a handful of figures)
            model = anim.transform; modelBase = model.localPosition;
            FindLegs(anim);   // (before the graph poses anything: the kneecaps' rest directions)
            graph = PlayableGraph.Create("HubFigure");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", anim);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            var w = walk != null ? walk : (profile != null && profile.walkForward != null ? profile.walkForward : idleClip);
            var r = run != null ? run : w;
            idleP = AnimationClipPlayable.Create(graph, idleClip);
            walkLen = Mathf.Max(.1f, w.length); runLen = Mathf.Max(.1f, r.length);
            graph.Connect(idleP, 0, mixer, 0);
            graph.Connect(Stride(w, anim.isHuman ? walkMirror : null, out walkP, out walkM), 0, mixer, 1);
            graph.Connect(Stride(r, anim.isHuman ? (run != null ? runMirror : walkMirror) : null, out runP, out runM), 0, mixer, 2);
            output.SetSourcePlayable(mixer);
            Weights(0f);
            graph.Play();
            startFrame = Time.frameCount;
        }

        /// <summary>A stride clip, with its mirrored take's left leg layered over it when there is one. Both are driven by the shared
        /// phase (speed 0), not by the graph's clock.</summary>
        Playable Stride(AnimationClip clip, AnimationClip mirror, out AnimationClipPlayable main, out AnimationClipPlayable mirrored)
        {
            main = AnimationClipPlayable.Create(graph, clip);
            main.SetSpeed(0d);
            mirrored = default;
            if (mirror == null) return main;
            mirrored = AnimationClipPlayable.Create(graph, mirror);
            mirrored.SetSpeed(0d);
            var layers = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(main, 0, layers, 0);
            graph.Connect(mirrored, 0, layers, 1);
            layers.SetInputWeight(0, 1f);
            layers.SetInputWeight(1, 1f);
            layers.SetLayerMaskFromAvatarMask(1, LeftLeg());
            return layers;
        }

        /// <summary>A stride clip (and its mirror, half a cycle on) at `t` of its cycle (0–1).</summary>
        static void SetStride(AnimationClipPlayable main, AnimationClipPlayable mirrored, float len, float t)
        {
            main.SetTime(t * len);
            if (mirrored.IsValid()) mirrored.SetTime(Mathf.Repeat(t + .5f, 1f) * len);
        }

        // an Evaluate before the Animator has run its own first updates does not pose it (it binds then): sampled two frames in
        bool calibrated;
        int startFrame;

        /// <summary>Each clip alone, sampled over its cycle: how far its lowest toe sits above toeHeight (lift[i], m in the figure's space).</summary>
        void Calibrate()
        {
            var anim = model != null ? model.GetComponent<Animator>() : null;
            if (anim != null && anim.isHuman)
            {
                toeL = anim.GetBoneTransform(HumanBodyBones.LeftToes); toeR = anim.GetBoneTransform(HumanBodyBones.RightToes);
                FindLegs(anim);
            }
            if (toeL == null || toeR == null) return;
            var keep = model.localPosition;
            model.localPosition = modelBase;
            for (int c = 0; c < 3; c++)
            {
                for (int k = 0; k < 3; k++) mixer.SetInputWeight(k, k == c ? 1f : 0f);
                float low = float.MaxValue;
                for (int s = 0; s < 16; s++)
                {
                    float t = s / 16f;
                    if (c == 0) idleP.SetTime(t * idleP.GetAnimationClip().length);
                    else if (c == 1) SetStride(walkP, walkM, walkLen, t);
                    else SetStride(runP, runM, runLen, t);
                    graph.Evaluate(0f);
                    low = Mathf.Min(low, Mathf.Min(Height(toeL), Height(toeR)));
                }
                lift[c] = low - toeHeight;
            }
            idleP.SetTime(0d); SetStride(walkP, walkM, walkLen, 0f); SetStride(runP, runM, runLen, 0f);
            model.localPosition = keep;
        }

        float Height(Transform t) => transform.InverseTransformPoint(t.position).y;

        // ---- knees locked ------------------------------------------------------------------------------------------------------------
        // The HQ's walk / run splay the knees out and swing them with the stride, and at a small bend the shin even kinks sideways (up
        // to ~40° off the thigh's bend direction) — turning the thigh alone cannot take that out. So each leg is solved again after the graph
        // has posed it (SolveLeg): hip and ankle where the clip put them, the clip's bend, the knee in the plane facing kneeInward° inside
        // straight ahead, the kneecap (from the model's rest pose) facing the same way — every frame, fully (× kneeStraighten, the walk + run
        // weight × 4). The foot keeps where the clip put it and its rotation, so it keeps its own (mirrored) aim.
        Transform hipL, kneeL, ankleL, hipR, kneeR, ankleR;
        Vector3 capL, capR;        // the kneecap's direction in each thigh's local space (rest pose)
        bool capsReady;

        void FindLegs(Animator anim)
        {
            if (anim == null || !anim.isHuman) return;
            hipL = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg); kneeL = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            ankleL = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            hipR = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg); kneeR = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            ankleR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hipL == null || kneeL == null || ankleL == null || hipR == null || kneeR == null || ankleR == null) { hipL = null; return; }
            if (capsReady) return;
            // the rest pose (nothing evaluated yet): the kneecaps face the figure's forward
            capL = Quaternion.Inverse(hipL.rotation) * transform.forward;
            capR = Quaternion.Inverse(hipR.rotation) * transform.forward;
            capsReady = true;
        }

        void LateUpdate()
        {
            if (!calibrated || hipL == null || !capsReady || kneeStraighten <= 0f) return;
            // full strength from a quarter of the walk's weight on: no half-held knees while a step starts or stops
            float w = Mathf.Clamp01((weight[1] + weight[2]) * 4f);
            if (w < 1e-3f) return;
            SolveLeg(hipL, kneeL, ankleL, capL, -1f, w);
            SolveLeg(hipR, kneeR, ankleR, capR, 1f, w);
        }

        /// <summary>A two-bone solve on the clip's leg: the hip and the ankle stay where the clip put them, the knee keeps the clip's
        /// bend (the thigh / shin lengths fix it), but it is put in the plane through the hip → ankle line that faces kneeInward° inside
        /// straight ahead — so it can neither swivel nor kink sideways. The thigh aims at that knee and turns about itself so its kneecap
        /// faces the same way; the shin aims at the ankle; the foot keeps its world rotation.</summary>
        /// <param name="side">−1 left, +1 right (the knee's outward direction in the figure's x)</param>
        void SolveLeg(Transform hip, Transform knee, Transform ankle, Vector3 capLocal, float side, float w)
        {
            Vector3 h = hip.position, k = knee.position, a = ankle.position;
            float upper = (k - h).magnitude, lower = (a - k).magnitude;
            var d = a - h;
            float dist = d.magnitude;
            if (upper < 1e-4f || lower < 1e-4f || dist < 1e-4f) return;
            var n = d / dist;
            // the plane: straight ahead, a little inward, flattened onto the plane square to the hip → ankle line
            var want = Quaternion.AngleAxis(-side * kneeInward, transform.up) * transform.forward;
            want -= n * Vector3.Dot(want, n);
            if (want.sqrMagnitude < 1e-6f) return;
            want.Normalize();
            // the knee for these lengths at this hip → ankle distance (the law of cosines), in that plane
            float reach = Mathf.Min(dist, upper + lower - 1e-4f);
            float cos = Mathf.Clamp((upper * upper + reach * reach - lower * lower) / (2f * upper * reach), -1f, 1f);
            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos));
            var target = h + n * (upper * cos) + want * (upper * sin);
            target = Vector3.Lerp(k, target, kneeStraighten * w);

            var footRot = ankle.rotation;
            // thigh: aim it at the new knee, then turn it about itself so its kneecap faces the plane's way
            hip.rotation = Quaternion.FromToRotation(k - h, target - h) * hip.rotation;
            var thigh = (target - h).normalized;
            var cap = hip.rotation * capLocal; cap -= thigh * Vector3.Dot(cap, thigh);
            var face = want - thigh * Vector3.Dot(want, thigh);
            if (cap.sqrMagnitude > 1e-6f && face.sqrMagnitude > 1e-6f)
                hip.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(cap, face, thigh) * kneeStraighten * w, thigh) * hip.rotation;
            // shin: from the knee (where it is now) back to the clip's ankle
            var kneeNow = knee.position;
            knee.rotation = Quaternion.FromToRotation(ankle.position - kneeNow, a - kneeNow) * knee.rotation;
            ankle.rotation = footRot;
        }

        void Weights(float s)
        {
            float wIn = Mathf.Clamp01(s / Mathf.Max(.1f, walkSpeed));
            float rIn = Mathf.Clamp01((s - walkSpeed) / Mathf.Max(.1f, runSpeed - walkSpeed));
            weight[0] = 1f - wIn; weight[1] = wIn * (1f - rIn); weight[2] = wIn * rIn;
            for (int i = 0; i < 3; i++) mixer.SetInputWeight(i, weight[i]);
            // ground the feet: the clips' own lifts, blended like the clips
            float l = weight[0] * lift[0] + weight[1] * lift[1] + weight[2] * lift[2];
            if (model != null) model.localPosition = modelBase - Vector3.up * l;
        }

        void Update()
        {
            if (!graph.IsValid()) return;
            if (!calibrated && Time.frameCount > startFrame + 1) { calibrated = true; Calibrate(); }
            // ease the shown speed (≈ .15 s) so a start, a stop and Shift blend instead of popping
            shown = Mathf.Lerp(shown, Speed, 1f - Mathf.Exp(-14f * Time.deltaTime));
            if (Speed <= .01f && shown < .02f) shown = 0f;
            Weights(shown);

            // one stride for walk and run: cycles per second blended by their weights
            float sw = Mathf.Max(shown, walkSpeed * .6f), sr = Mathf.Max(shown, walkSpeed + (runSpeed - walkSpeed) * .5f);
            float rateW = Mathf.Clamp(sw / Mathf.Max(.1f, walkClipSpeed), .7f, 1.4f) / walkLen;
            float rateR = Mathf.Clamp(sr / Mathf.Max(.1f, runClipSpeed), .7f, 1.3f) / runLen;
            float moving = weight[1] + weight[2];
            if (moving > 1e-3f)
            {
                float rate = (weight[1] * rateW + weight[2] * rateR) / moving;
                phase = Mathf.Repeat(phase + rate * Time.deltaTime, 1f);
            }
            else phase = 0f;   // standing: the next step starts on the clips' first footfall
            SetStride(walkP, walkM, walkLen, phase);
            SetStride(runP, runM, runLen, phase);
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        /// <summary>Switches off every renderer under a node whose name reads like a gun.</summary>
        public static int HideGuns(Transform root)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                for (var t = r.transform; t != null && t != root.parent; t = t.parent)
                {
                    string nm = t.name.ToLowerInvariant();
                    bool gun = false;
                    foreach (var w in GunWords) if (nm.Contains(w)) { gun = true; break; }
                    if (!gun) continue;
                    r.enabled = false; n++;
                    break;
                }
            }
            return n;
        }
    }
}
