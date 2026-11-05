// Line of sight over a MapData's terrain, for the editor's overlays and checks. The game's VisionSystem is tied to the current map
// (MapGrid.Width reads MapLibrary.Current) and to a built MapGrid, and the editor must never touch the game's state, so this is a
// small copy of its three queries with the same rules and the same numbers: a wall (any kind) or the map edge blocks sight, crates
// and floors don't (VisionSystem.IsOpaque); rays step 0.05 (RayLength) / 0.1 (LineOfSight) and read the cell with JS-style rounding;
// a camera sees the cells within r = 3.2 that LineOfSight reaches, the target cell itself excepted (TeamVisionBuilder.AddCircleCells).
using System.Collections.Generic;
using UnityEngine;
using Blindside.Map;

namespace Blindside.MapEditor
{
    public static class MapSight
    {
        public const float CameraRadius = 3.2f;

        static int JsRound(float v) => Mathf.FloorToInt(v + .5f);

        public static bool Opaque(MapData d, int x, int y) =>
            x < 0 || y < 0 || x >= d.width || y >= d.height || MapData.IsWall(d.terrain[y * d.width + x]);

        /// <summary>VisionSystem.RayLength: how far a ray from (x,y) along `angle` (radians, grid axes) gets before it enters an
        /// opaque cell, at most maxR.</summary>
        public static float RayLength(MapData d, float x, float y, float angle, float maxR)
        {
            float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
            int sx = JsRound(x), sy = JsRound(y);
            for (float t = .05f; t <= maxR; t += .05f)
            {
                int cx = JsRound(x + cs * t), cy = JsRound(y + sn * t);
                if (cx == sx && cy == sy) continue;
                if (Opaque(d, cx, cy)) return t;
            }
            return maxR;
        }

        /// <summary>VisionSystem.LineOfSight (exceptCell = the target's own cell index, y*width+x).</summary>
        public static bool LineOfSight(MapData d, float x0, float y0, float x1, float y1, int exceptCell)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6f) return true;
            int n = Mathf.CeilToInt(len / .1f);
            int sx = JsRound(x0), sy = JsRound(y0);
            for (int i = 1; i < n; i++)
            {
                int cx = JsRound(x0 + dx * i / n), cy = JsRound(y0 + dy * i / n);
                if (cx == sx && cy == sy) continue;
                if (cx < 0 || cy < 0 || cx >= d.width || cy >= d.height) return false;
                int k = cy * d.width + cx;
                if (k == exceptCell) continue;
                if (MapData.IsWall(d.terrain[k])) return false;
            }
            return true;
        }

        /// <summary>The cells a camera at cell (cx,cy) with radius r sees, its own cell included.</summary>
        public static List<Vector2Int> VisibleCells(MapData d, int cx, int cy, float r)
        {
            var cells = new List<Vector2Int>();
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                {
                    if (x < 0 || y < 0 || x >= d.width || y >= d.height) continue;
                    float dx = x - cx, dy = y - cy;
                    if (Mathf.Sqrt(dx * dx + dy * dy) > r) continue;
                    if (x == cx && y == cy || LineOfSight(d, cx, cy, x, y, y * d.width + x)) cells.Add(new Vector2Int(x, y));
                }
            return cells;
        }

        /// <summary>The outline of what a 360° viewer at (cx,cy) sees out to r, cut by walls: one point per ray, in grid coordinates.</summary>
        public static List<Vector2> Polygon(MapData d, float cx, float cy, float r, float stepDegrees = 2f)
        {
            var pts = new List<Vector2>();
            int n = Mathf.Max(8, Mathf.RoundToInt(360f / stepDegrees));
            for (int i = 0; i < n; i++)
            {
                float a = i * 2f * Mathf.PI / n;
                float len = RayLength(d, cx, cy, a, r);
                pts.Add(new Vector2(cx + Mathf.Cos(a) * len, cy + Mathf.Sin(a) * len));
            }
            return pts;
        }
    }
}
