// Render Previews > KitGlitch (kit_glitch): one sheet of the glitch presets on the CRUSHER sample photo, each posed with
// ITimedEffect.Evaluate(t): CardEnter at 0.1 / 0.21 / 0.35 / 0.6 / 0.8s, Select at 0.07 / 0.14s, Hit at 0.1s, CardEnter with
// UIMotionSettings.reduceMotion on, and the name text's glitch at 0.2s. The card shake Evaluate returns is applied to each card here.
// "Blindside > UI > Measure Glitch GC" re-poses all of them for 300 frames and logs the managed bytes allocated (should be 0).
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewKitGlitch
    {
        public const string Name = "kit_glitch";
        const string PhotoPath = "Assets/Blindside/Art/Operators/CRUSHER.png";

        static readonly Color Label = new Color32(0x9a, 0xa2, 0xaa, 0xff);

        struct Cell
        {
            public string label; public UIGlitchImage.Preset preset; public float t; public bool reduce, name;
            public Cell(string label, UIGlitchImage.Preset preset, float t, bool reduce = false, bool name = false)
            { this.label = label; this.preset = preset; this.t = t; this.reduce = reduce; this.name = name; }
        }

        static readonly Cell[] Cells =
        {
            new Cell("CARD ENTER  t=0.10", UIGlitchImage.Preset.CardEnter, .10f),
            new Cell("CARD ENTER  t=0.21", UIGlitchImage.Preset.CardEnter, .21f),
            new Cell("CARD ENTER  t=0.35", UIGlitchImage.Preset.CardEnter, .35f),
            new Cell("CARD ENTER  t=0.60", UIGlitchImage.Preset.CardEnter, .60f),
            new Cell("CARD ENTER  t=0.80", UIGlitchImage.Preset.CardEnter, .80f),
            new Cell("SELECT  t=0.07", UIGlitchImage.Preset.Select, .07f),
            new Cell("SELECT  t=0.14", UIGlitchImage.Preset.Select, .14f),
            new Cell("HIT  t=0.10", UIGlitchImage.Preset.Hit, .10f),
            new Cell("REDUCE MOTION  CARD ENTER t=0.10", UIGlitchImage.Preset.CardEnter, .10f, reduce: true),
            new Cell("NAME GLITCH  t=0.20", UIGlitchImage.Preset.CardEnter, .20f, name: true),
        };

        public static void Build(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("KitGlitch", UITheme.CanvasKind.Menu, 0);
            var root = canvas.transform;
            var photo = AssetDatabase.LoadAssetAtPath<Texture2D>(PhotoPath);
            var ink2 = UITheme.Current.ink2;

            for (int i = 0; i < Cells.Length; i++)
            {
                var c = Cells[i];
                int col = i % 5, row = i / 5;
                float x = 40f + col * 276f, y = 30f + row * 322f;

                var cap = HudKit.AddText(root, c.label, 12, Label, false, TextAlignmentOptions.MidlineLeft, "Caption", UITheme.TextRole.Display);
                UILayout.Place(cap.rectTransform, x, y, 256f, 18f);

                var bg = HudKit.NewRect("DecoCell", root);
                UILayout.Place(bg, x, y + 22f, 256f, 256f);
                HudKit.AddImage(bg, ink2);

                if (c.name)
                {
                    var text = HudKit.AddText(bg, "CRUSHER", 40, Color.white, true, TextAlignmentOptions.Midline, "NameText", UITheme.TextRole.Display);
                    UILayout.Place(text.rectTransform, 28f, 88f, 200f, 60f);
                    var g = text.gameObject.AddComponent<UIGlitchName>();
                    g.Evaluate(c.t);
                    continue;
                }

                var rt = HudKit.NewRect("Photo", bg);
                UILayout.Place(rt, 28f, 28f, 200f, 200f);
                var img = rt.gameObject.AddComponent<UIGlitchImage>();
                img.preset = c.preset;
                img.SetTexture(photo);
                bool was = UIMotionSettings.reduceMotion;
                UIMotionSettings.reduceMotion = c.reduce;
                try
                {
                    Vector2 shake = img.Evaluate(c.t);
                    rt.anchoredPosition += shake;           // the caller applies the card shake
                }
                finally { UIMotionSettings.reduceMotion = was; }
            }
        }

        [MenuItem("Blindside/UI/Measure Glitch GC")]
        public static void MeasureGc()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[GlitchGC] edit mode only."); return; }
            var state = new UIPreview.State { name = Name, label = "KitGlitch", build = Build };
            using (var stage = UIPreview.Build(state))
            {
                var canvas = stage.canvases[0];
                var images = canvas.GetComponentsInChildren<UIGlitchImage>();
                var names = canvas.GetComponentsInChildren<UIGlitchName>();
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = (frame % 90) * (1f / 100f);
                    for (int i = 0; i < images.Length; i++) { images[i].Evaluate(t); }
                    for (int i = 0; i < names.Length; i++) names[i].Evaluate(t);
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[GlitchGC] 300 frames, {images.Length} glitch images, {names.Length} name effects: {bytes} bytes allocated ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
