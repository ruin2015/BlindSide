// The editor's live validation: required errors (a map with any can't be test-played), warnings (it can) and the wall statistics.
//
// Errors:   no hostage · fewer than 5 defender starts · no exfil zone · fewer than 5 drop cells (outdoor floor without a crate).
// Warnings: (1) "attack impossible": even if every breakable and reinforced wall could be walked through, no path leads from the
//           outdoor drop cells to the hostage; (2) the same assumption, no path from the hostage to an exfil zone; (3) a camera
//           that sees nothing but its own cell (walls all around it); (4) a defender start more than 20 cells from the hostage.
// A path walks on 4-connected cells and is blocked by ordinary (Solid) walls, crates and the map edge — crates block movement in
// the game (MovementSystem.BlockCell) — while Breakable and Reinforced walls count as open. Distance in (4) is straight-line.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Blindside.Map;

namespace Blindside.MapEditor
{
    public sealed class ValidationIssue
    {
        public bool error;
        public string text;
        public Vector2 focus;        // grid position the camera goes to when the row is clicked
        public ObjRef select;        // the object to select then (None: none)
    }

    public sealed class ValidationReport
    {
        public readonly List<ValidationIssue> issues = new List<ValidationIssue>();
        public int dropCells;
        public int solidCells, breakableCells, reinforcedCells, breakablePanels, reinforcedPanels;
        public int ErrorCount { get { int n = 0; foreach (var i in issues) if (i.error) n++; return n; } }
        public int WarningCount => issues.Count - ErrorCount;
        /// <summary>No required error: the map could be test-played.</summary>
        public bool CanTestPlay => ErrorCount == 0;
    }

    public static class MapValidator
    {
        public const int MinDropCells = 5, RequiredDefenders = 5;
        public const float FarDefenderDistance = 20f;

        public static ValidationReport Validate(MapEditModel m)
        {
            var d = m.Data; var o = m.Objects;
            var r = new ValidationReport();
            var center = d.MapCenter;

            // statistics
            for (int i = 0; i < d.terrain.Length; i++)
            {
                switch (d.terrain[i])
                {
                    case MapTerrain.Solid: r.solidCells++; break;
                    case MapTerrain.Breakable: r.breakableCells++; break;
                    case MapTerrain.Reinforced: r.reinforcedCells++; break;
                }
                if (d.IsDropCell(i % d.width, i / d.width)) r.dropCells++;
            }
            foreach (var p in m.Panels) { if (p.reinforced) r.reinforcedPanels++; else r.breakablePanels++; }

            // required errors
            bool wipeOnly = o.winRule == MapData.WinWipe;
            if (!o.hostage.HasValue && o.captives.Count == 0 && !wipeOnly) r.issues.Add(Err("人質も捕虜もいません (どちらか1人以上必要 · 勝利条件が敵の全員制圧なら不要)", center));
            if (o.winRule == MapData.WinExtract && !o.hostage.HasValue) r.issues.Add(Err("勝利条件「人質全員の救出」には人質が1人以上必要です", center));
            if (o.DefenderCount + o.terrorists.Count == 0) r.issues.Add(Err("防衛側が1人もいません (防衛開始位置かテロリストが1人以上必要)", center));
            if (o.exfils.Count == 0 && !wipeOnly) r.issues.Add(Err("救出地点がありません (1個以上必要 · 勝利条件が敵の全員制圧なら不要)", center));
            if (r.dropCells < MinDropCells) r.issues.Add(Err($"降下可能区域が{r.dropCells}マスです (屋外の空きマスが{MinDropCells}マス以上必要)", center));

            // warnings
            for (int i = 0; i < o.extraHostages.Count; i++)
            {
                var h = o.extraHostages[i]; var hf = new Vector2(h.x, h.y);
                if (r.dropCells > 0 && !AttackerCanReach(d, h))
                    r.issues.Add(new ValidationIssue { text = $"攻撃不可能: 人質{i + 2}が通常の壁で完全に塞がれている", focus = hf, select = new ObjRef(ObjKind.Hostage, i + 1) });
                if (o.exfils.Count > 0 && !HostageCanReachExfil(d, o, h))
                    r.issues.Add(new ValidationIssue { text = $"救出不可能: 人質{i + 2}から救出地点までの経路なし", focus = hf, select = new ObjRef(ObjKind.Hostage, i + 1) });
            }
            if (o.hostage.HasValue)
            {
                var h = o.hostage.Value; var hf = new Vector2(h.x, h.y);
                if (r.dropCells > 0 && !AttackerCanReach(d, h))
                    r.issues.Add(new ValidationIssue { text = "攻撃不可能: 通常の壁で完全に塞がれている", focus = hf, select = new ObjRef(ObjKind.Hostage, 0) });
                if (o.exfils.Count > 0 && !HostageCanReachExfil(d, o))
                    r.issues.Add(new ValidationIssue { text = "救出不可能: 人質から救出地点までの経路なし (通常の壁で完全に塞がれている)", focus = hf, select = new ObjRef(ObjKind.Hostage, 0) });
                for (int i = 0; i < ObjectState.DefenderSlots; i++)
                {
                    if (!o.defPlaced[i]) continue;
                    float dist = Vector2.Distance(new Vector2(o.defCell[i].x, o.defCell[i].y), hf);
                    if (dist > FarDefenderDistance)
                        r.issues.Add(new ValidationIssue
                        {
                            text = $"防衛開始位置{i + 1}番 {MapData.DefenderKeys[i]}が人質から{dist:0.#}マス離れている ({FarDefenderDistance:0}マス超過)",
                            focus = new Vector2(o.defCell[i].x, o.defCell[i].y), select = new ObjRef(ObjKind.Defender, i),
                        });
                }
            }
            for (int i = 0; i < o.cameras.Count; i++)
            {
                var c = o.cameras[i];
                // (a wall cell in reach counts as "seen" in the game, like any target cell — so count only the open cells besides its own)
                if (MapSight.VisibleCells(d, c.x, c.y, MapSight.CameraRadius).Count(v => !MapSight.Opaque(d, v.x, v.y)) <= 1)
                    r.issues.Add(new ValidationIssue { text = $"監視カメラ{i + 1}番 ({c.x}, {c.y})が壁に完全に囲まれている", focus = new Vector2(c.x, c.y), select = new ObjRef(ObjKind.Camera, i) });
            }
            return r;
        }

