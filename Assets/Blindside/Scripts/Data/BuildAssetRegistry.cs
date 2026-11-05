// (Blindside: what the game loads by name at runtime, in one asset under Resources so a player build has it too.
//  The editor used to read these straight from their project paths (AssetDatabase) and a build fell back to Resources.Load, where they
//  never were — so a build fielded operators without data and a unit without its figure. BuildAssetRegistryBuilder fills it
//  (Blindside > Build > Refresh Asset Registry, on load when missing, and before every build).)
using UnityEngine;
using Blindside.Cinematic;

namespace Blindside.Data
{
    [CreateAssetMenu(menuName = "Blindside/Build Asset Registry", fileName = "BlindsideAssetRegistry")]
    public class BuildAssetRegistry : ScriptableObject
    {
        public const string ResourceName = "BlindsideAssetRegistry";

        [Tooltip("Data/Operators/Operator_*.asset — found by OperatorData.key.")]
        public OperatorData[] operators = new OperatorData[0];
        [Tooltip("Data/Weapons/*.asset — found by asset name (Weapon_AR …).")]
        public WeaponData[] weapons = new WeaponData[0];
        [Tooltip("Prefabs/Units/Unit_Simple.prefab — the unit figure a match spawns when MatchBootstrap.unitPrefab is empty.")]
        public GameObject unitPrefab;
        [Tooltip("Data/Cinematic/BattleCamProfile.asset — the battle cinematic's tuning when MatchBootstrap.battleCamProfile is empty.")]
        public BattleCamProfile battleCamProfile;
        [Tooltip("Prefabs/UI/*.prefab the HUD instantiates by name (HUD_OperatorArtCard, HUD_UnitPanel, HUD_CmdMenu …).")]
        public GameObject[] uiPrefabs = new GameObject[0];

        static BuildAssetRegistry cached;
        static bool tried;

        /// <summary>The registry (Resources/BlindsideAssetRegistry), or null when it hasn't been built yet.</summary>
        public static BuildAssetRegistry Instance
        {
            get
            {
                if (cached == null && !tried) { cached = Resources.Load<BuildAssetRegistry>(ResourceName); tried = true; }
                return cached;
            }
        }

        public static OperatorData Operator(string key)
        {
            var r = Instance;
            if (r == null || r.operators == null) return null;
            foreach (var o in r.operators) if (o != null && (o.key == key || o.name == "Operator_" + key)) return o;
            return null;
        }

        public static WeaponData Weapon(string assetName)
        {
            var r = Instance;
            if (r == null || r.weapons == null) return null;
            foreach (var w in r.weapons) if (w != null && w.name == assetName) return w;
            return null;
        }

        public static GameObject UiPrefab(string name)
        {
            var r = Instance;
            if (r == null || r.uiPrefabs == null) return null;
            foreach (var p in r.uiPrefabs) if (p != null && p.name == name) return p;
            return null;
        }

        public static GameObject UnitPrefab => Instance != null ? Instance.unitPrefab : null;
        public static BattleCamProfile CamProfile => Instance != null ? Instance.battleCamProfile : null;
    }
}
