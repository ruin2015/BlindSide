// The player's progress (SaveSystem writes it as JSON, persistentDataPath/Save/save_0.json): not a match in progress — the game is
// "pick a side and a map → one match → result" — but what carries from one match to the next:
//   tutorialDone   the tutorial was finished (its ending hands over to the main operation)
//   lastTeam/Map   the side and the map of the last sortie (the select screen opens on them after a load or a return to the title)
//   matches/wins/losses and a record per map (by its id: the map store's stem, "" = the built-in house, "@id" = an exported map)
//   playSeconds    play time, added up at every save
// `version` is the format; SaveSystem upgrades older files when it reads them.
using System;
using System.Collections.Generic;
using Blindside.Data;

namespace Blindside.Save
{
    [Serializable]
    public class MapRecord
    {
        public string map = "";     // MapStore stem ("" = the default house)
        public int plays, wins;
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string createdUtc = "";      // ISO 8601 (round-trip "o")
        public string savedUtc = "";
        public double playSeconds;

        public bool tutorialDone;
        public int lastTeam;                // (int)OperatorTeam
        public string lastMap = "";         // MapStore stem of the last sortie ("" = the default house)

        public int matches, wins, losses;
        public List<MapRecord> maps = new List<MapRecord>();

        public OperatorTeam LastTeam
        {
            get => lastTeam == (int)OperatorTeam.Defense ? OperatorTeam.Defense : OperatorTeam.Attack;
            set => lastTeam = (int)value;
        }

        public static SaveData Fresh()
        {
            string now = DateTime.UtcNow.ToString("o");
            return new SaveData { createdUtc = now, savedUtc = now };
        }

        /// <summary>The record of `map` (made when it has none).</summary>
        public MapRecord RecordOf(string map)
        {
            map = map ?? "";
            if (maps == null) maps = new List<MapRecord>();
            foreach (var r in maps) if (r != null && string.Equals(r.map, map, StringComparison.Ordinal)) return r;
            var made = new MapRecord { map = map };
            maps.Add(made);
            return made;
        }

        /// <summary>The time of the last save in local time (DateTime.MinValue when unreadable).</summary>
        public DateTime SavedLocal
        {
            get
            {
                return DateTime.TryParse(savedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d.ToLocalTime() : DateTime.MinValue;
            }
        }

        /// <summary>Fills what an older or hand-edited file left out.</summary>
        public void Normalize()
        {
            if (maps == null) maps = new List<MapRecord>();
            maps.RemoveAll(r => r == null);
            foreach (var r in maps) if (r.map == null) r.map = "";
            if (lastMap == null) lastMap = "";
            if (createdUtc == null) createdUtc = "";
            if (savedUtc == null) savedUtc = "";
            if (lastTeam != (int)OperatorTeam.Defense) lastTeam = (int)OperatorTeam.Attack;
            if (playSeconds < 0 || double.IsNaN(playSeconds)) playSeconds = 0;
            version = CurrentVersion;
        }
    }
}
