// The campaign around the matches: quests, the calendar, the squad's bonds (co-op) and how well an operation went (rank).
//   quests    メイン (the story, in order: the built-in house first, then the map catalog's main entries by mainOrder) and サブ (any other
//             listed catalog map marked sub — repeatable, each open only by day, by night (夜戦) or at any time)
//   calendar  the save counts half days (halfDays: even = 昼, odd = 夜; day 1 is halfDays 0..1). Things done at the HQ spend them
//             (Costs). Once a main quest is won the next one is due daysAfterPrevious days later (mainDeadline): when the calendar reaches
//             it, the HQ sends the squad out on it (HubScreen)
//   co-op     FLARE (the protagonist, Commander) and each operator: bond points growing a co-op rank 1 … 10 (CoopNeed), the way a
//             confidant grows; between two other operators only a light bond (points, no rank). What gives how much: Coop*
//   rank      S … E from the damage the squad took, how much of the enemy fell and the rounds used (RankRules); it scales the reward
//             and S / A earn 褒賞休暇 (leave days, spent at the HQ with an operator)
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Flow;
using Blindside.Save;

namespace Blindside.Progress
{
    public enum QuestKind { Main = 0, Sub = 1 }
    public enum QuestTime { Any = 0, Day = 1, Night = 2 }

    public class Quest
    {
        public string stem = "";            // "" = the built-in house, "@id" = a catalog map
        public string name = "";
        public QuestKind kind;
        public QuestTime time;
        public int order;                   // a main quest's place in the story (1, 2, …)
        public int daysAfterPrevious = 15;  // a main quest: due this many days after the one before it was won
        public MapCatalog.Entry entry;      // null: the house
        public string scene;                // the catalog map's own battle scene (null: the default)
        public int money, contribution;
    }

    public static class Campaign
    {
        public const string Commander = "FLARE";   // the protagonist: every co-op rank is FLARE's with someone
        public const int HalfDay = 1, Day = 2;

        // ---- what things cost (half days) ---------------------------------------------------------------------------------------
        public const int CostSubQuest = Day, CostTalk = HalfDay, CostTraining = HalfDay, CostCooking = HalfDay;

        // ---- what they give (bond points) ---------------------------------------------------------------------------------------
        public const int CoopLeave = 30, CoopLeavePerExtraDay = 10, CoopTalk = 15, CoopTraining = 3, CoopCooking = 3, CoopCombat = 1;
        public static readonly int[] CoopGift = { 4, 6, 9 };          // by the gift's price tier
        /// <summary>The points each co-op level needs (level = how many are reached, 0 … 5).</summary>
        public static readonly int[] CoopNeed = { 10, 25, 45, 70, 100, 135, 175, 220, 270, 330 };   // ranks 1 … 10
        public const int MealHp = 10;   // 炊事支援: the next sortie's squad +10 max HP

        // ---- quests ----------------------------------------------------------------------------------------------------------------
        public static List<Quest> Quests()
        {
            var list = new List<Quest>();
            var house = StageRewards.For("");
            list.Add(new Quest { stem = "", name = string.IsNullOrEmpty(house.label) ? "基本邸宅" : house.label, kind = QuestKind.Main, order = 1,
                                 money = house.money, contribution = house.contribution });
            var cat = MapCatalog.Instance;
            if (cat != null)
                foreach (var e in cat.entries)
                {
                    if (e == null || !e.listed || e.mapJson == null) continue;
                    string stem = MapCatalog.StemPrefix + e.id;
                    var r = StageRewards.For(stem);
                    list.Add(new Quest
                    {
                        stem = stem, entry = e, scene = string.IsNullOrEmpty(e.sceneName) ? null : e.sceneName,
                        name = !string.IsNullOrEmpty(r.label) ? r.label : (string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName),
                        kind = (QuestKind)e.questKind, time = (QuestTime)e.questTime, order = e.mainOrder, daysAfterPrevious = e.daysAfterPrevious,
                        money = r.money, contribution = r.contribution,
                    });
                }
            return list;
        }

        public static List<Quest> Mains()
        {
            var list = Quests().FindAll(q => q.kind == QuestKind.Main);
            list.Sort((a, b) => a.order.CompareTo(b.order));
            return list;
        }

