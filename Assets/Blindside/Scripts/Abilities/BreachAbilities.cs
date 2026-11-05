// Ported from the 'hammer'/'launcher'/'thermite' cases of abilitySpec()/useAbility() in
// blindside_source/04_gameplay_combat_ai.js (~line 218-220, 233-235).
using System.Collections;
using UnityEngine;
using Blindside.Map;

namespace Blindside.Abilities
{
    /// <summary>Shared plumbing for the three breachers: face the panel, then hand off to the subclass's
    /// hit sequence, then stamp the panel with the new tile state and report what it used to be.</summary>
    public abstract class BreachAbilityBase : IAbility
    {
        public abstract string Key { get; }
        protected abstract TileType ResultState { get; }

        public abstract AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx);

        /// <summary>The per-hit visual beats before the panel actually breaks (swings/rings + waits).</summary>
        protected abstract IEnumerator PlayHitSequence(IAbilityUnit u, WallPanel p, AbilityContext ctx);

        /// <summary>What the panel becomes (ResultState, unless a subclass says otherwise for this panel).</summary>
        protected virtual TileType ResultFor(WallPanel p, AbilityContext ctx) => ResultState;

        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            var p = target.Panel;
            u.Facing = Mathf.Atan2(p.centerY - u.Y, p.centerX - u.X);

            yield return PlayHitSequence(u, p, ctx);

            bool wasReinforced = p.reinforced;
            var state = ResultFor(p, ctx);
            ctx.Map.BreakPanel(p.id, state);
            ctx.Vision.MapVersion++;
            ctx.System.RaiseBreachComplete(u, p, state, wasReinforced);
        }

        /// <summary>A panel the launcher has holed (drones through, people not): thermite or a second launcher charge opens it
        /// the rest of the way.</summary>
        protected static bool Holed(WallPanel p, AbilityContext ctx) => ctx.Map.TileAt(p.cells[0].x, p.cells[0].y) == TileType.Hole;
    }

    /// <summary>Port of 'hammer': adjacent normal walls only, 3 swings at 190ms, opens to rubble (fully passable).</summary>
    public class HammerAbility : BreachAbilityBase
    {
        public override string Key => "hammer";
        protected override TileType ResultState => TileType.Rubble;

        public override AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(
            AbilityTargetKind.Panel,
            hint: "破壊する壁を選択してください", sub: "隣接する通常の壁のみ可能",
            // a mirror on a plain (unreinforced) wall breaks too
            validPanel: p => ctx.Map.TileAt(p.cells[0].x, p.cells[0].y) == TileType.Wall
                && !p.reinforced && AbilitySystem.PanelNear(p, u));

        protected override IEnumerator PlayHitSequence(IAbilityUnit u, WallPanel p, AbilityContext ctx)
        {
            for (int i = 0; i < 3; i++)
            {
                ctx.System.RaiseBreachSwing(u, p);
                yield return new WaitForSeconds(0.19f);
            }
        }
    }

    /// <summary>Port of 'launcher': 6m throw range with a clear line, opens a hole (drones pass, units don't).</summary>
    public class LauncherAbility : BreachAbilityBase
    {
        public override string Key => "launcher";
        protected override TileType ResultState => TileType.Hole;

        public override AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(
            AbilityTargetKind.Panel, range: 6f,
            hint: "除去する壁を選択してください", sub: "射程6m · 投擲経路の障害物に遮られる場合あり",
            validPanel: p => (ctx.Map.TileAt(p.cells[0].x, p.cells[0].y) == TileType.Wall || Holed(p, ctx))
                && AbilitySystem.ThrowClear(u, p, ctx));

        // a wall becomes a hole; a hole it made before opens up completely
        protected override TileType ResultFor(WallPanel p, AbilityContext ctx) => Holed(p, ctx) ? TileType.Rubble : TileType.Hole;

        protected override IEnumerator PlayHitSequence(IAbilityUnit u, WallPanel p, AbilityContext ctx)
        {
            ctx.System.RaiseBreachSwing(u, p); // single arc-travel beat; caller plays fxArc + shot sfx
            yield return new WaitForSeconds(0.62f);
        }
    }

    /// <summary>Port of 'thermite': adjacent walls/reinforced (not electrified), 5 rings at 160ms, opens to rubble.</summary>
    public class ThermiteAbility : BreachAbilityBase
    {
        public override string Key => "thermite";
        protected override TileType ResultState => TileType.Rubble;

        public override AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(
            AbilityTargetKind.Panel,
            hint: "爆破する壁を選択してください", sub: "隣接する壁・強化壁 · 通電中の壁は不可",
            // mirrors included (reinforced or not), and a launcher hole (opened the rest of the way)
            validPanel: p => (ctx.Map.TileAt(p.cells[0].x, p.cells[0].y) == TileType.Wall || Holed(p, ctx))
                && !p.electrified && AbilitySystem.PanelNear(p, u));

        protected override IEnumerator PlayHitSequence(IAbilityUnit u, WallPanel p, AbilityContext ctx)
        {
            for (int i = 0; i < 5; i++)
            {
                ctx.System.RaiseBreachSwing(u, p);
                yield return new WaitForSeconds(0.16f);
            }
        }
    }
}