        static ValidationIssue Err(string text, Vector2 focus) => new ValidationIssue { error = true, text = text, focus = focus };

        // A cell a unit could stand on if every breakable / reinforced wall were open.
        static bool Passable(MapData d, int x, int y) => d.InBounds(x, y) && d.TerrainAt(x, y) != MapTerrain.Solid && !d.CrateAt(x, y);

        static readonly Vector2Int[] Dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        static bool[] Flood(MapData d, IEnumerable<Vector2Int> sources)
        {
            var seen = new bool[d.width * d.height];
            var queue = new Queue<Vector2Int>();
            foreach (var s in sources)
                if (Passable(d, s.x, s.y) && !seen[d.Index(s.x, s.y)]) { seen[d.Index(s.x, s.y)] = true; queue.Enqueue(s); }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var dir in Dirs)
                {
                    int nx = c.x + dir.x, ny = c.y + dir.y;
                    if (!Passable(d, nx, ny) || seen[d.Index(nx, ny)]) continue;
                    seen[d.Index(nx, ny)] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return seen;
        }

        /// <summary>Some outdoor drop cell has a path to `hostage`.</summary>
        public static bool AttackerCanReach(MapData d, Vector2Int hostage)
        {
            var drops = new List<Vector2Int>();
            for (int y = 0; y < d.height; y++)
                for (int x = 0; x < d.width; x++)
                    if (d.IsDropCell(x, y)) drops.Add(new Vector2Int(x, y));
            return Flood(d, drops)[d.Index(hostage.x, hostage.y)];
        }

        /// <summary>The hostage has a path to some cell of some exfil zone.</summary>
        public static bool HostageCanReachExfil(MapData d, ObjectState o) => HostageCanReachExfil(d, o, o.hostage.Value);

        public static bool HostageCanReachExfil(MapData d, ObjectState o, Vector2Int from)
        {
            var seen = Flood(d, new[] { from });
            foreach (var e in o.exfils)
                for (int y = e.y0; y <= e.y1; y++)
                    for (int x = e.x0; x <= e.x1; x++)
                        if (d.InBounds(x, y) && seen[d.Index(x, y)]) return true;
            return false;
        }
    }
}
