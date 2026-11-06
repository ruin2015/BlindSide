// What each HQ station lists in the HubListPanel (rules and goods: HubCatalog). Every change goes into the session's save and is
// written at once (SaveSystem.Write: the SAVE notice). Without a save session (a scene played on its own) the lists show but every
// button is off. Rows are rebuilt on a click only.
//   ShopContent        装備購入        (supply clerk) 武器 (guns not owned yet) / アタッチメント (once for the squad) / ガジェット (tactical,
//                                      stock up to MaxGadgetStock)
//   MedicalContent     医務室          (SERUM) medical items, stock up to MaxGadgetStock
//   TrainingContent    訓練所          (BASTION) per operator: the five stats, money for one point each (TrainCost, MaxTrain per stat)
//   OperationsContent  作戦室          (CRUSHER) the operations on offer (HubCatalog.Operations) → 出撃
//   IgnisContent       (IGNIS) the choice first: ロードアウト or 装備購入, then that panel
//   LoadoutContent     ロードアウト    per operator: its guns, one attachment per slot, one item
//   SquadInfoContent   隊員情報        (the ESC menu) per operator: level and points (能力分配 → the growth panel), stats, ability, gun,
//                                      attachments, item
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Save;
using Blindside.UI;

namespace Blindside.Hub
{
    public abstract class HubContentBase : IHubContent
    {
        protected readonly HubScreen screen;
        protected HubContentBase(HubScreen s) { screen = s; }
        protected SaveData D => screen.Data;
        protected bool Editable => screen.Editable;

        public abstract string Title { get; }
        public abstract string En { get; }
        public virtual int TabCount => 0;
        public virtual string Tab(int i) => "";
        public abstract void Fill(int tab, List<HubRow> into);
        public abstract string Act(int tab, int row);
        public virtual string Status() => Editable ? Yen("所持金  ", D.money) : "セーブデータがありません (変更はできません)";

        public static string Yen(string head, int v) => head + "¥" + v.ToString("N0");

        protected static List<OperatorData> Squad => HubCatalog.Squad;

        protected static string SquadTab(int i) => i < Squad.Count ? Squad[i].DisplayName : "";

        /// <summary>Takes `price` from the money; false (nothing changed) when it isn't there.</summary>
        protected bool Pay(int price)
        {
            if (!Editable || D.money < price) return false;
            D.money -= price;
            return true;
        }

        protected static void Save() => SaveSystem.Write(false);

        /// <summary>A stock row for gadget `g` (shop / 医務室).</summary>
        protected HubRow GadgetRow(HubCatalog.Gadget g)
        {
            int have = D.GadgetOf(g.key).count;
            bool full = have >= HubCatalog.MaxGadgetStock;
            return new HubRow
            {
                name = g.name, note = g.note + "   所持 " + have + " / " + HubCatalog.MaxGadgetStock,
                value = Yen("", g.price), button = full ? "上限" : "購入", enabled = Editable && !full && D.money >= g.price,
            };
        }

        protected string BuyGadget(HubCatalog.Gadget g)
        {
            if (!Editable || g == null) return null;
            var line = D.GadgetOf(g.key);
            if (line.count >= HubCatalog.MaxGadgetStock) return null;
            if (!Pay(g.price)) return "所持金が足りません";
            line.count++;
            Save();
            return g.name + " を購入しました (所持 " + line.count + ")";
        }

        protected static int CountOf(bool medical)
        {
            int n = 0;
            foreach (var g in HubCatalog.Gadgets) if (g.medical == medical) n++;
            return n;
        }

        protected static HubCatalog.Gadget NthOf(bool medical, int i)
        {
            foreach (var g in HubCatalog.Gadgets) if (g.medical == medical) { if (i == 0) return g; i--; }
            return null;
        }
    }

