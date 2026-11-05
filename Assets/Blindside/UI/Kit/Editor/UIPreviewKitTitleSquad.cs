// Render Previews > title_spot_glitch: the title (attack side) with the spotlight 0.21s into its card glitch (wipe over, jitter / strips on,
// the card shake and the brightness flash) — posed with KitSpotlight.PoseGlitch(t); nothing runs on a clock.
// "Blindside > UI > Measure Kit Title Squad GC" poses the roster's entrance and the spotlight's entrance + glitch for 300 frames and logs
// the managed bytes allocated (should be 0).
using UnityEditor;
using UnityEngine;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewKitTitleSquad
    {
        public const string Name = "title_spot_glitch";
        public const float GlitchT = .21f;

        public static void Build(UIPreview.Stage s)
        {
            UIPreview.BuildTitleAttack(s);
            var spot = s.canvases[0].GetComponentInChildren<KitSpotlight>(true);
            if (spot != null) spot.PoseGlitch(GlitchT);
        }

        /// <summary>Blindside > UI > Measure Kit Title Squad GC: poses the roster and the spotlight through their entrance and the glitch for
        /// 300 frames and logs the managed bytes allocated after 30 warm-up frames. Should be 0.</summary>
        [MenuItem("Blindside/UI/Measure Kit Title Squad GC")]
        public static void MeasureGc()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitTitleSquadGC] edit mode only."); return; }
            var state = new UIPreview.State { name = "title_attack", label = "title", build = UIPreview.BuildTitleAttack };
            using (var stage = UIPreview.Build(state))
            {
                var squad = stage.canvases[0].GetComponentInChildren<KitTitleSquad>(true);
                var spot = stage.canvases[0].GetComponentInChildren<KitSpotlight>(true);
                if (squad == null || spot == null) { Debug.LogWarning("[KitTitleSquadGC] the title has no kit roster / spotlight."); return; }
                squad.PlayEnter();
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = (frame % 120) * (1f / 60f);
                    squad.Evaluate(t); _ = squad.Duration;
                    spot.PoseGlitch(t); _ = spot.Duration;
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitTitleSquadGC] 300 frames, roster + spotlight: {bytes} bytes allocated ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
