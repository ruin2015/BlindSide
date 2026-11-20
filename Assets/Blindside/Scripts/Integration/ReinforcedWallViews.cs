// The reinforced-panel half of cellMesh() (blindside_source/03_renderer_threejs.js:101, 107):
//   else if(p.reinforced&&(G.side==='def'||p.seenByAtk)){ col=0xffffff; opts={map:GEO.steel,metalness:.35,roughness:.55}; }
//   if(p&&p.reinforced&&!p.mirror&&(G.side==='def'||p.seenByAtk)){ frame box 1.04×.06×1.04 (0x3a414a) on top,
//     yellow rail 1.03×.05×1.03 (0xffd21f, unlit) at the foot }
// plus updateDiscovery()'s `if(p.reinforced&&!p.seenByAtk){ p.seenByAtk=true; refreshPanel(p) }` — the attack side sees a
// reinforced wall as plain wood until one of its cells has been in an attacker's sight (sticky). The defense always sees it.
// The steel is a bolted reinforcement plate in the manner of Rainbow Six's reinforced walls (SteelTexture: upright ribs, a bolted
// brace, scuffs, a hazard band at the foot), drawn once in code.
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
            // the top's hazard band runs along the picture's u (world x): a wall running along the grid's y turns its cap a quarter
            if (RunsAlongY(c)) fr.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var rail = Box("Rail", go.transform, RailMat());
            // The wall blocks stand half below the floor (bounds ≈ y −.6….6): the rail sits .05 above the floor plane.
            rail.transform.position = new Vector3(b.center.x, Mathf.Max(b.min.y, 0f) + .05f, b.center.z);
            rail.transform.localScale = new Vector3(b.size.x * 1.03f, .05f, b.size.z * 1.03f);
            return go;
        }

        /// <summary>Whether the wall at `c` runs along the grid's y: its panel's cells share x, or (a one-cell panel) its neighbours
        /// above / below are wall and those left / right are not.</summary>
        bool RunsAlongY(Vector2Int c)
        {
            foreach (var p in map.Panels)
            {
                if (!p.cells.Contains(c)) continue;
                if (p.cells.Count > 1) return p.cells[0].x == p.cells[p.cells.Count - 1].x;
                break;
            }
            bool Wall(int x, int y) => x >= 0 && y >= 0 && x < MapGrid.Width && y < map.Height && map.TileAt(x, y) == TileType.Wall;
            bool vertical = Wall(c.x, c.y - 1) || Wall(c.x, c.y + 1), horizontal = Wall(c.x - 1, c.y) || Wall(c.x + 1, c.y);
            return vertical && !horizontal;
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

        // The steel is a bolted reinforcement plate (Rainbow Six's reinforced wall): SteelTexture on a white base, a little more metal.
        Material SteelMat()
        {
            if (steelMat != null) return steelMat;
            steelMat = Make("ReinforcedSteel", SteelTint, .5f, .42f, false);   // (the red cast: told apart at a glance)
            var tex = SteelTexture();
            if (steelMat.HasProperty("_BaseMap")) steelMat.SetTexture("_BaseMap", tex);
            if (steelMat.HasProperty("_MainTex")) steelMat.SetTexture("_MainTex", tex);
            return steelMat;
        }

        // ---- the plate's picture (built once) ----------------------------------------------------------------------------------
        // The wall block is a unit cube scaled 1 × 1.2 × 1 standing half below the floor, so a side face shows v .5 … 1 (0.6 m) of its
        // 0 … 1 picture. Drawn there, top to bottom: the plate's frame, a bolted brace across it, three upright ribs, scuffs and
        // grime, and a yellow / black hazard band along the foot. Below v .5 (under the floor) only the bare plate.
        public const int SteelTexSize = 256;
        static Texture2D steelTex;
        static readonly Color PlateA = new Color32(0x4b, 0x52, 0x5a, 0xff), PlateDark = new Color32(0x2c, 0x31, 0x37, 0xff);
        static readonly Color RibLight = new Color32(0x84, 0x8d, 0x97, 0xff), RibBody = new Color32(0x5c, 0x64, 0x6d, 0xff);
        static readonly Color BoltLight = new Color32(0xa6, 0xae, 0xb7, 0xff), Hazard = new Color32(0xff, 0xc8, 0x1e, 0xff), HazardInk = new Color32(0x14, 0x16, 0x1a, 0xff);

        public static Texture2D SteelTexture()
        {
            if (steelTex != null) return steelTex;
            const int N = SteelTexSize;
            var px = new Color[N * N];
            var rng = new System.Random(4127);
            // the bare plate: brushed upright (a slight per-column tone), darker toward the foot
            var col = new float[N];
            for (int x = 0; x < N; x++) col[x] = ((float)rng.NextDouble() - .5f) * .05f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = (y + .5f) / N, grime = v < .66f && v > .5f ? (1f - (v - .5f) / .16f) * .16f : 0f;
                    float n = col[x] + ((float)rng.NextDouble() - .5f) * .025f - grime;
                    px[y * N + x] = new Color(PlateA.r + n, PlateA.g + n, PlateA.b + n, 1f);
                }
            int V(float v) => Mathf.Clamp(Mathf.RoundToInt(v * N), 0, N - 1);
            void Rect(int x0, int y0, int x1, int y1, Color c) { for (int y = Mathf.Max(0, y0); y < Mathf.Min(N, y1); y++) for (int x = Mathf.Max(0, x0); x < Mathf.Min(N, x1); x++) px[y * N + x] = c; }
            void Bolt(int cx, int cy)
            {
                for (int y = -5; y <= 5; y++)
                    for (int x = -5; x <= 5; x++)
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        int i = (cy + y) * N + cx + x;
                        if (i < 0 || i >= px.Length || cx + x < 0 || cx + x >= N) continue;
                        if (d <= 3.2f) px[i] = Color.Lerp(BoltLight, PlateDark, Mathf.Clamp01((x - y) / 8f + .3f));   // lit from the top left
                        else if (d <= 4.6f) px[i] = PlateDark;
                    }
            }

            // three upright ribs: a lit left edge, the body, a shadow on the right
            foreach (float u in new[] { .25f, .5f, .75f })
            {
                int cx = Mathf.RoundToInt(u * N);
                Rect(cx - 7, V(.5f), cx - 5, N, RibLight);
                Rect(cx - 5, V(.5f), cx + 5, N, RibBody);
                Rect(cx + 5, V(.5f), cx + 8, N, PlateDark);
            }
            // the frame: dark edges left / right / top, a lit inner line
            Rect(0, 0, 8, N, PlateDark); Rect(N - 8, 0, N, N, PlateDark); Rect(0, N - 8, N, N, PlateDark);
            Rect(8, 0, 10, N - 8, RibLight); Rect(10, N - 10, N - 8, N - 8, RibLight);
            // the brace across, bolted
            int b0 = V(.75f), b1 = V(.81f);
            Rect(8, b0, N - 8, b1, PlateDark);
            Rect(8, b1, N - 8, b1 + 2, RibLight);
            for (int i = 0; i < 6; i++) Bolt(Mathf.RoundToInt((i + .5f) / 6f * N), (b0 + b1) / 2);
            // corner bolts at the top and over the hazard band
            foreach (float u in new[] { .07f, .93f }) { Bolt(Mathf.RoundToInt(u * N), V(.94f)); Bolt(Mathf.RoundToInt(u * N), V(.6f)); }
            // scuffs: short light strokes
            for (int s = 0; s < 46; s++)
            {
                int x = rng.Next(12, N - 12), y = rng.Next(V(.57f), N - 12), len = rng.Next(4, 16);
                float slope = ((float)rng.NextDouble() - .5f) * .8f, a = .1f + (float)rng.NextDouble() * .18f;
                for (int k = 0; k < len; k++)
                {
                    int xx = x + k, yy = y + Mathf.RoundToInt(k * slope);
                    if (xx >= N || yy < 0 || yy >= N) break;
                    px[yy * N + xx] = Color.Lerp(px[yy * N + xx], BoltLight, a);
                }
            }
            // the hazard band along the foot (v .5 … .565): 45° yellow / ink stripes, a dark lip above
            int h0 = V(.5f), h1 = V(.565f);
            for (int y = h0; y < h1; y++)
                for (int x = 0; x < N; x++)
                    px[y * N + x] = (((x + y) / 12) & 1) == 0 ? Hazard : HazardInk;
            Rect(0, h1, N, h1 + 3, PlateDark);

            steelTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "ReinforcedSteelTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            steelTex.SetPixels(px);
            steelTex.Apply(true, true);
            return steelTex;
        }
        // The cap over the wall's top (what the battle camera mostly sees): the same steel seen from above — TopTexture, a bolted plate
        // with an X brace — in the same red tint as the sides.
        Material FrameMat()
        {
            if (frameMat != null) return frameMat;
            frameMat = Make("ReinforcedTop", SteelTint, .45f, .4f, false);
            var tex = TopTexture();
            if (frameMat.HasProperty("_BaseMap")) frameMat.SetTexture("_BaseMap", tex);
            if (frameMat.HasProperty("_MainTex")) frameMat.SetTexture("_MainTex", tex);
            return frameMat;
        }

        /// <summary>The red cast over the steel (sides and top): a reinforced wall reads apart from the plain grey ones at a glance.</summary>
        public static readonly Color SteelTint = new Color(1f, .74f, .7f, 1f);

        // ---- the top's picture (built once): the plate from above — a dark frame with a lit inner line, an X brace corner to corner
        // (lit on one edge, shadowed on the other), bolts at the corners and where the brace crosses, light scuffs ----
        public const int TopTexSize = 128;
        static Texture2D topTex;

        public static Texture2D TopTexture()
        {
            if (topTex != null) return topTex;
            const int N = TopTexSize;
            var px = new Color[N * N];
            var rng = new System.Random(7741);
            for (int i = 0; i < px.Length; i++)
            {
                float n = ((float)rng.NextDouble() - .5f) * .04f;
                px[i] = new Color(PlateA.r + n, PlateA.g + n, PlateA.b + n, 1f);
            }
            void Put(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < N && y < N) px[y * N + x] = c; }
            // the X brace: two bars corner to corner, 9 wide, a lit edge and a shadowed one
            for (int k = 0; k < N; k++)
                for (int w = -5; w <= 5; w++)
                {
                    Color c = w == -5 ? RibLight : w == 5 ? PlateDark : RibBody;
                    Put(k + w, k, c);
                    Put(N - 1 - k + w, k, c);
                }
            // the frame
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(N - 1 - x, N - 1 - y));
                    if (e < 7) px[y * N + x] = PlateDark;
                    else if (e < 9) px[y * N + x] = RibLight;
                }
            // bolts: the corners and the crossing
            void Bolt(int cx, int cy)
            {
                for (int y = -5; y <= 5; y++)
                    for (int x = -5; x <= 5; x++)
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        if (d <= 3.2f) Put(cx + x, cy + y, Color.Lerp(BoltLight, PlateDark, Mathf.Clamp01((x - y) / 8f + .3f)));
                        else if (d <= 4.6f) Put(cx + x, cy + y, PlateDark);
                    }
            }
            int m = 16;
            Bolt(m, m); Bolt(N - 1 - m, m); Bolt(m, N - 1 - m); Bolt(N - 1 - m, N - 1 - m);
            // a hazard band through the middle, edge to edge (the cap is turned so it runs along the wall): 45° yellow / ink stripes
            // between dark lips, like the band at the sides' foot
            int h0 = N / 2 - 8, h1 = N / 2 + 8;
            for (int y = h0 - 2; y < h1 + 2; y++)
                for (int x = 0; x < N; x++)
                    px[y * N + x] = y < h0 || y >= h1 ? PlateDark : (((x + y) / 8) & 1) == 0 ? Hazard : HazardInk;
            // scuffs
            for (int s = 0; s < 22; s++)
            {
                int x = rng.Next(10, N - 10), y = rng.Next(10, N - 10), len = rng.Next(3, 10);
                float a = .1f + (float)rng.NextDouble() * .15f;
                for (int k = 0; k < len && x + k < N; k++) px[y * N + x + k] = Color.Lerp(px[y * N + x + k], BoltLight, a);
            }
            topTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "ReinforcedTopTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            topTex.SetPixels(px);
            topTex.Apply(true, true);
            return topTex;
        }
        Material RailMat() => railMat != null ? railMat : railMat = Make("ReinforcedRail", Rail, 0f, 0f, true);               // mkBasic
    }
}
