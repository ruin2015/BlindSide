// A figure at the HQ (the player's FLARE or an NPC): its operator model's idle and walk clips (UnitAnimationProfile) mixed by how fast
// it moves, through a Playables graph of its own (the battle's UnitAnimator aims, fires and carries a gun — none of that here). Guns
// are never shown at the HQ: renderers under a gun-like node (gun / weapon / rifle / pistol) are switched off. Speed comes from the
// owner (HubPlayer) or stays 0 (an NPC idles). No allocation per frame.
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Blindside.Rendering;

namespace Blindside.Hub
{
    public class HubFigure : MonoBehaviour
    {
        [Tooltip("Idle / walk clips (the operator's UnitAnimationProfile).")]
        public UnitAnimationProfile profile;
        [Tooltip("Walk speed (m/s) at which the walk clip plays at its own pace.")]
        public float walkSpeedAtClip = 1.6f;

        /// <summary>Ground speed now (m/s); the owner sets it every frame.</summary>
        public float Speed { get; set; }

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable walk;
        float blend;

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
            mixer = AnimationMixerPlayable.Create(graph, 2);
            var idle = AnimationClipPlayable.Create(graph, profile.idle);
            graph.Connect(idle, 0, mixer, 0);
            var wclip = profile.walkForward != null ? profile.walkForward : profile.idle;
            walk = AnimationClipPlayable.Create(graph, wclip);
            graph.Connect(walk, 0, mixer, 1);
            mixer.SetInputWeight(0, 1f); mixer.SetInputWeight(1, 0f);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        void Update()
        {
            if (!graph.IsValid()) return;
            float target = Mathf.Clamp01(Speed / Mathf.Max(.1f, walkSpeedAtClip * .5f));
            blend = Mathf.MoveTowards(blend, target, Time.deltaTime * 6f);
            mixer.SetInputWeight(0, 1f - blend);
            mixer.SetInputWeight(1, blend);
            float rate = Speed > .05f ? Mathf.Clamp(Speed / walkSpeedAtClip, .5f, 2.2f) : 1f;
            walk.SetSpeed(rate * (profile != null ? profile.walkClipSpeed : 1f));
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
