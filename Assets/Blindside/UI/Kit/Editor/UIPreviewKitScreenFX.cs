// Render Previews > KitScreenFX (kit_screenfx): the full-screen effect layer (UIScreenFX) over the title and over the HUD, each OFF /
// ON / REDUCE MOTION side by side. ON is posed with ITimedEffect.Evaluate(t) at half the band period (the light band mid-screen) with a
// glitch burst running; the bottom row repeats OFF / ON at 3× (point sampled) around the band so the 3px scanlines and noise show.
// Each cell is its own throw-away stage (background + a top "ScreenFXCanvas" holding the Deco overlay), rendered and then laid out
// here as images. "Blindside > UI > Measure ScreenFX GC" re-poses the layer for 300 frames and logs the bytes allocated (should be 0).
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewKitScreenFX
    {
        public const string Name = "kit_screenfx";

        enum Mode { Off, On, Reduce }

        static readonly Color Label = new Color32(0x9a, 0xa2, 0xaa, 0xff);
        static readonly List<Texture2D> shots = new List<Texture2D>();   // kept until the next Build (the stage renders after Build)

        // a 112×63 px window at the band centre over the squad list (title) / the illustration card (HUD), shown at 3×
        const float ZoomX = 1240f, ZoomY = 414f, ZoomW = 112f, ZoomH = 63f;

        public static void Build(UIPreview.Stage s)
        {
            foreach (var t in shots) if (t != null) Object.DestroyImmediate(t);
            shots.Clear();

            // render every cell first: the sheet canvas must not exist while the cell stages render (they share the camera spot)
            System.Action<UIPreview.Stage>[] backgrounds = { UIPreview.BuildTitleAttack, UIPreview.BuildHudSelected };
            var cells = new Texture2D[2, 3];
            for (int row = 0; row < 2; row++)
                for (int m = 0; m < 3; m++)
                    cells[row, m] = RenderCell(backgrounds[row], (Mode)m);

            var canvas = s.NewCanvas("KitScreenFX", UITheme.CanvasKind.Menu, 0);
            s.PinScale(canvas, 1f);
            var root = canvas.transform;
            string[] rows = { "TITLE", "HUD" }, modes = { "OFF", "ON  t=4.0s (BAND MID) + BURST", "REDUCE MOTION (SCANLINES ONLY)" };

            for (int row = 0; row < 2; row++)
                for (int m = 0; m < 3; m++)
                {
                    float x = 16f + m * 464f, y = 10f + row * 288f;
                    Caption(root, rows[row] + "   " + modes[m], x, y, 448f);
                    Shot(root, cells[row, m], new Rect(0f, 0f, 1f, 1f), x, y + 20f, 448f, 252f, false);
                }

            var uv = new Rect(ZoomX / UIPreview.W, 1f - (ZoomY + ZoomH) / UIPreview.H, ZoomW / UIPreview.W, ZoomH / UIPreview.H);
            string[] zooms = { "TITLE OFF ×3", "TITLE ON ×3", "HUD OFF ×3", "HUD ON ×3" };
            for (int i = 0; i < 4; i++)
            {
                float x = 16f + i * 352f, y = 586f;
                Caption(root, zooms[i], x, y, 336f);
                Shot(root, cells[i / 2, i % 2], uv, x, y + 20f, 336f, 189f, true);
            }
        }

        static Texture2D RenderCell(System.Action<UIPreview.Stage> background, Mode mode)
        {
            bool wasFx = UIMotionSettings.screenFx, wasReduce = UIMotionSettings.reduceMotion;
            UIMotionSettings.screenFx = mode != Mode.Off;
            UIMotionSettings.reduceMotion = mode == Mode.Reduce;
            try
            {
                using (var stage = new UIPreview.Stage(UIPreview.W, UIPreview.H, UIPreview.BackdropColor, ownScene: false))
                {
                    background(stage);
                    var top = stage.NewCanvas(UIScreenFX.CanvasName, UITheme.CanvasKind.Menu, UIScreenFX.SortingOrder);
                    var fx = UIScreenFX.Create(top.transform);
                    stage.Settle();
                    float t = fx.Duration * .5f;
                    fx.PoseBurst(t - .03f, 37f);
                    fx.Evaluate(t);
                    Canvas.ForceUpdateCanvases();
                    var tex = stage.Render();
                    tex.hideFlags = HideFlags.HideAndDontSave;
                    shots.Add(tex);
                    return tex;
                }
            }
            finally { UIMotionSettings.screenFx = wasFx; UIMotionSettings.reduceMotion = wasReduce; }
        }

        static void Caption(Transform parent, string text, float x, float y, float w)
        {
            var t = HudKit.AddText(parent, text, 12, Label, false, TextAlignmentOptions.MidlineLeft, "Caption", UITheme.TextRole.Display);
            UILayout.Place(t.rectTransform, x, y, w, 18f);
        }

        static void Shot(Transform parent, Texture2D source, Rect uv, float x, float y, float w, float h, bool point)
        {
            var tex = source;
            if (point)
            {
                // a point-sampled copy of the window, so 1px lines stay 1px wide at 3×
                int px = Mathf.RoundToInt(uv.x * source.width), py = Mathf.RoundToInt(uv.y * source.height);
                int pw = Mathf.RoundToInt(uv.width * source.width), ph = Mathf.RoundToInt(uv.height * source.height);
                tex = new Texture2D(pw, ph, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels(source.GetPixels(px, py, pw, ph));
                tex.Apply(false, false);
                shots.Add(tex);
                uv = new Rect(0f, 0f, 1f, 1f);
            }
            else
            {
                // a mipmapped copy for the 1/3-size thumbnail: without mips the 3px scanlines alias into broad dark bands
                tex = new Texture2D(source.width, source.height, TextureFormat.RGB24, true) { filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels(source.GetPixels());
                tex.Apply(true, false);
                shots.Add(tex);
            }
            var go = new GameObject("DecoShot", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = tex; raw.uvRect = uv; raw.raycastTarget = false;
            UILayout.Place((RectTransform)go.transform, x, y, w, h);
        }

        [MenuItem("Blindside/UI/Measure ScreenFX GC")]
        public static void MeasureGc()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[ScreenFXGC] edit mode only."); return; }
            using (var stage = new UIPreview.Stage(UIPreview.W, UIPreview.H, UIPreview.BackdropColor))
            {
                var top = stage.NewCanvas(UIScreenFX.CanvasName, UITheme.CanvasKind.Menu, UIScreenFX.SortingOrder);
                var fx = UIScreenFX.Create(top.transform);
                stage.Settle();
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = frame * (1f / 60f);
                    if (frame % 100 == 50) fx.PoseBurst(t, frame);     // a burst now and then
                    fx.Evaluate(t);
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[ScreenFXGC] 300 frames (band, noise, bursts): {bytes} bytes allocated ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
