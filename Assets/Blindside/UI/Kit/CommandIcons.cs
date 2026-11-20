// The command buttons' icons (the user's sheet, cut into Resources/UI/CommandIcons/Cmd_NN_*.png: white on transparent — tint them with
// the Image colour). KeyFor picks one from the command's id (and, for the ability, the operator's ability key; for the gadgets, the
// line's English word); For loads it once and caches it. Nothing per frame.
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.UI
{
    public static class CommandIcons
    {
        const string Folder = "UI/CommandIcons/";

        static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            { "Move", "Cmd_01_Move" }, { "View", "Cmd_02_View" }, { "Fire", "Cmd_03_Fire" }, { "Hammer", "Cmd_04_Hammer" },
            { "Medic", "Cmd_05_Medic" }, { "Bomb", "Cmd_06_Bomb" }, { "Blast", "Cmd_07_Blast" }, { "Shield", "Cmd_08_Shield" },
            { "Shock", "Cmd_09_Shock" }, { "Heartbeat", "Cmd_10_Heartbeat" }, { "Pouch", "Cmd_11_Pouch" }, { "Frag", "Cmd_12_Frag" },
            { "Flash", "Cmd_13_Flash" }, { "Drone", "Cmd_14_Drone" }, { "Camera", "Cmd_15_Camera" }, { "Charge", "Cmd_16_Charge" },
            { "Smoke", "Cmd_17_Smoke" }, { "Wait", "Cmd_18_Wait" },
        };
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>The icon for command `id` (`abilityKey`: the operator's ability, for "skill"; `en`: the line's English word, for
        /// the gadgets and items). Null: none (戻る).</summary>
        public static string KeyFor(string id, string abilityKey, string en)
        {
            switch (id)
            {
                case "move": return "Move";
                case "face": return "View";
                case "fire": case "swap": return "Fire";
                case "skill": return AbilityIcon(abilityKey);
                case "gadgets": return "Pouch";
                case "gadget": return en == "CAMERA" ? "Camera" : "Drone";
                case "reinforce": return "Shield";
                case "escort": case "rescue": return "Medic";
                case "wait": return "Wait";
                case "back": return null;
            }
            if (id != null && id.StartsWith("item:"))
            {
                switch (en)
                {
                    case "FRAG": return "Frag";
                    case "SMOKE": return "Smoke";
                    case "FLASH": return "Flash";
                    case "BREACH": return "Charge";
                    case "CLAYMORE": return "Bomb";
                    default: return "Medic";
                }
            }
            return null;
        }

        /// <summary>The ability's icon: hammer, launcher (blast), thermite (charge), stim (medic), shield, shock, gas (smoke),
        /// ghost (heartbeat), mirror (shield), cam (camera), drone.</summary>
        public static string AbilityIcon(string key)
        {
            switch (key)
            {
                case "hammer": return "Hammer";
                case "launcher": return "Blast";
                case "thermite": return "Charge";
                case "stim": return "Medic";
                case "shield": case "mirror": return "Shield";
                case "shock": return "Shock";
                case "gas": return "Smoke";
                case "ghost": return "Heartbeat";
                case "cam": return "Camera";
                case "drone": return "Drone";
                default: return "Blast";
            }
        }

        /// <summary>The sprite for icon `key` (null: none).</summary>
        public static Sprite For(string key)
        {
            if (string.IsNullOrEmpty(key) || !Files.TryGetValue(key, out var file)) return null;
            if (cache.TryGetValue(file, out var s)) return s;
            s = Resources.Load<Sprite>(Folder + file);
            cache[file] = s;
            return s;
        }
    }
}
