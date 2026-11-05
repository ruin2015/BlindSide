// Render Previews of the game menu (PauseMenu) and the settings panel's tabs (KitOptionsPanel), posed settled (nothing runs on a clock):
//   pause_menu         the menu over the HUD (CRUSHER selected): 再開 / 保存 / 読み込み / 設定 / タイトルへ / ゲーム終了
//   pause_menu_test    an editor test play: 保存 / 読み込み dim, エディタへ instead of タイトルへ
//   pause_confirm      タイトルへ asked
//   options_controls   the settings panel on 操作 · options_display on 画面 · options_effects on 演出 (options_default is サウンド)
// The panel shows the values as they are in this editor's PlayerPrefs; nothing is changed.
using UnityEngine;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewPause
    {
        public const string Menu = "pause_menu", MenuTest = "pause_menu_test", Confirm = "pause_confirm",
            Controls = "options_controls", Display = "options_display", Effects = "options_effects";

        static PauseMenu Build(UIPreview.Stage s)
        {
            UIPreview.BuildHudSelected(s);
            var canvas = s.NewCanvas("PauseMenu_Canvas", UITheme.CanvasKind.Menu, 200);
            return PauseMenu.BuildInto(canvas.transform);
        }

        public static void BuildMenu(UIPreview.Stage s) => Build(s).PoseOpen(false, true, true);
        public static void BuildMenuTest(UIPreview.Stage s) => Build(s).PoseOpen(true, false, false);

        public static void BuildConfirm(UIPreview.Stage s)
        {
            var m = Build(s);
            m.PoseOpen(false, true, true);
            m.dialog.Open("タイトルへ", "TITLE", "作戦を中断して、タイトルへ戻りますか？\n作戦の途中経過は保存されません。", "はい", "いいえ");
        }

        static void Options(UIPreview.Stage s, int tab)
        {
            var m = Build(s);
            m.PoseOpen(false, true, true);
            m.options.Open();
            m.options.ShowTab(tab);
        }

        public static void BuildControls(UIPreview.Stage s) => Options(s, KitOptionsPanel.TabControls);
        public static void BuildDisplay(UIPreview.Stage s) => Options(s, KitOptionsPanel.TabDisplay);
        public static void BuildEffects(UIPreview.Stage s) => Options(s, KitOptionsPanel.TabEffects);
    }
}
