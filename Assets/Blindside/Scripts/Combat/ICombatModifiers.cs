// A unit's growth in a match (the player's operators: their level's stat points and their weapon's mastery — Progress.ProgressBonus).
// CombatSystem / TurnManager / the AI read it only when a unit has it and only when a value differs from the neutral one (×1, +0), so a
// unit without growth — every AI unit, every AI-vs-AI match — plays exactly as before (the same numbers, the same GameRng draws).
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
    }

    public static class CombatModifiers
    {
        /// <summary>The unit's move range: its operator / weapon's (WeaponData.MoveRangeOf) + its 機動力 bonus.</summary>
        public static float MoveRange(int mv, WeaponData weapon, object unit)
        {
            float r = WeaponData.MoveRangeOf(mv, weapon);
            if (unit is ICombatModifiers m && m.MoveBonus != 0f) r += m.MoveBonus;
            return r;
        }
    }
}
