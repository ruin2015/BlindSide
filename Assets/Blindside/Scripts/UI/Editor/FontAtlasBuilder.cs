// The UI's TMP font assets (Assets/Blindside/Fonts):
//   BSTactical SDF      static, every glyph of BSTactical-Bold.ttf, sampling 96, atlas 1024          display (var(--fd), var(--fm))
//   NotoJP-Medium SDF   static, ASCII + charset_ja_used.txt + all kana + every Japanese character in Assets; the largest sampling
//                       (96…72) that fits one page of 2048 or 4096                                    body (var(--fk))
//   NotoJP-Black SDF    static, the same characters, sampling 96, atlas 2048 (more pages when needed) title (big Japanese headings)
//   NotoJP-Dynamic SDF  dynamic, multi-atlas, sampling 48, from Noto Sans JP Medium                  last fallback (user map names …)
// Fallbacks: display = BSTactical → Black → Dynamic, body = Medium → Dynamic, title = Black → Dynamic. UITheme's font fields and
// TMP Settings (default font = body, global fallback = Dynamic) are filled from them.
//
// "Blindside/UI/Create Font Assets" builds all four; "Blindside/Rebuild Font Atlases" re-collects the Japanese characters used in
// Assets (prefabs, scenes, assets, scripts, JSON — not _Recovery, blindside_source or TextMesh Pro) and rebuilds Medium and Black.
// A rebuild keeps each asset's main object, material and first atlas texture (their file IDs are what texts reference), so no
// text loses its font.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Blindside.UI;

namespace Blindside.UI.EditorTools
{
    public static class FontAtlasBuilder
    {
        public const string Dir = "Assets/Blindside/Fonts";
        public const string TacticalTtf = Dir + "/BSTactical-Bold.ttf";
        public const string MediumTtf = Dir + "/NotoSansJP-Medium.ttf";
        public const string BlackTtf = Dir + "/NotoSansJP-Black.ttf";
        public const string UsedCharset = Dir + "/Charsets/charset_ja_used.txt";
        public const string TacticalPath = Dir + "/BSTactical SDF.asset";
        public const string MediumPath = Dir + "/NotoJP-Medium SDF.asset";
        public const string BlackPath = Dir + "/NotoJP-Black SDF.asset";
        public const string DynamicPath = Dir + "/NotoJP-Dynamic SDF.asset";

        static readonly string[] Excluded = { "Assets/_Recovery", "Assets/blindside_source", "Assets/TextMesh Pro" };

        public struct Result
        {
            public string asset; public int requested, glyphs, missing, sampling, atlas, pages; public string missingChars, notInFont;
            public override string ToString() => $"{asset}: requested {requested}, glyphs {glyphs}, missing {missing}{(missing > 0 ? " [" + missingChars + "]" : "")}, sampling {sampling}, atlas {atlas}×{atlas} ×{pages} page(s)"
                + (string.IsNullOrEmpty(notInFont) ? "" : $"; not in the font file, left to the fallbacks / OS symbol font: {notInFont.Length} [{notInFont}]");
        }

        /// <summary>The characters of `chars` the font file has no glyph for.</summary>
        static string NotInFont(Font font, string chars)
        {
            if (FontEngine.LoadFontFace(font, 90) != FontEngineError.Success) throw new Exception("can't load " + font.name);
            return new string(chars.Where(c => !char.IsWhiteSpace(c) && !(FontEngine.TryGetGlyphIndex(c, out uint g) && g != 0)).ToArray());
        }

        // ---- menus -------------------------------------------------------------------------------------------------------------

