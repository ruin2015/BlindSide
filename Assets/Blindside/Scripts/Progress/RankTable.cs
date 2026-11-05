// The commander's rank (the player): 貢献度 (contribution points) earned by successful operations climb sixteen steps —
// 二等兵 4 → 3 → 2 → 1 → 一等兵 4 … → 上等兵 … → 兵長 1. Steps[i] is what rank i needs to reach rank i+1 (not a running total).
using UnityEngine;

namespace Blindside.Progress
{
    public static class RankTable
    {
        public static readonly string[] Tiers = { "二等兵", "一等兵", "上等兵", "兵長" };
        public static readonly string[] TiersEn = { "PRIVATE", "PRIVATE 1ST CLASS", "CORPORAL", "SERGEANT" };

        /// <summary>Points to go from rank i to rank i+1 (15 promotions).</summary>
        public static readonly int[] Steps = { 500, 750, 1000, 1500, 2200, 3000, 3900, 5000, 6000, 7200, 8500, 10000, 12000, 15000, 20000 };

        public static int Count => Steps.Length + 1;
        public static int Top => Steps.Length;

        /// <summary>Total points needed to reach rank `index`.</summary>
        public static int Threshold(int index)
        {
            int sum = 0;
            for (int i = 0; i < Mathf.Min(index, Steps.Length); i++) sum += Steps[i];
            return sum;
        }

        /// <summary>The rank a total of `points` stands at (0 = 二等兵 4).</summary>
        public static int RankOf(int points)
        {
            int r = 0, sum = 0;
            while (r < Steps.Length && points >= sum + Steps[r]) { sum += Steps[r]; r++; }
            return r;
        }

        /// <summary>"二等兵 4" … "兵長 1".</summary>
        public static string Name(int index)
        {
            index = Mathf.Clamp(index, 0, Top);
            return Tiers[index / 4] + " " + (4 - index % 4);
        }

        public static string NameEn(int index)
        {
            index = Mathf.Clamp(index, 0, Top);
            return TiersEn[index / 4] + " " + (4 - index % 4);
        }

        /// <summary>Progress 0–1 from rank `index` to the next (1 at the top).</summary>
        public static float Progress(int points, int index)
        {
            if (index >= Top) return 1f;
            return Mathf.Clamp01((points - Threshold(index)) / (float)Steps[index]);
        }
    }
}
