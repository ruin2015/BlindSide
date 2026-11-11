// What FLARE's co-op rank with an operator unlocks (Campaign.Rank), rank by rank:
//   1  経験値共有 I     an operator left out of a sortie still earns 1/3 of the squad's experience (MatchAward)
//   2  支援射撃        an ally fires: this operator joins in when the target is in its sight (extra damage)
//   3  経験値共有 II    … 1/2 — and the operator's own upgrade (Unique3: CRUSHER's three-panel breach, SERUM's 75 heal …)
//   4  ガジェット枠 2    two gadgets carried
//   5  経験値共有 III   … the whole share
//   6  銃 2丁          two guns, swapped when firing
//   7  ガジェット枠 3    three gadgets
//   8  能力強化         the operator's ability at its strongest (Unique8)
// Has(d, key, rank) answers "is it unlocked"; Lines(key) lists them for the co-op screen. (Which of them already act in a match is
// noted in each perk's `live`.)
using System.Collections.Generic;
using Blindside.Save;

namespace Blindside.Progress
{
    public class CoopPerk
    {
        public int rank;
        public string name = "", text = "";
        /// <summary>It already works in the game (the others are listed, their effect comes with a later update).</summary>
        public bool live;
    }

    public static class CoopPerks
    {
        public const int ExpThird = 1, Pursuit = 2, ExpHalf = 3, Unique3 = 3, Gadgets2 = 4, ExpFull = 5, TwoGuns = 6, Gadgets3 = 7, Unique8 = 8;

        public static bool Has(SaveData d, string key, int rank) => Campaign.Rank(d, key) >= rank;

        /// <summary>Item slots the operator has (1; 2 at rank 4; 3 at rank 7).</summary>
        public static int GadgetSlots(SaveData d, string key)
        {
            int r = Campaign.Rank(d, key);
            return r >= Gadgets3 ? 3 : r >= Gadgets2 ? 2 : 1;
        }

        /// <summary>Two guns, swapped in the field (rank 6).</summary>
        public static bool HasTwoGuns(SaveData d, string key) => Campaign.Rank(d, key) >= TwoGuns;

        /// <summary>The share of the squad's experience an operator left out of the sortie earns (0: none).</summary>
        public static float BenchShare(SaveData d, string key)
        {
            int r = Campaign.Rank(d, key);
            return r >= ExpFull ? 1f : r >= ExpHalf ? .5f : r >= ExpThird ? 1f / 3f : 0f;
        }

        /// <summary>Every perk of `key`, by rank (the co-op screen's list).</summary>
        public static List<CoopPerk> Lines(string key)
        {
            var u3 = Unique(key, 3); var u8 = Unique(key, 8);
            return new List<CoopPerk>
            {
                new CoopPerk { rank = 1, name = "経験値共有 I", text = "出撃しなくても経験値の1/3を得る", live = true },
                new CoopPerk { rank = 2, name = "支援射撃", text = "味方の射撃時、狙われた敵が視界にいれば一緒に撃って追加ダメージ", live = true },
                new CoopPerk { rank = 3, name = "経験値共有 II", text = "出撃しなくても経験値の1/2を得る", live = true },
                new CoopPerk { rank = 3, name = u3.name, text = u3.text, live = true },
                new CoopPerk { rank = 4, name = "ガジェット枠 II", text = "装備できるガジェットが2個に", live = true },
                new CoopPerk { rank = 5, name = "経験値共有 III", text = "出撃しなくても経験値をすべて得る", live = true },
                new CoopPerk { rank = 6, name = "二丁持ち", text = "銃を2丁装備し、射撃時に持ち替えられる", live = true },
                new CoopPerk { rank = 7, name = "ガジェット枠 III", text = "装備できるガジェットが3個に", live = true },
                new CoopPerk { rank = 8, name = u8.name, text = u8.text, live = true },
            };
        }

        static (string name, string text) Unique(string key, int rank)
        {
            if (rank == 3)
                switch (key)
                {
                    case "CRUSHER": return ("破壊戦車", "壁の破壊範囲が3枚まで広がる");
                    case "SERUM": return ("アドレナリン活性", "回復量が75に増える");
                    case "BASTION": return ("堅牢な移動", "移動力が2に増える");
                    case "IGNIS": return ("より大きな爆発", "起爆時、周囲の敵にもダメージ");
                    case "SILENCE": return ("性能強化", "探知・無力化できる範囲が広がる");
                    case "GHOST": return ("逃さない", "心拍センサーの範囲が広がる");
                }
            else
                switch (key)
                {
                    case "CRUSHER": return ("止められない力", "能力を2回分使い、強化壁も破壊できる");
                    case "SERUM": return ("ナノボット支援", "どこにいても味方を指定して回復できる");
                    case "BASTION": return ("ハイブリッド", "防御中も攻撃できる");
                    case "IGNIS": return ("焼夷爆薬", "起爆後、壁の周りが2ターン燃え続け、炎の中にいる・踏み込んだ敵に毎ターン15ダメージ");
                    case "SILENCE": return ("完全掌握", "ガジェットを無力化せず破壊し、開始時に敵のガジェットをすべて探知");
                    case "GHOST": return ("鼓動を聴く者", "開始時に敵の心拍をすべて探知。探知した敵は1ターン「弱点露出」（クリティカル率・ダメージ2倍）");
                }
            return ("能力強化", "（準備中）");
        }
    }
}
