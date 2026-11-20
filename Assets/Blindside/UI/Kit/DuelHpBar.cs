// The duel card's HP bar (the user's design): a row of slanted segments, the HP's in the side's colour, the rest white; a hit leaves the
// lost segments in a ghost colour that catches up later. Each segment is a soft-edged parallelogram (DuelShape.AddPolygon), so the
// slants show no steps. FromRight fills from the right end (the card on the right of the screen).
// Damage: the fill follows in SegmentedBar.FillTime (.35s), the ghost hpGhostDelay later over hpGhost (UITheme.motion), UIEase.Tactical,
// as SegmentedBar does. Evaluate(t) poses it; Update only advances an unscaled clock. Nothing is allocated per frame.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class DuelHpBar : MaskableGraphic, ITimedEffect
    {
        [Tooltip("How many segments the bar is cut into.")] public int segments = 19;
        [Tooltip("The gap between segments (canvas units).")] public float gap = 2.1f;
        [Tooltip("How far each segment's top leans right of its foot (canvas units).")] public float slant = 3.3f;
        public bool fromRight;
        public Color ghostColor = new Color(1f, .6f, .6f, 1f);
        public Color emptyColor = Color.white;
        public float feather = DuelShape.DefaultFeather;
        [SerializeField, Range(0f, 1f)] float fill = 1f;
        [SerializeField, Range(0f, 1f)] float ghost = 1f;

        float fromFill, fromGhost, target, clock;
        bool running;
        static readonly Vector2[] quad = new Vector2[4];

        public float Fill => fill;

        public void SetColors(Color fillColor, Color ghostCol, Color emptyCol)
        {
            color = fillColor; ghostColor = ghostCol; emptyColor = emptyCol;
            SetVerticesDirty();
        }

        public void SetImmediate(float ratio)
        {
            running = false;
            fill = ghost = target = Mathf.Clamp01(ratio);
            SetVerticesDirty();
        }

        /// <summary>Shows `ratio` at once (an entrance's fill); the mesh is remade only when a segment changes.</summary>
        public void ShowRatio(float ratio)
        {
            running = false;
            ratio = Mathf.Clamp01(ratio);
            bool changed = Count(ratio) != Count(fill) || Count(ratio) != Count(ghost);
            fill = ghost = target = ratio;
            if (changed) SetVerticesDirty();
        }

        public void SetTarget(float ratio)
        {
            if (!isActiveAndEnabled) { SetImmediate(ratio); return; }
            fromFill = fill; fromGhost = ghost; target = Mathf.Clamp01(ratio);
            clock = 0f; running = true;
            Evaluate(0f);
        }

        // ---- ITimedEffect ---------------------------------------------------------------------------------------------------
        static float GhostDelay => UITheme.Current.motion.hpGhostDelay > 0f ? UITheme.Current.motion.hpGhostDelay : .45f;
        static float GhostTime => UITheme.Current.motion.hpGhost > 0f ? UITheme.Current.motion.hpGhost : .8f;
        public float Duration => Mathf.Max(SegmentedBar.FillTime, GhostDelay + GhostTime);

        public void Evaluate(float t)
        {
            float f, g;
            if (t >= Duration) { f = g = target; }
            else
            {
                f = Mathf.LerpUnclamped(fromFill, target, UIEase.Tactical(t / SegmentedBar.FillTime));
                g = Mathf.LerpUnclamped(fromGhost, target, UIEase.Tactical((t - GhostDelay) / GhostTime));
                if (g < f) g = f;
            }
            if (Count(f) == Count(fill) && Count(g) == Count(ghost)) { fill = f; ghost = g; return; }   // (no segment changed: no rebuild)
            fill = f; ghost = g;
            SetVerticesDirty();
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        /// <summary>How many segments a ratio lights (any HP left shows at least one).</summary>
        int Count(float ratio) => ratio <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(ratio * segments - .001f), 1, segments);

        // ---- mesh -----------------------------------------------------------------------------------------------------------
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            int n = Mathf.Max(1, segments);
            if (r.width <= 0f || r.height <= 0f) return;
            // the slant eats into the width: the first foot at xMin, the last top at xMax
            float span = r.width - Mathf.Abs(slant);
            float seg = (span - gap * (n - 1)) / n;
            if (seg <= 0f) return;
            int lit = Count(fill), ghosted = Count(ghost);
            Color32 cFill = color, cGhost = ghostColor, cEmpty = emptyColor;
            float footX = r.xMin + (slant < 0f ? -slant : 0f);
            for (int i = 0; i < n; i++)
            {
                int rank = fromRight ? n - 1 - i : i;            // (the fill grows from this end)
                Color32 c = rank < lit ? cFill : rank < ghosted ? cGhost : cEmpty;
                float x0 = footX + i * (seg + gap), x1 = x0 + seg;
                quad[0] = new Vector2(x0, r.yMin); quad[1] = new Vector2(x0 + slant, r.yMax);
                quad[2] = new Vector2(x1 + slant, r.yMax); quad[3] = new Vector2(x1, r.yMin);
                DuelShape.AddPolygon(vh, quad, 4, c, feather);
            }
        }
    }
}
