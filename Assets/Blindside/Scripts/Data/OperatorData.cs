// Ported from OPS in blindside_source/01_core_data_map_vision.js.
using UnityEngine;

namespace Blindside.Data
{
    public enum OperatorTeam { Attack, Defense }

    [CreateAssetMenu(fileName = "Operator_", menuName = "Blindside/Operator Data")]
    public class OperatorData : ScriptableObject
    {
        public string key;             // CRUSHER, FLARE, IGNIS, SERUM, BASTION, VOLT, MIASMA, WRAITH, OBSIDIAN, ARGUS
        public string kr;              // クラッシャー (the name shown beside the English codename)
        public string role;            // ブリーチャー
        public OperatorTeam team;
        public int hp;
        public int mv;
        public WeaponData weapon;
        [Tooltip("The guns this operator can take into the operation (picked before it starts). Empty: `weapon`.")]
        public WeaponData[] weapons;
        public string abilityKey;      // hammer, launcher, thermite, stim, shield, shock, gas, ghost, mirror, cam
        public string abilityName;     // ブリーチングハンマー
        public int charges;            // source uses 99 as BASTION's "unlimited" sentinel
        public Color color = Color.white;
        [TextArea] public string description;

        [Header("Shown as (optional — empty: the key)")]
        [Tooltip("The codename shown on screen in place of the key (e.g. TERRORIST). The key stays the operator's id in data and code.")]
        public string displayName;
        [Tooltip("Whose illustration / face this operator uses (Art/Operators/<artKey>.png …); the art library maps the key to it.")]
        public string artKey;
        /// <summary>The codename to show: displayName, else the key.</summary>
        public string DisplayName => string.IsNullOrEmpty(displayName) ? key : displayName;

        [Header("Model + animation (optional — empty uses the unit prefab's)")]
        [Tooltip("This operator's character model (prefab or FBX).")]
        public GameObject modelPrefab;
        [Tooltip("This operator's clips (Create > Blindside > Unit Animation Profile).")]
        public Blindside.Rendering.UnitAnimationProfile animationProfile;
    }
}
