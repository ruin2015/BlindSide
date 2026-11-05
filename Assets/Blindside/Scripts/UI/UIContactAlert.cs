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
    //  Contact alert ("CONTACT — 敵と遭遇しました！")
    // ------------------------------------------------------------------
    public class UIContactAlert : MonoBehaviour, ITimedEffect
    {
        public CanvasGroup group; public RectTransform box, mark;
        public TMP_Text subText; public Image[] borderAndMark; public UIGlitchText glitch;
        /// <summary>The ct-mark's "!" — .ct-in.m2 .ct-mark{color:var(--ink)}, white otherwise (optional).</summary>
        public TMP_Text markText;
        /// <summary>Phase 4-4: the diagonal hazard-stripe line along the box's top (a Deco layer), in the alert's colour.</summary>
        public HazardStripe hazard;
        /// <summary>Optional content-width fit (.ct-in is display:flex, sized by its text): box width =
        /// fitLeft + max(fitMin, widest of fitTexts) + fitRight, re-measured on every Play.</summary>
        public TMP_Text[] fitTexts; public float fitLeft, fitRight, fitMin;
        public float duration = 1.1f;
        Vector3 boxBase; bool inited;
        // the box's resting position (Awake in Play; the first Evaluate in an edit-mode preview)
        void Init() { if (inited) return; inited = true; boxBase = box.localPosition; }
        void Awake() { Init(); group.alpha = 0f; }

        public float Duration => duration;

        /// <summary>Colours, sub line and box width for `kind`, without playing (the first part of Play).</summary>
        /// <param name="kind">1 = we spotted first (blue), 2 = mutual (yellow), 3 = ambushed (red)</param>
        public void Setup(int kind, string sub)
        {
            Color c = kind == 1 ? UIPalette.Blue : kind == 2 ? UIPalette.Accent : UIPalette.Foe;
            foreach (var g in borderAndMark) g.color = c;
            if (hazard != null) { hazard.stripeColor = c; hazard.SetVerticesDirty(); }
            if (markText) markText.color = kind == 2 ? UIPalette.Ink : Color.white;
            subText.text = sub;
            if (fitTexts != null && fitTexts.Length > 0)
            {
                float w = fitMin;
                foreach (var ft in fitTexts) if (ft) w = Mathf.Max(w, ft.GetPreferredValues(ft.text).x);
                box.sizeDelta = new Vector2(fitLeft + w + fitRight, box.sizeDelta.y);
                var self = (RectTransform)transform;                    // the root is as wide as the box (the box no longer hangs out of it)
                self.sizeDelta = new Vector2(box.sizeDelta.x, self.sizeDelta.y);
            }
        }

        /// <param name="kind">1 = we spotted first (blue), 2 = mutual (yellow), 3 = ambushed (red)</param>
        public IEnumerator Play(int kind, string sub)
        {
            Setup(kind, sub);
            if (glitch) glitch.Burst(1f);
            StartCoroutine(UIAnim.Shake(mark, 4f, .4f, 2));
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                Pose(t);
                yield return null;
            }
            group.alpha = 0f; box.localPosition = boxBase;
        }

        void Pose(float t)
        {
            Init();
            float p = t / duration;
            if (p < .12f) { float q = UIEase.Tactical(p / .12f); box.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, q); group.alpha = q; box.localPosition = boxBase; }
            else if (p < .85f) { box.localScale = Vector3.one; group.alpha = 1f; }
            else { float q = (p - .85f) / .15f; group.alpha = 1f - q; box.localPosition = boxBase + new Vector3(0, 10f * q, 0); }
        }

        /// <summary>The alert `t` seconds into a play (t ≥ duration: gone, as after Play). The mark's shake is a separate random wobble
        /// and is not posed.</summary>
        public void Evaluate(float t)
        {
            Pose(t);
            if (t >= duration) { group.alpha = 0f; box.localPosition = boxBase; }
        }
    }
}
