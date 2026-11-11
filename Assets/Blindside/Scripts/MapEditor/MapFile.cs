// Saved maps: the file format (MapFile), reading / writing / listing them on disk (MapStore) and the little colour-coded picture
// of a map (MapPreview).
//
// A file holds what the editor holds — terrain, crates and the objects as placed, *possibly incomplete* (a map saved half-way has
// no hostage yet, or three defender starts) — so it is not a MapData (which always carries a complete set); the game's MapData is
// built from it by MapEditModel.BuildGameData, and only when the map has no required error. Files live in
// Application.persistentDataPath/Maps/{name}.json; the built-in default house is never a file.
//
// Reading is strict: a file that doesn't parse, whose arrays don't match its size, that has a terrain value, operator key, slot or
// version the game doesn't know, or objects that could not have been placed, is "corrupt" — it is listed as such with the reason
// and never opened; nothing throws out of MapStore.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Blindside.Map;

namespace Blindside.MapEditor
{
    [Serializable]
    public class MapFileDefender
    {
        public int slot;             // 0..4 = VOLT, MIASMA, WRAITH, OBSIDIAN, ARGUS
        public string operatorKey;
        public Vector2Int cell;
        public float facing;         // radians
    }

    [Serializable]
    public class MapFile
    {
        public const int CurrentFormat = 1;

        public int format = CurrentFormat;
        public string name;
        public int width, height;
        public int[] terrain;        // width*height, y*width+x; MapTerrain values (ints, so an unknown one can be told)
        public bool[] crates;
        public bool hasHostage;
        public Vector2Int hostage;
        public MapFileDefender[] defenders;
        public MapTerrorist[] terrorists;   // generic terrorists beyond the five (none in older files)
        public MapCaptive[] captives;       // captured operators (none in older files)
        public Vector2Int[] extraHostages;  // civilian hostages after the first (none in older files)
        public int winRule;                 // MapData.winRule (0 in older files: either)
        public MapGadget[] gadgets;         // the defense's gadgets (none in older files)
        public Vector2Int[] cameras;
        public MapExfil[] exfils;

