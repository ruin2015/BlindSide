// (Blindside: keeps a player build complete.
//  1) Resources/BlindsideAssetRegistry.asset — the operators, weapons, unit figure, battle camera profile and HUD prefabs the game
//     loads by name (BuildAssetRegistry).
//  2) Resources/ShaderKeep/*.mat — one material per URP shader / surface the code creates at runtime with Shader.Find. A build only
//     carries shaders (and their keyword variants) some included material uses; without these Shader.Find returned null in the player
//     and every runtime material threw ArgumentNullException.
//  Run: Blindside > Build > Refresh Asset Registry; also on editor load when missing, and before every player build.)
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Blindside.Cinematic;

namespace Blindside.Data.EditorTools
{
    [InitializeOnLoad]
    public static class BuildAssetRegistryBuilder
    {
        const string ResourcesDir = "Assets/Blindside/Resources";
        const string RegistryPath = ResourcesDir + "/" + BuildAssetRegistry.ResourceName + ".asset";
        const string ShaderKeepDir = ResourcesDir + "/ShaderKeep";

        static BuildAssetRegistryBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<BuildAssetRegistry>(RegistryPath) == null || !AssetDatabase.IsValidFolder(ShaderKeepDir))
                    Refresh();
            };
        }

        [MenuItem("Blindside/Build/Refresh Asset Registry")]
        public static void Refresh()
        {
            var reg = AssetDatabase.LoadAssetAtPath<BuildAssetRegistry>(RegistryPath);
            if (reg == null)
            {
                reg = ScriptableObject.CreateInstance<BuildAssetRegistry>();
                AssetDatabase.CreateAsset(reg, RegistryPath);
            }
            reg.operators = LoadAll<OperatorData>("Assets/Blindside/Data/Operators");
            reg.weapons = LoadAll<WeaponData>("Assets/Blindside/Data/Weapons");
            reg.unitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blindside/Prefabs/Units/Unit_Simple.prefab");
            reg.battleCamProfile = AssetDatabase.LoadAssetAtPath<BattleCamProfile>("Assets/Blindside/Data/Cinematic/BattleCamProfile.asset");
            reg.uiPrefabs = LoadAll<GameObject>("Assets/Blindside/Prefabs/UI");
            EditorUtility.SetDirty(reg);
            EnsureShaderKeepers();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Blindside] asset registry: {reg.operators.Length} operators, {reg.weapons.Length} weapons, " +
                      $"{reg.uiPrefabs.Length} UI prefabs, unit {(reg.unitPrefab != null ? "ok" : "MISSING")}, " +
                      $"cam profile {(reg.battleCamProfile != null ? "ok" : "MISSING")}");
        }

        static T[] LoadAll<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new T[0];
            string filter = typeof(T) == typeof(GameObject) ? "t:Prefab" : "t:" + typeof(T).Name;
            return AssetDatabase.FindAssets(filter, new[] { folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).OrderBy(a => a.name).ToArray();
        }

        // ---- shader keepers -------------------------------------------------------------------------------------------------------

        static void EnsureShaderKeepers()
        {
            if (!AssetDatabase.IsValidFolder(ShaderKeepDir)) AssetDatabase.CreateFolder(ResourcesDir, "ShaderKeep");
            Keep("Unlit_Opaque", "Universal Render Pipeline/Unlit", false, false);
            Keep("Unlit_Transparent", "Universal Render Pipeline/Unlit", true, false);
            Keep("Lit_Opaque", "Universal Render Pipeline/Lit", false, false);
            Keep("Lit_Transparent", "Universal Render Pipeline/Lit", true, false);
            Keep("Lit_Emission", "Universal Render Pipeline/Lit", false, true);
            Keep("Lit_TransparentEmission", "Universal Render Pipeline/Lit", true, true);
            Keep("Builtin_SpritesDefault", "Sprites/Default", false, false);
        }

        static void Keep(string file, string shaderName, bool transparent, bool emission)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogWarning($"[Blindside] shader not found: {shaderName}"); return; }
            string path = $"{ShaderKeepDir}/{file}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            if (transparent && m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            if (emission && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
            }
            EditorUtility.SetDirty(m);
        }

        // ---- before every build -----------------------------------------------------------------------------------------------------

        class Preprocess : IPreprocessBuildWithReport
        {
            public int callbackOrder => -100;
            public void OnPreprocessBuild(BuildReport report) => Refresh();
        }
    }
}
