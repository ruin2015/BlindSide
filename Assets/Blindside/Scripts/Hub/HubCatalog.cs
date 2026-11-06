// The squad HQ's rules and goods (phase 1 — tuned later):
//   Squad       the attack squad (MatchBootstrap.AttackerKeys) from the asset registry; the HQ manages these five
//   Stations    who runs what: 作戦室 CRUSHER · 訓練所 BASTION (the old gym) · 隊員・装備管理 IGNIS (the old weapon-care room: stat points,
//               guns, attachments, the carried item) · 医務室 SERUM (medical items) · 装備購入 the supply clerk (guns, attachments, gadgets)
//   Weapons     every squad operator's guns (OperatorData.weapons). The first is the operator's own (owned from the start); the others
//               are sold at the shop (WeaponPrice) and, once bought, can be equipped by whoever lists them
//   Attachments 19 parts with their illustrations (Resources/Equipment), bought once for the squad, one per slot per operator; slots
//               マズル / 光学機器 / グリップ / マガジン / バレル / ストック
//   Items       consumables (Gadgets): tactical ones at the shop, medical ones at the 医務室; one carried per operator. Attachments and
//               items are kept and equipped in the save; the match does not read them yet (phase 2)
//   Training    money for one stat point: TrainCost(n) for the operator's n-th session in that stat (0-based), MaxTrain per stat
//   Operations  the operations the war room offers: the built-in house, then every listed catalog map (MapCatalog), attack side,
//               paid as StageRewards says
//   Talk        what CRUSHER / SERUM / IGNIS / BASTION say at the HQ (Lines; a talk picks the next line each time)
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Flow;
using Blindside.Save;

namespace Blindside.Hub
{
    /// <summary>What an NPC opens. Growth / Loadout are the old separate panels (kept so older scenes load; Management replaces both).</summary>
    public enum HubStation { Growth, Shop, Talk, Training, Operations, Loadout, Management, Medical }

    public static class HubCatalog
    {
        // ---- squad --------------------------------------------------------------------------------------------------------------
        static readonly List<OperatorData> squad = new List<OperatorData>(5);

        /// <summary>The attack squad in roster order (operators missing from the registry are left out). The list is shared.</summary>
        public static List<OperatorData> Squad
        {
            get
            {
                if (squad.Count == 0)
                    foreach (var k in Blindside.Integration.MatchBootstrap.AttackerKeys)
                    {
                        var o = BuildAssetRegistry.Operator(k);
                        if (o != null) squad.Add(o);
                    }
                return squad;
            }
        }

        // ---- weapons -------------------------------------------------------------------------------------------------------------
        /// <summary>`op`'s guns (its list, else its one weapon).</summary>
        public static WeaponData[] GunsOf(OperatorData op)
        {
            if (op == null) return new WeaponData[0];
            if (op.weapons != null && op.weapons.Length > 0) return op.weapons;
            return op.weapon != null ? new[] { op.weapon } : new WeaponData[0];
        }

        /// <summary>A gun some squad operator starts with (never sold).</summary>
        public static bool IsStarter(WeaponData w)
        {
            if (w == null) return false;
            foreach (var o in Squad) { var g = GunsOf(o); if (g.Length > 0 && g[0] == w) return true; }
            return false;
        }

        public static bool Owns(SaveData d, WeaponData w) => w != null && (IsStarter(w) || (d != null && d.OwnsWeapon(w.key)));

        /// <summary>The shop's price of `w` (yen): from its damage per volley, rounded to 1,000.</summary>
        public static int WeaponPrice(WeaponData w)
        {
            if (w == null) return 0;
            int v = 12000 + w.damage * Mathf.Max(1, w.shots) * 150;
            return Mathf.RoundToInt(v / 1000f) * 1000;
        }

        static readonly List<WeaponData> forSale = new List<WeaponData>(8);

        /// <summary>Every squad gun that isn't a starter, once each, in roster order. The list is shared.</summary>
        public static List<WeaponData> WeaponsForSale
        {
            get
            {
                if (forSale.Count == 0)
                    foreach (var o in Squad)
                        foreach (var w in GunsOf(o))
                            if (w != null && !IsStarter(w) && !forSale.Contains(w)) forSale.Add(w);
                return forSale;
            }
        }

