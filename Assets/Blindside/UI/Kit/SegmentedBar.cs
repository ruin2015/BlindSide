// The HP / stat bar (.seg: repeating-linear-gradient(90deg, c 0 6px, transparent 6px 8px)) as one MaskableGraphic drawn by the
// "Blindside/UI/Segmented" shader. Every bar uses the same shared material (no per-bar instance, so bars batch); the per-bar values
// travel in the mesh vertices (colour = fill colour, uv0..uv2 and normal — see the shader header). Stencil masks and RectMask2D
// clip it like any Graphic. The canvas needs TexCoord1, TexCoord2 and Normal in "Additional Shader Channels"; the bar switches
// them on for its root canvas.
//
// Damage: the fill follows in FillTime (.35s), the ghost bar starts hpGhostDelay (.45s) later and takes hpGhost (.8s), both with
// UIEase.Tactical. The animation is ITimedEffect.Evaluate(t); Update only advances an unscaled clock and calls it, so a preview can
// pose it with Pose(from, to) + Evaluate(t). Nothing is allocated per frame (VertexHelper is pooled, UIVertex is a struct).
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Segmented Bar")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class SegmentedBar : MaskableGraphic, ITimedEffect
    {
        public enum Style { Card, Detail, Custom }

        public const float FillTime = 0.35f;
        const AdditionalCanvasShaderChannels NeededChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.Normal;

        [Tooltip("Card: segment 6 / gap 2 (roster card).  Detail: 8 / 3 (unit panel).")]
        public Style style = Style.Card;
        public float customSegment = 6f, customGap = 2f;
        [Tooltip("The ghost bar: what was just lost, shown until it catches up.")]
        public Color ghostColor = new Color32(0xE5, 0x36, 0x4D, 0xFF);
        [Tooltip("Unfilled cells.")]
        public Color emptyColor = new Color(0f, 0f, 0f, 0.16f);
        [SerializeField, Range(0f, 1f)] float fill = 1f;
        [SerializeField, Range(0f, 1f)] float ghost = 1f;

        float fromFill, fromGhost, target, clock;
        bool running;

        // ---- shared material -----------------------------------------------------------------------------------------------
        static Material shared;
        static Material Shared
        {
            get
            {
                if (shared == null)
                {
                    var shader = Shader.Find("Blindside/UI/Segmented");
                    shared = new Material(shader) { name = "Segmented (shared)", hideFlags = HideFlags.HideAndDontSave };
                }
                return shared;
            }
        }

        public override Material defaultMaterial => Shared;

        // ---- values ---------------------------------------------------------------------------------------------------------
        public float Fill => fill;
        public float Ghost => ghost;
        public float SegmentWidth => style == Style.Card ? 6f : style == Style.Detail ? 8f : customSegment;
        public float SegmentGap => style == Style.Card ? 2f : style == Style.Detail ? 3f : customGap;

        /// <summary>Colours in one call: fill, ghost, empty cells.</summary>
        public void SetColors(Color fillColor, Color ghostCol, Color emptyCol)
        {
            color = fillColor; ghostColor = ghostCol; emptyColor = emptyCol;
            SetVerticesDirty();
        }

        /// <summary>Jumps to `ratio` (0..1): fill and ghost together, no animation.</summary>
        public void SetImmediate(float ratio)
        {
            running = false;
            fill = ghost = target = Mathf.Clamp01(ratio);
            SetVerticesDirty();
        }

        /// <summary>A stat bar: value ÷ max (HP ÷ 140, MOV ÷ 10).</summary>
        public void SetStat(float value, float max) => SetImmediate(max > 0f ? value / max : 0f);

        /// <summary>Follows to `ratio`: the fill after FillTime, the ghost later (damage); a rise just fills.</summary>
        public void SetTarget(float ratio)
        {
            if (!isActiveAndEnabled) { SetImmediate(ratio); return; }
            fromFill = fill; fromGhost = ghost; target = Mathf.Clamp01(ratio);
            clock = 0f; running = true;
            Evaluate(0f);
        }

        /// <summary>Sets up a run from `from` to `to` without starting the clock (for Evaluate(t) previews).</summary>
        public void Pose(float from, float to)
        {
            running = false;
            fromFill = fromGhost = Mathf.Clamp01(from); target = Mathf.Clamp01(to);
        }

        // ---- ITimedEffect ---------------------------------------------------------------------------------------------------
        float GhostDelay => UITheme.Current.motion.hpGhostDelay > 0f ? UITheme.Current.motion.hpGhostDelay : 0.45f;
        float GhostTime => UITheme.Current.motion.hpGhost > 0f ? UITheme.Current.motion.hpGhost : 0.8f;

        public float Duration => Mathf.Max(FillTime, GhostDelay + GhostTime);

        public void Evaluate(float t)
        {
            float f, g;
            if (t >= Duration) { f = g = target; }
            else
            {
                f = Mathf.LerpUnclamped(fromFill, target, UIEase.Tactical(t / FillTime));
                g = Mathf.LerpUnclamped(fromGhost, target, UIEase.Tactical((t - GhostDelay) / GhostTime));
                if (g < f) g = f;                  // the ghost is never below the fill (a heal fills at once)
            }
            if (Mathf.Approximately(f, fill) && Mathf.Approximately(g, ghost)) return;
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

        // ---- mesh -----------------------------------------------------------------------------------------------------------
        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureChannels();
        }

        void EnsureChannels()
        {
            var c = canvas;
            if (c == null) return;
            c = c.rootCanvas;
            if ((c.additionalShaderChannels & NeededChannels) != NeededChannels) c.additionalShaderChannels |= NeededChannels;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;
            EnsureChannels();

            // the vertex colour is converted by the canvas in a Linear project; the colours carried in the other channels are not
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color ghostOut = linear ? ghostColor.linear : ghostColor, emptyOut = linear ? emptyColor.linear : emptyColor;

            var v = UIVertex.simpleVert;
            v.color = color;
            v.normal = new Vector3(emptyOut.r, emptyOut.g, emptyOut.b);
            v.tangent = new Vector4(1f, 0f, 0f, -1f);
            v.uv1 = new Vector4(fill, ghost, SegmentWidth, SegmentGap);
            v.uv2 = new Vector4(ghostOut.r, ghostOut.g, ghostOut.b, ghostColor.a);
            v.uv3 = Vector4.zero;

            for (int i = 0; i < 4; i++)
            {
                bool right = i == 2 || i == 3, top = i == 1 || i == 2;
                v.position = new Vector3(right ? r.xMax : r.xMin, top ? r.yMax : r.yMin, 0f);
                v.uv0 = new Vector4(right ? 1f : 0f, r.width, color.a, emptyColor.a);
                vh.AddVert(v);
            }
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            color = UITheme.Current.acc;
            raycastTarget = false;
        }
#endif
    }
}
