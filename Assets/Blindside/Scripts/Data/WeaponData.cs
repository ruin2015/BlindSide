// Ported from WEAP in blindside_source/01_core_data_map_vision.js, extended with per-gun stats:
//   damage / shots (the magazine: rounds per volley) / accuracy / range, mobility (move range and initiative), and how damage
//   and accuracy fall off with distance — each gun its own curve. Defaults reproduce the source's weapons exactly
//   (accuracy −12 per tile past range, no damage falloff, mobility 0).
// Plus the 3D model a unit holds (scaled and placed in the right hand by UnitView).
using UnityEngine;

namespace Blindside.Data
{
    [CreateAssetMenu(fileName = "Weapon_", menuName = "Blindside/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string key;            // AR15, M870, … (the source's AR, SMG, SG, PST for the original four)
        public string weaponName;     // e.g. "K-417 突撃銃"
        [Tooltip("Class for the headshot table: AR, SMG, SG, PST, DMR. Empty: the key.")]
        public string weaponClass;

        [Header("Stats")]
        [Tooltip("Damage per round at close range.")]
        public int damage;
        [Tooltip("Magazine: rounds fired per volley.")]
        public int shots;
        [Tooltip("Base hit chance, percent.")]
        public int accuracy;
        [Tooltip("Effective range, tiles (m). Accuracy starts falling past it.")]
        public int range;
        [Tooltip("Mobility, metres: added to the move range; half of it to initiative (SPD, who shoots first on a mutual sighting).")]
        public float mobility;
        public bool breach;           // shotguns: +15 hit chance within 2.5 m (and the source's wall rule)

        [Header("Falloff with distance")]
        [Tooltip("Hit chance lost per tile past `range`.")]
        public float accuracyFalloffPerTile = 12f;
        [Tooltip("Damage starts falling past this many tiles (99: never).")]
        public float damageFalloffStart = 99f;
        [Tooltip("Damage lost per tile past damageFalloffStart, percent of the base damage.")]
        public float damageFalloffPerTile = 0f;
        [Tooltip("Damage never drops below this percent of the base.")]
        [Range(0f, 100f)] public float minDamagePercent = 100f;

        [Header("Model (held in the right hand)")]
        public GameObject model;
        [Tooltip("The model's barrel axis points along +X (1) or −X (−1).")]
        public float modelForwardSign = 1f;
        [Tooltip("Length of the gun for a 1.7 m person's arms, metres (scaled to each figure's arm length).")]
        public float modelLength = .8f;
        [Tooltip("Where the right hand grips it: 0 = the rear (stock) end, 1 = the muzzle.")]
        [Range(0f, 1f)] public float gripFromRear = .38f;
        [Tooltip("Grip height: 0 = the bottom of the model, 1 = the top.")]
        [Range(0f, 1f)] public float gripHeight = .35f;
        [Tooltip("Where the left hand holds it (the vertical grip / handguard): 0 = the rear end, 1 = the muzzle.")]
        [Range(0f, 1f)] public float foregripFromRear = .65f;
        [Tooltip("Left-hand height: 0 = the bottom of the model, 1 = the top.")]
        [Range(0f, 1f)] public float foregripHeight = .5f;
        [Tooltip("Turn the left hand about the barrel, degrees: 0 cups the handguard from below, ~80 wraps a vertical grip.")]
        [Range(-180f, 180f)] public float foregripRoll = 0f;
        [Tooltip("Height of the top of the stock (where the cheek rests), 0 = bottom, 1 = top: aiming lowers the head onto it.")]
        [Range(0f, 1f)] public float sightHeight = .82f;
        [Tooltip("Height of the butt's middle, 0 = bottom, 1 = top: aiming puts it in the right shoulder pocket.")]
        [Range(0f, 1f)] public float buttHeight = .55f;

        public string ClassKey => string.IsNullOrEmpty(weaponClass) ? key : weaponClass;

        /// <summary>Damage of one round at `distance` tiles: the base, minus the falloff past damageFalloffStart, floored at minDamagePercent.</summary>
        public float DamageAt(float baseDamage, float distance)
        {
            if (distance <= damageFalloffStart || damageFalloffPerTile <= 0f) return baseDamage;
            float pct = Mathf.Max(minDamagePercent, 100f - (distance - damageFalloffStart) * damageFalloffPerTile);
            return baseDamage * pct / 100f;
        }

        /// <summary>moveRange: `mv*3+1` metres plus the gun's mobility.</summary>
        public static float MoveRangeOf(int mv, WeaponData w) => mv * 3f + 1f + (w != null ? w.mobility : 0f);

        /// <summary>The gun's share of initiative (SPD): half its mobility.</summary>
        public static float SpeedBonusOf(WeaponData w) => w != null ? w.mobility * .5f : 0f;
    }
}
