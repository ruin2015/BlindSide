// The end of a match, ported from finish(winner,reason) (05_ui_input_intro.js ~443-453):
//   G.over=true; G.busy=true; then setTimeout(1200) → clearUI(); show #result; sfx(win?'ui':'boom');
//   [再挑戦] → startGame() (same side, from scratch); [タイトルへ] → back to the faction-select screen.
// TurnManager owns the win conditions / checkWin timing and raises OnFinish once; this file is what happens next.
//
// "Win" is from the player's side (`winner===G.side`). An AI-vs-AI match (playerTeam null, the dev harness) has no
// side to judge and no screen: it prints one Console line with the winner and the reason instead.
using System.Collections;
using UnityEngine;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class MatchBootstrap
    {
        /// <summary>finish()'s delay before the result appears (`dur(1200)`), real time.</summary>
        const float ResultDelaySeconds = 1.2f;

        /// <summary>G.stats — kills per team this match: [0] attack, [1] defense (non-hostage victims; gas counts for the defense).</summary>
        readonly int[] kills = new int[2];
        ResultScreen resultScreen;
        OperationResultScreen operationResult;

        /// <summary>G.stats[team] — units that team has killed this match.</summary>
        public int KillsBy(Blindside.Data.OperatorTeam team) => kills[team == Blindside.Data.OperatorTeam.Attack ? 0 : 1];

        void OnMatchFinished(TurnManager turns, System.Collections.Generic.IReadOnlyList<SimUnit> units, SimHostage hostage, MatchResult result, string reason)
        {
            if (!playerTeam.HasValue)
            {
                Debug.Log($"[Match] 勝者: {(result == MatchResult.AttackWins ? "攻撃チーム" : "防衛チーム")} — {reason}");
                return;
            }
            // G.busy=true: no more commands. (PlayerCanAct() already reads turns.Over; this also drops the open menu / tip / path.)
            if (selection != null) selection.LockForMatchEnd();
            StartCoroutine(FinishRoutine(turns, units, result, reason));
        }

        IEnumerator FinishRoutine(TurnManager turns, System.Collections.Generic.IReadOnlyList<SimUnit> units, MatchResult result, string reason)
        {
            var side = playerTeam.Value;
            bool win = (result == MatchResult.AttackWins) == (side == Blindside.Data.OperatorTeam.Attack);
            int losses = 0;
            foreach (var u in units) if (u.Team == side && !u.Alive) losses++;

            // The finishing shot / kill / walk keeps playing out while the 1.2s runs; whatever it leaves behind
            // (slow motion, a held camera, a locked input, a frozen time scale) is released whether or not the wait completed.
            // A staged finishing exchange plays to its end first — its experience beat and the camera back over the map — then a short
            // pause (12s at most, should the stage hang).
            try
            {
                yield return new WaitForSecondsRealtime(ResultDelaySeconds);
                float until = Time.unscaledTime + 12f;
                bool waited = false;
                while (battleDirector != null && battleDirector.Directing && Time.unscaledTime < until) { waited = true; yield return null; }
                if (waited) yield return new WaitForSecondsRealtime(.6f);
            }
            finally { ReleaseEngagementState(); }

            // the save (a real match only: not the tutorial's, not the editor's test play): experience, mastery, money, contribution
            // (a win only) and the record, in one write
            bool real = !tutorialMode && !PlaySession.IsTest;
            Record?.Close();
            Blindside.Progress.AwardReport report = real ? Blindside.Progress.MatchAward.Grant(Record, side, win, Live) : null;
            // the rescue operation won: GHOST and SILENCE join the squad for good (their guns with them); written with the rest below
            if (real && win && RescueMatch && Blindside.Save.SaveSystem.Current != null)
            {
                var d = Blindside.Save.SaveSystem.Current;
                foreach (var k in RescueKeys)
                {
                    if (!d.unlockedOperators.Contains(k)) d.unlockedOperators.Add(k);
                    var op = LoadOperator(k);
                    if (op != null) foreach (var g in Blindside.Hub.HubCatalog.GunsOf(op)) if (g != null && !d.OwnsWeapon(g.key)) d.ownedWeapons.Add(g.key);
                }
                HudLog.Add("GHOST と SILENCE が分隊に合流しました", HudLogType.Ally);
            }
            if (real) Blindside.Save.AutoSave.MatchEnded(win);

            // a won operation: the radio, 作戦成功, then the operation result (the squad's experience, the MVP, the reward, the rank)
            if (win && report != null)
            {
                if (operationResult == null) operationResult = GetComponent<OperationResultScreen>();
                if (operationResult == null) operationResult = gameObject.AddComponent<OperationResultScreen>();
                operationResult.Play(report, side, ReturnToHq);
                yield break;
            }

            if (resultScreen == null) resultScreen = GetComponent<ResultScreen>();
            if (resultScreen == null) resultScreen = gameObject.AddComponent<ResultScreen>();
            resultScreen.Show(win, reason, turns.Round, KillsBy(side), losses, Retry, ReturnToTitle, PlaySession.IsTest ? (System.Action)ReturnToEditor : null);
        }

        /// <summary>Ends any staged engagement and unfreezes time; the camera stays input-locked behind the result screen.</summary>
        void ReleaseEngagementState()
        {
            if (battleDirector != null) battleDirector.Abort();
            Time.timeScale = 1f;
            BattlefieldCamera.InputLocked = true;
        }

        /// <summary>[再挑戦] — the same side, from scratch: everything the last match touched is reset, then the start flow runs again
        /// (the attack side flies the intro in once more, like startGame()).</summary>
        void Retry()
        {
            var title = FindAnyObjectByType<TitleSelectScreen>(FindObjectsInactive.Include);
            if (title == null) return;
            ClearMatch();
            BattlefieldCamera.Deactivate();   // the intro Timeline drives the camera through Cinemachine
            title.Restart();
        }

        /// <summary>The game menu's タイトルへ / 読み込み: the match is dropped where it is (nothing recorded) and the title comes back.</summary>
        public void AbandonToTitle() => ReturnToTitle();

        /// <summary>The player won a real match (not the tutorial, not a test play): the radio and 作戦成功 replace the phase banner's
        /// ATTACKER / DEFENDER VICTORY (GameplayUIBridge asks).</summary>
        public bool PlayerWonRealMatch(MatchResult result) =>
            playerTeam.HasValue && !tutorialMode && !PlaySession.IsTest
            && (result == MatchResult.AttackWins) == (playerTeam.Value == Blindside.Data.OperatorTeam.Attack);

        /// <summary>The game menu's エディタへ (a test play): the match is dropped and the map editor resumes.</summary>
        public void AbandonToEditor() => ReturnToEditor();

        /// <summary>[タイトルへ] — back to the faction-select screen with the world reset behind it (and the default map the current one again).</summary>
        void ReturnToTitle()
        {
            var title = FindAnyObjectByType<TitleSelectScreen>(FindObjectsInactive.Include);
            ClearMatch();
            BattlefieldCamera.Deactivate();
            PlaySession.End();
            RestoreDefaultWorld();
            // scene flow: the title is its own scene (only the old one-scene flow shows it in place)
            var role = Blindside.Flow.SceneFlow.ActiveRole;
            if (Application.isPlaying && role != null && role != Blindside.Flow.SceneRole.Title)
            {
                Blindside.Tutorial.TutorialDirector.Disarm(this);
                Blindside.Flow.SceneFlow.GoTitle();
                return;
            }
            if (title != null) title.ShowAgain();
        }

        /// <summary>本部へ戻る (a won operation) — the squad's HQ scene when the build has it (Blindside.Hub), else the title as before.</summary>
        void ReturnToHq()
        {
            if (!Application.isPlaying || !Blindside.Flow.SceneFlow.HubAvailable || Blindside.Flow.SceneFlow.ActiveRole == null) { ReturnToTitle(); return; }
            ClearMatch();
            BattlefieldCamera.Deactivate();
            PlaySession.End();
            RestoreDefaultWorld();
            Blindside.Tutorial.TutorialDirector.Disarm(this);
            Blindside.Flow.SceneFlow.GoHub();
        }

        /// <summary>[エディタへ] (test play) — the match is cleared, the world is the default one again, and the editor resumes exactly as it
        /// was left (map, undo history, selection, camera); the map data it held was never the game's.</summary>
        void ReturnToEditor()
        {
            var resume = PlaySession.ReturnToEditor;
            ClearMatch();
            BattlefieldCamera.Deactivate();
            PlaySession.End();
            RestoreDefaultWorld();
            resume?.Invoke();
        }

        /// <summary>The tutorial's end: its match is cleared like [再挑戦] clears one, the default world comes back, and the default map's
        /// operation starts with its intro (TitleSelectScreen.LaunchMainOperation — the tutorial is disarmed by that launch).</summary>
        /// <param name="deployBriefing">the deployment that follows opens with CRUSHER's briefing (Blindside.Tutorial.TutorialDeployGuide)</param>
        public void TutorialToMainOperation(bool deployBriefing = false)
        {
            Blindside.Tutorial.TutorialDeployGuide.Pending = deployBriefing;
            Blindside.Save.AutoSave.TutorialFinished();     // the save: tutorial done, the next sortie the default house's attack
            var title = FindAnyObjectByType<TitleSelectScreen>(FindObjectsInactive.Include);
            ClearMatch();
            BattlefieldCamera.Deactivate();
            PlaySession.End();
            RestoreDefaultWorld();
            if (Blindside.Flow.SceneFlow.InRole(Blindside.Flow.SceneRole.Tutorial))
            {
                // the tutorial scene: the operation is the battle scene's, on the default house with its intro (the briefing flag carries over)
                Blindside.Tutorial.TutorialDirector.Disarm(this);
                Blindside.Flow.SceneFlow.GoBattle(new Blindside.Flow.LaunchRequest
                    { team = Blindside.Data.OperatorTeam.Attack, map = Blindside.Map.MapLibrary.LoadDefault() });
                return;
            }
            if (title != null) title.LaunchMainOperation();
        }

        /// <summary>After a match on another map: the default map is the current one again and the scene's floors / exfil planes fit it.</summary>
        static void RestoreDefaultWorld()
        {
            Blindside.Map.MapLibrary.SetCurrent(Blindside.Map.MapLibrary.LoadDefault());
            Blindside.Rendering.MapFloorSync.Apply(Blindside.Map.MapLibrary.Current);
        }
    }
}
