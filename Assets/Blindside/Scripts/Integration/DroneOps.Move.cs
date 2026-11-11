// deployDrone / moveDrone / aiDrone from blindside_source/04_gameplay_combat_ai.js (~192-209, 323-328).
//   deployDrone(u): {id:'d'+u.id,team:'atk',owner:u,x:u.x,y:u.y,rx,ry,alive:true,moveLeft:8}; u.drone=d; u.droneSpent=true
//   moveDrone(d,pts): steps of <=.25m along the path; per step: show=d.team===G.side||cellVisible(G.side,x,y) → panTo(160);
//     tween(rx,ry, show?st*200:5); a step inside an electric field destroys the drone (boom 0x4fb3ff, field.discovered,
//     log 'ドローンが電界によって破壊されました'); every living defender within 2.5m, not jamming (`!e.ghost`) and with
//     losF(drone→e,'atk') gets an 'atk' mark ('drone') unless one within .7m is still live — the player's own drone also
//     logs `ドローン: ${e.kr}の位置を表示` + sfx('alert'). Leftover move under .3 is zeroed.
//   aiDrone(): round<=2, the first living attacker that hasn't spent its drone deploys it and flies it toward the hostage
//     (else a defender): planPath(d, round(tgt.x), round(tgt.y)-1, drone, 'atk', 8).
// Added on top of the source (the user's spec — "敵の能力設置物が視界に入ったら表示"): an enemy camera / electric field whose
// cell is inside the drone's 2.5m LOS circle (the same cells teamVision's gadget half lights) gets a MarkKind.Gadget mark
// and, like updateDiscovery, its `discovered` flag.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Drones;
using Blindside.Map;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.UI;
using Blindside.Vision;

namespace Blindside.Integration
{
    public partial class DroneOps
    {
        static readonly Color ZapBlue = new Color32(0x4f, 0xb3, 0xff, 0xff);   // 0x4fb3ff

        /// <summary>planPath / dijkstra (the drone flavour).</summary>
        public MovementSystem Movement;
        /// <summary>G.marks.</summary>
        public MarkBoard Marks;
        public SimHostage Hostage;
        /// <summary>G.round.</summary>
        public System.Func<int> Round;
        /// <summary>cellVisible(G.side,x,y) — the player's current sight (null: everything is visible).</summary>
        public System.Func<float, float, bool> CellVisibleToPlayer;
        /// <summary>The map's mines and sensors (the shop drone marks them too).</summary>
        public MapGadgetRuntime MapGadgets;

        /// <summary>deployDrone(u).</summary>
        public Drone Deploy(SimUnit u)
        {
            var d = ctx.Drones.Deploy(u);
            u.Drone = d; u.DroneSpent = true;
            return d;
        }

        bool ShownToPlayer(Drone d, float x, float y) =>
            !PlayerSide.HasValue || d.team == PlayerSide.Value || CellVisibleToPlayer == null || CellVisibleToPlayer(x, y);

        /// <summary>tween(d,{rx,ry},ms,true) — linear.</summary>
        static IEnumerator Tween(Drone d, float x, float y, float ms)
        {
            float fx = d.rx, fy = d.ry, dur = ms / 1000f, t0 = Time.time;
            if (dur <= .001f) { d.rx = x; d.ry = y; yield break; }
            while (true)
            {
                float p = Mathf.Min(1f, (Time.time - t0) / dur);
                d.rx = fx + (x - fx) * p; d.ry = fy + (y - fy) * p;
                if (p >= 1f) yield break;
                yield return null;
            }
        }

        bool HasLiveMark(float x, float y)
        {
            if (Marks == null) return false;
            foreach (var m in Marks.Marks)
                if (m.Team == OperatorTeam.Attack && Hypot(m.X - x, m.Y - y) < .7f && Marks.IsLive(m)) return true;
            return false;
        }

        /// <summary>moveDrone(d,pts).</summary>
        public IEnumerator MoveDrone(Drone d, List<Vector2> pts)
        {
            var cur = new Vector2(d.x, d.y);
            bool halt = false;
            foreach (var q in pts)
            {
                float seg = Vector2.Distance(cur, q);
                float a = Mathf.Atan2(q.y - cur.y, q.x - cur.x), cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                while (seg > 1e-3f)
                {
                    if (!d.alive || d.moveLeft <= 1e-3f) { halt = true; break; }
                    float st = Mathf.Min(.25f, Mathf.Min(seg, d.moveLeft));
                    float nx = cur.x + cs * st, ny = cur.y + sn * st;
                    d.x = nx; d.y = ny; cur = new Vector2(nx, ny); d.moveLeft -= st; seg -= st;
                    bool show = ShownToPlayer(d, nx, ny);
                    if (show) PanTo?.Invoke(nx, ny, 160f);
                    yield return Tween(d, nx, ny, show ? st * 200f : 5f);

                    ElectricField el = null;
                    foreach (var e in ctx.Electrics) if (e.disabledLeft <= 0 && Hypot(e.x - nx, e.y - ny) <= e.r) { el = e; break; }   // (a knocked-out field lets it pass)
                    if (el != null)
                    {
                        d.alive = false;
                        CombatFx.Boom(GridSpace.GridToWorld(nx, ny), ZapBlue, 14);
                        el.discovered = true;
                        HudLog.Add("ドローンが電界によって破壊されました", HudLogType.Warn);
                        halt = true; break;
                    }
                    MarkSeen(d, nx, ny);
                }
                if (halt) break;
                cur = new Vector2(d.x, d.y);
            }
            if (d.moveLeft < .3f) d.moveLeft = 0f;
            UnitsChanged?.Invoke();
        }

