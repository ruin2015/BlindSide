// Checks the map files and everything that reads them, without playing: name sanitizing, save → read round trips (complete and
// half-finished maps), every kind of damaged file, duplicate / delete / list order, the MapData built for a match (valid, independent
// of the editor's data, drops outside the exfil zones), the remembered title selection and the title's exfil sentence.
// Works in a scratch folder (MapStore.DirectoryOverride) — the player's own Maps folder and PlayerPrefs selection are restored.
// Menu: Blindside/Map/Verify Map Files.
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Blindside.MapEditor;
using Blindside.UI;

namespace Blindside.Map.Editor
{
    public static class MapStoreVerify
    {
        static Vector2Int P(int x, int y) => new Vector2Int(x, y);

        [MenuItem("Blindside/Map/Verify Map Files")]
        public static void Menu()
        {
            string report = Run(out bool ok);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        /// <summary>A 30×22 map of three rooms (breakable, solid and reinforced walls, a crate), two cameras, two exfil zones.</summary>
        public static MapEditModel ThreeRooms(string name = "3つの部屋")
        {
            var m = new MapEditModel(MapData.CreateBlank(name, 30, 22), false);
            m.Apply(EditTool.InFloor, MapEditModel.RectCells(P(5, 5), P(11, 10), false));      // room 1
            m.Apply(EditTool.InFloor, MapEditModel.RectCells(P(15, 5), P(21, 10), false));     // room 2
            m.Apply(EditTool.InFloor, MapEditModel.RectCells(P(9, 14), P(17, 18), false));     // room 3
            m.Apply(EditTool.BreakableWall, MapEditModel.RectCells(P(4, 4), P(12, 11), true));
            m.Apply(EditTool.SolidWall, MapEditModel.RectCells(P(14, 4), P(22, 11), true));
            m.Apply(EditTool.ReinforcedWall, MapEditModel.RectCells(P(8, 13), P(18, 19), true));
            m.Apply(EditTool.Erase, new[] { P(8, 11), P(18, 11), P(13, 13) });                // doors (wall → floor)
            m.Apply(EditTool.Crate, new[] { P(10, 16) });
            m.PlaceHostage(P(16, 15));
            for (int i = 0; i < 5; i++) m.PlaceDefender(P(6 + i, 6), i * .5f, out _);
            m.PlaceCamera(P(5, 5)); m.PlaceCamera(P(21, 10));
            m.AddExfil(P(0, 0), P(2, 1)); m.AddExfil(P(27, 20), P(29, 21));
            return m;
        }

        static void Write(string stem, string text) => File.WriteAllText(MapStore.PathFor(stem), text, new UTF8Encoding(false));

        public static string Run(out bool ok)
        {
            var sb = new StringBuilder(); int bad = 0;
            void Check(string what, bool pass) { sb.AppendLine((pass ? "PASS  " : "FAIL  ") + what); if (!pass) bad++; }

            string scratch = Path.Combine(Path.GetTempPath(), "BlindsideMapStoreVerify_" + Guid.NewGuid().ToString("N"));
            string oldSel = PlayerPrefs.GetString("Blindside.SelectedMap", "");
            MapStore.DirectoryOverride = scratch;
            Directory.CreateDirectory(scratch);
            try
            {
                // ---- names ----
                Check("sanitize: / \\ : * ? \" < > | become _", MapStore.SanitizeFileName("a/b\\c:d*e?f\"g<h>i|j") == "a_b_c_d_e_f_g_h_i_j");
                Check("sanitize: edges trimmed, trailing dots dropped, empty → \"新規マップ\", device names get a _", MapStore.SanitizeFileName("  map.. ") == "map" && MapStore.SanitizeFileName("") == "新規マップ" && MapStore.SanitizeFileName("...") == "新規マップ" && MapStore.SanitizeFileName("con") == "_con" && MapStore.SanitizeFileName("NUL") == "_NUL");
                Check("sanitize: 50 characters at most; Japanese, spaces and parentheses are kept", MapStore.SanitizeFileName(new string('あ', 80)).Length == 50 && MapStore.SanitizeFileName("基本邸宅 (複製)") == "基本邸宅 (複製)");
                Check("the default house's name is reserved (case-insensitive)", MapStore.IsDefaultStem("基本邸宅") && MapStore.IsDefaultStem(MapStore.SanitizeFileName("基本邸宅")));
                Check("sanitizing gives a legal file name in every case", new[] { "a/b", "x:y", "?", "COM1", "end.", " ", "a\tb" }.All(n => MapStore.SanitizeFileName(n).IndexOfAny(Path.GetInvalidFileNameChars()) < 0));

                // ---- a complete map: save → read ----
                var full = ThreeRooms();
                var file = MapFile.FromModel(full);
                MapStore.Save(file, "3つの部屋");
                var e = MapStore.ReadFile(MapStore.PathFor("3つの部屋"));
                Check("saved map reads back as a normal entry: name, 30×22, no required error", e.kind == MapEntry.EntryKind.Saved && e.displayName == "3つの部屋" && e.width == 30 && e.height == 22 && !e.HasRequiredError);
                var back = e.ToModel();
                Check("round trip keeps terrain, crates and every object exactly (JSON identical)", MapFile.FromModel(back).ToJson() == file.ToJson()
                    && back.Data.terrain.SequenceEqual(full.Data.terrain) && back.Data.crates.SequenceEqual(full.Data.crates)
                    && back.Objects.SameAs(full.Objects));
                Check("the three wall kinds and the crate survive (3 kinds present, 1 crate)",
                    back.Data.terrain.Contains(MapTerrain.Breakable) && back.Data.terrain.Contains(MapTerrain.Solid) && back.Data.terrain.Contains(MapTerrain.Reinforced) && back.Data.crates.Count(c => c) == 1);
                Check("file is UTF-8 without BOM and pretty JSON", File.ReadAllBytes(MapStore.PathFor("3つの部屋"))[0] == (byte)'{');

                // ---- a half-finished map is saved and listed, with its required errors ----
                var wip = new MapEditModel(MapData.CreateBlank("作業中", 20, 16), false);
                wip.Apply(EditTool.InFloor, MapEditModel.RectCells(P(4, 4), P(9, 8), false));
                wip.PlaceHostage(P(5, 5));
                MapStore.Save(MapFile.FromModel(wip), "作業中");
                var ew = MapStore.ReadFile(MapStore.PathFor("作業中"));
                Check("half-finished map (no defenders, no exfil) saves and reads back: Saved + required errors, not corrupt",
                    ew.kind == MapEntry.EntryKind.Saved && ew.HasRequiredError && ew.errors.Count == 2 && ew.file.hasHostage && ew.file.defenders.Length == 0);
                Check("…and it can't be started (no game data), while the complete one can", ew.BuildGameData() == null && e.BuildGameData() != null);
                var blank = new MapEditModel(MapData.CreateBlank("空のマップ", 16, 16), false);
                MapStore.Save(MapFile.FromModel(blank), "空のマップ");
                var eb = MapStore.ReadFile(MapStore.PathFor("空のマップ"));
                Check("an empty map saves too (3 required errors: hostage, defenders, exfil; its outdoor floor is plenty)", eb.kind == MapEntry.EntryKind.Saved && eb.errors.Count == 3);

                // ---- damaged files ----
                void Bad(string stem, string text, string expect, string what)
                {
                    Write(stem, text);
                    var d = MapStore.ReadFile(MapStore.PathFor(stem));
                    Check($"damaged: {what} → Corrupt, reason contains \"{expect}\" (got: {d.corruptReason})", d.IsCorrupt && d.corruptReason != null && d.corruptReason.Contains(expect) && d.BuildGameData() == null);
                }
                string good = file.ToJson();
                Bad("x_empty", "", "空です", "empty file");
                Bad("x_text", "hello world", "JSON", "not JSON");
                Bad("x_trunc", good.Substring(0, good.Length / 2), "", "truncated JSON");
                Bad("x_array", "[1,2,3]", "JSON", "JSON array instead of an object");
                Bad("x_noarrays", "{\"format\":1,\"name\":\"n\",\"width\":20,\"height\":16}", "サイズが合いません", "missing arrays");
                Bad("x_size", good.Replace("\"width\": 30", "\"width\": 31"), "サイズが合いません", "width no longer matches the cell count");
                Bad("x_huge", good.Replace("\"width\": 30", "\"width\": 500"), "範囲", "size out of range");
                Bad("x_terrain", Regex(good, "\"terrain\": \\[\\s*0", "\"terrain\": [\n        9"), "不明な床の値", "unknown terrain value 9");
                Bad("x_format", good.Replace("\"format\": 1", "\"format\": 7"), "対応していないファイル形式", "unknown format version");
                Bad("x_key", good.Replace("\"operatorKey\": \"MIASMA\"", "\"operatorKey\": \"GHOST\""), "不明な隊員名", "unknown operator key");
                Bad("x_slot", good.Replace("\"slot\": 4", "\"slot\": 9"), "防衛開始位置の番号", "unknown defender slot");
                Bad("x_hostage", good.Replace("\"hostage\": {\n        \"x\": 16", "\"hostage\": {\n        \"x\": 8"), "壁の上", "hostage moved onto a wall");
                Bad("x_dup", good.Replace("\"slot\": 1,", "\"slot\": 0,"), "重複", "a defender slot used twice");

                // ---- listing ----
                var list = MapStore.List();
                Check("list: the default house is first and read-only", list[0].IsDefault && list[0].displayName == "基本邸宅" && list[0].errors.Count == 0 && list[0].width == 26 && list[0].height == 20);
                Check("list: saved maps, damaged ones included, no exception (3 good + 13 damaged + default)", list.Count == 1 + 3 + 13 && list.Count(x => x.IsCorrupt) == 13);
                Check("list: newest saved map first after the default", list.Skip(1).Zip(list.Skip(2), (a, b) => a.modified >= b.modified).All(v => v));
                Check("the default house's file is never listed twice nor deletable as a file", MapStore.Find("") != null && MapStore.Find("基本邸宅").IsDefault && !File.Exists(MapStore.PathFor("基本邸宅")));

                // ---- duplicate / delete ----
                string copyStem = MapStore.Duplicate(e);
                var ec = MapStore.ReadFile(MapStore.PathFor(copyStem));
                Check("duplicate: \"3つの部屋 (複製)\", an independent valid copy with the same content", copyStem == "3つの部屋 (複製)" && ec.kind == MapEntry.EntryKind.Saved && ec.displayName == copyStem && ec.file.terrain.SequenceEqual(e.file.terrain));
                string copy2 = MapStore.Duplicate(e);
                Check("duplicating again gives a numbered name, nothing is overwritten", copy2 == "3つの部屋 (複製) 2" && MapStore.Exists(copyStem));
                string defCopy = MapStore.Duplicate(list[0]);
                var edc = MapStore.ReadFile(MapStore.PathFor(defCopy));
                Check("duplicating the default house → \"基本邸宅 (複製)\": a normal editable map, 26×20, 4 exfil zones, no errors", defCopy == "基本邸宅 (複製)" && edc.kind == MapEntry.EntryKind.Saved && edc.width == 26 && edc.file.exfils.Length == 4 && !edc.HasRequiredError);
                MapStore.Delete(copy2);
                Check("delete removes the file (and deleting a missing file is harmless)", !MapStore.Exists(copy2)); MapStore.Delete(copy2);
                Check("unique stem never collides with the default house", MapStore.UniqueStem("基本邸宅") == "基本邸宅 2");

                // ---- the MapData a match reads ----
                var data = full.BuildGameData();
                Check("game data: valid (MapData.Validate has no complaint), name, no house intro", data.Validate().Count == 0 && data.name == "3つの部屋" && !data.useHouseIntro);
                Check("game data: hostage, 5 starts in VOLT…ARGUS order with their facing, 2 cameras, exfil zones A and B",
                    data.hostage == P(16, 15) && data.defenderStarts.Length == 5 && data.defenderStarts[0].operatorKey == "VOLT" && data.defenderStarts[4].operatorKey == "ARGUS"
                    && Mathf.Approximately(data.defenderStarts[3].facing, 1.5f) && data.cameras.Length == 2 && data.exfils.Length == 2 && data.exfils[0].name == "A" && data.exfils[1].name == "B");
                var drops = data.anchors.attackerDrops;
                Check("game data: 5 distinct attacker drop cells, all free outdoor cells outside the exfil zones",
                    drops.Length == 5 && drops.Distinct().Count() == 5 && drops.All(c => data.IsDropCell(c.x, c.y) && !data.InExfil(c.x, c.y)));
                string before = MapFile.FromModel(full).ToJson();
                data.terrain[data.Index(5, 4)] = MapTerrain.Out; data.hostage = P(1, 1); data.cameras[0] = P(0, 0); data.exfils[0].x1 = 9;
                Check("the game's MapData is a separate object: changing it never touches the editor's map", MapFile.FromModel(full).ToJson() == before);
                var clone = full.BuildGameData().Clone();
                Check("a clone of the game data is independent of it too (what Launch hands the game)", !ReferenceEquals(clone.terrain, data.terrain) && clone.Validate().Count == 0);
                bool threw = false;
                try { wip.BuildGameData(); } catch (InvalidOperationException) { threw = true; }
                Check("game data of an incomplete map is refused (InvalidOperationException)", threw);
                var prev = MapLibrary.Current;
                try
                {
                    var g = full.BuildGameData();
                    Check("MapLibrary accepts it and MapGrid builds it (walls → panels)", MapLibrary.SetCurrent(g) && new MapGrid(g).Panels.Count > 0);
                    Check("outdoor cameras are accepted by the game's validation now (one camera outdoors)", MapLibrary.SetCurrent(MapStoreVerifyOutdoorCamera()));
                }
                finally { MapLibrary.SetCurrent(prev); }

                // ---- the title's choice ----
                PlayerPrefs.SetString("Blindside.SelectedMap", "3つの部屋");
                var sel = MapStore.LoadSelected();
                Check("remembered selection is read back", MapStore.SelectedStem == "3つの部屋" && sel.displayName == "3つの部屋");
                MapStore.SelectedStem = "x_size";
                Check("a remembered map that is damaged falls back to the default house (and the memory is reset)", MapStore.LoadSelected().IsDefault && MapStore.SelectedStem == "");
                MapStore.SelectedStem = "存在しないマップ";
                Check("a remembered map that is gone falls back to the default house", MapStore.LoadSelected().IsDefault && MapStore.SelectedStem == "");
                Check("title exfil sentence: default 4 zones, three rooms 2 zones, one zone",
                    TitleSelectScreen.ExfilGoalText(list[0]) == "救出地点4か所(EXFIL A〜D)のいずれかまで護送"
                    && TitleSelectScreen.ExfilGoalText(e) == "救出地点2か所(EXFIL A〜B)のいずれかまで護送"
                    && TitleSelectScreen.ExfilGoalText(eb) == "救出地点0か所 (マップに救出地点なし)");
                var one = ThreeRooms(); one.Delete(new ObjRef(ObjKind.Exfil, 1));
                MapStore.Save(MapFile.FromModel(one), "ひとつ");
                Check("one zone reads \"救出地点1か所(EXFIL A)まで護送\"", TitleSelectScreen.ExfilGoalText(MapStore.ReadFile(MapStore.PathFor("ひとつ"))) == "救出地点1か所(EXFIL A)まで護送");

                // ---- the picture ----
                var tex = MapPreview.Make(e.file);
                Color Px(int x, int y) => tex.GetPixel(x, e.height - 1 - y);
                Check("preview: one pixel per cell; outdoor, indoor, the three wall kinds, crate, exfil and hostage each have their own colour",
                    tex.width == 30 && tex.height == 22 && Px(2, 8) == MapPreview.Outdoor && Px(6, 6) == MapPreview.Indoor && Px(14, 6) == MapPreview.Solid
                    && Px(4, 8) == MapPreview.Breakable && Px(8, 15) == MapPreview.Reinforced && Px(10, 16) == MapPreview.Crate && Px(1, 0) == MapPreview.Exfil && Px(16, 15) == MapPreview.Hostage);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                MapStore.DirectoryOverride = null;
                PlayerPrefs.SetString("Blindside.SelectedMap", oldSel);
                try { Directory.Delete(scratch, true); } catch { }
            }
            ok = bad == 0;
            sb.AppendLine(ok ? "ALL MAP-FILE CHECKS PASSED" : $"{bad} CHECK(S) FAILED");
            return sb.ToString();
        }

        static string Regex(string text, string pattern, string replacement) => System.Text.RegularExpressions.Regex.Replace(text, pattern, replacement, System.Text.RegularExpressions.RegexOptions.None);

        // the three-room map with a camera on outdoor floor (next to a room), for the validation relaxation
        static MapData MapStoreVerifyOutdoorCamera()
        {
            var m = ThreeRooms();
            m.PlaceCamera(P(2, 12));
            return m.BuildGameData();
        }
    }
}
