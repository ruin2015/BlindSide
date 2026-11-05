// Ported from the vision block in blindside_source/01_core_data_map_vision.js:
// VIS constant, opaqueRaw/opqGrid, losF, inView, seesPt/sees, fovCells, rayLen, visionPoly.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Map;

namespace Blindside.Vision
{
    public static class VisionConfig
    {
        public const float Circle = 2.3f;  // always-visible radius regardless of facing
        public const float Range = 10f;    // max sight distance inside the FOV cone
        public const float Half = 0.68f;   // half-angle of the FOV cone, radians
    }

    /// <summary>Read-only view of whatever unit/operator is doing the looking.</summary>
    public interface IVisionAgent
    {
        float X { get; }
        float Y { get; }
        float Facing { get; }              // radians
        OperatorTeam Team { get; }
        bool Alive { get; }
        bool Placed { get; }               // false during the pre-round placement phase (u.placed===false)
    }

    /// <summary>u.revealT — until when (Time.unscaledTime, seconds) the unit stays drawn for the other side
    /// even out of sight: shoot() sets the shooter +3.5s / the target max(…,+2.5s), resolveEncounter() both
    /// +3s, moveAlong's blocked step the blocker +3s. Read by shownToPlayer().</summary>
    public interface IRevealable
    {
        float RevealT { get; set; }
    }

    /// <summary>u.ghost (WRAITH's jamming): enemy drones/cameras don't pick the unit up (enemyVisible's `!e.ghost`).</summary>
    public interface IJammable
    {
        bool Ghost { get; }
    }

    /// <summary>
    /// Cell-grid + cone/circle vision queries against a <see cref="MapGrid"/>.
    /// Mirror wall panels are opaque to Attack but transparent to Defense (blindside_source
    /// opaqueRaw: `if(p.mirror&&team==='def') return false;`), matching the mirror operator ability.
    /// </summary>
    public class VisionSystem
    {
        readonly MapGrid map;

        int cachedVersion = -1;
        byte[] atkGrid;
        byte[] defGrid;

        /// <summary>Bump this whenever a wall panel's mirror/reinforced state (or any tile) changes.</summary>
        public int MapVersion;

        public VisionSystem(MapGrid map)
        {
            this.map = map;
            map.Changed += () => MapVersion++;   // bumpMap(): any panel change invalidates the opaque grids
        }

        static int JsRound(float v) => Mathf.FloorToInt(v + 0.5f); // matches JS Math.round, not banker's rounding

        static float AngNorm(float a)
        {
            while (a > Mathf.PI) a -= 2f * Mathf.PI;
            while (a < -Mathf.PI) a += 2f * Mathf.PI;
            return a;
        }

        /// <summary>gasSet: cells under a toxic gas cloud (y*Width+x), rebuilt by refreshGas (GasAbility.RefreshGas).</summary>
        public readonly HashSet<int> GasCells = new HashSet<int>();

        // 1:1 port of opaqueRaw(x,y,team): off-map/hard block, a wall blocks unless it's a mirror seen by
        // Defense, and any other cell blocks while it's in gasSet.
        // `'void'` is TL()'s off-map result — MapGrid.TileAt reports off-map cells as Out, so bounds are
        // checked here; the outdoor ground itself ('out') is not opaque.
        public bool IsOpaque(int x, int y, OperatorTeam team)
        {
            if (x < 0 || y < 0 || x >= MapGrid.Width || y >= map.Height) return true;
            TileType t = map.TileAt(x, y);
            if (t == TileType.Hard) return true;
            if (t == TileType.Wall)
            {
                var p = map.PanelAt(x, y);
                if (p.HasValue && p.Value.mirror && team == OperatorTeam.Defense) return false;
                return true;
            }
            return GasCells.Contains(y * MapGrid.Width + x);
        }

        byte[] BuildOpaqueGrid(OperatorTeam team)
        {
            var grid = new byte[MapGrid.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < MapGrid.Width; x++)
                    grid[y * MapGrid.Width + x] = IsOpaque(x, y, team) ? (byte)1 : (byte)0;
            return grid;
        }

        // Port of opqGrid(team): cached per team, invalidated when MapVersion changes.
        byte[] GetOpaqueGrid(OperatorTeam team)
        {
            if (cachedVersion != MapVersion)
            {
                cachedVersion = MapVersion;
                atkGrid = null;
                defGrid = null;
            }
            if (team == OperatorTeam.Attack)
                return atkGrid ??= BuildOpaqueGrid(team);
            return defGrid ??= BuildOpaqueGrid(team);
        }

        /// <summary>
        /// Port of losF(x0,y0,x1,y1,team,ex). `exceptCell`/`exceptCells` mirror the source's `ex`
        /// (a single cell index or a Set of indices) that is skipped instead of tested for opacity —
        /// used so a ray can terminate on/through the very wall cell it's evaluating visibility for.
        /// </summary>
        public bool LineOfSight(float x0, float y0, float x1, float y1, OperatorTeam team,
            int exceptCell = -1, HashSet<int> exceptCells = null)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 1e-6f) return true;

            int n = Mathf.CeilToInt(d / 0.1f);
            int sx = JsRound(x0), sy = JsRound(y0);
            byte[] grid = GetOpaqueGrid(team);

