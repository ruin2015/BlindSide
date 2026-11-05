// Blindside > UI > Measure Kit Options GC: poses the settings panel's entrance and flips its switches for 300 frames in the options stage and
// logs the managed bytes allocated after 30 warm-up frames (should be 0). Edit mode only.
using UnityEditor;
using UnityEngine;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class KitOptionsGc
    {
        [MenuItem("Blindside/UI/Measure Kit Options GC")]
        public static void Measure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitOptionsGC] edit mode only."); return; }
            using (var stage = UIPreview.Build(new UIPreview.State { name = "options_default", label = "options", build = UIPreview.BuildOptionsDefault }))
            {
                var panel = Object.FindAnyObjectByType<KitOptionsPanel>(FindObjectsInactive.Include);
                var sw = Object.FindObjectsByType<KitSwitch>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (panel == null || sw.Length == 0) { Debug.LogWarning("[KitOptionsGC] no panel in the stage."); return; }
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    panel.Evaluate((frame % 40) * .01f);
                    for (int i = 0; i < sw.Length; i++) sw[i].Evaluate((frame % 20) * .01f);
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitOptionsGC] panel entrance + {sw.Length} switches, 300 frames: {bytes} bytes ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
