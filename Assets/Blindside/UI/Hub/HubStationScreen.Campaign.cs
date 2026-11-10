// The campaign's side of the HQ stations (Progress.Campaign), the same screen and option tiles as the stations:
//   作戦室 (CRUSHER, F)  メイン作戦 — the next main quest (its due day) · サブクエスト — the sub quests open at this time of day (1日)
//   会話 (G on an operator; F on one who runs no station)  交流 (½日: co-op with FLARE) · 褒賞休暇 (the leave days earned, 1 … 3 days at
//                       once, a big co-op step) · 話す (the old talk) — CRUSHER adds サブクエスト, BASTION トレーニング
//   炊事場 (the cook, F)  炊事支援 with an operator (½日): the next sortie's squad +HP (Campaign.MealHp) and a little co-op
//   補給係's 贈り物     a gift to an operator (three prices, three sizes of co-op)
// Every action that takes time moves the calendar (Campaign.Spend) and saves; once the next main quest falls due the HQ sends the squad
// out (HubScreen.TimeSpent → OpenDeadline: CRUSHER and 出撃 alone).
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Hub;
using Blindside.Progress;
using Blindside.Save;

namespace Blindside.UI
{
    public partial class HubStationScreen
    {
        public const string KitchenKey = "KITCHEN";
        static readonly (string name, int price, int tier)[] Gifts = { ("缶コーヒー", 500, 0), ("お菓子の詰め合わせ", 2000, 1), ("ミリタリーウォッチ", 8000, 2) };

        // ---- 作戦室 ------------------------------------------------------------------------------------------------------------------
        void CampaignOps()
        {
            var main = Campaign.NextMain(D);
            int left = Campaign.DaysLeft(D);
            var list = new List<Option>();
            if (main != null)
                list.Add(new Option
                {
                    title = "メイン作戦", icon = "✦",
                    sub = main.name + (left >= 0 ? "  ·  期日まで " + left + "日" : ""),
                    value = "出撃", click = () => SortieQuest(main),
                    hoverLine = "本命の作戦だ。準備ができたらいつでも出られる。",
                });
            else list.Add(new Option { title = "メイン作戦", icon = "✦", sub = "現在、次の作戦の指示はない", enabled = false });
            var subs = Campaign.Subs();
            list.Add(new Option
            {
                title = "サブクエスト", icon = "◎", sub = subs.Count > 0 ? subs.Count + "件  ·  1件 1日" : "受けられる依頼はない",
                enabled = subs.Count > 0, click = SubQuests,
            });
            SetOptions("作戦を選ぶ", list.ToArray());
            onBack = null;
        }

        void SubQuests()
        {
            var subs = Campaign.Subs();
            var list = new Option[subs.Count];
            for (int i = 0; i < subs.Count; i++)
            {
                var q = subs[i];
                bool open = Campaign.TimeOpen(q, D);
                var rec = D.RecordOf(q.stem);
                list[i] = new Option
                {
                    title = q.name, icon = q.time == QuestTime.Night ? "◎" : "✦",
                    sub = Campaign.TimeLabel(q.time) + "  ·  報酬 " + q.money.ToString("N0") + " 円  ·  1日",
                    value = open ? "成功 " + rec.wins : (q.time == QuestTime.Night ? "夜のみ" : "昼のみ"),
                    enabled = open, click = () => SortieQuest(q),
                };
            }
            SetOptions("サブクエスト — 1日かかる", list);
            SetLine(Campaign.IsNight(D) ? "夜の依頼もある。夜戦は視界が悪い、気をつけろ。" : "手の空いている今のうちに、片付けておくか。");
            onBack = () => { CampaignOps(); };
        }

        void SortieQuest(Quest q)
        {
            if (q == null) return;
            Close();
            screen.Sortie(new HubCatalog.Operation { stem = q.stem, entry = q.entry, scene = q.scene, name = q.name, money = q.money, contribution = q.contribution });
        }

