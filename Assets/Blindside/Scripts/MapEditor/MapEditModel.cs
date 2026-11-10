// The in-game map editor's data side: the MapData being edited, the drawing tools' rules, the panel layout and the undo / redo
// history. No rendering and no input here — MapCellView draws it, MapEditorScreen drives it — so it can be checked on its own.
//
// A cell is (terrain, crate). Tools (the palette's hotkeys):
//   1 outdoor floor · 2 indoor floor   → set the terrain, a crate on it stays
//   3 Solid · 4 Breakable · 5 Reinforced wall → set the terrain, any crate goes (a crate never stands on a wall)
//   6 crate                            → puts a crate on the cell; on a wall cell the wall is replaced by floor first
//   0 erase                            → crate → removed (floor stays) · wall → floor · indoor floor → outdoor floor
// Wherever a wall is replaced by floor (erase, or a crate on a wall) the floor is the kind most of its four neighbours have
// (indoor on a tie), outdoor when it has no floor neighbour. The neighbours are read as they were before the stroke began.
// One stroke (a click, a line, a rectangle) is one undo step; 100 steps are kept.
//
// Objects (ObjectState: hostage, 5 defender starts, ≤8 cameras, ≤8 exfil rectangles) are placed by 7 hostage · 8 defender start ·
// 9 camera · - exfil zone; S only selects. Placement rules (CheckHostage / CheckDefender / CheckCamera / CheckExfil return the
// reason a cell is refused, or null): hostage, defender starts and cameras need a floor cell (no wall, no crate); hostage and
// defender starts indoor floor only; an exfil zone outdoor floor only, no crate; nothing overlaps anything else. Drawing terrain
// over an object that would no longer be valid there (a wall, a crate, the wrong floor kind) deletes that object — in the same undo
// step as the stroke, so undo brings both back. Every object change is one undo step too.
using System;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Map;

namespace Blindside.MapEditor
{
    public enum EditTool
    {
        Erase = 0, OutFloor = 1, InFloor = 2, SolidWall = 3, BreakableWall = 4, ReinforcedWall = 5, Crate = 6,
        Hostage = 7, Defender = 8, Camera = 9, Exfil = 10, Select = 11, Terrorist = 12, Captive = 13, Gadget = 14,
    }

    public struct CellChange
    {
        public int index;
        public MapTerrain oldTerrain, newTerrain;
        public bool oldCrate, newCrate;
    }

    public sealed class MapEditModel
    {
        public const int MaxUndoSteps = 100;
        public const int MaxCameras = 8;

        /// <summary>One undo step: the terrain cells it changed, and the objects before / after (the stroke may have deleted some).</summary>
        sealed class Step
        {
            public CellChange[] cells;
            public ObjectState before, after;
            public bool objectsChanged;
        }

        public MapData Data { get; }
        /// <summary>The objects being placed (may be incomplete — see MapValidator).</summary>
        public ObjectState Objects { get; private set; }
        /// <summary>Counts up on every change of terrain, crates or objects (apply / undo / redo) — lets panels refresh only when needed.</summary>
        public int Version { get; private set; }
        /// <summary>Raised after every change of the objects (placing, moving, deleting, a stroke that deleted one, undo / redo).</summary>
        public event Action ObjectsChanged;
        public int Width => Data.width;
        public int Height => Data.height;

        /// <summary>The panels a match would cut the current walls into (WallPanelSplit). Rebuilt when a wall cell changes.</summary>
        public IReadOnlyList<PanelRun> Panels { get; private set; }

        /// <summary>Raised after every apply / undo / redo with the indices (y*width+x) whose terrain or crate changed.</summary>
        public event Action<IReadOnlyList<int>> CellsChanged;
        /// <summary>Raised when the panel layout was rebuilt (a wall cell was added, removed or changed kind).</summary>
        public event Action PanelsChanged;
        public event Action HistoryChanged;

        readonly List<Step> undo = new List<Step>();
        readonly List<Step> redo = new List<Step>();
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public int UndoDepth => undo.Count;
        /// <summary>How many strokes have been applied since the editor opened (undone ones don't count).</summary>
        public int EditCount => undo.Count;

        /// <summary>`objectsFromData`: start with the objects the MapData carries (a cloned map); false starts with none (a new map).
        /// Objects that aren't valid where they stand are dropped at once.</summary>
        public MapEditModel(MapData data, bool objectsFromData = true)
        {
            Data = data;
            Objects = objectsFromData ? ObjectsOf(data) : new ObjectState();
            RecomputePanels();
            PruneInvalid();
        }

        /// <summary>A map loaded from a file: its objects are taken as they are (the file was checked with FindObjectProblem first).</summary>
        public MapEditModel(MapData data, ObjectState objects)
        {
            Data = data;
            Objects = objects;
            RecomputePanels();
        }

