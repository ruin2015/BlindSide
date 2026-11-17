// The first operation (BLINDSIDE), played as a scripted lesson every time until it is first cleared (TutorialDeployGuide hands the match
// over when Due). CRUSHER talks the player through it on the radio; each step lets the player work one unit, one command, one target
// (TutorialGate.Guided / Selectable / PanelFilter), the enemy's phases pass with nobody acting (EnemyAI.HoldTeam) and the squad can't
// fall (CombatSystem.FloorTeam), its shots always kill (SureKillTeam).
//   deployment  Tab: the hologram (held up by DeployHologram.Pinned) — the red contacts, then the walls by kind, one popup each
//               (TutorialWallPopup: soft, reinforced, solid); CRUSHER deploys the squad himself (TutorialGate.FixedDrop)
//   round 1     CRUSHER: to the marked spot, a shot through the soft wall at the enemy watching the west way in; FLARE in through it,
//               the surveillance camera that sees her shot down; the turn ends, the enemy's passes
//   round 2     CRUSHER hammers the soft wall open, flies his drone and finds the enemy hidden on the right; FLARE holes the reinforced
//               wall with her launcher, shoots the enemy through it, opens it the rest of the way
//   then        free: the hostage secured, the whole squad in its room → the alarm, the ambush (the reserve terrorists come on round
//               the building), IGNIS drops in outside and blows the reinforced stretch of the north wall; FLARE takes the hostage out
//               to the extraction point (the match's own win)
// The ambush's terrorists and IGNIS are made with the match, off the map (SimUnit.Reserve: MatchBootstrap, while Due).
// The spots below are this map's (map_7554805a, 26×20): moving its walls or its two terrorists means checking them again.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Integration;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialOperation : MonoBehaviour
    {
        // ================================================================
        //  The map's spots
        // ================================================================
        /// <summary>The ambush's terrorists (off the map until the hostage is secured): round the building, never in the hostage's room.</summary>
        public static readonly Vector2Int[] AmbushCells =
        {
            new Vector2Int(5, 5), new Vector2Int(9, 5), new Vector2Int(6, 9), new Vector2Int(9, 10), new Vector2Int(5, 14),
            new Vector2Int(8, 14), new Vector2Int(15, 11), new Vector2Int(19, 12), new Vector2Int(13, 14), new Vector2Int(20, 14),
        };
        /// <summary>Where IGNIS comes in: outside, by the reinforced stretch of the north wall.</summary>
        public static readonly Vector2Int IgnisCell = new Vector2Int(19, 2);

        public static readonly Vector2 CrusherDrop = new Vector2(15f, 18f), FlareDrop = new Vector2(1f, 11f);
        /// <summary>Outside the soft wall: the line to the west enemy crosses only soft wall (west of the solid stretch at x 11, y 13–15).</summary>
        public static readonly Vector2 CrusherSpot = new Vector2(11f, 17f);
        /// <summary>Beside the soft wall's east panel, for the hammer: the solid corner (14,16) hides him from the enemy on the right, and
        /// facing the panel he can't see it either — it waits round the hole, out of sight, for the drone to find.</summary>
        public static readonly Vector2 HammerSpot = new Vector2(14f, 17f);
        /// <summary>Inside the west room, in the camera's circle, within the launcher's reach of the reinforced wall.</summary>
        public static readonly Vector2 FlareSpot = new Vector2(6f, 9f);
        public static readonly Vector2Int SoftCell = new Vector2Int(12, 16), ReinforcedCell = new Vector2Int(11, 11), OuterCell = new Vector2Int(19, 3);
        public static readonly Vector2Int CameraCell = new Vector2Int(8, 7);
        /// <summary>Where the drone should go to find the enemy on the right.</summary>
        public static readonly Vector2 DroneSpot = new Vector2(13.5f, 14f);
        /// <summary>The hostage's room (grid rectangle).</summary>
        public static readonly Rect HostageRoom = new Rect(11.5f, 3.5f, 10f, 5f);
        public static readonly Vector2 ExfilSpot = new Vector2(21f, .5f);
        public const float ArriveRadius = .9f, CrusherBudget = 6f, FlareBudget = 10f, ExfilBudget = 14f;

        /// <summary>The match about to start is the story's first operation, not yet cleared (and not a map editor test play).</summary>
        public static bool Due()
        {
            if (PlaySession.IsTest) return false;
            var first = Blindside.Progress.Campaign.FirstMain();
            var data = MapLibrary.Current;
            var save = Blindside.Save.SaveSystem.Current;
            return first != null && first.entry != null && data != null && !string.IsNullOrEmpty(data.operationCode)
                   && data.operationCode == first.entry.operationCode && (save == null || save.mainCleared == 0);
        }

        // ================================================================
        //  Lines
        // ================================================================
        public const string LD1 = "よし、新入り。隊員を配置する前に、\n知っておくべきことを教えてやる。";
        public const string LD2 = "配置では、敵の位置を見て、\nどう倒すか作戦を立てることが大事だ。";
        public const string LD3 = "Tabを押して、建物の中を確認してみろ。";
        public const string LD4 = "見えるか？ これが今の建物の中の状況だ。";
        public const string LD5 = "この赤い印が敵の位置だ。どこに敵がいるかを把握して、\nそれに合わせて配置することが大事だ。";
        public const string LD6 = "色の違う印が見えるか？ これは壁の種類だ。";
        public const string LD7 = "壁には大きく三種類ある。\n通常壁、ソフト壁、強化壁だ。";
        public const string LSoft = "ソフト壁は銃弾が貫通するから、\n壁の向こうの敵を攻撃できる。\n壊すこともできる。";
        public const string LReinforced = "逆に強化壁は、銃弾が貫通しない。\nだが、特定の能力でなら壊せる。";
        public const string LSolid = "通常壁は、壊すことも、\n銃弾が貫通することもない。よく覚えておけ。";
        public const string LD8 = "今回は、まず俺が配置してみせる。";
        public const string LR1 = "作戦名「ブラインドサイド」、開始する。";
        public const string LR2 = "新入り、さっきの状況からすると、\nお前が入ろうとしている場所を\n敵が見張っている可能性が高い。";
        public const string LR3 = "だから俺が壁越しに敵を撃つ。\n待機していろ。";
        public const string LCrusherMove = "俺を選んで「移動」を押し、\n印の地点まで動かしてくれ。";
        public const string LCrusherFire = "次は「攻撃」だ。Tabで確認した敵の位置を、\nソフト壁越しに狙って撃て。";
        public const string LCrusherKill = "よし、敵を排除した。\n中に入ってきていいぞ。";
        public const string LFlareMove = "フレアを選んで、\n印の地点まで移動しろ。";
        public const string LCam1 = "待て、監視カメラだ。";
        public const string LCam2 = "監視カメラに映ると、敵に位置がばれる。\n見つけたらすぐに破壊しろ。";
        public const string LCamFire = "「攻撃」で監視カメラを撃て。";
        public const string LCamDone = "よし。ここでターンを終える。";
        public const string LR2a = "よし。今度は俺が突入する。";
        public const string LHammer1 = "ちなみに俺の能力は、\nソフト壁をハンマーで壊すことだ。";
        public const string LHammerMove = "一度やってみせる。まず俺を選んで、\n壁の横の印の地点まで動かしてくれ。";
        public const string LHammer2 = "次は能力だ。俺の能力で、\n目の前のソフト壁を壊してくれ。";
        public const string OHammerMove = "クラッシャーを壁の横まで移動しよう";
        public const string LDrone1 = "さらに、俺たちにはドローンという\nガジェットがある。";
        public const string LDrone2 = "近くに敵がいそうなら、先にドローンを送って\n敵の位置を把握しておくといい。";
        public const string LDrone3 = "俺の「ドローン」を飛ばして、\n印の辺りを探ってみろ。";
        public const string LDroneMiss = "何も見つからなかったか。\nもう一度、印の辺りまで飛ばしてみろ。";
        public const string LDroneFound = "いたぞ。死角に隠れて、\n壊した穴を狙っていやがった。";
        public const string LHard = "ここは敵が見張っているから、\n突入は少し難しいな……";
        public const string LYouTry = "新入り、お前がやってみるか？";
        public const string LLauncher = "お前の能力は強化壁を壊せる。\nそれで強化壁を壊して、敵を撃ってみろ。";
        public const string LLauncherTry = "フレアを選んで能力を使い、\n印の強化壁を狙え。";
        public const string LHoleFire = "穴が開いた。\nそこから「攻撃」で敵を撃て。";
        public const string LMissed = "外れたか。もう一度、\n印の敵を狙って撃て。";
        public const string LOpen = "一度半壊した壁は、もう一度能力を使えば\n完全に壊せる。やってみろ。";
        public const string LGo = "見事だ。このまま人質の位置へ突入する。";
        public const string LFree = "人質を確保したら、\n全員で人質の部屋に集まれ。";
        public const string LAmbush = "！！ 待ち伏せだ、\nこれはまずいな……";
        public const string LIgnis1 = "支援到着だ。";
        public const string LCrusherIgnis = "いいタイミングだ。イグニス、\nお前の能力で強化壁を吹き飛ばしてくれ。";
        public const string LIgnis2 = "オーケー。任せとけ。";
        public const string LIgnisTry = "イグニスを選んで能力を使い、\n印の強化壁を爆破しろ。";
        public const string LExit = "新入り！ このまま脱出するぞ！";
        public const string LExitTry = "人質を連れて、\n救出地点まで移動しろ。";
        public const string LMoveFail = "印の地点まで届いていないな。\nもう一度だ。";

        public const string GContact = "Tabで確認した敵の位置";
        public const string LMissedContact = "外れたか。もう一度、\nTabで確認した敵の位置を狙って撃て。";
        public const string GTarget = "目標地点", GFoe = "目標", GCamera = "監視カメラ", GHostageRoom = "人質の部屋", GExfil = "救出地点";
        public const string GMove = "「移動」をクリック", GFire = "「攻撃」をクリック", GDrone = "「ドローン」をクリック";
        public const string GPickSoft = "ソフト壁をクリック", GPickReinforced = "強化壁をクリック";
        public const string TFire = "左クリックで射撃", TDraw = "ドラッグで経路 · クリックで自動経路";
        public const string OCrusherMove = "クラッシャーを印の地点まで移動しよう", OCrusherFire = "ソフト壁越しに敵を撃とう";
        public const string OFlareMove = "フレアを印の地点まで移動しよう", OCam = "監視カメラを撃って破壊しよう";
        public const string OHammer = "能力でソフト壁を破壊しよう", ODrone = "ドローンで敵の位置を突き止めよう";
        public const string OLauncher = "能力で強化壁を破壊しよう", OFoe = "穴から敵を撃とう", OOpen = "もう一度能力で強化壁を壊そう";
        public const string OFree = "人質を確保し、\n全員で人質の部屋に集まろう", OIgnis = "能力で強化壁を爆破しよう", OExit = "人質を救出地点まで護送しよう";

        // ================================================================
        //  State
        // ================================================================
        MatchBootstrap mb;
        PlayerSelectionController sel;
        TurnManager turns;
        TutorialRadio radio;
        TutorialGuide guide;
        TutorialObjective objective;
        TutorialWallPopup popup;
        TutorialSiren siren;
        SimUnit crusher, flare, ignis;
        readonly List<GameObject> marks = new List<GameObject>();
        readonly List<(SimUnit u, Vector2 cell)> reserveCells = new List<(SimUnit, Vector2)>();
        bool moved, shot, droneDone;
        SimUnit movedUnit, shotUnit, droneUnit;
        Blindside.Combat.ICombatUnit shotTarget;
        bool shotGadget;

        /// <summary>Takes the match over (TutorialDeployGuide, while Due).</summary>
        public static void Begin(MatchBootstrap mb)
        {
            var op = mb.GetComponent<TutorialOperation>();
            if (op == null) op = mb.gameObject.AddComponent<TutorialOperation>();
            op.mb = mb;
            op.StopAllCoroutines();
            op.StartCoroutine(op.Run());
        }

        IEnumerator Run()
        {
            sel = mb.selection; turns = mb.ActiveTurns;
            if (turns == null || sel == null) yield break;
            crusher = Squad("CRUSHER"); flare = Squad("FLARE");
            // the reserves wait well off the map (their cells would otherwise stand in the way), each remembering where it comes on
            int park = 0;
            foreach (var u in mb.ActiveUnits)
            {
                if (!u.Reserve) continue;
                if (u.Team == OperatorTeam.Attack) ignis = u;
                else { reserveCells.Add((u, new Vector2(u.X, u.Y))); }
                u.MoveTo(-30f - park * 2f, -30f); park++;
            }
            if (crusher == null || flare == null) yield break;

            TutorialGate.Guided = true;
            TutorialGate.NoCallouts = true;          // (the squad's own radio calls would talk over CRUSHER's)
            DeployHologram.ExactContacts = true;     // (Tab shows the spots the first turn marks and shoots at)
            TutorialGate.HoldInput = true;
            TutorialGate.EndTurnOk = false;
            TutorialGate.Allow();
            EnemyAI.HoldTeam = OperatorTeam.Defense;
            turns.WinByExtract = false;   // (no slipping the hostage out round the side before the ambush)
            Blindside.Combat.CombatSystem.FloorHP = 1;
            Blindside.Combat.CombatSystem.FloorTeam = OperatorTeam.Attack;
            Blindside.Combat.CombatSystem.SureKillTeam = OperatorTeam.Attack;
            TutorialGate.MoveFinished += OnMove;
            TutorialGate.ShotFinished += OnShot;
            TutorialGate.DroneFinished += OnDrone;

            yield return Deployment();
            if (turns.Over) { Finish(); yield break; }
            yield return RoundOne();
            if (turns.Over) { Finish(); yield break; }
            yield return EnemyTurn();
            yield return RoundTwo();
            if (turns.Over) { Finish(); yield break; }
            yield return FreePart();
            if (turns.Over) { Finish(); yield break; }
            yield return Ambush();
            yield return Extraction();
            Finish();
        }

        // ================================================================
        //  The deployment: the hologram, the walls, CRUSHER's fixed deployment
        // ================================================================
        IEnumerator Deployment()
        {
            float waited = 0f;
            while (!turns.AwaitingDeploy && !turns.Over && waited < 15f) { waited += Time.unscaledDeltaTime; yield return null; }
            if (turns.Over) yield break;
            yield return Wait(.3f);
            sel.Deselect();
            radio = TutorialRadio.Build(transform);
            guide = TutorialGuide.Build(transform);
            objective = TutorialObjective.Build(radio.canvas.transform);
            popup = TutorialWallPopup.Build(radio.canvas.transform);

            yield return Say(LD1);
            yield return Say(LD2);
            // Tab: once the hologram is up it stays up (pinned) while he goes through it
            radio.Say(LD3, false);
            objective.Set("Tabを押して建物の中を確認しよう", ("Tab", "戦術ホログラム"));
            float held = 0f; waited = 0f;
            while (held < .5f && waited < 40f && !turns.Over)
            {
                float dt = Time.unscaledDeltaTime;
                waited += dt;
                held = DeployHologram.Showing ? held + dt : 0f;
                yield return null;
            }
            DeployHologram.Pinned = true;
            objective.Hide();
            yield return Wait(.3f);
            yield return Say(LD4);
            yield return Say(LD5);
            yield return Say(LD6);
            yield return Say(LD7);
            // the walls, one at a time: the popup over the hologram, that kind lit, the others dimmed
            var pages = new[] { (TutorialWallPopup.Kind.Soft, LSoft), (TutorialWallPopup.Kind.Reinforced, LReinforced), (TutorialWallPopup.Kind.Solid, LSolid) };
            for (int i = 0; i < pages.Length; i++)
            {
                DeployHologram.FocusKind = i;   // (TutorialWallPopup.Kind and DeployHologram.WallKinds share the order)
                popup.Show(pages[i].Item1, i, pages.Length);
                yield return Say(pages[i].Item2);
            }
            popup.Hide();
            DeployHologram.FocusKind = -1;
            DeployHologram.Pinned = false;
            yield return Wait(.3f);
            yield return Say(LD8);
            radio.Close();

            // the fixed deployment: [作戦開始] drops each on his spot
            TutorialGate.FixedDrop = u => u == crusher ? CrusherDrop : u == flare ? FlareDrop : (Vector2?)null;
            TutorialGate.HoldInput = false;
            var bridge = mb.uiBridge;
            if (bridge != null && bridge.endTurnButton != null) bridge.endTurnButton.onClick.Invoke();
            TutorialGate.HoldInput = true;
            while (!turns.Over && !(turns.Phase == MatchPhase.Attack && turns.AwaitingPlayer)) yield return null;
            TutorialGate.FixedDrop = null;
            yield return Wait(.4f);
            sel.Deselect();
        }

        // ================================================================
        //  Round 1: CRUSHER's shot through the soft wall, FLARE in, the camera
        // ================================================================
        IEnumerator RoundOne()
        {
            var westFoe = FoeNear(new Vector2(7f, 12f));
            // the enemy where Tab showed it, on the floor for this first turn only (a red ring each: the spot, not the figure)
            var contacts = new List<(SimUnit u, GameObject m)>();
            foreach (var u in mb.ActiveUnits)
                if (u.Team != OperatorTeam.Attack && u.Alive && u.Placed && !u.Reserve) contacts.Add((u, Mark(new Vector2(u.X, u.Y), UIPalette.Foe)));
            yield return Say(LR1);
            // the one watching the way in: the camera on its spot while he says so
            if (westFoe != null)
            {
                BattlefieldCamera.InputLocked = true;
                Pan(westFoe.X, westFoe.Y, 700f);
                guide.TargetLabel(GridSpace.GridToWorld(westFoe.X, westFoe.Y, 2.2f), GContact);
            }
            yield return Say(LR2);
            guide.HideTarget();
            BattlefieldCamera.InputLocked = false;
            Pan(crusher.X, crusher.Y, 600f);
            yield return Say(LR3);

            yield return MoveStep(crusher, CrusherSpot, CrusherBudget, LCrusherMove, OCrusherMove);
            crusher.Facing = westFoe != null ? Mathf.Atan2(westFoe.Y - crusher.Y, westFoe.X - crusher.X) : crusher.Facing;
            if (westFoe != null) yield return FireStep(crusher, westFoe, LCrusherFire, OCrusherFire, false);
            foreach (var c in contacts) if (!c.u.Alive) Unmark(c.m);
            yield return Say(LCrusherKill);

            yield return MoveStep(flare, FlareSpot, FlareBudget, LFlareMove, OFlareMove);

            // the camera that has her in sight
            var cam = CameraAt(CameraCell);
            if (cam != null && cam.alive)
            {
                cam.discovered = true;
                var m = Mark(new Vector2(cam.x, cam.y), UIPalette.Foe);
                Pan(cam.x, cam.y, 500f);
                guide.TargetLabel(GridSpace.GridToWorld(cam.x, cam.y, 2.4f), GCamera);
                yield return Say(LCam1);
                yield return Say(LCam2);
                guide.HideTarget();
                yield return Guided(flare, "fire", GFire, LCamFire, OCam, () => !cam.alive && !sel.Busy, GridSpace.GridToWorld(cam.x, cam.y, 1.6f), GCamera, TFire);
                Unmark(m);
            }
            yield return Say(LCamDone);
            radio.Close();
            foreach (var c in contacts) Unmark(c.m);   // (the spots Tab gave hold for the first turn only)
        }

        /// <summary>The player's turn handed over by the script; the enemy's (held) passes; back at the next attack phase.</summary>
        IEnumerator EnemyTurn()
        {
            TutorialGate.EndTurnOk = true;
            TutorialGate.HoldInput = false;
            var bridge = mb.uiBridge;
            int round = turns.Round;
            if (bridge != null && bridge.endTurnButton != null) bridge.endTurnButton.onClick.Invoke();
            TutorialGate.HoldInput = true;
            TutorialGate.EndTurnOk = false;
            while (!turns.Over && !(turns.Round > round && turns.Phase == MatchPhase.Attack && turns.AwaitingPlayer)) yield return null;
            yield return Wait(.5f);
            sel.Deselect();
        }

        // ================================================================
        //  Round 2: the hammer, the drone, FLARE's launcher and shot
        // ================================================================
        IEnumerator RoundTwo()
        {
            var rightFoe = FoeNear(new Vector2(15f, 13f));
            yield return Say(LR2a);
            yield return Say(LHammer1);
            yield return MoveStep(crusher, HammerSpot, CrusherBudget, LHammerMove, OHammerMove);
            crusher.Facing = Mathf.Atan2(SoftCell.y - crusher.Y, SoftCell.x + .5f - crusher.X);
            Pan(crusher.X, crusher.Y, 450f);
            yield return AbilityStep(crusher, SoftCell, TileType.Rubble, LHammer2, OHammer, GPickSoft);

            yield return Say(LDrone1);
            yield return Say(LDrone2);
            if (rightFoe != null) yield return DroneStep(crusher, rightFoe);
            if (rightFoe != null)
            {
                rightFoe.RevealT = Time.unscaledTime + 30f;
                Pan(rightFoe.X, rightFoe.Y, 500f);
                guide.TargetLabel(GridSpace.GridToWorld(rightFoe.X, rightFoe.Y, 2.2f), GFoe);
                yield return Say(LDroneFound);
                guide.HideTarget();
            }
            yield return Say(LHard);
            yield return Say(LYouTry);
            yield return Say(LLauncher);
            Pan(flare.X, flare.Y, 450f);
            yield return AbilityStep(flare, ReinforcedCell, TileType.Hole, LLauncherTry, OLauncher, GPickReinforced);
            if (rightFoe != null && rightFoe.Alive)
            {
                rightFoe.RevealT = Time.unscaledTime + 60f;
                flare.Facing = Mathf.Atan2(rightFoe.Y - flare.Y, rightFoe.X - flare.X);
                yield return FireStep(flare, rightFoe, LHoleFire, OFoe, true);
            }
            yield return AbilityStep(flare, ReinforcedCell, TileType.Rubble, LOpen, OOpen, GPickReinforced);
            yield return Say(LGo);
            radio.Close();
        }

        // ================================================================
        //  Free: the hostage secured, everyone in its room
        // ================================================================
        IEnumerator FreePart()
        {
            var room = Mark(HostageRoom.center, TutorialMarker.Destination);
            TutorialGate.Selectable = null;
            TutorialGate.Allow("move", "face", "fire", "skill", "gadget", "escort", "wait", "item", "swap");
            TutorialGate.EndTurnOk = true;
            radio.Say(LFree, false);
            objective.Set(OFree, ("左クリック", "隊員を選択"), ("「人質確保」", "人質の隣で選ぶ"), ("ターン終了", "次のターンへ"));
            TutorialGate.HoldInput = false;
            while (!turns.Over && !(Secured() && AllInRoom() && !sel.Busy && turns.Phase == MatchPhase.Attack)) yield return null;
            TutorialGate.HoldInput = true;
            TutorialGate.EndTurnOk = false;
            TutorialGate.Allow();
            objective.Hide();
            Unmark(room);
            radio.Close();
            yield return Wait(.4f);
            sel.Deselect();
        }

        bool Secured() => mb.ActiveHostage != null && mb.ActiveHostage.Escort != null;

        bool AllInRoom()
        {
            foreach (var u in mb.ActiveUnits)
                if (u.Team == OperatorTeam.Attack && u.Alive && u.Placed && !u.Reserve && !HostageRoom.Contains(new Vector2(u.X, u.Y))) return false;
            return true;
        }

        // ================================================================
        //  The ambush and IGNIS
        // ================================================================
        IEnumerator Ambush()
        {
            if (siren == null) siren = TutorialSiren.Build(radio.canvas.transform);
            siren.OnPulse = Sfx.Alert;
            siren.Play();
            yield return Wait(.6f);
            // the reserve terrorists come on round the building, all of them shown, facing the hostage's room
            var foes = new List<SimUnit>();
            foreach (var (u, cell) in reserveCells)
                if (u.Reserve && u.Alive)
                {
                    u.MoveTo(cell.x, cell.y);
                    u.Reserve = false; u.Placed = true;
                    u.Facing = Mathf.Atan2(HostageRoom.center.y - u.Y, HostageRoom.center.x - u.X);
                    u.RevealT = Time.unscaledTime + 9999f;
                    foes.Add(u);
                    Mark(new Vector2(u.X, u.Y), UIPalette.Foe);
                    CombatFx.Ring(GridSpace.GridToWorld(u.X, u.Y, .08f), UIPalette.Foe, .5f, .9f);
                }
            var cam = BattlefieldCamera.Instance;
            if (cam != null) { BattlefieldCamera.InputLocked = true; cam.PanTo(12f, 10f, 800f); }
            yield return Wait(1.2f);
            yield return Say(LAmbush);
            BattlefieldCamera.InputLocked = false;

            // IGNIS drops in outside the north wall
            if (ignis != null)
            {
                Pan(IgnisCell.x, IgnisCell.y, 600f);
                yield return Wait(.6f);
                ignis.Reserve = false;
                ignis.MoveTo(IgnisCell.x, IgnisCell.y);
                ignis.Facing = Mathf.PI / 2f;
                ignis.Airborne = true; ignis.Placed = false; ignis.RZ = 7f;
                float t = 0f;
                while (t < .55f) { t += Time.unscaledDeltaTime; float p = Mathf.Clamp01(t / .55f); ignis.RZ = 7f * (1f - p * p); yield return null; }
                ignis.RZ = 0f; ignis.Airborne = false; ignis.Placed = true;
                CombatFx.Ring(GridSpace.GridToWorld(ignis.X, ignis.Y, .08f), new Color32(0xd6, 0xc9, 0xa8, 0xff), .5f, .9f);
                if (cam != null) cam.Shake(.08f);
                ignis.MoveLeft = 0f; ignis.Acted = false; ignis.Ended = false; ignis.AbilityUsed = false;
                if (ignis.Charges < 1) ignis.Charges = 1;
                yield return Wait(.4f);
                Speaker("IGNIS");
                yield return Say(LIgnis1);
                Speaker("CRUSHER");
                yield return Say(LCrusherIgnis);
                Speaker("IGNIS");
                yield return Say(LIgnis2);
                Speaker("CRUSHER");
                yield return AbilityStep(ignis, OuterCell, TileType.Rubble, LIgnisTry, OIgnis, GPickReinforced);
            }
        }

        // ================================================================
        //  The extraction: FLARE with the hostage to the extraction point (the match's own win)
        // ================================================================
        IEnumerator Extraction()
        {
            yield return Say(LExit);
            var m = Mark(ExfilSpot, TutorialMarker.Destination);
            turns.WinByExtract = true;
            var escort = mb.ActiveHostage != null ? mb.ActiveHostage.Escort as SimUnit : null;
            if (escort == null) escort = flare;
            Pan(escort.X, escort.Y, 450f);
            TutorialGate.Selectable = u => u == escort;
            while (!turns.Over)
            {
                escort.MoveLeft = Mathf.Max(escort.MoveLeft, ExfilBudget); escort.Ended = false;
                moved = false;
                yield return Guided(escort, "move", GMove, LExitTry, OExit, () => moved && !sel.Busy, GridSpace.GridToWorld(ExfilSpot.x, ExfilSpot.y, 1.9f), GExfil, TDraw);
                turns.CheckWin();
            }
            Unmark(m);
        }

        // ================================================================
        //  The guided steps
        // ================================================================
        IEnumerator MoveStep(SimUnit who, Vector2 spot, float budget, string line, string goal)
        {
            var start = new Vector2(who.X, who.Y);
            float facing = who.Facing;
            var m = Mark(spot, TutorialMarker.Destination);
            Pan(spot.x, spot.y, 450f);
            while (!turns.Over)
            {
                who.MoveLeft = budget; who.Ended = false; who.Acted = false;
                moved = false;
                yield return Guided(who, "move", GMove, line, goal, () => moved && movedUnit == who && !sel.Busy, GridSpace.GridToWorld(spot.x, spot.y, 1.9f), GTarget, TDraw);
                if (Vector2.Distance(new Vector2(who.X, who.Y), spot) <= ArriveRadius) break;
                yield return Say(LMoveFail);
                who.MoveTo(start.x, start.y); who.Facing = facing;
                Pan(start.x, start.y, 400f);
            }
            who.Ended = false; who.Acted = false;
            Unmark(m);
        }

        /// <summary>A shot at `foe`. `reveal`: the enemy is shown and marked (one found by the drone); otherwise only the spot Tab gave is
        /// marked (round 1's contact) and the shot goes at that — the enemy itself unseen behind the wall.</summary>
        IEnumerator FireStep(SimUnit who, SimUnit foe, string line, string goal, bool reveal)
        {
            var m = reveal ? Mark(new Vector2(foe.X, foe.Y), UIPalette.Foe) : null;
            if (reveal) foe.RevealT = Mathf.Max(foe.RevealT, Time.unscaledTime + 60f);
            string l = line;
            while (!turns.Over && foe.Alive)
            {
                who.Acted = false; who.Ended = false;
                shot = false;
                yield return Guided(who, "fire", GFire, l, goal, () => shot && shotUnit == who && !sel.Busy, GridSpace.GridToWorld(foe.X, foe.Y, 1.6f), reveal ? GFoe : GContact, TFire);
                yield return Wait(.5f);
                l = reveal ? LMissed : LMissedContact;
            }
            Unmark(m);
        }

        IEnumerator AbilityStep(SimUnit who, Vector2Int cell, TileType until, string line, string goal, string pickLabel)
        {
            var map = mb.ActiveMap;
            int pid = map.PanelIndexAt[cell.y * MapGrid.Width + cell.x];
            if (pid < 0) yield break;
            int panelId = map.Panels[pid].id;
            var p = map.Panels[pid];
            var centre = new Vector2(p.centerX, p.centerY);
            TutorialGate.PanelFilter = id => id == panelId;
            var m = Mark(centre, UIPalette.Accent);
            string skill = "「" + (who.Op != null ? who.Op.abilityName : "能力") + "」をクリック";
            who.AbilityUsed = false; who.Ended = false;
            if (who.Charges < 1) who.Charges = 1;
            yield return Guided(who, "skill", skill, line, goal, () => map.TileAt(cell.x, cell.y) == until && !sel.Busy, GridSpace.GridToWorld(centre.x, centre.y, 1.3f), pickLabel, null);
            TutorialGate.PanelFilter = null;
            who.Ended = false;
            Unmark(m);
            yield return Wait(.4f);
        }

        IEnumerator DroneStep(SimUnit who, SimUnit foe)
        {
            var seesFn = mb.ActiveVision != null ? mb.ActiveVision.GadgetSees : null;
            var m = Mark(DroneSpot, TutorialMarker.Destination);
            string line = LDrone3;
            while (!turns.Over)
            {
                if (who.Drone != null) who.Drone.alive = false;
                who.Drone = null; who.DroneSpent = false; who.GadgetUsed = false; who.Ended = false;
                droneDone = false;
                bool found = false;
                yield return Guided(who, "gadget", GDrone, line, ODrone, () =>
                {
                    if (seesFn != null && seesFn(OperatorTeam.Attack, foe.X, foe.Y)) found = true;
                    return droneDone && droneUnit == who && !sel.Busy;
                }, GridSpace.GridToWorld(DroneSpot.x, DroneSpot.y, 1.9f), GTarget, TDraw);
                if (seesFn != null && seesFn(OperatorTeam.Attack, foe.X, foe.Y)) found = true;
                if (found) break;
                line = LDroneMiss;
            }
            who.Ended = false;
            Unmark(m);
        }

        /// <summary>One guided action: only `who` can be picked and only `command` used; the guide frames the unit, then the command, then
        /// the target in the world (with `tip` at the mouse) until `done`.</summary>
        IEnumerator Guided(SimUnit who, string command, string commandLabel, string line, string goal, System.Func<bool> done,
                           Vector3? world, string worldLabel, string tip)
        {
            TutorialGate.Selectable = u => u == who;
            TutorialGate.Allow(command);
            radio.Say(line, false);
            objective.Set(goal, ("左クリック", NameOf(who) + "を選択"), (commandLabel.Replace("をクリック", ""), "コマンド"), ("ESC", "キャンセル"));
            TutorialGate.HoldInput = false;
            while (!done() && !turns.Over)
            {
                Point(who, command, commandLabel, world, worldLabel, tip);
                yield return null;
            }
            TutorialGate.HoldInput = true;
            TutorialGate.Allow();
            guide.HideFrame(); guide.HideMouseTip();
            objective.Hide();
            yield return null;   // (the unit picked again after its action: taken away)
            sel.Deselect();
            yield return Wait(.35f);
        }

        void Point(SimUnit who, string command, string label, Vector3? world, string worldLabel, string tip)
        {
            if (sel.Busy) { guide.HideFrame(); guide.HideMouseTip(); return; }
            bool aiming = sel.InMoveMode || sel.InFireMode || sel.InAbilityMode || sel.InThrowMode;
            if (aiming)
            {
                if (tip != null) guide.MouseTip(tip); else guide.HideMouseTip();
                if (world.HasValue) guide.FrameWorld(world.Value, worldLabel); else guide.HideFrame();
                return;
            }
            guide.HideMouseTip();
            var btn = sel.MenuButtonRect(command);
            if (btn != null) { guide.FrameRect(btn, label); return; }
            if (who.View != null) guide.FrameWorld(who.View.LabelPoint, NameOf(who) + "をクリック");
        }

        static string NameOf(SimUnit u) => u.Op != null ? u.Op.kr : u.Id;

        void OnMove(SimUnit u) { moved = true; movedUnit = u; }
        void OnShot(SimUnit u, Blindside.Combat.ICombatUnit target, bool gadget) { shot = true; shotUnit = u; shotTarget = target; shotGadget = gadget; }
        void OnDrone(SimUnit u) { droneDone = true; droneUnit = u; }

        // ================================================================
        //  Helpers
        // ================================================================
        SimUnit Squad(string key)
        {
            foreach (var u in mb.ActiveUnits) if (u.Team == OperatorTeam.Attack && u.Alive && !u.Reserve && u.Op != null && u.Op.key == key) return u;
            return null;
        }

        SimUnit FoeNear(Vector2 p)
        {
            SimUnit best = null; float bd = 2.5f;
            foreach (var u in mb.ActiveUnits)
            {
                if (u.Team == OperatorTeam.Attack || !u.Alive || u.Reserve) continue;
                float d = Vector2.Distance(new Vector2(u.X, u.Y), p);
                if (d < bd) { bd = d; best = u; }
            }
            return best;
        }

        Blindside.Abilities.SurveillanceCamera CameraAt(Vector2Int c)
        {
            var ctx = mb.ActiveAbilityCtx;
            if (ctx == null) return null;
            foreach (var k in ctx.Cameras) if (Mathf.Abs(k.x - c.x) < .6f && Mathf.Abs(k.y - c.y) < .6f) return k;
            return null;
        }

        GameObject Mark(Vector2 cell, Color color)
        {
            var m = TutorialMarker.Create(cell, transform, color);
            marks.Add(m.gameObject);
            return m.gameObject;
        }

        void Unmark(GameObject m) { if (m != null) { marks.Remove(m); Destroy(m); } }

        static void Pan(float x, float y, float ms) { var cam = BattlefieldCamera.Instance; if (cam != null) cam.PanTo(x, y, ms); }

        void Speaker(string key)
        {
            var op = BuildAssetRegistry.Operator(key);
            if (op != null && radio != null) radio.SetOperator(op.key, op.DisplayName, op.kr, op.color);
        }

        IEnumerator Say(string line)
        {
            radio.Say(line, true);
            while (!radio.Advanced) yield return null;
        }

        static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }

        // the radio over the unit panel while that is up
        void LateUpdate()
        {
            if (radio != null && sel != null) radio.Lift = sel.UnitPanelUp ? 236f : 0f;
        }

        void Finish()
        {
            TutorialGate.MoveFinished -= OnMove;
            TutorialGate.ShotFinished -= OnShot;
            TutorialGate.DroneFinished -= OnDrone;
            TutorialGate.Guided = false; TutorialGate.HoldInput = false;
            TutorialGate.Selectable = null; TutorialGate.FixedDrop = null; TutorialGate.EndTurnOk = true; TutorialGate.PanelFilter = null;
            TutorialGate.Allow();
            EnemyAI.HoldTeam = null;
            Blindside.Combat.CombatSystem.FloorHP = 0;
            Blindside.Combat.CombatSystem.FloorTeam = null;
            Blindside.Combat.CombatSystem.SureKillTeam = null;
            DeployHologram.Pinned = false; DeployHologram.FocusKind = -1; DeployHologram.ExactContacts = false;
            TutorialGate.NoCallouts = false;
            BattlefieldCamera.InputLocked = false;
            foreach (var m in marks) if (m != null) Destroy(m);
            marks.Clear();
            if (guide != null) { guide.HideFrame(); guide.HideMouseTip(); guide.HideTarget(); }
            if (radio != null) { radio.Close(); Destroy(radio.canvas.gameObject, .4f); }
            if (guide != null) Destroy(guide.GetComponentInParent<Canvas>().gameObject);
            radio = null; guide = null; objective = null; popup = null; siren = null;
        }

        void OnDestroy()
        {
            if (radio != null || guide != null) Finish();
        }
    }
}
