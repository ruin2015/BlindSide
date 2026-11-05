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

    /// <summary>An operator's growth: level / experience, unspent stat points and the points put into each stat (phase 2 applies them).</summary>
    [Serializable]
    public class OperatorProgress
    {
        public string key = "";
        public int level = 1, exp;
        public int points;                       // unspent
        public int hp, def, atk, mob, prec;      // 体力 / 防御力 / 攻撃力 / 機動力 / 精密力
    }

    /// <summary>A weapon's mastery (shared by everyone who uses it).</summary>
    [Serializable]
    public class WeaponProgress
    {
        public string key = "";
        public int level = 1, exp;
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 2;     // 2: rank points, money, operator / weapon progress

        public int version = CurrentVersion;
        public string createdUtc = "";      // ISO 8601 (round-trip "o")
        public string savedUtc = "";
        public double playSeconds;

        public bool tutorialDone;
        public int lastTeam;                // (int)OperatorTeam
        public string lastMap = "";         // MapStore stem of the last sortie ("" = the default house)

        public int matches, wins, losses;
        public List<MapRecord> maps = new List<MapRecord>();

        public int rankPoints;                   // 貢献度, all-time (RankTable)
        public int money;                        // yen
        public List<OperatorProgress> operators = new List<OperatorProgress>();
        public List<WeaponProgress> weapons = new List<WeaponProgress>();

        /// <summary>`key`'s growth (made at level 1 when it has none).</summary>
        public OperatorProgress OperatorOf(string key)
        {
            key = key ?? "";
            if (operators == null) operators = new List<OperatorProgress>();
            foreach (var o in operators) if (o != null && o.key == key) return o;
            var made = new OperatorProgress { key = key };
            operators.Add(made);
            return made;
        }

        /// <summary>`key`'s mastery (made at level 1 when it has none).</summary>
        public WeaponProgress WeaponOf(string key)
        {
            key = key ?? "";
            if (weapons == null) weapons = new List<WeaponProgress>();
            foreach (var w in weapons) if (w != null && w.key == key) return w;
            var made = new WeaponProgress { key = key };
            weapons.Add(made);
            return made;
        }

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
            if (operators == null) operators = new List<OperatorProgress>();
            operators.RemoveAll(o => o == null);
            foreach (var o in operators) { if (o.key == null) o.key = ""; if (o.level < 1) o.level = 1; if (o.exp < 0) o.exp = 0; if (o.points < 0) o.points = 0; }
            if (weapons == null) weapons = new List<WeaponProgress>();
            weapons.RemoveAll(w => w == null);
            foreach (var w in weapons) { if (w.key == null) w.key = ""; if (w.level < 1) w.level = 1; if (w.exp < 0) w.exp = 0; }
            if (rankPoints < 0) rankPoints = 0;
            if (money < 0) money = 0;
            version = CurrentVersion;
        }
    }
}
