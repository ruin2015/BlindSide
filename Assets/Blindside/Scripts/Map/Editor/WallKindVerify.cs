// Checks the three wall kinds (Solid / Breakable / Reinforced) on a throw-away map built in code — nothing is saved and
// MapLibrary.Current is restored afterwards. Menu: Blindside/Map/Verify Wall Kinds.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Combat;
using Blindside.Data;
using Blindside.Integration;
using Blindside.Vision;

namespace Blindside.Map.Editor
{
    public static class WallKindVerify
    {
        const int W = 14, H = 12;

        sealed class StubUnit : IAbilityUnit
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Facing { get; set; }
            public OperatorTeam Team { get; set; } = OperatorTeam.Attack;
            public bool Alive => true;
            public bool IsHostage => false;
            public int HP { get; set; } = 100;
            public WeaponData Weapon => null;
            public bool ShieldUp => Shield;
            public IUnitView View => null;
            public string Id { get; set; } = "STUB";
            public string AbilityKey { get; set; }
            public int MaxHP => 100;
            public int Mv => 3;
            public int Charges { get; set; } = 5;
            public bool AbilityUsed { get; set; }
            public float MoveLeft { get; set; }
            public bool Stim { get; set; }
            public bool Ghost { get; set; }
            public bool Shield { get; set; }
        }

