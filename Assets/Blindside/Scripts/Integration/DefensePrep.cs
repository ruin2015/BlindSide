// The defense AI's setup when the human plays the attack side (aiDefPrep, blindside_source/04_gameplay_combat_ai.js ~335-344):
//   const cand=panels.filter(canReinforce).sort((a,b)=>hypot(a.cx-h.x,a.cy-h.y)-hypot(b.cx-h.x,b.cy-h.y));
//   for(const p of cand.slice(0,10)){ p.reinforced=true; G.reinforceLeft--; }
//   for(const [x,y] of [[11,14],[15,16]]){ G.electrics.push({x,y,r:1.6,discovered:false}); volt.charges--;
//     for(const p of panels) if(p.reinforced&&p.cells.some(c=>hypot(c.x-x,c.y-y)<=2.2)) p.electrified=true; }
//   for(const [x,y] of [[8,12],[19,15]]){ G.cams.push({x,y,alive:true,discovered:false,placed:true}); argus.charges--; }
//   const mp=panelAt(14,13); if(mp){ mp.mirror=true; mp.reinforced=false; obs.charges--; } bumpMap();
// The source hard-codes the two electric fields, the two cameras and the mirror wall for its one house. Here they are chosen
// from the map, so any MapData works (no coordinates are read from it):
//   reinforce — the 10 intact Breakable panels nearest the hostage (a pre-placed Reinforced wall is not a candidate and is not
//               part of the 10);
//   VOLT      — two electric fields on free indoor cells, the ones with the most reinforced panels within the field's reach
//               (radius 1.6 + the wall cell's half-width, the same reach that electrifies a panel), 3 m or more apart;
//   ARGUS     — two cameras on free indoor cells within 8 m of the hostage, the ones that see the most indoor cells within the
//               camera's 3.2 m radius (line of sight, the defense's), 5 m or more apart;
//   OBSIDIAN  — a mirror on the Breakable panel nearest the hostage that has floor on both sides.
// A step with nowhere to go is skipped, never an exception. Steps 1-4 are deterministic (no random numbers).
// Then (both the computed and the hand-placed path) step 5, DefenseDispersal: the defenders spread out to ambush posts or stay —
// it draws GameRng (a seeded match is still reproducible).
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Map;
using Blindside.Vision;

namespace Blindside.Integration
{
    /// <summary>What one <see cref="DefensePrep.Apply"/> placed (for the log and the dev harness).</summary>
    public sealed class PrepReport
    {
        public int reinforced;
        public readonly List<Vector2Int> electrics = new List<Vector2Int>();
        public readonly List<Vector2Int> cameras = new List<Vector2Int>();
        public Vector2Int? mirror;
        /// <summary>Defenders DefenseDispersal sent out to an ambush post (the rest stayed at their start).</summary>
        public readonly List<(string id, Vector2Int cell)> posts = new List<(string, Vector2Int)>();

        public override string ToString()
        {
            var sb = new StringBuilder($"R={reinforced} E={electrics.Count}");
            foreach (var c in electrics) sb.Append($"({c.x},{c.y})");
            sb.Append($" C={cameras.Count}");
            foreach (var c in cameras) sb.Append($"({c.x},{c.y})");
            sb.Append(mirror.HasValue ? $" M=({mirror.Value.x},{mirror.Value.y})" : " M=-");
            sb.Append($" P={posts.Count}");
            foreach (var p in posts) sb.Append($"{p.id}({p.cell.x},{p.cell.y})");
            return sb.ToString();
        }
    }

    public static class DefensePrep
    {
        public const int ReinforceCount = 10;
        public const float FieldRadius = 1.6f;
        public const float ElectrifyReach = 2.2f;            // FieldRadius + .6: a panel with a cell this close to the centre is electrified
        public const float ElectricSpacing = 3f;
        public const float CameraRadius = 3.2f;              // a placed camera's sight (circleCells r)
        public const float CameraMaxHostageDistance = 8f;
        public const float CameraSpacing = 5f;

