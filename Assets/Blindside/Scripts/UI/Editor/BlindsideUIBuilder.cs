// =====================================================================
//  BLINDSIDE UI porting — Step 2 build tool.
//  Menu: Blindside > UI > Step 2 - Canvas & Common Sprites
//  Generates the two shared sprites (cut-corner panel, hazard stripe tile)
//  from the CSS spec and creates the root Screen Space Overlay canvas.
//  Editor-only: lives under an "Editor" folder so it never ships in builds.
// =====================================================================
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Blindside.UI.EditorTools
{
    public static class BlindsideUIBuilder
    {
        const string ArtDir = "Assets/Blindside/Art/UI";
        const string PrefabDir = "Assets/Blindside/Prefabs/UI";
        const string PanelTexPath = ArtDir + "/panel_cut_corner.png";
        const string StripeTexPath = ArtDir + "/hazard_stripe.png";
        const string CanvasPrefabPath = PrefabDir + "/BlindsideCanvas.prefab";

        // matches CSS: clip-path polygon(0 0, calc(100% - 14px) 0, 100% 14px, 100% 100%, 0 100%)
        // handoff asks for a 12px cut specifically.
        const int PanelSize = 64;
        const int PanelCut = 12;
        const int PanelBorderTR = PanelCut + 4; // a little slack around the diagonal so it never stretches

        // matches CSS: repeating-linear-gradient(135deg, var(--acc) 0 4px, var(--ink) 4px 8px)
        const int StripePeriod = 16;   // 2x the CSS 8px period, for a crisper diagonal at UI scale
        const int StripeTiles = 4;     // 4 periods -> 64px texture, seamless when tiled/scrolled

        [MenuItem("Blindside/UI/Step 2 - Canvas & Common Sprites")]
        public static void BuildStep2()
        {
            EnsureFolders();

            WritePanelSprite();
            WriteStripeTexture();
            AssetDatabase.Refresh();
            ConfigurePanelSprite();
            ConfigureStripeTexture();

            var canvasGO = CreateOrGetCanvas();
            EnsureEventSystem();

            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGO, CanvasPrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"[Blindside] Step 2 complete.\n- Canvas prefab: {CanvasPrefabPath} (also live in the open scene)\n- Panel sprite: {PanelTexPath}\n- Stripe texture: {StripeTexPath}");
            EditorUtility.DisplayDialog("Blindside UI — Step 2", "キャンバスと共通スプライトを作成しました。\n\n- BlindsideCanvas (シーン + プレハブ)\n- panel_cut_corner (9スライス、右上 12px カット)\n- hazard_stripe (135° acc/ink、タイル用)\n\n詳しいパスはConsoleを確認してください。", "OK");
        }

        static void EnsureFolders()
        {
            EnsureFolder("Assets/Blindside", "Art");
            EnsureFolder("Assets/Blindside/Art", "UI");
            EnsureFolder("Assets/Blindside", "Prefabs");
            EnsureFolder("Assets/Blindside/Prefabs", "UI");
        }

        static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        // -----------------------------------------------------------------
        //  Panel sprite: white plate, top-right corner cut PanelCut px.
        // -----------------------------------------------------------------
        static void WritePanelSprite()
        {
            int w = PanelSize, h = PanelSize;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            Color32 white = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);
            for (int y = 0; y < h; y++)
            {
                // texture row 0 = bottom in Unity; convert to CSS's top-down y for the clip-path test.
                int cssY = h - 1 - y;
                for (int x = 0; x < w; x++)
                {
                    bool cut = (x - cssY) > (w - PanelCut);
                    px[y * w + x] = cut ? clear : white;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(ToAbsolute(PanelTexPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(PanelTexPath);
        }

        static void ConfigurePanelSprite()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(PanelTexPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(4, 4, PanelBorderTR, PanelBorderTR); // left, bottom, right, top
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // -----------------------------------------------------------------
        //  Hazard stripe: 135° repeating acc/ink bands, tileable for UIStripeScroll.
        // -----------------------------------------------------------------
        static void WriteStripeTexture()
        {
            int size = StripePeriod * StripeTiles;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            Color32 acc = ToColor32(UIPalette.Accent);
            Color32 ink = ToColor32(UIPalette.Ink);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int band = ((x + y) % StripePeriod + StripePeriod) % StripePeriod;
                    px[y * size + x] = band < StripePeriod / 2 ? acc : ink;
                }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(ToAbsolute(StripeTexPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(StripeTexPath);
        }

        static void ConfigureStripeTexture()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(StripeTexPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        static Color32 ToColor32(Color c) => new Color32(
            (byte)Mathf.RoundToInt(c.r * 255f),
            (byte)Mathf.RoundToInt(c.g * 255f),
            (byte)Mathf.RoundToInt(c.b * 255f),
            255);

        static string ToAbsolute(string assetPath) =>
            Path.Combine(Directory.GetCurrentDirectory(), assetPath).Replace('/', Path.DirectorySeparatorChar);

        // -----------------------------------------------------------------
        //  Canvas: Screen Space Overlay, Scale With Screen Size at UITheme.menuReference, match 0.5.
        // -----------------------------------------------------------------
        static GameObject CreateOrGetCanvas()
        {
            var existing = GameObject.Find("BlindsideCanvas");
            var go = existing != null ? existing : new GameObject("BlindsideCanvas");

            if (!go.TryGetComponent<Canvas>(out var canvas)) canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (!go.TryGetComponent<CanvasScaler>(out var scaler)) scaler = go.AddComponent<CanvasScaler>();
            UITheme.Current.ApplyTo(scaler, UITheme.CanvasKind.Menu);
            if (!go.TryGetComponent<UIThemeCanvas>(out var themed)) themed = go.AddComponent<UIThemeCanvas>();
            themed.kind = UITheme.CanvasKind.Menu;

            if (!go.TryGetComponent<GraphicRaycaster>(out _)) go.AddComponent<GraphicRaycaster>();

            return go;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // Project uses the new Input System exclusively (Project Settings > activeInputHandler = 1),
            // so the legacy StandaloneInputModule would throw at runtime.
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
