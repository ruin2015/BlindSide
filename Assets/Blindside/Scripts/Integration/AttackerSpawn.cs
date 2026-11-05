// randomAtkSpots() + spawnAttackers(true) (blindside_source/05_ui_input_intro.js:409-419), read verbatim:
//   const zones={n:[],s:[],w:[],e:[]};
//   for y,x: if(TL(x,y)!=='out'||!freeAt(x,y,false)) continue;
//     if(y<=3&&x>=3) n; else if(y>=19) s; else if(x<=1&&y>=4) w; else if(x>=24) e;
//   const keys=Object.keys(zones).sort(()=>Math.random()-.5).slice(0,2), out=[];
//   for(let i=0;i<5;i++){ const z=zones[keys[i%2]]; for(tries<60){ c=random(z); if(out.every(o=>hypot>1.4)){ out.push(c); break; } } }
//   while(out.length<5) out.push({x:ATK_SPAWN[out.length][0],y:ATK_SPAWN[out.length][1]});
// The source's four bands are fixed rows / columns of its one map. Here the four directions are the free outdoor cells beyond
// the bounding box of the indoor floor (MapData.IndoorBounds): north = rows above it, south = rows below it, west / east = the
// columns beside it within its rows (so a cell is in one direction at most). Of the directions holding 5 or more cells, two
// chosen at random take the squad in turn (5 attackers, 1.4 m or more apart); with one such direction, only that one. If no
// direction has 5 cells but the map still has 5 free outdoor cells, all of them form one zone. With fewer than 5 free outdoor
// cells the result carries the warning "降下可能区域が不足しています" instead of throwing, and the squad is filled from the map's
// default drop points. Extraction cells are not 'out' tiles, so nobody is dropped on them (as in the source).
// spawnAttackers: each attacker appears on its spot facing the building centre — no rope drop (the defense-side player's START OP).
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Map;

