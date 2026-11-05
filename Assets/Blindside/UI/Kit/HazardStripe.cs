// The warning stripes (#hint::before: repeating-linear-gradient(135deg, acc 0 6px, ink 6px 12px); .start::before / #end::before scroll
// with @keyframes stripes): two colours at 135°, scrolling right by one period every `cycle` seconds (1.2–1.6s, ui_spec "경고 줄무늬").
// Drawn by the shared KitDeco material (the offset travels in the vertices). A pure decoration: name its object Deco… (Lint skips it).
// ITimedEffect: Evaluate(t) poses the offset at `t`; Update only advances an unscaled clock when `scroll` is on. No allocation per frame.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Hazard Stripe")]
    public class HazardStripe : KitShaderGraphic, ITimedEffect
    {
        public const float MinCycle = 1.2f, MaxCycle = 1.6f;

        [Tooltip("The first stripe (CSS: the 0..w colour).")]
        public Color stripeColor = new Color32(0xFF, 0xC8, 0x1E, 0xFF);
        [Tooltip("The second stripe. Alpha below 1 lets the layer underneath show (blended like the browser, see `over`).")]
        public Color gapColor = new Color32(0x14, 0x16, 0x1A, 0xFF);
        [Tooltip("Width of the first stripe, measured across the stripes (CSS px).")]
        public float stripeWidth = 6f;
        [Tooltip("One stripe + one gap, measured across the stripes (CSS px).")]
        public float period = 12f;
        [Range(MinCycle, MaxCycle), Tooltip("Seconds per one period of scroll.")]
        public float cycle = 1.4f;
        [Tooltip("Scroll on / off (off: the pattern stays where it is).")]
        public bool scroll = true;

        float clock, offset;

        // ---- ITimedEffect ---------------------------------------------------------------------------------------------------
        public float Duration => Mathf.Clamp(cycle, MinCycle, MaxCycle);

        /// <summary>Poses the stripes `t` seconds into the scroll (one period per Duration).</summary>
        public void Evaluate(float t)
        {
            float o = Mathf.Repeat(t, Duration) / Duration * Mathf.Max(period, .001f);
            if (o == offset) return;
            offset = o;
            SetVerticesDirty();
        }

        public void SetScroll(bool on) { scroll = on; }

        void Update()
        {
            if (!scroll || UIMotionSettings.reduceMotion) return;     // 「モーション軽減」: the stripes stand still
            clock += Time.unscaledDeltaTime;
            if (clock >= Duration) clock -= Duration * Mathf.Floor(clock / Duration);
            Evaluate(clock);
        }

        // ---- mesh -----------------------------------------------------------------------------------------------------------
        protected override float Mode => ModeHazard;
        protected override Color ColorA => stripeColor;
        protected override Color ColorB => gapColor;

        protected override void Build(VertexHelper vh, Rect r, float s, ref UIVertex v)
        {
            v.uv1 = new Vector4(Mathf.Clamp(stripeWidth, 0f, period), Mathf.Max(period, .001f), offset, 0f);
            AddQuad(vh, r, s, ref v, 0f, 0f, r.width - s, r.height);
        }

        /// <summary>A stripe layer named `name` (start it with Deco) stretched over `parent`.</summary>
        public static HazardStripe Create(Transform parent, string name, Color stripe, Color gap, float width = 6f, float periodPx = 12f, float cycleSeconds = 1.4f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var h = go.AddComponent<HazardStripe>();
            h.raycastTarget = false;
            h.stripeColor = stripe; h.gapColor = gap; h.stripeWidth = width; h.period = periodPx; h.cycle = cycleSeconds;
            h.Evaluate(0f);
            return h;
        }
    }
}
