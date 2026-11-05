// Blindside > UI > Lint (v2) — the second half of UILint (partial class): the two new checks and the new report.
//   size    a kit component's rect differs by 5% or more from its design size (LintSpec: component type → size read from UITheme)
//   layout  under kit UI: a Horizontal / Vertical / Grid Layout Group with Control Child Size on, a child whose real size differs from its
//           LayoutElement / sizeDelta / cell design size, or a ContentSizeFitter (it sizes the rect from its content)
// Both are measured in the same throw-away preview scenes as the five v1 checks (all states in one run). Nothing is fixed here — the report
// only lists. The report also ends with the components that are not in the LintSpec table ("unmatched") and the preview states rendered.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UITools.EditorTools
{
    public static partial class UILint
    {
        static readonly Dictionary<string, int> unmatched = new Dictionary<string, int>();

        static readonly HashSet<string> KitNames = new HashSet<string>
        {
            "SelectableCard", "TacticalButton", "TacticalPanel", "ResultScreen", "DialogView", "UIPhaseBanner", "UIContactAlert",
            "UIHeadshotBadge", "UIPulse", "Chip", "SegmentedBar",
        };

        /// <summary>The preview states the task asks for (a missing one is reported at the end of the report).</summary>
        static readonly string[] RequiredStates =
        {
            "title_attack", "title_defense", "hud_selected", "hud_unselected", "hud_hit", "hud_ended", "hud_moved", "hud_longdesc",
            "hud_cmd_long", "hud_cmd_disabled", "hud_log_full", "dialog_move", "phase_banner_t01", "phase_banner", "phase_banner_t12",
            "phase_banner_t17", "contact_alert", "headshot_badge", "result_win", "result_lose", "options_default", "options_reduce",
        };

        // ================================================================
        //  Measuring
        // ================================================================
        static void CheckV2(UIPreview.Stage stage, Canvas canvas, string state, List<Issue> issues)
        {
            var cam = stage.cam;

            // ---- size: component type → design size ----
            var targets = new List<LintSpec.Target>();
            foreach (var c in canvas.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (c == null) continue;
                var ty = c.GetType();
                if (ty.Namespace == null || !ty.Namespace.StartsWith("Blindside")) continue;
                if (LintSpec.Has(ty)) LintSpec.Collect(c, targets);
                else { unmatched.TryGetValue(ty.Name, out int n); unmatched[ty.Name] = n + 1; }
            }
            foreach (var t in targets)
            {
                if (AlphaOf(t.rt) < .01f) continue;
                Vector2 have = t.rt.rect.size;
                float dw = t.size.x > 0f ? Mathf.Abs(have.x - t.size.x) / t.size.x : 0f, dh = 0f;
                if (!t.ignoreHeight && t.size.y > 0f)
                    dh = t.heightMin ? (have.y >= t.size.y * .95f ? 0f : (t.size.y - have.y) / t.size.y) : Mathf.Abs(have.y - t.size.y) / t.size.y;
                float diff = Mathf.Max(dw, dh);
                if (diff < .05f) continue;
                string want = t.ignoreHeight ? $"{F(t.size.x)} wide" : t.heightMin ? $"{F(t.size.x)}x{F(t.size.y)}+" : $"{F(t.size.x)}x{F(t.size.y)}";
                Add(issues, state, Kind.Size, t.rt, RectOf(cam, t.rt), $"{F(have.x)}x{F(have.y)}", $"{want} ({t.what}), diff {F(diff * 100f)}%", diff * 200f);
            }

            // ---- layout: forced sizes under kit UI ----
            foreach (var lg in canvas.GetComponentsInChildren<LayoutGroup>(false))
            {
                if (!InKit(lg.transform) || AlphaOf(lg.transform) < .01f) continue;
                var rt = (RectTransform)lg.transform;
                var hv = lg as HorizontalOrVerticalLayoutGroup;
                var grid = lg as GridLayoutGroup;
                if (hv != null && (hv.childControlWidth || hv.childControlHeight))
                    Add(issues, state, Kind.Layout, rt, RectOf(cam, rt), $"{lg.GetType().Name}: Control Child Size on (width {hv.childControlWidth}, height {hv.childControlHeight})",
                        "off — children keep their designed sizes", 10f);
                foreach (Transform ch in rt)
                {
                    var crt = ch as RectTransform;
                    if (crt == null || !ch.gameObject.activeSelf) continue;
                    var le = ch.GetComponent<LayoutElement>();
                    if (le != null && le.ignoreLayout) continue;
                    float dw = Design(crt, le, 0, hv, grid != null ? grid.cellSize.x : -1f), dh = Design(crt, le, 1, hv, grid != null ? grid.cellSize.y : -1f);
                    Vector2 have = crt.rect.size;
                    float rw = dw > 0f && Mathf.Abs(have.x - dw) >= 1f ? Mathf.Abs(have.x - dw) / dw : 0f;
                    float rh = dh > 0f && Mathf.Abs(have.y - dh) >= 1f ? Mathf.Abs(have.y - dh) / dh : 0f;
                    float diff = Mathf.Max(rw, rh);
                    if (diff < .05f) continue;
                    Add(issues, state, Kind.Layout, crt, RectOf(cam, crt), $"child {F(have.x)}x{F(have.y)} under {lg.GetType().Name}",
                        $"design {(dw > 0f ? F(dw) : "-")}x{(dh > 0f ? F(dh) : "-")} (LayoutElement / sizeDelta / cell), diff {F(diff * 100f)}%", diff * 200f);
                }
            }
            foreach (var f in canvas.GetComponentsInChildren<ContentSizeFitter>(false))
            {
                if (!InKit(f.transform) || AlphaOf(f.transform) < .01f) continue;
                if (f.horizontalFit == ContentSizeFitter.FitMode.Unconstrained && f.verticalFit == ContentSizeFitter.FitMode.Unconstrained) continue;
                var rt = (RectTransform)f.transform;
                Add(issues, state, Kind.Layout, rt, RectOf(cam, rt), $"ContentSizeFitter (horizontal {f.horizontalFit}, vertical {f.verticalFit}) → {F(rt.rect.width)}x{F(rt.rect.height)}",
                    "a fixed designed size", 5f);
            }
        }

        /// <summary>A child's designed size on one axis: LayoutElement preferred size, else (if the group does not control that axis and the
        /// child is not stretched) its sizeDelta, else -1 (the size comes from the layout: no design value).</summary>
        static float Design(RectTransform c, LayoutElement le, int axis, HorizontalOrVerticalLayoutGroup hv, float cell)
        {
            if (cell >= 0f) return cell;
            if (le != null) { float p = axis == 0 ? le.preferredWidth : le.preferredHeight; if (p >= 0f) return p; }
            bool controls = hv != null && (axis == 0 ? hv.childControlWidth : hv.childControlHeight);
            if (controls) return -1f;
            bool stretched = axis == 0 ? c.anchorMin.x != c.anchorMax.x : c.anchorMin.y != c.anchorMax.y;
            if (stretched) return -1f;
            return axis == 0 ? c.sizeDelta.x : c.sizeDelta.y;
        }

        /// <summary>Kit UI = anything below a kit component (Kit… types, cards, buttons, panels, dialog, banner, result …).</summary>
        static bool InKit(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                foreach (var mb in p.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    string n = mb.GetType().Name;
                    if (n.StartsWith("Kit") || KitNames.Contains(n)) return true;
                }
            return false;
        }

        /// <summary>UITheme values that disagree with the kit's own constants / ui_spec.md (the theme asset is stale there).</summary>
        static StateResult ThemeState()
        {
            var sr = new StateResult { state = "theme_values", label = "UITheme vs ui_spec" };
            var h = UITheme.Current.hud;
            void Cmp(string name, float have, float want)
            {
                if (want <= 0f || Mathf.Abs(have - want) / want < .05f) return;
                sr.issues.Add(new Issue { state = sr.state, kind = Kind.Size, element = "UITheme/" + name, position = "-", measured = F(have), expected = F(want) + " (kit constant / ui_spec)", severity = Mathf.Round(Mathf.Abs(have - want) / want * 2000f) / 10f });
            }
            Cmp("hud.logoWidth", h.logoWidth, KitTopbar.LogoW);
            Cmp("hud.phaseWidth", h.phaseWidth, KitTopbar.PhaseW);
            Cmp("hud.roundWidth", h.roundWidth, KitTopbar.RoundW);
            Cmp("hud.objective.x", h.objective.x, KitTopbar.ObjW);
            Cmp("hud.objective.y", h.objective.y, KitTopbar.ObjH);
            Cmp("hud.topbarHeight", h.topbarHeight, KitTopbar.BarH);
            sr.count = sr.issues.Count;
            return sr;
        }

        static List<string> TakeUnmatched()
        {
            var list = new List<string>();
            foreach (var kv in unmatched) list.Add($"{kv.Key} ×{kv.Value}");
            list.Sort(StringComparer.Ordinal);
            unmatched.Clear();
            return list;
        }

        // ================================================================
        //  Report (v2)
        // ================================================================
        static string Report(Result r)
        {
            var sb = new StringBuilder();
            sb.Append("# UI Lint report (v2)\n\n");
            sb.Append($"Screen {r.screenW}x{r.screenH} · {r.total} issues · generated {DateTime.Now:yyyy-MM-dd HH:mm}\n\n");
            sb.Append("Severity (for ordering only): clipped = px over (text ×1.5, masked parent ×2) · offscreen = px × 1.5 · overlap = % of the smaller rect · aspect = diff% × 2 · fontsize = (11 − size) × 10 · size = diff% × 2 · layout = 10 (Control Child Size) / diff% × 2 / 5 (ContentSizeFitter).\n\n");
            var groups = Group(r);
            sb.Append($"## Unique causes ({groups.Count} groups, top {Mathf.Min(30, groups.Count)} by max severity)\n\n");
            sb.Append("Operator names and card numbers are normalised to OP / N before grouping.\n\n");
            sb.Append("| # | 종류 | 요소명(정규화) | 건수 | 상태 수 | 최대 심각도 | 대표 측정값 | 대표 기대값 |\n|---|---|---|---|---|---|---|---|\n");
            for (int g = 0; g < groups.Count && g < 30; g++)
            {
                var gr = groups[g];
                sb.Append($"| {g + 1} | {gr.kind} | {Cell(gr.element)} | {gr.count} | {gr.states.Count} | {F(gr.maxSeverity)} | {Cell(gr.measured)} | {Cell(gr.expected)} |\n");
            }
            string[] kinds = { Kind.Clipped, Kind.Overlap, Kind.Offscreen, Kind.Aspect, Kind.FontSize, Kind.Size, Kind.Layout };
            sb.Append("\n## Count by state\n\n| 상태 | 건수 | clipped | overlap | offscreen | aspect | fontsize | size | layout |\n|---|---|---|---|---|---|---|---|---|\n");
            var tot = new int[kinds.Length + 1];
            foreach (var s in r.states)
            {
                var row = new int[kinds.Length + 1];
                row[0] = s.count;
                foreach (var i in s.issues) for (int k = 0; k < kinds.Length; k++) if (i.kind == kinds[k]) row[k + 1]++;
                for (int k = 0; k < row.Length; k++) tot[k] += row[k];
                sb.Append("| " + s.state + " | " + string.Join(" | ", Array.ConvertAll(row, n => n.ToString())) + " |\n");
            }
            sb.Append("| **합계** | " + string.Join(" | ", Array.ConvertAll(tot, n => "**" + n + "**")) + " |\n");
            sb.Append("\n## Issues (all)\n\n| 상태 | 종류 | 요소명 | 위치 | 측정값 | 기대값 | 심각도 |\n|---|---|---|---|---|---|---|\n");
            foreach (var s in r.states)
                foreach (var i in s.issues)
                    sb.Append($"| {i.state} | {i.kind} | {Cell(i.element)} | {Cell(i.position)} | {Cell(i.measured)} | {Cell(i.expected)} | {F(i.severity)} |\n");

            sb.Append($"\n## 미매칭 목록 (LintSpec 표에 없는 Blindside 컴포넌트, {r.unmatched.Count}종)\n\n");
            foreach (var u in r.unmatched) sb.Append("- " + u + "\n");

            sb.Append("\n## 미리보기 상태 (Render Previews)\n\n| 상태 이름 | 해상도 | 설명 |\n|---|---|---|\n");
            var names = new HashSet<string>();
            foreach (var st in UIPreview.States) { names.Add(st.name); sb.Append($"| {st.name} | {UIPreview.W}x{UIPreview.H} | {Cell(st.label)} |\n"); }
            sb.Append("\n### 필수 상태 점검\n\n");
            int missing = 0;
            foreach (var n in RequiredStates) if (!names.Contains(n)) { sb.Append($"- 없음: {n}\n"); missing++; }
            if (missing == 0) sb.Append($"- 요구한 {RequiredStates.Length}개 상태가 모두 있습니다.\n");
            return sb.ToString();
        }
    }
}
