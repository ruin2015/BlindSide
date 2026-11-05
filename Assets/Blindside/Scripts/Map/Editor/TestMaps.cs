// Throw-away maps built in code for the dev harness (SimBatch.StartMaps) and the checks — nothing here is saved or shipped.
// None of them has the house intro (useHouseIntro false) and none relies on the default map's coordinates: the AI's
// preparation, the random drop and the fall-back goal are all derived from the terrain.
//   Small()   16×12  one room (8×6) with one door, a pillar, a crate and a pre-placed Reinforced stretch of wall
//   Multi()   30×24  a 24×18 building of six rooms (two Reinforced stretches, two pillars, crates), three outer doors
//   Sealed()  20×16  a building closed by ordinary (Solid) walls all round — the squad can never get in
using System.Linq;
using UnityEngine;

namespace Blindside.Map.Editor
{
    public static class TestMaps
    {
        sealed class Builder
        {
            public readonly int W, H;
            public readonly MapTerrain[] terrain;
            public readonly bool[] crates;
            public Builder(int w, int h) { W = w; H = h; terrain = new MapTerrain[w * h]; crates = new bool[w * h]; }
            public void Set(int x, int y, MapTerrain t) => terrain[y * W + x] = t;
            public void Rect(int x0, int y0, int x1, int y1, MapTerrain t) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, t); }
            /// <summary>A walled room: `wall` all round, indoor floor inside.</summary>
            public void Room(int x0, int y0, int x1, int y1, MapTerrain wall)
            {
                Rect(x0, y0, x1, y1, wall);
                Rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1, MapTerrain.In);
            }
            public void Crate(int x, int y) => crates[y * W + x] = true;

            public MapData Finish(string name, Vector2Int hostage, (int x, int y)[] defenders, Vector2 buildingCenter)
            {
                var d = new MapData
                {
                    name = name, width = W, height = H, useHouseIntro = false,
                    terrain = terrain, crates = crates, cameras = new Vector2Int[0], hostage = hostage,
                    exfils = new[]
                    {
                        new MapExfil { name = "A", x0 = 0, y0 = 0, x1 = 1, y1 = 1 },
                        new MapExfil { name = "B", x0 = W - 2, y0 = 0, x1 = W - 1, y1 = 1 },
                        new MapExfil { name = "C", x0 = W - 2, y0 = H - 2, x1 = W - 1, y1 = H - 1 },
                        new MapExfil { name = "D", x0 = 0, y0 = H - 2, x1 = 1, y1 = H - 1 },
                    },
                    defenderStarts = MapData.DefenderKeys.Select((k, i) => new MapDefenderStart { operatorKey = k, cell = new Vector2Int(defenders[i].x, defenders[i].y), facing = 0f }).ToArray(),
                    anchors = new MapAnchors
                    {
                        attackerDrops = Enumerable.Range(0, 5).Select(i => new Vector2Int(3 + i * 2, 0)).ToArray(),
                        facingTarget = buildingCenter,
                    },
                };
                return d;
            }
        }

        public static MapData Small()
        {
            var b = new Builder(16, 12);
            b.Room(4, 3, 11, 8, MapTerrain.Breakable);
            b.Set(7, 8, MapTerrain.In);                                   // the door, south
            b.Set(4, 5, MapTerrain.Reinforced); b.Set(4, 6, MapTerrain.Reinforced);   // west wall, pre-placed reinforcement
            b.Set(9, 7, MapTerrain.Solid);                                // a pillar
            b.Crate(9, 5);
            return b.Finish("小部屋", new Vector2Int(7, 5), new[] { (5, 4), (10, 4), (5, 7), (10, 6), (8, 6) }, new Vector2(7.5f, 5.5f));
        }

        public static MapData Multi()
        {
            var b = new Builder(30, 24);
            b.Room(3, 3, 26, 20, MapTerrain.Breakable);
            foreach (var (cx, cy) in new[] { (3, 3), (26, 3), (3, 20), (26, 20) }) b.Set(cx, cy, MapTerrain.Solid);   // corner posts
            for (int y = 4; y <= 19; y++) { b.Set(10, y, MapTerrain.Breakable); b.Set(18, y, MapTerrain.Breakable); }   // two long partitions
            for (int x = 4; x <= 25; x++) if (x != 10 && x != 18) b.Set(x, 11, MapTerrain.Breakable);                   // and a cross wall
            b.Set(10, 11, MapTerrain.Solid); b.Set(18, 11, MapTerrain.Solid);
            foreach (var (dx, dy) in new[] { (10, 8), (10, 15), (18, 8), (18, 15), (6, 11), (14, 11), (22, 11), (14, 20), (5, 3), (26, 12) }) b.Set(dx, dy, MapTerrain.In);   // doors
            b.Set(10, 12, MapTerrain.Reinforced); b.Set(10, 13, MapTerrain.Reinforced);
            b.Set(18, 5, MapTerrain.Reinforced); b.Set(18, 6, MapTerrain.Reinforced);
            b.Set(14, 7, MapTerrain.Solid); b.Set(14, 15, MapTerrain.Solid);
            b.Crate(5, 6); b.Crate(12, 14); b.Crate(22, 17); b.Crate(1, 10);
            return b.Finish("部屋の多い邸宅", new Vector2Int(14, 17), new[] { (13, 16), (15, 16), (12, 18), (16, 18), (14, 13) }, new Vector2(14.5f, 11.5f));
        }

        public static MapData Sealed()
        {
            var b = new Builder(20, 16);
            b.Room(4, 3, 15, 12, MapTerrain.Solid);                       // no door anywhere
            for (int y = 4; y <= 11; y++) b.Set(9, y, MapTerrain.Breakable);   // a partition inside, so the interior still has walls to plan around
            b.Set(9, 7, MapTerrain.In);
            b.Crate(12, 6);
            return b.Finish("密閉された部屋", new Vector2Int(12, 9), new[] { (11, 8), (13, 8), (11, 10), (13, 10), (7, 5) }, new Vector2(9.5f, 7.5f));
        }

        public static MapData[] All() => new[] { Small(), Multi(), Sealed() };
    }
}