namespace Blindside.Integration
{
    public sealed class SpawnPlan
    {
        public const string NotEnoughZone = "降下可能区域が不足しています";
        public readonly List<Vector2Int> spots = new List<Vector2Int>();
        public string warning;                       // null when the drop is fine
        public readonly int[] zoneSizes = new int[4]; // free outdoor cells per direction (n, s, w, e)
        public readonly List<int> usedZones = new List<int>();

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder("zones n/s/w/e=" + string.Join("/", zoneSizes) + " used=" + (usedZones.Count == 0 ? "-" : string.Join("+", usedZones.ConvertAll(z => z < 4 ? "nswe"[z].ToString() : z.ToString()))));
            if (warning != null) sb.Append(" WARNING=" + warning);
            return sb.ToString();
        }
    }

    public static class AttackerSpawn
    {
        const float UnitRadius = .28f;   // UR
        const int MinZoneCells = 5;
        const float MinSpacing = 1.4f;

        /// <summary>0 north, 1 south, 2 west, 3 east, -1 inside the indoor box's rows/columns (no direction).</summary>
        public static int ZoneOf(RectInt indoor, int x, int y)
        {
            int x0 = indoor.xMin, x1 = indoor.xMax - 1, y0 = indoor.yMin, y1 = indoor.yMax - 1;
            if (y < y0) return 0;
            if (y > y1) return 1;
            if (x < x0) return 2;
            if (x > x1) return 3;
            return -1;
        }

        /// <summary>The free outdoor cells of each direction (n, s, w, e) — the whole zone lists, before anything is picked.</summary>
        public static List<Vector2Int>[] Zones(MapGrid map, AbilityContext ctx)
        {
            var indoor = MapLibrary.Current.IndoorBounds();
            var zones = new List<Vector2Int>[] { new List<Vector2Int>(), new List<Vector2Int>(), new List<Vector2Int>(), new List<Vector2Int>() };
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < MapGrid.Width; x++)
                {
                    if (map.TileAt(x, y) != TileType.Out || !AbilitySystem.FreeAt(ctx, x, y, false, UnitRadius)) continue;
                    int z = ZoneOf(indoor, x, y);
                    if (z >= 0) zones[z].Add(new Vector2Int(x, y));
                }
            return zones;
        }

        public static SpawnPlan Plan(MapGrid map, AbilityContext ctx)
        {
            var authored = MapLibrary.Current.authored;
            if (authored != null && authored.HasSpawnZones) return AttackerSpawnAuthored.Plan(map, ctx, authored);   // hand-placed rectangles
            var plan = new SpawnPlan();
            var zones = Zones(map, ctx);
            for (int i = 0; i < 4; i++) plan.zoneSizes[i] = zones[i].Count;

            // every free outdoor cell, for the "fewer than 5" test and the pooled fallback (also the ones in no direction).
            var all = new List<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < MapGrid.Width; x++)
                    if (map.TileAt(x, y) == TileType.Out && AbilitySystem.FreeAt(ctx, x, y, false, UnitRadius)) all.Add(new Vector2Int(x, y));

            if (all.Count < 5)
            {
                plan.warning = SpawnPlan.NotEnoughZone;
                plan.spots.AddRange(all);
                var drops = MapLibrary.Current.anchors.attackerDrops;
                while (plan.spots.Count < 5) plan.spots.Add(drops[plan.spots.Count]);
                return plan;
            }

            var eligible = new List<int>();
            for (int i = 0; i < 4; i++) if (zones[i].Count >= MinZoneCells) eligible.Add(i);
            var pools = new List<List<Vector2Int>>();
            if (eligible.Count == 0) pools.Add(all);
            else
            {
                // random order (a Fisher-Yates in place of the source's sort-shuffle), the first two.
                for (int i = eligible.Count - 1; i > 0; i--) { int j = Blindside.Core.GameRng.Next(i + 1); (eligible[i], eligible[j]) = (eligible[j], eligible[i]); }
                for (int k = 0; k < eligible.Count && k < 2; k++) { pools.Add(zones[eligible[k]]); plan.usedZones.Add(eligible[k]); }
            }

            for (int i = 0; i < 5; i++)
            {
                // the unit's own direction first, then the other chosen one, then any free outdoor cell.
                var order = new List<List<Vector2Int>> { pools[i % pools.Count] };
                if (pools.Count > 1) order.Add(pools[(i + 1) % pools.Count]);
                order.Add(all);
                bool placed = false;
                foreach (var z in order)
                {
                    for (int tries = 0; tries < 60 && !placed; tries++)
                    {
                        var c = z[Blindside.Core.GameRng.Next(z.Count)];
                        if (Apart(plan.spots, c, MinSpacing)) { plan.spots.Add(c); placed = true; }
                    }
                    if (placed) break;
                }
                if (placed) continue;
                // no cell keeps 1.4 m from the others: take the free one that is farthest from them (never the same cell twice).
                Vector2Int best = all[0]; float bestD = -1f;
                foreach (var c in all)
                {
                    if (plan.spots.Contains(c)) continue;
                    float d = float.MaxValue;
                    foreach (var o in plan.spots) d = Mathf.Min(d, Vector2.Distance(o, c));
                    if (d > bestD) { bestD = d; best = c; }
                }
                plan.spots.Add(best);
            }
            return plan;
        }

        static bool Apart(List<Vector2Int> spots, Vector2Int c, float spacing)
        {
            foreach (var o in spots) if (Vector2.Distance(o, c) <= spacing) return false;
            return true;
        }

        /// <summary>The last drop's warning ("" when there was none) — read by the log and the dev harness.</summary>
        public static SpawnPlan LastPlan { get; private set; }

        /// <summary>spawnAttackers(true): the attack squad (in unit order) appears on random outside spots, facing the building centre.</summary>
        public static SpawnPlan Spawn(MapGrid map, AbilityContext ctx, IReadOnlyList<SimUnit> units)
        {
            var plan = Plan(map, ctx);
            LastPlan = plan;
            if (plan.warning != null)
            {
                Debug.LogWarning("[AttackerSpawn] " + plan.warning);
                Blindside.UI.HudLog.Add(plan.warning, Blindside.UI.HudLogType.Warn);
            }
            var face = MapLibrary.Current.FacingTarget;
            int i = 0;
            foreach (var u in units)
            {
                if (u.Team != OperatorTeam.Attack) continue;
                var s = plan.spots[i++];
                u.MoveTo(s.x, s.y);
                u.Facing = Mathf.Atan2(face.y - s.y, face.x - s.x);
                u.Placed = true;
            }
            return plan;
        }
    }
}