        // ---- gadgets -------------------------------------------------------------------------------------------------------------
        public sealed class Gadget
        {
            public string key, name, note;
            public int price;
            /// <summary>Sold at the 医務室 (SERUM) rather than the shop.</summary>
            public bool medical;
            /// <summary>The illustration in Resources/Equipment ("" = none: the shop shows a glyph).</summary>
            public string art = "";
            // what a medical item does (the match reads these once items are used there)
            /// <summary>HP restored at once.</summary>
            public int heal;
            /// <summary>How many turns the buffs below last (0 = none).</summary>
            public int turns;
            /// <summary>Buffs for `turns`: extra HP (a shield over the bar), mobility %, accuracy %, headshot chance %.</summary>
            public int bonusHp, mobilityPct, accPct, headshotPct;
            /// <summary>Used by itself when the carrier goes KIA: back up with this % of max HP (0 = not a revive).</summary>
            public int revivePct;
        }

        public const int MaxGadgetStock = 9;

        public static readonly Gadget[] Gadgets =
        {
            new Gadget { key = "frag",     name = "破片手榴弾",   note = "放物線で投げ、3秒後に爆発する。中心ほど大きなダメージを与え、壁を半壊させる", price = 5000, art = "26_frag_grenade" },
            new Gadget { key = "smoke",    name = "スモーク弾",   note = "煙幕で視界を遮る (次の自分のターンまで)。煙越しの射撃は命中率が下がる", price = 3000, art = "27_smoke_grenade" },
            new Gadget { key = "flash",    name = "閃光弾",       note = "範囲内の全員の命中率を大きく下げ、被弾は必ずヘッドショットになる (敵味方問わず)", price = 3000, art = "28_flashbang" },
            new Gadget { key = "breach",   name = "粘着爆薬",     note = "壁に貼り付けて爆破し、壁を取り除く", price = 2000, art = "29_breach_charge" },
            new Gadget { key = "claymore", name = "クレイモア",   note = "設置した場所に敵が近づくと爆発する。敵の射撃で破壊される", price = 2000, art = "30_claymore" },
            new Gadget { key = "kit_low",   name = "下級医療キット", note = "体力を30回復する",   price = 1000, medical = true, art = "20_medkit_basic", heal = 30 },
            new Gadget { key = "kit_mid",   name = "中級医療キット", note = "体力を50回復する",   price = 2500, medical = true, art = "21_medkit_mid", heal = 50 },
            new Gadget { key = "kit_high",  name = "上級医療キット", note = "体力を80回復する",   price = 4000, medical = true, art = "22_medkit_high", heal = 80 },
            new Gadget { key = "adrophin",  name = "アドロピン注射器", note = "3ターンの間、機動力+20%・追加体力+30", price = 5000, medical = true, art = "23_adrophin_injector", turns = 3, mobilityPct = 20, bonusHp = 30 },
            new Gadget { key = "stimulant", name = "覚醒薬",         note = "3ターンの間、命中率+20%・ヘッドショット率+10%", price = 5000, medical = true, art = "24_stimulant", turns = 3, accPct = 20, headshotPct = 10 },
            new Gadget { key = "defib",     name = "除細動器",       note = "KIA時に自動で使用し、体力50%で復帰する", price = 10000, medical = true, art = "25_defibrillator", revivePct = 50 },
        };

        static readonly Dictionary<string, Sprite> gadgetArt = new Dictionary<string, Sprite>(8);

        /// <summary>The item's illustration (loaded once; null when it has none).</summary>
        public static Sprite ArtOf(Gadget g)
        {
            if (g == null || string.IsNullOrEmpty(g.art)) return null;
            if (!gadgetArt.TryGetValue(g.art, out var s)) { s = Resources.Load<Sprite>("Equipment/" + g.art); gadgetArt[g.art] = s; }
            return s;
        }

        // ---- attachments ---------------------------------------------------------------------------------------------------------
        // the parts and their effects live in Data.Attachments (the match reads them too); these forward to it
        public static string[] AttachmentSlots => Blindside.Data.Attachments.Slots;
        public const int SlotCount = Blindside.Data.Attachments.SlotCount;
        public static Attachment[] Attachments => Blindside.Data.Attachments.All;
        public static Attachment AttachmentOf(string key) => Blindside.Data.Attachments.Of(key);
        public static Sprite ArtOf(Attachment a) => Blindside.Data.Attachments.ArtOf(a);

