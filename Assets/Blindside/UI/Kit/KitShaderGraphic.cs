// Base of the kit's drawn layers (HazardStripe, KitSweep, KitShadow, CornerBrackets): a MaskableGraphic drawn by the
// "Blindside/UI/KitDeco" shader with ONE shared material — every parameter travels in the vertices (see the shader header), so the
// layers batch and no material is ever instanced or changed. Colours are given as sRGB values; the shader matches the mockup's sRGB
// blending in a Linear project over `over` (the colour the layer sits on).
// The layer can take the kit's parallelogram itself (slant / slantDirection, same shape as SlantedGraphic: Right = top edge [S, W],
// bottom edge [0, W−S]) — don't put a SlantedGraphic on it: the pattern is laid out on the final positions, which a later shear would
// distort. The canvas needs TexCoord1–3, Normal and Tangent; the layer switches them on for its root canvas. Nothing allocates per
// re-mesh (VertexHelper is pooled, UIVertex is a struct).
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public abstract class KitShaderGraphic : MaskableGraphic
    {
        protected const float ModeFlat = 0f, ModeHazard = 1f, ModeSweep = 2f, ModeShadow = 3f;

        const AdditionalCanvasShaderChannels NeededChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2
            | AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

        [Tooltip("Parallelogram slant S in canvas units (0 = the plain rect).")]
        public float slant;
        public SlantedGraphic.Direction slantDirection = SlantedGraphic.Direction.Right;
        [Tooltip("The colour this layer sits on (for the sRGB alpha match in a Linear project).")]
        public Color over = new Color32(0x14, 0x16, 0x1A, 0xFF);

        // ---- shared material ------------------------------------------------------------------------------------------------
        static Material shared;
        static Material Shared
        {
            get
            {
                if (shared == null)
                    shared = new Material(Shader.Find("Blindside/UI/KitDeco")) { name = "KitDeco (shared)", hideFlags = HideFlags.HideAndDontSave };
                return shared;
            }
        }

        public override Material defaultMaterial => Shared;

        /// <summary>Takes the parallelogram of a SlantedGraphic (amount and direction), e.g. the button the layer sits in.</summary>
        public void MatchShape(SlantedGraphic g)
        {
            if (g == null) { slant = 0f; SetVerticesDirty(); return; }
            slant = g.Amount; slantDirection = g.direction;
            SetVerticesDirty();
        }

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

        // ---- mesh -----------------------------------------------------------------------------------------------------------
        /// <summary>Fills the mesh with AddQuad calls; `proto` already carries mode, colours, `over` and the group-alpha base.</summary>
        protected abstract void Build(VertexHelper vh, Rect r, float s, ref UIVertex proto);

        /// <summary>false = draw nothing (no vertices).</summary>
        protected virtual bool Visible => true;

        protected abstract float Mode { get; }
        protected abstract Color ColorA { get; }
        protected virtual Color ColorB => Color.clear;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f || !Visible) return;
            EnsureChannels();

            Color a = ColorA, b = ColorB;
            var v = UIVertex.simpleVert;
            v.color = color;
            v.uv0 = new Vector4(0f, 0f, Mode, color.a);
            v.uv2 = new Vector4(a.r, a.g, a.b, a.a);
            v.uv3 = new Vector4(b.r, b.g, b.b, b.a);
            v.normal = new Vector3(over.r, over.g, over.b);
            v.tangent = Vector4.zero;
            Build(vh, r, Mathf.Clamp(slant, 0f, r.width * .5f), ref v);
        }

        /// <summary>Left edge offset of the parallelogram at height y (canvas units from the rect's bottom).</summary>
        protected float EdgeOffset(float y, float h, float s)
        {
            float ny = y / h;
            return slantDirection == SlantedGraphic.Direction.Right ? s * ny : s * (1f - ny);
        }

        /// <summary>A quad given in the unsheared shape space (u from the left edge of the parallelogram, y from the bottom), laid on
        /// the parallelogram. uv0.xy = the final local position, tangent.xy = (u, y); tangent.z = `sigma`.</summary>
        protected void AddQuad(VertexHelper vh, Rect r, float s, ref UIVertex v, float u0, float y0, float u1, float y1, float sigma = 0f)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i < 4; i++)
            {
                bool right = i == 2 || i == 3, top = i == 1 || i == 2;
                float u = right ? u1 : u0, y = top ? y1 : y0;
                float x = u + EdgeOffset(y, r.height, s);
                v.position = new Vector3(r.xMin + x, r.yMin + y, 0f);
                v.uv0.x = x; v.uv0.y = y;
                v.tangent = new Vector4(u, y, sigma, 0f);
                vh.AddVert(v);
            }
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }
#endif
    }
}
