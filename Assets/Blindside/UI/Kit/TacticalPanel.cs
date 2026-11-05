// A flat panel in the kit's parallelogram: fill, an optional border and an optional left colour band (#cmd .h border-left, the command
// button's number box), in ONE mesh on the default UI material. The shape comes from the SlantedGraphic on the same object (it also
// answers raycasts with the parallelogram); without one the panel is a plain rect.
// SlantedGraphic shears every vertex after this mesh is built, so the pieces are emitted at the pre-images of where they must land:
// borders keep their width, and a Clip band keeps its vertical right edge (CSS: a box clipped by the parallelogram) while a Follow
// band runs along the slanted left edge. Fill colour = the Graphic's colour (Preset picks it from UITheme).
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Tactical Panel")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class TacticalPanel : MaskableGraphic
    {
        public enum Band { None, Follow, Clip }

        [Tooltip("Fill colour preset (Custom keeps the Graphic colour).")]
        public KitColor preset = KitColor.Paper;
        [Tooltip("Border width (canvas units, 0 = none).")]
        public float borderWidth;
        public KitColor borderPreset = KitColor.Ink;
        public Color borderCustom = Color.black;
        [Tooltip("Follow: a band along the slanted left edge.  Clip: the area left of x = bandWidth, cut by the slant.")]
        public Band band = Band.None;
        public float bandWidth = 4f;
        public KitColor bandPreset = KitColor.Blue;
        public Color bandCustom = Color.white;

        SlantedGraphic slanted;

        public Color BorderColor => KitPalette.Get(borderPreset, borderCustom);
        public Color BandColor => KitPalette.Get(bandPreset, bandCustom);

        /// <summary>Fill preset; the fill colour follows UITheme.</summary>
        public void SetPreset(KitColor p)
        {
            preset = p;
            if (p != KitColor.Custom) color = KitPalette.Get(p);
        }

        public void SetBorder(float width, KitColor c, Color custom = default)
        {
            borderWidth = width; borderPreset = c; borderCustom = custom;
            SetVerticesDirty();
        }

        public void SetBand(Band mode, float width, KitColor c, Color custom = default)
        {
            band = mode; bandWidth = width; bandPreset = c; bandCustom = custom;
            SetVerticesDirty();
        }

        /// <summary>Band colour without a re-preset (TacticalButton fades it); a Custom colour.</summary>
        public void SetBandColor(Color c)
        {
            if (bandPreset == KitColor.Custom && bandCustom == c) return;
            bandPreset = KitColor.Custom; bandCustom = c;
            SetVerticesDirty();
        }

        SlantedGraphic Slanted
        {
            get
            {
                if (slanted == null) slanted = GetComponent<SlantedGraphic>();
                return slanted;
            }
        }

        // ---- mesh -----------------------------------------------------------------------------------------------------------
        Rect rect;
        float slant, width;
        SlantedGraphic.Direction dir;

        // left edge of the parallelogram at height y (from the rect's bottom)
        float Edge(float y)
        {
            float ny = Mathf.Clamp01(y / rect.height);
            return dir == SlantedGraphic.Direction.Right ? slant * ny : slant * (1f - ny);
        }

        // the vertex SlantedGraphic will move to local x (from the rect's left) at height y
        Vector3 PreImage(float x, float y)
        {
            float inner = width - slant;
            float nx = inner > .001f ? (x - Edge(y)) / inner : 0f;
            return new Vector3(rect.xMin + nx * width, rect.yMin + y, 0f);
        }

        // a quad with corners bottom-left (bl, y0), top-left (tl, y1), top-right (tr, y1), bottom-right (br, y0) in final local x
        void Quad(VertexHelper vh, Color32 c, float bl, float tl, float tr, float br, float y0, float y1)
        {
            int start = vh.currentVertCount;
            vh.AddVert(PreImage(bl, y0), c, Vector4.zero);
            vh.AddVert(PreImage(tl, y1), c, Vector4.zero);
            vh.AddVert(PreImage(tr, y1), c, Vector4.zero);
            vh.AddVert(PreImage(br, y0), c, Vector4.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            rect = GetPixelAdjustedRect();
            width = rect.width;
            float h = rect.height;
            if (width <= 0f || h <= 0f) return;
            var sg = Slanted;
            bool on = sg != null && sg.isActiveAndEnabled;
            slant = on ? Mathf.Min(sg.Amount, width * .5f) : 0f;
            dir = on ? sg.direction : SlantedGraphic.Direction.Right;
            float inner = width - slant;
            float e0 = Edge(0f), e1 = Edge(h);

            // fill
            Quad(vh, color, e0, e1, e1 + inner, e0 + inner, 0f, h);

            // left band
            if (band != Band.None && bandWidth > 0f)
            {
                Color32 bc = BandColor * new Color(1f, 1f, 1f, color.a);
                if (band == Band.Follow)
                {
                    float bw = Mathf.Min(bandWidth, inner);
                    Quad(vh, bc, e0, e1, e1 + bw, e0 + bw, 0f, h);
                }
                else
                {
                    float x = Mathf.Min(bandWidth, width);
                    if (x >= Mathf.Max(e0, e1)) Quad(vh, bc, e0, e1, x, x, 0f, h);
                    else if (x > Mathf.Min(e0, e1))
                    {
                        // narrower than the slant: only the part below / above the height where the edge reaches x (a triangle)
                        float yc = slant > 0f ? h * (dir == SlantedGraphic.Direction.Right ? x / slant : 1f - x / slant) : 0f;
                        if (dir == SlantedGraphic.Direction.Right) Quad(vh, bc, e0, x, x, x, 0f, yc);
                        else Quad(vh, bc, x, e1, x, x, yc, h);
                    }
                }
            }

            // border (horizontal width along the slanted sides)
            if (borderWidth > 0f)
            {
                float b = Mathf.Min(borderWidth, h * .5f, inner * .5f);
                Color32 c = BorderColor * new Color(1f, 1f, 1f, color.a);
                float eb = Edge(b), et = Edge(h - b);
                Quad(vh, c, et, e1, e1 + inner, et + inner, h - b, h);                         // top   (y h−b..h)
                Quad(vh, c, e0, eb, eb + inner, e0 + inner, 0f, b);                            // bottom (y 0..b)
                Quad(vh, c, eb, et, et + b, eb + b, b, h - b);                                 // left
                Quad(vh, c, eb + inner - b, et + inner - b, et + inner, eb + inner, b, h - b); // right
            }
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
            SetPreset(preset);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (preset != KitColor.Custom) color = KitPalette.Get(preset);
        }
#endif

        /// <summary>A panel `name` under `parent` with its SlantedGraphic (place it with UILayout.Place).</summary>
        public static TacticalPanel Create(Transform parent, string name, KitColor fill, SlantedGraphic.Size slantSize = SlantedGraphic.Size.Large,
            float customSlant = 0f, SlantedGraphic.Direction direction = SlantedGraphic.Direction.Right)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<TacticalPanel>();
            p.raycastTarget = false;
            p.SetPreset(fill);
            go.AddComponent<SlantedGraphic>().Set(slantSize, direction, customSlant);
            return p;
        }
    }
}
