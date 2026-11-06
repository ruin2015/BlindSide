// What growth gives an operator in a match (the player's side, a real match, a save session — MatchBootstrap applies it at setup):
//   体力 (hp)     +4 max HP per point
//   防御力 (def)   −1.5% damage taken per point, at most −30%
//   攻撃力 (atk)   +2% damage per point
//   機動力 (mob)   +0.2 m move range per point
//   精密力 (prec)  +0.5 hit chance (percentage points) per point
//   weapon mastery  per level above 1: +3% damage and +0.3 hit chance (accuracy only a little, by design)
//   attachments     the parts fitted at the HQ, summed (Data.Attachments: hit / headshot by range band, rounds, damage, falloff, SPD,
//                   mobility, no 銃声 mark) — SimUnit.Gear
// The same numbers are shown on the HQ's stat panel (Describe*).
using UnityEngine;
using Blindside.Integration;
using Blindside.Save;

namespace Blindside.Progress
{
    public static class ProgressBonus
    {
        public const int HpPerPoint = 4;
        public const float DefPerPoint = .015f, DefMax = .30f, AtkPerPoint = .02f, MovePerPoint = .2f, AccPerPoint = .5f;
        public const float MasteryDamage = .03f, MasteryAccuracy = .3f;

        public static int Hp(OperatorProgress p) => p.hp * HpPerPoint;
        public static float TakenScale(OperatorProgress p) => 1f - Mathf.Min(DefMax, p.def * DefPerPoint);
        public static float DamageScale(OperatorProgress p, WeaponProgress w) => (1f + p.atk * AtkPerPoint) * (1f + (Mathf.Max(1, w.level) - 1) * MasteryDamage);
        public static float Accuracy(OperatorProgress p, WeaponProgress w) => p.prec * AccPerPoint + (Mathf.Max(1, w.level) - 1) * MasteryAccuracy;
        public static float Move(OperatorProgress p) => p.mob * MovePerPoint;

        /// <summary>Gives `u` its operator's growth from the session's save (nothing without a session).</summary>
        public static void Apply(SimUnit u)
        {
            var d = SaveSystem.Current;
            if (d == null || u == null || u.Op == null) return;
            var p = d.OperatorOf(u.Op.key);
            var w = d.WeaponOf(u.Weapon != null ? u.Weapon.key : "");
            int hp = Hp(p);
            if (hp != 0) { u.MaxHP += hp; u.HP = u.MaxHP; }
            u.DamageScale = DamageScale(p, w);
            u.AccuracyBonus = Accuracy(p, w);
            u.DamageTakenScale = TakenScale(p);
            u.MoveBonus = Move(p);
            // the attachments fitted at the HQ to the gun it carries (null when none: the unit plays as before)
            var fit = u.Weapon != null ? p.FindFit(u.Weapon.key) : null;
            u.Gear = fit != null ? Blindside.Data.Attachments.Sum(fit.parts) : null;
            // the item it carries out of the HQ (only while the squad still holds one)
            u.Item = !string.IsNullOrEmpty(p.gadget) && d.GadgetOf(p.gadget).count > 0 ? p.gadget : "";
            u.ItemSpent = false;
        }
    }
}