    // ---- 装備購入 ---------------------------------------------------------------------------------------------------------------
    public sealed class ShopContent : HubContentBase
    {
        public ShopContent(HubScreen s) : base(s) { }
        public override string Title => "装備購入";
        public override string En => "EQUIPMENT SHOP";
        public override int TabCount => 3;
        public override string Tab(int i) => i == 0 ? "武器" : i == 1 ? "アタッチメント" : "ガジェット";

        static string Users(WeaponData w)
        {
            string s = "";
            foreach (var o in Squad)
                foreach (var g in HubCatalog.GunsOf(o))
                    if (g == w) { s += (s.Length > 0 ? " / " : "") + o.DisplayName; break; }
            return s;
        }

        public static string GunNote(WeaponData w) =>
            "ダメージ " + w.damage + " × " + w.shots + " · 命中 " + w.accuracy + "% · 射程 " + w.range + "m";

        public override void Fill(int tab, List<HubRow> into)
        {
            into.Clear();
            if (tab == 0)
            {
                foreach (var w in HubCatalog.WeaponsForSale)
                {
                    bool owned = HubCatalog.Owns(D, w);
                    int price = HubCatalog.WeaponPrice(w);
                    into.Add(new HubRow
                    {
                        name = w.weaponName, note = GunNote(w) + "   使用: " + Users(w),
                        value = owned ? "所持済み" : Yen("", price), current = owned,
                        button = owned ? "購入済み" : "購入", enabled = Editable && !owned && D.money >= price,
                    });
                }
                return;
            }
            if (tab == 1)
            {
                // by count: each copy goes on one gun of one operator
                foreach (var a in HubCatalog.Attachments)
                {
                    int have = D.AttachmentStockOf(a.key).count, fitted = D.AttachmentsFitted(a.key);
                    bool full = have >= SaveData.MaxAttachmentStock;
                    into.Add(new HubRow
                    {
                        face = HubCatalog.ArtOf(a),
                        name = a.name + "  (" + HubCatalog.AttachmentSlots[a.slot] + ")", note = a.Summary,
                        value = Yen("", a.price) + "  所持 " + have + (fitted > 0 ? " (装着 " + fitted + ")" : ""), current = have > 0,
                        button = full ? "上限" : "購入", enabled = Editable && !full && D.money >= a.price,
                    });
                }
                return;
            }
            foreach (var g in HubCatalog.Gadgets) if (!g.medical) into.Add(GadgetRow(g));
        }

        public override string Act(int tab, int row)
        {
            if (!Editable) return null;
            if (tab == 0)
            {
                var list = HubCatalog.WeaponsForSale;
                if (row >= list.Count) return null;
                var w = list[row];
                if (HubCatalog.Owns(D, w)) return null;
                if (!Pay(HubCatalog.WeaponPrice(w))) return "所持金が足りません";
                D.ownedWeapons.Add(w.key);
                Save();
                return w.weaponName + " を購入しました (隊員・装備管理で装備できます)";
            }
            if (tab == 1)
            {
                if (row >= HubCatalog.Attachments.Length) return null;
                var a = HubCatalog.Attachments[row];
                var line = D.AttachmentStockOf(a.key);
                if (line.count >= SaveData.MaxAttachmentStock) return null;
                if (!Pay(a.price)) return "所持金が足りません";
                line.count++;
                Save();
                return a.name + " を購入しました (所持 " + line.count + " · ロードアウトで1丁に1つ装着)";
            }
            return BuyGadget(NthOf(false, row));
        }
    }

    // ---- 医務室 ---------------------------------------------------------------------------------------------------------------
    public sealed class MedicalContent : HubContentBase
    {
        public MedicalContent(HubScreen s) : base(s) { }
        public override string Title => "医務室";
        public override string En => "MEDICAL SUPPLY";

        public override void Fill(int tab, List<HubRow> into)
        {
            into.Clear();
            foreach (var g in HubCatalog.Gadgets) if (g.medical) into.Add(GadgetRow(g));
        }

