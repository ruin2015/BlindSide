// The deployment briefing of the real operation: on the story's first operation, however it was reached (or when the tutorial's ending
// asks for it: Pending), the attack-side match's deployment is held (TutorialGate.HoldInput) while CRUSHER, on the radio, points out the
// hostage or the target building (camera on it, a red marker and chip) — then the player is free to deploy. Once the first operation's
// deployment is over, the squad's opening talk plays on the radio before round 1.
using System.Collections;
using UnityEngine;
using Blindside.Data;
using Blindside.Integration;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialDeployGuide : MonoBehaviour
    {
        public const string L1 = "よし。突入する前に、\n配置について教えてやる。";
        public const string L2 = "人質の位置が見えるか？";
        public const string L3 = "人質を安全に救出できる作戦を考え、\n能力に合った隊員を配置しろ。";
        public const string LFree = "どこに配置するかはお前の自由だ。\nお前の戦略を練ってみろ。";
        public const string GHostage = "人質";
        // (a map with no civilian: the building the enemy holds)
        public const string L2Foes = "敵はあの建物の中に潜んでいる。";
        public const string L3Foes = "全員を制圧できる作戦を考え、\n隊員を配置しろ。";
        public const string GBuilding = "目標建物";
        /// <summary>What the squad says once deployed on the first operation, before round 1.</summary>
        public static readonly (string key, string line)[] Opening =
        {
            ("CRUSHER", "今回の作戦は3人で開始する。\n各自、注意しろ。"),
            ("SERUM", "了解。新入りは大丈夫か？"),
            ("CRUSHER", "大丈夫だろう。\nこう見えても、最優秀隊員として\n入ってきた奴だからな。"),
            ("SERUM", "よし、作戦を開始する。\n目標は敵の殲滅だ。"),
        };

        /// <summary>The next attack-side match runs the briefing (set by the tutorial's ending).</summary>
        public static bool Pending;
        static bool briefedFirstOperation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            briefedFirstOperation = false;
            MatchBootstrap.MatchReady -= OnMatchReady;
            MatchBootstrap.MatchReady += OnMatchReady;
        }

        static void OnMatchReady(MatchBootstrap mb)
        {
            if (mb.tutorialMode || mb.playerTeam != OperatorTeam.Attack) return;
            // the first operation always opens with the briefing, however it was reached (the tutorial skipped, from the HQ) — once a session, not on every retry
            bool talk = OpeningDue(), brief = Pending || (talk && !briefedFirstOperation);
            if (!brief && !talk) return;
            Pending = false;
            if (talk) briefedFirstOperation = true;
            var g = mb.GetComponent<TutorialDeployGuide>();
            if (g == null) g = mb.gameObject.AddComponent<TutorialDeployGuide>();
            g.mb = mb;
            g.StopAllCoroutines();
            if (talk && mb.ActiveTurns != null) mb.ActiveTurns.BeforeFirstRound = g.OpeningTalk;
            if (brief) g.StartCoroutine(g.Run());
        }

        // the story's first operation, not yet won
        static bool OpeningDue()
        {
            var first = Blindside.Progress.Campaign.FirstMain();
            var data = MapLibrary.Current;
            var save = Blindside.Save.SaveSystem.Current;
            return first != null && first.entry != null && !string.IsNullOrEmpty(data.operationCode)
                   && data.operationCode == first.entry.operationCode && (save == null || save.mainCleared == 0);
        }

        IEnumerator OpeningTalk()
        {
            TutorialGate.HoldInput = true;
            mb.selection.Deselect();   // (the unit panel sits where the radio does)
            var call = TutorialRadio.Build(transform);
            foreach (var (key, line) in Opening)
            {
                var op = BuildAssetRegistry.Operator(key);
                if (op != null) call.SetOperator(op.key, op.DisplayName, op.kr, op.color);
                call.Say(line, true);
                while (!call.Advanced) yield return null;
            }
            call.Close();
            Destroy(call.canvas.gameObject, .4f);
            TutorialGate.HoldInput = false;
        }

        MatchBootstrap mb;
        TutorialRadio radio;
        TutorialGuide guide;

        IEnumerator Run()
        {
            TutorialGate.HoldInput = true;
            var turns = mb.ActiveTurns;
            // the deployment opens once the DEPLOYMENT banner is gone
            float waited = 0f;
            while (turns != null && !turns.AwaitingDeploy && !turns.Over && waited < 15f) { waited += Time.unscaledDeltaTime; yield return null; }
            if (turns == null || turns.Over) { Finish(); yield break; }
            yield return Wait(.3f);
            mb.selection.Deselect();   // (the art card of the auto-selected unit would sit under ours)

            radio = TutorialRadio.Build(transform);
            guide = TutorialGuide.Build(transform);

            yield return Say(L1);

            // the hostage (no civilian on the map: the building): camera on it, marked
            var cam = BattlefieldCamera.Instance;
            var data = MapLibrary.Current;
            bool civilian = data.HasCivilian;
            var h = civilian ? new Vector2(data.hostage.x, data.hostage.y) : data.FacingTarget;
            var marker = TutorialMarker.Create(h, transform, UIPalette.Foe);
            if (cam != null) { BattlefieldCamera.InputLocked = true; cam.PanTo(h.x, h.y, 700f); }
            guide.TargetLabel(GridSpace.GridToWorld(h.x, h.y, 2.2f), civilian ? GHostage : GBuilding);
            yield return Say(civilian ? L2 : L2Foes);
            yield return Say(civilian ? L3 : L3Foes);
            guide.HideTarget();
            Destroy(marker.gameObject);
            BattlefieldCamera.InputLocked = false;

            yield return Say(LFree);
            radio.Close();
            yield return Wait(.3f);
            Finish();

            // the first unit still aboard is selected for its drop, as the deployment opening does
            foreach (var u in mb.ActiveUnits)
                if (u.Team == OperatorTeam.Attack && !u.Placed && u.Alive) { mb.selection.SelectUnit(u); break; }
        }

        void Finish()
        {
            TutorialGate.HoldInput = false;
            BattlefieldCamera.InputLocked = false;
            if (radio != null) Destroy(radio.canvas.gameObject, .4f);
            if (guide != null) Destroy(guide.GetComponentInParent<Canvas>().gameObject);
            radio = null; guide = null;
        }

        void OnDestroy() { if (TutorialGate.HoldInput) TutorialGate.HoldInput = false; }

        IEnumerator Say(string line)
        {
            radio.Say(line, true);
            while (!radio.Advanced) yield return null;
        }

        static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }
    }
}
