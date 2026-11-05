// "Blindside/UI/Apply Font Roles": gives every TMP text already saved in the UI prefabs and the open scene its role font
// (UIFonts.RoleFor — display / body / title), clears fake Bold / Italic and Auto Size, and lifts screen text below
// UITheme.minTextSize. Prefabs that hold a Missing Script can't be saved by PrefabUtility, so their YAML is edited instead
// (font, material, style, auto size and size fields only). Writes a list of every text and its role to Captures/font_roles.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blindside.UI.EditorTools
{
    public static class FontRoleApplier
    {
        const string PrefabDir = "Assets/Blindside/Prefabs";
        const string ReportPath = "Captures/font_roles.md";
        const string UguiScriptGuid = "f4688fdb7df04437aeb418b961361dc5";   // TextMeshProUGUI
        const string WorldScriptGuid = "9541d86e2fd84c1d9990edf0852d74ab";  // TextMeshPro

        class Row { public string where, path, text, change; public UITheme.TextRole role; public bool ambiguous; }

        [MenuItem("Blindside/UI/Apply Font Roles")]
        public static void Menu()
        {
            var rows = new List<Row>();
            var yamlEdited = new List<string>();
            foreach (var path in Directory.GetFiles(PrefabDir, "*.prefab", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool missing = asset.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0);
                if (missing) { if (EditYaml(path, rows)) yamlEdited.Add(path); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(t)) continue;   // a nested prefab: done in its own file
                        changed |= ApplyOne(t, path, rows);
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var scene = EditorSceneManager.GetActiveScene();
            bool sceneChanged = false;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t)) continue;
                    sceneChanged |= ApplyOne(t, scene.path, rows);
                }
            if (sceneChanged) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            AssetDatabase.SaveAssets();

            // open each YAML-edited prefab once so an import / load error shows up now
            foreach (var p in yamlEdited) { var r = PrefabUtility.LoadPrefabContents(p); PrefabUtility.UnloadPrefabContents(r); }

            WriteReport(rows, yamlEdited);
            Debug.Log($"[Fonts] roles applied to {rows.Count} texts (display {rows.Count(r => r.role == UITheme.TextRole.Display)}, body {rows.Count(r => r.role == UITheme.TextRole.Body)}, title {rows.Count(r => r.role == UITheme.TextRole.Title)}; ambiguous {rows.Count(r => r.ambiguous)}); YAML-edited: {string.Join(", ", yamlEdited)} → {ReportPath}");
        }

        static bool ApplyOne(TMP_Text t, string where, List<Row> rows)
        {
            bool bold = (t.fontStyle & FontStyles.Bold) != 0;
            float size = t.enableAutoSizing ? t.fontSizeMax : t.fontSize;
            var role = UIFonts.RoleFor(t.text, size, bold);
            var before = (t.font, t.fontStyle, t.enableAutoSizing, t.fontSize);
            var changes = new List<string>();
            if ((t.fontStyle & (FontStyles.Bold | FontStyles.Italic)) != 0) changes.Add("style " + t.fontStyle + "→plain");
            if (t.enableAutoSizing) changes.Add($"auto size off ({t.fontSizeMin}–{t.fontSizeMax} → {t.fontSizeMax})");
            if (t is TextMeshProUGUI && size < UITheme.Current.minTextSize) changes.Add($"size {size}→{UITheme.Current.minTextSize}");
            UIFonts.Apply(t, role);
            bool changed = before != (t.font, t.fontStyle, t.enableAutoSizing, t.fontSize);
            if (changed) EditorUtility.SetDirty(t);
            rows.Add(new Row { where = where, path = UIFonts.PathOf(t.transform), text = t.text, role = role, change = string.Join("; ", changes), ambiguous = string.IsNullOrWhiteSpace(t.text) });
            return changed;
        }

        // ---- YAML (prefabs with a Missing Script) ---------------------------------------------------------------------------

        static readonly Regex Block = new Regex(@"^--- !u!(\d+) &(-?\d+)[^\n]*\n", RegexOptions.Multiline);

        static bool EditYaml(string path, List<Row> rows)
        {
            string yaml = File.ReadAllText(path, Encoding.UTF8);
            var names = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(yaml, @"--- !u!1 &(-?\d+)\nGameObject:.*?\n  m_Name: ([^\n]*)", RegexOptions.Singleline)) names[m.Groups[1].Value] = m.Groups[2].Value;

            var starts = Block.Matches(yaml).Cast<Match>().Select(m => m.Index).ToList();
            starts.Add(yaml.Length);
            var sb = new StringBuilder(yaml.Substring(0, starts[0]));
            bool any = false;
            for (int i = 0; i < starts.Count - 1; i++)
            {
                string b = yaml.Substring(starts[i], starts[i + 1] - starts[i]);
                var script = Regex.Match(b, @"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})");
                bool ugui = script.Success && script.Groups[1].Value == UguiScriptGuid, world = script.Success && script.Groups[1].Value == WorldScriptGuid;
                if (!(ugui || world) || !b.Contains("\n  m_fontAsset:")) { sb.Append(b); continue; }

                string text = YamlText(b);
                int style = Int(b, "m_fontStyle");
                bool auto = Int(b, "m_enableAutoSizing") == 1;
                float size = auto ? Float(b, "m_fontSizeMax") : Float(b, "m_fontSize");
                var role = UIFonts.RoleFor(text, size, (style & 1) != 0);
                var font = UITheme.Current.Font(role);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(font, out string fg, out long fid);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(font.material, out string mg, out long mid);

                var changes = new List<string>();
                string nb = Set(b, "m_fontAsset", $"{{fileID: {fid}, guid: {fg}, type: 2}}");
                nb = Set(nb, "m_sharedMaterial", $"{{fileID: {mid}, guid: {mg}, type: 2}}");
                if ((style & 3) != 0) { nb = Set(nb, "m_fontStyle", (style & ~3).ToString()); changes.Add("style→plain"); }
                if (auto) { nb = Set(nb, "m_enableAutoSizing", "0"); changes.Add("auto size off"); }
                float newSize = size;
                if (ugui && newSize < UITheme.Current.minTextSize) { changes.Add($"size {newSize}→{UITheme.Current.minTextSize}"); newSize = UITheme.Current.minTextSize; }
                if (newSize != Float(b, "m_fontSize") || auto)
                {
                    string s = newSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    nb = Set(nb, "m_fontSize", s); nb = Set(nb, "m_fontSizeBase", s);
                }
                any |= nb != b;
                sb.Append(nb);
                var go = Regex.Match(b, @"m_GameObject: \{fileID: (-?\d+)\}");
                rows.Add(new Row { where = path + " (YAML)", path = go.Success && names.TryGetValue(go.Groups[1].Value, out var n) ? n : "?", text = text, role = role, change = string.Join("; ", changes), ambiguous = string.IsNullOrWhiteSpace(text) });
            }
            if (!any) return false;
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            return true;
        }

        static string Set(string block, string key, string value) =>
            Regex.Replace(block, @"(\n  " + key + @": )[^\n]*", m => m.Groups[1].Value + value);

        static int Int(string block, string key) { var m = Regex.Match(block, @"\n  " + key + @": (-?\d+)"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; }
        static float Float(string block, string key)
        {
            var m = Regex.Match(block, @"\n  " + key + @": ([-0-9.eE]+)");
            return m.Success ? float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0f;
        }

        /// <summary>m_text as written in the YAML: plain, or double-quoted (escapes decoded, folded lines joined).</summary>
        static string YamlText(string block)
        {
            int k = block.IndexOf("\n  m_text: ", StringComparison.Ordinal);
            if (k < 0) return "";
            int s = k + "\n  m_text: ".Length;
            if (s >= block.Length) return "";
            if (block[s] != '"') { int e = block.IndexOf('\n', s); return (e < 0 ? block.Substring(s) : block.Substring(s, e - s)).Trim(); }
            var sb = new StringBuilder();
            for (int i = s + 1; i < block.Length; i++)
            {
                char c = block[i];
                if (c == '"') break;
                if (c == '\\' && i + 1 < block.Length)
                {
                    char n = block[++i];
                    if (n == 'u' && i + 4 < block.Length) { sb.Append((char)Convert.ToInt32(block.Substring(i + 1, 4), 16)); i += 4; }
                    else if (n == 'n') sb.Append('\n');
                    else if (n == '\n') { while (i + 1 < block.Length && block[i + 1] == ' ') i++; }
                    else sb.Append(n);
                }
                else if (c == '\n') { sb.Append(' '); while (i + 1 < block.Length && block[i + 1] == ' ') i++; }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ---- report -----------------------------------------------------------------------------------------------------------

        static void WriteReport(List<Row> rows, List<string> yamlEdited)
        {
            var sb = new StringBuilder("# Font roles\n\n");
            sb.AppendLine($"{rows.Count} texts — display {rows.Count(r => r.role == UITheme.TextRole.Display)}, body {rows.Count(r => r.role == UITheme.TextRole.Body)}, title {rows.Count(r => r.role == UITheme.TextRole.Title)}\n");
            sb.AppendLine("## Ambiguous (empty when saved → body)\n");
            foreach (var r in rows.Where(r => r.ambiguous)) sb.AppendLine($"- {r.where} · {r.path}");
            sb.AppendLine("\n## Size raised to the minimum\n");
            foreach (var r in rows.Where(r => r.change.Contains("size"))) sb.AppendLine($"- {r.where} · {r.path} · {r.change}");
            sb.AppendLine("\n## Edited as YAML (Missing Script)\n");
            foreach (var p in yamlEdited) sb.AppendLine("- " + p);
            sb.AppendLine("\n## All\n\n| file | object | role | text | change |\n|---|---|---|---|---|");
            foreach (var r in rows)
                sb.AppendLine($"| {r.where} | {r.path} | {r.role} | {Esc(r.text)} | {r.change} |");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        }

        static string Esc(string s) => (s ?? "").Replace("|", "\\|").Replace("\n", " ⏎ ");
    }
}
