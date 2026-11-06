// The HQ screens' button look (after the reference menus): rectangular tiles with a soft vertical gradient, a blue accent stripe on
// the left that fills in on hover, a hairline at the foot and a big faint icon in the corner (Deco); the back bar (dark, translucent:
// ＜ | 戻る); and the 操作説明 block — key caps (ink plates, the key in white; a drawn mouse for clicks) beside what they do.
// Everything here is built once (sprites are made once and shared); nothing runs per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public static class HubTiles
    {
        public const float AccentW = 7f;

        static Sprite gradient, grid, mouse;

        /// <summary>White at the top to 82 % grey at the foot (tinted by the image colour).</summary>
        public static Sprite Gradient()
        {
            if (gradient != null) return gradient;
            const int H = 64;
            var tex = new Texture2D(4, H, TextureFormat.RGBA32, false) { name = "HubTileGradient", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[4 * H];
            for (int y = 0; y < H; y++)
            {
                byte v = (byte)(255f * Mathf.Lerp(.82f, 1f, y / (H - 1f)));
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px); tex.Apply();
            gradient = Sprite.Create(tex, new Rect(0f, 0f, 4f, H), new Vector2(.5f, .5f));
            return gradient;
        }

        /// <summary>A 32-unit grid cell (one-pixel lines, white): tiled behind pictures.</summary>
        public static Sprite Grid()
        {
            if (grid != null) return grid;
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "HubGrid", wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(x == 0 || y == 0 ? 255 : 0));
            tex.SetPixels32(px); tex.Apply();
            grid = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero);
            return grid;
        }

        /// <summary>A mouse (24 × 34): the body outline, the button split, the left button filled.</summary>
        public static Sprite Mouse()
        {
            if (mouse != null) return mouse;
            const int W = 48, H = 68;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "HubMouseIcon", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            float cx = W * .5f, r = W * .5f - 2f, top = H - 2f - r, bottom = 2f + r;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float fx = x + .5f, fy = y + .5f;
                    // a capsule: distance to the segment (cx, bottom)–(cx, top)
                    float sy = Mathf.Clamp(fy, bottom, top);
                    float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - sy) * (fy - sy));
                    float inside = Mathf.Clamp01(r - d + .5f);
                    float ring = inside * Mathf.Clamp01(d - (r - 5f) + .5f);
                    bool upper = fy > H * .58f;
                    float split = upper && Mathf.Abs(fx - cx) < 2f ? inside : 0f;
                    float rule = Mathf.Abs(fy - H * .58f) < 2f ? inside : 0f;
                    float left = upper && fx < cx - 2f ? inside : 0f;
                    float a = Mathf.Max(Mathf.Max(ring, split), Mathf.Max(rule, left));
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            mouse = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(.5f, .5f));
            return mouse;
        }

        public struct Tile
        {
            public Image face;
            public RectTransform accent;
            public Color baseColor, hoverColor;
        }

        /// <summary>Turns `rt` into a tile: the gradient face (primary: blue, else light; disabled: grey), the left accent stripe (it fills
        /// to `fillTo` on hover when wired into HubHover), a hairline at the foot, and `icon` big and faint in the right corner.</summary>
        public static Tile Make(RectTransform rt, bool primary, bool enabled, string icon = null, float iconSize = 0f)
        {
            var theme = UITheme.Current;
            var t = new Tile();
            Color face = !enabled ? Color.Lerp(theme.paper2, theme.ink, .22f) : primary ? theme.blue : Color.white;
            t.face = HudKit.AddImage(rt, face, raycast: true);
            t.face.sprite = Gradient(); t.face.type = Image.Type.Simple;
            t.baseColor = face;
            t.hoverColor = !enabled ? face : primary ? Color.Lerp(theme.blue, Color.white, .2f) : Color.Lerp(Color.white, theme.blue, .1f);

            if (!string.IsNullOrEmpty(icon))
            {
                float s = iconSize > 0f ? iconSize : Mathf.Min(110f, rt.sizeDelta.y * 1.15f);
                var ic = HudKit.AddText(rt, icon, s, primary && enabled ? new Color(1f, 1f, 1f, .2f) : new Color(theme.ink.r, theme.ink.g, theme.ink.b, .09f),
                    true, TextAlignmentOptions.MidlineRight, "DecoIcon", UITheme.TextRole.Display);
                HudKit.UseSymbolFont(ic);
                ic.raycastTarget = false;
                var ir = ic.rectTransform;
                ir.anchorMin = new Vector2(1f, 0f); ir.anchorMax = new Vector2(1f, 1f); ir.pivot = new Vector2(1f, .5f);
                ir.anchoredPosition = new Vector2(-10f, 0f); ir.sizeDelta = new Vector2(s * 1.2f, 0f);
                var ig = ic.gameObject.AddComponent<LintIgnoreOverlap>();
                ig.reason = "a faint watermark icon under the tile's words";
                ic.gameObject.AddComponent<LintIgnoreClip>().reason = "the watermark bleeds past the tile's edge by design";
            }

            var foot = HudKit.NewRect("DecoFoot", rt);
            foot.anchorMin = new Vector2(0f, 0f); foot.anchorMax = new Vector2(1f, 0f); foot.pivot = new Vector2(.5f, 0f);
            foot.anchoredPosition = Vector2.zero; foot.sizeDelta = new Vector2(0f, 3f);
            HudKit.AddImage(foot, new Color(theme.ink.r, theme.ink.g, theme.ink.b, primary ? .25f : .16f));

            t.accent = HudKit.NewRect("DecoAccent", rt);
            t.accent.anchorMin = new Vector2(0f, 0f); t.accent.anchorMax = new Vector2(0f, 1f); t.accent.pivot = new Vector2(0f, .5f);
            t.accent.anchoredPosition = Vector2.zero; t.accent.sizeDelta = new Vector2(AccentW, 0f);
            var ai = HudKit.AddImage(t.accent, !enabled ? theme.dim : primary ? new Color(1f, 1f, 1f, .45f) : theme.blue);
            if (!primary && enabled) ai.color = new Color(theme.blue.r, theme.blue.g, theme.blue.b, 1f);
            t.accent.SetSiblingIndex(0);
            // the face draws first, then the accent fill (under the words), then the rest
            return t;
        }

        /// <summary>Wires a tile into a HubHover: tint, the accent stripe widening to `fill`.</summary>
        public static void Hover(HubHover hv, Tile t, float fill)
        {
            hv.graphic = t.face; hv.baseColor = t.baseColor; hv.hoverColor = t.hoverColor;
            hv.grow = t.accent; hv.growFrom = AccentW; hv.growTo = fill;
        }

        /// <summary>The back bar: dark and translucent, ＜ in a square, a divider, 「戻る」.</summary>
        public static RectTransform Back(RectTransform parent, UnityEngine.Events.UnityAction act)
        {
            var theme = UITheme.Current;
            var b = HudKit.NewRect("Back", parent);
            b.sizeDelta = new Vector2(176f, 46f);
            var img = HudKit.AddImage(b, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .88f), raycast: true);
            var btn = b.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.targetGraphic = img;
            btn.onClick.AddListener(() => { Sfx.Ui(); act(); });
            b.gameObject.AddComponent<KitBrighten>().image = img;
            var chev = HudKit.AddText(b, "＜", 22f, Color.white, true, TextAlignmentOptions.Center, "Chevron", UITheme.TextRole.Title);
            UILayout.Place(chev.rectTransform, 0f, 0f, 52f, 46f);
            var div = HudKit.NewRect("DecoDivider", b);
            UILayout.Place(div, 52f, 9f, 2f, 28f);
            HudKit.AddImage(div, new Color(1f, 1f, 1f, .3f));
            var t = HudKit.AddText(b, "戻る", 17f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Title);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(t.rectTransform, 70f, 0f, 96f, 46f);
            var line = HudKit.NewRect("DecoLine", b);
            line.anchorMin = new Vector2(0f, 0f); line.anchorMax = new Vector2(1f, 0f); line.pivot = new Vector2(.5f, 0f);
            line.anchoredPosition = Vector2.zero; line.sizeDelta = new Vector2(0f, 3f);
            HudKit.AddImage(line, theme.blue);
            return b;
        }

        /// <summary>The 操作説明 block at (x, y) (top-left, canvas units): the heading, then one row per (keys, what) — keys split by
        /// spaces, each a cap ("MOUSE" draws the mouse) — in `columns` columns 240 wide. `light`: drawn over a light ground (ink caps)
        /// or a dark one (paper caps).</summary>
        public static RectTransform Help(RectTransform parent, float x, float y, bool light, params string[] pairs) => Help(parent, x, y, light, 1, pairs);

        public static RectTransform Help(RectTransform parent, float x, float y, bool light, int columns, params string[] pairs)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("Help", parent);
            const float RowH = 34f, CapH = 28f, ColW = 240f;
            int items = pairs.Length / 2;
            columns = Mathf.Max(1, columns);
            int rows = (items + columns - 1) / columns;
            UILayout.Place(root, x, y, ColW * columns, 30f + rows * RowH);
            Color capColor = light ? theme.ink : theme.paper, capText = light ? Color.white : theme.ink, word = light ? theme.ink : Color.white;
            var head = HudKit.AddText(root, "操作説明", 17f, word, true, TextAlignmentOptions.MidlineLeft, "Head", UITheme.TextRole.Title);
            head.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(head.rectTransform, 0f, 0f, 200f, 24f);
            var rule = HudKit.NewRect("DecoRule", root);
            UILayout.Place(rule, 0f, 25f, 40f, 3f);
            HudKit.AddImage(rule, theme.blue);
            for (int i = 0; i < items; i++)
            {
                float ry = 34f + (i % rows) * RowH;
                float col = (i / rows) * ColW;
                float cx = col;
                var keys = pairs[i * 2].Split(' ');
                foreach (var k in keys)
                {
                    var cap = HudKit.NewRect("Cap", root);
                    if (k == "MOUSE")
                    {
                        UILayout.Place(cap, cx, ry, 22f, CapH);
                        var mi = cap.gameObject.AddComponent<Image>();
                        mi.sprite = Mouse(); mi.color = capColor; mi.preserveAspect = true; mi.raycastTarget = false;
                        cx += 22f + 6f;
                        continue;
                    }
                    float w = Mathf.Max(CapH, 14f + 10f * k.Length);
                    UILayout.Place(cap, cx, ry, w, CapH);
                    HudKit.AddImage(cap, capColor);
                    cap.gameObject.AddComponent<CutCornerGraphic>().Set(5f, CutCornerGraphic.Corners.All);
                    var kt = HudKit.AddText(cap, k, k.Length > 2 ? 12f : 15f, capText, true, TextAlignmentOptions.Center, "Key", UITheme.TextRole.Body);
                    kt.textWrappingMode = TextWrappingModes.NoWrap;
                    HudKit.Stretch(kt.rectTransform);
                    cx += w + 5f;
                }
                var wt = HudKit.AddText(root, pairs[i * 2 + 1], 16f, word, true, TextAlignmentOptions.MidlineLeft, "What", UITheme.TextRole.Body);
                wt.textWrappingMode = TextWrappingModes.NoWrap;
                float wx = cx + 8f;
                UILayout.Place(wt.rectTransform, wx, ry, col + ColW - wx, CapH);
            }
            return root;
        }
    }
}
