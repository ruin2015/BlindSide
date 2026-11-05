// Checks the map editor's object side without playing: placement rules, the defender numbering, deletion by drawing walls, undo of
// all that, the validation panel's errors / warnings, the wall statistics and the wall-cut camera polygon.
// Menu: Blindside/Map/Verify Map Editor Objects.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Blindside.MapEditor;

namespace Blindside.Map.Editor
{
    public static class MapObjectVerify
    {
        static Vector2Int P(int x, int y) => new Vector2Int(x, y);

        [MenuItem("Blindside/Map/Verify Map Editor Objects")]
        public static void Menu()
        {
            string report = Run(out bool ok);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        /// <summary>A 30×24 blank map: indoor room x5..14 × y5..12, everything else outdoor; no objects.</summary>
        static MapEditModel Room()
        {
            var m = new MapEditModel(MapData.CreateBlank("t", 30, 24), objectsFromData: false);
            m.Apply(EditTool.InFloor, MapEditModel.RectCells(P(5, 5), P(14, 12), false));
            return m;
        }

        static void FillValid(MapEditModel m)
        {
            m.PlaceHostage(P(9, 8));
            for (int i = 0; i < 5; i++) m.PlaceDefender(P(6 + i, 6), 0f, out _);
            m.AddExfil(P(0, 0), P(1, 1));
        }

        public static string Run(out bool ok)
        {
            var sb = new StringBuilder(); int bad = 0;
            void Check(string what, bool pass) { sb.AppendLine((pass ? "PASS  " : "FAIL  ") + what); if (!pass) bad++; }

            // ---- a new map has no objects and all four required errors ----
            var m = Room();
            var r0 = MapValidator.Validate(m);
            Check("new map: no objects at all, and (outdoor plentiful) exactly 3 required errors",
                m.Objects.hostage == null && m.Objects.DefenderCount == 0 && m.Objects.cameras.Count == 0 && m.Objects.exfils.Count == 0
                && r0.issues.Count(i => i.error) == 3 && r0.dropCells >= 5);

            // ---- hostage ----
            Check("hostage: outdoor floor refused", m.PlaceHostage(P(2, 2)) != null && m.Objects.hostage == null);
            Check("hostage: indoor floor accepted", m.PlaceHostage(P(6, 6)) == null && m.Objects.hostage == P(6, 6));
            Check("hostage: clicking again elsewhere moves it (still exactly one)", m.PlaceHostage(P(8, 8)) == null && m.Objects.hostage == P(8, 8));
            m.Apply(EditTool.SolidWall, new[] { P(9, 9) });
            m.Apply(EditTool.Crate, new[] { P(10, 9) });
            Check("hostage: wall and crate cells refused", m.PlaceHostage(P(9, 9)) != null && m.PlaceHostage(P(10, 9)) != null && m.Objects.hostage == P(8, 8));

            // ---- defender starts ----
            var facings = new[] { 0f, .5f, 1f, 1.5f, 2f };
            var slots = new List<int>();
            for (int i = 0; i < 5; i++) { m.PlaceDefender(P(6 + i, 11), facings[i], out int sl); slots.Add(sl); }
            Check("defender starts fill slots 1→5 in click order", slots.SequenceEqual(new[] { 0, 1, 2, 3, 4 }) && m.Objects.DefenderCount == 5);
            Check("defender slots are VOLT, MIASMA, WRAITH, OBSIDIAN, ARGUS", string.Join(",", MapData.DefenderKeys) == "VOLT,MIASMA,WRAITH,OBSIDIAN,ARGUS");
            m.PlaceDefender(P(13, 7), 2.5f, out int moved);
            Check("a 6th click moves the last (5th) one instead of adding", moved == 4 && m.Objects.DefenderCount == 5 && m.Objects.defCell[4] == P(13, 7) && Mathf.Approximately(m.Objects.defFacing[4], 2.5f));
            Check("the facing given with the placement is stored", Mathf.Approximately(m.Objects.defFacing[2], 1f));
            Check("defender: outdoor / wall / crate / occupied cells refused",
                m.PlaceDefender(P(2, 2), 0, out _) != null && m.PlaceDefender(P(9, 9), 0, out _) != null && m.PlaceDefender(P(10, 9), 0, out _) != null
                && m.PlaceDefender(P(6, 11), 0, out _) != null && m.PlaceDefender(P(8, 8), 0, out _) != null);
            m.Delete(new ObjRef(ObjKind.Defender, 1));
            m.PlaceDefender(P(12, 11), 0f, out int refill);
            Check("deleting start 2 then clicking fills slot 2 again (MIASMA), not a new slot", refill == 1 && m.Objects.defCell[1] == P(12, 11) && m.Objects.DefenderCount == 5);

            // ---- cameras ----
            Check("camera: outdoor floor accepted (any floor), wall / crate / occupied refused",
                m.PlaceCamera(P(2, 2)) == null && m.PlaceCamera(P(9, 9)) != null && m.PlaceCamera(P(10, 9)) != null && m.PlaceCamera(P(2, 2)) != null && m.PlaceCamera(P(8, 8)) != null);
            for (int i = 0; i < 7; i++) m.PlaceCamera(P(16 + i, 2));
            Check("camera: 8 fit, the 9th is refused with a count message", m.Objects.cameras.Count == 8 && m.PlaceCamera(P(16, 4)) != null && m.CheckCamera(P(16, 4)).Contains("8"));

            // ---- exfil zones ----
            Check("exfil: a rectangle on outdoor floor is accepted", m.AddExfil(P(0, 14), P(3, 16)) == null && m.Objects.exfils.Count == 1);
            Check("exfil: indoor floor refused", m.AddExfil(P(6, 6), P(6, 6)) != null);
            Check("exfil: wall / crate overlap refused", m.AddExfil(P(9, 9), P(9, 9)) != null && m.AddExfil(P(10, 9), P(10, 9)) != null);
            Check("exfil: overlapping another zone or an object refused", m.AddExfil(P(3, 16), P(5, 17)) != null && m.AddExfil(P(16, 1), P(17, 3)) != null);
            m.AddExfil(P(20, 14), P(21, 14)); m.AddExfil(P(25, 20), P(26, 20));
            Check("exfil letters follow the order: A, B, C", MapEditModel.ExfilLabel(0) == "A" && MapEditModel.ExfilLabel(2) == "C" && m.Objects.exfils.Count == 3);
            m.Delete(new ObjRef(ObjKind.Exfil, 0));
            Check("deleting zone A leaves two zones; the old B now comes first (relabelled A)", m.Objects.exfils.Count == 2 && m.Objects.exfils[0].x0 == 20);
            for (int i = 0; i < 6; i++) m.AddExfil(P(i * 2, 22), P(i * 2, 22));
            Check("exfil: at most 8, the 9th is refused", m.Objects.exfils.Count == 8 && m.AddExfil(P(25, 2), P(25, 2)) != null);

            // ---- drawing a wall over objects deletes them, in the stroke's undo step ----
            var d = Room();
            FillValid(d);
            d.PlaceCamera(P(7, 9));
            int depth = d.UndoDepth;
            var before = d.Objects.Clone();
            d.Apply(EditTool.BreakableWall, MapEditModel.RectCells(P(5, 5), P(14, 12), false));
            Check("a wall stroke over the room deletes the hostage, the defender starts and the camera there (one step)",
                d.Objects.hostage == null && d.Objects.DefenderCount == 0 && d.Objects.cameras.Count == 0 && d.Objects.exfils.Count == 1 && d.UndoDepth == depth + 1);
            d.Undo();
            Check("undo brings the walls and the objects back exactly", d.Objects.SameAs(before) && d.TerrainAt(9, 8) == MapTerrain.In);
            d.Redo();
            Check("redo removes them again", d.Objects.hostage == null && d.Objects.DefenderCount == 0);
            d.Undo();
            d.Apply(EditTool.SolidWall, new[] { P(0, 0) });
            Check("a wall over an exfil zone deletes the whole zone", d.Objects.exfils.Count == 0);
            d.Undo();
            d.Apply(EditTool.Crate, new[] { P(9, 8) });
            Check("a crate over the hostage deletes the hostage", d.Objects.hostage == null);
            d.Undo();
            d.Apply(EditTool.OutFloor, new[] { P(6, 6) });
            Check("outdoor floor under a defender start deletes that start (indoor only)", !d.Objects.defPlaced[0] && d.Objects.DefenderCount == 4);

            // ---- object edits are undo steps too ----
            var u = Room();
            u.PlaceHostage(P(8, 8)); u.PlaceHostage(P(9, 9)); u.PlaceCamera(P(2, 2)); u.AddExfil(P(0, 0), P(1, 1)); u.Delete(new ObjRef(ObjKind.Camera, 0));
            Check("every object edit is one undo step (room stroke + 5 edits = 6 steps)", u.UndoDepth == 1 + 5);
            u.Undo(); Check("undo a delete brings the camera back", u.Objects.cameras.Count == 1);
            u.Undo(); u.Undo(); u.Undo(); Check("undo the move puts the hostage back on its first cell", u.Objects.hostage == P(8, 8) && u.Objects.exfils.Count == 0);
            int v0 = u.Version; u.Redo();
            Check("Version changes on every apply / undo / redo", u.Version > v0);

            // ---- validation ----
            var v = Room();
            var rep = MapValidator.Validate(v);
            Check("blank room: errors = no hostage, 0/5 defender starts, no exfil (3 of the 4)",
                rep.issues.Count(i => i.error) == 3 && rep.issues.Any(i => i.text.Contains("人質")) && rep.issues.Any(i => i.text.Contains("0/5")) && rep.issues.Any(i => i.text.Contains("救出地点がありません")) && !rep.CanTestPlay);
            // few drop cells: a 16×16 map whose outdoor is mostly indoor floor
            var tiny = new MapEditModel(MapData.CreateBlank("t", 16, 16), false);
            tiny.Apply(EditTool.InFloor, MapEditModel.RectCells(P(0, 0), P(15, 15), false));
            tiny.Apply(EditTool.OutFloor, new[] { P(0, 0), P(1, 0), P(2, 0), P(3, 0) });
            var rt = MapValidator.Validate(tiny);
            Check("drop cells < 5 → the 4th required error (4 cells: error; 5 cells: none)",
                rt.dropCells == 4 && rt.issues.Any(i => i.error && i.text.Contains("降下可能")));
            tiny.Apply(EditTool.OutFloor, new[] { P(4, 0) });
            Check("  …and 5 outdoor empty cells clear it", !MapValidator.Validate(tiny).issues.Any(i => i.text.Contains("降下可能")));

            FillValid(v);
            rep = MapValidator.Validate(v);
            Check("a complete map (hostage, 5 starts, 1 exfil, plenty of outdoor): no errors, test play allowed", rep.ErrorCount == 0 && rep.CanTestPlay);
            Check("  …and no warnings while every wall is open", rep.WarningCount == 0);

            // attack impossible: a ring of ordinary walls round the room
            var w = Room(); FillValid(w);
            w.Apply(EditTool.SolidWall, MapEditModel.RectCells(P(4, 4), P(15, 13), true));
            rep = MapValidator.Validate(w);
            Check("ring of ordinary walls: \"攻撃不可能: 通常の壁で完全に塞がれている\" and no escape route warnings",
                rep.issues.Any(i => !i.error && i.text == "攻撃不可能: 通常の壁で完全に塞がれている") && rep.issues.Any(i => i.text.Contains("救出不可能")) && rep.ErrorCount == 0);
            // the same ring made breakable / reinforced: passable for the check
            var bw = Room(); FillValid(bw);
            bw.Apply(EditTool.BreakableWall, MapEditModel.RectCells(P(4, 4), P(15, 13), true));
            Check("same ring of breakable walls: no warning (breakable counts as passable)", MapValidator.Validate(bw).WarningCount == 0);
            var rw = Room(); FillValid(rw);
            rw.Apply(EditTool.ReinforcedWall, MapEditModel.RectCells(P(4, 4), P(15, 13), true));
            Check("same ring of reinforced walls: no warning either", MapValidator.Validate(rw).WarningCount == 0);
            // a crate seals a one-cell doorway
            var cw = Room(); FillValid(cw);
            cw.Apply(EditTool.SolidWall, MapEditModel.RectCells(P(4, 4), P(15, 13), true));
            cw.Apply(EditTool.Erase, new[] { P(10, 4) });     // a doorway in the ring (wall → floor)
            Check("a doorway in the solid ring opens the route (no attack warning)", !MapValidator.Validate(cw).issues.Any(i => i.text.StartsWith("攻撃不可能")));
            cw.Apply(EditTool.Crate, new[] { P(10, 4) });
            Check("a crate in the doorway closes it again (crates block movement)", MapValidator.Validate(cw).issues.Any(i => i.text.StartsWith("攻撃不可能")));
            // exfil unreachable but attack possible: wall the exfil corner in with ordinary walls
            var ew = Room(); FillValid(ew);
            ew.Apply(EditTool.SolidWall, new[] { P(2, 0), P(2, 1), P(0, 2), P(1, 2), P(2, 2) });
            rep = MapValidator.Validate(ew);
            Check("exfil walled in with solid walls but the room reachable: only the escape warning", !rep.issues.Any(i => i.text.StartsWith("攻撃不可能")) && rep.issues.Count(i => i.text.StartsWith("救出不可能")) == 1);

            // camera enclosed
            var cam = Room(); FillValid(cam);
            cam.PlaceCamera(P(20, 18));
            cam.Apply(EditTool.SolidWall, MapEditModel.RectCells(P(19, 17), P(21, 19), true));
            rep = MapValidator.Validate(cam);
            Check("camera with walls all round: warning names it; an open camera gives none",
                rep.issues.Count(i => i.text.Contains("壁に完全に囲まれている")) == 1);
            cam.Apply(EditTool.Erase, new[] { P(20, 17) });
            Check("  …open one wall and the warning goes", !MapValidator.Validate(cam).issues.Any(i => i.text.Contains("囲まれている")));

            // defender far from the hostage
            var far = new MapEditModel(MapData.CreateBlank("t", 40, 24), false);
            far.Apply(EditTool.InFloor, MapEditModel.RectCells(P(0, 2), P(39, 20), false));
            far.PlaceHostage(P(2, 10)); far.AddExfil(P(0, 22), P(1, 23));
            for (int i = 0; i < 5; i++) far.PlaceDefender(P(4 + i * 7, 10), 0, out _);   // x 4, 11, 18, 25, 32: distances 2, 9, 16, 23, 30
            rep = MapValidator.Validate(far);
            var farIssues = rep.issues.Where(i => i.text.Contains("離れている")).ToList();
            Check("defender starts 23 and 30 cells away are warned, 16 is not (limit 20)", farIssues.Count == 2 && farIssues.All(i => i.select.kind == ObjKind.Defender && i.select.index >= 3));
            Check("every warning row knows where to fly the camera (focus = the object's cell)", farIssues.All(i => i.focus.y == 10) && farIssues[0].focus.x == 25);

            // statistics
            var st = Room();
            st.Apply(EditTool.SolidWall, MapEditModel.LineCells(P(0, 0), P(4, 0)));                    // 5 solid
            st.Apply(EditTool.BreakableWall, MapEditModel.LineCells(P(0, 2), P(4, 2)));                // 5 breakable → 3 panels
            st.Apply(EditTool.ReinforcedWall, MapEditModel.LineCells(P(0, 4), P(2, 4)));               // 3 reinforced → 2 panels
            rep = MapValidator.Validate(st);
            Check($"statistics: solid {rep.solidCells}=5, breakable {rep.breakableCells}=5 (panels {rep.breakablePanels}=3), reinforced {rep.reinforcedCells}=3 (panels {rep.reinforcedPanels}=2)",
                rep.solidCells == 5 && rep.breakableCells == 5 && rep.breakablePanels == 3 && rep.reinforcedCells == 3 && rep.reinforcedPanels == 2);

            // ---- sight polygon ----
            var s = Room();
            var open = MapSight.Polygon(s.Data, 20, 18, MapSight.CameraRadius);
            Check("camera polygon in the open: every ray reaches the full 3.2", open.All(p => Mathf.Abs(Vector2.Distance(p, new Vector2(20, 18)) - 3.2f) < .01f));
            s.Apply(EditTool.SolidWall, MapEditModel.LineCells(P(22, 15), P(22, 21)));
            var cut = MapSight.Polygon(s.Data, 20, 18, MapSight.CameraRadius);
            float east = Vector2.Distance(cut[0], new Vector2(20, 18));
            Check($"a wall 2 cells east cuts the east ray to {east:0.00} (between 1.5 and 1.6); the west ray is still 3.2",
                east > 1.45f && east < 1.65f && Mathf.Abs(Vector2.Distance(cut[cut.Count / 2], new Vector2(20, 18)) - 3.2f) < .01f);
            Check("the cut polygon lies inside the radius everywhere and has a point per 2°", cut.Count == 180 && cut.All(p => Vector2.Distance(p, new Vector2(20, 18)) <= 3.25f));
            var vis = MapSight.VisibleCells(s.Data, 20, 18, MapSight.CameraRadius);
            Check("camera cells: the wall cell itself is seen (like any target cell), the cell behind it is not, the west side is seen to x=17", vis.Contains(P(21, 18)) && vis.Contains(P(22, 18)) && !vis.Contains(P(23, 18)) && vis.Contains(P(17, 18)));

            // ---- default map, cloned ----
            var clone = new MapEditModel(MapLibrary.LoadDefault(), true);
            var cr = MapValidator.Validate(clone);
            Check($"cloned default map: hostage 1, starts {clone.Objects.DefenderCount}=5, cameras {clone.Objects.cameras.Count}, exfils {clone.Objects.exfils.Count}=4, no required error",
                clone.Objects.hostage.HasValue && clone.Objects.DefenderCount == 5 && clone.Objects.exfils.Count == 4 && cr.ErrorCount == 0);
            sb.AppendLine("  (cloned default: warnings = " + string.Join(" | ", cr.issues.Where(i => !i.error).Select(i => i.text)) + $"; drop cells {cr.dropCells}; walls {cr.solidCells}/{cr.breakableCells}({cr.breakablePanels})/{cr.reinforcedCells}({cr.reinforcedPanels}))");

            ok = bad == 0;
            sb.AppendLine(ok ? "ALL MAP-EDITOR OBJECT CHECKS PASSED" : $"{bad} CHECK(S) FAILED");
            return sb.ToString();
        }
    }
}
