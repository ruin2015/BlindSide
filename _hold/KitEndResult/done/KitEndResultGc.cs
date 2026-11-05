// Blindside > UI > Measure Kit End/Result GC: poses the END TURN button (pulse wave, hover transition, sweep) and the result screen's
// entrance with Evaluate for 300 frames each and logs the managed bytes allocated after 30 warm-up frames. Should be 0. Edit mode only.
using UnityEditor;
using UnityEngine;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class KitEndResultGc
    {
        [MenuItem("Blindside/UI/Measure Kit End and Result GC")]
        public static void Measure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitEndResultGC] edit mode only."); return; }
            using (var stage = UIPreview.Build(new UIPreview.State { name = "hud_ended", label = "end", build = UIPreview.BuildHudEnd }))
            {
                var end = Object.FindAnyObjectByType<KitEndButton>(FindObjectsInactive.Include);
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = (frame % 120) * .01f;
                    end.PosePulse(t);
                    end.PoseState(frame % 2 == 0 ? TacticalButton.Visual.Hover : TacticalButton.Visual.Pressed, (frame % 50) * .01f);
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitEndResultGC] end button, 300 frames: {bytes} bytes ({bytes / 300f:0.##} per frame)");
            }
            using (var stage = UIPreview.Build(new UIPreview.State { name = "result_mid", label = "result", build = s => UIPreview.BuildResult(s, true, .3f) }))
            {
                var rs = Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include);
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    rs.Evaluate((frame % 100) * .01f);
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitEndResultGC] result screen, 300 frames: {bytes} bytes ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