        /// <summary>The player's drone has just put an enemy on the map (presentation: the squad's radio call).</summary>
        public System.Action<Drone, SimUnit> EnemySpotted;
        /// <summary>A drone has spotted an enemy (its owner, the enemy) - the match record's spotting assist.</summary>
        public System.Action<object, object> Spotted;

        void MarkSeen(Drone d, float nx, float ny)
        {
            bool own = d.team == HudLog.Side;
            foreach (var e in units)
            {
                if (!e.Alive || e.Team != OperatorTeam.Defense || e.Ghost || Hypot(e.X - nx, e.Y - ny) > 2.5f) continue;
                if (!vision.LineOfSight(nx, ny, e.X, e.Y, OperatorTeam.Attack)) continue;
                if (HasLiveMark(e.X, e.Y)) continue;
                Marks?.Add(OperatorTeam.Attack, e.X, e.Y, MarkKind.Spotted);
                Spotted?.Invoke(d.owner, e);
                if (own) { HudLog.Add($"ドローン: {e.Kr}の位置を表示", HudLogType.Ally); Sfx.Alert(); EnemySpotted?.Invoke(d, e); }
            }

            if (!d.revealsGadgets) return;   // (the player's basic drone only lights its circle; the shop's drone marks what it finds)

            // Enemy installations inside the drone's circle (cells the fog clears for the attack side).
            var cells = new HashSet<int>();
            Blindside.Rendering.TeamVisionBuilder.AddCircleCells(vision, map, nx, ny, 2.5f, OperatorTeam.Attack, cells);
            foreach (var c in ctx.Cameras)
            {
                if (!c.alive || !cells.Contains(Cell(c.x, c.y))) continue;
                c.discovered = true;
                if (HasLiveMark(c.x, c.y)) continue;
                Marks?.Add(OperatorTeam.Attack, c.x, c.y, MarkKind.Gadget);
                if (own) { HudLog.Add("ドローン: 監視カメラの位置を表示", HudLogType.Ally); Sfx.Alert(); }
            }
            foreach (var f in ctx.Electrics)
            {
                if (!cells.Contains(Cell(f.x, f.y))) continue;
                f.discovered = true;
                if (HasLiveMark(f.x, f.y)) continue;
                Marks?.Add(OperatorTeam.Attack, f.x, f.y, MarkKind.Gadget);
                if (own) { HudLog.Add("ドローン: 電界の位置を表示", HudLogType.Ally); Sfx.Alert(); }
            }
            // the map's own mines and motion sensors
            if (own && MapGadgets != null)
                MapGadgets.Scan(cells, (x, y, name) =>
                {
                    if (HasLiveMark(x, y)) return;
                    Marks?.Add(OperatorTeam.Attack, x, y, MarkKind.Gadget);
                    HudLog.Add($"ドローン: {name}の位置を表示", HudLogType.Ally);
                    Sfx.Alert();
                });
        }

        static int Cell(float x, float y) => Mathf.FloorToInt(y + .5f) * MapGrid.Width + Mathf.FloorToInt(x + .5f);

        /// <summary>aiDrone().</summary>
        public IEnumerator AiDrone()
        {
            if (Round != null && Round() > 2) yield break;
            SimUnit owner = null;
            foreach (var u in units) if (u.Alive && u.Team == OperatorTeam.Attack && !u.DroneSpent) { owner = u; break; }
            if (owner == null) yield break;
            var d = Deploy(owner);

            float tx, ty;
            if (Hostage != null && Hostage.Alive) { tx = Hostage.X; ty = Hostage.Y; }
            else
            {
                SimUnit def = null;
                foreach (var e in units) if (e.Alive && e.Team == OperatorTeam.Defense) { def = e; break; }
                if (def == null) yield break;
                tx = def.X; ty = def.Y;
            }
            var p = Movement != null
                ? Movement.PlanPath(new Vector2(d.x, d.y), MovementSystem.JsRound(tx), MovementSystem.JsRound(ty) - 1, true, OperatorTeam.Attack, 8f)
                : null;
            if (p != null && p.Count > 1) yield return MoveDrone(d, p.GetRange(1, p.Count - 1));
        }
    }
}
