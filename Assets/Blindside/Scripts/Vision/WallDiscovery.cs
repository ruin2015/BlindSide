// updateDiscovery()'s panel term (blindside_source/04_gameplay_combat_ai.js:21-29):
//   const tv=teamVision('atk');
//   for(const p of panels){ if(!p.cells.some(c=>tv.all.has(idx(c.x,c.y)))) continue;
//     if(p.reinforced&&!p.seenByAtk){ p.seenByAtk=true; ... refreshPanel(p) } }
// A reinforced wall is plain wood to the attack side until one of its cells has been in an attacker's sight (sticky).
// Shared by what draws the wall (ReinforcedWallViews) and what the attack AI may assume about it (EnemyAI.AiBreach),
// so both read the same MapGrid panel flag regardless of which side the human plays.
using System.Collections.Generic;
using Blindside.Data;
using Blindside.Map;

namespace Blindside.Vision
{
    public static class WallDiscovery
    {
        /// <summary>Marks every reinforced / electrified panel with a cell in a living, placed attacker's sight as
        /// discovered (`seenByAtk` / `elecSeen`). <paramref name="units"/> holds every unit; only IVisionAgents of the
        /// attack team count.</summary>
        /// <returns>true when at least one panel flag was newly set.</returns>
        public static bool Refresh(MapGrid map, VisionSystem vision, IEnumerable<object> units)
        {
            if (map == null || vision == null || units == null) return false;
            bool any = false;
            for (int i = 0; i < map.Panels.Count; i++)
            {
                var p = map.Panels[i];
                bool needReinf = p.reinforced && !p.seenByAtk, needElec = p.electrified && !p.elecSeen;
                if (!needReinf && !needElec) continue;
                bool seen = false;
                foreach (var c in p.cells)
                    if (SeenByAttack(vision, units, c.x, c.y)) { seen = true; break; }
                if (!seen) continue;
                if (needReinf) map.SetPanelSeenByAtk(p.id);
                if (needElec) map.SetPanelElecSeen(p.id);
                any = true;
            }
            return any;
        }

        // tv.all.has(idx(c.x,c.y)): the wall cell itself, hit by a ray (the cell is excluded from the ray's own block test).
        static bool SeenByAttack(VisionSystem vision, IEnumerable<object> units, int cx, int cy)
        {
            int k = cy * MapGrid.Width + cx;
            foreach (var o in units)
                if (o is IVisionAgent a && a.Alive && a.Placed && a.Team == OperatorTeam.Attack && vision.SeesPoint(a, cx, cy, exceptCell: k)) return true;
            return false;
        }
    }
}
