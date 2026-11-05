// The deployment briefing that follows the tutorial into the real operation: when the tutorial's ending launches the default map
// (Pending), the next attack-side match's deployment is held (TutorialGate.HoldInput) while CRUSHER, on the radio, points out the
// hostage (camera on it, a red marker and chip) and introduces the squad one by one (TutorialOperatorCard on the right: illustration,
// name, ability) — then the player is free to deploy.
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
        /// <summary>Who CRUSHER introduces, in order, and what he says (FLARE is the player: already known).</summary>
        public static readonly (string key, string line)[] Intros =
        {
            ("CRUSHER", "まずは俺だ。\n俺はハンマーで通常の壁を破壊できる。"),
            ("IGNIS", "この隊員はイグニス。\n強化壁を一度で破壊できる。"),
            ("SERUM", "次はセラム。アドレナリンで味方を治療し、\n行動範囲を広げてくれる。"),
            ("BASTION", "最後にバスティオン。\nシールドで敵の攻撃を防ぐことができる。"),
        };

        /// <summary>The next attack-side match runs the briefing (set by the tutorial's ending).</summary>
        public static bool Pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            MatchBootstrap.MatchReady -= OnMatchReady;
            MatchBootstrap.MatchReady += OnMatchReady;
        }

        static void OnMatchReady(MatchBootstrap mb)
        {
            if (!Pending || mb.tutorialMode || mb.playerTeam != OperatorTeam.Attack) return;
            Pending = false;
            var g = mb.GetComponent<TutorialDeployGuide>();
            if (g == null) g = mb.gameObject.AddComponent<TutorialDeployGuide>();
            g.mb = mb;
            g.StopAllCoroutines();
            g.StartCoroutine(g.Run());
        }

        MatchBootstrap mb;
        TutorialRadio radio;
        TutorialGuide guide;
        TutorialOperatorCard card;

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
            card = TutorialOperatorCard.Build(radio.canvas.transform);

            yield return Say(L1);

            // the hostage: camera on it, marked
            var cam = BattlefieldCamera.Instance;
            var h = MapLibrary.Current.hostage;
            var marker = TutorialMarker.Create(new Vector2(h.x, h.y), transform, UIPalette.Foe);
            if (cam != null) { BattlefieldCamera.InputLocked = true; cam.PanTo(h.x, h.y, 700f); }
            guide.TargetLabel(GridSpace.GridToWorld(h.x, h.y, 2.2f), GHostage);
            yield return Say(L2);
            yield return Say(L3);
            guide.HideTarget();
            Destroy(marker.gameObject);
            BattlefieldCamera.InputLocked = false;

            // the squad
            foreach (var (key, line) in Intros)
            {
                var op = FindOperator(key);
                if (op != null) card.Show(op);
                yield return Say(line);
            }
            card.Hide();
            yield return Say(LFree);
            radio.Close();
            yield return Wait(.3f);
            Finish();

            // the first unit still aboard is selected for its drop, as the deployment opening does
            foreach (var u in mb.ActiveUnits)
                if (u.Team == OperatorTeam.Attack && !u.Placed && u.Alive) { mb.selection.SelectUnit(u); break; }
        }

        OperatorData FindOperator(string key)
        {
            if (mb.ActiveUnits == null) return null;
            foreach (var u in mb.ActiveUnits) if (u.Op != null && u.Op.key == key) return u.Op;
            return null;
        }

        void Finish()
        {
            TutorialGate.HoldInput = false;
            BattlefieldCamera.InputLocked = false;
            if (radio != null) Destroy(radio.canvas.gameObject, .4f);
            if (guide != null) Destroy(guide.GetComponentInParent<Canvas>().gameObject);
            radio = null; guide = null; card = null;
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
