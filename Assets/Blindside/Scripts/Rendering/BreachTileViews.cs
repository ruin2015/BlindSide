// The 'rubble' / 'hole' branches of cellMesh(x,y) in blindside_source/03_renderer_threejs.js (~92-105) — what
// refreshPanel(p) draws after breakPanel(p,st). MapWallSync hides the broken cells' wall blocks; this adds
// what the source puts in their place, driven by the same MapGrid.Changed cue:
//   rubble: 6 small boxes, mkStd(i%2?0x7a5231:0x5a3b22), s=.08+((x*7+y*3+i*5)%7)*.025, scale (s,s*.8,s*1.2),
//           position (((x*13+i*29)%9)/9-.45, s*.4, ((y*17+i*23)%9)/9-.45), rotation.y=i
//   hole:   a 1×.34×1 stub mkStd(0x4a3320) + a dark inset mkBasic(0x020304) .46×.02×.46 at y=h+.011
// Grid → world goes through GridSpace (z = -y), so the source's local z offsets and y-rotation are mirrored.
// Not dimmed out of sight (MapWallSync.ApplyVision only knows the scene's static blocks).
using System.Collections.Generic;
using UnityEngine;
using Blindside.Map;

namespace Blindside.Rendering
{
    public static class BreachTileViews
    {
        static MapGrid bound;
        static Transform root;
        static readonly Dictionary<int, (TileType type, GameObject go)> cells = new Dictionary<int, (TileType, GameObject)>();
        static readonly Dictionary<int, Material> mats = new Dictionary<int, Material>();

        public static void Bind(MapGrid map)
        {
            if (bound != null) bound.Changed -= Sync;
            bound = map;
            foreach (var kv in cells) if (kv.Value.go != null) Object.Destroy(kv.Value.go);
            cells.Clear();
            if (map == null) return;
            map.Changed += Sync;
            Sync();
        }

        static void Sync()
        {
            if (bound == null) return;
            if (root == null) root = new GameObject("Blindside_BreachTiles").transform;
            for (int y = 0; y < bound.Height; y++)
                for (int x = 0; x < MapGrid.Width; x++)
                {
                    var t = bound.TileAt(x, y);
                    int k = y * MapGrid.Width + x;
                    bool want = t == TileType.Rubble || t == TileType.Hole;
                    if (cells.TryGetValue(k, out var have))
                    {
                        if (want && have.type == t && have.go != null) continue;
                        if (have.go != null) Object.Destroy(have.go);
                        cells.Remove(k);
                    }
                    if (want) cells[k] = (t, t == TileType.Rubble ? BuildRubble(x, y) : BuildHole(x, y));
                }
        }

        static GameObject Group(int x, int y, string name)
        {
            var g = new GameObject($"{name}_{x}_{y}");
            g.transform.SetParent(root, false);
            g.transform.position = GridSpace.GridToWorld(x, y);
            return g;
        }

        static GameObject BuildRubble(int x, int y)
        {
            var g = Group(x, y, "Rubble");
            for (int i = 0; i < 6; i++)
            {
                float s = .08f + ((x * 7 + y * 3 + i * 5) % 7) * .025f;
                var d = Box(g.transform, i % 2 == 1 ? 0x7a5231 : 0x5a3b22, true);
                d.localScale = new Vector3(s, s * .8f, s * 1.2f);
                d.localPosition = new Vector3(((x * 13 + i * 29) % 9) / 9f - .45f, s * .4f, -(((y * 17 + i * 23) % 9) / 9f - .45f));
                d.localRotation = Quaternion.Euler(0f, -i * Mathf.Rad2Deg, 0f);
                d.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return g;
        }

        static GameObject BuildHole(int x, int y)
        {
            const float h = .34f;
            var g = Group(x, y, "Hole");
            var m = Box(g.transform, 0x4a3320, true);
            m.localScale = new Vector3(1f, h, 1f);
            m.localPosition = new Vector3(0f, h / 2f, 0f);
            m.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // castShadow=t!=='hole'
            var inn = Box(g.transform, 0x020304, false);
            inn.localScale = new Vector3(.46f, .02f, .46f);
            inn.localPosition = new Vector3(0f, h + .011f, 0f);
            return g;
        }

        static Transform Box(Transform parent, int rgb, bool lit)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());   // grid-only game: never a raycast/physics target
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(rgb, lit);
            return go.transform;
        }

        // mkStd → URP Lit (low smoothness: URP's .5 default puts a hot specular on flat boxes); mkBasic → Unlit.
        static Material Mat(int rgb, bool lit)
        {
            int key = rgb | (lit ? 1 << 24 : 0);
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            var shader = lit ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find(lit ? "Standard" : "Unlit/Color");
            m = new Material(shader);
            var c = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xff);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .08f);
            mats[key] = m;
            return m;
        }
    }
}