        public override string Act(int tab, int row) => BuyGadget(NthOf(true, row));
    }

    // ---- 訓練所 ----------------------------------------------------------------------------------------------------------------
    public sealed class TrainingContent : HubContentBase
    {
        public TrainingContent(HubScreen s) : base(s) { }
        public override string Title => "訓練所";
        public override string En => "TRAINING GROUND";
        public override int TabCount => Squad.Count;
        public override string Tab(int i) => SquadTab(i);

        static readonly string[] Drills = { "持久走", "防御訓練", "射撃訓練", "機動訓練", "精密射撃訓練" };

        public override void Fill(int tab, List<HubRow> into)
        {
            into.Clear();
            if (tab >= Squad.Count) return;
            var p = D.OperatorOf(Squad[tab].key);
            for (int i = 0; i < 5; i++)
            {
                int done = p.trained[i];
                bool max = done >= HubCatalog.MaxTrain;
                int cost = HubCatalog.TrainCost(done);
                into.Add(new HubRow
                {
                    name = KitGrowthPanel.StatNames[i] + "  —  " + Drills[i],
                    note = "訓練 " + done + " / " + HubCatalog.MaxTrain + " · 現在 " + p.StatPoints(i) + " ポイント · 1回で +1 ポイント",
                    value = max ? "上限" : Yen("", cost), current = done > 0, band = Squad[tab].color,
                    button = max ? "完了" : "訓練", enabled = Editable && !max && D.money >= cost,
                });
            }
        }

        public override string Act(int tab, int row)
        {
            if (!Editable || tab >= Squad.Count || row >= 5) return null;
            var op = Squad[tab];
            var p = D.OperatorOf(op.key);
            if (p.trained[row] >= HubCatalog.MaxTrain) return null;
            if (!Pay(HubCatalog.TrainCost(p.trained[row]))) return "所持金が足りません";
            p.trained[row]++;
            p.AddStat(row, 1);
            Save();
            return op.DisplayName + " の" + KitGrowthPanel.StatNames[row] + "が上がりました (" + p.StatPoints(row) + ")";
        }
    }

    // ---- 作戦室 ----------------------------------------------------------------------------------------------------------------
    public sealed class OperationsContent : HubContentBase
    {
        public OperationsContent(HubScreen s) : base(s) { }
        public override string Title => "作戦室";
        public override string En => "OPERATIONS ROOM";

        readonly List<HubCatalog.Operation> ops = new List<HubCatalog.Operation>(8);

        public override void Fill(int tab, List<HubRow> into)
        {
            into.Clear();
            HubCatalog.Operations(ops);
            foreach (var o in ops)
            {
                var rec = D.RecordOf(o.stem);
                into.Add(new HubRow
                {
                    name = o.name, note = "攻撃 · 人質救出   報酬 " + Yen("", o.money) + " · 貢献度 " + o.contribution + (o.expScale != 1f ? " · 経験値 ×" + o.expScale.ToString("0.#") : ""),
                    value = "出撃 " + rec.plays + " · 成功 " + rec.wins, current = o.stem == D.lastMap,
                    button = "出撃", enabled = true,
                });
            }
        }

        public override string Act(int tab, int row)
        {
            if (row >= ops.Count) return null;
            screen.Sortie(ops[row]);
            return "出撃準備中…";
        }

        public override string Status() => "任務を受注して出撃します";
    }

    // ---- IGNIS: the choice first --------------------------------------------------------------------------------------------------
    /// <summary>IGNIS's station: what to do — ロードアウト (the workbench: HubLoadoutScreen), アイテム (LoadoutContent, items only) or
    /// 装備購入 (the shop) — then that screen.</summary>
    public sealed class IgnisContent : HubContentBase
    {
        readonly IHubContent items, shop;
        public IgnisContent(HubScreen s, IHubContent items, IHubContent shop) : base(s) { this.items = items; this.shop = shop; }
        public override string Title => "IGNIS";
        public override string En => "LOADOUT / SHOP";

