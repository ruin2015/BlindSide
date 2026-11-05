// (Blindside: makes and keeps the scene set (SceneFlow).
//  Blindside > Scenes > Build Scene Set — Title / Tutorial / Battle (in the build) and MapEditor (editor only) under Assets/Blindside/Scenes,
//      each a copy of Assets/Scenes/SampleScene.unity with its SceneEntry; a scene that already exists is left alone (it may have been
//      edited since). Also the flow settings and the map catalog under Resources, and the Build Settings.
//  Blindside > Scenes > Strip Title Scene — removes the match systems / world / intro / HUD from the title scene (done on creation too).
//  Blindside > Scenes > Add Main Menu to Title — the main menu (MainMenuScreen) over the select screen (done on creation too).
//  Blindside > Scenes > Apply Build Settings —Title first, Tutorial, Battle, then every listed catalog map's scene; never MapEditor.
//  Blindside > Scenes > Export Saved Map to Map Scene… — a map saved by the map editor (persistentDataPath/Maps/*.json) becomes
//      Assets/Blindside/Maps/{id}.json + Assets/Blindside/Scenes/Maps/{id}.unity (a copy of Battle playing it) + a catalog entry, so a build
//      ships it. Exporting the same map file again updates its data and keeps its id and scene.
//  Every player build first checks the list (Title first, no MapEditor) and refreshes nothing silently.)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Blindside.MapEditor;

