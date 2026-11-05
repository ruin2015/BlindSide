// A panel's drop shadow (#up / #spot box-shadow: 0 12px 30px rgba(0,0,0,.5)) as a gaussian-blurred parallelogram drawn by the shared
// KitDeco material, blended to the mockup's darkness over `over` (what lies under the panel). It is a sibling placed just before the
// panel (Behind) with the same rect, so it renders underneath; name it Deco…. The quad grows by 3 sigma on every side.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Shadow")]
    public class KitShadow : KitShaderGraphic
    {
        public Color shadowColor = new Color(0f, 0f, 0f, .5f);
        [Tooltip("CSS offset-y (canvas units, down).")]
        public float offsetY = 12f;
        [Tooltip("CSS blur radius (canvas units); sigma = blur ÷ 2.")]
        public float blur = 30f;

        protected override float Mode => ModeShadow;
        protected override Color ColorA => shadowColor;

        protected override void Build(VertexHelper vh, Rect r, float s, ref UIVertex v)
        {
            float sigma = Mathf.Max(blur * .5f, .01f), grow = sigma * 3f;
            float w = r.width - s, h = r.height;
            // moved down by offsetY, the shape's left edge sits where the panel's edge is offsetY higher: a constant u shift
            float d = EdgeOffset(offsetY, h, s) - EdgeOffset(0f, h, s);
            v.uv1 = new Vector4(d, -offsetY, w + d, h - offsetY);
            AddQuad(vh, r, s, ref v, d - grow, -offsetY - grow, w + d + grow, h - offsetY + grow, sigma);
        }

        /// <summary>A shadow sibling right before `target` (same anchors, position, size), taking its SlantedGraphic shape if any.</summary>
        public static KitShadow Behind(RectTransform target, Color over, float alpha = .5f, float offset = 12f, float blurPx = 30f)
        {
            var go = new GameObject("DecoShadow" + target.name, typeof(RectTransform));
            go.layer = target.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(target.parent, false);
            rt.SetSiblingIndex(target.GetSiblingIndex());
            rt.anchorMin = target.anchorMin; rt.anchorMax = target.anchorMax; rt.pivot = target.pivot;
            rt.anchoredPosition = target.anchoredPosition; rt.sizeDelta = target.sizeDelta;
            var sh = go.AddComponent<KitShadow>();
            sh.raycastTarget = false;
            sh.over = over; sh.shadowColor = new Color(0f, 0f, 0f, alpha); sh.offsetY = offset; sh.blur = blurPx;
            sh.MatchShape(target.GetComponent<SlantedGraphic>());
            return sh;
        }
    }
}
