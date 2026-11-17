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

        // ---- the first operation teaches as it goes (the new game starts here: there is no training first) ----
        public const string LOp1 = "任務だ、新入り。\n今回は実戦の中で、\nやり方を教えてやる。";
        public const string LFixed = "今回の配置は俺が決めておいた。\nまずは実戦の流れを覚えろ。";
        public const string LTab = "突入する前に、敵の位置を確かめておけ。\nTabを長押ししてみろ。";
        public const string LTabDone = "赤い反応が敵だ。正確な位置ではないが、\n作戦を立てる手がかりになる。";
        public const string LStart = "よし、作戦開始だ。";
        public const string LMove = "まずは移動だ。隊員を選んで「移動」を選び、\nマウスでなぞって経路を描け。";
        public const string LMoveTry = "建物に近づいてみろ。";
        public const string LDemo = "まずは俺が手本を見せる。";
        public const string LFire = "次は射撃だ。マウスの方向で射撃方向を決め、\n左クリックで撃つ。手本を見せる。";
        public const string LFireDone = "敵が見えたら、同じように撃て。";
        public const string GMoveCmd = "移動", GFireCmd = "攻撃", GFaceCmd = "視界変更";
        public const string LMoveDone = "いいだろう。";
        public const string LLook = "次は視界だ。建物の中で戦う俺たちにとって、\n視界は命に直結する。";
        public const string LLookTry = "「視界変更」で向きを決めて左クリックすると、\nその向きで視界を固定できる。\n建物に向けて固定してみろ。";
        public const string LLookDone = "どちらが先に敵を見つけたかで、\n先制攻撃が決まる。気をつけろ。";
        public const string LFreePlay = "あとは自由にやれ。\n目標は敵の全員制圧だ。";
        public const string LSpot = "敵を発見した！ 隊員を選んで「攻撃」を選べ。\n狙いを合わせると攻撃予測が出る。\nよく見てから撃て。";

        /// <summary>The next attack-side match runs the briefing (set by the tutorial's ending).</summary>
        public static bool Pending;
        static bool briefedFirstOperation, taughtFirstOperation;
        bool lessons;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            briefedFirstOperation = false; taughtFirstOperation = false;
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
            // the first operation of a session teaches (a fixed deployment, the hologram, moving, the view, the first shot); a retry doesn't
            bool teach = talk && brief && !taughtFirstOperation;
            if (talk) briefedFirstOperation = true;
            if (teach) taughtFirstOperation = true;
            var g = mb.GetComponent<TutorialDeployGuide>();
            if (g == null) g = mb.gameObject.AddComponent<TutorialDeployGuide>();
            g.mb = mb;
            g.lessons = teach;
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
            if (lessons) { yield return FirstOperation(); yield break; }
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

        // ================================================================
        //  The first operation, taught as it is played: the deployment is CRUSHER's (fixed: [作戦開始] drops the squad on the map's own
        //  points), the hologram is tried, and in round 1 the controls are held to one command at a time (TutorialGate.Guided) — a move,
        //  then the view fixed — before the squad is the player's; the first enemy seen brings one line on shooting.
        // ================================================================
        IEnumerator FirstOperation()
        {
            TutorialGate.HoldInput = true;
            var turns = mb.ActiveTurns;
            float waited = 0f;
            while (turns != null && !turns.AwaitingDeploy && !turns.Over && waited < 15f) { waited += Time.unscaledDeltaTime; yield return null; }
            if (turns == null || turns.Over) { Finish(); yield break; }
            yield return Wait(.3f);
            mb.selection.Deselect();

            radio = TutorialRadio.Build(transform);
            guide = TutorialGuide.Build(transform);
            yield return Say(LOp1);

            // the target: camera on it, marked
            var cam = BattlefieldCamera.Instance;
            var data = MapLibrary.Current;
            bool civilian = data.HasCivilian;
            var h = civilian ? new Vector2(data.hostage.x, data.hostage.y) : data.FacingTarget;
            var marker = TutorialMarker.Create(h, transform, UIPalette.Foe);
            if (cam != null) { BattlefieldCamera.InputLocked = true; cam.PanTo(h.x, h.y, 700f); }
            guide.TargetLabel(GridSpace.GridToWorld(h.x, h.y, 2.2f), civilian ? GHostage : GBuilding);
            yield return Say(civilian ? L2 : L2Foes);
            guide.HideTarget();
            Destroy(marker.gameObject);
            BattlefieldCamera.InputLocked = false;
            yield return Say(LFixed);

            // the hologram: Tab held for a moment (or, after a while, on without it)
            radio.Say(LTab, false);
            float held = 0f; waited = 0f;
            while (held < .8f && waited < 25f && !turns.Over)
            {
                float dt = Time.unscaledDeltaTime;
                waited += dt;
                held = DeployHologram.Showing ? held + dt : 0f;
                yield return null;
            }
            while (DeployHologram.Showing && waited < 30f) { waited += Time.unscaledDeltaTime; yield return null; }   // (let go of Tab first)
            yield return Say(LTabDone);
            yield return Say(LStart);
            radio.Close();
            yield return Wait(.3f);
            Finish();

            // the fixed deployment: [作戦開始] drops every squad member on the map's points (the squad's opening talk follows, before round 1)
            var bridge = mb.uiBridge;
            if (bridge != null && bridge.endTurnButton != null) bridge.endTurnButton.onClick.Invoke();
            yield return Lessons();
        }

        IEnumerator Lessons()
        {
            var turns = mb.ActiveTurns;
            // round 1, the attack's turn, the opening talk over
            while (turns != null && !turns.Over && (turns.Phase != Blindside.Turns.MatchPhase.Attack || TutorialGate.HoldInput)) yield return null;
            if (turns == null || turns.Over) yield break;
            yield return Wait(.4f);
            mb.selection.Deselect();
            TutorialGate.HoldInput = true;
            radio = TutorialRadio.Build(transform);

            guide = TutorialGuide.Build(transform);
            var crusher = Squad("CRUSHER");
            var building = MapLibrary.Current.FacingTarget;
            TutorialGate.Guided = true;

            // ---- moving: CRUSHER shows how (the hand picks him, presses 移動, draws the path; he walks it), then the player ----
            yield return Say(LMove);
            if (crusher != null)
            {
                yield return Say(LDemo);
                radio.Close();
                TutorialGate.Allow("move");
                var dir = (building - new Vector2(crusher.X, crusher.Y));
                var path = dir.sqrMagnitude > .01f ? mb.selection.TutorialPlanPath(crusher, new Vector2(crusher.X, crusher.Y) + dir.normalized * 3f, 3.5f) : null;
                if (path != null)
                    yield return Demo(crusher, () => mb.selection.TutorialDemoMove(crusher, path, 1.6f, () => DemoPress("move", GMoveCmd)));
            }
            TutorialGate.Allow("move");
            radio.Say(LMoveTry, false);
            TutorialGate.HoldInput = false;
            bool moved = false;
            System.Action<SimUnit> onMove = u => { if (u != null && u.Team == OperatorTeam.Attack) moved = true; };
            TutorialGate.MoveFinished += onMove;
            while (!moved && InAttack(turns)) yield return null;
            TutorialGate.MoveFinished -= onMove;

            // ---- shooting: CRUSHER aims (the line sweeps) and fires a burst out of the way, away from the building ----
            if (InAttack(turns))
            {
                TutorialGate.HoldInput = true;
                yield return Say(LMoveDone);
                if (crusher != null && crusher.Alive)
                {
                    yield return Say(LFire);
                    radio.Close();
                    TutorialGate.Allow("fire");
                    float away = Mathf.Atan2(crusher.Y - building.y, crusher.X - building.x);
                    yield return Demo(crusher, () => mb.selection.TutorialDemoFire(crusher, away - .5f, away + .3f, 1.8f, true, () => DemoPress("fire", GFireCmd), () => guide.Click()));
                    yield return Say(LFireDone);
                }
            }

            // ---- the view, fixed: CRUSHER turns his view to the building and fixes it, then the player ----
            if (InAttack(turns))
            {
                TutorialGate.HoldInput = true;
                yield return Say(LLook);
                if (crusher != null && crusher.Alive)
                {
                    yield return Say(LDemo);
                    radio.Close();
                    TutorialGate.Allow("face");
                    float toward = Mathf.Atan2(building.y - crusher.Y, building.x - crusher.X);
                    yield return Demo(crusher, () => mb.selection.TutorialDemoFace(crusher, crusher.Facing, toward, 1.4f, () => DemoPress("face", GFaceCmd), () => guide.Click()));
                    crusher.LockFacing = false;   // (the demonstration leaves him as he was: the player's lock is the one that counts)
                }
                TutorialGate.Allow("face");
                radio.Say(LLookTry, false);
                TutorialGate.HoldInput = false;
                float waited = 0f;
                while (InAttack(turns) && !AnyLocked() && waited < 90f) { waited += Time.unscaledDeltaTime; yield return null; }
                if (InAttack(turns))
                {
                    TutorialGate.HoldInput = true;
                    yield return Say(LLookDone);
                }
            }

            // ---- the squad is the player's ----
            TutorialGate.Guided = false;
            TutorialGate.Allow();
            if (!turns.Over)
            {
                TutorialGate.HoldInput = true;
                yield return Say(LFreePlay);
            }
            radio.Close();
            TutorialGate.HoldInput = false;

            // ---- the first enemy seen: one line on shooting ----
            while (!turns.Over && !(turns.Phase == Blindside.Turns.MatchPhase.Attack && AnyEnemyShown())) yield return null;
            if (turns.Over) { Finish(); yield break; }
            yield return Wait(.3f);
            TutorialGate.HoldInput = true;
            yield return Say(LSpot);
            radio.Close();
            yield return Wait(.3f);
            Finish();
        }

        static bool InAttack(Blindside.Turns.TurnManager t) => t != null && !t.Over && t.Phase == Blindside.Turns.MatchPhase.Attack;

        SimUnit Squad(string key)
        {
            foreach (var u in mb.ActiveUnits) if (u.Team == OperatorTeam.Attack && u.Alive && u.Op != null && u.Op.key == key) return u;
            return null;
        }

        // ---- the demonstrations (as the training room's): the hand clicks the unit, presses the command, then rides the path's tip / the
        // aim's end / the view's edge. A demonstration is free: the unit's move, action and turn are given back after it ----
        IEnumerator Demo(SimUnit who, System.Func<IEnumerator> play)
        {
            float moveLeft = who.MoveLeft; bool acted = who.Acted, ended = who.Ended;
            yield return DemoSelect(who);
            var follow = StartCoroutine(FollowDemoCursor());
            yield return play();
            StopCoroutine(follow);
            guide.HideCursor();
            guide.HideFrame();
            TutorialGate.Allow();
            who.MoveLeft = moveLeft; who.Acted = acted; who.Ended = ended;
            mb.selection.Deselect();
            yield return Wait(.4f);
        }

        IEnumerator DemoSelect(SimUnit who)
        {
            var cam = Camera.main;
            if (cam == null || who.View == null) yield break;
            if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.PanTo(who.X, who.Y, 450f);
            yield return Wait(.5f);
            var s = cam.WorldToScreenPoint(who.View.LabelPoint);
            guide.CursorTo(new Vector2(s.x + 160f, s.y - 120f), true);
            yield return Wait(.15f);
            guide.CursorTo(new Vector2(s.x, s.y));
            yield return Wait(.55f);
            guide.Click();
            yield return Wait(.25f);
        }

        IEnumerator DemoPress(string command, string label)
        {
            var btn = mb.selection.MenuButtonRect(command);
            if (btn == null) yield break;
            guide.FrameRect(btn, label);
            guide.CursorToRect(btn);
            yield return Wait(.6f);
            guide.Click();
            yield return Wait(.35f);
            guide.HideFrame();
        }

        IEnumerator FollowDemoCursor()
        {
            bool pressed = false;
            while (true)
            {
                var g = mb.selection.DemoCursorGrid;
                var cam = Camera.main;
                if (g.HasValue && cam != null)
                {
                    var s = cam.WorldToScreenPoint(GridSpace.GridToWorld(g.Value.x, g.Value.y, 0f));
                    guide.CursorTo(new Vector2(s.x, s.y));
                    if (!pressed) { pressed = true; guide.Click(); }   // (the press that starts the drag / the aim)
                }
                yield return null;
            }
        }

        // the radio sits bottom left: over the unit panel while that is up (as the tutorial's and the squad's radio do)
        void LateUpdate()
        {
            if (radio != null && mb != null && mb.selection != null) radio.Lift = mb.selection.UnitPanelUp ? 236f : 0f;
        }

        bool AnyLocked()
        {
            foreach (var u in mb.ActiveUnits) if (u.Team == OperatorTeam.Attack && u.Alive && u.LockFacing) return true;
            return false;
        }

        bool AnyEnemyShown()
        {
            if (mb.visibility == null) return false;
            foreach (var u in mb.ActiveUnits)
                if (u.Team != OperatorTeam.Attack && u.Alive && mb.visibility.ShownToPlayer(u)) return true;
            return false;
        }

        void Finish()
        {
            TutorialGate.Guided = false;
            TutorialGate.HoldInput = false;
            BattlefieldCamera.InputLocked = false;
            if (radio != null) Destroy(radio.canvas.gameObject, .4f);
            if (guide != null) Destroy(guide.GetComponentInParent<Canvas>().gameObject);
            radio = null; guide = null;
        }

        void OnDestroy() { if (TutorialGate.HoldInput) TutorialGate.HoldInput = false; TutorialGate.Guided = false; }

        IEnumerator Say(string line)
        {
            if (radio == null || !radio.IsOpen) radio = radio != null ? radio : TutorialRadio.Build(transform);
            radio.Say(line, true);
            while (!radio.Advanced) yield return null;
        }

        static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }
    }
}
