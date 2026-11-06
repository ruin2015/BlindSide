// A figure at the HQ (the player's FLARE or an NPC): idle (the operator's UnitAnimationProfile) and the HQ's own walk / run clips
// (AnimationPack/Walking, Running — the HQ only; the battle keeps its profile clips), mixed by how fast it moves through a Playables
// graph of its own (the battle's UnitAnimator aims, fires and carries a gun — none of that here):
//   0 … walkSpeed      idle → walk
//   walkSpeed … runSpeed  walk → run (HubPlayer: Shift)
// Each clip plays at speed / its reference speed (clamped), so the feet keep up. Guns are never shown at the HQ: renderers under a
// gun-like node are switched off. Speed comes from the owner (HubPlayer) or stays 0 (an NPC idles). No allocation per frame.
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Blindside.Rendering;

namespace Blindside.Hub
{
    public class HubFigure : MonoBehaviour
    {
        [Tooltip("Idle (the operator's UnitAnimationProfile); also the fallback walk when `walk` is empty.")]
        public UnitAnimationProfile profile;
        [Tooltip("The HQ's walk loop (in place).")]
        public AnimationClip walk;
        [Tooltip("The HQ's run loop (in place).")]
        public AnimationClip run;
        [Tooltip("Ground speed (m/s) of a full walk / a full run (HubPlayer's walkSpeed / runSpeed).")]
        public float walkSpeed = 1.7f, runSpeed = 4.2f;
        [Tooltip("The speed (m/s) each clip's steps are made for: it plays at speed / this.")]
        public float walkClipSpeed = 1.6f, runClipSpeed = 4f;

        /// <summary>Ground speed now (m/s); the owner sets it every frame.</summary>
        public float Speed { get; set; }

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable walkP, runP;
        float shown;

        static readonly string[] GunWords = { "gun", "weapon", "rifle", "pistol", "wpn" };

        void Start()
        {
            HideGuns(transform);
            var anim = GetComponentInChildren<Animator>();
            if (anim == null || profile == null || profile.idle == null) return;
            anim.applyRootMotion = false;
            graph = PlayableGraph.Create("HubFigure");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", anim);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            graph.Connect(AnimationClipPlayable.Create(graph, profile.idle), 0, mixer, 0);
            var w = walk != null ? walk : (profile.walkForward != null ? profile.walkForward : profile.idle);
            var r = run != null ? run : w;
            walkP = AnimationClipPlayable.Create(graph, w);
            runP = AnimationClipPlayable.Create(graph, r);
            graph.Connect(walkP, 0, mixer, 1);
            graph.Connect(runP, 0, mixer, 2);
            Weights(0f);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        void Weights(float s)
        {
            float wIn = Mathf.Clamp01(s / Mathf.Max(.1f, walkSpeed));
            float rIn = Mathf.Clamp01((s - walkSpeed) / Mathf.Max(.1f, runSpeed - walkSpeed));
            mixer.SetInputWeight(0, 1f - wIn);
            mixer.SetInputWeight(1, wIn * (1f - rIn));
            mixer.SetInputWeight(2, wIn * rIn);
        }

        void Update()
        {
            if (!graph.IsValid()) return;
            // ease the shown speed (≈ .15 s) so a start, a stop and Shift blend instead of popping
            shown = Mathf.Lerp(shown, Speed, 1f - Mathf.Exp(-14f * Time.deltaTime));
            if (Speed <= .01f && shown < .02f) shown = 0f;
            Weights(shown);
            float sw = Mathf.Max(shown, walkSpeed * .6f), sr = Mathf.Max(shown, walkSpeed + (runSpeed - walkSpeed) * .5f);
            walkP.SetSpeed(Mathf.Clamp(sw / Mathf.Max(.1f, walkClipSpeed), .7f, 1.4f));
            runP.SetSpeed(Mathf.Clamp(sr / Mathf.Max(.1f, runClipSpeed), .7f, 1.3f));
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