        /// <summary>Why the objects can't stand as they are (a hand-edited or damaged file), or null: hostage and defender starts need free
        /// indoor floor, cameras a free floor cell, exfil zones free outdoor cells in the map; at most 8 cameras / zones; nothing overlaps.</summary>
        public string FindObjectProblem()
        {
            var o = Objects;
            if (o.cameras.Count > MaxCameras) return $"監視カメラが{o.cameras.Count}個 (最大{MaxCameras}個)";
            if (o.exfils.Count > MapData.MaxExfils) return $"救出地点が{o.exfils.Count}か所 (最大{MapData.MaxExfils}か所)";
            var used = new HashSet<Vector2Int>();
            string Place(string what, Vector2Int c, bool indoor)
            {
                if (!InBounds(c.x, c.y)) return $"{what} ({c.x}, {c.y})がマップの外にある";
                if (MapData.IsWall(TerrainAt(c.x, c.y))) return $"{what} ({c.x}, {c.y})が壁の上にある";
                if (CrateAt(c.x, c.y)) return $"{what} ({c.x}, {c.y})が木箱の上にある";
                if (indoor && TerrainAt(c.x, c.y) != MapTerrain.In) return $"{what} ({c.x}, {c.y})が屋内の床ではないマスにある";
                if (!used.Add(c)) return $"{what} ({c.x}, {c.y})が他のオブジェクトと重なっている";
                return null;
            }
            string why;
            if (o.hostage.HasValue && (why = Place("人質", o.hostage.Value, true)) != null) return why;
            for (int i = 0; i < o.extraHostages.Count; i++)
                if ((why = Place($"人質{i + 2}", o.extraHostages[i], true)) != null) return why;
            for (int i = 0; i < o.gadgets.Count; i++)
                if ((why = Place($"{MapData.GadgetName(o.gadgets[i].kind)}{i + 1}", o.gadgets[i].cell, false)) != null) return why;
            for (int i = 0; i < ObjectState.DefenderSlots; i++)
                if (o.defPlaced[i] && (why = Place($"防衛開始位置{i + 1}番", o.defCell[i], true)) != null) return why;
            for (int i = 0; i < o.cameras.Count; i++)
                if ((why = Place($"監視カメラ{i + 1}番", o.cameras[i], false)) != null) return why;
            for (int i = 0; i < o.exfils.Count; i++)
            {
                var e = o.exfils[i];
                if (e.x0 > e.x1 || e.y0 > e.y1 || !InBounds(e.x0, e.y0) || !InBounds(e.x1, e.y1)) return $"救出地点{ExfilLabel(i)}がマップの外にあるか、座標が反転している";
                for (int y = e.y0; y <= e.y1; y++)
                    for (int x = e.x0; x <= e.x1; x++)
                    {
                        if (TerrainAt(x, y) != MapTerrain.Out || CrateAt(x, y)) return $"救出地点{ExfilLabel(i)} ({x}, {y})が屋外の空いた床ではない";
                        if (!used.Add(new Vector2Int(x, y))) return $"救出地点{ExfilLabel(i)} ({x}, {y})が他のオブジェクトと重なっている";
                    }
            }
            return null;
        }

        /// <summary>The MapData a match reads, built from this map (terrain, crates and all objects; the editor's own data is not touched).
        /// Only for a map without required errors. Anchors the editor doesn't author are derived: five attacker drop cells picked
        /// among the outdoor cells around the building, and the building's middle; no house intro.</summary>
        static Vector2Int FirstDefenseCell(ObjectState o)
        {
            for (int i = 0; i < ObjectState.DefenderSlots; i++) if (o.defPlaced[i]) return o.defCell[i];
            return o.terrorists.Count > 0 ? o.terrorists[0].cell : Vector2Int.zero;
        }