        public static PrepReport Apply(MapGrid map, VisionSystem vision, AbilityContext ctx, IReadOnlyList<SimUnit> units, SimHostage hostage)
        {
            var report = new PrepReport();
            float hx = hostage != null ? hostage.X : MapLibrary.Current.hostage.x;
            float hy = hostage != null ? hostage.Y : MapLibrary.Current.hostage.y;
            float DistToHostage(float x, float y) => Mathf.Sqrt((x - hx) * (x - hx) + (y - hy) * (y - hy));

            // canReinforce(p): intact, not reinforced, not a mirror — nearest the hostage first (OrderBy is stable, like Array.sort).
            var cand = map.Panels
                .Where(p => !p.reinforced && !p.mirror && p.cells.All(c => map.TileAt(c.x, c.y) == TileType.Wall))
                .OrderBy(p => DistToHostage(p.centerX, p.centerY))
                .ToList();

            // 1) reinforce the 10 nearest.
            foreach (var p in cand.Take(ReinforceCount)) { map.SetPanelReinforced(p.id, true); report.reinforced++; }

            var volt = Find(units, "VOLT"); var argus = Find(units, "ARGUS"); var obs = Find(units, "OBSIDIAN");
            var authored = MapLibrary.Current.authored;
            if (authored != null && authored.defensePrep)
            {
                DefensePrepAuthored.Apply(map, vision, ctx, authored, report, volt, argus, obs);   // hand-placed: steps 2-4 as listed
                DefenseDispersal.Apply(map, vision, ctx, units, hostage, report);                  // 5) ambush posts (computed on every map)
                return report;
            }
            var free = FreeIndoorCells(map, units, hostage);

            // 2) two electric fields where they reach the most reinforced panels.
            var scored = new List<(Vector2Int cell, int score, float d)>();
            foreach (var c in free)
            {
                int n = 0;
                foreach (var p in map.Panels)
                    if (p.reinforced && p.cells.Any(q => Dist(q.x, q.y, c.x, c.y) <= ElectrifyReach)) n++;
                if (n > 0) scored.Add((c, n, DistToHostage(c.x, c.y)));
            }
            foreach (var s in PickApart(scored, 2, ElectricSpacing))
            {
                ctx.Electrics.Add(new ElectricField { x = s.x, y = s.y, r = FieldRadius, discovered = false });
                if (volt != null) volt.Charges--;
                report.electrics.Add(s);
                for (int i = 0; i < map.Panels.Count; i++)
                {
                    var p = map.Panels[i];
                    if (!p.reinforced) continue;
                    if (p.cells.Any(q => Dist(q.x, q.y, s.x, s.y) <= ElectrifyReach)) map.SetPanelElectrified(p.id, true);
                }
            }

            // 3) two cameras where they see the most indoor floor, near the hostage.
            scored.Clear();
            if (vision != null)
                foreach (var c in free)
                {
                    float dh = DistToHostage(c.x, c.y);
                    if (dh > CameraMaxHostageDistance) continue;
                    int n = IndoorCellsSeen(map, vision, c);
                    if (n > 0) scored.Add((c, n, dh));
                }
            foreach (var s in PickApart(scored, 2, CameraSpacing))
            {
                ctx.Cameras.Add(new SurveillanceCamera { x = s.x, y = s.y, alive = true, discovered = false, placed = true });
                if (argus != null) argus.Charges--;
                report.cameras.Add(s);
            }

            // 4) one mirror wall: the nearest Breakable panel with floor on both sides (it keeps its reinforcement, if any —
            //    MirrorAbility).
            foreach (var p0 in cand)
            {
                if (!FloorBothSides(map, p0)) continue;
                map.SetPanelMirror(p0.id, true);
                if (obs != null) obs.Charges--;
                report.mirror = new Vector2Int(Mathf.RoundToInt(p0.centerX), Mathf.RoundToInt(p0.centerY));
                break;
            }

            // bumpMap(): opacity grids / fog / polygons rebuild.
            if (vision != null) vision.MapVersion++;
            map.BumpMap();

            // 5) the defenders spread out to ambush posts, or stay (DefenseDispersal decides from the map).
            DefenseDispersal.Apply(map, vision, ctx, units, hostage, report);
            return report;
        }