        public override void Fill(int tab, List<HubRow> into)
        {
            into.Clear();
            into.Add(new HubRow { name = "ロードアウト", note = "作業台で武器を選び、部位ごとにパーツを装着する", button = "開く", enabled = true, band = UITheme.Current.blue });
            into.Add(new HubRow { name = "アイテム", note = "隊員ごとに出撃時のアイテムを選ぶ", button = "開く", enabled = true, band = UITheme.Current.paper2 });
            into.Add(new HubRow { name = "装備購入", note = "武器・アタッチメント・ガジェットを購入する", button = "開く", enabled = true, band = UITheme.Current.acc });
        }

        public override string Act(int tab, int row)
        {
            if (row == 0) { screen.List.Close(); screen.OpenLoadout(); }
            else screen.List.Open(row == 1 ? items : shop);
            return null;
        }

        public override string Status() => "何をする？";
    }

    // ---- ロードアウト ------------------------------------------------------------------------------------------------------------
    public sealed class LoadoutContent : HubContentBase
    {
        /// <summary>Only the item rows (IGNIS's アイテム: the guns and parts are on the workbench).</summary>
        readonly bool itemsOnly;
        public LoadoutContent(HubScreen s, bool itemsOnly = false) : base(s) { this.itemsOnly = itemsOnly; }
        public override string Title => itemsOnly ? "アイテム" : "ロードアウト";
        public override string En => itemsOnly ? "ITEMS" : "LOADOUT";
        public override int TabCount => Squad.Count;
        public override string Tab(int i) => SquadTab(i);

        // the rows, in order: the guns (null ones skipped) · every attachment · item なし · every gadget
        enum Kind { Gun, Attachment, NoItem, Item }
        readonly List<Kind> kinds = new List<Kind>(24);
        readonly List<int> refs = new List<int>(24);

        void Add(List<HubRow> into, HubRow r, Kind k, int idx) { into.Add(r); kinds.Add(k); refs.Add(idx); }

        public override void Fill(int tab, List<HubRow> into)
        {
            into.Clear(); kinds.Clear(); refs.Clear();
            if (tab >= Squad.Count) return;
            var op = Squad[tab];
            var p = D.OperatorOf(op.key);

            var guns = itemsOnly ? new WeaponData[0] : HubCatalog.GunsOf(op);
            int choice = Loadout.Choice(op);
            for (int i = 0; i < guns.Length; i++)
            {
                var w = guns[i];
                if (w == null) continue;
                bool owned = HubCatalog.Owns(D, w), on = i == choice;
                var m = D.WeaponOf(w.key);
                Add(into, new HubRow
                {
                    name = "武器  " + w.weaponName, note = ShopContent.GunNote(w) + " · 熟練度 Lv " + m.level,
                    value = on ? "装備中" : (owned ? "所持" : "未所持 (装備購入)"), current = on, band = op.color,
                    button = on ? "装備中" : "装備", enabled = owned && !on && (Editable || !Application.isPlaying),
                }, Kind.Gun, i);
            }

            // the carried gun's parts: one per slot, each a copy from the stock (a copy on another gun — anyone's — is not free).
            // Listed: the parts on this gun and the ones with a free copy; 装着 takes a copy (swapping out the slot's part)
            var gun = Loadout.Of(op, true);
            var fit = gun != null ? p.FindFit(gun.key) : null;
            int shown = 0;
            if (!itemsOnly)
            for (int s = 0; s < HubCatalog.SlotCount; s++)
                for (int i = 0; i < HubCatalog.Attachments.Length; i++)
                {
                    var a = HubCatalog.Attachments[i];
                    if (a.slot != s) continue;
                    bool on = fit != null && fit.parts[a.slot] == a.key;
                    int free = D.AttachmentsFree(a.key);
                    if (!on && free <= 0) continue;
                    shown++;
                    Add(into, new HubRow
                    {
                        face = HubCatalog.ArtOf(a),
                        name = HubCatalog.AttachmentSlots[a.slot] + "  " + a.name, note = a.Summary,
                        value = on ? "装着中" : "残り " + free, current = on,
                        button = on ? "外す" : "装着", enabled = Editable && gun != null,
                    }, Kind.Attachment, i);
                }
            if (shown == 0 && !itemsOnly)
                Add(into, new HubRow { name = "アタッチメント  なし", note = "装備購入で購入すると、ここで " + (gun != null ? gun.weaponName : "武器") + " に装着できます" }, Kind.NoItem, -1);

            bool none = string.IsNullOrEmpty(p.gadget);
            Add(into, new HubRow
            {
                name = "アイテム  なし", note = "アイテムを持たずに出撃する", value = none ? "装備中" : "", current = none,
                button = none ? "装備中" : "外す", enabled = Editable && !none,
            }, Kind.NoItem, 0);
            for (int i = 0; i < HubCatalog.Gadgets.Length; i++)
            {
                var g = HubCatalog.Gadgets[i];
                int have = D.GadgetOf(g.key).count;
                bool on = p.gadget == g.key;
                Add(into, new HubRow
                {
                    name = "アイテム  " + g.name, note = g.note + (g.medical ? " (医務室)" : " (装備購入)"),
                    value = (on ? "装備中 · " : "") + "所持 " + have, current = on,
                    button = on ? "装備中" : "装備", enabled = Editable && !on && have > 0,
                }, Kind.Item, i);
            }
        }

