// Ported from the 'stim'/'shield'/'ghost' cases of abilitySpec()/useAbility() in
// blindside_source/04_gameplay_combat_ai.js (~line 221-222, 236-238).
using System.Collections;
using UnityEngine;

namespace Blindside.Abilities
{
    /// <summary>Port of 'stim': heal +50 HP and +50% move range to an ally within 2.5m (self included).
    /// Co-op: rank 3 (アドレナリン活性) heals 75; rank 8 (ナノボット支援) reaches any ally, wherever.</summary>
    public class StimAbility : IAbility
    {
        public string Key => "stim";

        public static int Heal(IAbilityUnit u) => Blindside.Combat.CoopRules.Has(u, "SERUM", Blindside.Combat.CoopRules.Unique3) ? 75 : 50;
        static bool Anywhere(IAbilityUnit u) => Blindside.Combat.CoopRules.Has(u, "SERUM", Blindside.Combat.CoopRules.Unique8);

        public AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => Anywhere(u)
            ? new AbilitySpec(AbilityTargetKind.Ally, range: 999f,
                hint: "ナノボットを送る味方を選択してください", sub: "どこにいる味方でも (自分を含む) · 回復 " + Heal(u),
                validAlly: a => a.Alive && a.Team == u.Team)
            : new AbilitySpec(AbilityTargetKind.Ally, range: 2.5f,
                hint: "薬剤を投与する味方を選択してください", sub: "2.5m以内の味方 (自分を含む) · 回復 " + Heal(u),
                validAlly: a => a.Alive && a.Team == u.Team && AbilitySystem.Dist(a, u) <= 2.5f);

        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            var a = target.Ally;
            int heal = Heal(u);
            int hpAdded = Mathf.Min(a.MaxHP, a.HP + heal) - a.HP;
            a.HP = Mathf.Min(a.MaxHP, a.HP + heal);
            float moveAdded = Blindside.Combat.CombatModifiers.MoveRange(a.Mv, a.Weapon, a) * 0.5f;
            a.MoveLeft += moveAdded;
            a.Stim = true;
            if (a is Blindside.Turns.ITurnUnit tu) tu.Ended = false;   // a.ended=false: an ended ally can move again

            ctx.System.RaiseHeal(u, a, hpAdded, moveAdded);
            yield return new WaitForSeconds(0.4f);
        }
    }

    /// <summary>Port of 'shield': BASTION raises a shield that blocks fire from within ~69° of its facing
    /// (enforced by CombatSystem.ShieldBlocks, not here) until it's next lowered.</summary>
    public class ShieldAbility : IAbility
    {
        public string Key => "shield";

        public AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(AbilityTargetKind.Instant, null, null);

        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            u.Shield = true;
            ctx.System.RaiseShieldUp(u);
            yield return new WaitForSeconds(0.3f);
        }
    }

    /// <summary>Port of 'ghost': WRAITH goes untracked by enemy drones/cameras (checked in
    /// enemyVisible's `if(!v&&!e.ghost) v=gadgetSees(...)`, not implemented in this file).</summary>
    public class GhostAbility : IAbility
    {
        public string Key => "ghost";

        public AbilitySpec GetSpec(IAbilityUnit u, AbilityContext ctx) => new AbilitySpec(AbilityTargetKind.Instant, null, null);

        public IEnumerator Use(IAbilityUnit u, AbilityTarget target, AbilityContext ctx)
        {
            u.Ghost = true;
            ctx.System.RaiseGhostToggle(u, true);
            yield return new WaitForSeconds(0.3f);
        }
    }
}
