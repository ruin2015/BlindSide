// The rescue operation's deployment briefing (MatchBootstrap.RescueMatch: GHOST and SILENCE held by the enemy): before the squad is
// placed, CRUSHER on the radio points out a wall carrying current (camera on it, a blue marker and chip) — while it does, neither FLARE's
// nor IGNIS's ability breaks the reinforced wall; the gadget feeding it has to be shot, or an EMP used — then says SILENCE, who has the
// EMP, is missing (camera on where she was last reported: a marker and chip, her figure still hidden), and that GHOST senses heartbeats
// through walls. Then the deployment is the player's. Once a session (a retry goes straight to the deployment).
// With no electrified wall on the map the wall's part is left out. Lines are CRUSHER's (Japanese, ko_ja_dictionary / glossary_ja terms).
using System.Collections;
using UnityEngine;
using Blindside.Data;
using Blindside.Integration;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialRescueBrief : MonoBehaviour
    {
        public const string L1 = "ああ、新入り。\nもう一つ教えてやろう。";
        public const string LSpark = "今、火花が散っている壁が見えるか？";
        public const string LSpark2 = "この壁がある限り、\nお前やイグニスの能力では\n強化壁を破れない。";
        public const string LSpark3 = "あれを取り除くには、\nガジェットを銃で壊すか、\nEMPを使うしかない。";
        public const string LMia = "だが今、EMPを使えるサイレンスは\nMIAになっているな……";
        public const string LLast = "最後に報告を受けた位置はあそこだ。\n先に救出できれば、なお良いだろう。";
        public const string LGhost = "ちなみにもう一人、ゴーストは\n壁の向こうにいる敵の心拍を\n感知できる。";
        public const string LGhost2 = "救出すれば、敵の位置情報を\nつかみやすくなるだろう。";
        public const string GSpark = "通電中の強化壁";
        public const string GLast = "サイレンスの最終報告地点";
        public const string GGhost = "ゴーストの最終報告地点";
        static readonly Color SparkBlue = new Color(.31f, .7f, 1f);

        static bool briefed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            briefed = false;
            MatchBootstrap.MatchReady -= OnMatchReady;
            MatchBootstrap.MatchReady += OnMatchReady;
        }

        static void OnMatchReady(MatchBootstrap mb)
        {
            if (briefed || mb.tutorialMode || mb.playerTeam != OperatorTeam.Attack || !MatchBootstrap.RescueMatch) return;
            briefed = true;
            var b = mb.GetComponent<TutorialRescueBrief>();
            if (b == null) b = mb.gameObject.AddComponent<TutorialRescueBrief>();
            b.mb = mb;
            b.StopAllCoroutines();
            b.StartCoroutine(b.Run());
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
            mb.selection.Deselect();   // (the unit panel sits where the radio does)

            radio = TutorialRadio.Build(transform);
            guide = TutorialGuide.Build(transform);
            var cam = BattlefieldCamera.Instance;
            yield return Say(L1);

            // the wall carrying current: the one nearest the squad's drop points
            if (SparkWall(out var wall))
            {
                var m = TutorialMarker.Create(wall, transform, SparkBlue);
                if (cam != null) { BattlefieldCamera.InputLocked = true; cam.PanTo(wall.x, wall.y, 700f); }
                yield return Wait(.5f);
                guide.TargetLabel(GridSpace.GridToWorld(wall.x, wall.y, 2.4f), GSpark);
                yield return Say(LSpark);
                yield return Say(LSpark2);
                yield return Say(LSpark3);
                guide.HideTarget();
                Destroy(m.gameObject);
            }

            yield return Say(LMia);
            // where SILENCE was last reported: her cell marked (the figure stays hidden until the squad's sight finds her)
            var silence = Captive("SILENCE");
            if (silence != null)
            {
                var at = new Vector2(silence.X, silence.Y);
                var m = TutorialMarker.Create(at, transform, TutorialMarker.Destination);
                if (cam != null) { BattlefieldCamera.InputLocked = true; cam.PanTo(at.x, at.y, 800f); }
                yield return Wait(.6f);
                guide.TargetLabel(GridSpace.GridToWorld(at.x, at.y, 2.2f), GLast);
                yield return Say(LLast);
                guide.HideTarget();
                Destroy(m.gameObject);
            }
            else yield return Say(LLast);
            // GHOST: where he is held, the same way
            var ghost = Captive("GHOST");
            if (ghost != null)
            {
                var at = new Vector2(ghost.X, ghost.Y);
                var m = TutorialMarker.Create(at, transform, TutorialMarker.Destination);
                if (cam != null) { BattlefieldCamera.InputLocked = true; cam.PanTo(at.x, at.y, 800f); }
                yield return Wait(.6f);
                guide.TargetLabel(GridSpace.GridToWorld(at.x, at.y, 2.2f), GGhost);
                yield return Say(LGhost);
                yield return Say(LGhost2);
                guide.HideTarget();
                Destroy(m.gameObject);
            }
            else { yield return Say(LGhost); yield return Say(LGhost2); }
            BattlefieldCamera.InputLocked = false;
            radio.Close();
            yield return Wait(.3f);
            Finish();

            // the first unit still aboard is selected for its drop, as the deployment opening does
            foreach (var u in mb.ActiveUnits)
                if (u.Team == OperatorTeam.Attack && !u.Placed && !u.Captive && u.Alive) { mb.selection.SelectUnit(u); break; }
        }

        /// <summary>The centre of the electrified, still-standing panel nearest the squad's drop points.</summary>
        bool SparkWall(out Vector2 at)
        {
            at = default;
            var map = mb.ActiveMap;
            var data = MapLibrary.Current;
            if (map == null || map.Panels == null) return false;
            Vector2 drop = Vector2.zero;
            int n = 0;
            if (data != null && data.anchors != null && data.anchors.attackerDrops != null)
                foreach (var d in data.anchors.attackerDrops) { drop += new Vector2(d.x, d.y); n++; }
            if (n > 0) drop /= n;
            float best = float.MaxValue;
            bool found = false;
            foreach (var p in map.Panels)
            {
                if (!p.electrified || p.cells == null || p.cells.Count == 0 || map.TileAt(p.cells[0].x, p.cells[0].y) != TileType.Wall) continue;
                var c = new Vector2(p.centerX, p.centerY);
                float dd = n > 0 ? (c - drop).sqrMagnitude : 0f;
                if (dd < best) { best = dd; at = c; found = true; }
            }
            return found;
        }

        SimUnit Captive(string key)
        {
            foreach (var u in mb.ActiveUnits) if (u.Captive && u.Alive && u.Op != null && u.Op.key == key) return u;
            return null;
        }

        void LateUpdate()
        {
            if (radio != null && mb != null && mb.selection != null) radio.Lift = mb.selection.TutorialRadioLift;
        }

        void Finish()
        {
            TutorialGate.HoldInput = false;
            BattlefieldCamera.InputLocked = false;
            if (radio != null) Destroy(radio.canvas.gameObject, .4f);
            if (guide != null) Destroy(guide.GetComponentInParent<Canvas>().gameObject);
            radio = null; guide = null;
        }

        void OnDestroy() { if (radio != null || guide != null) Finish(); }

        IEnumerator Say(string line)
        {
            radio.Say(line, true);
            while (!radio.Advanced) yield return null;
        }

        static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }
    }
}
