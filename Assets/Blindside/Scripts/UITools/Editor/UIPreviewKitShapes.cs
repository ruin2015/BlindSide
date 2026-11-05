// Render Previews > KitShapes (kit_shapes): the UI kit's shapes and bars on one menu canvas — slanted shapes (large / card / small,
// both directions), cut-corner panels, square face photos inside SlantedMask frames (a circle must stay a circle), and segmented
// bars at 100%, 56%, 0.2s and 0.6s after a hit, in card and detail sizes, as stat bars, and clipped by a Mask / RectMask2D.
// The hit states are posed with SegmentedBar.Pose + Evaluate(t); nothing runs on a clock.
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewKitShapes
    {
        public const string Name = "kit_shapes";

        static readonly Color Label = new Color32(0x9a, 0xa2, 0xaa, 0xff);
        static readonly Color Crusher = new Color32(0xEA, 0xAF, 0x20, 0xFF);
        static Texture2D faceTexture;

        public static void Build(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("KitShapes", UITheme.CanvasKind.Menu, 0);
            var root = canvas.transform;
            var theme = UITheme.Current;
            var paper = theme.paper; var blue = theme.blue; var acc = theme.acc;

            // ---- slanted shapes ---------------------------------------------------------------------------------------------
            Caption(root, "SLANTEDGRAPHIC   L22 · CARD 16 · CARD 14 · S12 · S10 · REVERSED", 40, 18);
            var R = SlantedGraphic.Direction.Right; var L = SlantedGraphic.Direction.Left;
            Shape(root, "SlantLarge", 40, 42, 160, 56, blue, SlantedGraphic.Size.Large, R, "22", Color.white);
            Shape(root, "SlantCard16", 222, 42, 160, 56, paper, SlantedGraphic.Size.CardMax, R, "16", theme.ink);
            Shape(root, "SlantCard14", 404, 42, 160, 56, paper, SlantedGraphic.Size.CardMin, R, "14", theme.ink);
            Shape(root, "SlantSmall12", 586, 42, 160, 56, acc, SlantedGraphic.Size.SmallMax, R, "12", theme.ink);
            Shape(root, "SlantSmall10", 768, 42, 160, 56, acc, SlantedGraphic.Size.SmallMin, R, "10", theme.ink);
            Shape(root, "SlantLargeRev", 950, 42, 160, 56, blue, SlantedGraphic.Size.Large, L, "22 REV", Color.white);
            Shape(root, "SlantCardRev", 1132, 42, 160, 56, paper, SlantedGraphic.Size.CardMax, L, "16 REV", theme.ink);

            // ---- cut corners -------------------------------------------------------------------------------------------------
            Caption(root, "CUTCORNERGRAPHIC   TR+BL 24 · ALL 16 · TL+BR 24", 40, 124);
            Cut(root, "CutMockup", 40, 148, 200, 110, paper, 24f, CutCornerGraphic.Corners.Mockup, "TR+BL 24", theme.ink);
            Cut(root, "CutAll", 262, 148, 200, 110, blue, 16f, CutCornerGraphic.Corners.All, "ALL 16", Color.white);
            Cut(root, "CutOther", 484, 148, 200, 110, acc, 24f, CutCornerGraphic.Corners.TopLeft | CutCornerGraphic.Corners.BottomRight, "TL+BR 24", theme.ink);

            // ---- slanted mask with a square face (a round mark must stay round) ----------------------------------------------
            Caption(root, "SLANTEDMASK   SQUARE PHOTO 1:1 IN 72×60 · 62×62 · 104×104", 740, 124);
            var tex = FaceTexture();
            SlantedMask.Create(root, "Mask72x60", new Vector2(72, 60), SlantedGraphic.Size.CardMax, tex);
            UILayout.Place((RectTransform)root.Find("Mask72x60"), 740, 152, 72, 60);
            SlantedMask.Create(root, "Mask62x62", new Vector2(62, 62), SlantedGraphic.Size.CardMin, tex);
            UILayout.Place((RectTransform)root.Find("Mask62x62"), 834, 152, 62, 62);
            var big = SlantedMask.Create(root, "Mask104x104", new Vector2(104, 104), SlantedGraphic.Size.Custom, tex);
            big.slant.custom = 20f;
            UILayout.Place((RectTransform)root.Find("Mask104x104"), 918, 152, 104, 104);
            SlantedMask.Create(root, "Mask104x104Rev", new Vector2(104, 104), SlantedGraphic.Size.Custom, tex, SlantedGraphic.Direction.Left).slant.custom = 20f;
            UILayout.Place((RectTransform)root.Find("Mask104x104Rev"), 1044, 152, 104, 104);
            Caption(root, "TOP OF THE FACE STAYS · BOTTOM CROPPED", 740, 262);

            // ---- segmented bars on cards ------------------------------------------------------------------------------------
            Caption(root, "SEGMENTEDBAR   CARD 6/2   100% · 56% · HIT +0.2S · HIT +0.6S", 40, 290);
            var emptyPaper = new Color(0f, 0f, 0f, 0.16f);
            var foe = theme.foe;
            float[,] poses = { { 1f, 1f, 0f }, { 1f, .56f, 99f }, { 1f, .56f, .2f }, { 1f, .56f, .6f } };
            string[] names = { "100%", "56%", "HIT +0.2s", "HIT +0.6s" };
            for (int i = 0; i < 4; i++)
            {
                float x = 40 + i * 272, y = 314;
                var card = Shape(root, "Card" + i, x, y, 250, 62, paper, SlantedGraphic.Size.CardMax, R, null, theme.ink);
                Text(card, "CRUSHER   " + names[i], 12, theme.ink, 30, 6, 200, 16);
                Bar(card, 30, 36, 190, 7, SegmentedBar.Style.Card, Crusher, foe, emptyPaper, poses[i, 0], poses[i, 1], poses[i, 2]);
            }

            Caption(root, "SEGMENTEDBAR   DETAIL 8/3 (SELECTED, DARK CARD)", 40, 392);
            var emptyDark = new Color(1f, 1f, 1f, 0.14f);
            for (int i = 0; i < 4; i++)
            {
                float x = 40 + i * 272, y = 416;
                var card = Shape(root, "DarkCard" + i, x, y, 250, 62, theme.ink, SlantedGraphic.Size.CardMax, R, null, Color.white);
                Text(card, "CRUSHER   " + names[i], 12, Color.white, 30, 6, 200, 16);
                Bar(card, 30, 34, 190, 10, SegmentedBar.Style.Detail, Crusher, foe, emptyDark, poses[i, 0], poses[i, 1], poses[i, 2]);
            }

            // ---- stat bars (HP ÷ 140, MOV ÷ 10) and clipping -----------------------------------------------------------------
            Caption(root, "STAT BARS  HP ÷ 140 · MOV ÷ 10     CLIPPED BY MASK (STENCIL) · RECTMASK2D", 40, 494);
            var stat = Cut(root, "DecoStatPanel", 40, 518, 300, 84, theme.ink2, 0f, CutCornerGraphic.Corners.None, null, Color.white);
            Text(stat, "HP  118/140", 12, Color.white, 14, 12, 90, 16);
            Bar(stat, 110, 17, 88, 7, SegmentedBar.Style.Card, blue, foe, emptyDark, 1f, 1f, 0f).SetStat(118f, 140f);
            Text(stat, "MOV  6/10", 12, Color.white, 14, 46, 90, 16);
            Bar(stat, 110, 51, 88, 7, SegmentedBar.Style.Card, blue, foe, emptyDark, 1f, 1f, 0f).SetStat(6f, 10f);

            var maskFrame = SlantedMask.Create(root, "MaskedBar", new Vector2(226, 40), SlantedGraphic.Size.SmallMax, null);
            UILayout.Place((RectTransform)maskFrame.transform, 380, 520, 226, 40);
            Object.DestroyImmediate(maskFrame.photo.gameObject);
            var inner = Bar(maskFrame.transform, 0, 14, 300, 12, SegmentedBar.Style.Detail, Crusher, foe, emptyDark, 1f, .72f, 99f);
            inner.gameObject.AddComponent<LintIgnoreClip>().reason = "wider than the mask on purpose: the stencil clips it";
            Text(root, "MASK (STENCIL) 226", 12, Label, 620, 532, 150, 16);

            var clipBox = HudKit.NewRect("ClipBox", root);
            UILayout.Place(clipBox, 380, 580, 160, 22);
            clipBox.gameObject.AddComponent<RectMask2D>();
            var clipBar = Bar(clipBox, 0, 8, 300, 7, SegmentedBar.Style.Card, Crusher, foe, emptyDark, 1f, .72f, 99f);
            Text(root, "RECTMASK2D 160", 12, Label, 548, 583, 130, 16);
            clipBar.name = "ClippedBar";
            clipBar.gameObject.AddComponent<LintIgnoreClip>().reason = "wider than the RectMask2D on purpose";
        }

        /// <summary>Blindside > UI > Measure Kit GC: re-poses every bar and re-meshes every kit graphic for 300 frames in the KitShapes
        /// stage and logs the managed bytes allocated (after 30 warm-up frames). Should be 0.</summary>
        [UnityEditor.MenuItem("Blindside/UI/Measure Kit GC")]
        public static void MeasureGc()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitGC] edit mode only."); return; }
            var state = new UIPreview.State { name = Name, label = "KitShapes", build = Build };
            using (var stage = UIPreview.Build(state))
            {
                var canvas = stage.canvases[0];
                var bars = canvas.GetComponentsInChildren<SegmentedBar>();
                var slanted = canvas.GetComponentsInChildren<SlantedGraphic>();
                var cuts = canvas.GetComponentsInChildren<CutCornerGraphic>();
                var dirty = new Graphic[slanted.Length + cuts.Length];
                for (int i = 0; i < slanted.Length; i++) dirty[i] = slanted[i].GetComponent<Graphic>();
                for (int i = 0; i < cuts.Length; i++) dirty[slanted.Length + i] = cuts[i].GetComponent<Graphic>();
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = (frame % 150) * (1f / 100f);
                    for (int i = 0; i < bars.Length; i++) { bars[i].Pose(1f, .56f); bars[i].Evaluate(t); }
                    for (int i = 0; i < dirty.Length; i++) dirty[i].SetVerticesDirty();
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitGC] 300 frames, {bars.Length} bars, {slanted.Length} slanted, {cuts.Length} cut-corner graphics: {bytes} bytes allocated ({bytes / 300f:0.##} per frame)");
            }
        }

        static Texture2D FaceTexture()
        {
            if (faceTexture != null) return faceTexture;
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGB24, false) { name = "KitFacePhoto", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - N / 2f + .5f, dy = y - N / 2f + .5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = Color.Lerp(new Color32(0x3b, 0x6e, 0x9c, 0xff), new Color32(0x1d, 0x2f, 0x45, 0xff), y / (float)N);
                    if (d < 46f) c = new Color32(0xf2, 0xc9, 0x9b, 0xff);                         // a round "head"
                    if (d >= 46f && d < 50f) c = Color.white;                                       // a ring: an oval would show at once
                    if (y > N - 14) c = new Color32(0xe5, 0x36, 0x4d, 0xff);                        // top edge marker (texture y is up)
                    if (x < 14 && y < 14) c = Color.black;                                          // bottom-left corner marker
                    t.SetPixel(x, y, c);
                }
            t.Apply(false, false);
            return faceTexture = t;
        }

        static void Caption(Transform parent, string text, float x, float y)
        {
            var t = HudKit.AddText(parent, text, 12, Label, false, TextAlignmentOptions.MidlineLeft, "Caption", UITheme.TextRole.Display);
            UILayout.Place(t.rectTransform, x, y, 700, 18);
        }

        static void Text(Transform parent, string text, float size, Color color, float x, float y, float w, float h)
        {
            var t = HudKit.AddText(parent, text, size, color, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Display);
            UILayout.Place(t.rectTransform, x, y, w, h);
        }

        static RectTransform Shape(Transform parent, string name, float x, float y, float w, float h, Color color,
            SlantedGraphic.Size size, SlantedGraphic.Direction dir, string label, Color labelColor)
        {
            var rt = HudKit.NewRect(name, parent);
            UILayout.Place(rt, x, y, w, h);
            HudKit.AddImage(rt, color);
            rt.gameObject.AddComponent<SlantedGraphic>().Set(size, dir);
            if (label != null)
            {
                var t = HudKit.AddText(rt, label, 12, labelColor, true, TextAlignmentOptions.Midline, "Label", UITheme.TextRole.Display);
                HudKit.Stretch(t.rectTransform);
            }
            return rt;
        }

        static RectTransform Cut(Transform parent, string name, float x, float y, float w, float h, Color color, float cut,
            CutCornerGraphic.Corners corners, string label, Color labelColor)
        {
            var rt = HudKit.NewRect(name, parent);
            UILayout.Place(rt, x, y, w, h);
            HudKit.AddImage(rt, color);
            if (cut > 0f) rt.gameObject.AddComponent<CutCornerGraphic>().Set(cut, corners);
            if (label != null)
            {
                var t = HudKit.AddText(rt, label, 12, labelColor, true, TextAlignmentOptions.Midline, "Label", UITheme.TextRole.Display);
                HudKit.Stretch(t.rectTransform);
            }
            return rt;
        }

        static SegmentedBar Bar(Transform parent, float x, float y, float w, float h, SegmentedBar.Style style,
            Color fill, Color ghost, Color empty, float from, float to, float t)
        {
            var rt = HudKit.NewRect("Bar", parent);
            UILayout.Place(rt, x, y, w, h);
            var bar = rt.gameObject.AddComponent<SegmentedBar>();
            bar.style = style;
            bar.SetColors(fill, ghost, empty);
            bar.Pose(from, to);
            bar.Evaluate(t);
            return bar;
        }
    }
}
