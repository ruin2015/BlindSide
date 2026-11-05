// Two corner brackets (#oc .br: 18×18, 3px border on two sides, ink at opacity .6) at the top-left and bottom-right of this rect, in
// one mesh drawn by the shared KitDeco material. The colour's alpha is blended like the browser over `over` (default paper).
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Corner Brackets")]
    public class CornerBrackets : KitShaderGraphic
    {
        public Color bracketColor = new Color(0x14 / 255f, 0x16 / 255f, 0x1A / 255f, .6f);
        [Tooltip("Arm length of each bracket (canvas units).")]
        public float size = 18f;
        [Tooltip("Line thickness (canvas units).")]
        public float thickness = 3f;
        [Tooltip("Distance of the brackets from the rect's corners (canvas units).")]
        public float inset;

        protected override float Mode => ModeFlat;
        protected override Color ColorA => bracketColor;

        public void Set(Color c, float armSize, float lineThickness)
        {
            bracketColor = c; size = armSize; thickness = lineThickness;
            SetVerticesDirty();
        }

        protected override void Build(VertexHelper vh, Rect r, float s, ref UIVertex v)
        {
            float w = r.width, h = r.height;
            float a = Mathf.Min(size, w * .5f, h * .5f), t = Mathf.Min(thickness, a);
            float l = inset, rr = w - inset, b = inset, top = h - inset;
            // top-left: across the top, then down the left side
            AddQuad(vh, r, 0f, ref v, l, top - t, l + a, top);
            AddQuad(vh, r, 0f, ref v, l, top - a, l + t, top - t);
            // bottom-right: across the bottom, then up the right side
            AddQuad(vh, r, 0f, ref v, rr - a, b, rr, b + t);
            AddQuad(vh, r, 0f, ref v, rr - t, b + t, rr, b + a);
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            over = UITheme.Current.paper;
        }
#endif

        /// <summary>Brackets stretched over `parent`, `inset` from its corners.</summary>
        public static CornerBrackets Create(Transform parent, string name, Color c, float armSize = 18f, float lineThickness = 3f, float insetPx = 0f, Color? overColor = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var cb = go.AddComponent<CornerBrackets>();
            cb.raycastTarget = false;
            cb.bracketColor = c; cb.size = armSize; cb.thickness = lineThickness; cb.inset = insetPx;
            cb.over = overColor ?? UITheme.Current.paper;
            return cb;
        }
    }
}
