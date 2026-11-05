// =====================================================================
//  BLINDSIDE porting — HeliRig material assignment.
//  Menu: Blindside/Art/Assign Heli Rig Materials
//  Assigns URP materials to HeliRig.prefab's 21 mesh renderers, all of
//  which had an empty material slot (rendering magenta under URP).
//  Colors/roughness/metalness/opacity ported from makeHeli() in
//  blindside_source/03_renderer_threejs.js (hull/dark/cockpit/stripe/
//  rotor-disc/rope materials).
//  Editor-only: lives under an "Editor" folder so it never ships in builds.
// =====================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blindside.Cinematic.EditorTools
{
    public static class HeliRigMaterialBuilder
    {
        const string PrefabPath = "Assets/Blindside/Prefabs/Cinematic/HeliRig.prefab";
        const string MatDir = "Assets/Blindside/Art/Materials/Heli";

        [MenuItem("Blindside/Art/Assign Heli Rig Materials")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
                AssetDatabase.CreateFolder("Assets/Blindside/Art/Materials", "Heli");

            // hull = mkStd(0x2e3a33, roughness .6, metalness .3) — body, tail boom, tail fins.
            var hull = MakeMat("Mat_Heli_Hull", Hex(0x2e3a33), smoothness: 0.4f, metallic: 0.3f);
            // dark = mkStd(0x151a1f, roughness .5, metalness .4) — engine block, skids, rotor blades.
            var dark = MakeMat("Mat_Heli_Dark", Hex(0x151a1f), smoothness: 0.5f, metallic: 0.4f);
            // cockpit glass = mkStd(0x2b8fa8, roughness .1, metalness .6, transparent opacity .85).
            var cockpit = MakeMat("Mat_Heli_Cockpit", Hex(0x2b8fa8), smoothness: 0.9f, metallic: 0.6f, alpha: 0.85f);
            // belly stripe = mkBasic(0xffd21f) — unlit in the source; given a touch of emission so it
            // still reads as a flat bright yellow stripe under real scene lighting.
            var stripe = MakeMat("Mat_Heli_Stripe", Hex(0xffd21f), smoothness: 0.3f, metallic: 0f);
            stripe.EnableKeyword("_EMISSION");
            stripe.SetColor("_EmissionColor", (Color)Hex(0xffd21f) * 0.4f);
            stripe.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            // rotor blur disc = mkBasic(0x9aa4ae, transparent, opacity .08) — near-invisible spin blur.
            var rotorDisc = MakeMat("Mat_Heli_RotorDisc", Hex(0x9aa4ae), smoothness: 0.2f, metallic: 0f, alpha: 0.08f);
            // rope = mkStd(0xd8c9a0), roughness unspecified in source.
            var rope = MakeMat("Mat_Heli_Rope", Hex(0xd8c9a0), smoothness: 0.2f, metallic: 0f);

            var map = new Dictionary<string, Material>
            {
                { "Hull", hull },
                { "TailBoom", hull },
                { "TailFinV", hull },
                { "TailFinH", hull },
                { "Cockpit", cockpit },
                { "EngineBlock", dark },
                { "SkidStripe", stripe },
                { "Skid_0", dark }, { "Skid_1", dark },
                { "SkidStrut_0_a", dark }, { "SkidStrut_0_b", dark },
                { "SkidStrut_1_a", dark }, { "SkidStrut_1_b", dark },
                { "Blade", dark },           // BladeArm_0..3/Blade — TryGetValue keys on leaf name below
                { "Disc", rotorDisc },
                { "TailBlade_0", dark }, { "TailBlade_1", dark },
                { "RopeVisual", rope },
            };

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            int assigned = 0;
            var seen = new HashSet<string>();
            foreach (var r in renderers)
            {
                if (!map.TryGetValue(r.gameObject.name, out var mat)) continue;
                r.sharedMaterial = mat;
                seen.Add(r.gameObject.name);
                assigned++;
            }

            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();

            var expectedNames = new HashSet<string>(map.Keys);
            var rendererNames = new HashSet<string>();
            foreach (var r in renderers) rendererNames.Add(r.gameObject.name);
            expectedNames.ExceptWith(rendererNames);
            if (expectedNames.Count > 0)
                Debug.LogWarning("[HeliRigMaterialBuilder] No renderer found for: " + string.Join(", ", expectedNames));
            Debug.Log($"[HeliRigMaterialBuilder] Assigned materials to {assigned}/{renderers.Length} HeliRig renderers.");
        }

        static Color Hex(int rgb) => new Color32((byte)(rgb >> 16 & 0xff), (byte)(rgb >> 8 & 0xff), (byte)(rgb & 0xff), 0xff);

        static Material MakeMat(string name, Color color, float smoothness, float metallic, float alpha = 1f)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = name };
            color.a = alpha;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);

            if (alpha < 1f)
            {
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 0f);   // Alpha
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
