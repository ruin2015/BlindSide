// Editor-only: converts the original hard-coded map (the values the game code used before MapData existed) into
// Resources/DefaultMap.json, and verifies — cell by cell, against the original MAP in blindside_source — that the file
// still says exactly the same thing. The legacy constants live only here; the game reads the JSON.
//   Blindside ▸ Map ▸ Build DefaultMap.json   (writes the file, then sets it read-only)
//   Blindside ▸ Map ▸ Verify DefaultMap.json  (logs the report; DefaultMapBuilder.Verify() for code)
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Blindside.Map.Editor
{
    public static class DefaultMapBuilder
    {
        public const string JsonPath = "Assets/Blindside/Resources/DefaultMap.json";
        const string SourceJsPath = "Assets/blindside_source/01_core_data_map_vision.js";

        // ---- the original values, verbatim from the pre-MapData code -------------------------------------------------
        // MapGrid.DefaultRows: the source's MAP with its single 'E' block (x0-2,y0-1) written as '.'.
        static readonly string[] LegacyRows =
        {
            "..........................",
            "........o.........o.......",
            "..........................",
            "......o..........o........",
            "..........................",
            "...HWWWWWHWW,,WWHWWWWWH...",
            "...W,,,,,W,,,,,,W,,,,,W...",
            "...W,c,,,W,,,,,,,,,,,,W...",
            "...W,,,,,,,,o,,,W,,,,,,...",
            "...W,,,,,W,,,o,,W,,,,,W...",
            "...W,,,,,W,,,,,,W,,,c,W...",
            "...HWW,WWH,,,,,,HWW,WWH...",
            "...,,,,,,,,,,,,,,,o,,,W...",
            "...HWW,WWWHWW,WWHWW,WWH...",
            "...W,,,,,,W,,,,,W,,,,,W...",
            "...W,,,,,,W,,h,,W,,,,,W...",
            "...W,c,,,,W,,,,,W,,,,,W...",
            "...HWWWWWWHWWWWWHWW,WWH...",
            "..........................",
            ".....o...............o....",
        };
        // ExfilZone.All (name, x0, x1, y0, y1).
        static readonly (string name, int x0, int x1, int y0, int y1)[] LegacyExfils =
        {
            ("A", 0, 2, 0, 1), ("B", 23, 25, 0, 1), ("C", 23, 25, 18, 19), ("D", 0, 2, 18, 19),
        };
        // BLD = {x0:3,x1:22,y0:5,y1:17} — used only to tell the floor under a crate (inside the building → In).
        const int BldX0 = 3, BldX1 = 22, BldY0 = 5, BldY1 = 17;
        static bool InsideBld(int x, int y) => x >= BldX0 && x <= BldX1 && y >= BldY0 && y <= BldY1;

        // MatchBootstrap: the defenders' (x, y, facing), in DEF_POST order.
        static readonly (string key, int x, int y, float facing)[] LegacyDefenders =
        {
            ("VOLT", 12, 14, -Mathf.PI / 2f), ("MIASMA", 14, 14, -Mathf.PI / 2f), ("WRAITH", 11, 15, Mathf.PI),
            ("OBSIDIAN", 15, 15, 0f), ("ARGUS", 13, 16, Mathf.PI / 2f),
        };
        static readonly int[,] LegacyAtkSpawn = { { 4, 2 }, { 7, 2 }, { 10, 3 }, { 13, 2 }, { 16, 2 } };
        // aiDefPrep (04_gameplay_combat_ai.js:340-342): the two electric fields, the two cameras and the mirror wall's cell.
        static readonly int[,] LegacyElectrics = { { 11, 14 }, { 15, 16 } };
        static readonly int[,] LegacyPrepCameras = { { 8, 12 }, { 19, 15 } };
        static readonly Vector2Int LegacyMirror = new Vector2Int(14, 13);
        // randomAtkSpots (05_ui_input_intro.js:412): n = y<=3 & x>=3, else s = y>=19, else w = x<=1 & y>=4, else e = x>=24 — first match
        // wins, so as inclusive rectangles on the 26x20 grid (x0, y0, x1, y1):
        static readonly int[,] LegacySpawnZones = { { 3, 0, 25, 3 }, { 0, 19, 25, 19 }, { 0, 4, 1, 18 }, { 24, 4, 25, 18 } };
        // (The attack AI's fall-back goal used to be listed here; it is now derived from the terrain — EnemyAI.)
        static readonly Vector2 LegacyFacingTarget = new Vector2(13f, 11f);         // Atan2(11-y, 13-x) in AttackerSpawn / Rappel
        static readonly Vector2 LegacyAttackCam = new Vector2(11f, 6f);             // TitleSelectScreen / IntroCameraRig
        static readonly Vector2 LegacyDefenseCam = new Vector2(13f, 12f);           // TitleSelectScreen
        static readonly Vector2 LegacyHouse = new Vector2(12.5f, 11f);              // IntroCameraRig / HeliDeployRig HX,HZ
        static readonly Vector2 LegacyHeliHover = new Vector2(10.5f, 1.5f);         // HELI_P[3], HeliDeployRig.Begin

        // ---- build -----------------------------------------------------------------------------------------------------
        [MenuItem("Blindside/Map/Build DefaultMap.json")]
        public static void BuildMenu()
        {
            Build();
            AssetDatabase.ImportAsset(JsonPath);
            Debug.Log("[DefaultMapBuilder] wrote " + JsonPath + "\n" + Verify(out _));
        }

        public static MapData Convert()
        {
            const int W = 26, H = 20;
            var d = new MapData
            {
                name = "基本邸宅", version = 1, width = W, height = H, useHouseIntro = true,
                terrain = new MapTerrain[W * H], crates = new bool[W * H],
            };
            var cams = new List<Vector2Int>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    switch (LegacyRows[y][x])
                    {
                        case '.': d.terrain[i] = MapTerrain.Out; break;
                        case ',': d.terrain[i] = MapTerrain.In; break;
                        case 'H': d.terrain[i] = MapTerrain.Solid; break;       // pillar
                        case 'W': d.terrain[i] = MapTerrain.Breakable; break;   // wood wall
                        case 'o': d.terrain[i] = InsideBld(x, y) ? MapTerrain.In : MapTerrain.Out; d.crates[i] = true; break;
                        case 'c': d.terrain[i] = MapTerrain.In; cams.Add(new Vector2Int(x, y)); break;
                        case 'h': d.terrain[i] = MapTerrain.In; d.hostage = new Vector2Int(x, y); break;
                        default: throw new System.InvalidOperationException($"unknown map char '{LegacyRows[y][x]}' at ({x},{y})");
                    }
                }
            d.cameras = cams.ToArray();
            var ex = new List<MapExfil>();
            foreach (var z in LegacyExfils) ex.Add(new MapExfil { name = z.name, x0 = z.x0, y0 = z.y0, x1 = z.x1, y1 = z.y1 });
            d.exfils = ex.ToArray();
            var ds = new List<MapDefenderStart>();
            foreach (var s in LegacyDefenders) ds.Add(new MapDefenderStart { operatorKey = s.key, cell = new Vector2Int(s.x, s.y), facing = s.facing });
            d.defenderStarts = ds.ToArray();
            d.anchors = new MapAnchors
            {
                attackerDrops = Cells(LegacyAtkSpawn), facingTarget = LegacyFacingTarget,
                attackCamStart = LegacyAttackCam, defenseCamStart = LegacyDefenseCam,
                introHouseCenter = LegacyHouse, introHeliHover = LegacyHeliHover,
            };
            d.authored = new MapAuthored
            {
                defensePrep = true, electrics = Cells(LegacyElectrics), prepCameras = Cells(LegacyPrepCameras),
                hasMirror = true, mirror = LegacyMirror, spawnZones = Rects(LegacySpawnZones),
            };
            return d;
        }

        static MapRect[] Rects(int[,] a)
        {
            var r = new MapRect[a.GetLength(0)];
            for (int i = 0; i < r.Length; i++) r[i] = new MapRect { x0 = a[i, 0], y0 = a[i, 1], x1 = a[i, 2], y1 = a[i, 3] };
            return r;
        }

        static Vector2Int[] Cells(int[,] a)
        {
            var r = new Vector2Int[a.GetLength(0)];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector2Int(a[i, 0], a[i, 1]);
            return r;
        }

        public static void Build()
        {
            var data = Convert();
            var problems = data.Validate();
            if (problems.Count > 0) throw new System.InvalidOperationException("converted map is invalid: " + string.Join("; ", problems));
            Directory.CreateDirectory(Path.GetDirectoryName(JsonPath));
            if (File.Exists(JsonPath)) File.SetAttributes(JsonPath, FileAttributes.Normal);
            File.WriteAllText(JsonPath, MapLibrary.ToJson(data), new UTF8Encoding(false));
            File.SetAttributes(JsonPath, FileAttributes.ReadOnly);   // the default map is read-only: rebuild here, never hand-edit
        }

        // ---- verify ----------------------------------------------------------------------------------------------------
        [MenuItem("Blindside/Map/Verify DefaultMap.json")]
        public static void VerifyMenu()
        {
            string report = Verify(out bool ok);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        /// <summary>Compares the file on disk with the original MAP / values. `ok` is true only when every check has zero mismatches.</summary>
        public static string Verify(out bool ok)
        {
            var sb = new StringBuilder();
            int bad = 0;
            void Check(string what, int mismatches, string detail = null)
            {
                sb.AppendLine($"{(mismatches == 0 ? "PASS" : "FAIL")}  {what}" + (mismatches == 0 ? "" : $"  ({mismatches} mismatches{(detail != null ? ": " + detail : "")})"));
                if (mismatches != 0) bad++;
            }

            var data = MapLibrary.FromJson(File.ReadAllText(JsonPath));
            var problems = data.Validate();
            Check("DefaultMap.json parses and passes MapData.Validate()", problems.Count, string.Join("; ", problems));
            if (problems.Count > 0) { ok = false; return sb.ToString(); }
            int W = data.width, H = data.height;
            Check($"size {W}x{H} = 26x20, name \"基本邸宅\", useHouseIntro", (W == 26 ? 0 : 1) + (H == 20 ? 0 : 1) + (data.name == "基本邸宅" ? 0 : 1) + (data.useHouseIntro ? 0 : 1));

            // 1) the original MAP in the JS source vs the transcription this project used (only the 'E' block may differ).
            var js = ReadSourceMap();
            int diff = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char jc = js[y][x], lc = LegacyRows[y][x];
                    bool inA = x <= 2 && y <= 1;
                    if (!(jc == lc || (inA && jc == 'E' && lc == '.'))) diff++;
                }
            Check($"original MAP in {SourceJsPath} ({js.Length} rows) == transcribed rows (E block as '.')", diff);

            // 2) expected tiles from the ORIGINAL string: E→Extract, then the four exfil zones, exactly as the game stamped them.
            var expect = new TileType[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    expect[y * W + x] = LegacyTile(js[y][x]);
            foreach (var z in LegacyExfils)
                for (int y = z.y0; y <= z.y1; y++) for (int x = z.x0; x <= z.x1; x++) expect[y * W + x] = TileType.Extract;

            // 3) what the game builds from the JSON (MapGrid), cell by cell.
            var prev = MapLibrary.Current;
            MapGrid grid;
            try { MapLibrary.SetCurrent(data); grid = new MapGrid(data); ExfilZone.Stamp(grid); }   // same two steps as MapGrid.CreateDefault()
            finally { MapLibrary.SetCurrent(prev); }
            int tileDiff = 0; string first = null;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (grid.TileAt(x, y) != expect[y * W + x]) { tileDiff++; first ??= $"({x},{y}) {grid.TileAt(x, y)} vs {expect[y * W + x]}"; }
            Check($"MapGrid built from JSON == original MAP + 4 exfil zones, all {W * H} cells (tile type)", tileDiff, first);

            // 4) the JSON read directly (no MapGrid): characters rebuilt from terrain / crates / cameras / hostage / exfils.
            int charDiff = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char c;
                    var t = data.TerrainAt(x, y);
                    if (data.InExfil(x, y)) c = 'E';
                    else if (data.CrateAt(x, y)) c = 'o';
                    else if (t == MapTerrain.Solid) c = 'H';
                    else if (t == MapTerrain.Breakable) c = 'W';
                    else if (t == MapTerrain.Out) c = '.';
                    else if (data.hostage.x == x && data.hostage.y == y) c = 'h';
                    else if (System.Array.IndexOf(data.cameras, new Vector2Int(x, y)) >= 0) c = 'c';
                    else c = ',';
                    // the source draws one 'E' block (zone A); zones B-D are this project's additions, on '.' cells of the string.
                    bool legacyZone = false;
                    foreach (var z in LegacyExfils) if (x >= z.x0 && x <= z.x1 && y >= z.y0 && y <= z.y1) legacyZone = true;
                    char want = legacyZone ? 'E' : js[y][x];
                    if (c != want) charDiff++;
                }
            Check("characters rebuilt from the JSON == original MAP strings (E on the four zones)", charDiff);

            // 5) exfil zones (2-G-0): four rectangles.
            int exDiff = data.exfils.Length == LegacyExfils.Length ? 0 : 1;
            for (int i = 0; i < System.Math.Min(data.exfils.Length, LegacyExfils.Length); i++)
            {
                var e = data.exfils[i]; var l = LegacyExfils[i];
                if (e.name != l.name || e.x0 != l.x0 || e.x1 != l.x1 || e.y0 != l.y0 || e.y1 != l.y1) exDiff++;
            }
            Check("exfils: 4 zones A-D with the original rectangles", exDiff);

            // 6) crates and their floor: outside crates on Out, inside crates on In; nothing else carries a crate.
            int crateDiff = 0, crateCount = 0, inCrates = 0, outCrates = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool jsCrate = js[y][x] == 'o';
                    if (jsCrate != data.CrateAt(x, y)) crateDiff++;
                    if (!jsCrate) continue;
                    crateCount++;
                    var want = InsideBld(x, y) ? MapTerrain.In : MapTerrain.Out;
                    if (data.TerrainAt(x, y) != want) crateDiff++;
                    if (want == MapTerrain.In) inCrates++; else outCrates++;
                }
            Check($"crates: {crateCount} ({inCrates} inside on In floor, {outCrates} outside on Out floor) match the original 'o' cells", crateDiff);

            // 7) cameras / hostage in the original scan order.
            var jsCams = new List<Vector2Int>(); Vector2Int jsHost = default;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { if (js[y][x] == 'c') jsCams.Add(new Vector2Int(x, y)); if (js[y][x] == 'h') jsHost = new Vector2Int(x, y); }
            int camDiff = jsCams.Count == data.cameras.Length ? 0 : 1;
            for (int i = 0; i < System.Math.Min(jsCams.Count, data.cameras.Length); i++) if (jsCams[i] != data.cameras[i]) camDiff++;
            Check($"fixed cameras ({data.cameras.Length}) and hostage {data.hostage} == original 'c' / 'h' cells", camDiff + (jsHost == data.hostage ? 0 : 1));

            // 8) defenders / attackers' fall-back points / AI + camera + intro anchors, exact.
            int dd = 0;
            for (int i = 0; i < LegacyDefenders.Length; i++)
            {
                var s = data.defenderStarts[i]; var l = LegacyDefenders[i];
                if (s.operatorKey != l.key || s.cell.x != l.x || s.cell.y != l.y || s.facing != l.facing) dd++;
            }
            Check("defenderStarts (VOLT, MIASMA, WRAITH, OBSIDIAN, ARGUS): cell and facing bit-exact", dd);
            var a = data.anchors; int ad = 0;
            ad += SameCells(a.attackerDrops, LegacyAtkSpawn);
            if (a.facingTarget != LegacyFacingTarget) ad++;
            if (a.attackCamStart != LegacyAttackCam) ad++;
            if (a.defenseCamStart != LegacyDefenseCam) ad++;
            if (a.introHouseCenter != LegacyHouse) ad++;
            if (a.introHeliHover != LegacyHeliHover) ad++;
            Check("anchors: attacker drops, facing target, camera starts, intro house/heli", ad);
            var au = data.authored; int aud = 0;
            if (au == null || !au.defensePrep || !au.hasMirror || !au.HasSpawnZones) aud++;
            else
            {
                aud += SameCells(au.electrics, LegacyElectrics) + SameCells(au.prepCameras, LegacyPrepCameras);
                if (au.mirror != LegacyMirror) aud++;
                if (au.spawnZones.Length != LegacySpawnZones.GetLength(0)) aud++;
                else
                    for (int i = 0; i < au.spawnZones.Length; i++)
                    {
                        var z = au.spawnZones[i];
                        if (z.x0 != LegacySpawnZones[i, 0] || z.y0 != LegacySpawnZones[i, 1] || z.x1 != LegacySpawnZones[i, 2] || z.y1 != LegacySpawnZones[i, 3]) aud++;
                    }
            }
            Check("authored: defense prep (2 electric fields, 2 cameras, mirror cell) and the 4 random-drop bands == aiDefPrep / randomAtkSpots", aud);

            // 9) the derived rules that replace BLD: built-up bounds, drop cells, random-spawn bands.
            var built = data.BuiltBounds();
            Check("derived built-up bounds == BLD {x3..22, y5..17}", built.xMin == BldX0 && built.xMax - 1 == BldX1 && built.yMin == BldY0 && built.yMax - 1 == BldY1 ? 0 : 1, built.ToString());
            int outInBld = 0;
            for (int y = BldY0; y <= BldY1; y++) for (int x = BldX0; x <= BldX1; x++) if (data.TerrainAt(x, y) == MapTerrain.Out) outInBld++;
            Check("no outdoor-floor cell lies inside the old building rectangle", outInBld);
            int maskDiff = 0, dropCells = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var t = grid.TileAt(x, y);
                    bool legacy = !InsideBld(x, y) && (t == TileType.Out || t == TileType.Extract);   // deployMask() / deployValid()
                    bool now = data.IsDropCell(x, y);
                    if (legacy != now) maskDiff++;
                    if (now) dropCells++;
                }
            Check($"drop zone (outdoor, no crate, exfil cells included) == old deployMask ({dropCells} cells)", maskDiff);
            // The random drop's four directions are no longer the old fixed bands (they follow the indoor floor's bounding box);
            // what must hold is that each has room for the squad and none reaches into a room.
            var indoor = data.IndoorBounds();
            int zoneBad = 0, zonesWith5 = 0; var zoneCount = new int[4];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int z = Blindside.Integration.AttackerSpawn.ZoneOf(indoor, x, y);
                    if (z < 0) continue;
                    if (data.TerrainAt(x, y) == MapTerrain.Out && !data.CrateAt(x, y) && !data.InExfil(x, y)) zoneCount[z]++;
                }
            foreach (int n in zoneCount) if (n >= 5) zonesWith5++;
            if (zonesWith5 < 2) zoneBad++;
            Check($"random-spawn directions n/s/w/e (around the indoor floor {indoor}) hold {string.Join("/", zoneCount)} free outdoor cells — at least two have 5+", zoneBad);

            ok = bad == 0;
            sb.AppendLine(ok ? "ALL CHECKS PASSED — the JSON matches the original map cell for cell." : $"{bad} CHECK(S) FAILED");
            return sb.ToString();
        }

        static int SameCells(Vector2Int[] got, int[,] want)
        {
            if (got == null || got.Length != want.GetLength(0)) return 1;
            int d = 0;
            for (int i = 0; i < got.Length; i++) if (got[i].x != want[i, 0] || got[i].y != want[i, 1]) d++;
            return d;
        }

        // MapGrid.ParseTiles' original char table.
        static TileType LegacyTile(char ch)
        {
            switch (ch)
            {
                case ',': case 'c': case 'h': return TileType.Floor;
                case 'W': return TileType.Wall;
                case 'H': return TileType.Hard;
                case 'o': return TileType.Crate;
                case 'E': return TileType.Extract;
                default: return TileType.Out;
            }
        }

        static string[] ReadSourceMap()
        {
            string text = File.ReadAllText(SourceJsPath);
            var m = Regex.Match(text, @"const MAP=\[(.*?)\];", RegexOptions.Singleline);
            if (!m.Success) throw new InvalidDataException("MAP not found in " + SourceJsPath);
            var rows = new List<string>();
            foreach (Match r in Regex.Matches(m.Groups[1].Value, @"'([^']*)'")) rows.Add(r.Groups[1].Value);
            return rows.ToArray();
        }
    }
}
