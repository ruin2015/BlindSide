// Split out of BlindsideUIAnim.cs: Unity only serializes one MonoScript "main class" per file
// reliably, so every MonoBehaviour that gets AddComponent'd (and, above all, ever saved into a
// prefab) needs its own file, or its component reference silently rots into "Missing (Mono Script)"
// on the next reload. Body copied verbatim from the original file.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    // ------------------------------------------------------------------
    //  Phase banner (bannerIn + textSlide)
    // ------------------------------------------------------------------
    public class UIPhaseBanner : MonoBehaviour, ITimedEffect
    {
        public RectTransform strip;              // full-width dark strip
        public CanvasGroup group;
        public TMP_Text mainText, subText, krText;
        public Image[] colorBlocks;              // small colored blocks left/right of the text
        public float duration = 1.7f;
        float baseSpacing; Vector3 mainBase; bool inited;

        // the resting text spacing / position the slide-in returns to (Awake in Play; the first Evaluate in an edit-mode preview)
        void Init() { if (inited) return; inited = true; baseSpacing = mainText.characterSpacing; mainBase = mainText.transform.localPosition; }
        void Awake() { Init(); group.alpha = 0f; }

        public float Duration => duration;

        /// <summary>The banner's texts and colour, without playing it.</summary>
        public void SetContent(string main, string kr, string sub, Color color)
        {
            mainText.text = main; krText.text = kr; subText.text = sub; krText.color = color;
            foreach (var b in colorBlocks) b.color = color;
        }

        public void Play(string main, string kr, string sub, Color color)
        {
            SetContent(main, kr, sub, color);
            StopAllCoroutines(); StartCoroutine(Run());
        }
        public IEnumerator PlayRoutine(string main, string kr, string sub, Color color) { Play(main, kr, sub, color); yield return new WaitForSecondsRealtime(duration); }

        IEnumerator Run()
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                Pose(t);
                yield return null;
            }
            group.alpha = 0f;
        }

        void Pose(float t)
        {
            Init();
            float p = t / duration;
            // bannerIn: open from centre (0–14%), hold (–82%), fold vertically out
            if (p < .14f) { float q = UIEase.Tactical(p / .14f); strip.localScale = new Vector3(q, 1, 1); group.alpha = q; }
            else if (p < .82f) { strip.localScale = Vector3.one; group.alpha = 1f; }
            else { float q = (p - .82f) / .18f; strip.localScale = new Vector3(1, Mathf.Lerp(1f, .2f, q), 1); group.alpha = 1f - q; }
            // textSlide: from -60px with wide letter spacing, settled at 20%
            float ts = UIEase.Tactical(Mathf.Clamp01(p / .2f));
            mainText.transform.localPosition = mainBase + new Vector3(Mathf.Lerp(-60f, 0f, ts), 0, 0);
            mainText.characterSpacing = Mathf.Lerp(baseSpacing + 30f, baseSpacing, ts);
        }

        /// <summary>The banner `t` seconds into a play (t ≥ duration: gone, as after Run).</summary>
        public void Evaluate(float t)
        {
            Pose(t);
            if (t >= duration) group.alpha = 0f;
        }
    }
}