        /// <summary>The next main quest has fallen due: CRUSHER, and 出撃 alone.</summary>
        public void OpenDeadline()
        {
            var main = Campaign.NextMain(D);
            if (main == null) return;
            Open("CRUSHER");
            SetLine("時間だ、フレア。作戦の期日が来た。全員、出撃準備！");
            SetOptions("メイン作戦 — 期日", new Option { title = main.name, icon = "✦", sub = "期日の作戦。今すぐ出撃する", value = "出撃", click = () => SortieQuest(main) });
            forced = true;   // (no way out: ESC / 戻る stay here)
        }

        // ---- 会話 --------------------------------------------------------------------------------------------------------------------
        /// <summary>G on an operator: spend time with them (FLARE's co-op), take a leave together, or just talk.</summary>
        public void OpenTalk(string key)
        {
            Open(key, talk: true);
        }

        void TalkOptions(string key)
        {
            var op = BuildAssetRegistry.Operator(key);
            string name = op != null ? op.DisplayName : key;
            int pts = Campaign.Points(D, Campaign.Commander, key), rank = Campaign.Level(pts);
            var list = new List<Option>();
            list.Add(new Option
            {
                title = "交流", sub = "する  ·  ½日", icon = "✚", face = OperatorArt.Face(key),
                value = "コープ Rank " + rank, enabled = Editable, click = () => Hangout(key),
                hoverLine = "少し時間あるか？ 付き合えよ。",
            });
            list.Add(new Option
            {
                title = "褒賞休暇", sub = "で一緒に出かける  ·  残り " + D.leaveDays + "日", icon = "➜",
                value = D.leaveDays > 0 ? "日数を選ぶ" : "休暇なし", enabled = Editable && D.leaveDays > 0, click = () => LeaveDays(key),
            });
            if (key == "CRUSHER") list.Add(new Option { title = "サブクエスト", sub = "を受ける  ·  1日", icon = "◎", click = SubQuests });
            if (key == "BASTION") list.Add(new Option { title = "トレーニング", sub = "する  ·  ½日", icon = "▣", click = () => { BastionStats(); onBack = () => TalkOptions(key); } });
            list.Add(new Option { title = "話す", sub = "雑談する", icon = "◎", click = () => { Close(); screen.OpenChat(key); } });
            SetOptions(name + "  ·  コープ Rank " + rank + " / " + Campaign.CoopNeed.Length, list.ToArray());
            onBack = null;
        }

        void Hangout(string key)
        {
            if (!Editable) return;
            Campaign.Spend(D, Campaign.CostTalk);
            bool up = Campaign.AddCoop(D, Campaign.Commander, key, Campaign.CoopTalk);
            AfterAction(key, up, HangoutLine(key));
        }

        static string HangoutLine(string key)
        {
            switch (key)
            {
                case "CRUSHER": return "お前と話してると、昔の部隊を思い出す。……いい意味でな。";
                case "SERUM": return "ふふ、たまにはこういう時間も必要よ。また来てね。";
                case "IGNIS": return "装備の話なら一晩中でもいけるぜ。次は新しいパーツを見せてやる。";
                case "BASTION": return "肩の力が抜けたな。休むのも訓練のうちだ。";
                case "GHOST": return "……静かな時間は嫌いじゃない。お前となら、なおさらだ。";
                case "SILENCE": return "ノイズのない会話って貴重なの。……ありがとう、フレア。";
            }
            return "いい時間だった。";
        }

        void LeaveDays(string key)
        {
            int max = Mathf.Min(3, D.leaveDays);
            var list = new Option[max];
            for (int i = 0; i < max; i++)
            {
                int days = i + 1;
                bool late = Campaign.WouldPassDeadline(D, days * Campaign.Day);
                list[i] = new Option
                {
                    title = days + "日", sub = "の休暇  ·  コープ +" + (Campaign.CoopLeave + Campaign.CoopLeavePerExtraDay * i), icon = "➜",
                    value = late ? "期日を過ぎる" : "", click = () => TakeLeave(key, days),
                };
            }
            SetOptions("褒賞休暇 — 何日出かける？", list);
            SetLine("休暇か！ どこへ行く？ ……何日いける？");
            onBack = () => TalkOptions(key);
        }

        void TakeLeave(string key, int days)
        {
            if (!Editable || D.leaveDays < days) return;
            D.leaveDays -= days;
            Campaign.Spend(D, days * Campaign.Day);
            bool up = Campaign.AddCoop(D, Campaign.Commander, key, Campaign.CoopLeave + Campaign.CoopLeavePerExtraDay * (days - 1));
            AfterAction(key, up, days + "日間、街で過ごした。……楽しかったな。また行こう。");
        }

