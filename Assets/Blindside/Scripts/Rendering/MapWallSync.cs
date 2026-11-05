// The drawFloor()/refreshPanel(p) half of breakPanel(p,st) (blindside_source/04_gameplay_combat_ai.js:214):
// the battlefield's walls are static blocks under Blindside_MapRoot/Walls named Wall_Wood_{x}_{y}, one per
// wall cell, so a cell that MapGrid no longer reports as 'wall' (rubble/hole after BreakPanel) has its
// block hidden. MapGrid.Changed is the cue (the source's bumpMap()); binding a new match's map shows every
// block again first, since each match starts from a fresh MapGrid.
//
// Also the wall half of syncVision() (03_renderer_threejs.js:167):
//   for(const [k,g] of R.cells){ const m=g.userData.main; if(!m) continue; const v=title||tv.all.has(k);
//     m.material.color.copy(g.userData.base).multiplyScalar(v?1:.36); }
// Crates (Crate_{x}_{y}, baked for the default map) follow the map the same way: hidden where the bound map has none, cloned where it has one.
// userData.main is each wall / hard / crate cell's main box (the crate's yellow band is a separate mesh and
// keeps its color) — here Wall_Wood_{x}_{y}, Hard_Block_{x}_{y} and Crate_{x}_{y}/Crate_Body.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Map;

namespace Blindside.Rendering
{
    public static class MapWallSync
    {
        const string WallsPath = "Blindside_MapRoot/Walls";
        const string WallPrefix = "Wall_Wood_";
        const string HardPrefix = "Hard_Block_";
        const string CratePrefix = "Crate_";
        const float OutOfSight = .36f;

        static MapGrid bound;
        static Transform wallsRoot;
        static readonly Dictionary<Vector2Int, GameObject> blocks = new Dictionary<Vector2Int, GameObject>();
        static readonly Dictionary<Vector2Int, GameObject> hardBlocks = new Dictionary<Vector2Int, GameObject>();   // Solid walls
        static readonly Dictionary<Vector2Int, GameObject> crateBlocks = new Dictionary<Vector2Int, GameObject>();  // crates (shown where the map has one)
        // Blocks cloned at runtime for cells of a map the scene has no block for (the scene bakes the default map only);
        // they stay hidden unless the bound map has a wall of that kind there.
        static readonly HashSet<GameObject> runtimeBlocks = new HashSet<GameObject>();
        static readonly List<(Vector2Int cell, Renderer main, Color baseColor)> mains = new List<(Vector2Int, Renderer, Color)>();
        static MaterialPropertyBlock mpb;

        // Occluder fade (BattleDirector): renderers of the blocks between the battle camera and the two
        // fighters are swapped to a transparent twin of their material and drawn at `fadeAlpha`.
        static readonly Dictionary<Renderer, Material> fadedOriginals = new Dictionary<Renderer, Material>();
        static readonly Dictionary<Material, Material> transparentTwins = new Dictionary<Material, Material>();
        static float fadeAlpha = 1f;
        static HashSet<int> lastVisible;

        // Main-box materials swapped by SetMainMaterial (mirror panels), original kept for the next match.
        static readonly Dictionary<Renderer, Material> swappedOriginals = new Dictionary<Renderer, Material>();

        public static void Bind(MapGrid map)
        {
            if (bound != null) bound.Changed -= Sync;
            bound = map;
            foreach (var kv in swappedOriginals) if (kv.Key != null) kv.Key.sharedMaterial = kv.Value;
            swappedOriginals.Clear();
            Collect();
            foreach (var go in blocks.Values) if (go != null) go.SetActive(!runtimeBlocks.Contains(go));
            foreach (var go in hardBlocks.Values) if (go != null) go.SetActive(!runtimeBlocks.Contains(go));
            foreach (var go in crateBlocks.Values) if (go != null) go.SetActive(!runtimeBlocks.Contains(go));
            ApplyVision(null);
            if (map == null) return;
            map.Changed += Sync;
            Sync();
        }

        static void Collect()
        {
            ClearOccluderFade();
            blocks.Clear();
            hardBlocks.Clear();
            crateBlocks.Clear();
            mains.Clear();
            var root = GameObject.Find(WallsPath);
            wallsRoot = root != null ? root.transform : null;
            if (root == null) return;
            foreach (Transform c in root.transform) Register(c);
            AddMissingBlocks(root.transform);
        }