        public MapData BuildGameData()
        {
            var o = Objects;
            bool wipeOnly = o.winRule == MapData.WinWipe;
            if ((!o.hostage.HasValue && o.captives.Count == 0 && !wipeOnly) || o.DefenderCount + o.terrorists.Count == 0 || (o.exfils.Count == 0 && !wipeOnly))
                throw new InvalidOperationException("the map is incomplete (a hostage or a captive, a defender or terrorist and an exfil zone are required)");
            var d = Data.Clone();
            d.name = Data.name; d.version = 1; d.useHouseIntro = false;
            // no civilian: `hostage` is still where the defense centres itself — the first captive
            d.noHostage = !o.hostage.HasValue;
            // (nobody held — a wipe-out map: the defense centres on its first defender / terrorist)
            d.hostage = o.hostage ?? (o.captives.Count > 0 ? o.captives[0].cell : FirstDefenseCell(o));
            d.captives = new MapCaptive[o.captives.Count];
            for (int i = 0; i < o.captives.Count; i++) d.captives[i] = new MapCaptive { cell = o.captives[i].cell, operatorKey = o.captives[i].operatorKey };
            d.rescueIntro = o.captives.Count > 0;   // (the rescue intro and rules)
            d.extraHostages = o.hostage.HasValue ? o.extraHostages.ToArray() : new Vector2Int[0];
            d.winRule = o.winRule;
            // only the defenders placed (a map may field only terrorists)
            var starts = new List<MapDefenderStart>();
            for (int i = 0; i < ObjectState.DefenderSlots; i++)
                if (o.defPlaced[i]) starts.Add(new MapDefenderStart { operatorKey = MapData.DefenderKeys[i], cell = o.defCell[i], facing = o.defFacing[i] });
            d.defenderStarts = starts.ToArray();
            d.gadgets = new MapGadget[o.gadgets.Count];
            for (int i = 0; i < o.gadgets.Count; i++) d.gadgets[i] = new MapGadget { cell = o.gadgets[i].cell, kind = o.gadgets[i].kind };
            d.cameras = o.cameras.ToArray();
            d.terrorists = new MapTerrorist[o.terrorists.Count];
            for (int i = 0; i < o.terrorists.Count; i++)
                d.terrorists[i] = new MapTerrorist { cell = o.terrorists[i].cell, facing = o.terrorists[i].facing, weapon = o.terrorists[i].weapon };
            d.exfils = new MapExfil[o.exfils.Count];
            for (int i = 0; i < o.exfils.Count; i++)
            {
                var e = o.exfils[i];
                d.exfils[i] = new MapExfil { name = ExfilLabel(i), x0 = e.x0, y0 = e.y0, x1 = e.x1, y1 = e.y1 };
            }
            d.authored = new MapAuthored();       // the editor places no hand-authored AI values: an edited copy of a map that had them uses the computed ones
            var indoor = d.IndoorBounds();
            d.anchors = new MapAnchors
            {
                attackerDrops = PickAttackerDrops(d),
                facingTarget = new Vector2(indoor.xMin + (indoor.width - 1) / 2f, indoor.yMin + (indoor.height - 1) / 2f),
            };
            return d;
        }

        /// <summary>Five default drop cells (what [START OP] falls back to): free outdoor cells 2-6 cells off the building (else any),
        /// spread out by farthest-point picking from the top-left-most one. Deterministic.</summary>
        public static Vector2Int[] PickAttackerDrops(MapData d)
        {
            var all = new List<Vector2Int>();
            for (int y = 0; y < d.height; y++)
                for (int x = 0; x < d.width; x++)
                    if (d.IsDropCell(x, y) && !d.InExfil(x, y)) all.Add(new Vector2Int(x, y));      // not inside an exfil zone…
            if (all.Count < 5)
                for (int y = 0; y < d.height; y++)
                    for (int x = 0; x < d.width; x++)
                        if (d.IsDropCell(x, y) && d.InExfil(x, y)) all.Add(new Vector2Int(x, y));   // …unless there is no other room
            var built = d.BuiltBounds();
            int Dist(Vector2Int c) => built.width == 0 ? 0 : Mathf.Max(0, Mathf.Max(Mathf.Max(built.xMin - c.x, c.x - (built.xMax - 1)), Mathf.Max(built.yMin - c.y, c.y - (built.yMax - 1))));
            var pool = all.FindAll(c => Dist(c) >= 2 && Dist(c) <= 6);
            if (pool.Count < 5) pool = all;
            var chosen = new List<Vector2Int>();
            if (pool.Count == 0) { for (int i = 0; i < 5; i++) chosen.Add(new Vector2Int(Mathf.Min(d.width - 1, i), 0)); return chosen.ToArray(); }
            pool.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            chosen.Add(pool[0]);
            while (chosen.Count < 5 && chosen.Count < pool.Count)
            {
                Vector2Int best = pool[0]; int bestD = -1;
                foreach (var c in pool)
                {
                    if (chosen.Contains(c)) continue;
                    int near = int.MaxValue;
                    foreach (var q in chosen) near = Mathf.Min(near, (q - c).sqrMagnitude);
                    if (near > bestD) { bestD = near; best = c; }
                }
                chosen.Add(best);
            }
            while (chosen.Count < 5) chosen.Add(chosen[chosen.Count - 1]);
            return chosen.ToArray();
        }

