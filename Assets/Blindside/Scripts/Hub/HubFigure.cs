// A figure at the HQ (the player's FLARE or an NPC): the HQ's own idle / walk / run clips (AnimationPack/Neutral Idle, Walking, Running —
// the HQ only; the battle keeps its profile clips), mixed by how fast it moves through a Playables graph of its own (the battle's
// UnitAnimator aims, fires and carries a gun — none of that here):
//   0 … walkSpeed         idle → walk
//   walkSpeed … runSpeed  walk → run (HubPlayer: Shift)
// Feet on the floor: at Start each clip is sampled over its cycle (16 poses) for its lowest toe; the model is shifted so that toe sits
// at toeHeight (the toe bone's height over the sole, measured by the builder in the bind pose) — per clip, blended by the weights.
// Foot turn: the same sampling measures each planted foot's heel→toe yaw; a clip that lands a foot turned out past the idle stance by
// more than 10° (the HQ's Running: the left foot ~35° too far) has that foot turned back around its shin in LateUpdate, weighted like
// the clip.
// One stride: walk and run share a phase (both start on the same footfall), advanced at the blended step rate and written into both
// clips, so a walk ↔ run blend never mixes two different leg positions. Guns are never shown at the HQ (renderers under a gun-like
// node are off). Speed comes from the owner (HubPlayer) or stays 0 (an NPC idles). Nothing is allocated per frame.
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
        [Tooltip("Ground speed (m/s) of a full walk / a full run (HubPlayer's walkSpeed / runSpeed).")]
        public float walkSpeed = 1.7f, runSpeed = 4.2f;
        [Tooltip("The speed (m/s) each clip's steps are made for: it plays at speed / this.")]
        public float walkClipSpeed = 1.6f, runClipSpeed = 4f;
        [Tooltip("The toe bone's height over the sole (m), from the bind pose (the builder measures it). Feet are grounded on it.")]
        public float toeHeight = .035f;

        /// <summary>Ground speed now (m/s); the owner sets it every frame.</summary>
        public float Speed { get; set; }

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable idleP, walkP, runP;
        float walkLen = 1f, runLen = 1f;
        float shown, phase;
        Transform model, toeL, toeR;
        Vector3 modelBase;
        readonly float[] lift = new float[3];
        readonly float[] weight = new float[3];

        static readonly string[] GunWords = { "gun", "weapon", "rifle", "pistol", "wpn" };

        void Start()
        {
            HideGuns(transform);
            var anim = GetComponentInChildren<Animator>();
            var idleClip = idle != null ? idle : (profile != null ? profile.idle : null);
            if (anim == null || idleClip == null) return;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // a culled Animator isn't posed by the foot sampling (a handful of figures)
            model = anim.transform; modelBase = model.localPosition;
            if (anim.isHuman)
            {
                toeL = anim.GetBoneTransform(HumanBodyBones.LeftToes); toeR = anim.GetBoneTransform(HumanBodyBones.RightToes);
            }
            graph = PlayableGraph.Create("HubFigure");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", anim);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            var w = walk != null ? walk : (profile != null && profile.walkForward != null ? profile.walkForward : idleClip);
            var r = run != null ? run : w;
            idleP = AnimationClipPlayable.Create(graph, idleClip);
            walkP = AnimationClipPlayable.Create(graph, w);
            runP = AnimationClipPlayable.Create(graph, r);
            walkLen = Mathf.Max(.1f, w.length); runLen = Mathf.Max(.1f, r.length);
            graph.Connect(idleP, 0, mixer, 0);
            graph.Connect(walkP, 0, mixer, 1);
            graph.Connect(runP, 0, mixer, 2);
            output.SetSourcePlayable(mixer);
            // the stride clips are driven by the shared phase, not by the graph's clock
            walkP.SetSpeed(0d); runP.SetSpeed(0d);
            Weights(0f);
            graph.Play();
            startFrame = Time.frameCount;
        }

        // an Evaluate before the Animator has run its own first updates does not pose it (it binds then): sampled two frames in
        bool calibrated;
        int startFrame;

        // foot turn: a clip may land a foot turned out further than the idle stance (the HQ's Running lands the left foot ~60° out,
        // the right ~24°). Per clip and foot, the planted heel→toe yaw is compared with the idle's; past FootTurnTolerance the foot is
        // turned back around its shin in LateUpdate, weighted like the clip
        public const float FootTurnTolerance = 10f;
        Transform footL, footR, shinL, shinR;
        readonly float[] turnL = new float[3], turnR = new float[3];
        float turnNowL, turnNowR;

        /// <summary>Each clip alone, sampled over its cycle: how far its lowest toe sits above toeHeight (lift[i], m in the figure's
        /// space), and each planted foot's yaw against the idle's (turnL / turnR, degrees to turn back).</summary>
        void Calibrate()
        {
            var anim = model != null ? model.GetComponent<Animator>() : null;
            if (anim != null && anim.isHuman)
            {
                toeL = anim.GetBoneTransform(HumanBodyBones.LeftToes); toeR = anim.GetBoneTransform(HumanBodyBones.RightToes);
                footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot); footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                shinL = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg); shinR = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            }
            if (toeL == null || toeR == null) return;
            var keep = model.localPosition;
            model.localPosition = modelBase;
            var clips = new[] { idleP, walkP, runP };
            const int N = 24;
            var hL = new float[N]; var hR = new float[N]; var yL = new float[N]; var yR = new float[N];
            float idleL = 0f, idleR = 0f;
            for (int c = 0; c < 3; c++)
            {
                for (int k = 0; k < 3; k++) mixer.SetInputWeight(k, k == c ? 1f : 0f);
                double len = clips[c].GetAnimationClip().length;
                float low = float.MaxValue, lowL = float.MaxValue, lowR = float.MaxValue;
                for (int s = 0; s < N; s++)
                {
                    clips[c].SetTime(len * s / N);
                    graph.Evaluate(0f);
                    hL[s] = Height(toeL); hR[s] = Height(toeR);
                    if (footL != null) { yL[s] = Yaw(footL, toeL); yR[s] = Yaw(footR, toeR); }
                    lowL = Mathf.Min(lowL, hL[s]); lowR = Mathf.Min(lowR, hR[s]);
                }
                low = Mathf.Min(lowL, lowR);
                lift[c] = low - toeHeight;
                clips[c].SetTime(0d);
                if (footL == null) continue;
                // the planted frames: a toe within 2 cm of its lowest
                float sl = 0f, sr = 0f; int nl = 0, nr = 0;
                for (int s = 0; s < N; s++)
                {
                    if (hL[s] < lowL + .02f) { sl += yL[s]; nl++; }
                    if (hR[s] < lowR + .02f) { sr += yR[s]; nr++; }
                }
                float pl = nl > 0 ? sl / nl : 0f, pr = nr > 0 ? sr / nr : 0f;
                if (c == 0) { idleL = pl; idleR = pr; continue; }
                turnL[c] = Mathf.Abs(idleL - pl) > FootTurnTolerance ? idleL - pl : 0f;
                turnR[c] = Mathf.Abs(idleR - pr) > FootTurnTolerance ? idleR - pr : 0f;
            }
            model.localPosition = keep;
        }

        float Height(Transform t) => transform.InverseTransformPoint(t.position).y;

        /// <summary>The heel→toe direction's yaw in the figure's space (degrees, + toward +x).</summary>
        float Yaw(Transform foot, Transform toe)
        {
            var d = transform.InverseTransformDirection(toe.position - foot.position);
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        void LateUpdate()
        {
            // after the graph has posed the bones: turn a foot back around its shin (knee → ankle), as much as its clip needs
            if (!calibrated || footL == null) return;
            turnNowL = weight[1] * turnL[1] + weight[2] * turnL[2];
            turnNowR = weight[1] * turnR[1] + weight[2] * turnR[2];
            if (Mathf.Abs(turnNowL) > .1f) TurnFoot(footL, shinL, turnNowL);
            if (Mathf.Abs(turnNowR) > .1f) TurnFoot(footR, shinR, turnNowR);
        }

        void TurnFoot(Transform foot, Transform shin, float deg)
        {
            var axis = shin != null ? shin.position - foot.position : transform.up;   // up the shin: + turns toward +x like Yaw
            if (axis.sqrMagnitude < 1e-6f) axis = transform.up;
            foot.rotation = Quaternion.AngleAxis(deg, axis.normalized) * foot.rotation;
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
            walkP.SetTime(phase * walkLen);
            runP.SetTime(phase * runLen);
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
