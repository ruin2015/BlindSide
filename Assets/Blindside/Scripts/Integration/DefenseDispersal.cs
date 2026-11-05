// The last step of the defense AI's preparation (after the walls, fields, cameras and mirror): the defenders spread out from the
// hostage room to ambush posts, or stay — the AI decides from the map.
//   threats — where the attack can come in: indoor floor next to outdoor ground (a doorway, weight 1), or next to an intact,
//             unreinforced, non-mirror wall with outdoor ground right behind it (a breach point, weight .6);
//   posts   — free indoor floor within PostHostageReach of the hostage (EnemyAI's defence leash is 9 m), reachable on foot, not in
//             a 1-wide corridor (a defender there would seal it for the attackers' path planning), and at least AmbushStandOff from
//             every threat it covers (it shoots first instead of standing in the doorway). A post covers a threat it has a line
//             of sight to within AmbushRange. Its value is the threats it covers (already covered ones count CoveredFactor),
//             plus a little for walls/crates right next to it (cover), times a ±Jitter roll of GameRng — so two matches
//             on the same map need not be set up the same way, while a seeded match always is;
//   choice  — greedily the best post, PostSpacing apart, while its value still reaches StopFraction of the first post's (and
//             MinGain): a map with one way in sends one or none, a house with many entrances sends several. The defender
//             nearest the hostage always stays; the others go to the post nearest them on foot (best post first), facing the
//             threats they cover. Everyone left over keeps their start.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Core;
using Blindside.Data;
using Blindside.Map;
using Blindside.Vision;

namespace Blindside.Integration
{
    public static class DefenseDispersal
    {
        public const float PostHostageReach = 8.5f;
        public const float AmbushRange = 6f;
        public const float AmbushStandOff = 2f;
        public const float PostSpacing = 3.5f;
        public const int MaxPosts = 3;
        public const float CoveredFactor = .3f;
        public const float StopFraction = .35f;
        public const float MinGain = 1f;
        public const float Jitter = .2f;

        public static void Apply(MapGrid map, VisionSystem vision, AbilityContext ctx, IReadOnlyList<SimUnit> units, SimHostage hostage, PrepReport report)
        {
            var data = MapLibrary.Current;
            if (data == null || units == null || vision == null) return;
            var defenders = units.Where(u => u.Team == OperatorTeam.Defense && u.Alive).ToList();
            if (defenders.Count < 2) return;
            float hx = hostage != null ? hostage.X : data.hostage.x, hy = hostage != null ? hostage.Y : data.hostage.y;

            bool InMap(int x, int y) => x >= 0 && y >= 0 && x < MapGrid.Width && y < map.Height;
            bool Walkable(int x, int y)
            {
                if (!InMap(x, y)) return false;
                var t = map.TileAt(x, y);
                return t == TileType.Floor || t == TileType.Out || t == TileType.Rubble || t == TileType.Extract;
            }
            bool Outdoor(int x, int y) => Walkable(x, y) && data.TerrainAt(x, y) != MapTerrain.In;
            bool IndoorFloor(int x, int y) => InMap(x, y) && data.TerrainAt(x, y) == MapTerrain.In && map.TileAt(x, y) == TileType.Floor;

            // ---- threats ----
            var threats = new List<(Vector2Int c, float w)>();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < MapGrid.Width; x++)
                {
                    if (!IndoorFloor(x, y)) continue;
                    float w = 0f;
                    foreach (var d in Dir4)
                    {
                        int nx = x + d.x, ny = y + d.y;
                        if (Outdoor(nx, ny)) { w = 1f; break; }
                        if (!InMap(nx, ny) || map.TileAt(nx, ny) != TileType.Wall) continue;
                        var p = map.PanelAt(nx, ny);
                        if (p.HasValue && !p.Value.reinforced && Outdoor(nx + d.x, ny + d.y)) w = Mathf.Max(w, .6f);
                    }
                    if (w > 0f) threats.Add((new Vector2Int(x, y), w));
                }
            if (threats.Count == 0) return;

            // ---- where the defenders can walk to (nobody else's cell, not the hostage's) ----
            bool Blocked(int x, int y, SimUnit self)
            {
                if (!Walkable(x, y)) return true;
                if (hostage != null && Mathf.RoundToInt(hostage.X) == x && Mathf.RoundToInt(hostage.Y) == y) return true;
                foreach (var o in units) if (o != self && o.Alive && o.Placed && Mathf.RoundToInt(o.X) == x && Mathf.RoundToInt(o.Y) == y) return true;
                return false;
            }
            var gadgetCells = new HashSet<Vector2Int>();
            foreach (var e in ctx.Electrics) gadgetCells.Add(new Vector2Int(Mathf.RoundToInt(e.x), Mathf.RoundToInt(e.y)));
            foreach (var c in ctx.Cameras) gadgetCells.Add(new Vector2Int(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y)));

