// Blindside > Rendering > Apply Toon Shader (all materials) / Revert Toon Shader.
// Every opaque or cut-out URP/Lit (or Simple Lit) material under the game's art folders gets the toon shader (Blindside/Toon): the
// same-named properties carry over (base map and colour, normal map, emission, smoothness, cutoff), the keywords follow, and the
// outline width is set by what the material is for (characters thicker, guns medium, the map and the town thin). Materials still
// embedded in a model file are extracted next to it first (Materials/), so they can be changed. Transparent materials stay URP/Lit.
// What each material was is written to ToonConversionRecord.json beside this script, and Revert puts every one back.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Blindside.Rendering.EditorTools
{
    public static class ToonConverter
    {
        static readonly string[] Folders = { "Assets/3DModels", "Assets/Blindside/Art", "Assets/GunModels", "Assets/russian_buildings", "Assets/Materials" };
        static readonly string[] LitShaders = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit" };
        const string RecordPath = "Assets/Blindside/Scripts/Rendering/Editor/ToonConversionRecord.json";

        [System.Serializable] class Entry { public string path; public string shader; public string[] keywords; }
        [System.Serializable] class Record { public List<Entry> entries = new List<Entry>(); }

        [MenuItem("Blindside/Rendering/Apply Toon Shader (all materials)")]
        public static void ApplyMenu()
        {
            int n = Apply(out string report);
            Debug.Log($"[Toon] converted {n} materials\n{report}");
        }

        public static int Apply(out string report)
        {
            var toon = Shader.Find(ToonShader.Name);
            if (toon == null) { report = "Blindside/Toon is missing"; return 0; }
            ExtractEmbedded();
            var rec = Load();
            var done = new HashSet<string>();
            foreach (var e in rec.entries) done.Add(e.path);
            int n = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", Folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null || System.Array.IndexOf(LitShaders, m.shader.name) < 0) continue;
                if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > .5f) continue;   // (transparent: stays Lit)
                if (!done.Contains(path)) rec.entries.Add(new Entry { path = path, shader = m.shader.name, keywords = m.shaderKeywords });
                Convert(m, toon, WidthFor(path), IsCharacter(path) ? CharacterSoftness : .05f);
                EditorUtility.SetDirty(m);
                n++;
                sb.AppendLine(path);
            }
            AssetDatabase.SaveAssets();
            Save(rec);
            report = sb.ToString();
            return n;
        }

        // the characters' meshes are low-poly (faceted normals): a softer step keeps the shadow from breaking into triangles
        const float CharacterSoftness = .16f;
        static bool IsCharacter(string path) => path.StartsWith("Assets/3DModels") || path.Contains("/Characters/") || path.Contains("AnimationSample");

        static float WidthFor(string path)
        {
            if (path.StartsWith("Assets/3DModels") || path.Contains("/Characters/") || path.Contains("AnimationSample")) return 1.6f;
            if (path.StartsWith("Assets/GunModels")) return 1.1f;
            return .9f;
        }

        static void Convert(Material m, Shader toon, float width, float softness)
        {
            bool clip = m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > .5f || m.IsKeywordEnabled("_ALPHATEST_ON");
            bool emission = m.IsKeywordEnabled("_EMISSION");
            bool normal = m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null;
            int queue = m.renderQueue;
            m.shader = toon;
            m.shaderKeywords = new string[0];
            if (clip) m.EnableKeyword("_ALPHATEST_ON");
            if (emission) m.EnableKeyword("_EMISSION");
            if (normal) m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 0f); m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_OutlineWidth", width);
            m.SetFloat("_ShadowSoftness", softness);
            m.renderQueue = clip ? 2450 : (queue >= 2000 && queue < 2500 ? queue : 2000);
        }

        [MenuItem("Blindside/Rendering/Revert Toon Shader")]
        public static void RevertMenu() => Debug.Log($"[Toon] reverted {Revert()} materials");

        public static int Revert()
        {
            var rec = Load();
            int n = 0;
            foreach (var e in rec.entries)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(e.path);
                var sh = Shader.Find(e.shader);
                if (m == null || sh == null) continue;
                m.shader = sh;
                m.shaderKeywords = e.keywords ?? new string[0];
                EditorUtility.SetDirty(m);
                n++;
            }
            AssetDatabase.SaveAssets();
            rec.entries.Clear();
            Save(rec);
            return n;
        }

        /// <summary>Materials living inside a model file (not editable) are written out to Materials/ beside it; the model then uses those.</summary>
        static void ExtractEmbedded()
        {
            var models = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", Folders)) models.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var path in models)
            {
                var dir = Path.GetDirectoryName(path).Replace('\\', '/') + "/Materials";
                bool any = false;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(o is Material mat) || !AssetDatabase.IsSubAsset(mat)) continue;
                    if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), "Materials");
                    string to = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{Sanitize(mat.name)}.mat");
                    string err = AssetDatabase.ExtractAsset(mat, to);
                    if (string.IsNullOrEmpty(err)) any = true; else Debug.LogWarning($"[Toon] extract {path}/{mat.name}: {err}");
                }
                if (any) AssetDatabase.WriteImportSettingsIfDirty(path);
                if (any) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        static Record Load()
        {
            if (!File.Exists(RecordPath)) return new Record();
            return JsonUtility.FromJson<Record>(File.ReadAllText(RecordPath)) ?? new Record();
        }

        static void Save(Record r)
        {
            File.WriteAllText(RecordPath, JsonUtility.ToJson(r, true));
            AssetDatabase.ImportAsset(RecordPath);
        }
    }
}