        public static MapFile FromModel(MapEditModel m)
        {
            var o = m.Objects;
            var f = new MapFile
            {
                name = m.Data.name, width = m.Width, height = m.Height,
                terrain = new int[m.Data.terrain.Length], crates = (bool[])m.Data.crates.Clone(),
                hasHostage = o.hostage.HasValue, hostage = o.hostage ?? default,
                cameras = o.cameras.ToArray(),
            };
            for (int i = 0; i < f.terrain.Length; i++) f.terrain[i] = (int)m.Data.terrain[i];
            var defs = new List<MapFileDefender>();
            for (int s = 0; s < ObjectState.DefenderSlots; s++)
                if (o.defPlaced[s]) defs.Add(new MapFileDefender { slot = s, operatorKey = MapData.DefenderKeys[s], cell = o.defCell[s], facing = o.defFacing[s] });
            f.defenders = defs.ToArray();
            f.extraHostages = o.extraHostages.ToArray();
            f.winRule = o.winRule;
            f.gadgets = new MapGadget[o.gadgets.Count];
            for (int i = 0; i < f.gadgets.Length; i++) f.gadgets[i] = new MapGadget { cell = o.gadgets[i].cell, kind = o.gadgets[i].kind };
            f.captives = new MapCaptive[o.captives.Count];
            for (int i = 0; i < f.captives.Length; i++) f.captives[i] = new MapCaptive { cell = o.captives[i].cell, operatorKey = o.captives[i].operatorKey };
            f.terrorists = new MapTerrorist[o.terrorists.Count];
            for (int i = 0; i < f.terrorists.Length; i++)
                f.terrorists[i] = new MapTerrorist { cell = o.terrorists[i].cell, facing = o.terrorists[i].facing, weapon = o.terrorists[i].weapon };
            f.exfils = new MapExfil[o.exfils.Count];
            for (int i = 0; i < f.exfils.Length; i++)
                f.exfils[i] = new MapExfil { name = MapEditModel.ExfilLabel(i), x0 = o.exfils[i].x0, y0 = o.exfils[i].y0, x1 = o.exfils[i].x1, y1 = o.exfils[i].y1 };
            return f;
        }

        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary>Reads and checks a file's text. False with a Japanese reason (shown to the player as "このマップを開けません: …") when
        /// it is not a map this game can open.</summary>
        public static bool TryParse(string json, out MapFile file, out string why)
        {
            file = null;
            if (string.IsNullOrWhiteSpace(json)) { why = "ファイルが空です"; return false; }
            MapFile f;
            try
            {
                if (json.TrimStart()[0] != '{') { why = "JSON形式ではありません"; return false; }
                f = JsonUtility.FromJson<MapFile>(json);
            }
            catch (Exception ex) { why = "JSONを読み込めません (" + FirstLine(ex.Message) + ")"; return false; }
            if (f == null) { why = "JSONを読み込めません"; return false; }

            if (f.format != CurrentFormat) { why = $"対応していないファイル形式です (format {f.format}, 対応: {CurrentFormat})"; return false; }
            if (f.width < MapEditorScreen.MinSize || f.width > MapEditorScreen.MaxSize || f.height < MapEditorScreen.MinSize || f.height > MapEditorScreen.MaxSize)
            { why = $"マップサイズが範囲外です ({f.width}×{f.height}, 許容 {MapEditorScreen.MinSize}〜{MapEditorScreen.MaxSize})"; return false; }
            int n = f.width * f.height;
            if (f.terrain == null || f.terrain.Length != n) { why = $"サイズが合いません: 床のマス{(f.terrain == null ? 0 : f.terrain.Length)}個、{f.width}×{f.height}には{n}個が必要です"; return false; }
            if (f.crates == null || f.crates.Length != n) { why = $"サイズが合いません: 木箱のマス{(f.crates == null ? 0 : f.crates.Length)}個、{n}個が必要です"; return false; }
            for (int i = 0; i < n; i++)
                if (f.terrain[i] < 0 || f.terrain[i] > (int)MapTerrain.Reinforced) { why = $"不明な床の値です: {f.terrain[i]} ({i % f.width}, {i / f.width})"; return false; }
            for (int i = 0; i < n; i++)
                if (f.crates[i] && f.terrain[i] >= (int)MapTerrain.Solid) { why = $"壁の上に木箱があります ({i % f.width}, {i / f.width})"; return false; }

            f.defenders = f.defenders ?? new MapFileDefender[0];
            f.terrorists = f.terrorists ?? new MapTerrorist[0];
            f.captives = f.captives ?? new MapCaptive[0];
            f.extraHostages = f.extraHostages ?? new Vector2Int[0];
            if (f.winRule < MapData.WinEither || f.winRule > MapData.WinExtract) { why = "勝利条件の値が不正です"; return false; }
            f.gadgets = f.gadgets ?? new MapGadget[0];
            if (f.gadgets.Length > MapData.MaxGadgets) { why = $"ガジェットが多すぎます (最大{MapData.MaxGadgets}個)"; return false; }
            foreach (var g in f.gadgets)
                if (g == null || System.Array.IndexOf(MapData.GadgetKinds, g.kind) < 0) { why = "不明なガジェットがあります"; return false; }
            if (!f.hasHostage && f.extraHostages.Length > 0) { why = "最初の人質がないのに追加の人質があります"; return false; }
            if (f.extraHostages.Length + 1 > MapData.MaxHostages) { why = $"人質が多すぎます (最大{MapData.MaxHostages}人)"; return false; }
            foreach (var c in f.captives) if (c == null || string.IsNullOrEmpty(c.operatorKey)) { why = "捕虜の一覧に空の項目があります"; return false; }
            foreach (var t in f.terrorists)
            {
                if (t == null) { why = "テロリストの一覧に空の項目があります"; return false; }
                if (float.IsNaN(t.facing) || float.IsInfinity(t.facing)) { why = "テロリストの向きの値が正しくありません"; return false; }
                if (System.Array.IndexOf(MapData.TerroristWeapons, t.weapon) < 0) t.weapon = MapData.TerroristWeapons[0];
            }
            f.cameras = f.cameras ?? new Vector2Int[0];
            f.exfils = f.exfils ?? new MapExfil[0];
            var seen = new bool[ObjectState.DefenderSlots];
            foreach (var d in f.defenders)
            {
                if (d == null) { why = "防衛開始位置の一覧に空の項目があります"; return false; }
                if (d.slot < 0 || d.slot >= ObjectState.DefenderSlots) { why = $"不明な防衛開始位置の番号です: {d.slot}"; return false; }
                if (seen[d.slot]) { why = $"防衛開始位置{d.slot + 1}番が重複しています"; return false; }
                seen[d.slot] = true;
                if (d.operatorKey != MapData.DefenderKeys[d.slot]) { why = $"不明な隊員名です: \"{d.operatorKey}\" (防衛開始位置{d.slot + 1}番は{MapData.DefenderKeys[d.slot]})"; return false; }
                if (float.IsNaN(d.facing) || float.IsInfinity(d.facing)) { why = $"防衛開始位置{d.slot + 1}番の向きの値が正しくありません"; return false; }
            }
            foreach (var e in f.exfils) if (e == null) { why = "救出地点の一覧に空の項目があります"; return false; }
            if (string.IsNullOrWhiteSpace(f.name)) f.name = "名前のないマップ";

            var model = f.ToModelUnchecked();
            string problem = model.FindObjectProblem();
            if (problem != null) { why = problem; return false; }
            file = f; why = null;
            return true;
        }

