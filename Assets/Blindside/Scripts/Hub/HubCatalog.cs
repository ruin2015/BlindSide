// The squad HQ's rules and goods (phase 1 — tuned later):
//   Squad       the attack squad (MatchBootstrap.AttackerKeys) from the asset registry; the HQ manages these five
//   Stations    who runs what: 作戦室 CRUSHER · 訓練所 BASTION (the old gym) · 隊員・装備管理 IGNIS (the old weapon-care room: stat points,
//               guns, attachments, the carried item) · 医務室 SERUM (medical items) · 装備購入 the supply clerk (guns, attachments, gadgets)
//   Weapons     every squad operator's guns (OperatorData.weapons). The first is the operator's own (owned from the start); the others
//               are sold at the shop (WeaponPrice) and, once bought, can be equipped by whoever lists them
//   Attachments bought once for the squad (Attachments, a slot each: マズル / 光学機器 / グリップ / マガジン), one per slot per operator
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
        }

        public const int MaxGadgetStock = 9;

        public static readonly Gadget[] Gadgets =
        {
            new Gadget { key = "frag",  name = "破片手榴弾",   note = "爆発で範囲内の敵にダメージを与える",   price = 4000 },
            new Gadget { key = "smoke", name = "スモーク弾",   note = "煙幕で一定範囲の視界を遮る",         price = 2500 },
            new Gadget { key = "flash", name = "閃光弾",       note = "範囲内の敵の命中率を一時的に下げる",   price = 3000 },
            new Gadget { key = "drone", name = "予備ドローン", note = "偵察ドローンを1機追加する",           price = 6000 },
            new Gadget { key = "potion",    name = "回復薬",         note = "体力を少し回復する",             price = 1500, medical = true },
            new Gadget { key = "medkit",    name = "救急キット",     note = "体力を大きく回復する",           price = 3500, medical = true },
            new Gadget { key = "painkiller", name = "鎮痛剤",        note = "一時的に被ダメージを軽減する",   price = 2500, medical = true },
            new Gadget { key = "adrenaline", name = "アドレナリン注射", note = "一時的に移動力を上げる",      price = 3000, medical = true },
        };

        // ---- attachments ---------------------------------------------------------------------------------------------------------
        public static readonly string[] AttachmentSlots = { "マズル", "光学機器", "グリップ", "マガジン" };

        public sealed class Attachment
        {
            public string key, name, note;
            public int slot, price;
        }

        public static readonly Attachment[] Attachments =
        {
            new Attachment { key = "suppressor", slot = 0, name = "サプレッサー",     note = "発砲音を抑え、位置を知られにくくする", price = 8000 },
            new Attachment { key = "compensator", slot = 0, name = "コンペンセイター", note = "反動を抑えて命中率を上げる",           price = 6000 },
            new Attachment { key = "dotsight",  slot = 1, name = "ドットサイト",     note = "近距離での照準が速くなる",             price = 5000 },
            new Attachment { key = "scope",     slot = 1, name = "スコープ",         note = "遠距離の命中率が上がる",               price = 9000 },
            new Attachment { key = "foregrip",  slot = 2, name = "フォアグリップ",   note = "射撃の安定性が上がる",                 price = 5000 },
            new Attachment { key = "extmag",    slot = 3, name = "拡張マガジン",     note = "1回の射撃で撃てる弾数が増える",        price = 7000 },
        };

        public static Attachment AttachmentOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var a in Attachments) if (a.key == key) return a;
            return null;
        }

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
