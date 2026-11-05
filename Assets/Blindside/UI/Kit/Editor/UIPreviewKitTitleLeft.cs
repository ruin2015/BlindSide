// Render Previews > title_logo: the title (attack side) with the left column 0.3s into its entrance — the first letters of the logo in
// their steps glitch, the code line fading up, the rest still waiting — posed with KitTitleLeft.PoseEnter(t); nothing runs on a clock.
// "Blindside > UI > Measure Kit Title Left GC" poses the left column's entrance (and the background's stripes / band) and the screen wipe
// for 300 frames and logs the managed bytes allocated (should be 0).
using UnityEditor;
using UnityEngine;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewKitTitleLeft
    {
        public const string Name = "title_logo";
        public const float LogoT = .3f;

        public static void Build(UIPreview.Stage s)
        {
            UIPreview.BuildTitleAttack(s);
            var left = s.canvases[0].GetComponentInChildren<KitTitleLeft>(true);
            if (left != null) left.PoseEnter(LogoT);
        }

        /// <summary>Blindside > UI > Measure Kit Title Left GC: poses the left column through its entrance and a wipe for 300 frames and logs
        /// the managed bytes allocated after 30 warm-up frames. Should be 0.</summary>
        [MenuItem("Blindside/UI/Measure Kit Title Left GC")]
        public static void MeasureGc()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitTitleLeftGC] edit mode only."); return; }
            var state = new UIPreview.State { name = "title_attack", label = "title", build = UIPreview.BuildTitleAttack };
            using (var stage = UIPreview.Build(state))
            {
                var left = stage.canvases[0].GetComponentInChildren<KitTitleLeft>(true);
                if (left == null) { Debug.LogWarning("[KitTitleLeftGC] the title has no kit left column."); return; }
                var wipe = KitWipe.Create(stage.canvases[0].transform);
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = (frame % 120) * (1f / 60f);
                    left.PoseEnter(t); _ = left.Duration;
                    wipe.Evaluate(t); _ = wipe.Duration;
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitTitleLeftGC] 300 frames, left column + wipe: {bytes} bytes allocated ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
