// The hover light that runs across a button (#cmd button::after: linear-gradient(105deg, transparent 30%, rgba(255,255,255,.55) 50%,
// transparent 70%), translateX −120% → 120% in .5s with the tactical ease). Drawn by the shared KitDeco material inside the button's
// parallelogram (MatchShape), blended to the mockup's brightness over `over` (the button colour under it). A decoration: Deco….
// ITimedEffect: Evaluate(t) poses the light `t` seconds into its run; Rest() parks it off the left edge (nothing drawn there).
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Sweep")]
    public class KitSweep : KitShaderGraphic, ITimedEffect
    {
        public const float From = -1.2f, To = 1.2f;

        public Color lightColor = new Color(1f, 1f, 1f, .55f);
        public float duration = .5f;

        float shift = From;    // in box widths

        public float Duration => duration;

        public void Evaluate(float t)
        {
            Set(Mathf.LerpUnclamped(From, To, UIEase.Tactical(t / Mathf.Max(duration, .001f))));
        }

        /// <summary>Back off the left edge at once (CSS: the transition is only on :hover, leaving snaps back).</summary>
        public void Rest() { Set(From); }

        void Set(float k)
        {
            if (k == shift) return;
            bool wasVisible = Visible;
            shift = k;
            if (Visible || wasVisible) SetVerticesDirty();
        }

        // ---- mesh -----------------------------------------------------------------------------------------------------------
        protected override bool Visible => shift > From + .001f && shift < To - .001f && lightColor.a > 0f;
        protected override float Mode => ModeSweep;
        protected override Color ColorA => lightColor;

        protected override void Build(VertexHelper vh, Rect r, float s, ref UIVertex v)
        {
            v.uv1 = new Vector4(shift * r.width, r.width, r.height, 0f);
            AddQuad(vh, r, s, ref v, 0f, 0f, r.width - s, r.height);
        }
    }
}
