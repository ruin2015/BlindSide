// Orchestrates the beats of playIntro() in blindside_source/05_ui_input_intro.js (~line 237) that
// aren't pure keyframe curves: the two caption() calls, the operation-title glitch title, and the
// rappel() landing fx (~line 270) at the end of the rope-descent. The continuous camera orbit,
// helicopter flight path and rope/unit-descent motion are baked into the Timeline's animation
// tracks instead (see Intro_Timeline), since those are just eased position lerps in the source.
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using TMPro;
using Blindside.UI;
using Blindside.Rendering;

namespace Blindside.Cinematic
{
    public class IntroSequence : MonoBehaviour
    {
        [Header("Title (chTitle + UIGlitchText)")]
        public CanvasGroup titleGroup;
        public const string TitleTexture = "Intro/IntroOperationTitle";
        public TMP_Text titleText;
        public UIGlitchText titleGlitch;

        [Header("Captions")]
        public IntroCaption caption;

        [Header("Rappel landing fx")]
        public Transform unitDropPoint;

        // Ported from the Fade AnimationTrack in Intro_Timeline.playable, driven directly instead of
        // through Timeline: a PlayableDirector AnimationTrack bound to a CanvasGroup's m_Alpha doesn't
        // actually apply in this project (confirmed live — Transform tracks like Camera_Motion/Heli_
        // Motion animate fine through the same director, but CanvasGroup generic bindings are silently
        // no-ops even with an Animator on the target), so the black start-of-intro overlay never
        // cleared and the whole game looked stuck on a black screen. TitleOn/TitleOff above dodge this
        // same trap already by setting titleGroup.alpha directly from a Signal instead of a track.
        [Header("Fade (black overlay covering the scene before/after the intro)")]
        public CanvasGroup fadeGroup;
        public float fadeInSeconds = 1f;
        PlayableDirector director;
        Coroutine fadeRoutine;

        void Awake()
        {
            director = GetComponent<PlayableDirector>();
            if (director != null) director.played += OnDirectorPlayed;
        }

        void OnDestroy()
        {
            if (director != null) director.played -= OnDirectorPlayed;
        }

        void OnDirectorPlayed(PlayableDirector d) => BeginFadeIn();

        /// <summary>Fades the black overlay out to reveal the scene — call whenever the intro (re)starts.</summary>
        public void BeginFadeIn()
        {
            if (fadeGroup == null) return;
            fadeGroup.alpha = 1f;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeTo(0f, fadeInSeconds));
        }

        IEnumerator FadeTo(float target, float duration)
        {
            float start = fadeGroup.alpha, t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            fadeGroup.alpha = target;
            fadeRoutine = null;
        }

        /// <summary>Source: `ttl.classList.add('on'); sfx('alert')` right before the opening fade-in.</summary>
        public void TitleOn()
        {
            // The title is the picture glitch now (BlindsideTitle over Resources/Intro/IntroOperationTitle.png — the same words in
            // BSTactical); it runs its own ~3 s including the exit, so TitleOff doesn't cut it. The TMP title stays in the scene, hidden;
            // without the picture (or to go back) the old gather (IntroTitleAssemble) below runs as before.
            var titleTex = Resources.Load<Texture2D>(TitleTexture);
            if (titleTex != null)
            {
                if (titleGroup) titleGroup.alpha = 0f;
                var bt = GetComponent<BlindsideTitle>();
                if (bt == null) bt = gameObject.AddComponent<BlindsideTitle>();
                bt.playOnEnable = false;
                bt.titleTexture = titleTex;
                bt.Play();
                return;
            }
            if (titleGroup) titleGroup.alpha = 1f;
            // the letters gather in from wide out (IntroTitleAssemble) instead of the plain RGB-split burst; the loop glitch on the
            // same text is held off so it doesn't fight it
            if (titleText)
            {
                if (titleGlitch) { titleGlitch.driven = true; titleGlitch.SetSplit(0f); }
                var asm = titleText.GetComponent<IntroTitleAssemble>();
                if (asm == null) { asm = titleText.gameObject.AddComponent<IntroTitleAssemble>(); asm.source = titleText; }
                asm.Play();
            }
            else if (titleGlitch) titleGlitch.Burst(1.5f);
        }

        /// <summary>Source: `ttl.classList.remove('on'); ttl.classList.add('off')` before caption 1.</summary>
        public void TitleOff()
        {
            if (titleGroup) titleGroup.alpha = 0f;
        }

        // The source's two bottom-left captions (TARGET // 目標建物, INSERTION // 降下) gave way to the spoken lines at the bottom
        // centre (IntroSubtitle, timed by IntroCameraRig). Their signals still fire on the Timeline; they just show nothing now.
        public void ShowCaptionTarget() { }

        public void ShowCaptionInsertion() { }

        public void HideCaption()
        {
            if (caption) caption.Hide();
        }

        /// <summary>Port of the rappel() touchdown: `fxRing(x,y,0xd6c9a8,500,.9)`.</summary>
        public void RappelLandingFx()
        {
            // the intro's rappellers play their own touchdowns on the roof
            if (IntroCameraRig.OwnsLandingFx) return;
            // on the ground under the drop point (the figure waits up on the rope)
            if (unitDropPoint) CombatFx.Ring(new Vector3(unitDropPoint.position.x, .05f, unitDropPoint.position.z), new Color32(0xd6, 0xc9, 0xa8, 0xff), 0.5f, 0.9f);
        }
    }
}
