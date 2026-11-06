// A figure at the HQ (the player's FLARE or an NPC): the HQ's own idle / walk / run clips (AnimationPack/Neutral Idle, Walking, Running —
// the HQ only; the battle keeps its profile clips), mixed by how fast it moves through a Playables graph of its own (the battle's
// UnitAnimator aims, fires and carries a gun — none of that here):
//   0 … walkSpeed         idle → walk
//   walkSpeed … runSpeed  walk → run (HubPlayer: Shift)
// Feet on the floor: at Start each clip is sampled over its cycle (16 poses) for its lowest toe; the model is shifted so that toe sits
// at toeHeight (the toe bone's height over the sole, measured by the builder in the bind pose) — per clip, blended by the weights.
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

        /// <summary>Each clip alone, sampled over its cycle: how far its lowest toe sits above toeHeight (lift[i], m in the figure's space).</summary>
        void Calibrate()
        {
            var anim = model != null ? model.GetComponent<Animator>() : null;
            if (anim != null && anim.isHuman)
            {
                toeL = anim.GetBoneTransform(HumanBodyBones.LeftToes); toeR = anim.GetBoneTransform(HumanBodyBones.RightToes);
            }
            if (toeL == null || toeR == null) return;
            var keep = model.localPosition;
            model.localPosition = modelBase;
            var clips = new[] { idleP, walkP, runP };
            for (int c = 0; c < 3; c++)
            {
                for (int k = 0; k < 3; k++) mixer.SetInputWeight(k, k == c ? 1f : 0f);
                double len = clips[c].GetAnimationClip().length;
                float low = float.MaxValue;
                for (int s = 0; s < 16; s++)
                {
                    clips[c].SetTime(len * s / 16.0);
                    graph.Evaluate(0f);
                    low = Mathf.Min(low, Mathf.Min(Height(toeL), Height(toeR)));
                }
                lift[c] = low - toeHeight;
                clips[c].SetTime(0d);
            }
            model.localPosition = keep;
        }

        float Height(Transform t) => transform.InverseTransformPoint(t.position).y;

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
