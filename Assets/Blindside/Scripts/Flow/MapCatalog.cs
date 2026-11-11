// (Blindside: the maps a build ships — Resources/BlindsideMapCatalog.asset. Each entry is a map exported from the editor
//  (Blindside > Scenes > Export Saved Map to Map Scene…): its data (Assets/Blindside/Maps/{id}.json) and its own battle scene
//  (Assets/Blindside/Scenes/Maps/{id}.unity, a copy of Battle a planner can dress). The title lists them after the default house.
//  Maps are known by id, never by name.)
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Flow
{
    /// <summary>What an operation's challenge asks (Progress.Challenges).</summary>
    public enum ChallengeKind { NoLosses = 0, WithinRounds = 1, AllCameras = 2, AllEnemies = 3 }

    [System.Serializable]
    public class MapChallenge
    {
        public ChallengeKind kind;
        [Tooltip("WithinRounds: how many rounds.")]
        public int value;
    }

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
            [Tooltip("The rescue operation: GHOST and SILENCE are held here (the map's hostage cell and the cells below) instead of the " +
                     "civilian, the squad goes in three strong, the rescue intro plays, and a captive freed joins the squad.")]
            public bool rescue;
            [Tooltip("The rescue operation: where the second captive (SILENCE) is held. Empty: the indoor cell farthest from the hostage cell.")]
            public Vector2Int[] rescueHostages;
            [Tooltip("Quest kind: 0 メイン (the story, in mainOrder) / 1 サブ (repeatable).")]
            public int questKind = 1;
            [Tooltip("A sub quest's time: 0 any / 1 by day / 2 by night (夜戦).")]
            public int questTime;
            [Tooltip("A main quest's place in the story (the built-in house is 1).")]
            public int mainOrder = 2;
            [Tooltip("A main quest: due this many days after the one before it is won.")]
            public int daysAfterPrevious = 15;
            [Tooltip("The operation's code word: the intro title Resources/Intro/IntroOperationTitle_{code}.png (empty: OPERATION BLINDSIDE).")]
            public string operationCode;
            [Tooltip("The operation's name in the briefing: 作戦「…」.")]
            public string operationNameJa;
            [Tooltip("The briefing's WEATHER / LOCATION lines and its clock (HH:mm; empty: now).")]
            public string briefWeather, briefLocation, briefClock;
            [Tooltip("Night (moonlight, dark sky, the helicopter's searchlight) and rain (falling rain, wet surfaces) — MissionAtmosphere.")]
            public bool night, rain;
            [Tooltip("The operation's challenges: all cleared S, one missed A, two B, more C. Empty: no losses / 10 rounds / every enemy down.")]
            public MapChallenge[] challenges;
            [Tooltip("Who the player fields here, by operator key (empty: the whole squad).")]
            public string[] squad;
            [Tooltip("The operators' abilities are off on this operation.")]
            public bool noAbilities;
        }

        /// <summary>The entry's operation settings onto map data built from its JSON (the rescue operation).</summary>
        public static Blindside.Map.MapData Apply(Entry e, Blindside.Map.MapData d)
        {
            if (e == null || d == null) return d;
            if (e.rescue) { d.rescueIntro = true; d.rescueHostages = e.rescueHostages; }
            d.operationCode = e.operationCode; d.operationNameJa = e.operationNameJa;
            d.briefWeather = e.briefWeather; d.briefLocation = e.briefLocation; d.briefClock = e.briefClock;
            d.night = e.night; d.rain = e.rain;
            d.squadKeys = e.squad; d.noAbilities = e.noAbilities;
            return d;
        }

        /// <summary>The entry's map as a match reads it, its operation settings on (null, logged, when it can't be played).</summary>
        public static Blindside.Map.MapData DataOf(Entry e)
        {
            if (e == null || e.mapJson == null) return null;
            if (!Blindside.MapEditor.MapFile.TryParse(e.mapJson.text, out var file, out var why)) { Debug.LogError("[MapCatalog] " + e.id + ": " + why); return null; }
            try { return Apply(e, file.ToModel().BuildGameData()); }
            catch (System.Exception ex) { Debug.LogError("[MapCatalog] " + e.id + ": " + ex.Message); return null; }
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
