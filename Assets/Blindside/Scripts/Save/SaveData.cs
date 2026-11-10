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
        /// <summary>Of the stats above, how many came from the HQ's training ground (bought with money; Hub.HubCatalog.TrainCost) —
        /// counted per stat in the same order, so the price rises with each session.</summary>
        public int[] trained = new int[5];
        /// <summary>The gadget the operator carries (Hub.HubCatalog.Gadgets key; "" = none). Phase 2 takes it into the match.</summary>
        public string gadget = "";
        /// <summary>v4 / v5: the parts in each slot, for whatever gun was carried. Read once by Normalize (the parts go back to the stock),
        /// then left empty — the parts are fitted per weapon now (fits).</summary>
        public string[] attachments = new string[0];
        public const int AttachmentSlots = 6;   // マズル / 光学機器 / グリップ / マガジン / バレル / ストック
        /// <summary>The parts fitted to each of this operator's guns (one copy of a part per fit: SaveData.attachmentStock).</summary>
        public List<WeaponFit> fits = new List<WeaponFit>();

        /// <summary>The fit of gun `weapon` (made empty when it has none).</summary>
        public WeaponFit FitOf(string weapon)
        {
            weapon = weapon ?? "";
            if (fits == null) fits = new List<WeaponFit>();
            foreach (var f in fits) if (f != null && f.weapon == weapon) return f;
            var made = new WeaponFit { weapon = weapon };
            fits.Add(made);
            return made;
        }

        /// <summary>The fit of gun `weapon`, or null when nothing was ever fitted to it (no entry is made).</summary>
        public WeaponFit FindFit(string weapon)
        {
            if (fits == null) return null;
            foreach (var f in fits) if (f != null && f.weapon == weapon) return f;
            return null;
        }

        public int StatPoints(int i)
        {
            switch (i) { case 0: return hp; case 1: return def; case 2: return atk; case 3: return mob; default: return prec; }
        }

        public void AddStat(int i, int n)
        {
            switch (i) { case 0: hp += n; break; case 1: def += n; break; case 2: atk += n; break; case 3: mob += n; break; default: prec += n; break; }
        }
    }

    /// <summary>The attachments on one operator's one gun: a part per slot (Data.Attachments.Slots order; "" = none).</summary>
    [Serializable]
    public class WeaponFit
    {
        public string weapon = "";   // WeaponData.key
        public string[] parts = { "", "", "", "", "", "" };
    }

    /// <summary>How many of a consumable item (a gadget) the squad holds.</summary>
    [Serializable]
    public class ItemCount
    {
        public string key = "";
        public int count;
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
        public const int CurrentVersion = 6;     // 2: rank points, money, operator / weapon progress; 3: the HQ (owned weapons, gadgets, training); 4: attachments; 5: six slots, 19 parts; 6: parts by count, fitted per operator per gun

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
        /// <summary>Weapons bought at the HQ's shop (WeaponData.key). Every operator's first gun is owned without being listed.</summary>
        public List<string> ownedWeapons = new List<string>();
        /// <summary>Operators who joined the squad beyond the first five (the rescue operation won: GHOST, SILENCE).</summary>
        public List<string> unlockedOperators = new List<string>();
        /// <summary>Gadgets in stock (bought at the shop / the 医務室).</summary>
        public List<ItemCount> gadgets = new List<ItemCount>();
        /// <summary>v4 / v5: attachments bought once for the squad. Read once by Normalize (each becomes one in attachmentStock), then empty.</summary>
        public List<string> ownedAttachments = new List<string>();
        /// <summary>Attachments bought at the shop, by count: each copy fits one gun of one operator (fitted copies count too —
        /// AttachmentsFree tells how many are on no gun).</summary>
        public List<ItemCount> attachmentStock = new List<ItemCount>();

        public const int MaxAttachmentStock = 9;

        /// <summary>The stock line of attachment `key` (made at 0 when it has none).</summary>
        public ItemCount AttachmentStockOf(string key)
        {
            key = key ?? "";
            if (attachmentStock == null) attachmentStock = new List<ItemCount>();
            foreach (var g in attachmentStock) if (g != null && g.key == key) return g;
            var made = new ItemCount { key = key };
            attachmentStock.Add(made);
            return made;
        }

        /// <summary>How many copies of `key` are fitted to some gun, over the whole squad.</summary>
        public int AttachmentsFitted(string key)
        {
            int n = 0;
            if (string.IsNullOrEmpty(key) || operators == null) return 0;
            foreach (var o in operators)
                if (o != null && o.fits != null)
                    foreach (var f in o.fits)
                        if (f != null && f.parts != null)
                            foreach (var p in f.parts) if (p == key) n++;
            return n;
        }

        /// <summary>Copies of `key` bought and on no gun yet.</summary>
        public int AttachmentsFree(string key) => AttachmentStockOf(key).count - AttachmentsFitted(key);

        public bool OwnsWeapon(string key) => !string.IsNullOrEmpty(key) && ownedWeapons != null && ownedWeapons.Contains(key);

        /// <summary>The medical items of older saves under today's keys (回復薬 / 鎮痛剤 → 下級医療キット, 救急キット → 中級,
        /// アドレナリン注射 → アドロピン注射器; 予備ドローン is gone: "").</summary>
        public static string RenamedGadget(string key)
        {
            switch (key)
            {
                case "potion": case "painkiller": return "kit_low";
                case "medkit": return "kit_mid";
                case "adrenaline": return "adrophin";
                case "drone": return "";
                default: return key ?? "";
            }
        }

        /// <summary>The stock line of gadget `key` (made at 0 when it has none).</summary>
        public ItemCount GadgetOf(string key)
        {
            key = key ?? "";
            if (gadgets == null) gadgets = new List<ItemCount>();
            foreach (var g in gadgets) if (g != null && g.key == key) return g;
            var made = new ItemCount { key = key };
            gadgets.Add(made);
            return made;
        }

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

        /// <summary>v4's first attachment set → the part that replaced each (v5: the 19 illustrated parts).</summary>
        static string RenamedPart(string key)
        {
            switch (key)
            {
                case "dotsight": return "holo";
                case "scope": return "scope4x";
                case "foregrip": return "vgrip";
                default: return key;
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
            foreach (var o in operators)
            {
                if (o.key == null) o.key = ""; if (o.level < 1) o.level = 1; if (o.exp < 0) o.exp = 0; if (o.points < 0) o.points = 0;
                if (o.trained == null || o.trained.Length != 5) { var t = new int[5]; if (o.trained != null) for (int i = 0; i < 5 && i < o.trained.Length; i++) t[i] = o.trained[i]; o.trained = t; }
                for (int i = 0; i < 5; i++) if (o.trained[i] < 0) o.trained[i] = 0;
                if (o.gadget == null) o.gadget = "";
                // fits: one per gun, six slots each
                int n = OperatorProgress.AttachmentSlots;
                if (o.fits == null) o.fits = new List<WeaponFit>();
                o.fits.RemoveAll(f => f == null);
                foreach (var f in o.fits)
                {
                    if (f.weapon == null) f.weapon = "";
                    if (f.parts == null || f.parts.Length != n)
                    {
                        var a = new string[n];
                        for (int i = 0; i < n; i++) a[i] = f.parts != null && i < f.parts.Length ? f.parts[i] ?? "" : "";
                        f.parts = a;
                    }
                    for (int i = 0; i < n; i++) f.parts[i] = f.parts[i] ?? "";
                }
            }
            // v4 / v5: parts bought once for the squad, fitted per operator (whatever gun) → one copy each in the stock, nothing fitted
            if (attachmentStock == null) attachmentStock = new List<ItemCount>();
            attachmentStock.RemoveAll(g => g == null || string.IsNullOrEmpty(g.key));
            foreach (var g in attachmentStock) if (g.count < 0) g.count = 0;
            if (ownedAttachments != null)
                foreach (var k in ownedAttachments)
                    if (!string.IsNullOrEmpty(k)) { var line = AttachmentStockOf(RenamedPart(k)); if (line.count < 1) line.count = 1; }
            ownedAttachments = new List<string>();
            foreach (var o in operators) o.attachments = new string[0];
            if (ownedWeapons == null) ownedWeapons = new List<string>();
            if (unlockedOperators == null) unlockedOperators = new List<string>();
            unlockedOperators.RemoveAll(string.IsNullOrEmpty);
            ownedWeapons.RemoveAll(string.IsNullOrEmpty);
            if (gadgets == null) gadgets = new List<ItemCount>();
            gadgets.RemoveAll(g => g == null || string.IsNullOrEmpty(g.key));
            foreach (var g in gadgets) if (g.count < 0) g.count = 0;
            // the medical items renamed (the 医務室's six): old stock merged into today's keys, carried ones follow
            for (int i = gadgets.Count - 1; i >= 0; i--)
            {
                var g = gadgets[i];
                string k = RenamedGadget(g.key);
                if (k == g.key) continue;
                gadgets.RemoveAt(i);
                if (k == "") { money += 6000 * g.count; continue; }   // 予備ドローン: no longer sold, refunded
                var line = GadgetOf(k);
                line.count = Math.Min(9, line.count + g.count);
            }
            foreach (var o in operators) o.gadget = RenamedGadget(o.gadget);
            if (weapons == null) weapons = new List<WeaponProgress>();
            weapons.RemoveAll(w => w == null);
            foreach (var w in weapons) { if (w.key == null) w.key = ""; if (w.level < 1) w.level = 1; if (w.exp < 0) w.exp = 0; }
            if (rankPoints < 0) rankPoints = 0;
            if (money < 0) money = 0;
            version = CurrentVersion;
        }
    }
}
