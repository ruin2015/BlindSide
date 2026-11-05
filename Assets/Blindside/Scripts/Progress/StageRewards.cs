// What a successful operation pays, per stage (map id: the map store's stem — "" for the built-in house, "@id" for an exported map).
// Resources/BlindsideStageRewards.asset (made by the editor on first use, edited in the Inspector). A map without its own line uses
// `fallback`. Money in yen, contribution in rank points, expScale multiplies every operator's / weapon's experience for the match.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Progress
{
    [CreateAssetMenu(fileName = "BlindsideStageRewards", menuName = "Blindside/Stage Rewards")]
    public class StageRewards : ScriptableObject
    {
        public const string ResourceName = "BlindsideStageRewards";

        [Serializable]
        public class Entry
        {
            [Tooltip("Map id: \"\" = the built-in house, a saved map's file name, \"@id\" for an exported (catalog) map.")]
            public string map = "";
            [Tooltip("The display name on the result screen (empty: the map's own).")]
            public string label = "";
            public int money = 30000;
            public int contribution = 400;
            [Min(0f)] public float expScale = 1f;
        }

        public Entry fallback = new Entry();
        public List<Entry> entries = new List<Entry> { new Entry { map = "", label = "基本邸宅", money = 30000, contribution = 400, expScale = 1f } };

        static StageRewards instance;

        public static StageRewards Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<StageRewards>(ResourceName);
                if (instance == null) { instance = CreateInstance<StageRewards>(); instance.hideFlags = HideFlags.DontSave; }
                return instance;
            }
        }

        /// <summary>The line for `map` (its own, else the fallback).</summary>
        public static Entry For(string map)
        {
            var s = Instance;
            map = map ?? "";
            if (s.entries != null)
                foreach (var e in s.entries) if (e != null && string.Equals(e.map ?? "", map, StringComparison.Ordinal)) return e;
            return s.fallback ?? new Entry();
        }
    }
}