        public static List<Quest> Subs() => Quests().FindAll(q => q.kind == QuestKind.Sub);

        public static Quest Find(string stem) => Quests().Find(q => q.stem == (stem ?? ""));

        /// <summary>The main quest to do next (null: the story so far is done).</summary>
        public static Quest NextMain(SaveData d)
        {
            var m = Mains();
            return d != null && d.mainCleared < m.Count ? m[d.mainCleared] : null;
        }

        public static int MainIndex(string stem)
        {
            var m = Mains();
            for (int i = 0; i < m.Count; i++) if (m[i].stem == (stem ?? "")) return i;
            return -1;
        }

        // ---- the calendar ----------------------------------------------------------------------------------------------------------
        public static int DayNumber(SaveData d) => d.halfDays / 2 + 1;
        public static bool IsNight(SaveData d) => (d.halfDays & 1) == 1;
        public static string TimeName(SaveData d) => IsNight(d) ? "夜" : "昼";

        /// <summary>Days until the next main quest is due (−1: none is due).</summary>
        public static int DaysLeft(SaveData d)
        {
            if (d == null || d.mainDeadline < 0 || NextMain(d) == null) return -1;
            return Mathf.Max(0, Mathf.CeilToInt((d.mainDeadline - d.halfDays) / 2f));
        }

        public static bool DeadlineReached(SaveData d) => d != null && d.mainDeadline >= 0 && NextMain(d) != null && d.halfDays >= d.mainDeadline;

        /// <summary>The calendar moves on `halves` half days (an action at the HQ, a sub quest, a leave).</summary>
        public static void Spend(SaveData d, int halves)
        {
            if (d == null || halves <= 0) return;
            d.halfDays += halves;
        }

        /// <summary>Would `halves` more half days pass the next main quest's day?</summary>
        public static bool WouldPassDeadline(SaveData d, int halves) => d != null && d.mainDeadline >= 0 && NextMain(d) != null && d.halfDays + halves >= d.mainDeadline;

        public static bool TimeOpen(Quest q, SaveData d) =>
            q.time == QuestTime.Any || (q.time == QuestTime.Night) == IsNight(d);

        public static string TimeLabel(QuestTime t) => t == QuestTime.Night ? "夜戦" : t == QuestTime.Day ? "昼間" : "随時";

        // ---- after a quest ---------------------------------------------------------------------------------------------------------
        /// <summary>A quest's end, written into the save with the rest of the result: a main quest won moves the story on (the next one due
        /// its days later); a good rank earns leave; the meal buff is used up.</summary>
        public static void QuestEnded(SaveData d, string stem, bool win, QuestRank rank)
        {
            if (d == null) return;
            d.mealBuff = false;
            if (!win) return;
            d.leaveDays += RankRules.LeaveDays(rank);
            int i = MainIndex(stem);
            if (i < 0 || i + 1 <= d.mainCleared) return;
            d.mainCleared = i + 1;
            var next = NextMain(d);
            d.mainDeadline = next != null ? d.halfDays + next.daysAfterPrevious * Day : -1;
        }

        /// <summary>A sortie: a sub quest takes its day now.</summary>
        public static void Sortied(SaveData d, string stem)
        {
            var q = Find(stem);
            if (d != null && q != null && q.kind == QuestKind.Sub) Spend(d, CostSubQuest);
        }

        // ---- co-op -------------------------------------------------------------------------------------------------------------------
        public static CoopLink LinkOf(SaveData d, string a, string b)
        {
            if (string.CompareOrdinal(a, b) > 0) { var t = a; a = b; b = t; }
            if (d.coop == null) d.coop = new List<CoopLink>();
            foreach (var l in d.coop) if (l != null && l.a == a && l.b == b) return l;
            var made = new CoopLink { a = a, b = b };
            d.coop.Add(made);
            return made;
        }

        public static int Points(SaveData d, string a, string b) => d == null || a == b ? 0 : LinkOf(d, a, b).points;

        public static int Level(int points)
        {
            int n = 0;
            foreach (var need in CoopNeed) if (points >= need) n++;
            return n;
        }

