// =====================================================================
//  BLINDSIDE porting — House_Exterior material assignment.
//  Menu: Blindside/Art/Assign House Exterior Materials
//  Assigns URP materials to House_Exterior.prefab's 16 mesh renderers,
//  which all had an empty material slot (rendering magenta under URP).
//  Colors are ported from makeHouse()/makeWoodTex()/makeShutterTex() in
//  blindside_source/03_renderer_threejs.js: solid-color parts (siding,
//  slate roof, trim, chimney, step) use that function's literal hex
//  values; the two procedural canvas textures (wood siding, garage-door
//  shutter) are approximated as their average color since this port has
//  no canvas-texture generator — flat color, not the plank/ridge detail.
//  MainRoof/LeftWingRoof use makeShingleTex(), which wasn't read for this
//  pass, so that one is a plain dark asphalt-gray guess, not a source value.
//  Editor-only: lives under an "Editor" folder so it never ships in builds.
// =====================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blindside.Cinematic.EditorTools
{
    public static class HouseExteriorMaterialBuilder
    {
        const string PrefabPath = "Assets/Blindside/Prefabs/Cinematic/House_Exterior.prefab";
        const string MatDir = "Assets/Blindside/Art/Materials/House";

        [MenuItem("Blindside/Art/Assign House Exterior Materials")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
                AssetDatabase.CreateFolder("Assets/Blindside/Art/Materials", "House");

            // siding: sidingM = mkStd(0xd9d3c4, roughness .9) — MainBlock's upper walls + main gable.
            var siding = MakeMat("Mat_House_Siding", Hex(0xd9d3c4), smoothness: 0.1f);
            // roof (shingle): roofM = mkStd(0xffffff, map: makeShingleTex(), roughness .85). That
            // texture generator wasn't part of this read — approximated as a dark asphalt gray.
            var roofShingle = MakeMat("Mat_House_RoofShingle", Hex(0x2f2c28), smoothness: 0.15f);
            // roof (flat): roofM2 = mkStd(0x55606b, roughness .7) — garage roof + porch roof + eave strips.
            var roofSlate = MakeMat("Mat_House_RoofSlate", Hex(0x55606b), smoothness: 0.3f);
            // wood siding: GEO.wood = makeWoodTex() — 4 plank cols averaged (#8a5f37/#7d5431/#94683d/#80572f).
            var wood = MakeMat("Mat_House_Wood", new Color32(0x87, 0x5d, 0x35, 0xff), smoothness: 0.1f);
            // garage door: GEO.steel = makeShutterTex() (#5d6873 -> #8e99a5 gradient, averaged), roughness .6.
            var garageDoor = MakeMat("Mat_House_GarageDoor", new Color32(0x76, 0x81, 0x8d, 0xff), smoothness: 0.4f);
            // trim: trimM = mkStd(0xf1ede4) — porch posts, garage-door frame, window frames.
            var trim = MakeMat("Mat_House_Trim", Hex(0xf1ede4), smoothness: 0.25f);
            // chimney: mkStd(0x7b4a3a).
            var chimney = MakeMat("Mat_House_Chimney", Hex(0x7b4a3a), smoothness: 0.1f);
            // step: mkStd(0x8d8a84).
            var step = MakeMat("Mat_House_Step", Hex(0x8d8a84), smoothness: 0.15f);
            // window pane: wm = mkBasic(0xffc46e) — unlit warm glow in the source; ported as an emissive
            // URP/Lit so it reads as a lit window at dusk under real scene lighting.
            var window = MakeMat("Mat_House_Window", Hex(0x2a2016), smoothness: 0.6f);
            window.EnableKeyword("_EMISSION");
            window.SetColor("_EmissionColor", (Color)new Color32(0xff, 0xc4, 0x6e, 0xff) * 1.4f);
            window.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            var map = new Dictionary<string, Material>
            {
                { "MainBlock", siding },
                { "MainRoof", roofShingle },
                { "LeftWing", wood },
                { "LeftWingRoof", roofShingle },
                { "GarageWing", wood },
                { "GarageRoof", roofSlate },
                { "GarageDoor", garageDoor },
                { "Chimney", chimney },
                { "PorchRoof", roofSlate },
                { "PorchPost_L", trim },
                { "PorchPost_R", trim },
                { "Step", step },
                { "Window_1", window },
                { "Window_2", window },
                { "Window_Upper_L", window },
                { "Window_Upper_R", window },
            };

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            int assigned = 0;
            var missing = new List<string>(map.Keys);
            foreach (var r in renderers)
            {
                if (!map.TryGetValue(r.gameObject.name, out var mat)) continue;
                r.sharedMaterial = mat;
                missing.Remove(r.gameObject.name);
                assigned++;
            }

            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();

            if (missing.Count > 0)
                Debug.LogWarning("[HouseExteriorMaterialBuilder] No renderer found for: " + string.Join(", ", missing));
            Debug.Log($"[HouseExteriorMaterialBuilder] Assigned materials to {assigned}/{map.Count} House_Exterior renderers.");
        }

        static Color Hex(int rgb) => new Color32((byte)(rgb >> 16 & 0xff), (byte)(rgb >> 8 & 0xff), (byte)(rgb & 0xff), 0xff);

        static Material MakeMat(string name, Color color, float smoothness)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
