// Checks the map editor's data side without playing: the drawing shapes and tool rules, undo / redo, and that the shared panel
// splitting (WallPanelSplit, now used by MapGrid too) gives exactly what the original MapGrid.BuildPanels gave — the original
// algorithm is kept here as `Legacy` and compared on the default map, the test maps and a few hundred random wall layouts.
// Menu: Blindside/Map/Verify Map Editor Model.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Blindside.MapEditor;

namespace Blindside.Map.Editor
{
    public static class MapEditVerify
    {
        // ---- the original MapGrid.BuildPanels (before WallPanelSplit), verbatim in behaviour --------------------------------
        struct LegacyPanel { public List<Vector2Int> cells; public bool reinforced; public int wall; }

        static List<LegacyPanel> Legacy(int W, int H, int[] kind)   // kind: 0 none, 1 solid, 2 breakable, 3 reinforced
        {
            bool IsWall(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && kind[y * W + x] >= 2;
            bool Wallish(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && kind[y * W + x] >= 1;
            var panelAt = new int[W * H]; for (int i = 0; i < panelAt.Length; i++) panelAt[i] = -1;
            var result = new List<LegacyPanel>(); int walls = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int ci = y * W + x;
                    if (kind[ci] < 2 || panelAt[ci] >= 0) continue;
                    bool hz = Wallish(x - 1, y) || Wallish(x + 1, y), vt = Wallish(x, y - 1) || Wallish(x, y + 1);
                    bool horiz = hz || !vt; int dx = horiz ? 1 : 0, dy = horiz ? 0 : 1;
                    bool startsReinforced = kind[ci] == 3;
                    var run = new List<Vector2Int>(); int cx = x, cy = y;
                    while (IsWall(cx, cy) && panelAt[cy * W + cx] < 0 && (kind[cy * W + cx] == 3) == startsReinforced) { run.Add(new Vector2Int(cx, cy)); cx += dx; cy += dy; }
                    int wallId = walls++;
                    for (int i = 0; i < run.Count; i += 2)
                    {
                        var cells = run.GetRange(i, Mathf.Min(2, run.Count - i));
                        foreach (var c in cells) panelAt[c.y * W + c.x] = result.Count;
                        result.Add(new LegacyPanel { cells = cells, reinforced = startsReinforced, wall = wallId });
                    }
                }
            return result;
        }

        static int[] KindsOf(MapData d)
        {
            var k = new int[d.width * d.height];
            for (int i = 0; i < k.Length; i++)
                k[i] = d.terrain[i] == MapTerrain.Solid ? 1 : d.terrain[i] == MapTerrain.Breakable ? 2 : d.terrain[i] == MapTerrain.Reinforced ? 3 : 0;
            return k;
        }

        static bool Same(List<LegacyPanel> a, IReadOnlyList<PanelRun> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].reinforced != b[i].reinforced || a[i].wall != b[i].wallId || !a[i].cells.SequenceEqual(b[i].cells)) return false;
            }
            return true;
        }

        [MenuItem("Blindside/Map/Verify Map Editor Model")]
        public static void Menu()
        {
            string report = Run(out bool ok);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        public static string Run(out bool ok)
        {
            var sb = new StringBuilder(); int bad = 0;
            void Check(string what, bool pass) { sb.AppendLine((pass ? "PASS  " : "FAIL  ") + what); if (!pass) bad++; }
            var prev = MapLibrary.Current;
            try
            {
                // ---- panel splitting equals the original algorithm ----
                var maps = new List<MapData> { prev };
                maps.AddRange(TestMaps.All());
                bool same = true; int panels = 0;
                foreach (var m in maps)
                {
                    var legacy = Legacy(m.width, m.height, KindsOf(m));
                    var split = WallPanelSplit.Split(m.width, m.height, (x, y) => KindsOf(m)[y * m.width + x]);
                    MapLibrary.SetCurrent(m);
                    var grid = new MapGrid(m);
                    var gridRuns = grid.Panels.Select(p => new PanelRun { cells = p.cells, reinforced = p.reinforced, wallId = p.wallId }).ToList();
                    bool ok1 = Same(legacy, split) && Same(legacy, gridRuns);
                    // wall groups: same ids, same panels per group
                    ok1 &= grid.Walls.Count == (legacy.Count == 0 ? 0 : legacy.Max(p => p.wall) + 1);
                    same &= ok1; panels += legacy.Count;
                }
                MapLibrary.SetCurrent(prev);
                Check($"panel split == original BuildPanels, and MapGrid built from it, on the default map + 3 test maps ({panels} panels)", same);

                var rng = new System.Random(7);
                int diffs = 0;
                for (int n = 0; n < 300; n++)
                {
                    int w = 6 + rng.Next(10), h = 6 + rng.Next(8);
                    var kind = new int[w * h];
                    for (int i = 0; i < kind.Length; i++) { int r = rng.Next(10); kind[i] = r < 5 ? 0 : r == 5 ? 1 : r < 9 ? 2 : 3; }
                    if (!Same(Legacy(w, h, kind), WallPanelSplit.Split(w, h, (x, y) => kind[y * w + x]))) diffs++;
                }
                Check("panel split == original on 300 random wall layouts (all three kinds mixed)", diffs == 0);

                // ---- shapes ----
                var line = MapEditModel.LineCells(new Vector2Int(2, 3), new Vector2Int(7, 5));
                var lineV = MapEditModel.LineCells(new Vector2Int(2, 3), new Vector2Int(3, 9));
                var lineTie = MapEditModel.LineCells(new Vector2Int(2, 2), new Vector2Int(4, 4));
                var lineBack = MapEditModel.LineCells(new Vector2Int(7, 3), new Vector2Int(2, 3));
                Check("drag line locks to the longer axis (horizontal / vertical / tie → horizontal / backwards)",
                    line.Count == 6 && line.All(c => c.y == 3) && lineV.Count == 7 && lineV.All(c => c.x == 2) && lineTie.Count == 3 && lineTie.All(c => c.y == 2)
                    && lineBack.Count == 6 && lineBack[0].x == 7 && lineBack[5].x == 2);
                var fill = MapEditModel.RectCells(new Vector2Int(4, 3), new Vector2Int(1, 1), false);
                var border = MapEditModel.RectCells(new Vector2Int(4, 3), new Vector2Int(1, 1), true);
                Check("rectangle: filled 4×3 = 12 cells, border only = 10 cells (corners in any order)", fill.Count == 12 && border.Count == 10 && !border.Contains(new Vector2Int(2, 2)));
                Check("Shift+drag: walls give the border, floors / crates / eraser give the fill",
                    MapEditModel.StrokeCells(EditTool.BreakableWall, new Vector2Int(1, 1), new Vector2Int(4, 3), true).Count == 10
                    && MapEditModel.StrokeCells(EditTool.InFloor, new Vector2Int(1, 1), new Vector2Int(4, 3), true).Count == 12
                    && MapEditModel.StrokeCells(EditTool.Crate, new Vector2Int(1, 1), new Vector2Int(4, 3), true).Count == 12
                    && MapEditModel.StrokeCells(EditTool.Erase, new Vector2Int(1, 1), new Vector2Int(4, 3), true).Count == 12
                    && MapEditModel.StrokeCells(EditTool.SolidWall, new Vector2Int(3, 3), new Vector2Int(3, 3), true).Count == 1);

                // ---- tool rules ----
                var m0 = MapData.CreateBlank("t", 16, 16);
                var e = new MapEditModel(m0);
                Vector2Int P(int x, int y) => new Vector2Int(x, y);
                e.Apply(EditTool.InFloor, MapEditModel.RectCells(P(4, 4), P(8, 8), false));
                Check("indoor floor tool fills a rectangle; the outside stays outdoor", e.TerrainAt(5, 5) == MapTerrain.In && e.TerrainAt(2, 2) == MapTerrain.Out);
                e.Apply(EditTool.Crate, new[] { P(6, 6) });
                e.Apply(EditTool.OutFloor, new[] { P(6, 6) });
                Check("floor tool on a crate keeps the crate (only the floor kind changes)", e.CrateAt(6, 6) && e.TerrainAt(6, 6) == MapTerrain.Out);
                e.Apply(EditTool.BreakableWall, new[] { P(6, 6) });
                Check("a wall tool on a crate removes the crate", !e.CrateAt(6, 6) && e.TerrainAt(6, 6) == MapTerrain.Breakable);
                e.Apply(EditTool.Crate, new[] { P(6, 6) });
                Check("crate tool on a wall: the wall becomes floor (its neighbours are indoor → indoor) and the crate stands there",
                    e.TerrainAt(6, 6) == MapTerrain.In && e.CrateAt(6, 6));
                e.Apply(EditTool.Erase, new[] { P(6, 6) });
                Check("erase a crate: the crate goes, the floor stays", !e.CrateAt(6, 6) && e.TerrainAt(6, 6) == MapTerrain.In);
                e.Apply(EditTool.SolidWall, new[] { P(6, 6) });
                e.Apply(EditTool.Erase, new[] { P(6, 6) });
                Check("erase a wall between indoor cells → indoor floor", e.TerrainAt(6, 6) == MapTerrain.In);
                e.Apply(EditTool.ReinforcedWall, new[] { P(12, 12) });
                e.Apply(EditTool.Erase, new[] { P(12, 12) });
                Check("erase a wall with no floor around it (all outdoor) → outdoor floor", e.TerrainAt(12, 12) == MapTerrain.Out);
                // the indoor block is x4..8 × y4..8; column 9 is just outside it.
                e.Apply(EditTool.BreakableWall, new[] { P(9, 6) });   // neighbours: (8,6) indoor, (10,6) (9,5) (9,7) outdoor
                var st = e.TerrainAt(9, 5) == MapTerrain.Out && e.TerrainAt(9, 7) == MapTerrain.Out && e.TerrainAt(10, 6) == MapTerrain.Out && e.TerrainAt(8, 6) == MapTerrain.In;
                e.Apply(EditTool.Erase, new[] { P(9, 6) });
                Check("erase a wall with 1 indoor and 3 outdoor neighbours → outdoor (the majority)", st && e.TerrainAt(9, 6) == MapTerrain.Out);
                e.Apply(EditTool.InFloor, new[] { P(9, 6) });
                e.Apply(EditTool.BreakableWall, new[] { P(9, 7) });
                e.Apply(EditTool.Erase, new[] { P(9, 7) });   // neighbours: (8,7) In, (9,6) In, (9,8) Out, (10,7) Out → 2 : 2 → indoor
                Check("erase a wall with 2 indoor : 2 outdoor neighbours → indoor (a tie goes indoor)", e.TerrainAt(9, 7) == MapTerrain.In);
                e.Apply(EditTool.Erase, new[] { P(5, 5) });
                Check("erase plain indoor floor → outdoor floor", e.TerrainAt(5, 5) == MapTerrain.Out);

                // ---- one stroke = one undo step, undo / redo, history size ----
                var m1 = MapData.CreateBlank("t", 16, 16);
                var u = new MapEditModel(m1);
                var t0 = (MapTerrain[])m1.terrain.Clone(); var c0 = (bool[])m1.crates.Clone();
                u.Apply(EditTool.BreakableWall, MapEditModel.LineCells(P(2, 2), P(9, 2)));
                Check("a drag of 8 cells is one undo step", u.UndoDepth == 1);
                u.Apply(EditTool.InFloor, MapEditModel.RectCells(P(3, 4), P(8, 8), false));
                u.Apply(EditTool.Crate, new[] { P(5, 5) });
                var t3 = (MapTerrain[])m1.terrain.Clone(); var c3 = (bool[])m1.crates.Clone();
                bool undoOk = u.Undo() && u.Undo() && u.Undo() && !u.CanUndo && m1.terrain.SequenceEqual(t0) && m1.crates.SequenceEqual(c0);
                bool redoOk = u.Redo() && u.Redo() && u.Redo() && !u.CanRedo && m1.terrain.SequenceEqual(t3) && m1.crates.SequenceEqual(c3);
                Check("Undo ×3 restores the exact starting map, Redo ×3 the exact edited one", undoOk && redoOk);
                u.Undo();
                bool hadRedo = u.CanRedo;
                u.Apply(EditTool.InFloor, new[] { P(1, 1) });          // a real change: the redo history goes
                bool cleared = !u.CanRedo; int depth = u.UndoDepth;
                u.Apply(EditTool.InFloor, new[] { P(1, 1) });          // the same again changes nothing: no history entry
                Check("a new stroke after Undo clears Redo; a stroke that changes nothing adds no history", hadRedo && cleared && u.UndoDepth == depth);
                var m2 = MapData.CreateBlank("t", 16, 16); var many = new MapEditModel(m2);
                for (int i = 0; i < 130; i++) many.Apply(i % 2 == 0 ? EditTool.SolidWall : EditTool.BreakableWall, new[] { P(i % 14 + 1, i / 14 + 1) });
                Check($"history keeps the last {MapEditModel.MaxUndoSteps} strokes (≥ 50 required): depth {many.UndoDepth}", many.UndoDepth == MapEditModel.MaxUndoSteps && MapEditModel.MaxUndoSteps >= 50);

                // ---- events and panels ----
                var m3 = MapData.CreateBlank("t", 16, 16); var ev = new MapEditModel(m3);
                int cellsRaised = 0, panelsRaised = 0; IReadOnlyList<int> last = null;
                ev.CellsChanged += ix => { cellsRaised++; last = ix; };
                ev.PanelsChanged += () => panelsRaised++;
                ev.Apply(EditTool.InFloor, new[] { P(3, 3), P(4, 3) });
                Check("a floor stroke raises CellsChanged with exactly the changed cells and no PanelsChanged", cellsRaised == 1 && last.Count == 2 && panelsRaised == 0);
                ev.Apply(EditTool.BreakableWall, MapEditModel.LineCells(P(2, 8), P(6, 8)));
                Check("a wall line of 5 cuts into 3 panels (2+2+1) and raises PanelsChanged", ev.Panels.Count == 3 && ev.Panels[2].cells.Count == 1 && panelsRaised == 1);
                ev.Apply(EditTool.ReinforcedWall, new[] { P(4, 8) });
                Check("changing the middle cell to Reinforced cuts the run where the kind changes (panels: {2,3} · {4} reinforced · {5,6})",
                    ev.Panels.Count == 3 && !ev.Panels[0].reinforced && ev.Panels[1].reinforced && ev.Panels[1].cells.Count == 1 && ev.Panels[2].cells.Count == 2);
                ev.Undo();
                Check("undoing it joins the run back (3 panels 2+2+1) and raises PanelsChanged again", ev.Panels.Count == 3 && ev.Panels[1].cells.Count == 2 && !ev.Panels.Any(p => p.reinforced) && panelsRaised == 3);
                Check("edits change only the model's own map: MapLibrary.Current is the default map still", ReferenceEquals(MapLibrary.Current, prev));
            }
            finally { MapLibrary.SetCurrent(prev); }
            ok = bad == 0;
            sb.AppendLine(ok ? "ALL MAP-EDITOR MODEL CHECKS PASSED" : $"{bad} CHECK(S) FAILED");
            return sb.ToString();
        }
    }
}
