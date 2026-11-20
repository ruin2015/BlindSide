// The guns' silhouettes for the battle's operator cards (Resources/UI/Guns/Gun_NN_*.png, white on transparent: tint them with the
// Image colour) and the short name the cards print (the weapon's name up to its first space: "AR15", "K-417", "M-870" …).
// One sprite per family; a key with no picture (the pistol) gets none. Loaded once, cached; nothing per frame.
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.UI
{
    public static class GunIcons
    {
        const string Folder = "UI/Guns/";

        // weapon key → picture
        static readonly Dictionary<string, string> Files = new Dictionary<string, string>
        {
            { "M870", "Gun_01_Pump" }, { "SG", "Gun_01_Pump" },
            { "P90", "Gun_02_P90" },
            { "DB", "Gun_03_LongShotgun" },
            { "Vector", "Gun_04_Vector" }, { "SMG", "Gun_09_VectorStock" },
            { "MP7", "Gun_05_MP7" },
            { "MP5", "Gun_06_MP5" },
            { "MX50", "Gun_07_Sniper" },
            { "M16", "Gun_08_M16" },
            { "AK47", "Gun_10_AK47" },
            { "AR15", "Gun_11_M4" }, { "AR", "Gun_11_M4" }, { "SCAR", "Gun_11_M4" },
            { "SAIGA", "Gun_12_Saiga" },
        };
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>The silhouette for weapon `key` (null: none).</summary>
        public static Sprite For(string key)
        {
            if (string.IsNullOrEmpty(key) || !Files.TryGetValue(key, out var file)) return null;
            if (cache.TryGetValue(file, out var s)) return s;
            s = Resources.Load<Sprite>(Folder + file);
            cache[file] = s;
            return s;
        }

        /// <summary>The card's weapon line: the name up to its first space ("AR15 突撃銃" → "AR15"), else the key.</summary>
        public static string Label(string weaponName, string key)
        {
            if (string.IsNullOrEmpty(weaponName)) return key ?? "";
            int sp = weaponName.IndexOf(' ');
            return sp > 0 ? weaponName.Substring(0, sp) : weaponName;
        }
    }
}
