// Blindside > UI > Lint (Live) — the same Lint v2 checks (clipped, overlap, offscreen, aspect, fontsize, size, layout) over every active root
// Canvas of the running scene (HUD, title, battle direction overlay, options …), measured on the Game view's real resolution. Play mode only.
// Screen-space overlay canvases are measured straight in screen pixels; screen-space camera canvases through their camera; world-space canvases
// are skipped (they are not screen UI). Output: D:\Blind\UIPreview\report_live.md (+ lint_live.json), same format as report.md with the
// unique-cause table; each canvas takes the place of a preview state.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Blindside.UITools.EditorTools
{
    public static partial class UILint
    {
        const string LiveMenuPath = "Blindside/UI/Lint (Live)";

        [MenuItem(LiveMenuPath)]
        public static void LintLiveMenu()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[UILint] Lint (Live) runs in Play mode only."); return; }
            var r = RunLive();
            var sb = new StringBuilder($"[UILint Live] {r.total} issues, {r.groupCount} unique causes, {r.screenW}x{r.screenH} → {Path.Combine(UIPreview.OutDir, "report_live.md")}");
            string[] kinds = { Kind.Clipped, Kind.Overlap, Kind.Offscreen, Kind.Aspect, Kind.FontSize, Kind.Size, Kind.Layout };
            foreach (var k in kinds)
            {
                int n = 0;
                foreach (var s in r.states) foreach (var i in s.issues) if (i.kind == k) n++;
                sb.Append($"\n  {k}: {n}");
            }
            foreach (var s in r.states) sb.Append($"\n  canvas {s.state}: {s.count}");
            var groups = Group(r);
            for (int g = 0; g < groups.Count && g < 10; g++)
                sb.Append($"\n  #{g + 1} {groups[g].kind} ×{groups[g].count} sev {F(groups[g].maxSeverity)} {groups[g].element}");
            Debug.Log(sb.ToString());
        }

        [MenuItem(LiveMenuPath, true)]
        static bool LintLiveValidate() => Application.isPlaying;

        /// <summary>Lints every active root canvas of the running scene, writes report_live.md + lint_live.json, returns the result.</summary>
        public static Result RunLive()
        {
            Canvas.ForceUpdateCanvases();
            ScrW = Screen.width; ScrH = Screen.height;
            TakeUnmatched();
            var result = new Result { screenW = ScrW, screenH = ScrH };
            var all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var list = new List<Canvas>(all);
            list.Sort((a, b) => { int c = a.sortingOrder.CompareTo(b.sortingOrder); return c != 0 ? c : string.CompareOrdinal(a.name, b.name); });
            foreach (var canvas in list)
            {
                if (!canvas.isRootCanvas || !canvas.enabled || !canvas.gameObject.activeInHierarchy) continue;
                Camera cam = null;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                    if (cam == null) continue;
                }
                var sr = new StateResult { state = canvas.name, label = $"{canvas.renderMode}, order {canvas.sortingOrder}" };
                Measure(cam, new[] { canvas }, sr.state, sr.issues);
                sr.issues.Sort((a, b) => b.severity.CompareTo(a.severity));
                sr.count = sr.issues.Count;
                result.total += sr.count;
                result.states.Add(sr);
            }
            result.unmatched = TakeUnmatched();
            result.groupCount = Group(result).Count;

            Directory.CreateDirectory(UIPreview.OutDir);
            File.WriteAllText(Path.Combine(UIPreview.OutDir, "lint_live.json"), JsonUtility.ToJson(result, true), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(UIPreview.OutDir, "report_live.md"), Report(result, true), new UTF8Encoding(false));
            ScrW = UIPreview.W; ScrH = UIPreview.H;
            return result;
        }
    }
}
