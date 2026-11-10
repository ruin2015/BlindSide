// Runs the tutorial: started from the title's チュートリアル (TitleSelectScreen.LaunchTutorial → Prepare), it waits for the match
// MatchBootstrap sets up in tutorial mode (MatchReady) and then plays the lessons as one coroutine, holding the controls through
// TutorialGate and talking through TutorialRadio. Every lesson that the player can fail resets the unit and tries again.
//   stage 1  greeting · movement: a demonstration path draws itself and is walked, then a straight move to a marked point, then a
//            move round the low wall that has to be drawn by hand (no click auto path)
// Lines are CRUSHER's (Japanese, ko_ja_dictionary / glossary_ja terms).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Combat;
using Blindside.Data;
using Blindside.Integration;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialDirector : MonoBehaviour
    {
        // ---- CRUSHER's lines ----
        // (broken by hand at phrase boundaries, at most ~20 characters a line — the radio's width at its type size)
        public const string LWelcome = "よく来たな、新入り。\n我々の部隊へようこそ。";
        public const string LUnit = "我々の部隊はCQBを主体とし、\n建物内に潜む敵の位置を突き止めて突入する。";
        public const string LTeach = "これから、そのやり方を教えてやる。";
        public const string LMoveIntro = "まずは移動について説明する。\nマウスで直接なぞることで、\n移動経路を設定できる。";
        public const string LMove1 = "目標地点が見えるか？\nそこまで移動してみろ。";
        public const string LMoveFail = "がっかりだな。最近の新入りは、\n移動ひとつまともにできないのか？";
        public const string LMove2 = "いいだろう。今度は直線ではなく、\n目標地点まで自分で経路を描いてみろ。";
        public const string LMove2Done = "いいだろう。伊達にうちの部隊に\n来たわけじゃない、ということか。";
        public const string LFireIntro = "よし、次は射撃について説明する。";
        public const string LFireHow = "マウスの方向で射撃方向を決め、\n左クリックで撃つことができる。";
        public const string LCam = "練習として、\nそこにある監視カメラを破壊してみろ。";
        public const string LCamFail = "止まっているもの一つ当てられないで、\nどうやって動く敵を撃つつもりだ？";
        public const string LCamDone = "そうだ。監視カメラは敵に情報を渡す。\n破壊しておくに越したことはない。";
        public const string LDuel = "次は対人戦だ。\n練習として、俺を撃ってみろ。";
        public const string LDuelFail = "迷っているのか？ そんな気持ちで、\nどうやって戦場に立つつもりだ？";
        public const string LDuelDone = "いいだろう。\nこうして敵と撃ち合うことができる。";
        public const string LWalls = "ちなみに……銃には、\n貫通できる壁とできない壁がある。";
        public const string LWallsTip = "貫通できる壁の向こうに\n敵がいると思ったら、\n一発撃ってみるのも悪くないだろう。";
        public const string LDummy = "試しに、壁の向こうの\n訓練用ダミーを撃ってみろ。";
        public const string LDummyFail = "よく狙え。\n壁越しでも、狙う方向は同じだ。";
        public const string LDummyDone = "そうだ。壁越しでも当てられる。\nただし命中率は下がるから気をつけろ。";
        public const string LVisIntro = "次は、最も重要な\n視界について説明する。";
        public const string LVisShow = "見えるか？\nこれが今、お前に見えている範囲だ。";
        public const string LVisLife = "建物内で戦う俺たちにとって、\n視界は命に直結する。";
        public const string LHide = "俺が隠れてみるから、\n俺の視界を避けて移動し、\n攻撃してみろ。";
        public const string LHideFail = "どこを見ている？\n俺はここだ。";
        public const string LHideDone = "いいだろう。どちらが先に\n敵を視認したかで、先制攻撃が決まる。\n気をつけろ。";
        public const string LLock = "視界を固定したまま\n移動することもできる。やってみろ。";
        public const string LLockDone = "そうだ。視界を固定すれば、\n警戒すべき方向を見たまま動ける。";
        public const string LDrone1 = "俺たちにはドローンという装備がある。\nこれを使えば、建物の内部を偵察できる。";
        public const string LDrone2 = "俺が隠れてみるから、\nドローンで俺の位置を突き止めてみろ。";
        public const string LDroneDone = "いいだろう。こうして敵の位置を\n把握してから突入するんだ。";
        public const string LBreach1 = "今度は壁の破壊について教えてやる。";
        public const string LBreach2 = "建物には三種類の壁がある。\n強化壁、破壊できない壁、破壊できる壁だ。";
        public const string LBreach3 = "通常の壁は銃で貫通できるし、\n能力で破壊することもできる。";
        public const string LBreach4 = "一度手本を見せてやる。";
        public const string LBreach5 = "強化壁は、普段は破壊も貫通もできないが、\n能力でなら破壊できる。";
        public const string LBreach6 = "そうだ、お前の能力なら\n強化壁を破壊できる。やってみろ。";
        public const string LBreach7 = "視界が広がっただろう？\nもう一度使えば突入までできる。\nやってみろ。";
        public const string LBreach8 = "こうして壁を破壊して、\n視界を広げていけ。";
        public const string LEnd1 = "俺が教えられるのはここまでだ。";
        public const string LRadio = "現在、テロリストが民間人1名を拉致。\n速やかに移動されたし。";
        public const string LFinal = "よし、新入り。いよいよ実戦だ。\n期待しているぞ。";
        // ---- guide chips ----
        public const string GDrone = "「ドローン」をクリック";
        public const string GPickWall = "強化壁をクリック";
        public const string GPickBreakable = "壁をクリック";
        public const string GWallReinforced = "強化壁";
        public const string GWallUnbreakable = "破壊できない壁";
        public const string GWallBreakable = "破壊できる壁";
        public const string GView = "「視界変更」をクリック";
        public const string GLock = "[F] 視界固定をオン";
        public const string GSelect = "フレアをクリック";
        public const string GMove = "「移動」をクリック";
        public const string GFire = "「攻撃」をクリック";
        public const string GTarget = "目標地点";
        public const string GCamera = "監視カメラ";
        public const string GCrusher = "クラッシャー";
        public const string GWallThrough = "貫通できる壁";
        public const string GWallSolid = "貫通できない壁";
        // ---- the chip beside the player's own pointer ----
        public const string TFire = "左クリックで射撃";
        public const string TDraw = "左ボタンを押したままドラッグ";
        public const string TDrawOrClick = "ドラッグで経路 · クリックで自動経路";

        // ---- the objective card (right): what to do, and the controls that do it ----
        public const string OMove1 = "目標地点に向かって移動しよう";
        public const string OMove2 = "壁を回り込み、目標地点まで\n自分で経路を描いて移動しよう";
        public const string OCam = "監視カメラを撃って破壊しよう";
        public const string ODuel = "クラッシャーを撃ってみよう";
        public const string ODummy = "壁越しに訓練用ダミーを撃とう";
        public const string OHide = "クラッシャーの視界を避け、\n先に発見して攻撃しよう";
        public const string OLock = "視界を固定したまま移動しよう";
        public const string ODrone = "ドローンでクラッシャーの\n位置を突き止めよう";
        public const string OLauncher = "能力で強化壁を破壊しよう";
        public static readonly (string, string)[] CMove1 =
            { ("左クリック", "フレアを選択"), ("「移動」", "移動モードにする"), ("ドラッグ", "経路を描いて移動"), ("クリック", "自動経路で移動"), ("ESC", "キャンセル") };
        static readonly (string, string)[] CMove2 =
            { ("左クリック", "フレアを選択"), ("「移動」", "移動モードにする"), ("ドラッグ", "壁を避けて経路を描く"), ("ESC", "キャンセル") };
        public static readonly (string, string)[] CFire =
            { ("左クリック", "フレアを選択"), ("「攻撃」", "射撃モードにする"), ("マウス", "射撃方向を決める"), ("左クリック", "射撃"), ("ESC", "キャンセル") };
        static readonly (string, string)[] CHide =
            { ("左クリック", "フレアを選択"), ("「移動」", "視界を避けて経路を描く"), ("「攻撃」", "先に発見したら攻撃") };
        static readonly (string, string)[] CLock =
            { ("「視界変更」", "向きを変える"), ("F", "視界固定をオン"), ("左クリック", "向きを確定"), ("「移動」", "向きを保って移動") };
        static readonly (string, string)[] CDrone =
            { ("左クリック", "フレアを選択"), ("「ドローン」", "ドローンを展開"), ("ドラッグ", "飛行経路を描く"), ("ESC", "キャンセル") };

        MatchBootstrap mb;
        PlayerSelectionController sel;
        TutorialRadio radio;
        TutorialGuide guide;
        TutorialObjective objective;
        SimUnit flare, crusher;
        bool moveDone;

        /// <summary>Arms the tutorial on `bootstrap`'s object (the next match it runs is the tutorial's).</summary>
        public static TutorialDirector Prepare(MatchBootstrap bootstrap)
        {
            var d = bootstrap.GetComponent<TutorialDirector>();
            if (d == null) d = bootstrap.gameObject.AddComponent<TutorialDirector>();
            d.mb = bootstrap;
            TutorialGate.Reset();
            TutorialGate.Active = true;
            TutorialGate.BlockInput = true;
            MatchBootstrap.MatchReady -= d.OnMatchReady;
            MatchBootstrap.MatchReady += d.OnMatchReady;
            return d;
        }

        /// <summary>Disarms it (a normal match is about to start).</summary>
        public static void Disarm(MatchBootstrap bootstrap)
        {
            TutorialGate.Reset();
            if (bootstrap == null) return;
            bootstrap.tutorialMode = false;
            var d = bootstrap.GetComponent<TutorialDirector>();
            if (d != null) d.Stop();
        }

        void Stop()
        {
            MatchBootstrap.MatchReady -= OnMatchReady;
            StopAllCoroutines();
            if (radio != null) Destroy(radio.canvas.gameObject);
            if (guide != null) Destroy(guide.GetComponentInParent<Canvas>().gameObject);
            TutorialGate.MoveFinished -= OnMoveFinished;
            TutorialGate.ShotFinished -= OnShotFinished;
            if (sel != null && sel.Movement != null) sel.Movement.EncounterResolved -= OnEncounter;
            TutorialGate.DroneFinished -= OnDroneFinished;
            if (mb != null && mb.visionPolys != null) { mb.visionPolys.ForceShow = null; mb.visionPolys.ForceHide = null; mb.visionPolys.Refresh(); }
            if (entranceOn)
            {
                // stopped half way through the entrance: what it switched off comes back
                ShowHud(entranceHud);
                if (entranceFog != null) entranceFog.SetActive(entranceFogWas);
                MapWallSync.ForceLit = false;
                entranceOn = false;
            }
            if (stage != null) { stage.Revert(); stage = null; }
            CombatSystem.FloorHP = 0;
            radio = null; guide = null; objective = null;
            foreach (var m in GetComponentsInChildren<TutorialMarker>()) Destroy(m.gameObject);
        }

        void OnDestroy() => MatchBootstrap.MatchReady -= OnMatchReady;

        Canvas guideCanvas;
        /// <summary>The unit panel's height with its face poking up (190 + 30) and a gap.</summary>
        public const float RadioLiftOverPanel = 236f;

        // the radio and the guide step aside while a battle cinematic is on screen (it hides the HUD the same way)
        void LateUpdate()
        {
            if (radio == null || mb == null) return;
            bool show = !entranceOn && (mb.battleDirector == null || !mb.battleDirector.Directing);
            radio.Lift = sel != null && sel.UnitPanelUp ? RadioLiftOverPanel : 0f;   // (bottom left, over the unit panel while it's up)
            if (radio.canvas.enabled != show) radio.canvas.enabled = show;
            if (guideCanvas == null && guide != null) guideCanvas = guide.GetComponentInParent<Canvas>();
            if (guideCanvas != null && guideCanvas.enabled != show) guideCanvas.enabled = show;
            if (TutorialGate.BlockInput && guide != null) guide.HideMouseTip();   // (a lesson over: no pointer chip)
        }

        void OnMatchReady(MatchBootstrap b)
        {
            if (b != mb || !b.tutorialMode) return;
            MatchBootstrap.MatchReady -= OnMatchReady;
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        // ================================================================
        //  The lessons
        // ================================================================
        IEnumerator Run()
        {
            sel = mb.selection;
            foreach (var u in mb.ActiveUnits)
            {
                if (u.Team == Blindside.Data.OperatorTeam.Attack) flare = u;
                else crusher = u;
            }
            if (radio == null)
            {
                radio = TutorialRadio.Build(transform);
                guide = TutorialGuide.Build(transform);
                objective = TutorialObjective.Build(radio.canvas.transform);
                skipButton = TutorialSkipButton.Build(guide.GetComponentInParent<Canvas>().transform);
                skipButton.OnSkip = Skip;
            }
            skipping = false;
            skipButton.gameObject.SetActive(true);
            skipButton.ResetHold();
            TutorialGate.MoveFinished -= OnMoveFinished;
            TutorialGate.MoveFinished += OnMoveFinished;
            TutorialGate.ShotFinished -= OnShotFinished;
            TutorialGate.ShotFinished += OnShotFinished;
            if (sel.Movement != null) { sel.Movement.EncounterResolved -= OnEncounter; sel.Movement.EncounterResolved += OnEncounter; }
            TutorialGate.DroneFinished -= OnDroneFinished;
            TutorialGate.DroneFinished += OnDroneFinished;
            TutorialGate.BlockInput = true;
            TutorialGate.Allow();

            // the training hall replaces the town
            if (stage == null) stage = TutorialStage.Apply(transform, mb.ActiveMap, MapLibrary.Current);

            // the camera on the yard (where the entrance shot hands over)
            var cam = BattlefieldCamera.Instance;
            if (cam != null) { cam.x = 6f; cam.y = 12.5f; cam.h = 20f; cam.Apply(); }
            if (StartStage <= 1) yield return Entrance();
            else
            {
                // (checks only: no entrance to fade the black in)
                var fade = GameObject.Find("BlindsideCanvas/Fade");
                var cg = fade != null ? fade.GetComponent<CanvasGroup>() : null;
                if (cg != null) cg.alpha = 0f;
                yield return Wait(.5f);
            }

            CombatSystem.FloorHP = 1;   // nobody falls in practice
            if (StartStage <= 1) yield return Stage1();
            else
            {
                // (checks: straight into a later stage, FLARE where stage 1 leaves it)
                flare.MoveTo(TutorialMap.Move2.x, TutorialMap.Move2.y);
                if (cam != null) cam.PanTo(flare.X, flare.Y, 300f);
                yield return Wait(.4f);
            }
            if (StartStage <= 2) yield return Stage2();
            else
            {
                // (checks: CRUSHER where stage 2 leaves him)
                crusher.MoveTo(TutorialMap.DuelSpot.x, TutorialMap.DuelSpot.y);
                crusher.Facing = Mathf.Atan2(flare.Y - crusher.Y, flare.X - crusher.X);
                crusher.Placed = true;
            }
            if (StartStage <= 3) yield return Stage3();
            else
            {
                // (checks: CRUSHER where stage 3 leaves him)
                crusher.MoveTo(TutorialMap.HideSpot.x, TutorialMap.HideSpot.y);
                crusher.Facing = TutorialMap.HideFacing;
            }
            yield return Stage4();
            yield return Ending();
        }

        // ================= stage 4: drone, breaching =================
        IEnumerator Stage4()
        {
            var cam = BattlefieldCamera.Instance;
            // ---- the drone ----
            if (droneCard == null) droneCard = TutorialDroneCard.Build(radio.canvas.transform);
            droneCard.Play();
            yield return Say(LDrone1);
            droneCard.Hide();
            yield return Say(LDrone2);
            radio.Close();
            // he goes into the south-west room; FLARE takes up a spot outside its west door
            if (cam != null) cam.PanTo(12f, 13.5f, 500f);
            yield return WalkUnit(crusher, TutorialMap.DroneHideRoute, 2.6f);
            crusher.Facing = Mathf.Atan2(13f - crusher.Y, 14f - crusher.X);   // toward the door
            // (north of the low wall: round its east end, never through it)
            var here = new Vector2(flare.X, flare.Y);
            var toStart = here.y < TutorialMap.MoveWallY + .6f
                ? new[] { here, new Vector2(TutorialMap.MoveWallX1 + 1.6f, here.y), new Vector2(TutorialMap.MoveWallX1 + 1.6f, 12f), TutorialMap.DroneStart }
                : new[] { here, TutorialMap.DroneStart };
            yield return WalkUnit(flare, toStart, 2.6f);
            flare.Facing = 0f;
            yield return DroneLesson();
            yield return Say(LDroneDone);

            // ---- walls ----
            yield return Say(LBreach1);
            // the three kinds at a glance while he names them
            WallCard().Show(HWallKinds, TutorialWallCard.Kind.Reinforced, TutorialWallCard.Kind.Solid, TutorialWallCard.Kind.Breakable);
            yield return Say(LBreach2);
            wallCard.Hide();
            radio.Close();
            yield return WallKindsTour();
            yield return Say(LBreach3);
            yield return Say(LBreach4);
            radio.Close();
            yield return HammerDemo();
            // FLARE's own gadget on the right while he tells her what it does (kept up through LBreach6's line)
            GadgetCard().Play();
            yield return Say(LBreach5);
            // FLARE goes in through the door, the reinforced stretch in front of her
            radio.Close();
            if (cam != null) cam.PanTo(15f, 11.5f, 500f);
            var route = new Vector2[TutorialMap.LauncherRoute.Length];
            TutorialMap.LauncherRoute.CopyTo(route, 0);
            route[0] = new Vector2(flare.X, flare.Y);
            yield return WalkUnit(flare, route, 2.6f);
            flare.Facing = Mathf.Atan2(TutorialMap.ReinforcedCentre.y - flare.Y, TutorialMap.ReinforcedCentre.x - flare.X);
            yield return LauncherLesson(LBreach6, TileType.Hole);
            yield return LauncherLesson(LBreach7, TileType.Rubble);
            yield return Say(LBreach8);
        }

        TutorialDroneCard droneCard;
        TutorialSiren siren;
        TutorialWallCard wallCard;
        public const string HWallsShot = "銃弾が貫通する壁と、貫通しない壁";
        public const string HWallKinds = "建物にある三種類の壁";

        TutorialGadgetCard gadgetCard;
        TutorialGadgetCard GadgetCard()
        {
            if (gadgetCard == null)
                gadgetCard = TutorialGadgetCard.Build(radio.canvas.transform, flare.Op != null ? flare.Op.abilityName : null, flare.Op != null ? flare.Op.charges : 4);
            return gadgetCard;
        }

        TutorialWallCard WallCard()
        {
            if (wallCard == null) wallCard = TutorialWallCard.Build(radio.canvas.transform);
            return wallCard;
        }

        /// <summary>Only DRONE is offered; done when, during a flight, the drone had CRUSHER in its circle of sight. A flight that ends
        /// without finding him is answered with LHideFail (camera on him), and the drone is taken back for another try.</summary>
        IEnumerator DroneLesson()
        {
            var seesFn = mb.ActiveVision != null ? mb.ActiveVision.GadgetSees : null;
            objective.Set(ODrone, CDrone);
            while (true)
            {
                ResetDrone();
                TutorialGate.Allow("gadget");
                radio.Say(LDrone2, false);
                droneDone = false;
                bool found = false;
                TutorialGate.BlockInput = false;
                while (!droneDone)
                {
                    if (seesFn != null && seesFn(OperatorTeam.Attack, crusher.X, crusher.Y)) found = true;
                    PointGuide("gadget", GDrone);
                    yield return null;
                }
                if (seesFn != null && seesFn(OperatorTeam.Attack, crusher.X, crusher.Y)) found = true;
                TutorialGate.BlockInput = true;
                TutorialGate.Allow();
                guide.HideFrame();
                yield return null;
                sel.Deselect();   // (the end-of-action facing prompt the flight leaves up)
                yield return Wait(.4f);
                if (found) break;

                var cam = BattlefieldCamera.Instance;
                if (cam != null) cam.PanTo(crusher.X, crusher.Y, 500f);
                crusher.RevealT = Time.unscaledTime + 4f;
                guide.TargetLabel(crusher.View != null ? crusher.View.LabelPoint + Vector3.up * .5f : GridSpace.GridToWorld(crusher.X, crusher.Y, 2f), GCrusher);
                yield return Say(LHideFail);
                guide.HideTarget();
                if (cam != null) cam.PanTo(flare.X, flare.Y, 400f);
                yield return Wait(.45f);
            }
            ResetDrone();
            objective.Hide();
            flare.Ended = false;
        }

        /// <summary>The drone back in FLARE's pocket: the flown one gone, a fresh one ready to deploy, the gadget unused.</summary>
        void ResetDrone()
        {
            if (flare.Drone != null) flare.Drone.alive = false;
            flare.Drone = null;
            flare.DroneSpent = false; flare.GadgetUsed = false;
            flare.Ended = false; flare.Acted = false;
        }

        bool droneDone;
        void OnDroneFinished(SimUnit u) { if (u == flare) droneDone = true; }

        /// <summary>The three kinds of wall, one after another: reinforced (yellow), solid (red), ordinary (blue).</summary>
        IEnumerator WallKindsTour()
        {
            var cam = BattlefieldCamera.Instance;
            BattlefieldCamera.InputLocked = true;
            var stops = new[]
            {
                (TutorialMap.ReinforcedCentre, GWallReinforced, UIPalette.Accent),
                (TutorialMap.SolidCentre, GWallUnbreakable, UIPalette.Foe),
                ((Vector2)TutorialMap.HammerWall, GWallBreakable, UIPalette.Blue),
            };
            foreach (var (p, label, col) in stops)
            {
                var m = TutorialMarker.Create(p, transform, col);
                if (cam != null) cam.PanTo(p.x, p.y, 650f);
                yield return Wait(.7f);
                guide.TargetLabel(GridSpace.GridToWorld(p.x, p.y, 2.2f), label);
                yield return Wait(1.5f);
                guide.HideTarget();
                Destroy(m.gameObject);
            }
            BattlefieldCamera.InputLocked = false;
        }

        /// <summary>CRUSHER walks up to the room's west wall and breaks it with his hammer (the real ability: swings, fx, rubble).</summary>
        IEnumerator HammerDemo()
        {
            var cam = BattlefieldCamera.Instance;
            if (cam != null) cam.PanTo(15f, 11.5f, 500f);
            crusher.RevealT = Time.unscaledTime + 30f;   // shown the whole time, wall or no wall
            yield return WalkUnit(crusher, TutorialMap.HammerRoute, 2.2f);
            var map = mb.ActiveMap;
            int pid = map.PanelIndexAt[TutorialMap.HammerWall.y * MapGrid.Width + TutorialMap.HammerWall.x];
            if (pid >= 0 && mb.ActiveAbilities != null)
            {
                crusher.AbilityUsed = false; crusher.Acted = false; crusher.Ended = false;
                if (crusher.Charges < 1) crusher.Charges = 1;
                // shown the way the player does it: his card clicked, the ability's button, then the wall
                int panelId = map.Panels[pid].id;
                TutorialGate.PanelFilter = id => id == panelId;
                TutorialGate.Allow("skill");
                string skillChip = "「" + (crusher.Op != null ? crusher.Op.abilityName : "能力") + "」をクリック";
                var wallTop = GridSpace.GridToWorld(TutorialMap.HammerWall.x, TutorialMap.HammerWall.y, 1.3f);
                yield return DemoSelect(crusher);
                yield return sel.TutorialDemoAbility(crusher, pid, () => DemoPress("skill", skillChip), () => DemoPickWall(wallTop));
                guide.HideCursor(); guide.HideFrame();
                TutorialGate.Allow();
                TutorialGate.PanelFilter = null;
            }
            yield return Wait(.8f);
            crusher.Facing = Mathf.Atan2(flare.Y - crusher.Y, flare.X - crusher.X);
        }

        /// <summary>One launcher charge on the reinforced stretch (the only wall the ability mode offers): select → the ability → the
        /// wall; done when the panel's cells have become `until` (a hole, then open).</summary>
        IEnumerator LauncherLesson(string instruction, TileType until)
        {
            var map = mb.ActiveMap;
            int pid = map.PanelIndexAt[TutorialMap.ReinforcedCell.y * MapGrid.Width + TutorialMap.ReinforcedCell.x];
            TutorialGate.PanelFilter = id => id == pid;
            var marker = TutorialMarker.Create(TutorialMap.ReinforcedCentre, transform, UIPalette.Accent);
            yield return Say(instruction);
            if (gadgetCard != null) gadgetCard.Hide();      // her turn to use it: the card makes way for the objective card
            string skillName = "「" + (flare.Op != null ? flare.Op.abilityName : "能力") + "」";
            string skillChip = skillName + "をクリック";
            objective.Set(OLauncher, ("左クリック", "フレアを選択"), (skillName, "能力を選ぶ"), ("左クリック", "強化壁を選ぶ"), ("ESC", "キャンセル"));
            flare.AbilityUsed = false; flare.Ended = false; flare.Acted = false;
            if (flare.Charges < 1) flare.Charges = 1;
            TutorialGate.Allow("skill");
            radio.Say(instruction, false);
            TutorialGate.BlockInput = false;
            var wallTop = GridSpace.GridToWorld(TutorialMap.ReinforcedCentre.x, TutorialMap.ReinforcedCentre.y, 1.3f);
            while (!(map.TileAt(TutorialMap.ReinforcedCell.x, TutorialMap.ReinforcedCell.y) == until && !sel.Busy))
            {
                if (sel.Busy) guide.HideFrame();
                else if (sel.InAbilityMode) guide.FrameWorld(wallTop, GPickWall);
                else PointGuide("skill", skillChip);
                yield return null;
            }
            TutorialGate.BlockInput = true;
            TutorialGate.Allow();
            TutorialGate.PanelFilter = null;
            guide.HideFrame();
            objective.Hide();
            yield return null;
            sel.Deselect();
            Destroy(marker.gameObject);
            yield return Wait(.6f);
        }

        // ================= the entrance: first person, through the door into the training hall =================
        TutorialStage stage;
        bool entranceOn, entranceFogWas;
        List<Canvas> entranceHud;
        GameObject entranceFog;
        CamPose entrancePose;
        public const float EyeHeight = 1.62f;

        // (time, grid x, grid y, yaw°, pitch° up, fov): standing in the corridor → the door slides open → walking in → looking round the
        // hall (the shoot house to the right, up at the tall walls and the hall lights, the yard to the left) → stopping on FLARE's spot
        static readonly float[,] EntranceKeys =
        {
            { 0.0f, -11.2f, 13.6f,  90f,   0f, 62f },
            { 2.2f, -10.9f, 13.6f,  90f,   1f, 62f },
            { 4.5f,  -2.6f, 13.65f, 92f,   3f, 60f },
            { 5.8f,  -1.3f, 13.75f, 122f, -2f, 58f },
            { 7.1f,   0.0f, 13.85f, 72f,  15f, 58f },
            { 8.4f,   1.2f, 13.95f, 28f,  -4f, 56f },
            { 9.4f,   2.0f, 14.0f,  12f,  -6f, 54f },
        };
        public const float DoorOpenFrom = 1.8f, DoorOpenTo = 3.0f, WalkFrom = 2.2f, WalkTo = 4.5f, FadeIn = .9f, RiseTime = 1.9f;
        // the door's system voice at the bottom (IntroSubtitle, speaker "SYSTEM //"): it recognises you, then lets you in
        public const string SRecognising = "対象認識中……";
        public const string SWelcome = "ようこそ。コードネーム「フレア」";
        static readonly (float at, float seconds, string text)[] EntranceLines = { (.45f, 1.5f, SRecognising), (1.9f, 2.9f, SWelcome) };

        /// <summary>The first-person entrance: black → the corridor, the door opens, in through it, a look round the hall, then the camera
        /// rises out of FLARE's head into the game's top-down view. The HUD (and the fog, the radio, FLARE herself) wait off screen.</summary>
        IEnumerator Entrance()
        {
            var cam = BattlefieldCamera.Instance;
            if (cam == null) yield break;
            entranceOn = true;
            BattlefieldCamera.InputLocked = true;
            var hud = entranceHud = HideHud();
            var fog = entranceFog = GameObject.Find("FogOfWar");
            bool fogWas = entranceFogWas = fog != null && fog.activeSelf;
            if (fog != null) fog.SetActive(false);
            flare.Placed = false;   // (the figure isn't drawn while we look through her eyes)
            stage.SetCeiling(true);
            stage.SetDoorOpen(0f);
            var fade = GameObject.Find("BlindsideCanvas/Fade");
            var fadeCg = fade != null ? fade.GetComponent<CanvasGroup>() : null;
            if (fadeCg != null) fadeCg.alpha = 1f;

            MapWallSync.ForceLit = true;   // nobody is looking yet: the walls aren't "out of sight" dark
            float end = EntranceKeys[EntranceKeys.GetLength(0) - 1, 0];
            entrancePose = EntrancePoseAt(0f);
            cam.SetOverride(() => entrancePose, 0f);
            // the match set-up's long frames pass on black first (they'd make the clock jump past the door)
            for (int f = 0; f < 3; f++) yield return null;
            var blindsideCanvas = GameObject.Find("BlindsideCanvas");
            Cinematic.IntroSubtitle sub = null;
            if (blindsideCanvas != null)
            {
                sub = Cinematic.IntroSubtitle.Build(blindsideCanvas.transform);
                sub.transform.SetAsLastSibling();
                sub.speaker.text = "SYSTEM //";
                sub.speaker.color = TutorialStage.Glow;
            }
            int nextLine = 0;
            float t = 0f;
            while (t < end)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                while (sub != null && nextLine < EntranceLines.Length && t >= EntranceLines[nextLine].at)
                {
                    sub.Play(EntranceLines[nextLine].text, EntranceLines[nextLine].seconds);
                    nextLine++;
                }
                entrancePose = EntrancePoseAt(t);
                if (fadeCg != null) fadeCg.alpha = 1f - Mathf.Clamp01(t / FadeIn);
                float d = Mathf.Clamp01((t - DoorOpenFrom) / (DoorOpenTo - DoorOpenFrom));
                stage.SetDoorOpen(d * d * (3f - 2f * d));
                yield return null;
            }

            // up and out: the game camera takes over over the yard, FLARE where we stood
            if (sub != null) Destroy(sub.gameObject);
            stage.SetCeiling(false);
            MapWallSync.ForceLit = false;
            flare.Placed = true;
            if (fog != null) fog.SetActive(fogWas);
            cam.ReleaseOverride(RiseTime);
            yield return Wait(RiseTime * .7f);
            ShowHud(hud);
            yield return Wait(RiseTime * .3f + .2f);
            entranceOn = false;
            BattlefieldCamera.InputLocked = false;
        }

        /// <summary>The entrance camera at `t`: Catmull-Rom through the keys (position, yaw, pitch, fov), a walking bob while moving.</summary>
        static CamPose EntrancePoseAt(float t)
        {
            int n = EntranceKeys.GetLength(0);
            int i = 0;
            while (i < n - 2 && t > EntranceKeys[i + 1, 0]) i++;
            float t0 = EntranceKeys[i, 0], t1 = EntranceKeys[i + 1, 0];
            float u = Mathf.Clamp01((t - t0) / Mathf.Max(.001f, t1 - t0));
            u = u * u * (3f - 2f * u);
            int a = Mathf.Max(0, i - 1), b = i, c = i + 1, d = Mathf.Min(n - 1, i + 2);
            float K(int col) => CatmullRom(EntranceKeys[a, col], EntranceKeys[b, col], EntranceKeys[c, col], EntranceKeys[d, col], u);
            float gx = K(1), gy = K(2), yaw = K(3), pitch = K(4), fov = K(5);
            // footsteps: a stronger bob through the door, a gentle sway while looking round
            float walk = t >= WalkFrom && t <= WalkTo ? 1f : t > WalkTo ? .35f : .1f;
            float bob = Mathf.Sin(t * Mathf.PI * 2f * 1.8f) * .035f * walk;
            float roll = Mathf.Sin(t * Mathf.PI * 1.8f) * .6f * walk;
            var pos = GridSpace.GridToWorld(gx, gy, EyeHeight + bob);
            return new CamPose(pos, Quaternion.Euler(-pitch, yaw, roll), fov);
        }

        static float CatmullRom(float p0, float p1, float p2, float p3, float u)
            => .5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);

        /// <summary>Every on-screen overlay canvas but BlindsideCanvas (the fade, the letterbox) is switched off; returns what to restore.</summary>
        static List<Canvas> HideHud()
        {
            var off = new List<Canvas>();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.enabled || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (c.name == "BlindsideCanvas") continue;
                c.enabled = false; off.Add(c);
            }
            return off;
        }

        static void ShowHud(List<Canvas> off) { foreach (var c in off) if (c != null) c.enabled = true; }

        // ================= skipping =================
        TutorialSkipButton skipButton;
        bool skipping;

        /// <summary>The held SKIP: whatever lesson is running stops, the screen goes black and the real operation starts (as the ending
        /// does). Ignored once the tutorial is already on its way out.</summary>
        public void Skip()
        {
            if (skipping || mb == null) return;
            skipping = true;
            StopAllCoroutines();
            StartCoroutine(SkipRoutine());
        }

        IEnumerator SkipRoutine()
        {
            TutorialGate.BlockInput = true;
            TutorialGate.Allow();
            if (sel != null) sel.Deselect();
            if (radio != null) radio.Close();
            if (guide != null) { guide.HideFrame(); guide.HideTarget(); guide.HideCursor(); }
            if (objective != null) objective.Hide();
            if (wallCard != null) wallCard.Hide();
            if (droneCard != null) droneCard.Hide();
            skipButton.gameObject.SetActive(false);
            BattlefieldCamera.InputLocked = true;
            yield return FadeToBlack(.6f);
            yield return Wait(.2f);
            mb.TutorialToMainOperation();   // (disarms this tutorial: this coroutine ends here)
        }

        static IEnumerator FadeToBlack(float seconds)
        {
            var fade = GameObject.Find("BlindsideCanvas/Fade");
            var cg = fade != null ? fade.GetComponent<CanvasGroup>() : null;
            if (cg == null) yield break;
            float from = cg.alpha, t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; cg.alpha = Mathf.Lerp(from, 1f, Mathf.Clamp01(t / seconds)); yield return null; }
            cg.alpha = 1f;
        }

        // ================= the end: the alarm, the call, off to the real operation =================
        IEnumerator Ending()
        {
            skipping = true;   // (no skipping the last few lines: they lead into the operation anyway)
            skipButton.gameObject.SetActive(false);
            yield return Say(LEnd1);
            radio.Close();
            if (siren == null) siren = TutorialSiren.Build(radio.canvas.transform);
            siren.OnPulse = Sfx.Alert;
            siren.Play();
            yield return Wait(1.1f);
            radio.SetSpeaker(true);
            yield return Say(LRadio);
            radio.SetSpeaker(false);
            yield return Say(LFinal);
            radio.Close();
            guide.HideFrame(); guide.HideTarget();

            // to black, then the default house's operation with its intro (the intro opens on black and fades in itself)
            yield return FadeToBlack(.8f);
            yield return Wait(.3f);
            mb.TutorialToMainOperation(true);   // (disarms this tutorial: this coroutine ends here; the deployment opens with the briefing)
        }

        // ================= stage 3: vision =================
        IEnumerator Stage3()
        {
            yield return Say(LVisIntro);
            // both cones on the ground: FLARE's (blue) and CRUSHER's (red), CRUSHER turned toward FLARE
            crusher.Facing = Mathf.Atan2(flare.Y - crusher.Y, flare.X - crusher.X);
            flare.Facing = Mathf.Atan2(crusher.Y - flare.Y, crusher.X - flare.X);
            SetCones(true, false);   // FLARE's own cone only (the instructor's would muddle it)
            var cam = BattlefieldCamera.Instance;
            if (cam != null) cam.PanTo(flare.X + 3f, flare.Y, 500f);
            yield return Say(LVisShow);
            yield return Say(LVisLife);
            SetCones(false, false);

            yield return HideLesson();
            yield return Say(LHideDone);

            yield return LockLesson();
            yield return Say(LLockDone);
            flare.LockFacing = false;
        }

        /// <summary>CRUSHER walks off and hides south of the low wall looking west (his cone stays drawn); the player has to move so that
        /// FLARE spots him first and attack (SelfOnly + engaged → the preemptive battle). Anything else — he sees FLARE first, both at
        /// once, ignored, or never met — is answered with LHideFail (camera on him) and both are put back.</summary>
        IEnumerator HideLesson()
        {
            var start = new Vector2(flare.X, flare.Y);
            yield return Say(LHide);
            radio.Close();
            var cam = BattlefieldCamera.Instance;
            if (cam != null) cam.PanTo(5.5f, 11f, 500f);
            yield return WalkUnit(crusher, TutorialMap.HideRoute, 2.4f);
            crusher.Facing = TutorialMap.HideFacing;
            SetCones(false, true);
            if (cam != null) cam.PanTo(flare.X, flare.Y + 2f, 500f);
            yield return Wait(.6f);
            objective.Set(OHide, CHide);

            while (true)
            {
                flare.MoveLeft = TutorialMap.HideBudget; flare.Acted = false; flare.Ended = false;
                TutorialGate.NoAutoPath = false;
                TutorialGate.Allow("move");
                radio.Say(LHide, false);
                moveDone = false; encounterSeen = false; encounterEngaged = false;
                TutorialGate.BlockInput = false;
                while (!moveDone)
                {
                    PointGuide("move", GMove);
                    yield return null;
                }
                TutorialGate.BlockInput = true;
                TutorialGate.Allow();
                guide.HideFrame();
                yield return null;
                sel.Deselect();
                yield return Wait(.4f);
                if (encounterSeen && encounterKind == EncounterKind.SelfOnly && encounterEngaged) break;

                // where he was all along
                if (cam != null) cam.PanTo(crusher.X, crusher.Y, 500f);
                guide.TargetLabel(crusher.View != null ? crusher.View.LabelPoint + Vector3.up * .5f : GridSpace.GridToWorld(crusher.X, crusher.Y, 2f), GCrusher);
                crusher.RevealT = Time.unscaledTime + 4f;
                yield return Say(LHideFail);
                guide.HideTarget();
                flare.MoveTo(start.x, start.y);
                flare.HP = flare.MaxHP;
                crusher.MoveTo(TutorialMap.HideSpot.x, TutorialMap.HideSpot.y);
                crusher.Facing = TutorialMap.HideFacing;
                crusher.HP = crusher.MaxHP;
                if (cam != null) cam.PanTo(flare.X, flare.Y + 2f, 400f);
                yield return Wait(.5f);
            }
            SetCones(false, false);
            objective.Hide();
            flare.HP = flare.MaxHP; crusher.HP = crusher.MaxHP;
        }

        /// <summary>VIEW → [F] lock → confirm the facing → MOVE: done when a move ends with the facing locked. MOVE is only offered once
        /// the lock is on.</summary>
        IEnumerator LockLesson()
        {
            yield return Say(LLock);
            flare.LockFacing = false;
            flare.MoveLeft = TutorialMap.LockBudget; flare.Acted = false; flare.Ended = false;
            radio.Say(LLock, false);
            objective.Set(OLock, CLock);
            moveDone = false;
            TutorialGate.BlockInput = false;
            while (true)
            {
                bool locked = flare.LockFacing;
                if (locked) TutorialGate.Allow("face", "move"); else TutorialGate.Allow("face");
                if (moveDone)
                {
                    if (locked) break;
                    moveDone = false;   // (can't happen: MOVE waits for the lock)
                }
                if (sel.Busy || sel.InMoveMode) guide.HideFrame();
                else if (sel.InFacingMode)
                {
                    var lockBtn = !locked ? sel.HintButtonRect("lock") : null;
                    if (lockBtn != null) guide.FrameRect(lockBtn, GLock); else guide.HideFrame();
                }
                else
                {
                    var btn = sel.MenuButtonRect(locked ? "move" : "face");
                    if (btn != null) guide.FrameRect(btn, locked ? GMove : GView);
                    else if (flare.View != null) guide.FrameWorld(flare.View.LabelPoint, GSelect);
                }
                yield return null;
            }
            TutorialGate.BlockInput = true;
            TutorialGate.Allow();
            guide.HideFrame();
            objective.Hide();
            yield return null;
            sel.Deselect();
            yield return Wait(.3f);
        }

        /// <summary>Walks `u` along `route` at `speed` m/s (logical and drawn position together, facing the way it goes).</summary>
        static IEnumerator WalkUnit(SimUnit u, Vector2[] route, float speed)
        {
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = route[i - 1], b = route[i];
                float len = Vector2.Distance(a, b), t = 0f;
                u.Facing = Mathf.Atan2(b.y - a.y, b.x - a.x);
                while (t < len)
                {
                    t += speed * Time.unscaledDeltaTime;
                    var p = Vector2.Lerp(a, b, Mathf.Clamp01(t / len));
                    u.MoveTo(p.x, p.y);
                    yield return null;
                }
            }
        }

        bool showFlareCone, showCrusherCone;
        void SetCones(bool flareCone, bool crusherCone)
        {
            showFlareCone = flareCone; showCrusherCone = crusherCone;
            var vp = mb.visionPolys;
            if (vp == null) return;
            vp.ForceShow = (flareCone || crusherCone) ? (System.Func<SimUnit, bool>)(u => (u == flare && showFlareCone) || (u == crusher && showCrusherCone)) : null;
            // FLARE's cone alone: the instructor's (drawn because he is in sight) is held back too
            vp.ForceHide = flareCone && !crusherCone ? (System.Func<SimUnit, bool>)(u => u == crusher) : null;
            vp.Refresh();
        }

        bool encounterSeen, encounterEngaged;
        EncounterKind encounterKind;
        void OnEncounter(Blindside.Turns.ITurnUnit mover, Blindside.Turns.ITurnUnit enemy, EncounterKind kind, bool engaged)
        {
            if (mover != flare) return;
            encounterSeen = true; encounterKind = kind; encounterEngaged = engaged;
        }

        /// <summary>Checks only: the stage the next tutorial starts at (1 = the beginning).</summary>
        public static int StartStage = 1;

        IEnumerator Stage1()
        {
            yield return Say(LWelcome);
            yield return Say(LUnit);
            yield return Say(LTeach);

            // ---- movement: the demonstration ----
            yield return Say(LMoveIntro);
            radio.Close();
            TutorialGate.Allow("move");
            yield return DemoSelect();
            var follow = StartCoroutine(FollowDemoCursor());
            yield return sel.TutorialDemoMove(flare, TutorialMap.DemoPath, 1.6f, () => DemoPress("move", GMove));
            StopCoroutine(follow);
            guide.HideCursor();
            TutorialGate.Allow();
            yield return Wait(.4f);

            // ---- lesson 1: straight ----
            yield return MoveLesson(TutorialMap.Move1, TutorialMap.Move1Budget, false, LMove1);
            // ---- lesson 2: round the wall, drawn by hand ----
            yield return MoveLesson(TutorialMap.Move2, TutorialMap.Move2Budget, true, LMove2);
            yield return Say(LMove2Done);
        }

        // ================= stage 2: shooting =================
        IEnumerator Stage2()
        {
            yield return Say(LFireIntro);
            yield return Say(LFireHow);
            radio.Close();
            // the demonstration: the aim sweeps across the yard (line, ring and tip following) and a burst goes into the low wall
            float south = Mathf.Atan2(TutorialMap.MoveWallY - flare.Y, (TutorialMap.MoveWallX0 + .5f) - flare.X);
            float southEast = Mathf.Atan2(TutorialMap.MoveWallY - flare.Y, (TutorialMap.MoveWallX1 - .3f) - flare.X);
            TutorialGate.Allow("fire");
            yield return DemoSelect();
            var follow = StartCoroutine(FollowDemoCursor());
            yield return sel.TutorialDemoFire(flare, south, southEast, 1.8f, true, () => DemoPress("fire", GFire), () => guide.Click());
            StopCoroutine(follow);
            guide.HideCursor();
            TutorialGate.Allow();
            flare.Acted = false; flare.Ended = false;
            yield return Wait(.3f);

            // ---- the surveillance camera ----
            var cctv = mb.ActiveAbilityCtx != null && mb.ActiveAbilityCtx.Cameras.Count > 0 ? mb.ActiveAbilityCtx.Cameras[0] : null;
            if (cctv != null)
            {
                var camCell = new Vector2(cctv.x, cctv.y);
                yield return FireLesson(camCell, GridSpace.GridToWorld(camCell.x, camCell.y, 1.7f), GCamera, LCam, LCamFail,
                                        (target, gadget) => gadget && !cctv.alive, null, OCam);
                yield return Say(LCamDone);
            }

            // ---- the duel: CRUSHER steps out into the yard ----
            crusher.MoveTo(TutorialMap.DuelSpot.x, TutorialMap.DuelSpot.y);
            crusher.Facing = Mathf.Atan2(flare.Y - crusher.Y, flare.X - crusher.X);
            crusher.Placed = true;
            yield return FireLesson(TutorialMap.DuelSpot, Vector3.zero, GCrusher, LDuel, LDuelFail, (target, gadget) => target == crusher, crusher, ODuel);
            crusher.HP = crusher.MaxHP; flare.HP = flare.MaxHP;
            yield return Say(LDuelDone);

            // ---- walls: one a bullet goes through, one it doesn't ----
            yield return Say(LWalls);
            radio.Close();
            yield return WallTour();
            // the two walls side by side, mid screen, while he sums it up
            WallCard().Show(HWallsShot, TutorialWallCard.Kind.Breakable, TutorialWallCard.Kind.Solid);
            yield return Say(LWallsTip);
            wallCard.Hide();
            yield return DummyLesson();
        }

        public const string GDummy = "訓練用ダミー";

        /// <summary>A training dummy stands behind the ordinary outer wall; FIRE only, until a shot's line passes through a wall and
        /// within reach of it (it's knocked back on its spring). A miss is answered with LDummyFail.</summary>
        IEnumerator DummyLesson()
        {
            var spot = TutorialMap.DummySpot;
            var dummy = TutorialDummy.Create(spot, Mathf.Atan2(flare.Y - spot.y, flare.X - spot.x), transform);
            var marker = TutorialMarker.Create(spot, transform, UIPalette.Foe);
            var cam = BattlefieldCamera.Instance;
            if (cam != null) cam.PanTo((flare.X + spot.x) * .5f, (flare.Y + spot.y) * .5f, 500f);
            flare.Facing = Mathf.Atan2(spot.y - flare.Y, spot.x - flare.X);
            yield return Say(LDummy);
            objective.Set(ODummy, CFire);
            while (true)
            {
                flare.Acted = false; flare.Ended = false; flare.MoveLeft = 0f;
                TutorialGate.Allow("fire");
                radio.Say(LDummy, false);
                shotDone = false;
                TutorialGate.BlockInput = false;
                while (!shotDone)
                {
                    guide.TargetLabel(GridSpace.GridToWorld(spot.x, spot.y, 2.3f), GDummy);
                    PointGuide("fire", GFire);
                    yield return null;
                }
                TutorialGate.BlockInput = true;
                TutorialGate.Allow();
                guide.HideFrame();
                yield return null;
                sel.Deselect();
                // did the line of fire go through a wall and pass the dummy closely enough?
                float a = TutorialGate.LastShotAngle;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var to = spot - new Vector2(flare.X, flare.Y);
                float along = Vector2.Dot(to, dir), across = Mathf.Abs(to.x * dir.y - to.y * dir.x);
                if (along > 0f && across < .5f && TutorialGate.LastShotWalls > 0)
                {
                    dummy.Hit(a);
                    yield return Wait(1f);
                    break;
                }
                yield return Wait(.3f);
                yield return Say(LDummyFail);
                flare.Facing = Mathf.Atan2(spot.y - flare.Y, spot.x - flare.X);
            }
            guide.HideTarget();
            objective.Hide();
            Destroy(marker.gameObject);
            flare.Acted = false; flare.Ended = false;
            yield return Say(LDummyDone);
            Destroy(dummy.gameObject);
        }

        /// <summary>
        /// One shooting lesson: the target is marked (red ring + chip; `follow` makes the chip ride on that unit), FLARE turns to face it,
        /// and only FIRE is offered until a shot satisfies `ok(target the line of fire met, whether it was a gadget)`. Any other shot is
        /// answered with `failLine` and FLARE gets its action back.
        /// </summary>
        IEnumerator FireLesson(Vector2 at, Vector3 chipWorld, string chip, string instruction, string failLine,
                               System.Func<Blindside.Combat.ICombatUnit, bool, bool> ok, SimUnit follow, string goal)
        {
            var marker = TutorialMarker.Create(at, transform, UIPalette.Foe);
            flare.Facing = Mathf.Atan2(at.y - flare.Y, at.x - flare.X);
            yield return Say(instruction);
            objective.Set(goal, CFire);
            while (true)
            {
                flare.Acted = false; flare.Ended = false; flare.MoveLeft = 0f;
                TutorialGate.Allow("fire");
                radio.Say(instruction, false);
                shotDone = false;
                TutorialGate.BlockInput = false;
                while (!shotDone)
                {
                    guide.TargetLabel(follow != null && follow.View != null ? follow.View.LabelPoint + Vector3.up * .5f : chipWorld, chip);
                    PointGuide("fire", GFire);
                    yield return null;
                }
                TutorialGate.BlockInput = true;
                TutorialGate.Allow();
                guide.HideFrame();
                yield return null;   // a gadget shot re-selects the unit; take it away again
                sel.Deselect();
                yield return Wait(.4f);
                if (ok(shotTarget, shotGadget)) break;
                yield return Say(failLine);
                flare.Facing = Mathf.Atan2(at.y - flare.Y, at.x - flare.X);
            }
            objective.Hide();
            guide.HideTarget();
            Destroy(marker.gameObject);
            flare.Acted = false; flare.Ended = false;
        }

        /// <summary>The camera to an ordinary wall (shot through: blue) and to a solid one (stops a shot: red), a moment at each, and back.</summary>
        IEnumerator WallTour()
        {
            var cam = BattlefieldCamera.Instance;
            BattlefieldCamera.InputLocked = true;
            var stops = new[] { (TutorialMap.WallThrough, GWallThrough, UIPalette.Blue), (TutorialMap.WallSolid, GWallSolid, UIPalette.Foe) };
            foreach (var (p, label, col) in stops)
            {
                var m = TutorialMarker.Create(p, transform, col);
                if (cam != null) cam.PanTo(p.x, p.y, 700f);
                yield return Wait(.75f);
                guide.TargetLabel(GridSpace.GridToWorld(p.x, p.y, 2.2f), label);
                yield return Wait(1.8f);
                guide.HideTarget();
                Destroy(m.gameObject);
            }
            if (cam != null) cam.PanTo(flare.X, flare.Y, 600f);
            yield return Wait(.7f);
            BattlefieldCamera.InputLocked = false;
        }

        /// <summary>One move lesson: shows the target (camera to it and back), then lets the player move until a move ends on it; a move
        /// that ends anywhere else is answered with LMoveFail and the unit is put back.</summary>
        IEnumerator MoveLesson(Vector2 target, float budget, bool drawn, string instruction)
        {
            var startPos = new Vector2(flare.X, flare.Y);
            float startFacing = flare.Facing;
            var marker = TutorialMarker.Create(target, transform);
            var markerTop = GridSpace.GridToWorld(target.x, target.y, 1.9f);

            yield return Say(instruction);
            radio.Close();
            yield return ShowPoint(target);
            guide.TargetLabel(markerTop, GTarget);
            objective.Set(drawn ? OMove2 : OMove1, drawn ? CMove2 : CMove1);

            while (true)
            {
                flare.MoveLeft = budget; flare.Ended = false; flare.Acted = false;
                TutorialGate.NoAutoPath = drawn;
                TutorialGate.Allow("move");
                radio.Say(instruction, false);
                moveDone = false;
                TutorialGate.BlockInput = false;
                while (!moveDone)
                {
                    PointGuide("move", GMove);
                    yield return null;
                }
                TutorialGate.BlockInput = true;
                TutorialGate.Allow();
                guide.HideFrame();
                yield return null;   // AfterAction re-selects the unit; take it away again
                sel.Deselect();
                yield return Wait(.35f);

                if (Vector2.Distance(new Vector2(flare.X, flare.Y), target) <= TutorialMap.ArriveRadius) break;

                yield return Say(LMoveFail);
                flare.MoveTo(startPos.x, startPos.y);
                flare.Facing = startFacing;
                var cam = BattlefieldCamera.Instance;
                if (cam != null) cam.PanTo(startPos.x, startPos.y, 400f);
                yield return Wait(.45f);
            }
            TutorialGate.NoAutoPath = false;
            guide.HideTarget();
            objective.Hide();
            Destroy(marker.gameObject);
        }

        /// <summary>The guide frame on whatever the player should click next: FLARE, then the menu's `command`; nothing while drawing a path
        /// or aiming.</summary>
        void PointGuide(string command, string label)
        {
            string tip = sel.Busy ? null : sel.InFireMode ? TFire : sel.InMoveMode ? (TutorialGate.NoAutoPath ? TDraw : TDrawOrClick) : null;
            if (tip != null) guide.MouseTip(tip); else guide.HideMouseTip();
            if (sel.Busy || sel.InMoveMode || sel.InFireMode) { guide.HideFrame(); return; }
            var btn = sel.MenuButtonRect(command);
            if (btn != null) { guide.FrameRect(btn, label); return; }
            if (flare.View != null) guide.FrameWorld(flare.View.LabelPoint, GSelect);
        }

        void OnMoveFinished(SimUnit u) { if (u == flare) moveDone = true; }

        // ---- the demonstrations' pointer: it clicks FLARE, then the command's button, then rides the path's tip / the aim's end ----
        IEnumerator DemoSelect(SimUnit who = null)
        {
            if (who == null) who = flare;
            var cam = Camera.main;
            if (cam == null || who.View == null) yield break;
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
            var btn = sel.MenuButtonRect(command);
            if (btn == null) yield break;
            guide.FrameRect(btn, label);
            guide.CursorToRect(btn);
            yield return Wait(.6f);
            guide.Click();
            yield return Wait(.35f);
            guide.HideFrame();
        }

        /// <summary>The demonstration's pointer goes to a wall (the ability mode lights it), frames it, clicks.</summary>
        IEnumerator DemoPickWall(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            var s = cam.WorldToScreenPoint(world);
            guide.CursorTo(new Vector2(s.x, s.y));
            guide.FrameWorld(world, GPickBreakable);
            yield return Wait(.8f);
            guide.Click();
            yield return Wait(.3f);
            guide.HideFrame();
        }

        IEnumerator FollowDemoCursor()
        {
            bool pressed = false;
            while (true)
            {
                var g = sel.DemoCursorGrid;
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

        bool shotDone, shotGadget;
        Blindside.Combat.ICombatUnit shotTarget;
        void OnShotFinished(SimUnit u, Blindside.Combat.ICombatUnit target, bool gadget)
        {
            if (u != flare) return;
            shotDone = true; shotTarget = target; shotGadget = gadget;
        }

        // ================================================================
        //  Helpers
        // ================================================================
        IEnumerator Say(string line)
        {
            radio.Say(line, true);
            while (!radio.Advanced) yield return null;
        }

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        /// <summary>The camera to `p`, a moment there, and back to FLARE; nobody can touch anything meanwhile.</summary>
        IEnumerator ShowPoint(Vector2 p)
        {
            var cam = BattlefieldCamera.Instance;
            if (cam == null) yield break;
            BattlefieldCamera.InputLocked = true;
            cam.PanTo(p.x, p.y, 700f);
            yield return Wait(1.9f);
            cam.PanTo(flare.X, flare.Y, 600f);
            yield return Wait(.7f);
            BattlefieldCamera.InputLocked = false;
        }
    }
}
