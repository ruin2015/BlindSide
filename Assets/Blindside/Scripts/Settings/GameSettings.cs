// The player's options (not the save: they belong to this machine), in PlayerPrefs under "blindside.opt.*", applied at once and at app
// start. The settings panel (KitOptionsPanel) changes them through the setters; Changed is raised after every change.
//   sound     MasterVolume (AudioListener.volume) · SfxVolume (every sound effect: Sfx / SfxNoise play at this scale)       0–1
//   controls  CameraPanSpeed (WASD / arrow keys) · ZoomSpeed (one mouse-wheel notch)                                       0.5–2
//   display   Fullscreen (borderless full screen / window) · Resolution (one of Screen.resolutions) · VSync · FpsCap
//             (30 / 60 / 120 / no cap; ignored while VSync is on) · Quality (QualitySettings level)
//             — applied at start only once the player has changed them (a first run keeps the launcher's / OS's choice)
//   effects   ScreenShake (the battlefield camera's shake). The other effect options stay where they were:
//             UIMotionSettings (モーション軽減 / 画面エフェクト / エフェクトの強さ) and BattleDirector.Mode (戦闘演出).
// Nothing here runs per frame.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Settings
{
    public static class GameSettings
    {
        const string P = "blindside.opt.";
        const string KMaster = P + "masterVolume", KSfx = P + "sfxVolume", KPan = P + "cameraPanSpeed", KZoom = P + "zoomSpeed",
            KShake = P + "screenShake", KFull = P + "fullscreen", KResW = P + "resolutionW", KResH = P + "resolutionH", KVSync = P + "vsync",
            KFps = P + "fpsCap", KQuality = P + "quality", KDisplaySet = P + "displaySet";

        public const float MinSpeed = .5f, MaxSpeed = 2f;
        /// <summary>The frame-rate caps the panel offers (−1 = no cap).</summary>
        public static readonly int[] FpsCaps = { 30, 60, 120, -1 };

        public static float MasterVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static float CameraPanSpeed { get; private set; } = 1f;
        public static float ZoomSpeed { get; private set; } = 1f;
        public static bool ScreenShake { get; private set; } = true;
        public static bool Fullscreen { get; private set; } = true;
        public static Vector2Int Resolution { get; private set; }
        public static bool VSync { get; private set; } = true;
        public static int FpsCapIndex { get; private set; } = 3;
        public static int Quality { get; private set; }

        public static event Action Changed;

        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            loaded = false;
            Changed = null;
            Load();
            ApplyAudio();
            if (PlayerPrefs.GetInt(KDisplaySet, 0) != 0) ApplyDisplay();
        }

        /// <summary>Reads PlayerPrefs (once; Boot does it at app start).</summary>
        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KMaster, 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KSfx, 1f));
            CameraPanSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(KPan, 1f), MinSpeed, MaxSpeed);
            ZoomSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(KZoom, 1f), MinSpeed, MaxSpeed);
            ScreenShake = PlayerPrefs.GetInt(KShake, 1) != 0;
            Fullscreen = PlayerPrefs.GetInt(KFull, Screen.fullScreenMode == FullScreenMode.Windowed ? 0 : 1) != 0;
            var cur = Screen.currentResolution;
            Resolution = new Vector2Int(PlayerPrefs.GetInt(KResW, Screen.width > 0 ? Screen.width : cur.width),
                                        PlayerPrefs.GetInt(KResH, Screen.height > 0 ? Screen.height : cur.height));
            VSync = PlayerPrefs.GetInt(KVSync, QualitySettings.vSyncCount > 0 ? 1 : 0) != 0;
            FpsCapIndex = Mathf.Clamp(PlayerPrefs.GetInt(KFps, FpsCaps.Length - 1), 0, FpsCaps.Length - 1);
            Quality = Mathf.Clamp(PlayerPrefs.GetInt(KQuality, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        }

        // ---- sound -------------------------------------------------------------------------------------------------------------
        public static void SetMasterVolume(float v) { Load(); MasterVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KMaster, MasterVolume); Commit(); ApplyAudio(); }
        public static void SetSfxVolume(float v) { Load(); SfxVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KSfx, SfxVolume); Commit(); }

        static void ApplyAudio() => AudioListener.volume = MasterVolume;

        // ---- controls ----------------------------------------------------------------------------------------------------------
        public static void SetCameraPanSpeed(float v) { Load(); CameraPanSpeed = Mathf.Clamp(v, MinSpeed, MaxSpeed); PlayerPrefs.SetFloat(KPan, CameraPanSpeed); Commit(); }
        public static void SetZoomSpeed(float v) { Load(); ZoomSpeed = Mathf.Clamp(v, MinSpeed, MaxSpeed); PlayerPrefs.SetFloat(KZoom, ZoomSpeed); Commit(); }

        // ---- effects -----------------------------------------------------------------------------------------------------------
        public static void SetScreenShake(bool on) { Load(); ScreenShake = on; PlayerPrefs.SetInt(KShake, on ? 1 : 0); Commit(); }

        // ---- display -----------------------------------------------------------------------------------------------------------
        public static void SetFullscreen(bool on) { Load(); Fullscreen = on; PlayerPrefs.SetInt(KFull, on ? 1 : 0); DisplayChanged(); }
        public static void SetResolution(Vector2Int r) { Load(); Resolution = r; PlayerPrefs.SetInt(KResW, r.x); PlayerPrefs.SetInt(KResH, r.y); DisplayChanged(); }
        public static void SetVSync(bool on) { Load(); VSync = on; PlayerPrefs.SetInt(KVSync, on ? 1 : 0); DisplayChanged(); }
        public static void SetFpsCap(int index) { Load(); FpsCapIndex = Mathf.Clamp(index, 0, FpsCaps.Length - 1); PlayerPrefs.SetInt(KFps, FpsCapIndex); DisplayChanged(); }
        public static void SetQuality(int level) { Load(); Quality = Mathf.Clamp(level, 0, Mathf.Max(0, QualitySettings.names.Length - 1)); PlayerPrefs.SetInt(KQuality, Quality); DisplayChanged(); }

        static void DisplayChanged()
        {
            PlayerPrefs.SetInt(KDisplaySet, 1);
            ApplyDisplay();
            Commit();
        }

        static void ApplyDisplay()
        {
            if (QualitySettings.GetQualityLevel() != Quality) QualitySettings.SetQualityLevel(Quality, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : FpsCaps[FpsCapIndex];
#if !UNITY_EDITOR
            var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Resolution.x > 0 && Resolution.y > 0) Screen.SetResolution(Resolution.x, Resolution.y, mode);
            else Screen.fullScreenMode = mode;
#endif
        }

        static readonly List<Vector2Int> resolutions = new List<Vector2Int>();

        /// <summary>The window sizes the display offers (distinct, smallest first; at least the current one).</summary>
        public static List<Vector2Int> Resolutions()
        {
            resolutions.Clear();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 800 && v.y >= 600 && !resolutions.Contains(v)) resolutions.Add(v);
            }
            if (Resolution.x > 0 && !resolutions.Contains(Resolution)) resolutions.Add(Resolution);
            if (resolutions.Count == 0) resolutions.Add(new Vector2Int(1920, 1080));
            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return resolutions;
        }

        // ---- defaults ----------------------------------------------------------------------------------------------------------
        public static void ResetSound() { SetMasterVolume(1f); SetSfxVolume(1f); }
        public static void ResetControls() { SetCameraPanSpeed(1f); SetZoomSpeed(1f); }
        public static void ResetDisplay()
        {
            Load();
            var native = Screen.currentResolution;
            Fullscreen = true; VSync = true; FpsCapIndex = FpsCaps.Length - 1;
            Resolution = new Vector2Int(native.width, native.height);
            Quality = Mathf.Max(0, QualitySettings.names.Length - 1);
            PlayerPrefs.SetInt(KFull, 1); PlayerPrefs.SetInt(KVSync, 1); PlayerPrefs.SetInt(KFps, FpsCapIndex);
            PlayerPrefs.SetInt(KResW, Resolution.x); PlayerPrefs.SetInt(KResH, Resolution.y); PlayerPrefs.SetInt(KQuality, Quality);
            DisplayChanged();
        }
        public static void ResetEffects() { SetScreenShake(true); }

        static void Commit()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