        [MenuItem("Blindside/UI/Create Font Assets")]
        public static void CreateAll()
        {
            var report = new StringBuilder("FONT ASSETS\n");
            var tactical = Build(TacticalPath, TacticalTtf, TacticalChars(), new[] { 96 }, new[] { 1024 }, false, report);
            Rebuild(report);
            var dynamic = BuildDynamic(report);
            var medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MediumPath);
            var black = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BlackPath);
            Wire(tactical, medium, black, dynamic);
            Debug.Log(report.ToString());
        }

        [MenuItem("Blindside/Rebuild Font Atlases")]
        public static void RebuildMenu()
        {
            var report = new StringBuilder("FONT ATLAS REBUILD\n");
            Rebuild(report);
            Debug.Log(report.ToString());
        }

        static void Rebuild(StringBuilder report)
        {
            string chars = JapaneseUiChars(out int fromAssets);
            report.AppendLine($"characters: {chars.Length} (Japanese found in Assets: {fromAssets})");
            // the largest sampling 96…72 that fits one page, the smaller atlas first
            Build(MediumPath, MediumTtf, chars, new[] { 96, 94, 92, 90, 88, 86, 84, 82, 80, 78, 76, 74, 72 }, new[] { 2048, 4096 }, false, report);
            Build(BlackPath, BlackTtf, chars, new[] { 96 }, new[] { 2048 }, true, report);
        }

        // ---- characters --------------------------------------------------------------------------------------------------------

        /// <summary>Every glyph BSTactical-Bold.ttf has (its cmap, U+0020…U+2BFF): ASCII, arrows, · × – — … ■ ▶ and the icon glyphs ✦ ✚ ✛ ✹ ⬢ ▣ ◎ ⌖ ➜.</summary>
        public static string TacticalChars()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(TacticalTtf);
            var sb = new StringBuilder();
            if (FontEngine.LoadFontFace(font, 96) != FontEngineError.Success) throw new Exception("can't load " + TacticalTtf);
            for (int c = 0x20; c <= 0x2BFF; c++)
                if (FontEngine.TryGetGlyphIndex((uint)c, out uint g) && g != 0) sb.Append((char)c);
            return sb.ToString();
        }

        public static bool IsJapanese(char c) =>
            (c >= 0x3000 && c <= 0x30FF) || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0xFF00 && c <= 0xFFEF);

        static readonly Regex Escaped = new Regex(@"\\u([0-9a-fA-F]{4})");

        /// <summary>The Japanese characters in the project's prefabs, scenes, assets, scripts and JSON (YAML \uXXXX escapes decoded).</summary>
        public static SortedSet<char> CollectJapanese()
        {
            var set = new SortedSet<char>();
            foreach (var ext in new[] { "*.prefab", "*.unity", "*.asset", "*.cs", "*.json" })
                foreach (var path in Directory.GetFiles("Assets", ext, SearchOption.AllDirectories))
                {
                    string p = path.Replace('\\', '/');
                    if (Excluded.Any(x => p.StartsWith(x + "/", StringComparison.Ordinal))) continue;
                    if (p.EndsWith(" SDF.asset", StringComparison.Ordinal)) continue;
                    string text = File.ReadAllText(p, Encoding.UTF8);
                    // YAML writes non-ASCII as \uXXXX; in C# the same spelling is a character-range bound in code, not UI text
                    if (!p.EndsWith(".cs", StringComparison.Ordinal))
                        text = Escaped.Replace(text, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
                    foreach (char c in text) if (IsJapanese(c)) set.Add(c);
                }
            return set;
        }

        /// <summary>ASCII, charset_ja_used.txt, every hiragana and katakana, and the Japanese found in Assets.</summary>
        public static string JapaneseUiChars(out int fromAssets)
        {
            var set = new SortedSet<char>();
            for (char c = ' '; c <= '~'; c++) set.Add(c);
            if (File.Exists(UsedCharset))
            {
                foreach (char c in File.ReadAllText(UsedCharset, Encoding.UTF8)) if (!char.IsWhiteSpace(c)) set.Add(c);
            }
            else Debug.LogWarning("[Fonts] " + UsedCharset + " is missing");
            for (char c = (char)0x3041; c <= (char)0x3096; c++) set.Add(c);
            for (char c = (char)0x3099; c <= (char)0x30FF; c++) set.Add(c);
            var found = CollectJapanese();
            fromAssets = found.Count;
            set.UnionWith(found);
            return new string(set.ToArray());
        }

        // ---- building ----------------------------------------------------------------------------------------------------------

        /// <summary>A static asset at `path` from `ttf` holding `chars`: tries each sampling (in order) at each atlas size (in order)
        /// and keeps the first that leaves nothing out on one page — or, with `multiPage`, takes the first sampling/size with as
        /// many pages as it needs.</summary>
        static TMP_FontAsset Build(string path, string ttf, string chars, int[] samplings, int[] atlases, bool multiPage, StringBuilder report)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) throw new Exception("missing font file " + ttf);
            string absent = NotInFont(font, chars);
            chars = new string(chars.Where(c => absent.IndexOf(c) < 0).ToArray());
            TMP_FontAsset built = null; Result r = default;
            foreach (int s in samplings)
            {
                foreach (int a in atlases)
                {
                    var fa = TMP_FontAsset.CreateFontAsset(font, s, Mathf.Max(1, s / 10), GlyphRenderMode.SDFAA, a, a, AtlasPopulationMode.Dynamic, multiPage);
                    fa.TryAddCharacters(chars, out string missing);
                    missing = missing ?? "";
                    var res = new Result
                    {
                        asset = Path.GetFileNameWithoutExtension(path), requested = chars.Length, glyphs = fa.characterTable.Count,
                        missing = missing.Length, missingChars = missing, sampling = s, atlas = a, pages = fa.atlasTextures.Count(t => t != null && t.width > 1),
                        notInFont = absent,
                    };
                    if (missing.Length == 0 || (s == samplings[samplings.Length - 1] && a == atlases[atlases.Length - 1]))
                    {
                        built = fa; r = res; break;
                    }
                    DestroyBuilt(fa);
                }
                if (built != null) break;
            }
            built.atlasPopulationMode = AtlasPopulationMode.Static;
            var saved = Save(built, path);
            report.AppendLine(r.ToString());
            if (r.missing > 0) Debug.LogWarning("[Fonts] " + r);
            return saved;
        }

        static TMP_FontAsset BuildDynamic(StringBuilder report)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(MediumTtf);
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DynamicPath);
            if (existing != null) { report.AppendLine("NotoJP-Dynamic SDF: kept (dynamic)"); return existing; }
            var fa = TMP_FontAsset.CreateFontAsset(font, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            var so = new SerializedObject(fa);
            so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            var saved = Save(fa, DynamicPath);
            report.AppendLine("NotoJP-Dynamic SDF: dynamic, multi-atlas, sampling 48, atlas 1024×1024, cleared on build");
            return saved;
        }

        static void DestroyBuilt(TMP_FontAsset fa)
        {
            foreach (var t in fa.atlasTextures) if (t != null) UnityEngine.Object.DestroyImmediate(t);
            if (fa.material != null) UnityEngine.Object.DestroyImmediate(fa.material);
            UnityEngine.Object.DestroyImmediate(fa);
        }

        /// <summary>Writes a freshly built (in-memory) asset to `path`. When an asset is already there, its main object, material and
        /// atlas textures are reused and refilled, so every reference to them stays valid; its fallbacks are kept.</summary>
        static TMP_FontAsset Save(TMP_FontAsset built, string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var pages = built.atlasTextures.Where(t => t != null && t.width > 1).ToArray();
            if (pages.Length == 0) pages = new[] { built.atlasTextures[0] };
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing == null)
            {
                built.name = name;
                built.atlasTextures = pages;
                AssetDatabase.CreateAsset(built, path);
                for (int i = 0; i < pages.Length; i++) { pages[i].name = name + " Atlas" + (i == 0 ? "" : " " + i); AssetDatabase.AddObjectToAsset(pages[i], built); }
                built.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(built.material, built);
                EditorUtility.SetDirty(built);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path);
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }

            var fallbacks = existing.fallbackFontAssetTable != null ? new List<TMP_FontAsset>(existing.fallbackFontAssetTable) : new List<TMP_FontAsset>();
            var oldMat = existing.material;
            // a copy: CopySerialized below writes the new page references into the asset's own array
            var oldPages = existing.atlasTextures != null ? (Texture2D[])existing.atlasTextures.Clone() : new Texture2D[0];
            var builtMat = built.material;
            EditorUtility.CopySerialized(built, existing);
            existing.name = name;
            existing.fallbackFontAssetTable = fallbacks;

            var keep = new Texture2D[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                if (i < oldPages.Length && oldPages[i] != null)
                {
                    var t = oldPages[i];
                    t.Reinitialize(pages[i].width, pages[i].height, pages[i].format, false);
                    t.SetPixelData(pages[i].GetPixelData<byte>(0), 0);
                    t.Apply(false, false);
                    keep[i] = t;
                    UnityEngine.Object.DestroyImmediate(pages[i]);
                }
                else
                {
                    pages[i].name = name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(pages[i], existing);
                    keep[i] = pages[i];
                }
                EditorUtility.SetDirty(keep[i]);
            }
            for (int i = pages.Length; i < oldPages.Length; i++)
                if (oldPages[i] != null) { AssetDatabase.RemoveObjectFromAsset(oldPages[i]); UnityEngine.Object.DestroyImmediate(oldPages[i], true); }
            existing.atlasTextures = keep;

            if (oldMat != null)
            {
                oldMat.shader = builtMat.shader;
                oldMat.CopyPropertiesFromMaterial(builtMat);
                oldMat.SetTexture(ShaderUtilities.ID_MainTex, keep[0]);
                existing.material = oldMat;
                UnityEngine.Object.DestroyImmediate(builtMat);
                EditorUtility.SetDirty(oldMat);
            }
            else
            {
                builtMat.name = name + " Material";
                AssetDatabase.AddObjectToAsset(builtMat, existing);
            }
            UnityEngine.Object.DestroyImmediate(built);
            // drop any texture / material left in the file that the asset no longer uses
            foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if ((sub is Texture2D && Array.IndexOf(keep, sub) < 0) || (sub is Material && sub != existing.material))
                { AssetDatabase.RemoveObjectFromAsset(sub); UnityEngine.Object.DestroyImmediate(sub, true); }
            existing.ReadFontAssetDefinition();
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        // ---- fallbacks, theme, TMP settings ---------------------------------------------------------------------------------

        static void Wire(TMP_FontAsset tactical, TMP_FontAsset medium, TMP_FontAsset black, TMP_FontAsset dynamic)
        {
            tactical.fallbackFontAssetTable = new List<TMP_FontAsset> { black, dynamic };
            black.fallbackFontAssetTable = new List<TMP_FontAsset> { dynamic };
            medium.fallbackFontAssetTable = new List<TMP_FontAsset> { dynamic };
            dynamic.fallbackFontAssetTable = new List<TMP_FontAsset>();
            foreach (var f in new[] { tactical, medium, black, dynamic }) EditorUtility.SetDirty(f);

            var theme = AssetDatabase.LoadAssetAtPath<UITheme>("Assets/Blindside/Resources/UITheme.asset");
            theme.displayFont = tactical; theme.displayFallbacks = new List<TMP_FontAsset> { black, dynamic };
            theme.bodyFont = medium; theme.bodyFallbacks = new List<TMP_FontAsset> { dynamic };
            theme.titleFont = black; theme.titleFallbacks = new List<TMP_FontAsset> { dynamic };
            EditorUtility.SetDirty(theme);

            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = medium;
            var fb = so.FindProperty("m_fallbackFontAssets");
            fb.arraySize = 1; fb.GetArrayElementAtIndex(0).objectReferenceValue = dynamic;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