        static string FirstLine(string s) { int i = s.IndexOf('\n'); return (i < 0 ? s : s.Substring(0, i)).Trim(); }

        MapEditModel ToModelUnchecked()
        {
            var data = MapData.CreateBlank(name, width, height);
            for (int i = 0; i < terrain.Length; i++) data.terrain[i] = (MapTerrain)terrain[i];
            Array.Copy(crates, data.crates, crates.Length);
            var o = new ObjectState();
            if (hasHostage) o.hostage = hostage;
            if (hasHostage && extraHostages != null) o.extraHostages.AddRange(extraHostages);
            o.winRule = winRule;
            if (gadgets != null) foreach (var g in gadgets) if (g != null) o.gadgets.Add(new GadgetSpot { cell = g.cell, kind = g.kind });
            foreach (var d in defenders) { o.defPlaced[d.slot] = true; o.defCell[d.slot] = d.cell; o.defFacing[d.slot] = d.facing; }
            o.cameras.AddRange(cameras);
            foreach (var e in exfils) o.exfils.Add(new ExfilRect { x0 = e.x0, y0 = e.y0, x1 = e.x1, y1 = e.y1 });
            if (terrorists != null) foreach (var t in terrorists) if (t != null) o.terrorists.Add(new TerroristSpot { cell = t.cell, facing = t.facing, weapon = t.weapon });
            if (captives != null) foreach (var c in captives) if (c != null) o.captives.Add(new CaptiveSpot { cell = c.cell, operatorKey = c.operatorKey });
            return new MapEditModel(data, o);
        }

        /// <summary>A fresh editing model of this (already checked) file.</summary>
        public MapEditModel ToModel() => ToModelUnchecked();
    }

    /// <summary>One line of the map list.</summary>
    public sealed class MapEntry
    {
        public enum EntryKind { Default, Saved, Corrupt }

        public EntryKind kind;
        public string stem;             // file name without ".json" ("" for the built-in default house)
        public string displayName;
        public int width, height;       // 0 when corrupt
        public DateTime modified;       // Saved / Corrupt
        public MapFile file;            // what the preview draws (Default and Saved)
        public string corruptReason;    // Corrupt
        public List<string> errors = new List<string>();     // required errors (Default / Saved)

        public bool IsDefault => kind == EntryKind.Default;
        public bool IsCorrupt => kind == EntryKind.Corrupt;
        public bool HasRequiredError => errors.Count > 0;
        public string SizeText => IsCorrupt ? "?" : $"{width}×{height}";

        /// <summary>The editing model of this map (the default house: from its own data, with its house intro flag, for viewing only).</summary>
        public MapEditModel ToModel() => IsDefault ? new MapEditModel(MapLibrary.LoadDefault(), true) : file.ToModel();

