// Global motion settings for the UI. Static, read by the effects every time they are evaluated.
// Saving to PlayerPrefs and a settings screen come later; for now this is only the field.
namespace Blindside.UI
{
    public static class UIMotionSettings
    {
        /// <summary>On: glitch effects have strength 0 (no wipe, no jitter, no split, no strips, no shake, no sweep) and only the
        /// brightness flash (and a hit's red flash) is left.</summary>
        public static bool reduceMotion;

        /// <summary>Off: the full-screen effect layer (UIScreenFX) draws nothing (no draw call). Per-layer switches and strengths are in
        /// UITheme.screenFx; with reduceMotion only its scanlines stay.</summary>
        public static bool screenFx = true;
    }
}
