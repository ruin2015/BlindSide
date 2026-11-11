// Ported from MAP + buildMap() in blindside_source/01_core_data_map_vision.js.
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Map
{
    // Rubble/Hole added for the breaching abilities (breakPanel in 04_gameplay_combat_ai.js):
    // hammer/thermite leave 'rubble' (fully open), launcher leaves 'hole' (blocks foot traffic,
    // not drones, per blockCell's `if(isDrone&&t==='hole') return false`).
    public enum TileType { Out, Floor, Wall, Hard, Crate, Extract, Rubble, Hole }

    public struct WallPanel
    {
        public int id;
        public int wallId;
        public List<Vector2Int> cells;     // 1 or 2 cells (runs are sliced 2 at a time)
        public float centerX, centerY;
        public bool mirror;                // p.mirror in source: def-only see-through, set by the mirror ability
        public bool reinforced;            // p.reinforced in source: blocks LOS/throws same as an intact wall
        public bool electrified;           // p.electrified in source: set by the shock ability, blocks thermite
        public bool seenByAtk;             // p.seenByAtk in source: an attacker has had this reinforced panel in sight (sticky)
        public bool elecSeen;              // p.elecSeen in source: an attacker has had this electrified panel in sight (sticky)
    }

    public class WallGroup
    {
        public int id;
        public readonly List<int> panelIds = new List<int>();
    }

    /// <summary>Parses the map string grid into tiles + destructible wall panels (2-cell units).</summary>
    public class MapGrid
    {
        /// <summary>Width of the current map (MapLibrary.Current).</summary>
        public static int Width => MapLibrary.Current.width;
        public readonly int Height;

        public readonly TileType[] Tiles;          // row-major, index = y*Width+x
        public readonly int[] PanelIndexAt;        // -1 if the cell isn't part of a wall panel
        readonly bool[] reinforcedAtStart;         // MapTerrain.Reinforced cells: their panel starts reinforced, and a run never mixes them with Breakable
        public readonly List<WallPanel> Panels = new List<WallPanel>();
        public readonly List<WallGroup> Walls = new List<WallGroup>();
        public readonly List<Vector2Int> FixedCameras = new List<Vector2Int>();
        public Vector2Int? Hostage { get; private set; }

        /// <summary>A panel's opacity-relevant state changed (broken, mirror, reinforced) — the source's
        /// bumpMap()/G.ver++ after breakPanel and the mirror/reinforce effects.</summary>
        public event System.Action Changed;

        /// <summary>Builds the tiles and wall panels of `data`. It must be the current map (MapGrid.Width and ExfilZone.All read it).</summary>
        public MapGrid(MapData data)
        {
            if (!ReferenceEquals(data, MapLibrary.Current) && !MapLibrary.SetCurrent(data))
                throw new System.ArgumentException("map data is invalid");
            Height = data.height;
            Tiles = new TileType[Width * Height];
            PanelIndexAt = new int[Width * Height];
            reinforcedAtStart = new bool[Width * Height];
            for (int i = 0; i < PanelIndexAt.Length; i++) PanelIndexAt[i] = -1;
            ParseTiles(data);
            BuildPanels();
        }

        /// <summary>The battlefield of the current map: its terrain plus the extraction zones (ExfilZone.All is their only definition).</summary>
        public static MapGrid CreateDefault()
        {
            var map = new MapGrid(MapLibrary.Current);
            ExfilZone.Stamp(map);
            return map;
        }

        /// <summary>Sets tiles outside the string parse (the extraction zones). Not a wall change, so no <see cref="Changed"/>.</summary>
        public void StampTiles(IEnumerable<Vector2Int> cells, TileType type)
        {
            foreach (var c in cells) if (InBounds(c.x, c.y)) Tiles[Idx(c.x, c.y)] = type;
        }

        int Idx(int x, int y) => y * Width + x;
        bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public TileType TileAt(int x, int y) => InBounds(x, y) ? Tiles[Idx(x, y)] : TileType.Out;

        public WallPanel? PanelAt(int x, int y)
        {
            if (TileAt(x, y) != TileType.Wall) return null;
            int p = PanelIndexAt[Idx(x, y)];
            return p >= 0 ? Panels[p] : (WallPanel?)null;
        }

        // Structs in a List<T> aren't addressable in place, so mutating panel state (mirror/
        // reinforced ability effects) goes through these instead of `Panels[i].mirror = ...`.
        public void SetPanelMirror(int panelId, bool value)
        {
            var p = Panels[panelId]; p.mirror = value; Panels[panelId] = p;
            Changed?.Invoke();
        }

        public void SetPanelReinforced(int panelId, bool value)
        {
            var p = Panels[panelId]; p.reinforced = value; Panels[panelId] = p;
            Changed?.Invoke();
        }

        /// <summary>A reinforced / electrified panel was discovered by the attack side (updateDiscovery's
        /// `p.seenByAtk=true` / `p.elecSeen=true`, then refreshPanel(p)). Not an opacity change, so it doesn't go
        /// through <see cref="Changed"/>.</summary>
        public event System.Action PanelDiscovered;

        public void SetPanelSeenByAtk(int panelId)
        {
            var p = Panels[panelId];
            if (p.seenByAtk) return;
            p.seenByAtk = true; Panels[panelId] = p;
            PanelDiscovered?.Invoke();
        }

        public void SetPanelElecSeen(int panelId)
        {
            var p = Panels[panelId];
            if (p.elecSeen) return;
            p.elecSeen = true; Panels[panelId] = p;
            PanelDiscovered?.Invoke();
        }

        /// <summary>bumpMap() for a change outside the panels (refreshGas: gas clouds added/expired).</summary>
        public void BumpMap() => Changed?.Invoke();

        /// <summary>Cells under a smoke screen (y*Width+x): sight stops there and shots through them are harder. Bump the map after changing it.</summary>
        public readonly HashSet<int> SmokeCells = new HashSet<int>();

        public void SetPanelElectrified(int panelId, bool value)
        {
            var p = Panels[panelId]; p.electrified = value; Panels[panelId] = p;
        }

        /// <summary>
        /// Port of breakPanel(p,st): stamps every cell of the panel with the new tile state and
        /// clears reinforced/electrified/mirror. Caller still owns fx/sfx/log and bumping the
        /// VisionSystem's MapVersion (source: boom() per cell, sfx('boom'), bumpMap(), drawFloor()).
        /// </summary>
        public void BreakPanel(int panelId, TileType newState)
        {
            var p = Panels[panelId];
            p.reinforced = false; p.electrified = false; p.mirror = false;
            Panels[panelId] = p;
            foreach (var c in p.cells) Tiles[Idx(c.x, c.y)] = newState;
            Changed?.Invoke();
        }

        void ParseTiles(MapData data)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    bool crate = data.CrateAt(x, y);
                    TileType t;
                    switch (data.TerrainAt(x, y))
                    {
                        case MapTerrain.In: t = crate ? TileType.Crate : TileType.Floor; break;
                        case MapTerrain.Solid: t = TileType.Hard; break;   // no panel: nothing can target or break it
                        case MapTerrain.Breakable: t = TileType.Wall; break;
                        // A reinforced wall is a wall tile whose panel starts reinforced (p.reinforced): shots and sight stop
                        // at it, hammer and shotgun can't open it, launcher / thermite / mirror work as on any reinforced panel.
                        case MapTerrain.Reinforced: t = TileType.Wall; reinforcedAtStart[Idx(x, y)] = true; break;
                        default: t = crate ? TileType.Crate : TileType.Out; break;
                    }
                    Tiles[Idx(x, y)] = t;
                }
            FixedCameras.AddRange(data.cameras);
            Hostage = data.hostage;
        }

        // The cutting itself (runs, 2-cell slices, cuts where the kind changes; 'hard' pillars count as wall-like only for sensing
        // a run's direction) is WallPanelSplit's — the map editor draws the same panels.
        void BuildPanels()
        {
            var runs = WallPanelSplit.Split(Width, Height, (x, y) =>
            {
                var t = Tiles[Idx(x, y)];
                return t == TileType.Hard ? WallPanelSplit.Solid
                    : t == TileType.Wall ? (reinforcedAtStart[Idx(x, y)] ? WallPanelSplit.Reinforced : WallPanelSplit.Breakable)
                    : WallPanelSplit.None;
            });

            WallGroup wall = null;
            foreach (var run in runs)
            {
                if (wall == null || wall.id != run.wallId) { wall = new WallGroup { id = run.wallId }; Walls.Add(wall); }
                float sumX = 0f, sumY = 0f;
                foreach (var c in run.cells) { sumX += c.x; sumY += c.y; }
                var panel = new WallPanel
                {
                    id = Panels.Count,
                    wallId = wall.id,
                    cells = run.cells,
                    reinforced = run.reinforced,
                    centerX = sumX / run.cells.Count,
                    centerY = sumY / run.cells.Count,
                };
                foreach (var c in run.cells) PanelIndexAt[Idx(c.x, c.y)] = panel.id;
                Panels.Add(panel);
                wall.panelIds.Add(panel.id);
            }
        }
    }
}
