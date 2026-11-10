// DEPLOY phase for PlayerSelectionController — the attack-side player drops their squad from the helicopter
// after the intro. Ports, from blindside_source/05_ui_input_intro.js: deployValid (~420), deployMask (~421),
// enterDeploy (~422), rappel (~270), heliLeave (~279) (in HeliDeployRig), the deploy branch of onClick (~312-319)
// and, from 04_gameplay_combat_ai.js, endPhase's deploy branch (~368-370, the [作戦開始] flow).
//
// All placement rules — the drop zone (MapData: outdoor floor without a crate), the 0.7 m spacing, the facing to the building
// centre — are in grid coordinates (y as in the map); only drawing maps y to world z = -y.
//
// The reach overlay of the MOVE command is reused for the drop zone (ui.reach = deployMask(), budget 2), brighter.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Cinematic;
using Blindside.Data;
using Blindside.Map;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        // The drop zone tint: sky blue, translucent, brighter than the MOVE reach (47,228,255 / alpha 40).
        static readonly Color ReachDeploy = new Color32(90, 214, 255, 105);

        // The default drop points [START OP] falls back to are MapData.anchors.attackerDrops (ATK_SPAWN).
        // Landing height on the rope where the figure appears; how long the lowering / descent take.
        const float RopeEndHeight = 4.4f + HeliDeployRig.ExtraHeight;
        // the slide takes longer from the raised helicopter (half the extra height's share; source .95 s from 4.4)
        const float SlideScale = .5f + .5f * RopeEndHeight / 4.4f;
        // without the helicopter over the map (HeliDeployRig.ShowDuringDeploy off) the figure drops in from just above
        // the frame's ground — the last stretch of the rope — and lands
        const float DropInHeight = 2.6f, DropInSeconds = .32f;

        HeliDeployRig heli;

        /// <summary>Hooks the deploy phase up to a match: the helicopter to fly, and the cue that opens the input.</summary>
        public void BindDeploy(HeliDeployRig rig)
        {
            heli = rig;
            if (turns != null && rig != null) turns.DeployOpened += OnDeployOpened;
        }

        // showBanner('deploy').then(()=>{ G.busy=false; updateHUD(); enterDeploy(G.units.find(u=>u.team==='atk')); … })
        void OnDeployOpened()
        {
            busy = false;
            UnitsChanged?.Invoke();
            RevealWallIntel();   // reinforced / electrified walls shown for the drop (.DeployIntel.cs)
            foreach (var u in units)
                if (u.Team == side && u.Alive) { EnterDeploy(u); return; }
        }

        // ================================================================
        //  Rules
        // ================================================================
        /// <summary>A cell an attacker may be dropped on: outdoor floor with no crate on it (the exfil zones count), per MapData.</summary>
        bool OutsideFloor(int x, int y) => MapLibrary.Current.IsDropCell(x, y);   // out of the map → false (TL()==='void')

        /// <summary>deployValid(x,y,u): on outdoor floor (exfil zones included), clear of crates and
        /// pillars, and 0.7 m from every other unit that is already down.</summary>
        bool DeployValid(float x, float y, SimUnit u)
        {
            if (!OutsideFloor(MovementSystem.JsRound(x), MovementSystem.JsRound(y))) return false;
            if (!AbilitySystem.FreeAt(mapCtx, x, y, false, UnitRadius)) return false;
            foreach (var o in units)
            {
                if (o == u || !o.Alive || o.Team != OperatorTeam.Attack || !o.Placed) continue;
                float dx = o.X - x, dy = o.Y - y;
                if (Mathf.Sqrt(dx * dx + dy * dy) < .7f) return false;
            }
            return true;
        }

        /// <summary>deployMask(): 1 on every cell a unit can be dropped on, "far" everywhere else.</summary>
        float[] DeployMask()
        {
            int W = MapGrid.Width, H = map.Height;
            var m = new float[W * H];
            for (int i = 0; i < m.Length; i++) m[i] = 1e9f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (OutsideFloor(x, y)) m[y * W + x] = 1f;
            return m;
        }

        // ================================================================
        //  enterDeploy / onClick
        // ================================================================
        void EnterDeploy(SimUnit u)
        {
            bool changed = uiUnit != u;
            ResetMoveState(); HideTip(); hasFireCast = false;
            mode = Mode.Deploy; uiUnit = u; panelUnit = u;
            moveReach = DeployMask(); moveBudget = 2f; moveIsDrone = false; moveReachKey++;   // reach:deployMask(), budget:2
            HideMenu();
            ShowUnitPanel(u, !changed);
            ShowOpArt(u);
            if (changed && u.Placed && BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.PanTo(u.X, u.Y, 350f);
            SyncRings();
            NotifySelection();
            UnitsChanged?.Invoke();
            if (u.Placed)
                // already down: another spot re-drops it, or the button takes it back up into the helicopter
                SetHint($"{KrOf(u)} — 再降下する位置をクリックしてください",
                        "建物の外の明るく表示された区域ならどこでも可 · [降下取り消し]でヘリに戻す",
                        (UndropId, "降下取り消し", false), ("cancel", "ESC 選択解除", false));
            else if (undropUnit != null && undropUnit.Alive && undropUnit.Placed)
                // the next one is up, the one just dropped can still be taken back
                SetHint($"{KrOf(u)} — 降下する位置をクリックしてください",
                        "建物の外の明るく表示された区域ならどこでも可 · 他の隊員はクリックまたはQで選択",
                        (UndropId, $"{KrOf(undropUnit)}の降下取り消し", false), ("cancel", "ESC 選択解除", false));
            else
                SetHint($"{KrOf(u)} — 降下する位置をクリックしてください",
                        "建物の外の明るく表示された区域ならどこでも可 · 他の隊員はクリックまたはQで選択",
                        ("cancel", "ESC 選択解除", false));
        }

        // ================================================================
        //  undoing a drop: the unit goes back up the rope into the helicopter and is picked again
        // ================================================================
        const string UndropId = "undrop";
        /// <summary>The unit the hint bar's 降下取り消し takes back up (the one selected, or the last one down).</summary>
        SimUnit undropUnit;

        /// <summary>The hint bar's 降下取り消し (HintAction).</summary>
        void UndropClicked()
        {
            var u = mode == Mode.Deploy && uiUnit != null && uiUnit.Placed ? uiUnit : undropUnit;
            if (busy || u == null || !u.Alive || !u.Placed || turns == null || turns.Phase != MatchPhase.Deploy) return;
            StartCoroutine(UndropRoutine(u));
        }

        IEnumerator UndropRoutine(SimUnit u)
        {
            busy = true; HideHint();
            ResetMoveState();
            mode = Mode.Busy; uiUnit = u; panelUnit = u;
            UnitsChanged?.Invoke();
            var cam = BattlefieldCamera.Instance;
            bool haveHeli = heli != null && heli.IsReady && heli.Visible;
            if (haveHeli)
            {
                float d = Mathf.Sqrt((u.X - heli.X) * (u.X - heli.X) + (u.Y - heli.Y) * (u.Y - heli.Y));
                float T = Mathf.Min(1300f, 350f + d * 110f);
                if (d > .05f)
                {
                    heli.Hd = Mathf.Atan2(u.Y - heli.Y, u.X - heli.X);
                    if (cam != null) cam.PanTo(u.X, u.Y, T);
                    heli.Tilt = -.1f;
                    yield return heli.Fly(u.X, u.Y, heli.RestZ, T / 1000f);
                    heli.Tilt = 0f;
                }
                Sfx.Ui();
                yield return heli.RopeTo(HeliDeployRig.RopeFull, .3f);
            }
            else if (cam != null) cam.PanTo(u.X, u.Y, 300f);

            // up the rope (no sight while off the ground), then aboard again
            u.Placed = false; u.Airborne = true;
            UnitsChanged?.Invoke();
            float z1 = haveHeli ? RopeEndHeight : DropInHeight;
            float dur = haveHeli ? .8f * SlideScale : DropInSeconds, t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / dur);
                u.RZ = z1 * p * p;
                yield return null;
            }
            u.RZ = 0f; u.Airborne = false;
            if (haveHeli) yield return heli.RopeTo(0f, .28f);
            Log($"{KrOf(u)}: 降下を取り消しました", "");
            if (undropUnit == u) undropUnit = null;
            busy = false;
            UnitsChanged?.Invoke();
            if (turns == null || turns.Over) yield break;
            EnterDeploy(u);
        }

        /// <summary>The deploy branch of onClick: pick another of your own units, or drop the selected one here.</summary>
        void OnClickDeploy(Vector2 screenPos)
        {
            if (!BattlefieldCamera.GroundAt(screenPos, out var g)) return;
            var pu = PickUnit(screenPos);
            if (pu != null && pu.Team == side && pu != uiUnit) { EnterDeploy(pu); return; }
            if (mode == Mode.Deploy && uiUnit != null)
            {
                var u = uiUnit;
                if (!DeployValid(g.fx, g.fy, u)) { Log("建物の外の空きスペースにのみ配置できます", "w"); return; }
                StartCoroutine(DropRoutine(u, g.fx, g.fy));
                return;
            }
            if (pu != null && pu.Team == side) EnterDeploy(pu);
        }

        IEnumerator DropRoutine(SimUnit u, float x, float y)
        {
            busy = true; HideHint();
            ResetMoveState();
            mode = Mode.Busy; uiUnit = u; panelUnit = u;   // G.ui={mode:'busy',unit:u,panelUnit:u}
            UnitsChanged?.Invoke();
            yield return Rappel(u, x, y, false);
            busy = false;
            undropUnit = u;
            if (turns == null || turns.Over) yield break;
            foreach (var o in units)
                if (o.Team == side && o.Alive && !o.Placed) { EnterDeploy(o); yield break; }
            Deselect();
            SetHint("配置を確認したら[作戦開始]を押してください", "配置した隊員をクリックすると、再度降下させられます",
                    (UndropId, $"{KrOf(u)}の降下取り消し", false));
        }

        // ================================================================
        //  rappel(u,x,y,fast)
        // ================================================================
        /// <summary>Helicopter over the spot → rope down → the figure appears on the rope end and slides down →
        /// dust ring, shake, thud → rope up. `fast` halves every duration (the automatic drops of [作戦開始]).</summary>
        IEnumerator Rappel(SimUnit u, float x, float y, bool fast)
        {
            float k = fast ? .5f : 1f;
            var cam = BattlefieldCamera.Instance;
            bool haveHeli = heli != null && heli.IsReady && heli.Visible;
            if (haveHeli)
            {
                float d = Mathf.Sqrt((x - heli.X) * (x - heli.X) + (y - heli.Y) * (y - heli.Y));
                float T = Mathf.Min(1300f, 350f + d * 110f) * k;   // ms
                if (d > .05f)
                {
                    heli.Hd = Mathf.Atan2(y - heli.Y, x - heli.X);
                    if (cam != null) cam.PanTo(x, y, T);
                    heli.Tilt = -.1f;
                    yield return heli.Fly(x, y, heli.RestZ, T / 1000f);
                    heli.Tilt = 0f;
                }
                Sfx.Ui();
                yield return heli.RopeTo(HeliDeployRig.RopeFull, fast ? .16f : .32f);
            }
            else if (cam != null) cam.PanTo(x, y, 400f * k);

            // The figure shows up on the rope's end, facing the building centre; it has no sight until it lands.
            u.Placed = false; u.Airborne = true;
            float z0 = haveHeli ? RopeEndHeight : DropInHeight;
            u.RZ = z0;
            var face = MapLibrary.Current.FacingTarget;
            u.Facing = Mathf.Atan2(face.y - y, face.x - x);
            u.MoveTo(x, y);
            UnitsChanged?.Invoke();

            float dur = haveHeli ? (fast ? .42f : .95f) * SlideScale : DropInSeconds * k, t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / dur);
                u.RZ = haveHeli ? Mathf.Lerp(z0, 0f, p) : z0 * (1f - p * p);   // the rope: constant speed; the drop: falling
                yield return null;
            }
            u.RZ = 0f; u.Airborne = false;
            u.Placed = true;   // sight (vision polygon + fog) starts here
            CombatFx.Ring(GridSpace.GridToWorld(x, y, .08f), new Color32(0xd6, 0xc9, 0xa8, 0xff), .5f, .9f);
            Sfx.Step();
            if (cam != null) cam.Shake(.08f);
            UnitsChanged?.Invoke();

            if (haveHeli) yield return heli.RopeTo(0f, fast ? .14f : .28f);
        }

        // ================================================================
        //  [作戦開始] — endPhase()'s deploy branch
        // ================================================================
        void StartOperation() => StartCoroutine(StartOperationRoutine());

        IEnumerator StartOperationRoutine()
        {
            busy = true; ClearUI(); UnitsChanged?.Invoke();
            // Anyone still aboard drops, quickly, on the first default point that is free.
            var rest = new List<SimUnit>();
            foreach (var a in units) if (a.Team == OperatorTeam.Attack && a.Alive && !a.Placed) rest.Add(a);
            foreach (var a in rest)
            {
                var drops = MapLibrary.Current.anchors.attackerDrops;
                float sx = drops[0].x + (float)Blindside.Core.GameRng.NextDouble(), sy = drops[0].y;   // fallback: no default point valid
                bool found = false;
                for (int i = 0; i < drops.Length && !found; i++)
                    if (DeployValid(drops[i].x, drops[i].y, a)) { sx = drops[i].x; sy = drops[i].y; found = true; }
                // a map's default points may not suit it: then the first free outdoor cell will do.
                for (int y = 0; y < map.Height && !found; y++)
                    for (int x = 0; x < MapGrid.Width && !found; x++)
                        if (DeployValid(x, y, a)) { sx = x; sy = y; found = true; }
                yield return Rappel(a, sx, sy, true);
            }
            if (heli != null && heli.IsReady) StartCoroutine(heli.Leave());   // heliLeave() — not awaited
            yield return new WaitForSecondsRealtime(.5f);
            busy = false;
            if (turns != null) turns.FinishDeploy();
        }
    }
}
