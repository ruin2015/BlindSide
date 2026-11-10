// The serializable description of a battlefield (JsonUtility, so cells are 1-D arrays indexed y*width+x).
// Everything about the map that used to be a hard-coded number or string in the game code lives here; the game reads
// it through MapLibrary.Current. Grid coordinates as everywhere else: x right, y down the MAP rows (y=0 is the top row).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.Map
{
    /// <summary>What a cell is made of. Crates are a separate layer (<see cref="MapData.crates"/>) on top of Out / In floor.</summary>
    public enum MapTerrain
    {
        Out = 0,        // outdoor floor
        In = 1,         // indoor floor
        Solid = 2,      // ordinary wall (the source's 'H' pillar): blocks shots, sight and movement, indestructible, never a target
        Breakable = 3,  // wall that breaching can open (the source's 'W' wood wall); the defense may reinforce it in preparation
        Reinforced = 4, // pre-placed reinforced wall: starts reinforced, not counted in the defense's 10 reinforcements
    }

    /// <summary>One extraction zone: an inclusive rectangle of cells.</summary>
    [Serializable]
    public class MapExfil
    {
        public string name;          // "A".."D" — shown as "EXFIL A"
        public int x0, y0, x1, y1;   // inclusive bounds
    }

    /// <summary>A defense gadget placed on the map: where, and which (MapData.GadgetKinds).</summary>
    [System.Serializable]
    public class MapGadget
    {
        public Vector2Int cell;
        public string kind;
    }

    /// <summary>A captured operator held on the map: where, and who (an attack OperatorData key outside the first five).</summary>
    [System.Serializable]
    public class MapCaptive
    {
        public Vector2Int cell;
        public string operatorKey;
    }

    /// <summary>A generic terrorist placed beyond the five defenders: where, which way, which gun (MapData.TerroristWeapons).</summary>
    [System.Serializable]
    public class MapTerrorist
    {
        public Vector2Int cell;
        public float facing;
        public string weapon = "AR";
    }

    [Serializable]
    public class MapDefenderStart
    {
        public string operatorKey;   // VOLT, MIASMA, WRAITH, OBSIDIAN, ARGUS (this order)
        public Vector2Int cell;
        public float facing;         // radians, atan2(dy,dx) over grid x/y
    }

    /// <summary>
    /// Map-dependent coordinates that are not cells of the terrain layers: the attackers' default drop points and the
    /// camera / house-intro anchors. They are data so a different map can move them. Everything the AI decides — the
    /// defense's preparation, the attackers' random drop, the attack AI's fall-back goal — is derived from the terrain,
    /// so a map needs none of it; a map without the house intro (useHouseIntro false) doesn't use the camera / intro
    /// anchors either (see <see cref="MapData.IntroCenter"/> and friends).
    /// </summary>
    [Serializable]
    public class MapAnchors
    {
        public Vector2Int[] attackerDrops;   // default drop points, one per attacker (ATK_SPAWN)
        public Vector2 facingTarget;         // the point a freshly dropped attacker faces (the building centre)
        public Vector2 attackCamStart;       // battle camera start, attack side (also where the house intro leaves it)
        public Vector2 defenseCamStart;      // battle camera start, defense side
        public Vector2 introHouseCenter;     // the house the intro's camera orbits and the helicopter approaches
        public Vector2 introHeliHover;       // where the helicopter ends up hovering (the deploy phase starts here)
    }

    /// <summary>An inclusive rectangle of cells.</summary>
    [Serializable]
    public struct MapRect
    {
        public int x0, y0, x1, y1;
        public bool Contains(int x, int y) => x >= x0 && x <= x1 && y >= y0 && y <= y1;
    }

    /// <summary>
    /// Optional hand-placed values that stand in for what the AI otherwise works out from the terrain. Each part is used
    /// as written when present; a map without it (every map the editor makes) gets the computed behaviour. JsonUtility
    /// cannot store a null object, so "present" is a flag / a non-empty list, never a null reference.
    /// </summary>
    [Serializable]
    public class MapAuthored
    {
        public bool defensePrep;             // true: the three lists below replace the computed electric fields, cameras and mirror
        public Vector2Int[] electrics;       // electric fields (VOLT), placed in this order
        public Vector2Int[] prepCameras;     // cameras the defense places (ARGUS), in this order
        public bool hasMirror;
        public Vector2Int mirror;            // the mirror wall (OBSIDIAN): the panel containing this cell
        public MapRect[] spawnZones;         // non-empty: the random attacker drop draws from these rectangles (in this order) instead of the computed directions

        public bool HasSpawnZones => spawnZones != null && spawnZones.Length > 0;
    }

    [Serializable]
    public class MapData
    {
        public string name;
        public int version = 1;
        public int width, height;
        public bool useHouseIntro;
        /// <summary>The rescue intro: the house intro's flight, its hologram showing the captured operators (IntroCameraRig.RescueStyle).
        /// Implies the house intro.</summary>
        public bool rescueIntro;
        /// <summary>Where the captured operators are held (the rescue intro's markers; `hostage` is the first). Empty: only `hostage`.</summary>
        public Vector2Int[] rescueHostages;

        public MapTerrain[] terrain;         // width*height, y*width+x
        public bool[] crates;                // width*height — a crate stands on this cell's floor
        public Vector2Int[] cameras;         // fixed surveillance cameras (defense), in this order
        public Vector2Int hostage;
        public MapDefenderStart[] defenderStarts;   // 5, in DefenderKeys order
        /// <summary>Generic terrorists beyond the five (TerroristKey: no ability), each with its own gun (TerroristWeapons key).</summary>
        public MapTerrorist[] terrorists;
        /// <summary>Captured operators held on this map (a rescue: freed, each joins the squad). Placed in the editor.</summary>
        public MapCaptive[] captives;
        /// <summary>Civilian hostages beyond the first (`hostage`): every one is to be brought out. Older maps: none.</summary>
        public Vector2Int[] extraHostages;
        public const int MaxHostages = 8;
        /// <summary>How the attack wins: WinEither (older maps) — the defense wiped out or every hostage brought out, whichever first;
        /// WinWipe — only by wiping out the defense; WinExtract — only by bringing every hostage out.</summary>
        public int winRule;
        public const int WinEither = 0, WinWipe = 1, WinExtract = 2;

        /// <summary>The defense's gadgets placed in the editor (on top of the ones the defense AI sets up itself).</summary>
        public MapGadget[] gadgets;
        public const string GadgetElectric = "ELECTRIC", GadgetMine = "MINE", GadgetSensor = "SENSOR";
        public static readonly string[] GadgetKinds = { GadgetElectric, GadgetMine, GadgetSensor };
        public const int MaxGadgets = 40;
        public static string GadgetName(string kind) =>
            kind == GadgetMine ? "地雷" : kind == GadgetSensor ? "動体センサー" : "電気装置";

        public static string WinRuleText(int rule) =>
            rule == WinWipe ? "敵の全員制圧" : rule == WinExtract ? "人質全員の救出" : "制圧 または 人質救出";
        /// <summary>No civilian hostage on this map (`hostage` then only marks where the defense centres itself). Older maps: false.</summary>
        public bool noHostage;
        public MapExfil[] exfils;            // 1..8
        public MapAnchors anchors;
        public MapAuthored authored;         // optional: hand-placed defense preparation / attacker drop zones (see MapAuthored)

        public static readonly string[] DefenderKeys = { "VOLT", "MIASMA", "WRAITH", "OBSIDIAN", "ARGUS" };
        /// <summary>The generic terrorist's OperatorData key, and the guns a placed one may carry (WeaponData asset Weapon_{key}).</summary>
        public const string TerroristKey = "TERRORIST";
        public static readonly string[] TerroristWeapons = { "AR", "SMG", "SG", "PST" };
        public const int MaxTerrorists = 40;
        public const int MaxExfils = 8;

        /// <summary>An independent deep copy (through JSON, like a save and load would make).</summary>
        public MapData Clone() => JsonUtility.FromJson<MapData>(JsonUtility.ToJson(this));

        /// <summary>The operators the rescue holds captive, and where: the map's own captives (placed in the editor) when it has any —
        /// else the original operation's GHOST and SILENCE on CaptiveCells.</summary>
        public System.Collections.Generic.List<MapCaptive> RescueRoster()
        {
            var list = new System.Collections.Generic.List<MapCaptive>();
            if (captives != null) foreach (var c in captives) if (c != null && !string.IsNullOrEmpty(c.operatorKey)) list.Add(c);
            if (list.Count > 0) return list;
            var cells = CaptiveCells(LegacyCaptiveKeys.Length);
            for (int i = 0; i < LegacyCaptiveKeys.Length && i < cells.Count; i++) list.Add(new MapCaptive { cell = cells[i], operatorKey = LegacyCaptiveKeys[i] });
            return list;
        }

        /// <summary>Where the civilian hostages start: `hostage` first, then extraHostages — none on a map without a civilian.</summary>
        public System.Collections.Generic.List<Vector2Int> CivilianCells()
        {
            var list = new System.Collections.Generic.List<Vector2Int>();
            if (!HasCivilian) return list;
            list.Add(hostage);
            if (extraHostages != null) foreach (var c in extraHostages) if (!list.Contains(c) && list.Count < MaxHostages) list.Add(c);
            return list;
        }

        /// <summary>Who the original rescue operation (a catalog map without editor captives) holds.</summary>
        public static readonly string[] LegacyCaptiveKeys = { "GHOST", "SILENCE" };

        /// <summary>The map keeps its civilian hostage: every map but a rescue without one (the catalog's rescue, or an editor map
        /// whose only hostages are captives).</summary>
        public bool HasCivilian => !noHostage && !(rescueIntro && (captives == null || captives.Length == 0));

        /// <summary>The rescue map's `count` captive cells: `hostage` first, then rescueHostages; short of that, the indoor floor cell
        /// farthest from the first (a stand-in until the map gives one).</summary>
        public System.Collections.Generic.List<Vector2Int> CaptiveCells(int count)
        {
            var cells = new System.Collections.Generic.List<Vector2Int> { hostage };
            if (rescueHostages != null) foreach (var c in rescueHostages) if (c != hostage && !cells.Contains(c) && cells.Count < count) cells.Add(c);
            while (cells.Count < count && terrain != null)
            {
                Vector2Int best = hostage; float bd = -1f;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int k = y * width + x;
                        if (terrain[k] != MapTerrain.In || (crates != null && crates[k]) || cells.Contains(new Vector2Int(x, y))) continue;
                        float d = float.MaxValue;
                        foreach (var c in cells) d = Mathf.Min(d, (x - c.x) * (x - c.x) + (y - c.y) * (y - c.y));
                        if (d > bd) { bd = d; best = new Vector2Int(x, y); }
                    }
                if (bd < 0f) break;
                cells.Add(best);
            }
            return cells;
        }

        /// <summary>An empty map: all outdoor floor, no walls or crates, the four corner exfil zones and placeholder positions for the
        /// hostage, the defenders and the drop points (all in the middle / top row) that the editor's object step will replace.
        /// Not valid for play yet — the hostage stands on outdoor floor — and never made current.</summary>
        public static MapData CreateBlank(string name, int w, int h)
        {
            var d = new MapData
            {
                name = name, width = w, height = h, useHouseIntro = false,
                terrain = new MapTerrain[w * h], crates = new bool[w * h], cameras = new Vector2Int[0],
                hostage = new Vector2Int(w / 2, h / 2),
                exfils = new[]
                {
                    new MapExfil { name = "A", x0 = 0, y0 = 0, x1 = 1, y1 = 1 },
                    new MapExfil { name = "B", x0 = w - 2, y0 = 0, x1 = w - 1, y1 = 1 },
                    new MapExfil { name = "C", x0 = w - 2, y0 = h - 2, x1 = w - 1, y1 = h - 1 },
                    new MapExfil { name = "D", x0 = 0, y0 = h - 2, x1 = 1, y1 = h - 1 },
                },
                defenderStarts = new MapDefenderStart[DefenderKeys.Length],
                anchors = new MapAnchors
                {
                    attackerDrops = new Vector2Int[5],
                    facingTarget = new Vector2((w - 1) / 2f, (h - 1) / 2f),
                },
            };
            for (int i = 0; i < DefenderKeys.Length; i++)
                d.defenderStarts[i] = new MapDefenderStart { operatorKey = DefenderKeys[i], cell = new Vector2Int(w / 2 - 2 + i, h / 2), facing = 0f };
            for (int i = 0; i < 5; i++) d.anchors.attackerDrops[i] = new Vector2Int(Mathf.Min(w - 1, 3 + i * 2), 0);
            return d;
        }

        public int Index(int x, int y) => y * width + x;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
        public MapTerrain TerrainAt(int x, int y) => terrain[y * width + x];
        public bool CrateAt(int x, int y) => crates[y * width + x];

        public static bool IsWall(MapTerrain t) => t == MapTerrain.Solid || t == MapTerrain.Breakable || t == MapTerrain.Reinforced;

        public bool InExfil(int x, int y)
        {
            foreach (var e in exfils) if (x >= e.x0 && x <= e.x1 && y >= e.y0 && y <= e.y1) return true;
            return false;
        }

        /// <summary>Outdoor floor with no crate on it (exfil cells included) — where an attacker may be dropped.</summary>
        public bool IsDropCell(int x, int y) => InBounds(x, y) && TerrainAt(x, y) == MapTerrain.Out && !CrateAt(x, y);

        /// <summary>The middle of the grid in cell coordinates.</summary>
        public Vector2 MapCenter => new Vector2((width - 1) / 2f, (height - 1) / 2f);

        // Where the intro and the cameras look. The house intro's are authored (anchors); a map without it has no house to
        // aim at, so everything is the middle of the map.
        public Vector2 IntroCenter => useHouseIntro ? anchors.introHouseCenter : MapCenter;
        // the helicopter hovers HeliHoverNudge cells further up the screen than authored, clear of the map's top rooms
        // (presentation only: the drops fly to their own cells)
        public static readonly Vector2 HeliHoverNudge = new Vector2(0f, -3f);
        public Vector2 HeliHover => useHouseIntro ? anchors.introHeliHover + HeliHoverNudge : MapCenter;
        public Vector2 AttackCamStart => useHouseIntro ? anchors.attackCamStart : MapCenter;
        public Vector2 DefenseCamStart => useHouseIntro ? anchors.defenseCamStart : MapCenter;

        /// <summary>The point a freshly dropped attacker faces: the authored building centre, or the middle of the indoor floor.</summary>
        public Vector2 FacingTarget
        {
            get
            {
                if (useHouseIntro) return anchors.facingTarget;
                var b = IndoorBounds();
                return new Vector2(b.xMin + (b.width - 1) / 2f, b.yMin + (b.height - 1) / 2f);
            }
        }

        /// <summary>Bounding box of the indoor floor cells (crates included), inclusive-exclusive like RectInt. No indoor floor →
        /// <see cref="BuiltBounds"/>.</summary>
        public RectInt IndoorBounds()
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (TerrainAt(x, y) != MapTerrain.In) continue;
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
            return x1 < 0 ? BuiltBounds() : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        /// <summary>Bounding box of every cell that is not outdoor floor (the built-up area), inclusive. Empty map → (0,0,-1,-1).</summary>
        public RectInt BuiltBounds()
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (TerrainAt(x, y) == MapTerrain.Out) continue;
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
            return x1 < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        /// <summary>Checks the invariants the game relies on. Returns the problems found (empty when valid).</summary>
        public List<string> Validate()
        {
            var e = new List<string>();
            if (width <= 0 || height <= 0) { e.Add($"size {width}x{height} is not positive"); return e; }
            int n = width * height;
            if (terrain == null || terrain.Length != n) { e.Add($"terrain has {(terrain == null ? 0 : terrain.Length)} cells, expected {n}"); return e; }
            if (crates == null || crates.Length != n) { e.Add($"crates has {(crates == null ? 0 : crates.Length)} cells, expected {n}"); return e; }
            for (int i = 0; i < n; i++)
                if (crates[i] && IsWall(terrain[i])) e.Add($"crate on a wall at ({i % width},{i / width})");

            int minExfils = winRule == WinWipe ? 0 : 1;
            if (exfils == null || exfils.Length < minExfils || exfils.Length > MaxExfils) e.Add($"exfils must have {minExfils}..{MaxExfils} zones");
            else
            {
                var seen = new HashSet<int>();
                foreach (var z in exfils)
                {
                    if (z.x0 > z.x1 || z.y0 > z.y1 || !InBounds(z.x0, z.y0) || !InBounds(z.x1, z.y1)) { e.Add($"exfil {z.name} is outside the map or inverted"); continue; }
                    for (int y = z.y0; y <= z.y1; y++)
                        for (int x = z.x0; x <= z.x1; x++)
                        {
                            if (!seen.Add(y * width + x)) e.Add($"exfil {z.name} overlaps another at ({x},{y})");
                            if (TerrainAt(x, y) != MapTerrain.Out) e.Add($"exfil {z.name} cell ({x},{y}) is not outdoor floor");
                            if (CrateAt(x, y)) e.Add($"exfil {z.name} cell ({x},{y}) has a crate");
                        }
                }
            }

            bool Floor(Vector2Int c) => InBounds(c.x, c.y) && (TerrainAt(c.x, c.y) == MapTerrain.In) && !CrateAt(c.x, c.y);
            if (!Floor(hostage)) e.Add($"hostage {hostage} is not on a free indoor floor cell");
            // (a camera may stand on any free floor cell, outdoor too — the map editor allows it)
            bool FreeFloor(Vector2Int c) => InBounds(c.x, c.y) && !IsWall(TerrainAt(c.x, c.y)) && !CrateAt(c.x, c.y);
            if (cameras != null) foreach (var c in cameras) if (!FreeFloor(c)) e.Add($"camera {c} is not on a free floor cell");
            if (defenderStarts == null || defenderStarts.Length > DefenderKeys.Length) e.Add($"defenderStarts must have 0..{DefenderKeys.Length} entries");
            else
            {
                var keys = new HashSet<string>();
                for (int i = 0; i < defenderStarts.Length; i++)
                {
                    var d = defenderStarts[i];
                    if (d == null) { e.Add($"defenderStarts[{i}] is empty"); continue; }
                    if (System.Array.IndexOf(DefenderKeys, d.operatorKey) < 0 || !keys.Add(d.operatorKey)) e.Add($"defenderStarts[{i}] is {d.operatorKey}, not one of the five (or twice)");
                    if (!InBounds(d.cell.x, d.cell.y)) e.Add($"defender {d.operatorKey} start {d.cell} is outside the map");
                }
            }
            if (gadgets != null)
                foreach (var g in gadgets)
                    if (g == null || System.Array.IndexOf(GadgetKinds, g.kind) < 0 || !FreeFloor(g.cell)) e.Add($"gadget {(g == null ? "null" : g.kind + " " + g.cell)} is unknown or not on a free floor cell");
            if (anchors == null || anchors.attackerDrops == null || anchors.attackerDrops.Length != 5) e.Add("anchors.attackerDrops must have 5 points");
            if (authored != null)
            {
                if (authored.defensePrep)
                {
                    if (authored.electrics != null) foreach (var c in authored.electrics) if (!InBounds(c.x, c.y)) e.Add($"authored electric field {c} is outside the map");
                    if (authored.prepCameras != null) foreach (var c in authored.prepCameras) if (!InBounds(c.x, c.y)) e.Add($"authored camera {c} is outside the map");
                    if (authored.hasMirror && !InBounds(authored.mirror.x, authored.mirror.y)) e.Add($"authored mirror {authored.mirror} is outside the map");
                }
                if (authored.HasSpawnZones)
                    foreach (var z in authored.spawnZones)
                        if (z.x0 > z.x1 || z.y0 > z.y1 || !InBounds(z.x0, z.y0) || !InBounds(z.x1, z.y1)) e.Add($"authored spawn zone ({z.x0},{z.y0})-({z.x1},{z.y1}) is outside the map or inverted");
            }
            return e;
        }
    }
}
