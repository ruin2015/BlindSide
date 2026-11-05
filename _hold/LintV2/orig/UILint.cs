// Blindside > UI > Lint (v1) — builds every UIPreview state in a throw-away scene and measures the real UI elements (RectTransform, TMP),
// not the rendered images. Five checks:
//   clipped    a visible child sticks out of its parent's rect / a TMP's preferred size is larger than its own rect
//   overlap    two visible siblings partly cover each other (full containment = layering and is not reported; LintIgnoreOverlap excuses)
//   offscreen  a visible element reaches outside the 1440x810 screen
//   aspect     an Image / RawImage rect's width:height differs from its sprite / texture by 5% or more
//   fontsize   a TMP font size (or a <size=N> tag) below 11
// Positions are screen pixels at 1440x810, top-left origin. Hidden elements (inactive, CanvasGroup alpha 0, transparent graphics, empty
// text) are skipped. Output: D:\Blind\UIPreview\report.md and lint.json; UILint.RunJson() returns the same JSON for MCP in one call.
// v1.2 noise filters: aspect only for images named *Face*/*Photo*/*Art*/*Icon*; overlap only text vs text or text vs *Icon*/*Face* images;
// clipped and offscreen skip elements named *Shadow*; clipped also skips LintIgnoreClip; elements named Deco* are skipped by every check. The report starts with a table of unique causes
// (operator names and card numbers normalised to OP / N).
// v2 (not here): comparison with ui_spec.md sizes, forced-size detection of Layout Groups.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UILint
    {
        public const float ScreenTol = 2f, ClipTol = 1f, TextTol = 1f, AspectLimit = .05f, MinFont = 11f, MinSpritePx = 4f;

        public static class Kind
        {
            public const string Clipped = "clipped", Overlap = "overlap", Offscreen = "offscreen", Aspect = "aspect", FontSize = "fontsize";
        }

        [Serializable]
        public class Issue
        {
            public string state, kind, element, position, measured, expected;
            public float severity;
        }

        [Serializable]
        public class StateResult
        {
            public string state, label;
            public int count;
            public List<Issue> issues = new List<Issue>();
        }

        [Serializable]
        public class Result
        {
            public int screenW, screenH, total, groupCount;
            public List<StateResult> states = new List<StateResult>();
        }

        // ================================================================
        //  Entry points
        // ================================================================
        [MenuItem("Blindside/UI/Lint")]
        public static void LintMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[UILint] edit mode only — stop Play first."); return; }
            string json = RunJson();
            var r = JsonUtility.FromJson<Result>(json);
            var sb = new StringBuilder($"[UILint] {r.total} issues, {r.groupCount} unique causes →{Path.Combine(UIPreview.OutDir, "report.md")}");
            foreach (var s in r.states) sb.Append($"\n  {s.state}: {s.count}");
            Debug.Log(sb.ToString());
        }

        /// <summary>Runs the lint over every preview state, writes report.md + lint.json, returns the JSON (one MCP call).</summary>
        public static string RunJson()
        {
            var result = Run();
            Directory.CreateDirectory(UIPreview.OutDir);
            string json = JsonUtility.ToJson(result, true);
            File.WriteAllText(Path.Combine(UIPreview.OutDir, "lint.json"), json, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(UIPreview.OutDir, "report.md"), Report(result), new UTF8Encoding(false));
            return json;
        }

        public static Result Run()
        {
            var result = new Result { screenW = UIPreview.W, screenH = UIPreview.H };
            foreach (var st in UIPreview.States)
            {
                var sr = new StateResult { state = st.name, label = st.label };
                using (var stage = UIPreview.Build(st)) Measure(stage, st.name, sr.issues);
                sr.issues.Sort((a, b) => b.severity.CompareTo(a.severity));
                sr.count = sr.issues.Count;
                result.total += sr.count;
                result.states.Add(sr);
            }
            result.groupCount = Group(result).Count;
            return result;
        }

        /// <summary>One-time, idempotent: puts LintIgnoreClip on the HUD roster card template's SelArrow (the prefab was built before the component existed).</summary>
        [MenuItem("Blindside/UI/Lint Mark Intended Overhangs")]
        public static void MarkOverhangs()
        {
            const string path = "Assets/Blindside/UI/Legacy/HUD_Roster.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            int added = 0;
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "SelArrow" && t.GetComponent<LintIgnoreClip>() == null)
                    { t.gameObject.AddComponent<LintIgnoreClip>().reason = "arrow hangs off the card's left edge"; added++; }
                if (added > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log($"[UILint] LintIgnoreClip added to {added} SelArrow(s) in {path}");
        }

        // ================================================================
        //  Measuring
        // ================================================================
        struct Vis { public bool has; public Rect rect; }     // rect: screen px, top-left origin

        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static string Pos(Rect r) => $"x{F(r.x)} y{F(r.y)} {F(r.width)}x{F(r.height)}";

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static Rect ScreenRect(Camera cam, Vector3[] worldCorners)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var w in worldCorners)
            {
                Vector3 p = cam.WorldToScreenPoint(w);
                float y = UIPreview.H - p.y;
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static readonly Vector3[] corners = new Vector3[4];

        static Rect RectOf(Camera cam, RectTransform rt)
        {
            rt.GetWorldCorners(corners);
            return ScreenRect(cam, corners);
        }

        static float AlphaOf(Transform t)
        {
            float a = 1f;
            for (var p = t; p != null; p = p.parent)
            {
                var g = p.GetComponent<CanvasGroup>();
                if (g == null) continue;
                a *= g.alpha;
                if (g.ignoreParentGroups) break;
            }
            return a;
        }

        /// <summary>The visible footprint of what `rt` draws itself: its text's mesh bounds, or its graphic's rect.</summary>
        static Vis Visual(Camera cam, RectTransform rt)
        {
            if (AlphaOf(rt) < .01f) return default;
            var tmp = rt.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                if (!tmp.enabled || tmp.color.a < .01f || tmp.textInfo == null || tmp.textInfo.characterCount == 0) return default;
                var b = tmp.textBounds;
                if (b.size.x <= 0f || b.size.y <= 0f) return default;
                var t = tmp.transform;
                var c = new Vector3[4];
                c[0] = t.TransformPoint(new Vector3(b.min.x, b.min.y, 0f));
                c[1] = t.TransformPoint(new Vector3(b.min.x, b.max.y, 0f));
                c[2] = t.TransformPoint(new Vector3(b.max.x, b.max.y, 0f));
                c[3] = t.TransformPoint(new Vector3(b.max.x, b.min.y, 0f));
                return new Vis { has = true, rect = ScreenRect(cam, c) };
            }
            var g = rt.GetComponent<Graphic>();
            if (g != null && g.enabled && g.color.a >= .01f && (g is RawImage || g is Image))
            {
                if (g is Image im && im.sprite == null && im.color.a < .01f) return default;
                return new Vis { has = true, rect = RectOf(cam, rt) };
            }
            return default;
        }

        static bool IgnoredBounds(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                var ig = p.GetComponent<LintIgnoreOverlap>();
                if (ig != null && ig.bounds) return true;
            }
            return false;
        }

        static bool IsShadow(Transform t) => t.name.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0;

        static bool ClipExcused(Transform t) => IsShadow(t) || t.GetComponent<LintIgnoreClip>() != null;

        // decoration layers of new UI start with "Deco" and are skipped by every check
        static bool IsDeco(Transform t) => t.name.StartsWith("Deco", StringComparison.Ordinal);

        static bool IconOrFace(Transform t) => t.name.Contains("Icon") || t.name.Contains("Face");

        // aspect is only checked on images that matter for proportions: named Face / Photo / Art / Icon
        static bool AspectNamed(Transform t) => t.name.Contains("Face") || t.name.Contains("Photo") || t.name.Contains("Art") || t.name.Contains("Icon");

        static bool IsText(Transform t) => t.GetComponent<TMP_Text>() != null;

        static bool UnderScroll(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.GetComponent<ScrollRect>() != null) return true;
            return false;
        }

        static bool Masked(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.GetComponent<RectMask2D>() != null || (p.GetComponent<Mask>() != null && p.GetComponent<Mask>().enabled)) return true;
            return false;
        }

        static readonly Regex SizeTag = new Regex(@"<size=(\d+(?:\.\d+)?)(?:px)?>", RegexOptions.IgnoreCase);

        static void Add(List<Issue> list, string state, string kind, Transform t, Rect where, string measured, string expected, float severity)
        {
            list.Add(new Issue { state = state, kind = kind, element = PathOf(t), position = Pos(where), measured = measured, expected = expected, severity = Mathf.Round(severity * 10f) / 10f });
        }

        static void Measure(UIPreview.Stage stage, string state, List<Issue> issues)
        {
            var cam = stage.cam;
            var screen = new Rect(0, 0, UIPreview.W, UIPreview.H);
            foreach (var canvas in stage.canvases)
            {
                var all = canvas.GetComponentsInChildren<RectTransform>(false);
                foreach (var rt in all)
                {
                    if (rt == canvas.transform || IsDeco(rt)) continue;
                    var vis = Visual(cam, rt);
                    var tmp = rt.GetComponent<TMP_Text>();

                    // ---- fontsize / text overflow (TMP, own rect) ----
                    if (vis.has && tmp != null)
                    {
                        if (tmp.fontSize < MinFont - .01f)
                            Add(issues, state, Kind.FontSize, rt, vis.rect, "size " + F(tmp.fontSize), ">= " + F(MinFont), (MinFont - tmp.fontSize) * 10f);
                        foreach (Match m in SizeTag.Matches(tmp.text))
                        {
                            float v = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                            if (v < MinFont - .01f)
                                Add(issues, state, Kind.FontSize, rt, vis.rect, "<size=" + F(v) + "> tag", ">= " + F(MinFont), (MinFont - v) * 10f);
                        }
                        if (!IgnoredBounds(rt) && !ClipExcused(rt))
                        {
                            var r = rt.rect;
                            float pw = tmp.preferredWidth, ph = tmp.preferredHeight;
                            bool noWrap = tmp.textWrappingMode == TextWrappingModes.NoWrap || tmp.textWrappingMode == TextWrappingModes.PreserveWhitespaceNoWrap;
                            bool overW = noWrap && pw > r.width + TextTol, overH = ph > r.height + TextTol;
                            if (overW || overH)
                            {
                                float over = Mathf.Max(overW ? pw - r.width : 0f, overH ? ph - r.height : 0f);
                                Add(issues, state, Kind.Clipped, rt, vis.rect, $"preferred {F(pw)}x{F(ph)}", $"rect {F(r.width)}x{F(r.height)}", over * 1.5f);
                            }
                        }
                    }

                    // ---- offscreen ----
                    if (vis.has && !IgnoredBounds(rt) && !IsShadow(rt))
                    {
                        var v = vis.rect;
                        float l = screen.xMin - v.xMin, r2 = v.xMax - screen.xMax, tp = screen.yMin - v.yMin, bt = v.yMax - screen.yMax;
                        float worst = Mathf.Max(Mathf.Max(l, r2), Mathf.Max(tp, bt));
                        if (worst > ScreenTol)
                            Add(issues, state, Kind.Offscreen, rt, v, $"{F(v.xMin)},{F(v.yMin)} → {F(v.xMax)},{F(v.yMax)}", $"within 0,0 → {UIPreview.W},{UIPreview.H}", worst * 1.5f);
                    }

                    // ---- clipped by parent ----
                    var parent = rt.parent as RectTransform;
                    if (vis.has && parent != null && parent != canvas.transform && !IgnoredBounds(rt) && !ClipExcused(rt) && !UnderScroll(rt))
                    {
                        var pr = RectOf(cam, parent);
                        var v = vis.rect;
                        float l = pr.xMin - v.xMin, r2 = v.xMax - pr.xMax, tp = pr.yMin - v.yMin, bt = v.yMax - pr.yMax;
                        float worst = Mathf.Max(Mathf.Max(l, r2), Mathf.Max(tp, bt));
                        if (worst > ClipTol)
                            Add(issues, state, Kind.Clipped, rt, v, $"{Pos(v)} (over by {F(worst)})", $"inside parent {Pos(pr)}", worst * (Masked(rt) ? 2f : 1f));
                    }

                    // ---- aspect ----
                    if (vis.has && AspectNamed(rt) && vis.rect.width >= 1f && vis.rect.height >= 1f)
                    {
                        float sw = 0f, sh = 0f;
                        var im = rt.GetComponent<Image>();
                        var raw = rt.GetComponent<RawImage>();
                        if (im != null && im.sprite != null && !im.preserveAspect && im.type == Image.Type.Simple)
                        { sw = im.sprite.rect.width; sh = im.sprite.rect.height; }
                        else if (raw != null && raw.texture != null)
                        { sw = raw.texture.width * raw.uvRect.width; sh = raw.texture.height * raw.uvRect.height; }
                        if (sw > 0f && sh > 0f)
                        {
                            float have = vis.rect.width / vis.rect.height, want = sw / sh, diff = Mathf.Abs(have / want - 1f);
                            if (diff >= AspectLimit)
                                Add(issues, state, Kind.Aspect, rt, vis.rect, $"rect {F(have)} ({F(vis.rect.width)}x{F(vis.rect.height)})", $"sprite {F(want)} ({F(sw)}x{F(sh)}), diff {F(diff * 100f)}%", diff * 200f);
                        }
                    }

                    // ---- overlap among this element's visible children ----
                    var kids = new List<(RectTransform rt, Rect r)>();
                    foreach (Transform ch in rt)
                    {
                        if (!ch.gameObject.activeInHierarchy || ch.GetComponent<LintIgnoreOverlap>() != null || IsDeco(ch)) continue;
                        var cv = Visual(cam, (RectTransform)ch);
                        if (cv.has) kids.Add(((RectTransform)ch, cv.rect));
                    }
                    for (int i = 0; i < kids.Count; i++)
                        for (int j = i + 1; j < kids.Count; j++)
                            CheckOverlap(issues, state, kids[i], kids[j]);
                }

                // root-level children of the canvas: siblings too
                var rootKids = new List<(RectTransform rt, Rect r)>();
                foreach (Transform ch in canvas.transform)
                {
                    if (!ch.gameObject.activeInHierarchy || ch.GetComponent<LintIgnoreOverlap>() != null || IsDeco(ch)) continue;
                    var cv = Visual(cam, (RectTransform)ch);
                    if (cv.has) rootKids.Add(((RectTransform)ch, cv.rect));
                }
                for (int i = 0; i < rootKids.Count; i++)
                    for (int j = i + 1; j < rootKids.Count; j++)
                        CheckOverlap(issues, state, rootKids[i], rootKids[j]);
            }
        }

        static void CheckOverlap(List<Issue> issues, string state, (RectTransform rt, Rect r) a, (RectTransform rt, Rect r) b)
        {
            float x0 = Mathf.Max(a.r.xMin, b.r.xMin), x1 = Mathf.Min(a.r.xMax, b.r.xMax);
            float y0 = Mathf.Max(a.r.yMin, b.r.yMin), y1 = Mathf.Min(a.r.yMax, b.r.yMax);
            float w = x1 - x0, h = y1 - y0;
            if (w < 2f || h < 2f) return;
            float inter = w * h, areaA = a.r.width * a.r.height, areaB = b.r.width * b.r.height, smaller = Mathf.Min(areaA, areaB);
            if (smaller <= 0f) return;
            float ratio = inter / smaller;
            if (ratio >= .98f) return;        // one lies fully inside the other: layering, not a collision
            if (ratio < .05f) return;
            // only text vs text, or text vs an icon / face image; text over a background image is layering
            bool ta = IsText(a.rt), tb = IsText(b.rt);
            if (!((ta && tb) || (ta && IconOrFace(b.rt)) || (tb && IconOrFace(a.rt)))) return;
            var where = Rect.MinMaxRect(x0, y0, x1, y1);
            var issue = new Issue
            {
                state = state, kind = Kind.Overlap, element = PathOf(a.rt) + "  ×  " + a.rt.parent.name + "/" + b.rt.name,
                position = Pos(where), measured = $"overlap {F(w)}x{F(h)} ({F(ratio * 100f)}% of the smaller)",
                expected = "none (or add LintIgnoreOverlap) — A " + Pos(a.r) + " · B " + Pos(b.r), severity = Mathf.Round(ratio * 1000f) / 10f,
            };
            issues.Add(issue);
        }

        // ================================================================
        //  Report
        // ================================================================
        class CauseGroup
        {
            public string kind, element, measured, expected;
            public int count;
            public float maxSeverity;
            public HashSet<string> states = new HashSet<string>();
        }

        static readonly Regex CardNo = new Regex(@"(?<![A-Za-z])Card_\d+");
        static readonly Regex OperatorSeg = new Regex(@"(?:SquadList|SideCards)/([^/ ]+)");

        /// <summary>Unique-cause groups: (kind, element path with operator names → OP and card numbers → N), worst severity first.</summary>
        static List<CauseGroup> Group(Result r)
        {
            var ops = new HashSet<string>();
            foreach (var s in r.states)
                foreach (var i in s.issues)
                    foreach (Match m in OperatorSeg.Matches(i.element)) ops.Add(m.Groups[1].Value);
            var opNames = new List<string>(ops);
            opNames.Sort((a, b) => b.Length.CompareTo(a.Length));
            Regex opRx = null;
            if (opNames.Count > 0)
            {
                var parts = new List<string>();
                foreach (var n in opNames) parts.Add(Regex.Escape(n));
                opRx = new Regex(@"(?<![\w-])(?:" + string.Join("|", parts) + @")(?![\w-])");
            }

            var map = new Dictionary<string, CauseGroup>();
            var list = new List<CauseGroup>();
            foreach (var s in r.states)
                foreach (var i in s.issues)
                {
                    string el = CardNo.Replace(i.element, "Card_N");
                    if (opRx != null) el = opRx.Replace(el, m => m.Value.StartsWith("Side_") ? "Side_OP" : "OP");
                    string key = i.kind + "|" + el;
                    if (!map.TryGetValue(key, out var g))
                    {
                        g = new CauseGroup { kind = i.kind, element = el, maxSeverity = float.MinValue };
                        map[key] = g; list.Add(g);
                    }
                    g.count++;
                    g.states.Add(i.state);
                    if (i.severity > g.maxSeverity) { g.maxSeverity = i.severity; g.measured = i.measured; g.expected = i.expected; }
                }
            list.Sort((a, b) => { int c = b.maxSeverity.CompareTo(a.maxSeverity); return c != 0 ? c : b.count.CompareTo(a.count); });
            return list;
        }

        static string Cell(string s) => (s ?? "").Replace("|", "\\|").Replace("\n", " ");

        static string Report(Result r)
        {
            var sb = new StringBuilder();
            sb.Append("# UI Lint report\n\n");
            sb.Append($"Screen {r.screenW}x{r.screenH} · {r.total} issues · generated {DateTime.Now:yyyy-MM-dd HH:mm}\n\n");
            sb.Append("Severity (for ordering only): clipped = px over (text ×1.5, masked parent ×2) · offscreen = px × 1.5 · overlap = % of the smaller rect · aspect = diff% × 2 · fontsize = (11 − size) × 10.\n\n");
            var groups = Group(r);
            sb.Append($"## Unique causes ({groups.Count} groups, top {Mathf.Min(30, groups.Count)} by max severity)\n\n");
            sb.Append("Operator names and card numbers are normalised to OP / N before grouping.\n\n");
            sb.Append("| # | 종류 | 요소명(정규화) | 건수 | 상태 수 | 최대 심각도 | 대표 측정값 | 대표 기대값 |\n|---|---|---|---|---|---|---|---|\n");
            for (int g = 0; g < groups.Count && g < 30; g++)
            {
                var gr = groups[g];
                sb.Append($"| {g + 1} | {gr.kind} | {Cell(gr.element)} | {gr.count} | {gr.states.Count} | {F(gr.maxSeverity)} | {Cell(gr.measured)} | {Cell(gr.expected)} |\n");
            }
            sb.Append("\n## Count by state\n\n| 상태 | 건수 | clipped | overlap | offscreen | aspect | fontsize |\n|---|---|---|---|---|---|---|\n");
            var tot = new int[6];
            foreach (var s in r.states)
            {
                int Count(string k) { int n = 0; foreach (var i in s.issues) if (i.kind == k) n++; return n; }
                int[] row = { s.count, Count(Kind.Clipped), Count(Kind.Overlap), Count(Kind.Offscreen), Count(Kind.Aspect), Count(Kind.FontSize) };
                for (int k = 0; k < 6; k++) tot[k] += row[k];
                sb.Append($"| {s.state} | {row[0]} | {row[1]} | {row[2]} | {row[3]} | {row[4]} | {row[5]} |\n");
            }
            sb.Append($"| **합계** | **{tot[0]}** | **{tot[1]}** | **{tot[2]}** | **{tot[3]}** | **{tot[4]}** | **{tot[5]}** |\n");
            sb.Append("\n## Issues (all)\n\n| 상태 | 종류 | 요소명 | 위치 | 측정값 | 기대값 | 심각도 |\n|---|---|---|---|---|---|---|\n");
            foreach (var s in r.states)
                foreach (var i in s.issues)
                    sb.Append($"| {i.state} | {i.kind} | {Cell(i.element)} | {Cell(i.position)} | {Cell(i.measured)} | {Cell(i.expected)} | {F(i.severity)} |\n");
            return sb.ToString();
        }
    }
}