            bool Corridor(int x, int y)
            {
                bool l = Walkable(x - 1, y), r = Walkable(x + 1, y), u = Walkable(x, y - 1), dn = Walkable(x, y + 1);
                int n = (l ? 1 : 0) + (r ? 1 : 0) + (u ? 1 : 0) + (dn ? 1 : 0);
                return n == 2 && ((l && r) || (u && dn));
            }
            int CoverAround(int x, int y)
            {
                int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dy != 0) && InMap(x + dx, y + dy) && !Walkable(x + dx, y + dy)) n++;
                return Mathf.Min(n, 3);
            }

            // ---- candidate posts and the threats each one covers ----
            var guard = defenders.OrderBy(u => Dist(u.X, u.Y, hx, hy)).First();
            var movers = defenders.Where(u => u != guard).ToList();
            var fields = movers.ToDictionary(u => u, u => Field(map, Mathf.RoundToInt(u.X), Mathf.RoundToInt(u.Y), (x, y) => !Blocked(x, y, u)));

            var cands = new List<(Vector2Int c, List<int> covers, float cover, float jitter)>();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < MapGrid.Width; x++)
                {
                    if (!IndoorFloor(x, y) || Blocked(x, y, null) || gadgetCells.Contains(new Vector2Int(x, y))) continue;
                    if (Dist(x, y, hx, hy) > PostHostageReach || Corridor(x, y)) continue;
                    if (!movers.Any(u => fields[u][y * MapGrid.Width + x] != int.MaxValue)) continue;
                    var covers = new List<int>();
                    bool tooClose = false;
                    for (int i = 0; i < threats.Count; i++)
                    {
                        var t = threats[i].c;
                        float d = Dist(x, y, t.x, t.y);
                        if (d > AmbushRange) continue;
                        if (!vision.LineOfSight(x, y, t.x, t.y, OperatorTeam.Defense)) continue;
                        if (d < AmbushStandOff) { tooClose = true; break; }
                        covers.Add(i);
                    }
                    if (tooClose || covers.Count == 0) continue;
                    cands.Add((new Vector2Int(x, y), covers, CoverAround(x, y) * .15f, 1f + (GameRng.Value * 2f - 1f) * Jitter));
                }
            if (cands.Count == 0) return;

            // ---- greedy choice: best marginal value first, spaced apart, until it stops paying ----
            var covered = new bool[threats.Count];
            var posts = new List<(Vector2Int c, List<int> covers)>();
            float first = -1f;
            int maxPosts = Mathf.Min(MaxPosts, movers.Count);
            while (posts.Count < maxPosts)
            {
                int best = -1; float bestScore = 0f, bestGain = 0f;
                for (int i = 0; i < cands.Count; i++)
                {
                    var c = cands[i];
                    if (posts.Any(p => Dist(p.c.x, p.c.y, c.c.x, c.c.y) < PostSpacing)) continue;
                    float gain = 0f;
                    foreach (int t in c.covers) gain += threats[t].w * (covered[t] ? CoveredFactor : 1f);
                    float score = (gain + c.cover) * c.jitter;
                    if (score > bestScore) { bestScore = score; bestGain = gain; best = i; }
                }
                if (best < 0) break;
                if (first < 0f) first = bestGain;
                if (bestGain < MinGain || bestGain < first * StopFraction) break;
                posts.Add((cands[best].c, cands[best].covers));
                foreach (int t in cands[best].covers) covered[t] = true;
            }

            // ---- who goes where: the post's nearest free defender on foot ----
            var assigned = new HashSet<SimUnit>();
            foreach (var p in posts)
            {
                int idx = p.c.y * MapGrid.Width + p.c.x;
                SimUnit pick = null; int pickD = int.MaxValue;
                foreach (var u in movers)
                {
                    if (assigned.Contains(u)) continue;
                    int d = fields[u][idx];
                    if (d < pickD) { pickD = d; pick = u; }
                }
                if (pick == null || pickD == int.MaxValue) continue;
                assigned.Add(pick);
                float fx = 0f, fy = 0f, fw = 0f;
                foreach (int t in p.covers) { fx += threats[t].c.x * threats[t].w; fy += threats[t].c.y * threats[t].w; fw += threats[t].w; }
                pick.MoveTo(p.c.x, p.c.y);
                if (fw > 0f) pick.Facing = Mathf.Atan2(fy / fw - p.c.y, fx / fw - p.c.x);
                report.posts.Add((pick.Id, p.c));
            }
        }

        static readonly Vector2Int[] Dir4 = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        static float Dist(float ax, float ay, float bx, float by) => Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        /// <summary>4-directional step count from (sx,sy) over `passable` (the start cell itself always counts).</summary>
        static int[] Field(MapGrid map, int sx, int sy, System.Func<int, int, bool> passable)
        {
            var dist = new int[MapGrid.Width * map.Height];
            for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
            if (sx < 0 || sy < 0 || sx >= MapGrid.Width || sy >= map.Height) return dist;
            var q = new Queue<int>();
            dist[sy * MapGrid.Width + sx] = 0; q.Enqueue(sy * MapGrid.Width + sx);
            while (q.Count > 0)
            {
                int c = q.Dequeue(), cx = c % MapGrid.Width, cy = c / MapGrid.Width;
                foreach (var d in Dir4)
                {
                    int nx = cx + d.x, ny = cy + d.y;
                    if (nx < 0 || ny < 0 || nx >= MapGrid.Width || ny >= map.Height) continue;
                    int n = ny * MapGrid.Width + nx;
                    if (dist[n] != int.MaxValue || !passable(nx, ny)) continue;
                    dist[n] = dist[c] + 1;
                    q.Enqueue(n);
                }
            }
            return dist;
        }
    }
}
