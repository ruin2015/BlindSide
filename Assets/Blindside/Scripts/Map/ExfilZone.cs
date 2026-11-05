// The hostage extraction points, read from the current MapData (MapData.exfils — the one place their coordinates live). The
// source (blindside_source) has a single zone written as 'E' cells in MAP (01_core_data_map_vision.js:6-7, x0-2 × y0-1); here
// the terrain carries no 'E' and MapGrid.CreateDefault() stamps every zone into the tile grid as TileType.Extract, so everything that still
// reads the tile (walkability, the deployment mask) sees the same cells the win check and the AI goal use.
// Coordinates are grid coordinates (y as in MAP, y=0 the top row); the renderer alone flips to z=-y (GridSpace).
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Map
{
    public sealed class ExfilZone
    {
        public readonly string Name;                 // "A".."D"
        public readonly int X0, X1, Y0, Y1;          // inclusive grid bounds

        ExfilZone(string name, int x0, int x1, int y0, int y1) { Name = name; X0 = x0; X1 = x1; Y0 = y0; Y1 = y1; }

        public string Label => "EXFIL " + Name;
        public int Width => X1 - X0 + 1;
        public int Height => Y1 - Y0 + 1;
        /// <summary>Centre in grid coordinates (cell centres, so a 3×2 zone at x0-2,y0-1 centres on (1, .5)).</summary>
        public Vector2 Center => new Vector2((X0 + X1) * .5f, (Y0 + Y1) * .5f);
        public bool Contains(int x, int y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;

        public IEnumerable<Vector2Int> Cells
        {
            get { for (int y = Y0; y <= Y1; y++) for (int x = X0; x <= X1; x++) yield return new Vector2Int(x, y); }
        }

        static MapData builtFor;
        static List<ExfilZone> built;

        /// <summary>The current map's extraction zones (MapData.exfils), in order. The default map's are A top-left, B top-right,
        /// C bottom-right, D bottom-left — each 3×2 cells.</summary>
        public static IReadOnlyList<ExfilZone> All
        {
            get
            {
                var map = MapLibrary.Current;
                if (!ReferenceEquals(map, builtFor))
                {
                    built = new List<ExfilZone>();
                    foreach (var e in map.exfils) built.Add(new ExfilZone(e.name, e.x0, e.x1, e.y0, e.y1));
                    builtFor = map;
                }
                return built;
            }
        }

        /// <summary>"4か所(EXFIL A〜D)" — the wording HUD / title share (EXFIL and the letter joined by a no-break space).</summary>
        public static string RangeText => All.Count == 1 ? $"1か所(EXFIL {All[0].Name})" : $"{All.Count}か所(EXFIL {All[0].Name}〜{All[All.Count - 1].Name})";

        /// <summary>Is grid cell (x,y) inside any extraction zone? Callers round positions first (the source's TLp uses Math.round).</summary>
        public static bool IsExfilTile(int x, int y)
        {
            foreach (var z in All) if (z.Contains(x, y)) return true;
            return false;
        }

        /// <summary>Writes every zone cell into the map as TileType.Extract (what an 'E' in MAP did).</summary>
        public static void Stamp(MapGrid map)
        {
            foreach (var z in All) map.StampTiles(z.Cells, TileType.Extract);
        }

        /// <summary>
        /// The zone with the shortest walking distance from <paramref name="position"/> (grid coordinates): a 4-neighbour
        /// flood over the tiles a foot soldier can stand on (floor / outside / rubble / extraction — the tile part of the
        /// AI's blockCell), ties going to the earlier zone in <see cref="All"/>. Null when none is reachable.
        /// </summary>
        public static ExfilZone NearestExfil(MapGrid map, Vector2 position)
        {
            int W = MapGrid.Width, H = map.Height;
            int sx = Mathf.FloorToInt(position.x + .5f), sy = Mathf.FloorToInt(position.y + .5f);   // Math.round
            if (sx < 0 || sy < 0 || sx >= W || sy >= H) return null;
            var dist = new int[W * H];
            for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
            var q = new Queue<int>();
            dist[sy * W + sx] = 0; q.Enqueue(sy * W + sx);
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % W, cy = c / W;
                foreach (var d in dirs)
                {
                    int nx = cx + d.x, ny = cy + d.y;
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    int n = ny * W + nx;
                    if (dist[n] != int.MaxValue || !Walkable(map.TileAt(nx, ny))) continue;
                    dist[n] = dist[c] + 1; q.Enqueue(n);
                }
            }
            ExfilZone best = null; int bestD = int.MaxValue;
            foreach (var z in All)
                foreach (var c in z.Cells)
                {
                    int d = dist[c.y * W + c.x];
                    if (d < bestD) { bestD = d; best = z; }
                }
            return best;
        }

        static bool Walkable(TileType t) => t == TileType.Floor || t == TileType.Out || t == TileType.Rubble || t == TileType.Extract;
    }
}
