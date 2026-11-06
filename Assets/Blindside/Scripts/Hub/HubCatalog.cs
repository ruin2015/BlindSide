// The squad HQ's rules and goods (phase 1 — tuned later):
//   Squad       the attack squad (MatchBootstrap.AttackerKeys) from the asset registry; the HQ manages these five
//   Weapons     every squad operator's guns (OperatorData.weapons). The first is the operator's own (owned from the start); the others
//               are sold at the shop (WeaponPrice) and, once bought, can be equipped (装備設定) by whoever lists them
//   Gadgets     consumables sold at the shop, one carried per operator (装備設定). Stocked and equipped in the save; the match does not
//               read them yet (phase 2)
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
    public enum HubStation { Growth, Shop, Talk, Training, Operations, Loadout }

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
        }

        public const int MaxGadgetStock = 9;

        public static readonly Gadget[] Gadgets =
        {
            new Gadget { key = "frag",  name = "破片手榴弾",   note = "爆発で範囲内の敵にダメージを与える",   price = 4000 },
            new Gadget { key = "smoke", name = "スモーク弾",   note = "煙幕で一定範囲の視界を遮る",         price = 2500 },
            new Gadget { key = "flash", name = "閃光弾",       note = "範囲内の敵の命中率を一時的に下げる",   price = 3000 },
            new Gadget { key = "medkit", name = "救急キット",  note = "体力を回復する",                     price = 3500 },
            new Gadget { key = "drone", name = "予備ドローン", note = "偵察ドローンを1機追加する",           price = 6000 },
        };

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
                    "装備の手入れは怠るなよ。作戦室で次の任務を確認しておけ。",
                };
                case "SERUM": return new[]
                {
                    "怪我はない？ 無理だけはしないでね。",
                    "訓練所で鍛えた分は、必ず現場で生きるわ。",
                    "救急キットはショップで補充できるわよ。",
                };
                case "IGNIS": return new[]
                {
                    "強化壁だろうと関係ない。焼き切るだけだ。",
                    "新しい銃が入ったらしい。ショップを覗いてみろ。",
                    "準備ができたら作戦室へ行け。俺はいつでも出られる。",
                };
                case "BASTION": return new[]
                {
                    "盾の後ろにいろ。それが一番安全だ。",
                    "能力強化はこまめにな。ポイントを残しても意味はない。",
                    "次の現場も、全員で帰るぞ。",
                };
            }
            return new[] { "……" };
        }
    }
}
