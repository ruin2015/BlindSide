// enemyGadgets / destroyGadget / aiShootGadgets from blindside_source/04_gameplay_combat_ai.js (~104, 128-131, 263-265).
//   enemyGadgets(team): team==='atk' ? G.cams.filter(alive) : G.drones.filter(alive)   — the gadgets `team` can shoot
//   destroyGadget(u,g): u.facing=atan2(g-u); fxTracer(u,g,true); sfx('shot'); await sleep(160);
//                       g.alive=false; boom(g.x,g.y,0x7cf0c8,10); log(`${u.kr}: ${g.owner?'ドローン':'監視カメラ'}を破壊`, u.team===G.side?'a':'f');
//                       await sleep(300)
//   aiShootGadgets(team): each enemy gadget → the first living unit of `team` that seesPt() it with an open traceLine
//                       (no range limit) shoots it; the camera follows when that side is the player's or its shooter is seen.
// The drone half of DroneOps (deploy / moveDrone / aiDrone) is DroneOps.Move.cs.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Combat;
using Blindside.Data;
using Blindside.Drones;
using Blindside.Map;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;
using Blindside.Vision;

namespace Blindside.Integration
{
    /// <summary>One enemy gadget a team can shoot: an attacker's recon drone, a defender's surveillance camera, or (shot by the
    /// player only) a discovered electric field of VOLT's.</summary>
    public readonly struct GadgetTarget
    {
        public readonly Drone Drone;
        public readonly SurveillanceCamera Camera;
        public readonly ElectricField Field;
        public readonly MapGadgetRuntime.Claymore Claymore;

        public GadgetTarget(Drone d) { Drone = d; Camera = null; Field = null; Claymore = null; }
        public GadgetTarget(SurveillanceCamera c) { Drone = null; Camera = c; Field = null; Claymore = null; }
        public GadgetTarget(ElectricField f) { Drone = null; Camera = null; Field = f; Claymore = null; }
        public GadgetTarget(MapGadgetRuntime.Claymore m) { Drone = null; Camera = null; Field = null; Claymore = m; }

        public bool IsDrone => Drone != null;
        public bool IsField => Field != null;
        public float X => Drone != null ? Drone.x : Field != null ? Field.x : Claymore != null ? Claymore.x : Camera.x;
        public float Y => Drone != null ? Drone.y : Field != null ? Field.y : Claymore != null ? Claymore.y : Camera.y;
        public bool Alive => Drone != null ? Drone.alive : Claymore != null ? Claymore.alive : Field != null || Camera.alive;
        /// <summary>(a field is taken off the context's list by DroneOps.DestroyGadget)</summary>
        public void Kill() { if (Drone != null) Drone.alive = false; else if (Claymore != null) Claymore.Remove(); else if (Camera != null) Camera.alive = false; }
        public string Name => Drone != null ? "ドローン" : Field != null ? "電界" : Claymore != null ? "クレイモア" : "監視カメラ";
    }

    public partial class DroneOps
    {
        static readonly Color GadgetBoom = new Color32(0x7c, 0xf0, 0xc8, 0xff);   // 0x7cf0c8

        readonly MapGrid map;
        readonly VisionSystem vision;
        readonly AbilityContext ctx;
        readonly IReadOnlyList<SimUnit> units;

        /// <summary>G.side — the human player's team (null: AI-vs-AI, everything is shown).</summary>
        public OperatorTeam? PlayerSide;
        /// <summary>panTo(x,y,ms) on the battlefield camera.</summary>
        public System.Action<float, float, float> PanTo;
        /// <summary>updateHUD().</summary>
        public System.Action UnitsChanged;

        public DroneOps(MapGrid map, VisionSystem vision, AbilityContext ctx, IReadOnlyList<SimUnit> units)
        {
            this.map = map; this.vision = vision; this.ctx = ctx; this.units = units;
        }

        static float Hypot(float dx, float dy) => Mathf.Sqrt(dx * dx + dy * dy);

