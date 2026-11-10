// GHOST's and SILENCE's abilities (both instant, no target to pick). They only change game state and raise events; the presentation
// (pulses, the scan, popups, log, marks) and the per-phase countdown are ReconAbilityPresenter's (Integration).
//   heartbeat  心拍センサー (GHOST, 3 uses)  every living enemy inside GHOST's view (the cone up to HeartbeatRange, or the always-seen
//              circle) is sensed through walls: its heartbeat is shown where it stands and marked for his side (MarkBoard, until his next turn)
//   holo       ホロスキャン (SILENCE, 2 uses)  every enemy electronic gadget within ScanRadius — surveillance cameras, shock-wire fields — is
//              found (discovered: shown on the map from now on) and knocked out for DisableTurns phases: a camera sees nothing, a field
//              neither fries drones nor keeps its reinforced walls electrified. They come back on by themselves (the presenter).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Vision;

namespace Blindside.Abilities
{
    public class HeartbeatAbility : IAbility
    {
        public const float HeartbeatRange = 7f;

        public string Key => "heartbeat";

        public AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(AbilityTargetKind.Instant,
            hint: "心拍センサー", sub: "視界内 " + HeartbeatRange.ToString("0") + "m 以内の敵の心拍を壁越しに感知する");

        /// <summary>The enemies `u` would sense now (alive, in its view cone within HeartbeatRange or in the near circle; walls don't matter).</summary>
        public static void Sense(IAbilityUnit u, AbilityContext ctx, List<IAbilityUnit> into)
        {
            into.Clear();
            if (ctx.Units == null) return;
            foreach (var e in ctx.Units)
            {
                if (e == null || !e.Alive || e.Team == u.Team || e.IsHostage) continue;
                float dx = e.X - u.X, dy = e.Y - u.Y, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= VisionConfig.Circle) { into.Add(e); continue; }
                if (d > HeartbeatRange) continue;
                float a = Mathf.Atan2(dy, dx) - u.Facing;
                while (a > Mathf.PI) a -= 2f * Mathf.PI;
                while (a < -Mathf.PI) a += 2f * Mathf.PI;
                if (Mathf.Abs(a) <= VisionConfig.Half) into.Add(e);
            }
        }

        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            var found = new List<IAbilityUnit>(8);
            Sense(u, ctx, found);
            ctx.System.RaiseHeartbeat(u, found);
            yield return new WaitForSeconds(.9f);
        }
    }

    public class HoloScanAbility : IAbility
    {
        public const float ScanRadius = 7f;
        /// <summary>Phases a gadget stays dead: the rest of this one and the enemy's next — back on at SILENCE's next turn.</summary>
        public const int DisableTurns = 2;

        public string Key => "holo";

        public AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(AbilityTargetKind.Instant,
            hint: "ホロスキャン", sub: "周囲 " + ScanRadius.ToString("0") + "m の敵ガジェットを検出し、1ターン無力化する");

        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            var cams = new List<SurveillanceCamera>(4);
            var fields = new List<ElectricField>(4);
            // the cameras that watch for the other side (an attacker's scan finds the defenders' cameras)
            if (u.Team == OperatorTeam.Attack)
            {
                foreach (var c in ctx.Cameras)
                {
                    if (!c.alive || !Near(u, c.x, c.y)) continue;
                    c.discovered = true;
                    c.disabledLeft = DisableTurns;
                    cams.Add(c);
                }
                foreach (var f in ctx.Electrics)
                {
                    if (!Near(u, f.x, f.y)) continue;
                    f.discovered = true;
                    if (f.disabledLeft <= 0) SetFieldWalls(ctx, f, false);
                    f.disabledLeft = DisableTurns;
                    fields.Add(f);
                }
            }
            if (ctx.Vision != null) ctx.Vision.MapVersion++;
            ctx.System.RaiseHoloScan(u, cams, fields);
            yield return new WaitForSeconds(.9f);
        }

        static bool Near(IAbilityUnit u, float x, float y)
        {
            float dx = x - u.X, dy = y - u.Y;
            return dx * dx + dy * dy <= ScanRadius * ScanRadius;
        }

        /// <summary>The reinforced walls a shock field keeps electrified (ShockAbility's rule: a cell within r + .6), switched on / off.</summary>
        public static void SetFieldWalls(AbilityContext ctx, ElectricField f, bool on)
        {
            for (int i = 0; i < ctx.Map.Panels.Count; i++)
            {
                var p = ctx.Map.Panels[i];
                if (!p.reinforced) continue;
                bool inRange = false;
                foreach (var c in p.cells)
                {
                    float dx = c.x - f.x, dy = c.y - f.y;
                    if (Mathf.Sqrt(dx * dx + dy * dy) <= f.r + .6f) { inRange = true; break; }
                }
                if (inRange && p.electrified != on) ctx.Map.SetPanelElectrified(p.id, on);
            }
        }

        /// <summary>One phase passes: knocked-out gadgets count down and come back on at 0 (a field electrifies its walls again).</summary>
        public static void Tick(AbilityContext ctx)
        {
            foreach (var c in ctx.Cameras) if (c.disabledLeft > 0) c.disabledLeft--;
            foreach (var f in ctx.Electrics)
            {
                if (f.disabledLeft <= 0) continue;
                f.disabledLeft--;
                if (f.disabledLeft == 0) SetFieldWalls(ctx, f, true);
            }
        }
    }
}