            for (int i = 1; i < n; i++)
            {
                int cx = JsRound(x0 + dx * i / n);
                int cy = JsRound(y0 + dy * i / n);
                if (cx == sx && cy == sy) continue;
                if (cx < 0 || cy < 0 || cx >= MapGrid.Width || cy >= map.Height) return false;

                int k = cy * MapGrid.Width + cx;
                if (k == exceptCell) continue;
                if (exceptCells != null && exceptCells.Contains(k)) continue;
                if (grid[k] != 0) return false;
            }
            return true;
        }

        // Port of inView(u,px,py): circle radius always visible, else within the facing cone out to Range.
        public bool InView(IVisionAgent u, float px, float py)
        {
            float dx = px - u.X, dy = py - u.Y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d <= VisionConfig.Circle) return true;
            if (d > VisionConfig.Range) return false;
            return Mathf.Abs(AngNorm(Mathf.Atan2(dy, dx) - u.Facing)) <= VisionConfig.Half;
        }

        // Port of seesPt(u,px,py,ex).
        public bool SeesPoint(IVisionAgent u, float px, float py, int exceptCell = -1, HashSet<int> exceptCells = null)
        {
            if (!u.Alive || !u.Placed) return false;
            if (!InView(u, px, py)) return false;
            return LineOfSight(u.X, u.Y, px, py, u.Team, exceptCell, exceptCells);
        }

        // Port of sees(a,b).
        public bool Sees(IVisionAgent a, IVisionAgent b)
        {
            if (!a.Alive || !b.Alive || !b.Placed) return false;
            return SeesPoint(a, b.X, b.Y);
        }

        /// <summary>Port of fovCells(u): every grid cell this agent currently sees (no per-frame cache).</summary>
        public HashSet<int> FovCells(IVisionAgent u)
        {
            var s = new HashSet<int>();
            if (!u.Alive || !u.Placed) return s;

            const int R = (int)VisionConfig.Range;
            int ux = JsRound(u.X), uy = JsRound(u.Y);
            int yMin = Mathf.Max(0, uy - R), yMax = Mathf.Min(map.Height - 1, uy + R);
            int xMin = Mathf.Max(0, ux - R), xMax = Mathf.Min(MapGrid.Width - 1, ux + R);

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    int k = y * MapGrid.Width + x;
                    if (InView(u, x, y) && LineOfSight(u.X, u.Y, x, y, u.Team, exceptCell: k))
                        s.Add(k);
                }
            }
            s.Add(uy * MapGrid.Width + ux);
            return s;
        }

        /// <summary>G.units as teamVision()/enemyVisible() iterate them (set by the match owner).</summary>
        public IReadOnlyList<IVisionAgent> Agents { get; set; }

        /// <summary>Port of teamVision(team).units: the union of every living teammate's fovCells(). The
        /// gadget half (drones / cameras → teamVision().gadget, see TeamVisionBuilder) is not included.</summary>
        public HashSet<int> TeamVision(OperatorTeam team)
        {
            var s = new HashSet<int>();
            if (Agents == null) return s;
            foreach (var u in Agents)
                if (u.Alive && u.Team == team) s.UnionWith(FovCells(u));
            return s;
        }

        /// <summary>gadgetSees(team,px,py): a living drone (attack, r 2.5) / camera (defense, r 3.2) of `team` with
        /// LOS to the point. Supplied by the match owner, since the gadget lists live with the abilities.</summary>
        public System.Func<OperatorTeam, float, float, bool> GadgetSees;

        /// <summary>Port of enemyVisible(team,e): some living unit of `team` sees e's exact position (seesPt, not
        /// the cell grid), or — unless e is jamming (`!e.ghost`) — one of the team's gadgets does.</summary>
        public bool EnemyVisible(OperatorTeam team, IVisionAgent e)
        {
            if (!e.Alive || Agents == null) return false;
            foreach (var u in Agents)
                if (u.Alive && u.Team == team && SeesPoint(u, e.X, e.Y)) return true;
            if (e is IJammable j && j.Ghost) return false;
            return GadgetSees != null && GadgetSees(team, e.X, e.Y);
        }

        // Port of rayLen(x,y,a,maxR,team): distance to the first opaque cell along a ray, or maxR.
        public float RayLength(float x, float y, float angle, float maxR, OperatorTeam team)
        {
            float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
            int sx = JsRound(x), sy = JsRound(y);
            byte[] grid = GetOpaqueGrid(team);

            for (float d = 0.05f; d <= maxR; d += 0.05f)
            {
                int cx = JsRound(x + cs * d), cy = JsRound(y + sn * d);
                if (cx == sx && cy == sy) continue;
                if (cx < 0 || cy < 0 || cx >= MapGrid.Width || cy >= map.Height) return d;
                if (grid[cy * MapGrid.Width + cx] != 0) return d;
            }
            return maxR;
        }

        /// <summary>
        /// Port of visionPoly(u): outline of the vision circle + cone as a fan of world-space points,
        /// suitable for feeding a mesh/LineRenderer.
        /// </summary>
        public List<Vector2> VisionPolygon(IVisionAgent u)
        {
            var pts = new List<Vector2>();
            float half = VisionConfig.Half;
            float deg = Mathf.Deg2Rad;

            void Push(float off, float r)
            {
                float a = u.Facing + off;
                float len = RayLength(u.X, u.Y, a, r, u.Team);
                pts.Add(new Vector2(u.X + Mathf.Cos(a) * len, u.Y + Mathf.Sin(a) * len));
            }

            for (float o = -Mathf.PI; o < -half; o += 5f * deg) Push(o, VisionConfig.Circle);
            Push(-half - 1e-4f, VisionConfig.Circle);
            for (float o = -half; o <= half + 1e-6f; o += 0.9f * deg) Push(o, VisionConfig.Range);
            Push(half + 1e-4f, VisionConfig.Circle);
            for (float o = half + 5f * deg; o < Mathf.PI; o += 5f * deg) Push(o, VisionConfig.Circle);

            return pts;
        }
    }
}