        public override string Act(int tab, int row)
        {
            if (tab >= Squad.Count || row >= kinds.Count) return null;
            var op = Squad[tab];
            var p = D.OperatorOf(op.key);
            int i = refs[row];
            switch (kinds[row])
            {
                case Kind.Gun:
                {
                    var guns = HubCatalog.GunsOf(op);
                    if (!HubCatalog.Owns(D, guns[i])) return "まだ所持していません (装備購入で買えます)";
                    Loadout.SetChoice(op, i);
                    return op.DisplayName + " の武器を " + guns[i].weaponName + " にしました";
                }
                case Kind.Attachment:
                {
                    if (!Editable) return null;
                    var a = HubCatalog.Attachments[i];
                    var gun = Loadout.Of(op, true);
                    if (gun == null) return null;
                    var fit = p.FitOf(gun.key);
                    if (fit.parts[a.slot] == a.key) { fit.parts[a.slot] = ""; Save(); return gun.weaponName + " から" + a.name + "を外しました"; }
                    if (D.AttachmentsFree(a.key) <= 0) return "空きがありません (装備購入で買い足せます)";
                    fit.parts[a.slot] = a.key;   // (the slot's old part goes back to the stock: counts are derived from the fits)
                    Save();
                    return op.DisplayName + " の " + gun.weaponName + " に " + a.name + " を装着しました";
                }
                case Kind.NoItem:
                    if (!Editable || i < 0) return null;   // (−1: the "no attachments yet" line)
                    p.gadget = ""; Save();
                    return op.DisplayName + " のアイテムを外しました";
                case Kind.Item:
                {
                    if (!Editable) return null;
                    var g = HubCatalog.Gadgets[i];
                    if (D.GadgetOf(g.key).count <= 0) return "所持していません";
                    p.gadget = g.key;
                    Save();
                    return op.DisplayName + " に " + g.name + " を装備しました";
                }
            }
            return null;
        }

        public override string Status() => Editable ? "出撃時の武器・アタッチメント・アイテムを選びます" : base.Status();
    }

