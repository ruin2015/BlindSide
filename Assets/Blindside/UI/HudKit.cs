// Small uGUI building blocks shared by the HUD views (UnitPanelView, CommandMenuView, HintView, DialogView, HudWidgets) and by
// PlayerSelectionController / GameplayUIBridge, which used to carry their own copies. Same behaviour as those copies; the only
// addition is DestroySafe, which uses DestroyImmediate outside Play so the editor previews can rebuild a view in edit mode.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public static class HudKit
    {
        // palette (00_markup_and_styles.html :root) beyond UIPalette
        public static readonly Color PaperGrad0 = UIPalette.Hex("#F4F5F6");
        public static readonly Color PaperGrad1 = UIPalette.Hex("#E3E6E9");
        public static readonly Color PaperGrad2 = UIPalette.Hex("#D6D9DD");
        public static readonly Color Paper3 = UIPalette.Hex("#C3C8CD");
        public static readonly Color FoeHead = new Color32(0x3a, 0x11, 0x17, 0xff);   // #unitPanel.foe .up-head

        /// <summary>Animations only run in Play; edit-mode previews show each widget's settled state.</summary>
        public static bool Animate => Application.isPlaying;

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        public static Image AddImage(RectTransform rt, Color c, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = raycast;
            return img;
        }

        /// <param name="role">the font role; null = picked from the text (UIFonts.Auto)</param>
        public static TextMeshProUGUI AddText(Transform parent, string text, float size, Color color, bool bold, TextAlignmentOptions align, string name = "Text", UITheme.TextRole? role = null)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            if (role.HasValue) UIFonts.Apply(t, role.Value); else UIFonts.Auto(t, bold);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        public static LayoutElement LE(Component c, float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1, float minW = -1, float minH = -1)
        {
            var le = UIUtil.GetOrAdd<LayoutElement>(c.gameObject);
            le.preferredWidth = prefW; le.preferredHeight = prefH;
            le.flexibleWidth = flexW; le.flexibleHeight = flexH;
            le.minWidth = minW; le.minHeight = minH;
            return le;
        }

        public static HorizontalLayoutGroup HLG(Component c, float spacing, RectOffset pad, TextAnchor align = TextAnchor.MiddleLeft, bool expandH = false)
        {
            var g = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing; g.padding = pad; g.childAlignment = align;
            g.childControlWidth = true; g.childControlHeight = true;
            g.childForceExpandWidth = false; g.childForceExpandHeight = expandH;
            return g;
        }

        public static VerticalLayoutGroup VLG(Component c, float spacing, RectOffset pad)
        {
            var g = c.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing; g.padding = pad; g.childAlignment = TextAnchor.UpperLeft;
            g.childControlWidth = true; g.childControlHeight = true;
            g.childForceExpandWidth = true; g.childForceExpandHeight = false;
            return g;
        }

        // ---- GameplayUIBridge's rect / image / centred-text helpers ----
        public static RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
            return rt;
        }

        public static Image AddImg(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>A full-size, centred text (GameplayUIBridge.AddText).</summary>
        public static TMP_Text AddCenteredText(Transform parent, string text, float size, Color color, bool bold, UITheme.TextRole? role = null)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.color = color;
            if (role.HasValue) UIFonts.Apply(tmp, role.Value); else UIFonts.Auto(tmp, bold);
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        /// <summary>Destroy in Play, DestroyImmediate in edit mode (where Destroy is not allowed).</summary>
        public static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        /// <summary>Detaches and destroys every child (detached first, so a layout pass this frame no longer sees them).</summary>
        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var ch = parent.GetChild(i);
                ch.SetParent(null, false);
                DestroySafe(ch.gameObject);
            }
        }

        public static Color Grayscale(Color c)
        {
            float l = .2126f * c.r + .7152f * c.g + .0722f * c.b;
            return new Color(l, l, l, c.a);
        }

        // ---- icon glyph font ----
        // The UI fonts have no ✦ ➜ ✛ ⌖ ◉, which the commands and the illustration card use. Build a dynamic font from the OS
        // "Segoe UI Symbol" at runtime (no font file copied into the project) and fall back to the default font for everything
        // else. Null on platforms without it — the glyphs then render as TMP's missing-glyph box.
        static TMP_FontAsset symbolFont;
        static bool symbolFontTried;
        public static TMP_FontAsset SymbolFont
        {
            get
            {
                if (symbolFontTried) return symbolFont;
                symbolFontTried = true;
                try { symbolFont = TMP_FontAsset.CreateFontAsset("Segoe UI Symbol", "Regular", 90); }
                catch (System.Exception) { symbolFont = null; }
                if (symbolFont != null && TMP_Settings.defaultFontAsset != null)
                    symbolFont.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
                return symbolFont;
            }
        }

        public static void UseSymbolFont(TMP_Text t)
        {
            if (t != null && SymbolFont != null) t.font = SymbolFont;
        }

        // ---- generated textures ----
        /// <summary>Tileable 11×11 diagonal hazard stripe: accent/ink bands ≈4px wide at 135°.</summary>
        static Texture2D hazardTex;
        public static Texture2D HazardTexture()
        {
            if (hazardTex != null) return hazardTex;
            const int N = 11;
            hazardTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "HintHazard" };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    px[y * N + x] = ((x + y) % N) < N / 2f ? UIPalette.Accent : UIPalette.Ink;
            hazardTex.SetPixels(px); hazardTex.Apply();
            return hazardTex;
        }

        static Texture2D paperGradTex, hatchTex;
        public static Texture2D PaperGradientTexture()
        {
            if (paperGradTex != null) return paperGradTex;
            // 160deg ≈ top→bottom with a slight left→right lean; sampled along that axis.
            const int N = 64;
            paperGradTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Pp" };
            var px = new Color[N * N];
            float a = 160f * Mathf.Deg2Rad; var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));   // CSS angle: 0deg = up
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // CSS y grows downward; texture y grows upward.
                    var p = new Vector2(x / (N - 1f) - .5f, .5f - y / (N - 1f));
                    float half = Mathf.Abs(.5f * dir.x) + Mathf.Abs(.5f * dir.y);
                    float t = Mathf.Clamp01((Vector2.Dot(p, new Vector2(dir.x, dir.y)) + half) / (2f * half));
                    px[y * N + x] = t < .7f ? Color.Lerp(PaperGrad0, PaperGrad1, t / .7f) : Color.Lerp(PaperGrad1, PaperGrad2, (t - .7f) / .3f);
                }
            paperGradTex.SetPixels(px); paperGradTex.Apply();
            return paperGradTex;
        }

        public static Texture2D HatchTexture()
        {
            if (hatchTex != null) return hatchTex;
            // repeating-linear-gradient(135deg,rgba(0,0,0,.035) 0 1px,transparent 1px 5px)
            const int N = 7;
            hatchTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "Hatch" };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    px[y * N + x] = ((x + y) % N) == 0 ? new Color(0, 0, 0, .035f) : Color.clear;
            hatchTex.SetPixels(px); hatchTex.Apply();
            return hatchTex;
        }
    }
}
