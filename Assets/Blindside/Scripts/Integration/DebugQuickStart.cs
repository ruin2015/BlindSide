// Blindside > Debug > Start Attack Phase (runtime half): starts an attack-side match on the default map with a fixed seed, skipping the
// intro, presses [作戦開始] as soon as the deployment is the player's (StartOperation drops every squad member on the map's default
// points — the real game path), and stops once round 1's attack phase is waiting for the player. A check-out tool: it only calls what
// the title and the START OP button call.
using System.Collections;
using UnityEngine;
using Blindside.Core;
using Blindside.Data;
using Blindside.Map;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public class DebugQuickStart : MonoBehaviour
    {
        public const int Seed = 1001;
        const float Timeout = 90f;     // seconds of real time per wait

        /// <summary>Starts the quick start in the running game (Play mode).</summary>
        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[DebugQuickStart] Play mode only."); return; }
            if (FindFirstObjectByType<DebugQuickStart>() != null) return;
            new GameObject("DebugQuickStart").AddComponent<DebugQuickStart>();
        }

        IEnumerator Start()
        {
            TitleSelectScreen title = null; MatchBootstrap mb = null;
            float t0 = Time.realtimeSinceStartup;
            while (title == null || mb == null)
            {
                if (Time.realtimeSinceStartup - t0 > Timeout) { Fail("no TitleSelectScreen / MatchBootstrap in the scene"); yield break; }
                title = FindFirstObjectByType<TitleSelectScreen>(FindObjectsInactive.Include);
                mb = FindFirstObjectByType<MatchBootstrap>(FindObjectsInactive.Include);
                yield return null;
            }
            yield return null;   // the title's Awake / OnEnable have run

            GameRng.Seed(Seed);
            if (!title.Launch(OperatorTeam.Attack, MapLibrary.LoadDefault(), null, skipIntro: true)) { Fail("the default map was refused"); yield break; }

            // the deployment: wait until it is the player's to end, then [作戦開始]
            t0 = Time.realtimeSinceStartup;
            PlayerSelectionController sel = null;
            while (true)
            {
                if (Time.realtimeSinceStartup - t0 > Timeout) { Fail("the deployment never reached the player"); yield break; }
                sel = mb.selection != null ? mb.selection : mb.GetComponent<PlayerSelectionController>();
                var turns = sel != null ? sel.Turns : null;
                if (turns != null && turns.AwaitingDeploy) break;
                yield return null;
            }
            var bridge = mb.uiBridge != null ? mb.uiBridge : FindFirstObjectByType<GameplayUIBridge>();
            if (bridge == null || bridge.endTurnButton == null) { Fail("no START OP button"); yield break; }
            yield return null;   // the deploy banner's frame
            bridge.endTurnButton.onClick.Invoke();

            // round 1's attack phase, waiting for the player
            t0 = Time.realtimeSinceStartup;
            while (true)
            {
                if (Time.realtimeSinceStartup - t0 > Timeout) { Fail("the attack phase never reached the player"); yield break; }
                var turns = sel.Turns;
                if (turns != null && turns.Phase == MatchPhase.Attack && turns.AwaitingPlayer) break;
                yield return null;
            }
            // select CRUSHER the way a click on his roster card does (RosterClick → SelectUnit: card mark, art card, command menu)
            yield return null;
            var units = sel.AllUnits;
            if (units != null)
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u.Team == OperatorTeam.Attack && u.Op != null && u.Op.key == "CRUSHER") { sel.RosterClick(u); break; }
                }
            Debug.Log($"[DebugQuickStart] attack phase ready — round {sel.Turns.Round}, default map, seed {Seed}");
            Destroy(gameObject);
        }

        void Fail(string why)
        {
            Debug.LogError("[DebugQuickStart] " + why);
            Destroy(gameObject);
        }
    }
}
