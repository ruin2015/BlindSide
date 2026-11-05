// Render Previews of the title scene's main menu (MainMenuScreen + KitMenuLogo + KitTextButton) and the autosave notice
// (KitSaveIndicator), all posed at fixed times (nothing runs on a clock) from a sample save, never the one on disk, over the key art at
// MainMenuScreen.BackgroundPath (the plain background when it is not there yet):
//   menu_save         settled, a save exists (ニューゲーム / 読み込み / ゲーム終了)
//   menu_nosave       settled, no save (ニューゲーム / ゲーム終了)
//   menu_logo_heavy   0.4s into the first show: the heavy glitch intro (letters jumping and split, strips sliding, the word shaking)
//   menu_glitch       settled, inside a burst of the calm glitch loop
//   menu_hover        settled, the pointer over ニューゲーム (the text grown and white)
//   menu_confirm      ニューゲーム over a save: the overwrite question
//   save_indicator    the AUTO SAVE chip 0.8s into its run, over the HUD-coloured backdrop
// "Blindside > UI > Measure Main Menu GC" poses the menu (heavy intro + calm loop) and the save chip for 300 frames and logs the managed
// bytes allocated (should be 0).
using UnityEditor;
using UnityEngine;
using Blindside.Save;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewMainMenu
    {
        public const string Save = "menu_save", NoSave = "menu_nosave", LogoHeavy = "menu_logo_heavy", Glitch = "menu_glitch",
            Hover = "menu_hover", Confirm = "menu_confirm", Indicator = "save_indicator";
        public const float HeavyT = .4f, SettledT = 3f, IndicatorT = .8f;

        // a fixed save
        static SaveData Sample()
        {
            var d = SaveData.Fresh();
            d.savedUtc = new System.DateTime(2026, 11, 6, 1, 8, 0, System.DateTimeKind.Local).ToUniversalTime().ToString("o");
            d.tutorialDone = true;
            return d;
        }

        static MainMenuScreen Menu(UIPreview.Stage s, bool withSave)
        {
            var canvas = s.NewCanvas("BlindsideCanvas", UITheme.CanvasKind.Menu, 0);
            var art = AssetDatabase.LoadAssetAtPath<Texture>(MainMenuScreen.BackgroundPath);
            return MainMenuScreen.Create(canvas.transform, withSave ? Sample() : null, art);
        }

        public static void BuildSave(UIPreview.Stage s) => Menu(s, true).PoseAt(SettledT, false);
        public static void BuildNoSave(UIPreview.Stage s) => Menu(s, false).PoseAt(SettledT, false);
        public static void BuildLogoHeavy(UIPreview.Stage s) => Menu(s, true).PoseAt(HeavyT, true);

        /// <summary>A time inside the calm loop's burst (the second loop's window, a step in).</summary>
        public static float GlitchT(KitMenuLogo l) => l.EnterTime + KitMenuLogo.Period * (1f + KitMenuLogo.BurstFrom) + KitMenuLogo.StepTime * 1.5f;

        public static void BuildGlitch(UIPreview.Stage s)
        {
            var m = Menu(s, true);
            m.PoseAt(KitMenuLogo.CalmIn + KitMenuLogo.Period * (1f + KitMenuLogo.BurstFrom) + KitMenuLogo.StepTime * 1.5f, false);
        }

        public static void BuildHover(UIPreview.Stage s)
        {
            var m = Menu(s, true);
            m.PoseAt(SettledT, false);
            m.newGameButton.PoseHover(true);
        }

        public static void BuildConfirm(UIPreview.Stage s)
        {
            var m = Menu(s, true);
            m.PoseAt(SettledT, false);
            m.AskOverwrite();
        }

        public static void BuildIndicator(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("SaveIndicator_Canvas", UITheme.CanvasKind.Menu, 0);
            var v = KitSaveIndicator.BuildInto(canvas.transform);
            v.Evaluate(IndicatorT);
        }

        [MenuItem("Blindside/UI/Measure Main Menu GC")]
        public static void MeasureGc()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[MainMenuGC] edit mode only."); return; }
            var state = new UIPreview.State { name = Save, label = "menu", build = BuildSave };
            using (var stage = UIPreview.Build(state))
            {
                var m = stage.canvases[0].GetComponentInChildren<MainMenuScreen>(true);
                var ind = KitSaveIndicator.BuildInto(stage.canvases[0].transform);
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = frame * (1f / 60f);
                    m.PoseAt(t * 2f, true); _ = m.Duration;      // (×2: the heavy intro and well into the calm loop)
                    m.newGameButton.Evaluate(t % .2f);
                    ind.Evaluate(t % ind.Duration);
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[MainMenuGC] 300 frames, main menu + save chip: {bytes} bytes allocated ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
