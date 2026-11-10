// The reinforced-panel half of cellMesh() (blindside_source/03_renderer_threejs.js:101, 107):
//   else if(p.reinforced&&(G.side==='def'||p.seenByAtk)){ col=0xffffff; opts={map:GEO.steel,metalness:.35,roughness:.55}; }
//   if(p&&p.reinforced&&!p.mirror&&(G.side==='def'||p.seenByAtk)){ frame box 1.04×.06×1.04 (0x3a414a) on top,
//     yellow rail 1.03×.05×1.03 (0xffd21f, unlit) at the foot }
// plus updateDiscovery()'s `if(p.reinforced&&!p.seenByAtk){ p.seenByAtk=true; refreshPanel(p) }` — the attack side sees a
// reinforced wall as plain wood until one of its cells has been in an attacker's sight (sticky). The defense always sees it.
// The steel is the averaged makeShutterTex() colour (#5d6873 -> #8e99a5), same as the garage door's material.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.Vision;

namespace Blindside.Integration
{
    public class ReinforcedWallViews : MonoBehaviour
    {
        public static readonly Color Steel = new Color32(0x76, 0x81, 0x8d, 0xff);   // (the map editor draws reinforced walls the same way)
        public static readonly Color Frame = new Color32(0x3a, 0x41, 0x4a, 0xff);
        public static readonly Color Rail = new Color32(0xff, 0xd2, 0x1f, 0xff);
        const float SeenCheckInterval = .2f;

        MapGrid map;
        VisionSystem vision;
        IReadOnlyList<SimUnit> units;
        OperatorTeam side;
        Transform root;
        Material steelMat, frameMat, railMat;
        float nextSeenCheck;

        // The cells currently drawn as steel with their trim.
        readonly Dictionary<Vector2Int, GameObject> trims = new Dictionary<Vector2Int, GameObject>();

        public void Bind(MapGrid map, VisionSystem vision, IReadOnlyList<SimUnit> units, OperatorTeam? playerTeam)
        {
            Unbind();
            this.map = map; this.vision = vision; this.units = units;
            side = playerTeam ?? OperatorTeam.Attack;
            map.Changed += Sync;
            map.PanelDiscovered += Sync;
            Sync();
        }

        public void Unbind()
        {
            if (map != null) { map.Changed -= Sync; map.PanelDiscovered -= Sync; }
            foreach (var kv in trims) MapWallSync.SetMainMaterial(kv.Key, null);
            trims.Clear();
            if (root != null) Destroy(root.gameObject);
            root = null; map = null; vision = null; units = null;
        }

        void OnDestroy() { if (map != null) { map.Changed -= Sync; map.PanelDiscovered -= Sync; } }

        // updateDiscovery(): runs whichever side the human plays — the attack AI reads the same p.seenByAtk (EnemyAI.AiBreach).
        void LateUpdate()
        {
            if (map == null || Time.unscaledTime < nextSeenCheck) return;
            nextSeenCheck = Time.unscaledTime + SeenCheckInterval;
            WallDiscovery.Refresh(map, vision, units);   // a discovery raises PanelDiscovered → Sync
        }

        bool ShowsAsSteel(WallPanel p) => p.reinforced && !p.mirror && (side == OperatorTeam.Defense || p.seenByAtk);

        // refreshPanel(p) for every panel: cells whose look changed get / lose the steel main box and its trim.
        void Sync()
        {
            if (map == null) return;
            var want = new HashSet<Vector2Int>();
            foreach (var p in map.Panels)
            {
                if (!ShowsAsSteel(p) || !p.cells.TrueForAll(c => map.TileAt(c.x, c.y) == TileType.Wall)) continue;
                foreach (var c in p.cells) want.Add(c);
            }
            List<Vector2Int> gone = null;
            foreach (var kv in trims) if (!want.Contains(kv.Key)) (gone ??= new List<Vector2Int>()).Add(kv.Key);
            if (gone != null)
                foreach (var c in gone)
                {
                    if (trims[c] != null) Destroy(trims[c]);
                    trims.Remove(c);
                    MapWallSync.SetMainMaterial(c, null);
                }
            foreach (var c in want)
            {
                if (trims.ContainsKey(c)) continue;
                MapWallSync.SetMainMaterial(c, SteelMat());
                trims[c] = BuildTrim(c);
            }
        }

        GameObject BuildTrim(Vector2Int c)
        {
            if (root == null) { root = new GameObject("ReinforcedWalls").transform; root.SetParent(transform, false); }
            if (!MapWallSync.TryGetBlockBounds(c, out var b)) b = new Bounds(GridSpace.GridToWorld(c.x, c.y, .5f), Vector3.one);
            var go = new GameObject($"Reinforced_{c.x}_{c.y}");
            go.transform.SetParent(root, false);
            var fr = Box("Frame", go.transform, FrameMat());
            fr.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
            fr.transform.localScale = new Vector3(b.size.x * 1.04f, .06f, b.size.z * 1.04f);
            var rail = Box("Rail", go.transform, RailMat());
            // The wall blocks stand half below the floor (bounds ≈ y −.6….6): the rail sits .05 above the floor plane.
            rail.transform.position = new Vector3(b.center.x, Mathf.Max(b.min.y, 0f) + .05f, b.center.z);
            rail.transform.localScale = new Vector3(b.size.x * 1.03f, .05f, b.size.z * 1.03f);
            return go;
        }

        static GameObject Box(string name, Transform parent, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Shader LitShader => Blindside.Rendering.ToonShader.Lit ?? Shader.Find("Standard");

        public static Material Make(string name, Color col, float metallic, float smoothness, bool unlit)
        {
            var shader = unlit ? (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")) : LitShader;
            var m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (!unlit)
            {
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            }
            return m;
        }

        Material SteelMat() => steelMat != null ? steelMat : steelMat = Make("ReinforcedSteel", Steel, .35f, .45f, false);   // metalness .35, roughness .55
        Material FrameMat() => frameMat != null ? frameMat : frameMat = Make("ReinforcedFrame", Frame, .3f, .4f, false);      // metalness .3, roughness .6
        Material RailMat() => railMat != null ? railMat : railMat = Make("ReinforcedRail", Rail, 0f, 0f, true);               // mkBasic
    }
}
