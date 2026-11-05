// The hostage's figure: its own model (HostageLook, Resources/HostageLook.asset) and two clips mixed through the Playables API —
//   captive (no escort), standing still   the sitting clip (looping)
//   moving                                the walking clip, its playback speed following the real speed
//   escorted, standing still              the walking clip held on its passing pose (feet together: standing)
// Facing is the UnitView's (the model root turns with it; no UnitAnimator on the hostage). Runs on game time like the operators'
// animator; no allocation per frame.
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Blindside.Rendering
{
    public class HostageAnimator : MonoBehaviour
    {
        /// <summary>Set every frame by the match: the hostage is being escorted.</summary>
        public bool Escorted { get; set; }

        PlayableGraph graph;
        AnimationMixerPlayable mix;
        AnimationClipPlayable sit, walk;
        HostageLook look;
        Transform body;
        Vector3 lastPos;
        bool hasLast, wasMoving;
        float speed, walkW;

        public void Init(Animator animator, HostageLook look, Transform body)
        {
            this.look = look; this.body = body;
            if (animator == null || look == null || look.sit == null || look.walk == null) { enabled = false; return; }
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("HostageAnimator_" + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Hostage", animator);
            mix = AnimationMixerPlayable.Create(graph, 2);
            sit = AnimationClipPlayable.Create(graph, look.sit);
            walk = AnimationClipPlayable.Create(graph, look.walk);
            graph.Connect(sit, 0, mix, 0);
            graph.Connect(walk, 0, mix, 1);
            output.SetSourcePlayable(mix);
            mix.SetInputWeight(0, 1f); mix.SetInputWeight(1, 0f);
            graph.Play();
        }

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

        void Update()
        {
            if (!graph.IsValid()) return;
            float dt = Time.deltaTime;
            var pos = body != null ? body.position : transform.position;
            if (hasLast && dt > 0f)
            {
                var step = pos - lastPos; step.y = 0f;
                float v = step.magnitude > 1.5f ? 0f : step.magnitude / dt;   // (a jump is not walking)
                speed = Mathf.Lerp(speed, v, 1f - Mathf.Exp(-dt * 10f));
            }
            lastPos = pos; hasLast = true;

            bool moving = speed > .2f;
            float wantWalk = moving || Escorted ? 1f : 0f;
            walkW = Mathf.MoveTowards(walkW, wantWalk, dt / .2f);
            mix.SetInputWeight(0, 1f - walkW);
            mix.SetInputWeight(1, walkW);

            if (moving) walk.SetSpeed(Mathf.Clamp(speed / look.walkClipSpeed, .5f, 2.5f));
            else
            {
                walk.SetSpeed(0f);
                if (wasMoving || walk.GetTime() == 0d) walk.SetTime(look.standPose * look.walk.length);   // stop on the passing pose
            }
            wasMoving = moving;
            Loop(sit); Loop(walk);
        }

        static void Loop(AnimationClipPlayable p)
        {
            float len = p.GetAnimationClip().length;
            if (len > 0f && p.GetTime() > len) p.SetTime(p.GetTime() % len);
        }
    }
}
