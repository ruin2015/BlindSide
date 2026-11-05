// Port of teamVision(team)/circleCells(...) from blindside_source/01_core_data_map_vision.js
// (~lines 161-172): the set of grid cells a team currently sees is the union of (1) every alive
// unit's cone/circle FOV and (2) each of the team's gadgets' fixed-radius, LOS-gated vision circle
// (attack: drones r=2.5; defense: cameras r=3.2). FogOfWarRenderer consumes the result directly.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Map;
using Blindside.Vision;
using Blindside.Drones;
using Blindside.Abilities;

namespace Blindside.Rendering
{
    public static class TeamVisionBuilder
    {
        /// <summary>Port of circleCells(cx,cy,r,team,out): LOS-gated disc of cells around a gadget.</summary>
        public static void AddCircleCells(VisionSystem vision, MapGrid map, float cx, float cy, float r,
            OperatorTeam team, HashSet<int> outCells)
        {
            int yMin = Mathf.FloorToInt(cy - r), yMax = Mathf.CeilToInt(cy + r);
            int xMin = Mathf.FloorToInt(cx - r), xMax = Mathf.CeilToInt(cx + r);
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    if (x < 0 || y < 0 || x >= MapGrid.Width || y >= map.Height) continue;   // TL(x,y)==='void' (off-map only)
                    float dx = x - cx, dy = y - cy;
                    if (Mathf.Sqrt(dx * dx + dy * dy) > r) continue;
                    int k = y * MapGrid.Width + x;
                    if (vision.LineOfSight(cx, cy, x, y, team, exceptCell: k))
                        outCells.Add(k);
                }
            }
        }

        /// <summary>
        /// Port of teamVision(team).all: stage 1 (unit FOV) unioned with stage 2 (gadget vision).
        /// Pass only the gadget list relevant to `team` (source: drones for attack, cams for defense).
        /// </summary>
        public static HashSet<int> BuildVisibleCells(
            VisionSystem vision, MapGrid map, OperatorTeam team,
            IEnumerable<IVisionAgent> units,
            IEnumerable<Drone> drones = null,
            IEnumerable<SurveillanceCamera> cameras = null)
        {
            var all = new HashSet<int>();

            // Stage 1: unit vision.
            foreach (var u in units)
            {
                if (u.Team != team || !u.Alive || !u.Placed) continue;
                foreach (var k in vision.FovCells(u)) all.Add(k);
            }

            // Stage 2: gadget vision (source: `team==='atk' ? drones : cams`).
            if (team == OperatorTeam.Attack && drones != null)
                foreach (var d in drones)
                    if (d.alive) AddCircleCells(vision, map, d.x, d.y, 2.5f, team, all);

            if (team == OperatorTeam.Defense && cameras != null)
                foreach (var c in cameras)
                    if (c.alive) AddCircleCells(vision, map, c.x, c.y, 3.2f, team, all);

            return all;
        }
    }
}
