// A unit's growth and gear in a match (the player's operators: their level's stat points, their weapon's mastery — Progress.ProgressBonus
// — and their fitted attachments — Data.Attachments). CombatSystem / TurnManager / the AI read it only when a unit has it and only when a
// value differs from the neutral one (×1, +0, no gear), so a unit without growth or gear — every AI unit, every AI-vs-AI match — plays
// exactly as before (the same numbers, the same GameRng draws).
using Blindside.Data;

namespace Blindside.Combat
{
    public interface ICombatModifiers
    {
        /// <summary>× the damage of every round this unit fires (攻撃力, weapon mastery). 1 = none.</summary>
        float DamageScale { get; }
        /// <summary>+ percentage points of hit chance (精密力, weapon mastery). 0 = none.</summary>
        float AccuracyBonus { get; }
        /// <summary>× the damage this unit takes (防御力). 1 = none.</summary>
        float DamageTakenScale { get; }
        /// <summary>+ metres of move range (機動力). 0 = none.</summary>
        float MoveBonus { get; }
        /// <summary>The fitted attachments, summed; null = none.</summary>
        AttachmentEffect Gear { get; }
    }

    public static class CombatModifiers
    {
        /// <summary>The unit's move range: its operator / weapon's (WeaponData.MoveRangeOf) + its 機動力 bonus, × its gear's 機動力 %.</summary>
        public static float MoveRange(int mv, WeaponData weapon, object unit)
        {
            float r = WeaponData.MoveRangeOf(mv, weapon);
            if (unit is ICombatModifiers m)
            {
                if (m.MoveBonus != 0f) r += m.MoveBonus;
                var g = m.Gear;
                if (g != null && g.mobilityPct != 0f) r *= 1f + g.mobilityPct / 100f;
            }
            return r;
        }

        public static AttachmentEffect GearOf(object unit) => unit is ICombatModifiers m ? m.Gear : null;

        /// <summary>Rounds per volley: the gun's + the gear's extra rounds.</summary>
        public static int ShotsOf(ICombatUnit u)
        {
            var g = GearOf(u);
            return u.Weapon.shots + (g != null ? g.extraShots : 0);
        }

        /// <summary>One round's damage at `distance` before preempt / growth: the gun's falloff curve, or none with a ロングバレル; × the
        /// gear's 攻撃力 %.</summary>
        public static float BaseDamage(ICombatUnit u, float distance)
        {
            var w = u.Weapon;
            var g = GearOf(u);
            if (g == null) return w.DamageAt(w.damage, distance);
            float d = g.noFalloff ? w.damage : w.DamageAt(w.damage, distance);
            if (g.damagePct != 0f) d *= 1f + g.damagePct / 100f;
            return d;
        }
    }
}
