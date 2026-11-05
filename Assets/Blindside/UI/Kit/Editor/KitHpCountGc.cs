// Blindside > UI > Measure Kit HP Count GC: in the HUD stage, hits the first roster card again and again (Hit + Evaluate through the .35s
// count) for 300 frames and logs the managed bytes allocated after 30 warm-up frames (the HP text is set with TMP's numeric SetText,
// so this should be 0). Edit mode only.
using UnityEditor;
using UnityEngine;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class KitHpCountGc
    {
        [MenuItem("Blindside/UI/Measure Kit HP Count GC")]
        public static void Measure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitHpGC] edit mode only."); return; }
            using (var stage = UIPreview.Build(new UIPreview.State { name = "hud_selected", label = "hp", build = UIPreview.BuildHudSelected }))
            {
                var card = Object.FindAnyObjectByType<SelectableCard>(FindObjectsInactive.Include);
                if (card == null) { Debug.LogWarning("[KitHpGC] no card in the stage."); return; }
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    if (frame % 40 == 0) card.SetHp(120 - (frame / 40) * 7, 120);       // a hit every 40 frames
                    card.Evaluate((frame % 40) * .01f);
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitHpGC] roster card hits + count, 300 frames: {bytes} bytes ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