        // ---- 炊事場 ------------------------------------------------------------------------------------------------------------------
        void KitchenOptions()
        {
            var squad = HubCatalog.Squad;
            var list = new List<Option>();
            foreach (var op in squad)
            {
                if (op == null || op.key == Campaign.Commander) continue;
                var o = op;
                list.Add(new Option
                {
                    title = o.DisplayName, face = OperatorArt.Face(o.key), sub = "と炊事支援  ·  ½日  ·  次の出撃 最大HP +" + Campaign.MealHp,
                    value = "コープ Rank " + Campaign.Rank(D, o.key), enabled = Editable, click = () => Cook(o.key),
                });
            }
            SetOptions(D.mealBuff ? "炊事支援 — 次の出撃の食事は用意済み" : "炊事支援 — 誰と作る？", list.ToArray());
            onBack = null;
        }

        void Cook(string key)
        {
            if (!Editable) return;
            Campaign.Spend(D, Campaign.CostCooking);
            D.mealBuff = true;
            bool up = Campaign.AddCoop(D, Campaign.Commander, key, Campaign.CoopCooking);
            var op = BuildAssetRegistry.Operator(key);
            AfterAction(null, up, (op != null ? op.DisplayName : key) + "と大鍋いっぱいのカレーを作った。次の出撃は体が軽いぞ。", key);
        }

        // ---- 贈り物 (the supply clerk) ---------------------------------------------------------------------------------------------
        void GiftTargets()
        {
            var squad = HubCatalog.Squad;
            var list = new List<Option>();
            foreach (var op in squad)
            {
                if (op == null || op.key == Campaign.Commander) continue;
                var o = op;
                list.Add(new Option { title = o.DisplayName, face = OperatorArt.Face(o.key), sub = "に贈り物をする", value = "コープ Rank " + Campaign.Rank(D, o.key), click = () => GiftItems(o.key) });
            }
            SetOptions("贈り物 — 誰に？", list.ToArray());
            onBack = () => { ClerkOptions(); };
        }

        void GiftItems(string key)
        {
            var list = new Option[Gifts.Length];
            for (int i = 0; i < Gifts.Length; i++)
            {
                var g = Gifts[i];
                list[i] = new Option
                {
                    title = g.name, icon = "▣", sub = "コープ +" + Campaign.CoopGift[g.tier], value = g.price.ToString("N0") + " 円",
                    enabled = Editable && D.money >= g.price, click = () => Give(key, g.name, g.price, g.tier),
                };
            }
            SetOptions("贈り物を選ぶ", list);
            onBack = GiftTargets;
        }

        void Give(string key, string item, int price, int tier)
        {
            if (!Editable || D.money < price) return;
            D.money -= price;
            bool up = Campaign.AddCoop(D, Campaign.Commander, key, Campaign.CoopGift[tier]);
            var op = BuildAssetRegistry.Operator(key);
            SaveSystem.Write(false);
            RefreshMoney();
            screen.RefreshTopbar();
            SetLine(item + "を" + (op != null ? op.DisplayName : key) + "に渡しておくよ。" + (up ? "  ……コープランクが上がったみたいだね！" : ""));
            GiftItems(key);
        }

        // ---- after an action that took time ---------------------------------------------------------------------------------------
        /// <summary>The calendar moved: saved, the topbar redrawn, the line (and the rank-up) said; the main quest's day may have come.</summary>
        void AfterAction(string talker, bool rankUp, string line, string bondWith = null)
        {
            SaveSystem.Write(false);
            RefreshMoney();
            string who = talker ?? bondWith;
            if (rankUp && who != null) line = "【コープランク UP — Rank " + Campaign.Rank(D, who) + "】  " + Campaign.RankUpLine(who, Campaign.Rank(D, who));
            SetLine(line);
            if (talker != null) TalkOptions(talker);
            else if (stationKey == KitchenKey) KitchenOptions();
            if (screen.TimeSpent()) return;   // (the main quest's day: the HQ has taken over)
        }
    }
}