        // Row 3: Solid (3,3) · Breakable (5,3) · Reinforced (7,3)(8,3)(9,3).
        // Row 5: Breakable (2,5)(3,5) · Reinforced (4,5)(5,5) · Breakable (6,5)  — the kind changes twice inside one line.
        // Rows 8 and 10: long Breakable lines, so the defense preparation has more than 10 candidates.
        static MapData MakeMap()
        {
            var d = new MapData
            {
                name = "wall-kinds-test", width = W, height = H,
                terrain = new MapTerrain[W * H], crates = new bool[W * H], cameras = new Vector2Int[0],
                hostage = new Vector2Int(1, 10),
                exfils = new[] { new MapExfil { name = "A", x0 = 0, y0 = 0, x1 = 0, y1 = 0 } },
                defenderStarts = MapData.DefenderKeys.Select((k, i) => new MapDefenderStart { operatorKey = k, cell = new Vector2Int(1 + i, 1) }).ToArray(),
                anchors = new MapAnchors
                {
                    attackerDrops = Enumerable.Range(0, 5).Select(i => new Vector2Int(1 + i, 0)).ToArray(),
                },
            };
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) d.terrain[y * W + x] = y == 0 ? MapTerrain.Out : MapTerrain.In;
            void Set(int x, int y, MapTerrain t) => d.terrain[y * W + x] = t;
            Set(3, 3, MapTerrain.Solid); Set(5, 3, MapTerrain.Breakable);
            for (int x = 7; x <= 9; x++) Set(x, 3, MapTerrain.Reinforced);
            Set(2, 5, MapTerrain.Breakable); Set(3, 5, MapTerrain.Breakable);
            Set(4, 5, MapTerrain.Reinforced); Set(5, 5, MapTerrain.Reinforced);
            Set(6, 5, MapTerrain.Breakable);
            for (int x = 0; x < W; x++) Set(x, 8, MapTerrain.Breakable);
            for (int x = 2; x < W; x++) Set(x, 10, MapTerrain.Breakable);
            return d;
        }

        [MenuItem("Blindside/Map/Verify Wall Kinds")]
        public static void Menu()
        {
            string report = Run(out bool ok);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        public static string Run(out bool ok)
        {
            var sb = new StringBuilder();
            int bad = 0;
            void Check(string what, bool pass) { sb.AppendLine((pass ? "PASS  " : "FAIL  ") + what); if (!pass) bad++; }

            var prev = MapLibrary.Current;
            try
            {
                var data = MakeMap();
                var problems = data.Validate();
                if (problems.Count > 0) { sb.AppendLine("temp map invalid: " + string.Join("; ", problems)); ok = false; return sb.ToString(); }
                MapGrid Fresh() { MapLibrary.SetCurrent(data); return new MapGrid(data); }

                // ---- panel splitting -----------------------------------------------------------------------------------
                var g = Fresh();
                var pSol = g.PanelAt(3, 3); var pBrk = g.PanelAt(5, 3); var pRe1 = g.PanelAt(7, 3); var pRe2 = g.PanelAt(9, 3);
                Check("Solid (3,3) is TileType.Hard and belongs to no panel", g.TileAt(3, 3) == TileType.Hard && g.PanelIndexAt[3 * W + 3] < 0 && !pSol.HasValue);
                Check("Reinforced (7,3)(8,3)(9,3) → 2 panels of 2+1 cells, both start reinforced",
                    pRe1.HasValue && pRe2.HasValue && pRe1.Value.cells.Count == 2 && pRe2.Value.cells.Count == 1 && pRe1.Value.reinforced && pRe2.Value.reinforced && pRe1.Value.id != pRe2.Value.id);
                Check("Breakable (5,3) is one panel, not reinforced", pBrk.HasValue && pBrk.Value.cells.Count == 1 && !pBrk.Value.reinforced);
                var a = g.PanelAt(2, 5); var b = g.PanelAt(4, 5); var c = g.PanelAt(6, 5);
                Check("row 5 B B R R B: the run is cut where the kind changes → panels {2,3} B, {4,5} R, {6} B",
                    a.HasValue && b.HasValue && c.HasValue
                    && a.Value.cells.Count == 2 && a.Value.cells[1].x == 3 && !a.Value.reinforced
                    && b.Value.cells.Count == 2 && b.Value.cells[0].x == 4 && b.Value.reinforced
                    && c.Value.cells.Count == 1 && !c.Value.reinforced
                    && g.PanelAt(3, 5).Value.id == a.Value.id && g.PanelAt(5, 5).Value.id == b.Value.id
                    && new HashSet<int> { a.Value.wallId, b.Value.wallId, c.Value.wallId }.Count == 3);
                Check("no panel mixes kinds (every panel's cells are all Breakable or all Reinforced in the data)",
                    g.Panels.All(p => p.cells.All(q => (data.TerrainAt(q.x, q.y) == MapTerrain.Reinforced) == p.reinforced)));

                // ---- abilities: which panels light up ------------------------------------------------------------------
                // unit stands on the floor cell just below the wall cell
                bool Valid(MapGrid m, string ab, int wx, int wy, bool electrified = false)
                {
                    var pn = PanelOf(m, wx, wy);   // a launcher hole is still its panel
                    if (!pn.HasValue) return false;
                    if (electrified) m.SetPanelElectrified(pn.Value.id, true);
                    var ctx = new AbilityContext { Map = m, Vision = new VisionSystem(m) };
                    var sys = AbilitySystem.CreateDefault();
                    var u = new StubUnit { AbilityKey = ab, X = wx, Y = wy + 1 };
                    var spec = sys.GetSpec(u, ctx);
                    return spec.ValidPanel(m.Panels[pn.Value.id]);
                }
                WallPanel? PanelOf(MapGrid m, int wx, int wy)
                {
                    int i = m.PanelIndexAt[wy * MapGrid.Width + wx];
                    return i >= 0 ? m.Panels[i] : (WallPanel?)null;
                }
                bool AnyValidOnCell(MapGrid m, string ab, int wx, int wy)
                {
                    var ctx = new AbilityContext { Map = m, Vision = new VisionSystem(m) };
                    var u = new StubUnit { AbilityKey = ab, X = wx, Y = wy + 1 };
                    var spec = AbilitySystem.CreateDefault().GetSpec(u, ctx);
                    foreach (var p in m.Panels) if (p.cells.Any(q => q.x == wx && q.y == wy) && spec.ValidPanel(p)) return true;
                    return false;
                }

                g = Fresh();
                Check("Reinforced: hammer does NOT highlight", !Valid(g, "hammer", 7, 3));
                g = Fresh(); Check("Reinforced: launcher highlights", Valid(g, "launcher", 7, 3));
                g = Fresh(); Check("Reinforced: thermite highlights", Valid(g, "thermite", 7, 3));
                g = Fresh(); Check("Reinforced with current: thermite does NOT highlight", !Valid(g, "thermite", 7, 3, electrified: true));
                g = Fresh(); Check("Reinforced: black mirror highlights", Valid(g, "mirror", 7, 3));

                g = Fresh();
                bool solidAny = false;
                foreach (var ab in new[] { "hammer", "launcher", "thermite", "mirror" }) solidAny |= AnyValidOnCell(g, ab, 3, 3);
                Check("Solid: no ability (hammer / launcher / thermite / mirror) can target it", !solidAny);

                g = Fresh();
                Check("Breakable: hammer, launcher, thermite, mirror all highlight",
                    Valid(g, "hammer", 5, 3) && Valid(Fresh(), "launcher", 5, 3) && Valid(Fresh(), "thermite", 5, 3) && Valid(Fresh(), "mirror", 5, 3));

                // ---- ability effects -----------------------------------------------------------------------------------
                void UseOn(IAbilityUnit u, WallPanel p, AbilityContext ctx, AbilitySystem sys)
                {
                    // Unity's coroutine rules without the waiting: a yielded IEnumerator is run to its end first,
                    // any other yielded value (WaitForSeconds) is simply skipped.
                    var stack = new Stack<IEnumerator>();
                    stack.Push(sys.Use(u, AbilityTarget.FromPanel(p), ctx));
                    while (stack.Count > 0)
                    {
                        if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                        if (stack.Peek().Current is IEnumerator inner) stack.Push(inner);
                    }
                }
                AbilityContext Ctx(MapGrid m, AbilitySystem sys) => new AbilityContext { Map = m, Vision = new VisionSystem(m), System = sys };

                g = Fresh(); var sysL = AbilitySystem.CreateDefault(); var cl = Ctx(g, sysL);
                var pl = g.PanelAt(7, 3).Value; UseOn(new StubUnit { AbilityKey = "launcher", X = 7, Y = 4 }, pl, cl, sysL);
                Check("Reinforced + launcher → hole on both cells, panel state cleared",
                    g.TileAt(7, 3) == TileType.Hole && g.TileAt(8, 3) == TileType.Hole && !g.Panels[pl.id].reinforced);
                // a launcher hole: thermite or a second launcher charge opens it completely (rubble); the hammer can't
                Check("launcher hole: hammer does NOT highlight, thermite and launcher do", !Valid(g, "hammer", 7, 3) && Valid(g, "thermite", 7, 3) && Valid(g, "launcher", 7, 3));
                UseOn(new StubUnit { AbilityKey = "launcher", X = 7, Y = 4 }, PanelOf(g, 7, 3).Value, cl, sysL);
                Check("launcher on its own hole → rubble on both cells", g.TileAt(7, 3) == TileType.Rubble && g.TileAt(8, 3) == TileType.Rubble);
                g = Fresh(); var sysL2 = AbilitySystem.CreateDefault(); var cl2 = Ctx(g, sysL2);
                UseOn(new StubUnit { AbilityKey = "launcher", X = 7, Y = 4 }, PanelOf(g, 7, 3).Value, cl2, sysL2);
                UseOn(new StubUnit { AbilityKey = "thermite", X = 7, Y = 4 }, PanelOf(g, 7, 3).Value, cl2, sysL2);
                Check("thermite on a launcher hole → rubble on both cells", g.TileAt(7, 3) == TileType.Rubble && g.TileAt(8, 3) == TileType.Rubble);
                g = Fresh(); var sysT = AbilitySystem.CreateDefault(); var ct = Ctx(g, sysT);
                var pt = g.PanelAt(7, 3).Value; UseOn(new StubUnit { AbilityKey = "thermite", X = 7, Y = 4 }, pt, ct, sysT);
                Check("Reinforced + thermite → passage (rubble) on both cells", g.TileAt(7, 3) == TileType.Rubble && g.TileAt(8, 3) == TileType.Rubble);
                g = Fresh(); var sysM = AbilitySystem.CreateDefault(); var cm = Ctx(g, sysM);
                var pm = g.PanelAt(7, 3).Value; UseOn(new StubUnit { AbilityKey = "mirror", X = 7, Y = 4 }, pm, cm, sysM);
                Check("Reinforced + black mirror → mirror set, still reinforced", g.Panels[pm.id].mirror && g.Panels[pm.id].reinforced && g.TileAt(7, 3) == TileType.Wall);
                Check("mirror on a reinforced wall: thermite and launcher only (no hammer), stops a shot",
                    !Valid(g, "hammer", 7, 3) && Valid(g, "thermite", 7, 3) && Valid(g, "launcher", 7, 3) && CombatSystem.TraceLine(g, 6, 3, 10, 3).Blocked);
                g = Fresh(); var sysM2 = AbilitySystem.CreateDefault(); var cm2 = Ctx(g, sysM2);
                var pm2 = g.PanelAt(5, 3).Value; UseOn(new StubUnit { AbilityKey = "mirror", X = 5, Y = 4 }, pm2, cm2, sysM2);
                var sMir = CombatSystem.TraceLine(g, 4, 3, 6, 3);
                Check("mirror on a plain wall: hammer opens it too, a shot goes through (1 wall)",
                    g.Panels[pm2.id].mirror && !g.Panels[pm2.id].reinforced && Valid(g, "hammer", 5, 3) && !sMir.Blocked && sMir.Walls == 1);

                // ---- shots and sight -----------------------------------------------------------------------------------
                g = Fresh();
                // (TraceLine skips the shooter's and the target's own cell, so both ends sit on floor cells or on the far wall.)
                var sSol = CombatSystem.TraceLine(g, 1, 3, 4, 3);         // crosses Solid (3,3)
                var sRe = CombatSystem.TraceLine(g, 6, 3, 10, 3);         // crosses Reinforced (7,3)(8,3)(9,3)
                var sBr = CombatSystem.TraceLine(g, 4, 3, 6, 3);          // crosses Breakable (5,3) only
                var sBr2 = CombatSystem.TraceLine(g, 1, 5, 4, 5);         // crosses Breakable (2,5)(3,5) only
                Check("Solid stops a shot", sSol.Blocked);
                Check("Reinforced stops a shot", sRe.Blocked);
                Check("Breakable is shot through (1 wall = 1 overlap, 2 walls = 2)", !sBr.Blocked && sBr.Walls == 1 && !sBr2.Blocked && sBr2.Walls == 2);
                var vis = new VisionSystem(g);
                bool allOpaque = true;
                foreach (var team in new[] { OperatorTeam.Attack, OperatorTeam.Defense })
                    allOpaque &= vis.IsOpaque(3, 3, team) && vis.IsOpaque(5, 3, team) && vis.IsOpaque(7, 3, team);
                Check("Solid, Breakable and Reinforced all block sight (both teams)", allOpaque);
                Check("sight through Breakable/Reinforced/Solid is cut (LineOfSight false for each)",
                    !vis.LineOfSight(5, 2, 5, 4, OperatorTeam.Attack) && !vis.LineOfSight(7, 2, 7, 4, OperatorTeam.Attack) && !vis.LineOfSight(3, 2, 3, 4, OperatorTeam.Attack));

                // ---- defense preparation -------------------------------------------------------------------------------
                g = Fresh();
                int preplaced = g.Panels.Count(p => p.reinforced);
                int breakableBefore = g.Panels.Count(p => !p.reinforced);
                var ctxP = new AbilityContext { Map = g, Vision = new VisionSystem(g) };
                var prep = DefensePrep.Apply(g, ctxP.Vision, ctxP, new List<SimUnit>(), new SimHostage { X = 1, Y = 10 });
                int after = g.Panels.Count(p => p.reinforced);
                // the mirror goes on the nearest Breakable panel, one of the 10 — it keeps that reinforcement.
                Check($"defense prep reinforces its 10 on top of the {preplaced} pre-placed panels (pre-placed are not part of the 10): {preplaced} → {after} of {g.Panels.Count} [{prep}]",
                    preplaced == 3 && breakableBefore >= 12 && prep.reinforced == DefensePrep.ReinforceCount
                    && after == preplaced + DefensePrep.ReinforceCount);

                // ---- default map regression guard ----------------------------------------------------------------------
                MapLibrary.SetCurrent(prev);
                var def = new MapGrid(prev);
                Check($"default map: no panel starts reinforced ({def.Panels.Count} panels, {def.Walls.Count} walls)", def.Panels.All(p => !p.reinforced));
            }
            finally { MapLibrary.SetCurrent(prev); }

            ok = bad == 0;
            sb.AppendLine(ok ? "ALL WALL-KIND CHECKS PASSED" : $"{bad} CHECK(S) FAILED");
            return sb.ToString();
        }
    }
}