        /// <summary>Adds bond points between `a` and `b`; true when it raised FLARE's co-op rank with the other (a bond between two other
        /// operators has no rank: always false).</summary>
        public static bool AddCoop(SaveData d, string a, string b, int points)
        {
            if (d == null || string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b || points <= 0) return false;
            var l = LinkOf(d, a, b);
            int before = Level(l.points);
            l.points += points;
            return (a == Commander || b == Commander) && Level(l.points) > before;
        }

        /// <summary>FLARE's co-op rank with `key` (0 … CoopNeed.Length).</summary>
        public static int Rank(SaveData d, string key) => d == null || key == Commander ? 0 : Level(Points(d, Commander, key));

        /// <summary>The share of the way from this rank to the next (1 at the top).</summary>
        public static float LevelProgress(int points)
        {
            int lv = Level(points);
            if (lv >= CoopNeed.Length) return 1f;
            int from = lv == 0 ? 0 : CoopNeed[lv - 1], to = CoopNeed[lv];
            return Mathf.Clamp01((points - from) / (float)(to - from));
        }

        /// <summary>What `key` says when FLARE's co-op rank with them reaches `rank`.</summary>
        public static string RankUpLine(string key, int rank)
        {
            if (rank >= CoopNeed.Length) return "……お前となら、どこへでも行ける。最後まで背中は任せた。";
            if (rank >= 7) return "最近、お前の動きがよく読める。不思議なもんだな。";
            if (rank >= 4) return "前より少し、お前のことが分かってきた気がする。";
            return "……悪くない時間だった。また話そう、フレア。";
        }
    }

    // ---- the rank -----------------------------------------------------------------------------------------------------------------
    public enum QuestRank { S, A, B, C, D, E }

    public class RankResult
    {
        public QuestRank rank = QuestRank.E;
        public float score, damage, elimination, speed;
        public int rounds, killed, enemies, taken, maxHp, down;
    }

    public static class RankRules
    {
        // the parts' weights and the score each rank needs
        public const float WDamage = .35f, WElimination = .35f, WSpeed = .30f;
        static readonly float[] Need = { .88f, .76f, .62f, .48f, .34f };   // S A B C D (below: E)
        public const int ParRounds = 4, WorstRounds = 12;
        public const float DownPenalty = .15f;

        public static string Letter(QuestRank r) => r.ToString();

        /// <summary>The reward's scale (money, contribution, experience).</summary>
        public static float Multiplier(QuestRank r)
        {
            switch (r) { case QuestRank.S: return 1.5f; case QuestRank.A: return 1.25f; case QuestRank.B: return 1f; case QuestRank.C: return .85f; case QuestRank.D: return .7f; default: return .5f; }
        }

        /// <summary>褒賞休暇 earned: S 3 days, A 1.</summary>
        public static int LeaveDays(QuestRank r) => r == QuestRank.S ? 3 : r == QuestRank.A ? 1 : 0;

        public static RankResult Evaluate(MatchRecord rec, OperatorTeam side, int rounds, bool win)
        {
            var res = new RankResult { rounds = rounds };
            if (rec == null) return res;
            foreach (var r in rec.Lines)
            {
                if (r.team == side)
                {
                    if (r.captive) continue;
                    res.taken += r.damageTaken; res.maxHp += Mathf.Max(1, r.maxHp);
                    if (!r.alive) res.down++;
                }
                else
                {
                    res.enemies++;
                    if (!r.alive) res.killed++;
                }
            }
            res.damage = Mathf.Clamp01(1f - (res.maxHp > 0 ? res.taken / (float)res.maxHp : 0f) - res.down * DownPenalty);
            res.elimination = res.enemies > 0 ? res.killed / (float)res.enemies : 1f;
            res.speed = Mathf.Clamp01(1f - (rounds - ParRounds) / (float)(WorstRounds - ParRounds));
            res.score = WDamage * res.damage + WElimination * res.elimination + WSpeed * res.speed;
            res.rank = QuestRank.E;
            if (win)
                for (int i = 0; i < Need.Length; i++) if (res.score >= Need[i]) { res.rank = (QuestRank)i; break; }
            return res;
        }
    }
}