        static ObjectState ObjectsOf(MapData d)
        {
            var o = new ObjectState();
            if (!d.noHostage && d.InBounds(d.hostage.x, d.hostage.y)) o.hostage = d.hostage;
            o.winRule = d.winRule;
            if (o.hostage.HasValue && d.extraHostages != null)
                foreach (var c in d.extraHostages) if (d.InBounds(c.x, c.y) && c != d.hostage && !o.extraHostages.Contains(c)) o.extraHostages.Add(c);
            if (d.captives != null) foreach (var c in d.captives) if (c != null) o.captives.Add(new CaptiveSpot { cell = c.cell, operatorKey = c.operatorKey });
            if (d.defenderStarts != null)
                for (int k = 0; k < d.defenderStarts.Length; k++)
                {
                    var s = d.defenderStarts[k];
                    if (s == null) continue;
                    int i = Array.IndexOf(MapData.DefenderKeys, s.operatorKey);
                    if (i < 0) i = k;   // (an unnamed start: its position in the list)
                    if (i >= ObjectState.DefenderSlots) continue;
                    o.defPlaced[i] = true; o.defCell[i] = s.cell; o.defFacing[i] = s.facing;
                }
            if (d.gadgets != null) foreach (var g in d.gadgets) if (g != null) o.gadgets.Add(new GadgetSpot { cell = g.cell, kind = g.kind });
            if (d.cameras != null) o.cameras.AddRange(d.cameras);
            if (d.exfils != null) foreach (var e in d.exfils) o.exfils.Add(new ExfilRect { x0 = e.x0, y0 = e.y0, x1 = e.x1, y1 = e.y1 });
            if (d.terrorists != null) foreach (var t in d.terrorists) if (t != null) o.terrorists.Add(new TerroristSpot { cell = t.cell, facing = t.facing, weapon = t.weapon });
            return o;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        int Idx(int x, int y) => y * Width + x;
        public MapTerrain TerrainAt(int x, int y) => Data.terrain[Idx(x, y)];
        public bool CrateAt(int x, int y) => Data.crates[Idx(x, y)];

        // ---- shapes ----------------------------------------------------------------------------------------------------

        /// <summary>A straight line from `a` toward `b` locked to the longer axis (horizontal on a tie), both ends included.</summary>
        public static List<Vector2Int> LineCells(Vector2Int a, Vector2Int b)
        {
            var list = new List<Vector2Int>();
            int dx = b.x - a.x, dy = b.y - a.y;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            {
                int step = dx >= 0 ? 1 : -1;
                for (int x = a.x; x != b.x + step; x += step) list.Add(new Vector2Int(x, a.y));
            }
            else
            {
                int step = dy >= 0 ? 1 : -1;
                for (int y = a.y; y != b.y + step; y += step) list.Add(new Vector2Int(a.x, y));
            }
            return list;
        }

        /// <summary>The cells of the rectangle with corners `a` and `b`: all of them, or only its border.</summary>
        public static List<Vector2Int> RectCells(Vector2Int a, Vector2Int b, bool borderOnly)
        {
            int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
            var list = new List<Vector2Int>();
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (!borderOnly || x == x0 || x == x1 || y == y0 || y == y1) list.Add(new Vector2Int(x, y));
            return list;
        }

        /// <summary>Walls are drawn as a rectangle's border only; floors, crates and the eraser fill it.</summary>
        public static bool IsWallTool(EditTool t) => t == EditTool.SolidWall || t == EditTool.BreakableWall || t == EditTool.ReinforcedWall;

        /// <summary>The cells one stroke covers: a single cell, a line (drag), or with `rectangle` (Shift+drag) a rectangle.</summary>
        public static List<Vector2Int> StrokeCells(EditTool tool, Vector2Int start, Vector2Int end, bool rectangle)
        {
            if (start == end) return new List<Vector2Int> { start };
            return rectangle ? RectCells(start, end, IsWallTool(tool)) : LineCells(start, end);
        }

        // ---- applying ------------------------------------------------------------------------------------------------------

        static MapTerrain? WallTerrain(EditTool t)
        {
            switch (t)
            {
                case EditTool.SolidWall: return MapTerrain.Solid;
                case EditTool.BreakableWall: return MapTerrain.Breakable;
                case EditTool.ReinforcedWall: return MapTerrain.Reinforced;
                default: return null;
            }
        }

        /// <summary>The floor a wall cell turns into when the wall goes: the neighbours' floor kind (indoor on a tie), else outdoor.</summary>
        MapTerrain NeighbourFloor(int x, int y, MapTerrain[] before)
        {
            int inside = 0, outside = 0;
            foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
            {
                int nx = x + d.x, ny = y + d.y;
                if (!InBounds(nx, ny)) continue;
                var t = before[Idx(nx, ny)];
                if (t == MapTerrain.In) inside++; else if (t == MapTerrain.Out) outside++;
            }
            return inside == 0 && outside == 0 ? MapTerrain.Out : inside >= outside ? MapTerrain.In : MapTerrain.Out;
        }

        void Result(EditTool tool, int x, int y, MapTerrain[] before, out MapTerrain t, out bool crate)
        {
            int i = Idx(x, y);
            t = before[i]; crate = Data.crates[i];
            switch (tool)
            {
                case EditTool.OutFloor: t = MapTerrain.Out; break;
                case EditTool.InFloor: t = MapTerrain.In; break;
                case EditTool.SolidWall: case EditTool.BreakableWall: case EditTool.ReinforcedWall:
                    t = WallTerrain(tool).Value; crate = false; break;
                case EditTool.Crate:
                    if (MapData.IsWall(t)) t = NeighbourFloor(x, y, before);
                    crate = true; break;
                case EditTool.Erase:
                    if (crate) crate = false;
                    else if (MapData.IsWall(t)) t = NeighbourFloor(x, y, before);
                    else if (t == MapTerrain.In) t = MapTerrain.Out;
                    break;
            }
        }

        /// <summary>Applies `tool` to `cells` as one undo step. Returns the indices that actually changed (empty: nothing to do,
        /// no history entry).</summary>
        public IReadOnlyList<int> Apply(EditTool tool, IEnumerable<Vector2Int> cells)
        {
            if (tool > EditTool.Crate) return Array.Empty<int>();   // object tools don't draw terrain
            var before = (MapTerrain[])Data.terrain.Clone();
            var seen = new HashSet<int>();
            var changes = new List<CellChange>();
            foreach (var c in cells)
            {
                if (!InBounds(c.x, c.y) || !seen.Add(Idx(c.x, c.y))) continue;
                Result(tool, c.x, c.y, before, out var t, out bool crate);
                int i = Idx(c.x, c.y);
                if (t == before[i] && crate == Data.crates[i]) continue;
                changes.Add(new CellChange { index = i, oldTerrain = before[i], newTerrain = t, oldCrate = Data.crates[i], newCrate = crate });
            }
            if (changes.Count == 0) return Array.Empty<int>();

            var arr = changes.ToArray();
            var objBefore = Objects.Clone();
            Write(arr, forward: true);
            PruneInvalid();     // objects that can't stand on the new terrain go with it
            var step = new Step { cells = arr, before = objBefore, after = Objects.Clone() };
            step.objectsChanged = !step.after.SameAs(step.before);
            PushUndo(step);
            Publish(step);
            return Indices(arr);
        }

        void PushUndo(Step step)
        {
            undo.Add(step);
            if (undo.Count > MaxUndoSteps) undo.RemoveAt(0);
            redo.Clear();
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            var step = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Write(step.cells, forward: false);
            if (step.objectsChanged) Objects = step.before.Clone();
            redo.Add(step);
            Publish(step);
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            var step = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Write(step.cells, forward: true);
            if (step.objectsChanged) Objects = step.after.Clone();
            undo.Add(step);
            Publish(step);
            return true;
        }

        void Write(CellChange[] step, bool forward)
        {
            foreach (var c in step)
            {
                Data.terrain[c.index] = forward ? c.newTerrain : c.oldTerrain;
                Data.crates[c.index] = forward ? c.newCrate : c.oldCrate;
            }
        }

        static int[] Indices(CellChange[] step)
        {
            var r = new int[step.Length];
            for (int i = 0; i < step.Length; i++) r[i] = step[i].index;
            return r;
        }

        void Publish(Step step)
        {
            bool walls = false;
            foreach (var c in step.cells) if (MapData.IsWall(c.oldTerrain) || MapData.IsWall(c.newTerrain)) { walls = true; break; }
            if (walls) { RecomputePanels(); }
            Version++;
            if (step.cells.Length > 0) CellsChanged?.Invoke(Indices(step.cells));
            if (walls) PanelsChanged?.Invoke();
            if (step.objectsChanged) ObjectsChanged?.Invoke();
            HistoryChanged?.Invoke();
        }

        // ---- objects ---------------------------------------------------------------------------------------------------------

        public const string MsgOccupied = "すでにオブジェクトがあるマスです (重ねて置けません)";

        static string NoWall => "壁の上には置けません (床のマスのみ可能)";
        static string NoCrate => "木箱の上には置けません (床のマスのみ可能)";

        /// <summary>Why a hostage / defender / camera can't stand on `c` (terrain only), or null.</summary>
        string CheckFloor(Vector2Int c, string indoorOnlyMessage)
        {
            if (!InBounds(c.x, c.y)) return "マップの外には置けません";
            if (MapData.IsWall(TerrainAt(c.x, c.y))) return NoWall;
            if (CrateAt(c.x, c.y)) return NoCrate;
            if (indoorOnlyMessage != null && TerrainAt(c.x, c.y) != MapTerrain.In) return indoorOnlyMessage;
            return null;
        }

        bool Ignored(ObjRef r, ObjRef ignore) => r == ignore;

        /// <summary>The object that occupies `c` (an exfil zone covers all its cells), skipping `ignore` — the object being moved.</summary>
        public ObjRef ObjectAt(Vector2Int c, ObjRef ignore = default)
        {
            var o = Objects;
            var h = new ObjRef(ObjKind.Hostage, 0);
            if (o.hostage.HasValue && o.hostage.Value == c && !Ignored(h, ignore)) return h;
            for (int i = 0; i < o.extraHostages.Count; i++)
                if (o.extraHostages[i] == c && !Ignored(new ObjRef(ObjKind.Hostage, i + 1), ignore)) return new ObjRef(ObjKind.Hostage, i + 1);
            for (int i = 0; i < ObjectState.DefenderSlots; i++)
                if (o.defPlaced[i] && o.defCell[i] == c && !Ignored(new ObjRef(ObjKind.Defender, i), ignore)) return new ObjRef(ObjKind.Defender, i);
            for (int i = 0; i < o.cameras.Count; i++)
                if (o.cameras[i] == c && !Ignored(new ObjRef(ObjKind.Camera, i), ignore)) return new ObjRef(ObjKind.Camera, i);
            for (int i = 0; i < o.terrorists.Count; i++)
                if (o.terrorists[i].cell == c && !Ignored(new ObjRef(ObjKind.Terrorist, i), ignore)) return new ObjRef(ObjKind.Terrorist, i);
            for (int i = 0; i < o.captives.Count; i++)
                if (o.captives[i].cell == c && !Ignored(new ObjRef(ObjKind.Captive, i), ignore)) return new ObjRef(ObjKind.Captive, i);
            for (int i = 0; i < o.gadgets.Count; i++)
                if (o.gadgets[i].cell == c && !Ignored(new ObjRef(ObjKind.Gadget, i), ignore)) return new ObjRef(ObjKind.Gadget, i);
            for (int i = 0; i < o.exfils.Count; i++)
                if (o.exfils[i].Contains(c.x, c.y) && !Ignored(new ObjRef(ObjKind.Exfil, i), ignore)) return new ObjRef(ObjKind.Exfil, i);
            return default;
        }

        /// <summary>The slot the next defender start goes into: the first empty one, or once all five stand, the last (it is moved).</summary>
        public int DefenderTargetSlot()
        {
            for (int i = 0; i < ObjectState.DefenderSlots; i++) if (!Objects.defPlaced[i]) return i;
            return ObjectState.DefenderSlots - 1;
        }

        public string CheckHostage(Vector2Int c)
        {
            var why = CheckFloor(c, "人質は屋内の床にのみ置けます");
            if (why != null) return why;
            if (!ObjectAt(c).IsNone) return MsgOccupied;
            return Objects.HostageCount >= MapData.MaxHostages ? $"人質は最大{MapData.MaxHostages}人まで置けます" : null;
        }

        public string CheckDefender(Vector2Int c)
        {
            var why = CheckFloor(c, "防衛開始位置は屋内の床にのみ置けます");
            if (why != null) return why;
            return ObjectAt(c, new ObjRef(ObjKind.Defender, DefenderTargetSlot())).IsNone ? null : MsgOccupied;
        }

        /// <summary>Who may be held captive: the attack operators in the registry beyond the first five (GHOST, SILENCE…).</summary>
        public static List<string> CaptiveKeys()
        {
            var keys = new List<string>();
            var reg = Blindside.Data.BuildAssetRegistry.Instance;
            if (reg == null) return keys;
            foreach (var op in reg.operators)
                if (op != null && op.team == Blindside.Data.OperatorTeam.Attack && Array.IndexOf(Blindside.Integration.MatchBootstrap.AttackerKeys, op.key) < 0
                    && !keys.Contains(op.key)) keys.Add(op.key);
            return keys;
        }

        public string CheckCaptive(Vector2Int c)
        {
            var why = CheckFloor(c, "捕虜は屋内の床にのみ置けます");
            if (why != null) return why;
            if (!ObjectAt(c).IsNone) return MsgOccupied;
            if (CaptiveKeys().Count == 0) return "捕虜にできる隊員がいません";
            return Objects.captives.Count >= CaptiveKeys().Count ? $"捕虜は最大{CaptiveKeys().Count}人まで置けます (隊員ごとに1人)" : null;
        }

        /// <summary>Holds the next operator not yet held (CaptiveKeys) on `c`. Returns the reason it was refused, or null.</summary>
        public string PlaceCaptive(Vector2Int c)
        {
            var why = CheckCaptive(c);
            if (why != null) return why;
            string key = null;
            foreach (var k in CaptiveKeys()) if (!Objects.captives.Exists(x => x.operatorKey == k)) { key = k; break; }
            if (key == null) return "捕虜にできる隊員が残っていません";
            Commit(o => o.captives.Add(new CaptiveSpot { cell = c, operatorKey = key }));
            return null;
        }

        /// <summary>Captive `i` becomes the next operator not held by another (round). Returns its key, or null.</summary>
        public string CycleCaptiveOperator(int i)
        {
            if (i < 0 || i >= Objects.captives.Count) return null;
            var keys = CaptiveKeys();
            if (keys.Count == 0) return null;
            string next = null;
            Commit(o =>
            {
                var cap = o.captives[i];
                int k = keys.IndexOf(cap.operatorKey);
                for (int step = 1; step <= keys.Count; step++)
                {
                    string cand = keys[(k + step + keys.Count) % keys.Count];
                    bool taken = false;
                    for (int j = 0; j < o.captives.Count; j++) if (j != i && o.captives[j].operatorKey == cand) taken = true;
                    if (!taken) { cap.operatorKey = cand; break; }
                }
                o.captives[i] = cap; next = cap.operatorKey;
            });
            return next;
        }

        public string CheckGadget(Vector2Int c)
        {
            var why = CheckFloor(c, null);
            if (why != null) return why;
            if (!ObjectAt(c).IsNone) return MsgOccupied;
            return Objects.gadgets.Count >= MapData.MaxGadgets ? $"ガジェットは最大{MapData.MaxGadgets}個まで置けます" : null;
        }

        /// <summary>Places a `kind` gadget on `c`. Returns the reason it was refused, or null.</summary>
        public string PlaceGadget(Vector2Int c, string kind)
        {
            var why = CheckGadget(c);
            if (why == null) Commit(o => o.gadgets.Add(new GadgetSpot { cell = c, kind = kind }));
            return why;
        }

        /// <summary>Gadget `i` becomes the next kind (round). Returns it, or null.</summary>
        public string CycleGadgetKind(int i)
        {
            if (i < 0 || i >= Objects.gadgets.Count) return null;
            string next = null;
            Commit(o =>
            {
                var g = o.gadgets[i];
                int k = Array.IndexOf(MapData.GadgetKinds, g.kind);
                g.kind = MapData.GadgetKinds[(k + 1) % MapData.GadgetKinds.Length];
                o.gadgets[i] = g; next = g.kind;
            });
            return next;
        }

        public string CheckTerrorist(Vector2Int c)
        {
            var why = CheckFloor(c, "テロリストは屋内の床にのみ置けます");
            if (why != null) return why;
            if (!ObjectAt(c).IsNone) return MsgOccupied;
            return Objects.terrorists.Count >= MapData.MaxTerrorists ? $"テロリストは最大{MapData.MaxTerrorists}人まで置けます" : null;
        }

        /// <summary>Adds a generic terrorist on `c` facing `facing` with `weapon`. Returns the reason it was refused, or null.</summary>
        public string PlaceTerrorist(Vector2Int c, float facing, string weapon)
        {
            var why = CheckTerrorist(c);
            if (why == null) Commit(o => o.terrorists.Add(new TerroristSpot { cell = c, facing = facing, weapon = string.IsNullOrEmpty(weapon) ? MapData.TerroristWeapons[0] : weapon }));
            return why;
        }

        /// <summary>The next gun (MapData.TerroristWeapons, round) for terrorist `i`. Returns its key, or null.</summary>
        public string CycleTerroristWeapon(int i)
        {
            if (i < 0 || i >= Objects.terrorists.Count) return null;
            string next = null;
            Commit(o =>
            {
                var t = o.terrorists[i];
                int k = System.Array.IndexOf(MapData.TerroristWeapons, t.weapon);
                t.weapon = MapData.TerroristWeapons[(k + 1 + MapData.TerroristWeapons.Length) % MapData.TerroristWeapons.Length];
                o.terrorists[i] = t; next = t.weapon;
            });
            return next;
        }

        public string CheckCamera(Vector2Int c)
        {
            var why = CheckFloor(c, null);
            if (why != null) return why;
            if (!ObjectAt(c).IsNone) return MsgOccupied;
            return Objects.cameras.Count >= MaxCameras ? $"監視カメラは最大{MaxCameras}個まで置けます" : null;
        }

        public string CheckExfil(Vector2Int a, Vector2Int b)
        {
            var r = new ExfilRect(a, b);
            if (!InBounds(r.x0, r.y0) || !InBounds(r.x1, r.y1)) return "マップの外には作れません";
            if (Objects.exfils.Count >= MapData.MaxExfils) return $"救出地点は最大{MapData.MaxExfils}個まで作れます";
            bool indoor = false;
            for (int y = r.y0; y <= r.y1; y++)
                for (int x = r.x0; x <= r.x1; x++)
                {
                    if (MapData.IsWall(TerrainAt(x, y)) || CrateAt(x, y)) return "救出地点は壁・木箱と重ねられません";
                    if (TerrainAt(x, y) == MapTerrain.In) indoor = true;
                }
            if (indoor) return "救出地点は屋外の床にのみ作れます";
            for (int y = r.y0; y <= r.y1; y++)
                for (int x = r.x0; x <= r.x1; x++)
                    if (!ObjectAt(new Vector2Int(x, y)).IsNone) return MsgOccupied;
            return null;
        }

        /// <summary>Runs `edit` on the objects and records it as one undo step (nothing recorded when it changed nothing).</summary>
        void Commit(Action<ObjectState> edit)
        {
            var before = Objects.Clone();
            edit(Objects);
            if (Objects.SameAs(before)) return;
            var step = new Step { cells = Array.Empty<CellChange>(), before = before, after = Objects.Clone(), objectsChanged = true };
            PushUndo(step);
            Publish(step);
        }

        /// <summary>Adds a civilian hostage on `c` (the first, or one more). Returns the reason it was refused, or null.</summary>
        public string PlaceHostage(Vector2Int c)
        {
            var why = CheckHostage(c);
            if (why == null) Commit(o => { if (!o.hostage.HasValue) o.hostage = c; else o.extraHostages.Add(c); });
            return why;
        }

        /// <summary>The next win rule (either → wipe → extract → either), as one undo step.</summary>
        public int CycleWinRule()
        {
            Commit(o => o.winRule = (o.winRule + 1) % 3);
            return Objects.winRule;
        }

        /// <summary>The ObjRef index of the hostage last added (0: the first, i: extraHostages[i - 1]).</summary>
        public int LastHostageIndex => Objects.extraHostages.Count;

        /// <summary>Puts the next defender start on `c` facing `facing` (radians): the first free slot 1→5, else the last one moves.
        /// `slot` is the slot used. Returns the reason it was refused, or null.</summary>
        public string PlaceDefender(Vector2Int c, float facing, out int slot)
        {
            slot = DefenderTargetSlot();
            var why = CheckDefender(c);
            if (why != null) return why;
            int s = slot;
            Commit(o => { o.defPlaced[s] = true; o.defCell[s] = c; o.defFacing[s] = facing; });
            return null;
        }

        public string PlaceCamera(Vector2Int c)
        {
            var why = CheckCamera(c);
            if (why == null) Commit(o => o.cameras.Add(c));
            return why;
        }

        public string AddExfil(Vector2Int a, Vector2Int b)
        {
            var why = CheckExfil(a, b);
            if (why == null) Commit(o => o.exfils.Add(new ExfilRect(a, b)));
            return why;
        }

        /// <summary>Deletes one object (later cameras / exfil zones move up a place; exfil letters follow their order).</summary>
        public bool Delete(ObjRef r)
        {
            bool found = false;
            Commit(o =>
            {
                switch (r.kind)
                {
                    case ObjKind.Hostage:
                        if (r.index <= 0)
                        {
                            // the first goes: the next (if any) becomes the first
                            found = o.hostage.HasValue;
                            o.hostage = null;
                            if (o.extraHostages.Count > 0) { o.hostage = o.extraHostages[0]; o.extraHostages.RemoveAt(0); }
                        }
                        else { found = r.index - 1 < o.extraHostages.Count; if (found) o.extraHostages.RemoveAt(r.index - 1); }
                        break;
                    case ObjKind.Defender: found = r.index >= 0 && r.index < ObjectState.DefenderSlots && o.defPlaced[r.index]; if (found) o.defPlaced[r.index] = false; break;
                    case ObjKind.Camera: found = r.index >= 0 && r.index < o.cameras.Count; if (found) o.cameras.RemoveAt(r.index); break;
                    case ObjKind.Exfil: found = r.index >= 0 && r.index < o.exfils.Count; if (found) o.exfils.RemoveAt(r.index); break;
                    case ObjKind.Terrorist: found = r.index >= 0 && r.index < o.terrorists.Count; if (found) o.terrorists.RemoveAt(r.index); break;
                    case ObjKind.Captive: found = r.index >= 0 && r.index < o.captives.Count; if (found) o.captives.RemoveAt(r.index); break;
                    case ObjKind.Gadget: found = r.index >= 0 && r.index < o.gadgets.Count; if (found) o.gadgets.RemoveAt(r.index); break;
                }
            });
            return found;
        }

        public static string ExfilLabel(int index) => ((char)('A' + index)).ToString();

        /// <summary>Deletes every object that may not stand where it is: the hostage and defenders need free indoor floor, cameras a
        /// free floor cell, an exfil zone only free outdoor cells. Returns whether something went.</summary>
        bool PruneInvalid()
        {
            var o = Objects; bool any = false;
            bool Free(Vector2Int c) => InBounds(c.x, c.y) && !MapData.IsWall(TerrainAt(c.x, c.y)) && !CrateAt(c.x, c.y);
            bool Indoor(Vector2Int c) => Free(c) && TerrainAt(c.x, c.y) == MapTerrain.In;
            any |= o.extraHostages.RemoveAll(c => !Indoor(c)) > 0;
            if (o.hostage.HasValue && !Indoor(o.hostage.Value))
            {
                o.hostage = null; any = true;
                if (o.extraHostages.Count > 0) { o.hostage = o.extraHostages[0]; o.extraHostages.RemoveAt(0); }
            }
            for (int i = 0; i < ObjectState.DefenderSlots; i++)
                if (o.defPlaced[i] && !Indoor(o.defCell[i])) { o.defPlaced[i] = false; any = true; }
            any |= o.cameras.RemoveAll(c => !Free(c)) > 0;
            any |= o.terrorists.RemoveAll(t => !Indoor(t.cell)) > 0;
            any |= o.captives.RemoveAll(t => !Indoor(t.cell)) > 0;
            any |= o.gadgets.RemoveAll(g => CheckFloor(g.cell, null) != null) > 0;
            any |= o.exfils.RemoveAll(e =>
            {
                if (!InBounds(e.x0, e.y0) || !InBounds(e.x1, e.y1)) return true;
                for (int y = e.y0; y <= e.y1; y++)
                    for (int x = e.x0; x <= e.x1; x++)
                        if (TerrainAt(x, y) != MapTerrain.Out || CrateAt(x, y)) return true;
                return false;
            }) > 0;
            return any;
        }

        void RecomputePanels()
        {
            Panels = WallPanelSplit.Split(Width, Height, (x, y) =>
            {
                switch (Data.terrain[y * Width + x])
                {
                    case MapTerrain.Solid: return WallPanelSplit.Solid;
                    case MapTerrain.Breakable: return WallPanelSplit.Breakable;
                    case MapTerrain.Reinforced: return WallPanelSplit.Reinforced;
                    default: return WallPanelSplit.None;
                }
            });
        }
    }
}
