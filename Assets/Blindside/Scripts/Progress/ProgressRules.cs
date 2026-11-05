// Experience rules for the operators (level) and their weapons (mastery). One match's experience for an operator:
//   attacks × 10 (every volley fired) + damage dealt × 1 + kills × 60 + assists × 30, × the stage's expScale (StageRewards)
// The same amount goes to the weapon the operator used (its mastery).
//   level      1 – 30, the next needs 100 + 60 × (level − 1); every level gives 2 stat points (allocated at HQ: 体力 / 防御力 /
//              攻撃力 / 機動力 / 精密力 — phase 2)
//   mastery    1 – 10, the next needs 80 + 40 × (level − 1)
using UnityEngine;

namespace Blindside.Progress
{
    public static class ProgressRules
    {
        public const int ExpPerAttack = 10, ExpPerDamage = 1, ExpPerKill = 60, ExpPerAssist = 30;
        public const int MaxLevel = 30, PointsPerLevel = 2, MaxMastery = 10;

        public static int LevelNeed(int level) => level >= MaxLevel ? 0 : 100 + 60 * (level - 1);
        public static int MasteryNeed(int level) => level >= MaxMastery ? 0 : 80 + 40 * (level - 1);

        /// <summary>One match's experience from its record line (before the stage scale).</summary>
        public static int MatchExp(UnitRecord r) =>
            r == null ? 0 : r.attacks * ExpPerAttack + r.damageDealt * ExpPerDamage + r.kills * ExpPerKill + r.assists * ExpPerAssist;

        /// <summary>Adds `exp` to (level, exp) along `need` up to `max`; returns how many levels were gained.</summary>
        public static int AddExp(ref int level, ref int exp, int add, System.Func<int, int> need, int max)
        {
            int gained = 0;
            level = Mathf.Clamp(level, 1, max);
            exp = Mathf.Max(0, exp) + Mathf.Max(0, add);
            while (level < max && exp >= need(level)) { exp -= need(level); level++; gained++; }
            if (level >= max) exp = 0;
            return gained;
        }
    }
}
