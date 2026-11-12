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
            if (d.mealBuff) hp += Campaign.MealHp;   // 炊事支援: a good meal before the sortie
            if (hp != 0) { u.MaxHP += hp; u.HP = u.MaxHP; }
            u.DamageTakenScale = TakenScale(p);
            u.MoveBonus = Move(p);
            ApplyWeapon(u);
            // the items it carries out of the HQ (only those the squad still holds): the first ready, the rest after it is spent; the
            // slots past the first need co-op rank 4 / 7 (CoopPerks.GadgetSlots); frag grenades first (the ones used in the field)
            var carry = new System.Collections.Generic.List<string>(3);
            var held = new System.Collections.Generic.Dictionary<string, int>();
            int slots = CoopPerks.GadgetSlots(d, u.Op.key);
            for (int i = 0; i < slots && i < OperatorProgress.ItemSlots; i++)
            {
                string k = p.Slot(i);
                if (string.IsNullOrEmpty(k)) continue;
                held.TryGetValue(k, out int n);
                if (d.GadgetOf(k).count <= n) continue;
                held[k] = n + 1;
                carry.Add(k);
            }
            // a drone isn't a throwable in the queue: it is the unit's drone command for the match
            u.ShopDrone = carry.RemoveAll(k => k == Blindside.Hub.HubCatalog.ReconDroneKey) > 0;
            carry.Sort((a, b) => (b == "frag" ? 1 : 0) - (a == "frag" ? 1 : 0));
            // the 除細動器 works by itself when the operator falls; the rest are the unit's item commands
            u.Defib = carry.RemoveAll(k => k == ItemEffects.DefibKey) > 0;
            u.Items.Clear();
            u.Items.AddRange(carry);
            // FLARE's co-op with this operator (CoopPerks): the rank its perks read; BASTION's rank-3 堅牢な移動 moves 2
            u.CoopRank = Campaign.Rank(d, u.Op.key);
            if (u.Op.key == "BASTION" && u.CoopRank >= CoopPerks.Unique3 && u.Mv < 2) u.Mv = 2;
            // 二丁持ち (rank 6): the second gun picked at the HQ (owned)
            u.Weapon2 = null;
            if (u.CoopRank >= CoopPerks.TwoGuns)
            {
                var w2 = Blindside.Data.Loadout.Second(u.Op);
                if (w2 != null && w2 != u.Weapon && Blindside.Hub.HubCatalog.Owns(d, w2)) u.Weapon2 = w2;
            }
        }

        /// <summary>Editor debug, a match with no save session: the co-op rank set by hand (Campaign.DebugRank) and what it gives at
        /// setup (BASTION's move 2, the second gun). Nothing when none is set.</summary>
        public static void ApplyDebugCoop(SimUnit u)
        {
            if (u == null || u.Op == null || !Campaign.DebugRank(u.Op.key, out int r)) return;
            u.CoopRank = r;
            if (u.Op.key == "BASTION" && r >= CoopPerks.Unique3 && u.Mv < 2) u.Mv = 2;
            if (r >= CoopPerks.TwoGuns && u.Weapon2 == null)
            {
                var w2 = Blindside.Data.Loadout.Second(u.Op);
                if (w2 != null && w2 != u.Weapon) u.Weapon2 = w2;
            }
        }

        /// <summary>What depends on the gun in hand: the growth's damage and accuracy with it (its mastery) and its fitted attachments.
        /// Again after 持ち替え.</summary>
        public static void ApplyWeapon(SimUnit u)
        {
            var d = SaveSystem.Current;
            if (d == null || u == null || u.Op == null) return;
            var p = d.OperatorOf(u.Op.key);
            var w = d.WeaponOf(u.Weapon != null ? u.Weapon.key : "");
            u.DamageScale = DamageScale(p, w);
            u.AccuracyBonus = Accuracy(p, w);
            // the attachments fitted at the HQ to the gun it carries (null when none: the unit plays as before)
            var fit = u.Weapon != null ? p.FindFit(u.Weapon.key) : null;
            u.Gear = fit != null ? Blindside.Data.Attachments.Sum(fit.parts) : null;
        }
    }
}