    // ---- 隊員情報 (the ESC menu) -------------------------------------------------------------------------------------------------
    /// <summary>Every operator at a glance: the stat points (強化 opens the growth panel — the only place they are given), the stats, the
    /// ability, the gun and its mastery, the attachments and the carried item. Read-only but for 強化.</summary>
    public sealed class SquadInfoContent : HubContentBase
    {
        public SquadInfoContent(HubScreen s) : base(s) { }
        public override string Title => "隊員情報";
        public override string En => "SQUAD STATUS";
        public override int TabCount => Squad.Count;
        public override string Tab(int i) => SquadTab(i);

        static readonly string[] StatShort = { "体力", "防御力", "攻撃力", "機動力", "精密力" };

        public override void Fill(int tab, List<HubRow> into)
        {
            into.Clear();
            if (tab >= Squad.Count) return;
            var op = Squad[tab];
            var p = D.OperatorOf(op.key);
            int need = Progress.ProgressRules.LevelNeed(p.level);
            into.Add(new HubRow
            {
                name = op.DisplayName + "  " + op.kr, face = OperatorArt.Face(op.key), band = op.color,
                note = op.role + " · 経験値 " + (need > 0 ? p.exp + " / " + need : "MAX"),
                value = "Lv " + p.level + "   残り " + p.points + " pt", current = p.points > 0,
                button = "能力分配", enabled = true,
            });
            for (int i = 0; i < 5; i++)
            {
                int v = p.StatPoints(i);
                string eff;
                switch (i)
                {
                    case 0: eff = "最大HP +" + (v * Progress.ProgressBonus.HpPerPoint); break;
                    case 1: eff = "被ダメージ −" + (Mathf.Min(Progress.ProgressBonus.DefMax, v * Progress.ProgressBonus.DefPerPoint) * 100f).ToString("0.#") + "%"; break;
                    case 2: eff = "与ダメージ +" + (v * Progress.ProgressBonus.AtkPerPoint * 100f).ToString("0") + "%"; break;
                    case 3: eff = "移動力 +" + (v * Progress.ProgressBonus.MovePerPoint).ToString("0.#") + "m"; break;
                    default: eff = "命中 +" + (v * Progress.ProgressBonus.AccPerPoint).ToString("0.#"); break;
                }
                into.Add(new HubRow { name = "ステータス  " + StatShort[i], note = eff + " (訓練 " + p.trained[i] + " 回)", value = v + " pt", band = op.color });
            }
            into.Add(new HubRow
            {
                name = "アビリティ  " + op.abilityName, note = string.IsNullOrEmpty(op.description) ? "" : op.description.Replace('\n', ' '),
                value = op.charges >= 99 ? "回数 無制限" : "回数 " + op.charges,
            });
            var gun = Loadout.Of(op, true);
            if (gun != null)
            {
                var m = D.WeaponOf(gun.key);
                into.Add(new HubRow { name = "武器  " + gun.weaponName, note = ShopContent.GunNote(gun), value = "熟練度 Lv " + m.level });
            }
            // the carried gun's parts
            var fit = gun != null ? p.FindFit(gun.key) : null;
            for (int s = 0; s < HubCatalog.SlotCount; s++)
            {
                var a = fit != null ? HubCatalog.AttachmentOf(fit.parts[s]) : null;
                into.Add(new HubRow { face = HubCatalog.ArtOf(a), name = HubCatalog.AttachmentSlots[s] + "  " + (a != null ? a.name : "なし"), note = a != null ? a.Summary : "", value = a != null ? "装着中" : "" });
            }
            var g = HubCatalog.GadgetOf(p.gadget);
            into.Add(new HubRow { name = "アイテム  " + (g != null ? g.name : "なし"), note = g != null ? g.note : "", value = g != null ? "所持 " + D.GadgetOf(g.key).count : "" });
        }

        public override string Act(int tab, int row)
        {
            if (row == 0 && tab < Squad.Count) screen.OpenGrowth(Squad[tab]);
            return null;
        }

        public override string Status() => "隊員を選んで確認します · 能力分配でポイントを割り振ります";
    }
}
