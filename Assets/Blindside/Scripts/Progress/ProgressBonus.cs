// What growth gives an operator in a match (the player's side, a real match, a save session — MatchBootstrap applies it at setup):
//   攻撃力 (atk)          +2% damage per point
//   ヘッドショット率 (prec) +0.5 critical (headshot) chance (percentage points) per point
//   防御力 (def)          −1.5% damage taken per point, at most −30%
//   回避力 (mob)          −1 enemy hit chance (percentage points) per point, at most −20 (the old 機動力's field)
//   (体力 is no stat any more: its points were given back — SaveData.Normalize)
//   weapon mastery  per level above 1: +3% damage and +0.3 critical chance (only scopes raise the hit chance)
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
        public const float DefPerPoint = .015f, DefMax = .30f, AtkPerPoint = .02f, CritPerPoint = .5f, EvaPerPoint = 1f, EvaMax = 20f;
        /// <summary>The stats shown and trained, in order (OperatorProgress.StatPoints indices): 攻撃力, ヘッドショット率, 防御力, 回避力.</summary>
        public static readonly int[] Shown = { 2, 4, 1, 3 };
        /// <summary>The stats' names by OperatorProgress.StatPoints index (0 体力 is no longer shown).</summary>
        public static readonly string[] StatNames = { "体力", "防御力", "攻撃力", "回避力", "ヘッドショット率" };
        public static readonly string[] StatEn = { "VIT", "DEF", "ATK", "EVA", "CRIT" };

        /// <summary>What `v` points of stat `i` (StatPoints index) give, in words.</summary>
        public static string Effect(int i, int v)
        {
            switch (i)
            {
                case 1: return "被ダメージ −" + (Mathf.Min(DefMax, v * DefPerPoint) * 100f).ToString("0.#") + "%";
                case 2: return "与ダメージ +" + (v * AtkPerPoint * 100f).ToString("0") + "%";
                case 3: return "敵の命中率 −" + Mathf.Min(EvaMax, v * EvaPerPoint).ToString("0.#") + "%";
                case 4: return "ヘッドショット率 +" + (v * CritPerPoint).ToString("0.#") + "%";
            }
            return "";
        }
        public const float MasteryDamage = .03f, MasteryCrit = .3f;

        public static int Hp(OperatorProgress p) => p.hp * HpPerPoint;
        public static float TakenScale(OperatorProgress p) => 1f - Mathf.Min(DefMax, p.def * DefPerPoint);
        public static float DamageScale(OperatorProgress p, WeaponProgress w) => (1f + p.atk * AtkPerPoint) * (1f + (Mathf.Max(1, w.level) - 1) * MasteryDamage);
        public static float Crit(OperatorProgress p, WeaponProgress w) => p.prec * CritPerPoint + (Mathf.Max(1, w.level) - 1) * MasteryCrit;
        public static float Evasion(OperatorProgress p) => Mathf.Min(EvaMax, p.mob * EvaPerPoint);

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
            u.Evasion = Evasion(p);
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

        /// <summary>What depends on the gun in hand: the growth's damage and critical chance with it (its mastery) and its fitted attachments.
        /// Again after 持ち替え.</summary>
        public static void ApplyWeapon(SimUnit u)
        {
            var d = SaveSystem.Current;
            if (d == null || u == null || u.Op == null) return;
            var p = d.OperatorOf(u.Op.key);
            var w = d.WeaponOf(u.Weapon != null ? u.Weapon.key : "");
            u.DamageScale = DamageScale(p, w);
            u.HeadshotBonus = Crit(p, w);
            // the attachments fitted at the HQ to the gun it carries (null when none: the unit plays as before)
            var fit = u.Weapon != null ? p.FindFit(u.Weapon.key) : null;
            u.Gear = fit != null ? Blindside.Data.Attachments.Sum(fit.parts) : null;
        }
    }
}
