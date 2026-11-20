// Debug: 自動クリア — the current map won at once. Every living enemy of the player's side goes down through CombatSystem.ReportKill
// (the same path as a shot kill: counted, K.I.A, then TurnManager.CheckWin ends the match as the player's win, so the result screen,
// the rewards and the save run as after a real clear). Shown only in the editor and in development builds, as the game's one skip:
// hold ESC (EscHoldSkip, its hint right above END TURN, 「DEBUG 長押しでスキップ」) — a short tap still opens the game menu (or cancels
// what the selection has open). Also Blindside > Debug > Auto Clear Map (Editor/DebugQuickStartMenu.cs). Offered only while the player
// can act (not during a battle cinematic, an action playing out or a dialog); gone once the match is over.
using UnityEngine;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        public const string DebugSkipLabel = "DEBUG 長押しでスキップ";
        EscHoldSkip debugSkip;
        System.Action debugSkipAction;

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

        /// <summary>At bind: who a tap of ESC belongs to (the selection, when it has something to cancel) for every hold-ESC hint.</summary>
        void EnsureDebugButton()
        {
            EscHoldSkip.TapTaken = () => this != null && PlayerCanAct() && HasSomethingToCancel;
        }

        /// <summary>Every frame: the debug skip offered while the player can act (editor / development builds only).</summary>
        void SyncDebugSkip()
        {
            if (!(Application.isEditor || Debug.isDebugBuild)) return;
            bool want = turns != null && !turns.Over && hasSide && !cinematicOn && PlayerCanAct();
            if (want && debugSkip == null)
                debugSkip = EscHoldSkip.Begin(debugSkipAction ?? (debugSkipAction = DebugClearMap), .3f,
                                              20f + KitEndButton.H + 10f, true, DebugSkipLabel);   // right above END TURN
            else if (!want && debugSkip != null) { if (!debugSkip.Fired) debugSkip.End(); debugSkip = null; }
        }

        void OnDisable()
        {
            if (debugSkip != null && !debugSkip.Fired) debugSkip.End();
            debugSkip = null;
        }
    }
}
