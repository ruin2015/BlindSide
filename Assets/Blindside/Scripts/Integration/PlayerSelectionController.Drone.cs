// Attack GADGET (ドローン) for PlayerSelectionController, and the click-to-shoot enemy gadget branch. Ports, from blindside_source:
//   05_ui_input_intro.js — doCommand's 'gadget' branch for 'atk' (~179-181):
//       let d=u.drone&&u.drone.alive?u.drone:null; if(!d){ d=deployDrone(u); log(`${u.kr}: ドローン展開`,'a'); }
//       startMoveMode(u,d,true); setHint('ドローンの移動経路を描いてください',`ドローン周囲の円形視界 · 発見した敵は今ターンの間マップに表示 · 移動力 ${m1(d.moveLeft)}m`,[cancel])
//     startMoveMode(u,mover,isDrone) (~162): reach = dijkstra(mover cell, isDrone, isDrone?null:blockedSet) — a drone ignores
//     bodies and may fly through 'hole' panels; extendPath / onMoveRelease (~286-309) branch on ui.isDrone (segFree/planPath with
//     isDrone, no bodyClear / spotClear, `ドローン` in the confirm text, moveDrone(...) then u.gadgetUsed=true).
//     onClick's enemyGadgets branch (~335-337): a click within .5 of a visible enemy gadget → the first ally that seesPt()s it
//     and canShoot()s it (ui.unit first) destroys it — no action spent; none → log('射撃可能な味方がいません …','w').
//   The reach overlay tint is `ui.isDrone?[124,240,200]:[47,228,255]` (03_renderer_threejs.js ~179).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Drones;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        DroneOps droneOps;

        // ---- G.ui for mode 'move' when ui.isDrone ----
        Drone moveDrone;          // ui.mover
        bool moveIsDrone;         // ui.isDrone

        static readonly Color ReachUnit = new Color32(47, 228, 255, 40);
        static readonly Color ReachDrone = new Color32(124, 240, 200, 40);

        public void BindDrones(DroneOps ops) { droneOps = ops; if (ops != null) ops.EnemySpotted = OnDroneSpotted; }

        /// <summary>The mover's grid position (ui.mover.x/y): the unit, or the drone in drone mode.</summary>
        Vector2? MoverPos()
        {
            if (moveIsDrone && moveDrone != null) return new Vector2(moveDrone.x, moveDrone.y);
            if (moveMover != null) return new Vector2(moveMover.X, moveMover.Y);
            return null;
        }

        /// <summary>doCommand's gadget branch for the attack team.</summary>
        void StartDrone(SimUnit u)
        {
            HideMenu();
            var d = u.Drone != null && u.Drone.alive ? u.Drone : null;
            if (d == null) { d = DeployPlayerDrone(u); Log($"{KrOf(u)}: ドローン展開", "a"); }
            StartDroneMoveMode(u, d);
            SetHint("ドローンの移動経路を描いてください", $"ドローン周囲の円形視界 · 発見した敵{(d.revealsGadgets ? "とガジェット" : "")}は今ターンの間マップに表示 · 移動力 {M1(d.moveLeft)}m", ("cancel", "ESC キャンセル", false));
        }

        /// <summary>The player's drone: one bought at the shop (10 m, marks enemy gadgets; one taken from the squad's stock) when the
        /// operator carries it, else the basic one (8 m).</summary>
        Drone DeployPlayerDrone(SimUnit u)
        {
            var d = droneOps.Deploy(u);
            if (u.ShopDrone)
            {
                d.moveMax = d.moveLeft = Blindside.Hub.HubCatalog.ReconDroneMove;
                var save = Blindside.Save.SaveSystem.Current;
                if (save != null) { var line = save.GadgetOf(Blindside.Hub.HubCatalog.ReconDroneKey); line.count = Mathf.Max(0, line.count - 1); }
            }
            else d.revealsGadgets = false;
            return d;
        }

        /// <summary>The operator has a drone to use this match: the basic one in the tutorial and test plays, else one carried from the shop.</summary>
        static bool HasDrone(SimUnit u) =>
            MatchBootstrap.FreeDrone || u.DroneSpent || (u.ShopDrone && ItemStock(Blindside.Hub.HubCatalog.ReconDroneKey) > 0);

        /// <summary>startMoveMode(u,d,true).</summary>
        void StartDroneMoveMode(SimUnit u, Drone d)
        {
            movement.Dijkstra(MovementSystem.JsRound(d.x), MovementSystem.JsRound(d.y), true, null, out var D, out _);
            mode = Mode.Move; uiUnit = u; panelUnit = u;
            NotifySelection();
            moveMover = null; moveDrone = d; moveIsDrone = true;
            moveBudget = d.moveLeft; moveReach = D; moveReachKey++;
            movePath = new List<Vector2>();
        }

        /// <summary>onClick's enemyGadgets branch. True when the click was consumed.</summary>
        bool OnClickGadget(Vector2 screenPos)
        {
            if (droneOps == null || !BattlefieldCamera.GroundAt(screenPos, out var g)) return false;
            GadgetTarget? hit = null;
            foreach (var t in droneOps.EnemyGadgets(side, true))
                if (Hypot(t.X - g.fx, t.Y - g.fy) < .5f) { hit = t; break; }
            if (!hit.HasValue) return false;
            var gd = hit.Value;
            if (droneOps.CellVisibleToPlayer != null && !droneOps.CellVisibleToPlayer(gd.X, gd.Y)) return false;   // cellVisible(G.side,gd)

            SimUnit shooter = null;
            if (uiUnit != null && CanShootFrom(uiUnit, gd)) shooter = uiUnit;
            else
                foreach (var a in units)
                    if (CanShootFrom(a, gd)) { shooter = a; break; }
            if (shooter == null) { Log("射撃可能な味方がいません (行動力の消費なし · 視界内に隊員が必要)", "w"); return true; }
            busy = true;
            StartCoroutine(GadgetShotRoutine(shooter, gd));
            return true;
        }

        bool CanShootFrom(SimUnit a, GadgetTarget gd) =>
            a != null && a.Alive && a.Team == side && vision != null && vision.SeesPoint(a, gd.X, gd.Y) && droneOps.CanShootGadget(a, gd);

        IEnumerator GadgetShotRoutine(SimUnit shooter, GadgetTarget gd)
        {
            yield return droneOps.DestroyGadget(shooter, gd);
            busy = false;
            UnitsChanged?.Invoke();
        }
    }
}
