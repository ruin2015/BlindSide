// Blindside > UI > Measure Kit Banner GC: in the HUD stages, poses the phase banner, contact alert, headshot badge and the move dialog
// with ITimedEffect.Evaluate for 300 frames each (moving times across the whole run) and logs the managed bytes allocated after 30
// warm-up frames. Should be 0. Edit mode only.
using UnityEditor;
using UnityEngine;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class KitBannerGc
    {
        [MenuItem("Blindside/UI/Measure Kit Banner GC")]
        public static void Measure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitBannerGC] edit mode only."); return; }
            using (var stage = UIPreview.Build(new UIPreview.State { name = "phase_banner", label = "banner", build = UIPreview.BuildHudBanner }))
            {
                var banner = Object.FindAnyObjectByType<UIPhaseBanner>(FindObjectsInactive.Include);
                var contact = Object.FindAnyObjectByType<UIContactAlert>(FindObjectsInactive.Include);
                var headshot = Object.FindAnyObjectByType<UIHeadshotBadge>(FindObjectsInactive.Include);
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = frame * (1.9f / 330f);
                    banner.Evaluate(t);
                    contact.Evaluate(t * .55f);
                    headshot.Evaluate(t * .75f);
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitBannerGC] banner + contact + headshot, 300 frames: {bytes} bytes ({bytes / 300f:0.##} per frame)");
            }
            using (var stage = UIPreview.Build(new UIPreview.State { name = "dialog_move", label = "dialog", build = UIPreview.BuildHudDialog }))
            {
                var dialog = Object.FindAnyObjectByType<DialogView>(FindObjectsInactive.Include);
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    dialog.Evaluate((frame % 40) * .01f);
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitBannerGC] dialog, 300 frames: {bytes} bytes ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