        /// <summary>enemyGadgets(team): the cameras an attacker can shoot, the drones a defender can. `player`: the human player's
        /// shots — an attacker can also shoot VOLT's electric fields it has discovered (the AI's own gadget shooting is unchanged).</summary>
        public List<GadgetTarget> EnemyGadgets(OperatorTeam team, bool player = false)
        {
            var list = new List<GadgetTarget>();
            if (team == OperatorTeam.Attack)
            {
                foreach (var c in ctx.Cameras) if (c.alive) list.Add(new GadgetTarget(c));
                if (player) foreach (var f in ctx.Electrics) if (f.discovered) list.Add(new GadgetTarget(f));
            }
            else
            {
                if (ctx.Drones != null) foreach (var d in ctx.Drones.Drones) if (d.alive) list.Add(new GadgetTarget(d));
                if (MapGadgets != null) foreach (var m in MapGadgets.Claymores) if (m.alive) list.Add(new GadgetTarget(m));
            }
            return list;
        }

        /// <summary>canShoot(a,g): open traceLine and within weapon range + 4.</summary>
        public bool CanShootGadget(ITurnUnit a, GadgetTarget g)
        {
            if (a.Weapon == null) return false;
            var path = CombatSystem.TraceLine(map, a.X, a.Y, g.X, g.Y);
            return !path.Blocked && Hypot(a.X - g.X, a.Y - g.Y) <= a.Weapon.range + 4f;
        }

        static string Kr(ITurnUnit u) => u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : u.Id;

        /// <summary>destroyGadget(u,g).</summary>
        public IEnumerator DestroyGadget(ITurnUnit u, GadgetTarget g)
        {
            u.Facing = Mathf.Atan2(g.Y - u.Y, g.X - u.X);
            var from = u.View != null ? u.View.TorsoPoint : GridSpace.GridToWorld(u.X, u.Y, .5f);
            CombatFx.Tracer(from, u.Facing, GridSpace.GridToWorld(g.X, g.Y, g.IsDrone ? .4f : g.IsField ? .25f : .9f), true, u.View != null ? u.View.MuzzlePoint : (Vector3?)null);
            SfxNoise.Shot();
            yield return new WaitForSeconds(.16f);
            g.Kill();
            if (g.IsField) RemoveField(g.Field);
            CombatFx.Boom(GridSpace.GridToWorld(g.X, g.Y), g.IsField ? FieldBoom : GadgetBoom, g.IsField ? 14 : 10);
            HudLog.Add($"{Kr(u)}: {g.Name}を破壊", HudLog.TeamType(u.Team));
            UnitsChanged?.Invoke();
            yield return new WaitForSeconds(.3f);
        }

        static readonly Color FieldBoom = new Color32(0x4f, 0xb3, 0xff, 0xff);   // the field's own blue

        /// <summary>A shot-out electric field: off the list, and the reinforced walls it charged lose the charge unless another field
        /// still reaches them (the shock's reach: radius + .6).</summary>
        void RemoveField(ElectricField f)
        {
            ctx.Electrics.Remove(f);
            for (int i = 0; i < map.Panels.Count; i++)
            {
                var p = map.Panels[i];
                if (!p.electrified) continue;
                bool still = false;
                foreach (var o in ctx.Electrics)
                {
                    foreach (var c in p.cells)
                        if (Hypot(c.x - o.x, c.y - o.y) <= o.r + .6f) { still = true; break; }
                    if (still) break;
                }
                if (!still) map.SetPanelElectrified(p.id, false);
            }
            vision.MapVersion++;
            map.BumpMap();
        }

        /// <summary>aiShootGadgets(team).</summary>
        public IEnumerator AiShootGadgets(OperatorTeam team)
        {
            foreach (var g in EnemyGadgets(team))
            {
                SimUnit shooter = null;
                foreach (var u in units)
                {
                    if (!u.Alive || u.Team != team || !vision.SeesPoint(u, g.X, g.Y)) continue;
                    if (CombatSystem.TraceLine(map, u.X, u.Y, g.X, g.Y).Blocked) continue;
                    shooter = u; break;
                }
                if (shooter == null) continue;
                if (PlayerSide.HasValue && (team == PlayerSide.Value || vision.EnemyVisible(PlayerSide.Value, shooter)))
                {
                    PanTo?.Invoke(g.X, g.Y, 300f);
                    yield return new WaitForSeconds(.3f);
                }
                yield return DestroyGadget(shooter, g);
            }
        }
    }
}
