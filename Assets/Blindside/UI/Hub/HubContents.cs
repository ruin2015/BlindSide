// What each HQ station lists in the HubListPanel (rules and goods: HubCatalog). Every change goes into the session's save and is
// written at once (SaveSystem.Write: the SAVE notice). Without a save session (a scene played on its own) the lists show but every
// button is off. Rows are rebuilt on a click only.
//   GrowthContent      隊員強化   the squad: Lv, points left → 強化 opens the growth panel (KitGrowthPanel) on that operator
//   ShopContent        ショップ   武器 (guns not owned yet, by price) / ガジェット (stock up to MaxGadgetStock)
//   TrainingContent    訓練所     per operator: the five stats, money for one point each (TrainCost, MaxTrain per stat)
//   OperationsContent  作戦室     the operations on offer (HubCatalog.Operations) → 出撃
//   LoadoutContent     装備設定   per operator: its guns (owned ones can be equipped) and one gadget
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

        /// <summary>Takes `price` from the money and writes the save; false (nothing changed) when it isn't there.</summary>
        protected bool Pay(int price)
        {
            if (!Editable || D.money < price) return false;
            D.money -= price;
            return true;
        }

        protected static void Save() => SaveSystem.Write(false);
    }

    // ---- 隊員強化 -------------------------------------------------------------------------------------------------------------
    public sealed class GrowthContent : HubContentBase
    {
        public GrowthContent(HubScreen s) : base(s) { }
        public override string Title => "隊員強化";
        public override string En => "OPERATOR GROWTH";

        public override void Fill(int tab, List<HubRow> into)
        {
            into.Clear();
            foreach (var op in Squad)
            {
                var p = D.OperatorOf(op.key);
                into.Add(new HubRow
                {
                    name = op.DisplayName, face = OperatorArt.Face(op.key), band = op.color,
                    note = op.kr + " · " + op.role + "   体力 " + p.hp + " · 防御 " + p.def + " · 攻撃 " + p.atk + " · 機動 " + p.mob + " · 精密 " + p.prec,
                    value = "Lv " + p.level + "   残り " + p.points + " pt",
                    current = p.points > 0,
                    button = "強化", enabled = true,
                });
            }
        }

        public override string Act(int tab, int row)
        {
            if (row < Squad.Count) screen.OpenGrowth(Squad[row]);
            return null;
        }

        public override string Status() => Editable ? "レベルアップで得たポイントを能力に割り振ります" : base.Status();
    }

    // ---- ショップ ---------------------------------------------------------------------------------------------------------------
    public sealed class ShopContent : HubContentBase
    {
        public ShopContent(HubScreen s) : base(s) { }
        public override string Title => "ショップ";
        public override string En => "SUPPLY SHOP";
        public override int TabCount => 2;
        public override string Tab(int i) => i == 0 ? "武器" : "ガジェット";

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
            foreach (var g in HubCatalog.Gadgets)
            {
                int have = D.GadgetOf(g.key).count;
                bool full = have >= HubCatalog.MaxGadgetStock;
                into.Add(new HubRow
                {
                    name = g.name, note = g.note + "   所持 " + have + " / " + HubCatalog.MaxGadgetStock,
                    value = Yen("", g.price), button = full ? "上限" : "購入", enabled = Editable && !full && D.money >= g.price,
                });
            }
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
                return w.weaponName + " を購入しました (装備設定で装備できます)";
            }
            if (row >= HubCatalog.Gadgets.Length) return null;
            var g = HubCatalog.Gadgets[row];
            var line = D.GadgetOf(g.key);
            if (line.count >= HubCatalog.MaxGadgetStock) return null;
            if (!Pay(g.price)) return "所持金が足りません";
            line.count++;
            Save();
            return g.name + " を購入しました (所持 " + line.count + ")";
        }
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

    // ---- 装備設定 ---------------------------------------------------------------------------------------------------------------
    public sealed class LoadoutContent : HubContentBase
    {
        public LoadoutContent(HubScreen s) : base(s) { }
        public override string Title => "装備設定";
        public override string En => "LOADOUT";
        public override int TabCount => Squad.Count;
        public override string Tab(int i) => SquadTab(i);

        public override void Fill(int tab, List<HubRow> into)
        {
            into.Clear();
            if (tab >= Squad.Count) return;
            var op = Squad[tab];
            var guns = HubCatalog.GunsOf(op);
            int choice = Loadout.Choice(op);
            for (int i = 0; i < guns.Length; i++)
            {
                var w = guns[i];
                if (w == null) continue;
                bool owned = HubCatalog.Owns(D, w), on = i == choice;
                var m = D.WeaponOf(w.key);
                into.Add(new HubRow
                {
                    name = "武器  " + w.weaponName, note = ShopContent.GunNote(w) + " · 熟練度 Lv " + m.level,
                    value = on ? "装備中" : (owned ? "所持" : "未所持 (ショップ)"), current = on, band = op.color,
                    button = on ? "装備中" : "装備", enabled = owned && !on && (Editable || !Application.isPlaying),
                });
            }
            var p = D.OperatorOf(op.key);
            into.Add(new HubRow
            {
                name = "ガジェット  なし", note = "ガジェットを持たずに出撃する", value = string.IsNullOrEmpty(p.gadget) ? "装備中" : "",
                current = string.IsNullOrEmpty(p.gadget), button = string.IsNullOrEmpty(p.gadget) ? "装備中" : "外す", enabled = Editable && !string.IsNullOrEmpty(p.gadget),
            });
            foreach (var g in HubCatalog.Gadgets)
            {
                int have = D.GadgetOf(g.key).count;
                bool on = p.gadget == g.key;
                into.Add(new HubRow
                {
                    name = "ガジェット  " + g.name, note = g.note, value = (on ? "装備中 · " : "") + "所持 " + have, current = on,
                    button = on ? "装備中" : "装備", enabled = Editable && !on && have > 0,
                });
            }
        }

        public override string Act(int tab, int row)
        {
            if (tab >= Squad.Count) return null;
            var op = Squad[tab];
            var guns = HubCatalog.GunsOf(op);
            // the rows: guns (null ones skipped), then なし, then every gadget
            int gi = -1, seen = 0;
            for (int i = 0; i < guns.Length; i++) { if (guns[i] == null) continue; if (seen == row) { gi = i; break; } seen++; }
            if (gi >= 0)
            {
                if (!HubCatalog.Owns(D, guns[gi])) return "まだ所持していません (ショップで購入できます)";
                Loadout.SetChoice(op, gi);
                return op.DisplayName + " の武器を " + guns[gi].weaponName + " にしました";
            }
            if (!Editable) return null;
            int k = row - seen;
            var p = D.OperatorOf(op.key);
            if (k == 0) { p.gadget = ""; Save(); return op.DisplayName + " のガジェットを外しました"; }
            k--;
            if (k < 0 || k >= HubCatalog.Gadgets.Length) return null;
            var g = HubCatalog.Gadgets[k];
            if (D.GadgetOf(g.key).count <= 0) return "所持していません";
            p.gadget = g.key;
            Save();
            return op.DisplayName + " に " + g.name + " を装備しました";
        }

        public override string Status() => Editable ? "出撃時の武器とガジェットを選びます" : base.Status();
    }
}
