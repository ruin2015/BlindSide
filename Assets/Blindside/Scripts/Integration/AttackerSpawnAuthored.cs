// The hand-placed random attacker drop (MapData.authored.spawnZones): randomAtkSpots() of blindside_source/05_ui_input_intro.js
// with the map's own rectangles in place of the source's four fixed bands (n / s / w / e, in that order on the default map):
//   each free outdoor cell (scan order: y, then x) belongs to the first rectangle that contains it;
//   the rectangles are shuffled (Fisher-Yates), the first two take the squad in turn, 5 attackers 1.4 m or more apart,
//   60 tries each; an attacker that finds no spot falls back to the map's default drop points (anchors.attackerDrops).
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Map;

namespace Blindside.Integration
{
    public static class AttackerSpawnAuthored
    {
        const float UnitRadius = .28f;   // UR
        const int Tries = 60;
        const float MinSpacing = 1.4f;

        public static SpawnPlan Plan(MapGrid map, AbilityContext ctx, MapAuthored authored)
        {
            var plan = new SpawnPlan();
            var rects = authored.spawnZones;
            var zones = new List<Vector2Int>[rects.Length];
            for (int i = 0; i < zones.Length; i++) zones[i] = new List<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < MapGrid.Width; x++)
                {
                    if (map.TileAt(x, y) != TileType.Out || !AbilitySystem.FreeAt(ctx, x, y, false, UnitRadius)) continue;
                    for (int i = 0; i < rects.Length; i++) if (rects[i].Contains(x, y)) { zones[i].Add(new Vector2Int(x, y)); break; }
                }
            for (int i = 0; i < rects.Length && i < plan.zoneSizes.Length; i++) plan.zoneSizes[i] = zones[i].Count;

            var keys = new List<int>();
            for (int i = 0; i < rects.Length; i++) keys.Add(i);
            for (int i = keys.Count - 1; i > 0; i--) { int j = Blindside.Core.GameRng.Next(i + 1); (keys[i], keys[j]) = (keys[j], keys[i]); }
            var chosen = new List<int>();
            for (int k = 0; k < keys.Count && k < 2; k++) { chosen.Add(keys[k]); plan.usedZones.Add(keys[k]); }

            for (int i = 0; i < 5; i++)
            {
                var z = zones[chosen[i % chosen.Count]];
                for (int tries = 0; tries < Tries && z.Count > 0; tries++)
                {
                    var c = z[Blindside.Core.GameRng.Next(z.Count)];
                    if (Apart(plan.spots, c)) { plan.spots.Add(c); break; }
                }
            }
            var drops = MapLibrary.Current.anchors.attackerDrops;
            while (plan.spots.Count < 5) plan.spots.Add(drops[plan.spots.Count]);
            return plan;
        }

        static bool Apart(List<Vector2Int> spots, Vector2Int c)
        {
            foreach (var o in spots) if (Vector2.Distance(o, c) <= MinSpacing) return false;
            return true;
        }
    }
}
