// Fixed-size layout helpers: every box gets its size from UITheme (or the mockup), and a LayoutGroup only stacks its children —
// Control Child Size and Force Expand are off, so a group never shrinks or stretches what it holds. A child's LayoutElement
// carries the same size (min = preferred, no flexible) for anything that asks it.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public static class UILayout
    {
        /// <summary>Sets the box to w×h (sizeDelta) and records the same in its LayoutElement (min = preferred, flexible 0).</summary>
        public static RectTransform Fixed(RectTransform rt, float w, float h)
        {
            rt.sizeDelta = new Vector2(w, h);
            var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = w; le.minHeight = le.preferredHeight = h;
            le.flexibleWidth = le.flexibleHeight = 0f;
            le.ignoreLayout = false;
            return rt;
        }

        public static RectTransform Fixed(Component c, float w, float h) => Fixed((RectTransform)c.transform, w, h);

        /// <summary>Top-left anchored at (x, -y) from the parent's top-left (mockup coordinates: y down), w×h.</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var le = rt.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = true;
            return rt;
        }

        public static RectTransform Place(Component c, float x, float y, float w, float h) => Place((RectTransform)c.transform, x, y, w, h);

        /// <summary>Anchored to a corner/edge of the parent: `anchor` (0..1 each axis) with the pivot on the same point, offset by
        /// (x, y) in canvas units (y up), size w×h.</summary>
        public static RectTransform Anchor(RectTransform rt, Vector2 anchor, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A group that only stacks: no child size control, no force expand.</summary>
        public static T Free<T>(T g) where T : HorizontalOrVerticalLayoutGroup
        {
            g.childControlWidth = g.childControlHeight = false;
            g.childForceExpandWidth = g.childForceExpandHeight = false;
            g.childScaleWidth = g.childScaleHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup Row(Component c, float spacing, RectOffset pad = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            var g = c.GetComponent<HorizontalLayoutGroup>() ?? c.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing; g.padding = pad ?? new RectOffset(); g.childAlignment = align;
            return Free(g);
        }

        public static VerticalLayoutGroup Column(Component c, float spacing, RectOffset pad = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            var g = c.GetComponent<VerticalLayoutGroup>() ?? c.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing; g.padding = pad ?? new RectOffset(); g.childAlignment = align;
            return Free(g);
        }

        /// <summary>CSS line-height (a multiple of the font size) as TMP line spacing over the font's own line height. For top-aligned
        /// text the first line is also moved to where CSS puts it: CSS centres the glyph box (ascent + descent) in the line box, TMP hangs
        /// the line from the ascender, so a line-height below the font's content height drew the text lower than the mockup.
        /// Call after the font and size are set.</summary>
        public static void LineHeight(TMP_Text t, float css)
        {
            if (t == null || t.font == null) return;
            var fi = t.font.faceInfo;
            if (fi.pointSize <= 0) return;
            float k = 1f / fi.pointSize;
            t.lineSpacing = (css - fi.lineHeight * k) * 100f;
            if (t.verticalAlignment == VerticalAlignmentOptions.Top)
            {
                float content = (fi.ascentLine - fi.descentLine) * k;      // descentLine is negative
                var m = t.margin; m.y = -(content - css) / 2f * t.fontSize; t.margin = m;
            }
        }

        /// <summary>Removes a ContentSizeFitter (it would resize the box from its content).</summary>
        public static void NoFitter(Component c)
        {
            var f = c.GetComponent<ContentSizeFitter>();
            if (f != null) Object.DestroyImmediate(f);
        }
    }
}