namespace Blindside.Flow.EditorTools
{
    [InitializeOnLoad]
    public static class SceneSetBuilder
    {
        // the first load after this script arrives makes the set once — only while nothing is unsaved and not in Play mode
        static SceneSetBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PathOf(Settings().titleScene)) != null) return;
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                    if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogWarning("[SceneSet] unsaved scene open — run Blindside > Scenes > Build Scene Set"); return; }
                BuildSceneSet();
            };
        }

        const string Source = "Assets/Scenes/SampleScene.unity";
        const string SceneDir = "Assets/Blindside/Scenes";
        const string MapSceneDir = SceneDir + "/Maps";
        const string EditorSceneDir = SceneDir + "/Editor";
        const string MapDataDir = "Assets/Blindside/Maps";
        const string ResourcesDir = "Assets/Blindside/Resources";
        const string SettingsPath = ResourcesDir + "/" + SceneFlowSettings.ResourceName + ".asset";
        const string CatalogPath = ResourcesDir + "/" + MapCatalog.ResourceName + ".asset";

        static string PathOf(string sceneName) => $"{SceneDir}/{sceneName}.unity";

        // ---- the scene set ------------------------------------------------------------------------------------------------------

        [MenuItem("Blindside/Scenes/Build Scene Set")]
        public static void BuildSceneSet()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[SceneSet] leave Play mode first"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) == null) { Debug.LogError("[SceneSet] " + Source + " is missing"); return; }
            EnsureFolder(SceneDir); EnsureFolder(MapSceneDir); EnsureFolder(EditorSceneDir); EnsureFolder(MapDataDir);
            var s = Settings();
            Catalog();
            string back = EditorSceneManager.GetActiveScene().path;

            var made = new List<string>();
            if (MakeFromSource(PathOf(s.titleScene), SceneRole.Title)) made.Add(s.titleScene);
            if (MakeFromSource(PathOf(s.tutorialScene), SceneRole.Tutorial)) made.Add(s.tutorialScene);
            if (MakeFromSource(PathOf(s.battleScene), SceneRole.Battle)) made.Add(s.battleScene);
            if (MakeFromSource(s.mapEditorScenePath, SceneRole.MapEditor)) made.Add("MapEditor");

            ApplyBuildSettings();
            if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            Debug.Log(made.Count > 0 ? "[SceneSet] made: " + string.Join(", ", made) : "[SceneSet] every scene already exists (nothing overwritten)");
        }

        /// <summary>Copies SampleScene to `path` and gives it a SceneEntry of `role`; false (untouched) when `path` exists.</summary>
        static bool MakeFromSource(string path, SceneRole role)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) return false;
            if (!AssetDatabase.CopyAsset(Source, path)) { Debug.LogError("[SceneSet] could not copy to " + path); return false; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var go = new GameObject($"[SceneEntry] {role}");
            var entry = go.AddComponent<SceneEntry>();
            entry.role = role;
            go.transform.SetSiblingIndex(0);
            if (role == SceneRole.Title) { StripTitle(scene); EnsureMainMenu(scene); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        // The title scene plays no match (SceneFlow hands it to the battle scene): the match systems, the world, the intro and the HUD are
        // left out of it. Paths from the scene root; anything not found is skipped.
        static readonly string[] TitleUnused =
        {
            "MatchBootstrap", "GameplayUIBridge", "BlindsideHUD", "VisionPolys", "HeliRig", "IntroSequence", "Blindside_MapRoot", "SkyDome",
            "BlindsideCanvas/Letterbox", "BlindsideCanvas/OperationTitle", "BlindsideCanvas/Caption",
        };

        [MenuItem("Blindside/Scenes/Strip Title Scene")]
        public static void StripTitleScene()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[SceneSet] leave Play mode first"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path = PathOf(Settings().titleScene);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { Debug.LogWarning("[SceneSet] missing " + path); return; }
            string back = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = StripTitle(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(back) && back != path) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            Debug.Log($"[SceneSet] title scene: {n} unused object(s) removed");
        }

        static int StripTitle(UnityEngine.SceneManagement.Scene scene)
        {
            int n = 0;
            var roots = scene.GetRootGameObjects();
            foreach (var p in TitleUnused)
            {
                int slash = p.IndexOf('/');
                string rootName = slash < 0 ? p : p.Substring(0, slash);
                var root = roots.FirstOrDefault(r => r != null && r.name == rootName);
                if (root == null) continue;
                var t = slash < 0 ? root.transform : root.transform.Find(p.Substring(slash + 1));
                if (t == null) continue;
                Object.DestroyImmediate(t.gameObject);
                n++;
            }
            return n;
        }

        [MenuItem("Blindside/Scenes/Add Main Menu to Title")]
        public static void AddMainMenuToTitle()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[SceneSet] leave Play mode first"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path = PathOf(Settings().titleScene);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { Debug.LogWarning("[SceneSet] missing " + path); return; }
            string back = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool added = EnsureMainMenu(scene);
            if (added) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            if (!string.IsNullOrEmpty(back) && back != path) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            Debug.Log(added ? "[SceneSet] title scene: main menu added / its key art set" : "[SceneSet] title scene already has its main menu (and its key art, or none is there yet)");
        }

        // the main menu (Blindside.UI.MainMenuScreen): an empty full-screen object, last under the title canvas (over the select screen);
        // it builds itself when the scene plays
        static bool EnsureMainMenu(UnityEngine.SceneManagement.Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var art = AssetDatabase.LoadAssetAtPath<Texture>(Blindside.UI.MainMenuScreen.BackgroundPath);
            foreach (var r in roots)
            {
                var existing = r.GetComponentInChildren<Blindside.UI.MainMenuScreen>(true);
                if (existing == null) continue;
                if (existing.background != null || art == null) return false;
                existing.background = art;              // the key art arrived after the menu was added
                EditorUtility.SetDirty(existing);
                return true;
            }
            var canvas = roots.FirstOrDefault(r => r.name == "BlindsideCanvas");
            if (canvas == null) { Debug.LogWarning("[SceneSet] no BlindsideCanvas in " + scene.path); return false; }
            var go = new GameObject("MainMenu", typeof(RectTransform));
            go.layer = canvas.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();
            var menu = go.AddComponent<Blindside.UI.MainMenuScreen>();
            menu.title = canvas.GetComponentInChildren<Blindside.UI.TitleSelectScreen>(true);
            menu.background = art;
            if (art == null) Debug.LogWarning("[SceneSet] no key art at " + Blindside.UI.MainMenuScreen.BackgroundPath + " — the menu shows the plain background until it is there (then run Add Main Menu to Title again)");
            return true;
        }

        [MenuItem("Blindside/Scenes/Apply Build Settings")]
        public static void ApplyBuildSettings()
        {
            var s = Settings();
            var list = new List<EditorBuildSettingsScene>();
            foreach (var name in new[] { s.titleScene, s.tutorialScene, s.battleScene })
            {
                string p = PathOf(name);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null) list.Add(new EditorBuildSettingsScene(p, true));
                else Debug.LogWarning("[SceneSet] missing " + p + " (Blindside > Scenes > Build Scene Set)");
            }
            var cat = Catalog();
            foreach (var e in cat.entries)
            {
                if (e == null || !e.listed || string.IsNullOrEmpty(e.sceneName)) continue;
                string p = $"{MapSceneDir}/{e.sceneName}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null) list.Add(new EditorBuildSettingsScene(p, true));
                else Debug.LogWarning($"[SceneSet] catalog map {e.id}: scene {p} is missing");
            }
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[SceneSet] Build Settings: " + string.Join(", ", list.Select(x => Path.GetFileNameWithoutExtension(x.path))));
        }

        // ---- exporting a saved map ---------------------------------------------------------------------------------------------

        [MenuItem("Blindside/Scenes/Export Saved Map to Map Scene…")]
        public static void ExportSavedMap()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[SceneSet] leave Play mode first"); return; }
            string file = EditorUtility.OpenFilePanel("Export map to map scene", MapStore.Directory, "json");
            if (string.IsNullOrEmpty(file)) return;
            string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
            if (!MapFile.TryParse(json, out var map, out var why)) { EditorUtility.DisplayDialog("Export", "This map file can't be read:\n" + why, "OK"); return; }
            var r = MapValidator.Validate(map.ToModel());
            var errors = r.issues.Where(i => i.error).Select(i => i.text).ToList();
            if (errors.Count > 0) { EditorUtility.DisplayDialog("Export", "This map has required errors:\n" + string.Join("\n", errors), "OK"); return; }
            Export(map, json, Path.GetFileNameWithoutExtension(file));
        }

        /// <summary>Writes the map's data asset, its scene (a Battle copy, made once) and its catalog entry; returns the id.</summary>
        public static string Export(MapFile map, string json, string sourceStem)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            var s = Settings();
            string battle = PathOf(s.battleScene);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(battle) == null) BuildSceneSet();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(battle) == null) { Debug.LogError("[SceneSet] no " + battle); return null; }
            EnsureFolder(MapSceneDir); EnsureFolder(MapDataDir);

            var cat = Catalog();
            // the same source file exported again keeps its id and scene
            var entry = cat.entries.FirstOrDefault(e => e != null && e.sourceStem == sourceStem);
            if (entry == null)
            {
                entry = new MapCatalog.Entry { id = "map_" + System.Guid.NewGuid().ToString("N").Substring(0, 8), sourceStem = sourceStem };
                cat.entries.Add(entry);
            }
            entry.displayName = string.IsNullOrEmpty(map.name) ? sourceStem : map.name;
            entry.sceneName = entry.id;
            entry.listed = true;

            string dataPath = MapDataPathFor(entry.id);
            File.WriteAllText(dataPath, json, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(dataPath);
            entry.mapJson = AssetDatabase.LoadAssetAtPath<TextAsset>(dataPath);
            EditorUtility.SetDirty(cat);

            string back = EditorSceneManager.GetActiveScene().path;
            string scenePath = $"{MapSceneDir}/{entry.sceneName}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null && !AssetDatabase.CopyAsset(battle, scenePath))
            { Debug.LogError("[SceneSet] could not copy " + battle); return null; }
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var se = Object.FindAnyObjectByType<SceneEntry>(FindObjectsInactive.Include);
            if (se == null) se = new GameObject("[SceneEntry] Battle").AddComponent<SceneEntry>();
            se.role = SceneRole.Battle;
            se.mapJson = entry.mapJson;
            se.mapId = entry.id;
            se.gameObject.name = $"[SceneEntry] Battle — {entry.id}";
            EditorUtility.SetDirty(se);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            ApplyBuildSettings();
            if (!string.IsNullOrEmpty(back) && back != scenePath) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            Debug.Log($"[SceneSet] exported \"{entry.displayName}\" as {entry.id} → {scenePath}");
            return entry.id;
        }

        static string MapDataPathFor(string id) => $"{MapDataDir}/{id}.json";

        // ---- assets ---------------------------------------------------------------------------------------------------------------

        static SceneFlowSettings Settings()
        {
            var s = AssetDatabase.LoadAssetAtPath<SceneFlowSettings>(SettingsPath);
            if (s != null) return s;
            EnsureFolder(ResourcesDir);
            s = ScriptableObject.CreateInstance<SceneFlowSettings>();
            AssetDatabase.CreateAsset(s, SettingsPath);
            return s;
        }

        static MapCatalog Catalog()
        {
            var c = AssetDatabase.LoadAssetAtPath<MapCatalog>(CatalogPath);
            if (c != null) return c;
            EnsureFolder(ResourcesDir);
            c = ScriptableObject.CreateInstance<MapCatalog>();
            AssetDatabase.CreateAsset(c, CatalogPath);
            return c;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---- before every build -----------------------------------------------------------------------------------------------------

        class Preprocess : IPreprocessBuildWithReport
        {
            public int callbackOrder => -90;
            public void OnPreprocessBuild(BuildReport report)
            {
                var s = Settings();
                var scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToList();
                if (scenes.Count == 0 || scenes[0] != PathOf(s.titleScene))
                    throw new BuildFailedException($"[SceneSet] the first scene must be {PathOf(s.titleScene)} (Blindside > Scenes > Apply Build Settings)");
                if (scenes.Contains(s.mapEditorScenePath))
                    throw new BuildFailedException("[SceneSet] the map editor scene must not be in the build (Blindside > Scenes > Apply Build Settings)");
                foreach (var need in new[] { s.tutorialScene, s.battleScene })
                    if (!scenes.Contains(PathOf(need)))
                        throw new BuildFailedException($"[SceneSet] {PathOf(need)} is not in the build (Blindside > Scenes > Apply Build Settings)");
            }
        }
    }
}
