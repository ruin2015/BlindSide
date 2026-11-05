// "Blindside/UI/Build Font Test Scene" makes Assets/Scenes/FontTest.unity: real strings from the mockup in each role (English names,
// numbers, Japanese descriptions, mixed labels, a kanji-heavy sentence, the minimum size, a user map name the static atlases don't
// hold, and three large overlapping-stroke kanji in both weights). "Blindside/UI/Capture Font Test" renders it at 1440×810 and
// 2560×1440 to Captures/FontTest_*.png and logs any character no font in the text's chain has.
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI.EditorTools
{
    public static class FontTestScene
    {
        const string ScenePath = "Assets/Scenes/FontTest.unity";

        struct Line { public string text; public float size; public UITheme.TextRole role; public Color color; }

        static Line L(string text, float size, UITheme.TextRole role, Color? color = null) => new Line { text = text, size = size, role = role, color = color ?? Color.white };

        static readonly Line[] Lines =
        {
            L("BLINDSIDE", 118, UITheme.TextRole.Display),
            L("ROUND 03 / 15   HP 78/120   12×5   ACC 70%   ▶▶", 30, UITheme.TextRole.Display, new Color32(0xff, 0xc8, 0x1e, 0xff)),
            L("VOLT · MIASMA · WRAITH · OBSIDIAN · ARGUS — OPERATOR FILE // OP-006", 21, UITheme.TextRole.Display),
            L("攻撃 · 作戦開始", 54, UITheme.TextRole.Title),
            L("MAP // 基本邸宅   26×20   EXFIL A", 21, UITheme.TextRole.Display, new Color32(0x1f, 0x96, 0xe8, 0xff)),
            L("人質を救出地点1か所(EXFIL A)まで護送せよ。", 16, UITheme.TextRole.Body),
            L("壁貫通射撃は命中率が低下し、遮蔽物の陰に潜む敵には照準が届かない。制限時間内に脱出地点へ到達せよ。", 13, UITheme.TextRole.Body),
            L("命中 78% · 12×5 · ヘッドショット ×2 · ダメージ×2", 15, UITheme.TextRole.Title),
            L("ユーザー作成マップ「黎明の砦」を読み込みました", 14, UITheme.TextRole.Body),
            L("最小サイズ 11px — マップ選択 ▶ 保存先: C:/Users/プレイヤー/Maps", 11, UITheme.TextRole.Body, new Color(1, 1, 1, .7f)),
        };

        [MenuItem("Blindside/UI/Build Font Test Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UITheme.Current.ink; cam.orthographic = true;
            camGo.tag = "MainCamera";

            var canvasGo = new GameObject("FontTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f;
            UITheme.Current.ApplyTo(canvasGo.GetComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);

            var col = new GameObject("Lines", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var colRt = (RectTransform)col.transform;
            colRt.SetParent(canvasGo.transform, false);
            colRt.anchorMin = Vector2.zero; colRt.anchorMax = Vector2.one; colRt.offsetMin = new Vector2(40, 24); colRt.offsetMax = new Vector2(-40, -16);
            var v = col.GetComponent<VerticalLayoutGroup>();
            v.spacing = 6; v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            foreach (var l in Lines) AddText(colRt, l.text, l.size, l.role, l.color);

            // overlap check: the same three kanji, large, in each weight
            var row = new GameObject("OverlapKanji", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(colRt, false);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 40; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false;
            AddText((RectTransform)row.transform, "護戦邸", 120, UITheme.TextRole.Body, Color.white);
            AddText((RectTransform)row.transform, "護戦邸", 120, UITheme.TextRole.Title, Color.white);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[FontTest] scene saved: " + ScenePath);
        }

        static void AddText(RectTransform parent, string text, float size, UITheme.TextRole role, Color color)
        {
            var go = new GameObject(role + " " + size, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
            UIFonts.Apply(t, role);
        }

        [MenuItem("Blindside/UI/Capture Font Test")]
        public static void Capture()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            var cam = Camera.main;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var report = new System.Text.StringBuilder("[FontTest]\n");
            foreach (var size in new[] { new Vector2Int(1440, 810), new Vector2Int(2560, 1440) })
            {
                var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                cam.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                foreach (var t in canvas.GetComponentsInChildren<TMP_Text>()) t.ForceMeshUpdate(true, true);
                Canvas.ForceUpdateCanvases();
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                string file = $"Captures/FontTest_{size.x}x{size.y}.png";
                Directory.CreateDirectory("Captures");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                report.AppendLine($"{file}: canvas scale {canvas.scaleFactor:0.###}");
                cam.targetTexture = null;
                Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            }
            foreach (var t in canvas.GetComponentsInChildren<TMP_Text>())
                report.AppendLine($"{t.name}: font {t.font.name}, missing [{MissingChars(t)}]");
            Debug.Log(report.ToString());
        }

        /// <summary>Characters of the text that neither its font nor any fallback (its own chain, then TMP Settings) can draw.</summary>
        public static string MissingChars(TMP_Text t)
        {
            var chain = new List<TMP_FontAsset> { t.font };
            for (int i = 0; i < chain.Count; i++)
                if (chain[i].fallbackFontAssetTable != null)
                    foreach (var f in chain[i].fallbackFontAssetTable) if (f != null && !chain.Contains(f)) chain.Add(f);
            if (TMP_Settings.fallbackFontAssets != null) foreach (var f in TMP_Settings.fallbackFontAssets) if (f != null && !chain.Contains(f)) chain.Add(f);
            var sb = new System.Text.StringBuilder();
            string plain = System.Text.RegularExpressions.Regex.Replace(t.text ?? "", "<[^>]*>", "");
            foreach (char c in plain)
            {
                if (char.IsWhiteSpace(c)) continue;
                bool ok = false;
                foreach (var f in chain) if (f.HasCharacter(c, false, true)) { ok = true; break; }
                if (!ok && sb.ToString().IndexOf(c) < 0) sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