        public static Gadget GadgetOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var g in Gadgets) if (g.key == key) return g;
            return null;
        }

        // ---- training ------------------------------------------------------------------------------------------------------------
        public const int MaxTrain = 10;

        /// <summary>The price of an operator's `done`-th training session in one stat (yen).</summary>
        public static int TrainCost(int done) => 3000 + 2000 * Mathf.Max(0, done);

        // ---- operations ----------------------------------------------------------------------------------------------------------
        public sealed class Operation
        {
            public string stem;        // MapStore stem: "" = the built-in house, "@id" = a catalog map
            public string name;
            public string scene;       // the scene it plays in (null = the battle scene)
            public MapCatalog.Entry entry;
            public int money, contribution;
            public float expScale;
        }

        /// <summary>What the war room offers now (built each time it opens).</summary>
        public static void Operations(List<Operation> into)
        {
            into.Clear();
            var house = Progress.StageRewards.For("");
            into.Add(new Operation { stem = "", name = string.IsNullOrEmpty(house.label) ? "基本邸宅" : house.label, money = house.money, contribution = house.contribution, expScale = house.expScale });
            var cat = MapCatalog.Instance;
            if (cat == null) return;
            foreach (var e in cat.entries)
            {
                if (e == null || !e.listed || e.mapJson == null) continue;
                string stem = MapCatalog.StemPrefix + e.id;
                var r = Progress.StageRewards.For(stem);
                into.Add(new Operation
                {
                    stem = stem, entry = e, scene = string.IsNullOrEmpty(e.sceneName) ? null : e.sceneName,
                    name = !string.IsNullOrEmpty(r.label) ? r.label : (string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName),
                    money = r.money, contribution = r.contribution, expScale = r.expScale,
                });
            }
        }

        // ---- station greetings (the station screen's speech bubble: one at random each time it opens) ------------------------------
        public const string ClerkKey = "CLERK";

        public static string[] StationLines(string key)
        {
            switch (key)
            {
                case "SERUM": return new[] { "医薬品は足りているか？", "どこが痛くて来たんだ？" };
                case "IGNIS": return new[] { "おう！新入り、何を買いに来た？", "新しい装備ってのは、いつだってワクワクするもんだよな？" };
                case "BASTION": return new[] { "強靭な肉体にこそ、強靭な精神が宿るものだ。", "鍛錬を怠るなよ。" };
                // (later: a line per operation, shown while the pointer is on it)
                case "CRUSHER": return new[] { "これが現在の作戦だ。よく選ぶように。" };
                case ClerkKey: return new[] { "いらっしゃい。今日は何にする？" };
            }
            return new[] { "……" };
        }

        /// <summary>BASTION, once a stat is chosen: who trains with him.</summary>
        public const string TrainPartnerLine = "共に鍛える隊員を選べ。";

        // ---- talk ----------------------------------------------------------------------------------------------------------------
        public static readonly string[] TalkKeys = { "CRUSHER", "SERUM", "IGNIS", "BASTION" };

        public static string[] Lines(string key)
        {
            switch (key)
            {
                case "CRUSHER": return new[]
                {
                    "よう、戻ったか。次の作戦まで少し休んでおけ。",
                    "壁の向こうに何がいるかは、開けてみるまで分からん。だから俺が開ける。",
                    "次の任務はここで受けろ。準備ができたら出撃だ。",
                };
                case "SERUM": return new[]
                {
                    "怪我はない？ 無理だけはしないでね。",
                    "回復薬や救急キットは、ここで用意してあるわ。",
                    "訓練所で鍛えた分は、必ず現場で生きるわ。",
                };
                case "IGNIS": return new[]
                {
                    "強化壁だろうと関係ない。焼き切るだけだ。",
                    "能力の割り振りと装備の調整は俺が見てやる。",
                    "新しいアタッチメントはショップで手に入る。",
                };
                case "BASTION": return new[]
                {
                    "盾の後ろにいろ。それが一番安全だ。",
                    "鍛えたい能力を選べ。付き合ってやる。",
                    "次の現場も、全員で帰るぞ。",
                };
            }
            return new[] { "……" };
        }
    }
}