        // Files one child of the Walls root: its cell, its kind (wood / hard / crate) and its main renderer's base colour.
        static void Register(Transform c)
        {
            string prefix = c.name.StartsWith(WallPrefix) ? WallPrefix : c.name.StartsWith(HardPrefix) ? HardPrefix : c.name.StartsWith(CratePrefix) ? CratePrefix : null;
            if (prefix == null) return;
            var parts = c.name.Substring(prefix.Length).Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y)) return;
            var cell = new Vector2Int(x, y);
            if (prefix == WallPrefix) blocks[cell] = c.gameObject;
            else if (prefix == HardPrefix) hardBlocks[cell] = c.gameObject;
            else crateBlocks[cell] = c.gameObject;

            var body = prefix == CratePrefix ? c.Find("Crate_Body") : c;
            var r = body != null ? body.GetComponent<Renderer>() : null;
            if (r == null || r.sharedMaterial == null) return;
            var m = r.sharedMaterial;
            var col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            mains.Add((cell, r, col));
        }

        // The scene bakes one block per wall cell of the default map. Any other map (a Reinforced wall, a pillar, a wood wall
        // somewhere new) gets a clone of a baked block of the same kind — wood: 1.2 high with Mat_Wall_Wood, Solid: 1.5 high,
        // concrete grey Mat_Hard — at its cell. Reinforced cells use the wood block: ReinforcedWallViews turns it into steel.
        static void AddMissingBlocks(Transform root)
        {
            if (bound == null) return;
            GameObject woodTemplate = null, hardTemplate = null, crateTemplate = null;
            foreach (var go in blocks.Values) if (go != null) { woodTemplate = go; break; }
            foreach (var go in hardBlocks.Values) if (go != null) { hardTemplate = go; break; }
            foreach (var go in crateBlocks.Values) if (go != null) { crateTemplate = go; break; }
            for (int y = 0; y < bound.Height; y++)
                for (int x = 0; x < MapGrid.Width; x++)
                {
                    var t = bound.TileAt(x, y);
                    var cell = new Vector2Int(x, y);
                    if (t == TileType.Wall && woodTemplate != null && !blocks.ContainsKey(cell)) Clone(woodTemplate, WallPrefix, cell, root);
                    else if (t == TileType.Hard && hardTemplate != null && !hardBlocks.ContainsKey(cell)) Clone(hardTemplate, HardPrefix, cell, root);
                    else if (t == TileType.Crate && crateTemplate != null && !crateBlocks.ContainsKey(cell)) Clone(crateTemplate, CratePrefix, cell, root);
                }
        }

        static void Clone(GameObject template, string prefix, Vector2Int cell, Transform root)
        {
            var go = Object.Instantiate(template, root);
            go.name = prefix + cell.x + "_" + cell.y;
            go.transform.localPosition = new Vector3(cell.x, template.transform.localPosition.y, cell.y);   // the Walls root carries the z mirror
            go.transform.localRotation = template.transform.localRotation;
            go.transform.localScale = template.transform.localScale;
            runtimeBlocks.Add(go);
            Register(go.transform);
        }

        static void Sync()
        {
            if (bound == null) return;
            foreach (var kv in blocks)
                if (kv.Value != null) kv.Value.SetActive(bound.TileAt(kv.Key.x, kv.Key.y) == TileType.Wall);
            foreach (var kv in hardBlocks)
                if (kv.Value != null) kv.Value.SetActive(bound.TileAt(kv.Key.x, kv.Key.y) == TileType.Hard);
            foreach (var kv in crateBlocks)
                if (kv.Value != null) kv.Value.SetActive(bound.TileAt(kv.Key.x, kv.Key.y) == TileType.Crate);
        }

        /// <summary>
        /// Out-of-sight dimming: every wall/hard/crate main box whose cell (y*Width+x) isn't in `visibleCells`
        /// is drawn at .36× its base color. Null lights everything (the source's `title` case — no side yet).
        /// </summary>
        /// <summary>Every block drawn as if in sight (the tutorial's entrance shot, before anyone is looking). Setting it re-applies.</summary>
        public static bool ForceLit
        {
            get => forceLit;
            set { if (forceLit == value) return; forceLit = value; ApplyVision(lastVisible); }
        }
        static bool forceLit;

        public static void ApplyVision(HashSet<int> visibleCells)
        {
            lastVisible = visibleCells;
            if (forceLit) visibleCells = null;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            foreach (var (cell, r, baseColor) in mains)
            {
                if (r == null) continue;
                bool v = visibleCells == null || visibleCells.Contains(cell.y * MapGrid.Width + cell.x);
                bool faded = fadedOriginals.ContainsKey(r);
                if (v && !faded) { r.SetPropertyBlock(null); continue; }
                float k = v ? 1f : OutOfSight;
                var c = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, faded ? baseColor.a * fadeAlpha : baseColor.a);
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// cellMesh's per-panel material (e.g. p.mirror's 0x0f3a4a glass): the main box of wall cell `cell` draws with
        /// `mat`, whose colour becomes the base the out-of-sight dimming scales. Null restores the block's own material.
        /// </summary>
        public static void SetMainMaterial(Vector2Int cell, Material mat)
        {
            for (int i = 0; i < mains.Count; i++)
            {
                var (c, r, _) = mains[i];
                if (c != cell || r == null) continue;
                if (!swappedOriginals.ContainsKey(r)) swappedOriginals[r] = r.sharedMaterial;
                var m = mat != null ? mat : swappedOriginals[r];
                if (mat == null) swappedOriginals.Remove(r);
                r.sharedMaterial = m;
                var col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                mains[i] = (c, r, col);
            }
            ApplyVision(lastVisible);
        }

        /// <summary>World bounds of the wall block drawn for `cell`, or false when the scene has none.</summary>
        public static bool TryGetBlockBounds(Vector2Int cell, out Bounds b)
        {
            b = default;
            if (!blocks.TryGetValue(cell, out var go) || go == null) return false;
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return false;
            b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return true;
        }

        /// <summary>The block (direct child of Blindside_MapRoot/Walls) a collider belongs to, or null.</summary>
        public static Transform BlockOf(Transform t)
        {
            if (wallsRoot == null || t == null) return null;
            while (t != null && t.parent != wallsRoot) t = t.parent;
            return t;
        }

        /// <summary>
        /// Draws exactly these blocks at `alpha` (everything previously faded and not in the set goes back
        /// to its own opaque material). Out-of-sight dimming keeps applying on top.
        /// </summary>
        public static void SetOccluderFade(ICollection<Transform> blockRoots, float alpha)
        {
            fadeAlpha = alpha;
            var want = new HashSet<Renderer>();
            if (blockRoots != null)
                foreach (var b in blockRoots)
                    if (b != null) foreach (var r in b.GetComponentsInChildren<Renderer>()) want.Add(r);

            var drop = new List<Renderer>();
            foreach (var kv in fadedOriginals) if (!want.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var r in drop) Unfade(r);
            foreach (var r in want)
                if (r != null && !fadedOriginals.ContainsKey(r) && r.sharedMaterial != null)
                {
                    fadedOriginals[r] = r.sharedMaterial;
                    r.sharedMaterial = TransparentTwin(r.sharedMaterial);
                }
            ApplyVision(lastVisible);
            ApplyFadeToExtras();
        }

        /// <summary>Every faded block back to its own material and colour.</summary>
        public static void ClearOccluderFade()
        {
            if (fadedOriginals.Count == 0) return;
            foreach (var r in new List<Renderer>(fadedOriginals.Keys)) Unfade(r);
            fadedOriginals.Clear();
            ApplyVision(lastVisible);
        }

        static void Unfade(Renderer r)
        {
            if (r != null && fadedOriginals.TryGetValue(r, out var orig))
            {
                r.sharedMaterial = orig;
                if (!IsMain(r)) r.SetPropertyBlock(null);
            }
            fadedOriginals.Remove(r);
        }

        static bool IsMain(Renderer r)
        {
            foreach (var m in mains) if (m.main == r) return true;
            return false;
        }

        // Faded renderers ApplyVision doesn't own (e.g. a crate's band): their own colour at fadeAlpha.
        static void ApplyFadeToExtras()
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            foreach (var kv in fadedOriginals)
            {
                var r = kv.Key;
                if (r == null || IsMain(r)) continue;
                var m = kv.Value;
                var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                c.a *= fadeAlpha;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb);
            }
        }

        static Material TransparentTwin(Material m)
        {
            if (transparentTwins.TryGetValue(m, out var t) && t != null) return t;
            // Same recipe UnitView/CombatFx use for URP Lit/Unlit transparency.
            t = new Material(m) { name = m.name + " (Occluder Fade)" };
            if (t.HasProperty("_Surface"))
            {
                t.SetFloat("_Surface", 1f);
                t.SetFloat("_Blend", 0f);
                t.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                t.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                t.SetFloat("_ZWrite", 0f);
                t.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                t.DisableKeyword("_ALPHATEST_ON");
            }
            t.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            transparentTwins[m] = t;
            return t;
        }
    }
}
