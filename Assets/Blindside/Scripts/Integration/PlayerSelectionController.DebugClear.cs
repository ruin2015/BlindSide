// Debug: 自動クリア — the current map won at once. Every living enemy of the player's side goes down through CombatSystem.ReportKill
// (the same path as a shot kill: counted, K.I.A, then TurnManager.CheckWin ends the match as the player's win, so the result screen,
// the rewards and the save run as after a real clear). Shown only in the editor and in development builds: a small button under the
// objective box (top right of the HUD canvas), and Blindside > Debug > Auto Clear Map (Editor/DebugQuickStartMenu.cs). Ignored while
// an action is playing out, during a dialog, or when the match is already over.
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        RectTransform debugClearButton;

        /// <summary>Wins the current match for the player now (debugging).</summary>
        public void DebugClearMap()
        {
            if (turns == null || turns.Over || !hasSide || busy || modal || units == null) return;
            SimUnit killer = null;
            foreach (var u in units) if (u.Team == side && (killer == null || (u.Alive && !killer.Alive))) killer = u;
            if (killer == null) return;
            Log("[DEBUG] 自動クリア", "a");
            foreach (var t in units)
            {
                if (!t.Alive || t.Team == side) continue;
                t.HP = 0;
                t.View?.PlayDeath();
                if (combat != null) combat.ReportKill(t, killer);
                else t.Alive = false;
                if (turns == null || turns.Over) break;
            }
            turns?.CheckWin();
        }

        /// <summary>The debug button (editor / development builds only), built once on the HUD canvas.</summary>
        void EnsureDebugButton()
        {
            if (!(Application.isEditor || Debug.isDebugBuild)) return;
            if (debugClearButton != null || canvas == null) return;
            var theme = UITheme.Current;
            var b = HudKit.NewRect("DebugAutoClear", canvas.transform);
            b.anchorMin = b.anchorMax = b.pivot = new Vector2(1f, 1f);
            b.anchoredPosition = new Vector2(-24f, -74f);
            b.sizeDelta = new Vector2(150f, 30f);
            var img = HudKit.AddImage(b, new Color(theme.foe.r, theme.foe.g, theme.foe.b, .9f), raycast: true);
            var btn = b.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.targetGraphic = img;
            btn.onClick.AddListener(() => { Sfx.Ui(); DebugClearMap(); });
            b.gameObject.AddComponent<KitBrighten>().image = img;
            var t = HudKit.AddText(b, "DEBUG  自動クリア", 13f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Body);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            HudKit.Stretch(t.rectTransform);
            debugClearButton = b;
        }
    }
}