        /// <summary>The MapData a match reads, or null when this map can't be played (corrupt / required error). Always a new object.</summary>
        public MapData BuildGameData()
        {
            if (IsDefault) return MapLibrary.LoadDefault();
            if (IsCorrupt || HasRequiredError) return null;
            try { return file.ToModel().BuildGameData(); } catch (Exception ex) { Debug.LogWarning("[MapStore] " + displayName + ": " + ex.Message); return null; }
        }
    }

    public static class MapStore
    {
        public const string DefaultName = "基本邸宅";
        const string SelectedKey = "Blindside.SelectedMap";

        /// <summary>Tests point this at a scratch folder so they never touch the player's saved maps; null in the game.</summary>
        public static string DirectoryOverride;
        public static string Directory => DirectoryOverride ?? Path.Combine(Application.persistentDataPath, "Maps");

        // ---- names ------------------------------------------------------------------------------------------------------------

        static readonly string[] Reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

        /// <summary>The file name (without extension) a map name is saved under: characters a file name can't hold become "_", edges are
        /// trimmed, device names (CON, NUL …) get a "_" in front, an empty name becomes "新規マップ"; at most 50 characters.</summary>
        public static string SanitizeFileName(string name)
        {
            var bad = new HashSet<char>(Path.GetInvalidFileNameChars()) { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
            var sb = new StringBuilder();
            foreach (char c in name ?? "") sb.Append(bad.Contains(c) || char.IsControl(c) ? '_' : c);
            string s = sb.ToString().Trim().TrimEnd('.', ' ');
            if (s.Length > 50) s = s.Substring(0, 50).TrimEnd('.', ' ');
            if (s.Length == 0) s = "新規マップ";
            if (Array.IndexOf(Reserved, s.ToUpperInvariant()) >= 0) s = "_" + s;
            return s;
        }

        public static bool IsDefaultStem(string stem) => string.Equals(stem, DefaultName, StringComparison.OrdinalIgnoreCase);
        public static string PathFor(string stem) => Path.Combine(Directory, stem + ".json");
        public static bool Exists(string stem) => File.Exists(PathFor(stem));

        /// <summary>`baseStem`, or "baseStem 2", "baseStem 3" … — the first that is not taken (and is not the default house).</summary>
        public static string UniqueStem(string baseStem)
        {
            string s = baseStem; int n = 2;
            while (IsDefaultStem(s) || Exists(s)) s = baseStem + " " + n++;
            return s;
        }

        // ---- writing ----------------------------------------------------------------------------------------------------------

        /// <summary>Writes the map to Maps/{stem}.json (through a temp file, so a failure leaves the old file intact). Throws IOException /
        /// UnauthorizedAccessException to the caller, which shows them.</summary>
        public static string Save(MapFile file, string stem)
        {
            System.IO.Directory.CreateDirectory(Directory);
            string path = PathFor(stem), tmp = path + ".tmp";
            File.WriteAllText(tmp, file.ToJson(), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return path;
        }

        /// <summary>Editor only: a saved map that a quest was exported from (MapCatalog.Entry.sourceStem == `stem`) goes straight into that quest's
        /// data, so the game, its scene and the hologram play it. Returns the quest's name (null: no quest uses this file);
        /// `rejected`: the map can't be played yet (required errors), so the quest keeps its last good version.</summary>
        public static string SyncCatalog(string stem, out bool rejected)
        {
            rejected = false;
#if UNITY_EDITOR
            var cat = Blindside.Flow.MapCatalog.Instance;
            if (cat == null) return null;
            foreach (var e in cat.entries)
            {
                if (e == null || e.mapJson == null || !string.Equals(e.sourceStem, stem, StringComparison.OrdinalIgnoreCase)) continue;
                string name = string.IsNullOrEmpty(e.displayName) ? e.id : e.displayName;
                var data = ReadFile(PathFor(stem)).BuildGameData();
                if (data == null || data.Validate().Count > 0) { rejected = true; return name; }
                string asset = UnityEditor.AssetDatabase.GetAssetPath(e.mapJson);
                File.WriteAllText(asset, File.ReadAllText(PathFor(stem), Encoding.UTF8), new UTF8Encoding(false));
                UnityEditor.AssetDatabase.ImportAsset(asset, UnityEditor.ImportAssetOptions.ForceUpdate);
                return name;
            }
            return null;
#else
            return null;
#endif
        }

        public static void Delete(string stem)
        {
            string path = PathFor(stem);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>Copies a listed map to a new file "{name} (複製)" (numbered when that exists). Returns the new stem.</summary>
        public static string Duplicate(MapEntry e)
        {
            var f = e.file;
            string baseName = (e.IsDefault ? DefaultName : e.displayName) + " (複製)";
            string stem = UniqueStem(SanitizeFileName(baseName));
            var copy = JsonUtility.FromJson<MapFile>(JsonUtility.ToJson(f));
            copy.name = stem;
            Save(copy, stem);
            return stem;
        }

        // ---- reading ----------------------------------------------------------------------------------------------------------

        public static MapEntry DefaultEntry()
        {
            var data = MapLibrary.LoadDefault();
            var model = new MapEditModel(data, true);
            var e = new MapEntry
            {
                kind = MapEntry.EntryKind.Default, stem = "", displayName = DefaultName, width = data.width, height = data.height, file = MapFile.FromModel(model),
            };
            FillErrors(e, model);
            return e;
        }

        static void FillErrors(MapEntry e, MapEditModel model)
        {
            var r = MapValidator.Validate(model);
            foreach (var i in r.issues) if (i.error) e.errors.Add(i.text);
        }

        /// <summary>Reads one file into a list entry (never throws; a file that can't be read is a Corrupt entry with the reason).</summary>
        public static MapEntry ReadFile(string path)
        {
            string stem = Path.GetFileNameWithoutExtension(path);
            var e = new MapEntry { stem = stem, displayName = stem };
            try { e.modified = File.GetLastWriteTime(path); } catch { e.modified = DateTime.MinValue; }
            string json;
            try { json = File.ReadAllText(path, Encoding.UTF8); }
            catch (Exception ex) { e.kind = MapEntry.EntryKind.Corrupt; e.corruptReason = "ファイルを読み込めません (" + ex.Message + ")"; return e; }
            if (!MapFile.TryParse(json, out var f, out var why)) { e.kind = MapEntry.EntryKind.Corrupt; e.corruptReason = why; return e; }
            e.kind = MapEntry.EntryKind.Saved; e.file = f; e.width = f.width; e.height = f.height; e.displayName = f.name;
            FillErrors(e, f.ToModel());
            return e;
        }

        /// <summary>The default house first, then the saved maps, newest first, damaged ones included.</summary>
        public static List<MapEntry> List()
        {
            var list = new List<MapEntry> { DefaultEntry() };
            var saved = new List<MapEntry>();
            try
            {
                if (System.IO.Directory.Exists(Directory))
                    foreach (var path in System.IO.Directory.GetFiles(Directory, "*.json")) saved.Add(ReadFile(path));
            }
            catch (Exception ex) { Debug.LogWarning("[MapStore] can't list " + Directory + ": " + ex.Message); }
            saved.Sort((a, b) => b.modified.CompareTo(a.modified));
            list.AddRange(CatalogEntries());
            list.AddRange(saved);
            return list;
        }

        /// <summary>One entry by its stem ("" = the default house, "@id" = a map the build ships); null when there is no such map.</summary>
        public static MapEntry Find(string stem)
        {
            if (string.IsNullOrEmpty(stem) || IsDefaultStem(stem)) return DefaultEntry();
            if (Blindside.Flow.MapCatalog.IsCatalogStem(stem))
            {
                var c = Blindside.Flow.MapCatalog.Find(Blindside.Flow.MapCatalog.IdOf(stem));
                return c != null && c.listed ? ReadCatalog(c) : null;
            }
            return Exists(stem) ? ReadFile(PathFor(stem)) : null;
        }

        // ---- maps the build ships (Resources/BlindsideMapCatalog: exported from the editor as map scenes) ---------------------------

        static List<MapEntry> CatalogEntries()
        {
            var list = new List<MapEntry>();
            var cat = Blindside.Flow.MapCatalog.Instance;
            if (cat == null) return list;
            foreach (var c in cat.entries) if (c != null && c.listed && c.mapJson != null) list.Add(ReadCatalog(c));
            return list;
        }

        static MapEntry ReadCatalog(Blindside.Flow.MapCatalog.Entry c)
        {
            var e = new MapEntry { stem = Blindside.Flow.MapCatalog.StemPrefix + c.id, displayName = string.IsNullOrEmpty(c.displayName) ? c.id : c.displayName };
            if (c.mapJson == null) { e.kind = MapEntry.EntryKind.Corrupt; e.corruptReason = "ファイルを読み込めません"; return e; }
            if (!MapFile.TryParse(c.mapJson.text, out var f, out var why)) { e.kind = MapEntry.EntryKind.Corrupt; e.corruptReason = why; return e; }
            e.kind = MapEntry.EntryKind.Saved; e.file = f; e.width = f.width; e.height = f.height;
            FillErrors(e, f.ToModel());
            return e;
        }

        // ---- the title's choice -----------------------------------------------------------------------------------------------

        /// <summary>The map the title will start ("" = the default house), remembered in PlayerPrefs.</summary>
        public static string SelectedStem
        {
            get => PlayerPrefs.GetString(SelectedKey, "");
            set { PlayerPrefs.SetString(SelectedKey, value ?? ""); PlayerPrefs.Save(); }
        }

        /// <summary>The remembered map; when it is gone or damaged, the default house (and the memory is reset).</summary>
        public static MapEntry LoadSelected()
        {
            var e = Find(SelectedStem);
            if (e == null || e.IsCorrupt) { SelectedStem = ""; e = DefaultEntry(); }
            return e;
        }
    }

    /// <summary>The map as a small picture: one pixel per cell — outdoor floor, indoor floor, the three wall kinds, crates, exfil zones and
    /// the hostage, each its own colour.</summary>
    public static class MapPreview
    {
        public static readonly Color Outdoor = new Color32(0x2a, 0x3a, 0x2c, 0xff), Indoor = new Color32(0xc4, 0xc9, 0xd2, 0xff);
        public static readonly Color Solid = new Color32(0x4a, 0x50, 0x5c, 0xff), Breakable = new Color32(0xd9, 0x8a, 0x3c, 0xff), Reinforced = new Color32(0x56, 0xb4, 0xf0, 0xff);
        public static readonly Color Crate = new Color32(0xb8, 0x9a, 0x28, 0xff), Exfil = new Color32(0xff, 0xe0, 0x3a, 0xff), Hostage = new Color32(0xff, 0x3d, 0x9a, 0xff);

        public static Texture2D Make(MapFile f)
        {
            var tex = new Texture2D(f.width, f.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "MapPreview" };
            var px = new Color32[f.width * f.height];
            for (int y = 0; y < f.height; y++)
                for (int x = 0; x < f.width; x++)
                {
                    int i = y * f.width + x;
                    Color c;
                    switch ((MapTerrain)f.terrain[i])
                    {
                        case MapTerrain.In: c = Indoor; break;
                        case MapTerrain.Solid: c = Solid; break;
                        case MapTerrain.Breakable: c = Breakable; break;
                        case MapTerrain.Reinforced: c = Reinforced; break;
                        default: c = Outdoor; break;
                    }
                    if (f.crates[i]) c = Crate;
                    px[(f.height - 1 - y) * f.width + x] = c;      // texture row 0 is the bottom; grid y = 0 is the top row
                }
            foreach (var e in f.exfils)
                for (int y = e.y0; y <= e.y1; y++)
                    for (int x = e.x0; x <= e.x1; x++) px[(f.height - 1 - y) * f.width + x] = Exfil;
            if (f.hasHostage) px[(f.height - 1 - f.hostage.y) * f.width + f.hostage.x] = Hostage;
            if (f.extraHostages != null)
                foreach (var h in f.extraHostages)
                    if (h.x >= 0 && h.y >= 0 && h.x < f.width && h.y < f.height) px[(f.height - 1 - h.y) * f.width + h.x] = Hostage;
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
