// (Blindside: who goes out on an operation that lets the player pick — MapCatalog.Entry.squadSize. The HQ's squad screen (HubSquadScreen)
//  sets the pick just before the sortie; MapCatalog.Apply hands it to the match as MapData.squadKeys. FLARE (Campaign.Commander) always
//  goes and always leads. The last pick per map is remembered (PlayerPrefs) so the screen opens on it next time.)
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Flow
{
    public static class SquadPick
    {
        const string PrefsPrefix = "blindside.squad.";
        public const string Leader = "FLARE";

        static string pickedMap;
        static string[] picked;

        /// <summary>The squad for `entry`'s next match (set right before the sortie).</summary>
        public static void Set(string mapId, IList<string> keys)
        {
            pickedMap = mapId;
            picked = Normalise(keys, keys != null ? keys.Count : 0);
            PlayerPrefs.SetString(PrefsPrefix + mapId, string.Join(",", picked));
        }

        /// <summary>Who `e` fields: this sortie's pick, else the last one remembered, else FLARE and the first of the squad.</summary>
        public static string[] KeysFor(MapCatalog.Entry e)
        {
            if (e == null || e.squadSize <= 0) return e != null ? e.squad : null;
            if (pickedMap == e.id && picked != null && picked.Length == e.squadSize) return (string[])picked.Clone();
            return Remembered(e);
        }

        /// <summary>The last pick for `e` (filled up to its size with the default order), FLARE first.</summary>
        public static string[] Remembered(MapCatalog.Entry e)
        {
            var keys = new List<string>();
            string s = PlayerPrefs.GetString(PrefsPrefix + e.id, "");
            if (s.Length > 0) keys.AddRange(s.Split(','));
            return Normalise(keys, e.squadSize);
        }

        /// <summary>FLARE first, no repeats or unknown names, at most `size` long and filled up from the base squad when short.</summary>
        static string[] Normalise(IList<string> keys, int size)
        {
            size = Mathf.Max(1, size);
            var list = new List<string> { Leader };
            if (keys != null)
                foreach (var k in keys)
                    if (!string.IsNullOrEmpty(k) && !list.Contains(k) && list.Count < size && Blindside.Data.BuildAssetRegistry.Operator(k) != null) list.Add(k);
            foreach (var k in Blindside.Integration.MatchBootstrap.AttackerKeys)
                if (list.Count < size && !list.Contains(k)) list.Add(k);
            return list.ToArray();
        }
    }
}
