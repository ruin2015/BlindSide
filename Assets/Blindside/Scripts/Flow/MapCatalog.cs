// (Blindside: the maps a build ships — Resources/BlindsideMapCatalog.asset. Each entry is a map exported from the editor
//  (Blindside > Scenes > Export Saved Map to Map Scene…): its data (Assets/Blindside/Maps/{id}.json) and its own battle scene
//  (Assets/Blindside/Scenes/Maps/{id}.unity, a copy of Battle a planner can dress). The title lists them after the default house.
//  Maps are known by id, never by name.)
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Flow
{
    [CreateAssetMenu(menuName = "Blindside/Map Catalog", fileName = "BlindsideMapCatalog")]
    public class MapCatalog : ScriptableObject
    {
        public const string ResourceName = "BlindsideMapCatalog";
        /// <summary>A catalog map's MapStore stem: this prefix + its id.</summary>
        public const string StemPrefix = "@";

        [System.Serializable]
        public class Entry
        {
            [Tooltip("Unique id (file and scene name). Never changes once exported.")]
            public string id;
            [Tooltip("Shown on the title's map card.")]
            public string displayName;
            [Tooltip("Off: kept in the project but not listed on the title.")]
            public bool listed = true;
            [Tooltip("The map's MapFile JSON.")]
            public TextAsset mapJson;
            [Tooltip("The scene the match is played in (empty = SceneFlowSettings.battleScene).")]
            public string sceneName;
            [Tooltip("The editor save it was exported from (persistentDataPath/Maps/{this}.json) — exporting that file again updates this entry.")]
            public string sourceStem;
        }

        public List<Entry> entries = new List<Entry>();

        static MapCatalog cached;
        static bool tried;

        public static MapCatalog Instance
        {
            get
            {
                if (cached == null && !tried) { cached = Resources.Load<MapCatalog>(ResourceName); tried = true; }
                return cached;
            }
        }

        public static Entry Find(string id)
        {
            var c = Instance;
            if (c == null || string.IsNullOrEmpty(id)) return null;
            foreach (var e in c.entries) if (e != null && e.id == id) return e;
            return null;
        }

        public static bool IsCatalogStem(string stem) => !string.IsNullOrEmpty(stem) && stem.StartsWith(StemPrefix);
        public static string IdOf(string stem) => IsCatalogStem(stem) ? stem.Substring(StemPrefix.Length) : null;
    }
}