        static float Dist(float ax, float ay, float bx, float by) => Mathf.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        /// <summary>Best first (score, then nearest the hostage, then scan order), each at least `spacing` from the ones taken.</summary>
        static List<Vector2Int> PickApart(List<(Vector2Int cell, int score, float d)> scored, int count, float spacing)
        {
            var picked = new List<Vector2Int>();
            foreach (var s in scored.OrderByDescending(s => s.score).ThenBy(s => s.d))
            {
                if (picked.Count >= count) break;
                if (picked.Any(p => Dist(p.x, p.y, s.cell.x, s.cell.y) < spacing)) continue;
                picked.Add(s.cell);
            }
            return picked;
        }

        /// <summary>Indoor floor cells nobody stands on and no crate fills — scan order.</summary>
        static List<Vector2Int> FreeIndoorCells(MapGrid map, IReadOnlyList<SimUnit> units, SimHostage hostage)
        {
            var data = MapLibrary.Current;
            var list = new List<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < MapGrid.Width; x++)
                {
                    if (data.TerrainAt(x, y) != MapTerrain.In || map.TileAt(x, y) != TileType.Floor) continue;
                    bool taken = false;
                    if (units != null) foreach (var u in units) if (u.Alive && Mathf.RoundToInt(u.X) == x && Mathf.RoundToInt(u.Y) == y) { taken = true; break; }
                    if (hostage != null && Mathf.RoundToInt(hostage.X) == x && Mathf.RoundToInt(hostage.Y) == y) taken = true;
                    if (!taken) list.Add(new Vector2Int(x, y));
                }
            return list;
        }

        /// <summary>circleCells(x,y,3.2,'def'): the indoor cells within a camera's radius it has a line to (its own cell included).</summary>
        static int IndoorCellsSeen(MapGrid map, VisionSystem vision, Vector2Int cam)
        {
            var data = MapLibrary.Current;
            int n = 0, r = Mathf.CeilToInt(CameraRadius);
            for (int y = cam.y - r; y <= cam.y + r; y++)
                for (int x = cam.x - r; x <= cam.x + r; x++)
                {
                    if (x < 0 || y < 0 || x >= MapGrid.Width || y >= map.Height) continue;
                    if (data.TerrainAt(x, y) != MapTerrain.In) continue;
                    if (Dist(x, y, cam.x, cam.y) > CameraRadius) continue;
                    if (vision.LineOfSight(cam.x, cam.y, x, y, Blindside.Data.OperatorTeam.Defense, exceptCell: y * MapGrid.Width + x)) n++;
                }
            return n;
        }

        static bool FloorLike(MapGrid map, int x, int y)
        {
            var t = map.TileAt(x, y);
            return t == TileType.Floor || t == TileType.Out || t == TileType.Extract || t == TileType.Rubble;
        }

        /// <summary>A wall panel whose two faces both border floor: a 2-cell panel along its run, a 1-cell panel along either axis.</summary>
        static bool FloorBothSides(MapGrid map, WallPanel p)
        {
            bool? horizontal = p.cells.Count > 1 ? p.cells[1].y == p.cells[0].y : (bool?)null;
            foreach (var c in p.cells)
            {
                bool acrossY = FloorLike(map, c.x, c.y - 1) && FloorLike(map, c.x, c.y + 1);   // a horizontal panel's faces
                bool acrossX = FloorLike(map, c.x - 1, c.y) && FloorLike(map, c.x + 1, c.y);   // a vertical panel's faces
                if (horizontal == true ? acrossY : horizontal == false ? acrossX : (acrossY || acrossX)) return true;
            }
            return false;
        }

        static SimUnit Find(IReadOnlyList<SimUnit> units, string key)
        {
            if (units == null) return null;
            foreach (var u in units) if (u.Team == Blindside.Data.OperatorTeam.Defense && u.Id == key) return u;
            return null;
        }
    }
}
