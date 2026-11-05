// Cuts the wall cells of a map into panels: runs of Breakable or Reinforced cells along the wall's direction, sliced two cells
// at a time, and cut wherever the kind changes (buildMap() in blindside_source/01_core_data_map_vision.js, plus the wall kinds).
// One implementation for the game (MapGrid.BuildPanels) and the map editor, so what the editor draws as a panel is what a match
// will treat as one. Solid walls are never panels; they only count as "wall-like" when sensing which way a run goes.
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Map
{
    /// <summary>One panel: its 1-2 cells, whether it starts reinforced, and the wall (run) it was cut from.</summary>
    public struct PanelRun
    {
        public List<Vector2Int> cells;
        public bool reinforced;
        public int wallId;
    }

    public static class WallPanelSplit
    {
        public const int None = 0, Solid = 1, Breakable = 2, Reinforced = 3;

        /// <summary>`kindAt(x,y)` gives None / Solid / Breakable / Reinforced for a cell inside the grid. Panels come out in
        /// the order buildMap() creates them (scan order, a wall's panels together), wall ids counting up from 0.</summary>
        public static List<PanelRun> Split(int width, int height, System.Func<int, int, int> kindAt)
        {
            var panels = new List<PanelRun>();
            var assigned = new bool[width * height];
            int Kind(int x, int y) => x < 0 || y < 0 || x >= width || y >= height ? None : kindAt(x, y);
            bool Wallish(int x, int y) => Kind(x, y) != None;   // a wall or a pillar, like wallish() in the source
            int walls = 0;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int k = Kind(x, y);
                    if ((k != Breakable && k != Reinforced) || assigned[y * width + x]) continue;

                    bool hz = Wallish(x - 1, y) || Wallish(x + 1, y);
                    bool vt = Wallish(x, y - 1) || Wallish(x, y + 1);
                    bool horiz = hz || !vt;
                    int dx = horiz ? 1 : 0, dy = horiz ? 0 : 1;

                    // the run also ends where the kind changes, so a panel is one kind throughout.
                    var run = new List<Vector2Int>();
                    int cx = x, cy = y;
                    while (Kind(cx, cy) == k && !assigned[cy * width + cx])
                    {
                        run.Add(new Vector2Int(cx, cy));
                        cx += dx; cy += dy;
                    }

                    int wallId = walls++;
                    for (int i = 0; i < run.Count; i += 2)
                    {
                        var cells = run.GetRange(i, Mathf.Min(2, run.Count - i));
                        foreach (var c in cells) assigned[c.y * width + c.x] = true;
                        panels.Add(new PanelRun { cells = cells, reinforced = k == Reinforced, wallId = wallId });
                    }
                }
            return panels;
        }
    }
}
