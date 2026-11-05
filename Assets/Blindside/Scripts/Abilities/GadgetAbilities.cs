// Ported from the 'shock'/'gas'/'mirror'/'cam' cases of abilitySpec()/useAbility() in
// blindside_source/04_gameplay_combat_ai.js (~line 223-226, 239-248).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Map;

namespace Blindside.Abilities
{
    /// <summary>Port of 'shock': places a 1.6m electric field that instantly destroys drones inside it
    /// and electrifies any reinforced wall within radius+0.6m (electrified walls can't be thermited).</summary>
    public class ShockAbility : IAbility
    {
        const float Radius = 1.6f;

        public string Key => "shock";

        public AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(
            AbilityTargetKind.Point, range: 3f, radius: Radius,
            hint: "電界を設置する区域を選択してください", sub: "設置範囲 3m",
            validPoint: (x, y) => AbilitySystem.FreeAt(ctx, x, y, false, 0.15f)
                && AbilitySystem.InRangeOfUnit(u, x, y, 3f)
                && ctx.Vision.LineOfSight(u.X, u.Y, x, y, u.Team));

        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            float x = target.X, y = target.Y;
            var field = new ElectricField { x = x, y = y, r = Radius, discovered = false };
            ctx.Electrics.Add(field);

            int electrified = 0;
            for (int i = 0; i < ctx.Map.Panels.Count; i++)
            {
                var p = ctx.Map.Panels[i];
                if (!p.reinforced) continue;
                bool inRange = false;
                foreach (var c in p.cells)
                {
                    float dx = c.x - x, dy = c.y - y;
                    if (Mathf.Sqrt(dx * dx + dy * dy) <= field.r + 0.6f) { inRange = true; break; }
                }
                if (!inRange) continue;
                if (!p.electrified) ctx.Map.SetPanelElectrified(p.id, true);
                electrified++;
            }

            var killed = ctx.Drones.DestroyInRadius(x, y, field.r);

            ctx.System.RaiseShockPlaced(u, field, electrified, killed);
            yield return new WaitForSeconds(0.35f);
        }
    }

    /// <summary>Port of 'gas': a 6m throw that leaves a 1.6m toxic cloud for 2 rounds (per-round tick is
    /// the caller's job, same as the field's TTL countdown); hits any enemy inside for 20 immediately.</summary>
    public class GasAbility : IAbility
    {
        const float Radius = 1.6f;

        public string Key => "gas";

        public AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(
            AbilityTargetKind.Point, range: 6f, radius: Radius,
            hint: "ガス弾を投げる位置を選択してください", sub: "投擲距離 6m",
            validPoint: (x, y) => AbilitySystem.FreeAt(ctx, x, y, true, 0.05f)
                && AbilitySystem.InRangeOfUnit(u, x, y, 6f)
                && ctx.Vision.LineOfSight(u.X, u.Y, x, y, u.Team));

        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            float x = target.X, y = target.Y;
            ctx.System.RaiseGasLaunched(u, x, y);   // fxArc(u,x,y,0x8cff5a,550)
            yield return new WaitForSeconds(0.56f); // throw travel time (fxArc)

            var cloud = new GasCloud { x = x, y = y, r = Radius, ttl = 2 };
            ctx.Gases.Add(cloud);
            RefreshGas(ctx);
            ctx.System.RaiseGasThrown(u, cloud);

            foreach (var victim in ctx.Units)
            {
                if (!victim.Alive || victim.Team == u.Team) continue;
                float dx = victim.X - x, dy = victim.Y - y;
                if (Mathf.Sqrt(dx * dx + dy * dy) > Radius) continue;
                victim.HP = Mathf.Max(0, victim.HP - 20);
                ctx.System.RaiseGasDamage(u, victim, 20, victim.HP <= 0);
            }

            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>Port of refreshGas(): gasSet = every on-map cell within g.r of a cloud's centre
        /// (scanning floor(g-r)..ceil(g+r)), then bumpMap() so the opaque grids / fog / polygons rebuild.</summary>
        public static void RefreshGas(AbilityContext ctx)
        {
            var set = ctx.Vision.GasCells;
            set.Clear();
            foreach (var g in ctx.Gases)
                for (int y = Mathf.FloorToInt(g.y - g.r); y <= Mathf.CeilToInt(g.y + g.r); y++)
                    for (int x = Mathf.FloorToInt(g.x - g.r); x <= Mathf.CeilToInt(g.x + g.r); x++)
                    {
                        if (x < 0 || y < 0 || x >= MapGrid.Width || y >= ctx.Map.Height) continue;   // TL(x,y)!=='void'
                        float dx = x - g.x, dy = y - g.y;
                        if (Mathf.Sqrt(dx * dx + dy * dy) <= g.r) set.Add(y * MapGrid.Width + x);
                    }
            ctx.Vision.MapVersion++;
            ctx.Map.BumpMap();
        }
    }

    /// <summary>Port of 'mirror': turns an adjacent intact wall into a one-way mirror (see-through for
    /// Defense only, per VisionSystem.IsOpaque). Unlike the source it keeps the wall's reinforcement: a mirror on a
    /// reinforced wall opens only to thermite / the launcher and stops rounds; on a plain wall the hammer opens it too
    /// and rounds go through.</summary>
    public class MirrorAbility : IAbility
    {
        public string Key => "mirror";

        public AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(
            AbilityTargetKind.Panel,
            hint: "設置する壁を選択してください", sub: "隣接する壁 · 防衛チームのみ向こう側を見られる",
            validPanel: p => ctx.Map.TileAt(p.cells[0].x, p.cells[0].y) == TileType.Wall
                && !p.mirror && AbilitySystem.PanelNear(p, u));

        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            var p = target.Panel;
            ctx.Map.SetPanelMirror(p.id, true);
            ctx.Vision.MapVersion++;

            ctx.System.RaiseMirrorPlaced(u, p);
            yield return new WaitForSeconds(0.3f);
        }
    }

    /// <summary>Port of 'cam': installs a fixed 3.2m-radius surveillance camera within 4m.</summary>
    public class CameraAbility : IAbility
    {
        const float Radius = 3.2f;

        public string Key => "cam";

        public AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(
            AbilityTargetKind.Point, range: 4f, radius: Radius,
            hint: "カメラを設置する区域を選択してください", sub: "設置範囲 4m",
            validPoint: (x, y) => AbilitySystem.FreeAt(ctx, x, y, false, 0.15f)
                && AbilitySystem.InRangeOfUnit(u, x, y, 4f)
                && ctx.Vision.LineOfSight(u.X, u.Y, x, y, u.Team));

        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            var cam = new SurveillanceCamera { x = target.X, y = target.Y };
            ctx.Cameras.Add(cam);
            ctx.System.RaiseCameraPlaced(u, cam);
            yield return new WaitForSeconds(0.25f);
        }
    }
}
