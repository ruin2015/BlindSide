// Each co-op's story, one episode per rank (Resources/BlindsideCoopStories.asset — written in the Inspector): the co-op screen shows the
// episode of the rank reached (and the ones before it). An episode not written yet shows a placeholder.
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Progress
{
    [CreateAssetMenu(menuName = "Blindside/Co-op Stories", fileName = "BlindsideCoopStories")]
    public class CoopStories : ScriptableObject
    {
        public const string ResourceName = "BlindsideCoopStories";

        [System.Serializable]
        public class Episode
        {
            [Tooltip("The operator (CRUSHER, SERUM …).")] public string key = "";
            [Tooltip("The co-op rank it belongs to (1 … 10).")] public int rank = 1;
            public string title = "";
            [TextArea(3, 8)] public string text = "";
        }

        [Tooltip("The episodes, any order (one per operator and rank).")]
        public List<Episode> episodes = new List<Episode>();

        static CoopStories cached;
        static bool tried;

        public static CoopStories Instance
        {
            get
            {
                if (cached == null && !tried) { cached = Resources.Load<CoopStories>(ResourceName); tried = true; }
                return cached;
            }
        }

        /// <summary>The episode of `key` at `rank` — a placeholder when it isn't written yet.</summary>
        public static Episode Of(string key, int rank)
        {
            var s = Instance;
            if (s != null)
                foreach (var e in s.episodes)
                    if (e != null && e.key == key && e.rank == rank && (!string.IsNullOrEmpty(e.title) || !string.IsNullOrEmpty(e.text))) return e;
            return new Episode { key = key, rank = rank, title = "第" + rank + "話", text = "（この話はまだ準備中です）" };
        }
    }
}
