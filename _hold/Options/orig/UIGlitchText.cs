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
    //  Glitch text (glA/glB loop, glitch one-shot, glIn/glOut)
    // ------------------------------------------------------------------
    /// <summary>
    /// Adds red/blue copies of a TMP text inside RectMask2D slices that flicker with offsets.
    /// loop = true  → glA/glB (period 2.6s, 1.1s for HEADSHOT)
    /// loop = false → call Burst() for the one-shot CONTACT glitch.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIGlitchText : MonoBehaviour, ITimedEffect
    {
        public TMP_Text source;
        public bool loop = true;
        public float period = 2.6f;
        public float maxOffset = 5f;
        [Tooltip("≥ 0: Evaluate draws its slices and offsets from this seed and t (repeatable editor previews) instead of the shared stream.")]
        public int previewSeed = -1;
        readonly TMP_Text[] copies = new TMP_Text[2];
        readonly RectMask2D[] masks = new RectMask2D[2];
        CanvasGroup cg; float burstUntil = -1f; string last; bool inited;

        // Its own generator: this runs every few frames, and UnityEngine.Random is the stream CombatSystem
        // rolls hits/headshots from — sharing it made combat outcomes depend on frame timing (so a skipped
        // battle cinematic could roll differently from a watched one).
        static readonly System.Random rng = new System.Random();
        static float Rand01() => (float)rng.NextDouble();
        static float RandRange(float min, float max) => min + (max - min) * Rand01();

        void Awake() { Init(); }

        // the red/blue copies (Awake in Play; the first Evaluate in an edit-mode preview, or after a reload lost them)
        void Init()
        {
            if (inited && masks[0] != null) return;
            inited = true;
            if (!source) source = GetComponent<TMP_Text>();
            cg = UIUtil.GetOrAdd<CanvasGroup>(gameObject);
            Color[] cols = { UIPalette.GlitchRed, UIPalette.GlitchBlue };
            for (int i = 0; i < 2; i++)
            {
                var holder = new GameObject("glitch" + i, typeof(RectTransform), typeof(RectMask2D));
                var hrt = (RectTransform)holder.transform; hrt.SetParent(source.transform, false);
                hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.offsetMin = hrt.offsetMax = Vector2.zero;
                masks[i] = holder.GetComponent<RectMask2D>();
                // build a fresh TMP copy (Instantiate would also clone this component → recursion)
                var copyGo = new GameObject("txt", typeof(RectTransform), typeof(TextMeshProUGUI));
                var crt = (RectTransform)copyGo.transform; crt.SetParent(hrt, false);
                crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
                var ct = copyGo.GetComponent<TextMeshProUGUI>();
                ct.font = source.font; ct.fontSize = source.fontSize; ct.fontStyle = source.fontStyle;
                ct.alignment = source.alignment; ct.characterSpacing = source.characterSpacing;
                ct.overflowMode = source.overflowMode; ct.text = source.text;
                ct.color = cols[i]; ct.raycastTarget = false;
                copyGo.AddComponent<LintIgnoreClip>().reason = "the red / blue copy is offset on purpose";
                copies[i] = ct;
                holder.SetActive(false);
            }
        }

        public void Burst(float seconds = 1f) { burstUntil = Time.unscaledTime + seconds; }

        [Tooltip("An outside effect (UIGlitchName) poses the copies through SetSplit; Update then leaves them alone.")]
        [HideInInspector] public bool driven;

        void Update() { if (!driven) Evaluate(Time.unscaledTime); }

        /// <summary>Poses only the RGB split: the red copy px to the left, the blue copy px to the right, no strips (px 0 hides both).</summary>
        public void SetSplit(float px)
        {
            Init();
            if (last != source.text) { last = source.text; foreach (var c in copies) if (c) c.text = last; }
            bool on = !Mathf.Approximately(px, 0f);
            for (int i = 0; i < 2; i++)
            {
                masks[i].gameObject.SetActive(on);
                if (!on) continue;
                masks[i].padding = Vector4.zero;
                copies[i].rectTransform.anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * px, 0f);
            }
        }

        public float Duration => period;

        /// <summary>The glitch at unscaled time `t` (Update passes Time.unscaledTime): the loop window / burst decides whether the
        /// copies show, and the slices re-roll every 4th frame from the shared stream — or, with previewSeed ≥ 0, every call from
        /// that seed and t.</summary>
        public void Evaluate(float t)
        {
            Init();
            System.Random seeded = previewSeed >= 0 ? new System.Random(previewSeed * 7919 + Mathf.RoundToInt(t * 1000f)) : null;
            float R01() => seeded != null ? (float)seeded.NextDouble() : Rand01();
            float RRange(float min, float max) => seeded != null ? min + (max - min) * R01() : RandRange(min, max);

            if (last != source.text) { last = source.text; foreach (var c in copies) if (c) c.text = last; }
            foreach (var c in copies) if (c) c.characterSpacing = source.characterSpacing;
            bool active;
            if (t < burstUntil) active = R01() < .6f;
            else if (loop) { float p = (t % period) / period; active = p > .78f && p < .94f; }
            else active = false;
            float h = ((RectTransform)source.transform).rect.height;
            for (int i = 0; i < 2; i++)
            {
                masks[i].gameObject.SetActive(active);
                if (!active) continue;
                // steps(1): re-roll the slice and offset a few times per second
                if (seeded != null || Time.frameCount % 4 == 0)
                {
                    float top = RRange(0f, .7f) * h, bottom = RRange(0f, .7f) * h;
                    if (top + bottom > h * .85f) bottom = h * .85f - top;
                    masks[i].padding = new Vector4(0, bottom, 0, top);
                    copies[i].rectTransform.anchoredPosition = new Vector2((i == 0 ? -1 : 1) * RRange(2f, maxOffset), 0);
                }
            }
        }

        /// <summary>glIn: skew/flicker in (steps). glInC for corner labels = same without the centre offset.</summary>
        public IEnumerator PlayIn(float dur = .9f)
        {
            var tr = transform; Vector3 b = tr.localPosition;
            (float t, float a, float x, float rot)[] k = { (0f, 0f, 0f, 0f), (.08f, 1f, 0f, 12f), (.14f, .2f, 10f, 0f), (.22f, 1f, -6f, -6f), (.30f, .6f, 0f, 0f), (.38f, 1f, 0f, 0f) };
            float s = 0f; int i = 0;
            while (s < dur)
            {
                s += Time.unscaledDeltaTime; float p = s / dur;
                while (i < k.Length - 1 && p >= k[i + 1].t) i++;
                cg.alpha = k[i].a; tr.localPosition = b + new Vector3(k[i].x, 0, 0); tr.localRotation = Quaternion.Euler(0, 0, k[i].rot);
                yield return null;
            }
            cg.alpha = 1f; tr.localPosition = b; tr.localRotation = Quaternion.identity;
            Burst(.4f);
        }

        /// <summary>glOut: flicker, squash vertically to a line, vanish.</summary>
        public IEnumerator PlayOut(float dur = .7f)
        {
            var tr = transform; Vector3 bp = tr.localPosition, bs = tr.localScale;
            Burst(dur);
            float s = 0f;
            while (s < dur)
            {
                s += Time.unscaledDeltaTime; float p = s / dur;
                if (p < .2f) { cg.alpha = .3f; tr.localPosition = bp + new Vector3(-12, 0, 0); }
                else if (p < .35f) { cg.alpha = 1f; tr.localPosition = bp + new Vector3(6, 0, 0); }
                else if (p < .55f) { cg.alpha = .2f; tr.localPosition = bp; tr.localScale = new Vector3(bs.x, bs.y * .2f, bs.z); }
                else { cg.alpha = 0f; tr.localScale = new Vector3(bs.x, bs.y * .02f, bs.z); }
                yield return null;
            }
            cg.alpha = 0f; tr.localPosition = bp; tr.localScale = bs;
        }
    }
}
