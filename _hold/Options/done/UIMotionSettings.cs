// Global motion settings for the UI. Static, read by the effects every time they are evaluated, so a change shows at once.
// Saved in PlayerPrefs under "blindside.ui.*" and loaded at app start; the settings panel (KitOptionsPanel) changes them through the
// Set… methods (field + save + Changed). The fields stay public: previews and tests may pose them directly without saving.
//   reduceMotion  on: glitch, text travel, flowing stripes and screen shake are off, only fades remain (UIScreenFX: scanlines only)
//   screenFx      off: UIScreenFX draws nothing
//   effectLevel   0 / 1 / 2 = 弱 / 中 / 強 = EffectScale 0.5 / 1.0 / 1.5 (UIScreenFX alphas and the glitch offsets; ignored by the glitch
//                 when reduceMotion is on, since the glitch is off then)
using UnityEngine;

namespace Blindside.UI
{
    public static class UIMotionSettings
    {
        public const string KeyReduce = "blindside.ui.reduceMotion", KeyScreenFx = "blindside.ui.screenFx", KeyLevel = "blindside.ui.effectLevel";
        public const int LevelWeak = 0, LevelMedium = 1, LevelStrong = 2;

        /// <summary>On: glitch effects have strength 0 (no wipe, no jitter, no split, no strips, no shake, no sweep) and only the
        /// brightness flash (and a hit's red flash) is left.</summary>
        public static bool reduceMotion;

        /// <summary>Off: the full-screen effect layer (UIScreenFX) draws nothing (no draw call). Per-layer switches and strengths are in
        /// UITheme.screenFx; with reduceMotion only its scanlines stay.</summary>
        public static bool screenFx = true;

        /// <summary>0 弱 / 1 中 (default) / 2 強.</summary>
        public static int effectLevel = LevelMedium;

        /// <summary>Strength multiplier of the screen effect and the glitch offsets: 0.5 / 1.0 / 1.5.</summary>
        public static float EffectScale => effectLevel == LevelWeak ? .5f : effectLevel == LevelStrong ? 1.5f : 1f;

        /// <summary>Raised after any Set… call (the panel refreshes its switches from it).</summary>
        public static event System.Action Changed;

        /// <summary>Reads the saved values (defaults when a key is missing: reduceMotion off, screenFx on, level 中).</summary>
        public static void Load()
        {
            reduceMotion = PlayerPrefs.GetInt(KeyReduce, 0) != 0;
            screenFx = PlayerPrefs.GetInt(KeyScreenFx, 1) != 0;
            effectLevel = Mathf.Clamp(PlayerPrefs.GetInt(KeyLevel, LevelMedium), LevelWeak, LevelStrong);
        }

        /// <summary>Writes the current values.</summary>
        public static void Save()
        {
            PlayerPrefs.SetInt(KeyReduce, reduceMotion ? 1 : 0);
            PlayerPrefs.SetInt(KeyScreenFx, screenFx ? 1 : 0);
            PlayerPrefs.SetInt(KeyLevel, effectLevel);
            PlayerPrefs.Save();
        }

        public static void SetReduceMotion(bool on) { reduceMotion = on; Save(); Changed?.Invoke(); }
        public static void SetScreenFx(bool on) { screenFx = on; Save(); Changed?.Invoke(); }
        public static void SetEffectLevel(int level) { effectLevel = Mathf.Clamp(level, LevelWeak, LevelStrong); Save(); Changed?.Invoke(); }

        /// <summary>Removes the saved keys (back to the defaults at the next Load).</summary>
        public static void DeleteSaved()
        {
            PlayerPrefs.DeleteKey(KeyReduce); PlayerPrefs.DeleteKey(KeyScreenFx); PlayerPrefs.DeleteKey(KeyLevel);
            PlayerPrefs.Save();
        }

        // "Enter Play Mode" without a domain reload keeps statics: load again (and drop old listeners) when the app starts
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnAppStart() { Changed = null; Load(); }
    }
}
