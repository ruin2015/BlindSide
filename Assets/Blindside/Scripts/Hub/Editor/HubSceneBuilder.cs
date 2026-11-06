// Blindside > Scenes > Build Squad Hub — makes (or remakes) Assets/Blindside/Scenes/Hub.unity, the squad's HQ in third person, and
// adds it to the Build Settings. Everything is built from primitives (blockout — replace with art later) with URP Lit materials
// under Art/Hub:
//   the building 32 × 22 m, walls 4 m:  lobby (centre, the player starts here) · 作戦室 (north, the big screen and the table) ·
//   ショップ / 装備設定 / 隊員強化 (the west wing: counter, lockers, consoles) · 訓練所 (east: mats, targets) · the lounge (south: sofas,
//   where CRUSHER / SERUM / IGNIS / BASTION stand)
//   the player:  FLARE's model (no gun: HubFigure hides gun nodes), CharacterController + HubPlayer, HubCamera behind
//   the stations: HubNpc on each; staff NPCs are plain figures, the four operators their own models
//   [SceneEntry] Hub, HubDirector, a menu canvas (UIThemeCanvas) and an EventSystem (Input System module)
// Remaking replaces the scene file (the scene is generated: dress it after the layout settles, or keep the builder in sync).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Flow;
using Blindside.Flow.EditorTools;
using Blindside.UI;

namespace Blindside.Hub.EditorTools
{
    public static class HubSceneBuilder
    {
        const string ScenePath = "Assets/Blindside/Scenes/Hub.unity";
        const string MatDir = "Assets/Blindside/Art/Hub";
        const float WallH = 4f, WallT = .3f, PersonH = 1.75f;

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Transform world;

        [MenuItem("Blindside/Scenes/Build Squad Hub")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Hub] leave Play mode first"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Build Squad Hub", ScenePath + " exists. Rebuild it from scratch?", "Rebuild", "Cancel")) return;
            Make();
        }

        /// <summary>Builds and saves the scene with no questions (tools).</summary>
        public static void Make()
        {
            string back = EditorSceneManager.GetActiveScene().path;
            EnsureFolder(MatDir);
            mats.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var entry = new GameObject("[SceneEntry] Hub").AddComponent<SceneEntry>();
            entry.role = SceneRole.Hub;

            Lighting();
            world = new GameObject("HubWorld").transform;
            Building();
            Props();

            // player
            var flare = BuildAssetRegistry.Operator("FLARE");
            var player = new GameObject("Player_FLARE");
            player.transform.SetPositionAndRotation(new Vector3(0f, 0f, -1.5f), Quaternion.identity);
            var cc = player.AddComponent<CharacterController>();
            cc.height = PersonH; cc.radius = .3f; cc.center = new Vector3(0f, PersonH * .5f + .02f, 0f); cc.stepOffset = .3f;
            Figure(player.transform, flare, new Color(.85f, .45f, .2f));
            var hp = player.AddComponent<HubPlayer>();
            hp.figure = player.GetComponentInChildren<HubFigure>();

            // camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = .1f; cam.farClipPlane = 200f; cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.05f, .06f, .08f);
            camGo.AddComponent<AudioListener>();
            var hc = camGo.AddComponent<HubCamera>();
            hc.target = player.transform; hc.yaw = 0f;
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 2.8f, -5.5f), Quaternion.Euler(18f, 0f, 0f));
            hp.view = camGo.transform;

            // stations
            Staff(HubStation.Operations, "作戦オペレーター", "作戦室 · 出撃", new Vector3(0f, 0f, 8.6f), 180f, new Color(.12f, .59f, .91f));
            Staff(HubStation.Shop, "補給係", "ショップ", new Vector3(-14.2f, 0f, 1f), 90f, new Color(1f, .78f, .12f));
            Staff(HubStation.Loadout, "武器係", "装備設定", new Vector3(-14.2f, 0f, 8f), 90f, new Color(.55f, .6f, .66f));
            Staff(HubStation.Growth, "分析官", "隊員強化", new Vector3(-14.2f, 0f, -7f), 90f, new Color(.4f, .85f, .55f));
            Staff(HubStation.Training, "訓練教官", "訓練所", new Vector3(10.8f, 0f, 0f), -90f, new Color(.9f, .21f, .3f));
            var spots = new[] { new Vector3(-3.2f, 0f, -7.4f), new Vector3(-1.1f, 0f, -8.6f), new Vector3(1.3f, 0f, -8.4f), new Vector3(3.4f, 0f, -7.2f) };
            for (int i = 0; i < HubCatalog.TalkKeys.Length; i++)
            {
                var op = BuildAssetRegistry.Operator(HubCatalog.TalkKeys[i]);
                var go = new GameObject("Talk_" + HubCatalog.TalkKeys[i]);
                go.transform.SetParent(world, false);
                go.transform.SetPositionAndRotation(spots[i], Quaternion.Euler(0f, 180f + (spots[i].x * -8f), 0f));
                Figure(go.transform, op, op != null ? op.color : Color.gray);
                Blocker(go.transform);
                var n = go.AddComponent<HubNpc>();
                n.station = HubStation.Talk; n.operatorKey = HubCatalog.TalkKeys[i];
                n.displayName = op != null ? op.DisplayName : HubCatalog.TalkKeys[i];
                n.caption = "会話";
                n.radius = 1.9f;
            }

            // UI
            var canvasGo = new GameObject("HubCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<UIThemeCanvas>().kind = UITheme.CanvasKind.Menu;
            canvasGo.AddComponent<GraphicRaycaster>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var dir = new GameObject("HubDirector").AddComponent<HubDirector>();
            dir.player = player.transform; dir.canvas = canvas; dir.view = cam;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            SceneSetBuilder.ApplyBuildSettings();
            Debug.Log("[Hub] built " + ScenePath);
            if (!string.IsNullOrEmpty(back) && back != ScenePath) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        }

        // ---- lighting --------------------------------------------------------------------------------------------------------------
        static void Lighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.42f, .46f, .52f);
            RenderSettings.ambientEquatorColor = new Color(.3f, .32f, .36f);
            RenderSettings.ambientGroundColor = new Color(.14f, .15f, .17f);
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.color = new Color(1f, .96f, .9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        }

        static void Lamp(Vector3 p, Color c, float range, float intensity)
        {
            var l = new GameObject("DecoLamp").AddComponent<Light>();
            l.transform.SetParent(world, false);
            l.transform.position = p;
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        }

        // ---- the building ------------------------------------------------------------------------------------------------------------
        static void Building()
        {
            Box("Floor", new Vector3(0f, -.1f, 0f), new Vector3(32.6f, .2f, 22.6f), Mat("Floor", new Color(.24f, .26f, .29f), .25f));
            Box("Ceiling", new Vector3(0f, WallH + .1f, 0f), new Vector3(32.6f, .2f, 22.6f), Mat("Ceiling", new Color(.16f, .17f, .19f), .1f), shadows: false);

            // outer walls
            WallX(-16f, 16f, 11f); WallX(-16f, 16f, -11f);
            WallZ(-16f, -11f, 11f); WallZ(16f, -11f, 11f);
            // the west wing (x = −9), doors to 隊員強化 / ショップ / 装備設定
            WallZ(-9f, -11f, -8.2f); WallZ(-9f, -5.8f, -.2f); WallZ(-9f, 2.2f, 6.8f); WallZ(-9f, 9.2f, 11f);
            WallX(-16f, -9f, -3f); WallX(-16f, -9f, 5f);
            // 作戦室 (x −6 … 6, z 5 … 11), its door in the middle
            WallZ(-6f, 5f, 11f); WallZ(6f, 5f, 11f);
            WallX(-6f, -1.6f, 5f); WallX(1.6f, 6f, 5f);
            // 訓練所 (x 9 … 16), a wide opening
            WallZ(9f, -11f, -2.5f); WallZ(9f, 2.5f, 11f);

            // floor markings
            var line = Mat("Line", new Color(.12f, .59f, .91f), .3f, new Color(.05f, .3f, .5f));
            Box("DecoLine_Lobby", new Vector3(0f, .005f, -1.5f), new Vector3(.12f, .01f, 13f), line, shadows: false);
            Box("DecoLine_West", new Vector3(-4.5f, .005f, 1f), new Vector3(9f, .01f, .12f), line, shadows: false);
            Box("DecoLine_East", new Vector3(4.5f, .005f, 0f), new Vector3(9f, .01f, .12f), line, shadows: false);
            var hazard = Mat("Hazard", new Color(1f, .78f, .12f), .3f);
            Box("DecoHazard_Training", new Vector3(9.4f, .006f, 0f), new Vector3(.5f, .01f, 5f), hazard, shadows: false);

            Lamp(new Vector3(0f, 3.4f, -2f), new Color(1f, .95f, .88f), 12f, 1.6f);
            Lamp(new Vector3(0f, 3.4f, 8f), new Color(.6f, .8f, 1f), 9f, 1.4f);
            Lamp(new Vector3(-12.5f, 3.4f, 1f), new Color(1f, .9f, .7f), 7f, 1.4f);
            Lamp(new Vector3(-12.5f, 3.4f, 8f), new Color(1f, .95f, .9f), 7f, 1.2f);
            Lamp(new Vector3(-12.5f, 3.4f, -7f), new Color(.7f, 1f, .8f), 7f, 1.2f);
            Lamp(new Vector3(12.5f, 3.4f, 0f), new Color(1f, .95f, .9f), 12f, 1.6f);
            Lamp(new Vector3(0f, 3.4f, -8.5f), new Color(1f, .85f, .7f), 8f, 1.3f);
        }

        static void WallX(float x0, float x1, float z) =>
            Box("Wall", new Vector3((x0 + x1) * .5f, WallH * .5f, z), new Vector3(Mathf.Abs(x1 - x0) + WallT, WallH, WallT), WallMat());

        static void WallZ(float x, float z0, float z1) =>
            Box("Wall", new Vector3(x, WallH * .5f, (z0 + z1) * .5f), new Vector3(WallT, WallH, Mathf.Abs(z1 - z0) + WallT), WallMat());

        static Material WallMat() => Mat("Wall", new Color(.62f, .65f, .69f), .15f);

        static void Props()
        {
            var ink = Mat("Ink", new Color(.08f, .09f, .1f), .4f);
            var metal = Mat("Metal", new Color(.4f, .43f, .47f), .55f);
            var screen = Mat("Screen", new Color(.05f, .2f, .35f), .8f, new Color(.1f, .5f, .9f) * 1.6f);
            var fabric = Mat("Fabric", new Color(.2f, .23f, .28f), .05f);
            var target = Mat("Target", new Color(.9f, .21f, .3f), .3f);
            var wood = Mat("Wood", new Color(.36f, .27f, .2f), .2f);

            // 作戦室: the screen wall and the table
            Box("OpsScreen", new Vector3(0f, 2.1f, 10.8f), new Vector3(6f, 2.2f, .1f), screen, shadows: false);
            Box("OpsTable", new Vector3(0f, .45f, 7.2f), new Vector3(3.6f, .9f, 1.6f), ink);
            Box("DecoOpsTableTop", new Vector3(0f, .91f, 7.2f), new Vector3(3.3f, .02f, 1.3f), screen, shadows: false);
            // ショップ: the counter and shelves
            Box("ShopCounter", new Vector3(-12.6f, .5f, 1f), new Vector3(.8f, 1f, 4f), wood);
            for (int i = 0; i < 3; i++) Box("ShopShelf", new Vector3(-15.6f, .7f + i * .9f, 1f), new Vector3(.5f, .06f, 5f), metal);
            for (int i = 0; i < 8; i++) Box("DecoCrate", new Vector3(-15.55f, .95f + (i % 3) * .9f, -1.2f + i * .6f), new Vector3(.35f, .3f, .4f), i % 2 == 0 ? fabric : metal);
            // 装備設定: lockers and the weapon rack
            for (int i = 0; i < 6; i++) Box("Locker", new Vector3(-15.6f, 1f, 5.9f + i * .85f), new Vector3(.6f, 2f, .8f), i % 2 == 0 ? metal : ink);
            Box("Bench", new Vector3(-12.2f, .25f, 8f), new Vector3(.5f, .5f, 3f), wood);
            // 隊員強化: consoles
            for (int i = 0; i < 3; i++)
            {
                Box("Console", new Vector3(-15.4f, .5f, -9.5f + i * 2.2f), new Vector3(.9f, 1f, 1.4f), ink);
                Box("DecoConsoleScreen", new Vector3(-15.75f, 1.6f, -9.5f + i * 2.2f), new Vector3(.06f, .8f, 1.2f), screen, shadows: false);
            }
            // 訓練所: mats and targets
            Box("DecoMat", new Vector3(12.5f, .02f, -6f), new Vector3(5f, .04f, 6f), fabric, shadows: false);
            for (int i = 0; i < 4; i++)
            {
                Box("TargetPost", new Vector3(15.2f, .7f, 3.5f + i * 1.8f), new Vector3(.08f, 1.4f, .08f), metal);
                Box("Target", new Vector3(15.2f, 1.45f, 3.5f + i * 1.8f), new Vector3(.06f, .9f, .6f), target);
            }
            Box("Barrier", new Vector3(11.5f, .5f, 6f), new Vector3(.4f, 1f, 4f), metal);
            // the lounge: sofas and a low table
            Box("Sofa", new Vector3(-3.5f, .3f, -10.2f), new Vector3(3f, .6f, .9f), fabric);
            Box("Sofa", new Vector3(3.5f, .3f, -10.2f), new Vector3(3f, .6f, .9f), fabric);
            Box("LowTable", new Vector3(0f, .22f, -9.6f), new Vector3(1.6f, .44f, .9f), wood);
        }

        // ---- figures -----------------------------------------------------------------------------------------------------------------
        /// <summary>`op`'s model under `parent` (scaled to PersonH, feet on the floor, gun hidden, no colliders) with a HubFigure; a plain
        /// figure in `color` when there is no model.</summary>
        static void Figure(Transform parent, OperatorData op, Color color)
        {
            var prefab = op != null ? op.modelPrefab : null;
            if (prefab == null) { PlainFigure(parent, color); return; }
            var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            m.name = "Model_" + prefab.name;
            m.transform.SetParent(parent, false);
            m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
            foreach (var c in m.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            HubFigure.HideGuns(m.transform);
            var b = Bounds(m);
            if (b.size.y > 1e-3f) m.transform.localScale *= PersonH / b.size.y;
            b = Bounds(m);
            m.transform.position += Vector3.up * (parent.position.y - b.min.y);
            var f = parent.gameObject.AddComponent<HubFigure>();
            f.profile = op.animationProfile;
            f.walk = Clip(WalkClipPath);
            f.run = Clip(RunClipPath);
        }

        // the HQ's own walk / run (the battle keeps the profiles' clips)
        const string WalkClipPath = "Assets/3DModels/AnimationPack/Walking.fbx", RunClipPath = "Assets/3DModels/AnimationPack/Running.fbx";

        static AnimationClip Clip(string fbx)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            Debug.LogWarning("[Hub] no clip in " + fbx);
            return null;
        }

        static void PlainFigure(Transform parent, Color color)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, .78f, 0f);
            body.transform.localScale = new Vector3(.55f, .78f, .4f);
            body.GetComponent<Renderer>().sharedMaterial = Mat("Uniform", new Color(.16f, .19f, .24f), .2f);
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.transform.SetParent(parent, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            head.transform.localScale = Vector3.one * .3f;
            head.GetComponent<Renderer>().sharedMaterial = Mat("Skin", new Color(.78f, .62f, .5f), .2f);
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = "DecoArmband";
            Object.DestroyImmediate(band.GetComponent<Collider>());
            band.transform.SetParent(parent, false);
            band.transform.localPosition = new Vector3(0f, 1.12f, .17f);
            band.transform.localScale = new Vector3(.4f, .1f, .08f);
            band.GetComponent<Renderer>().sharedMaterial = Mat("Mark_" + ColorUtility.ToHtmlStringRGB(color), color, .3f, color * .6f);
            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "DecoVisor";
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.transform.SetParent(parent, false);
            visor.transform.localPosition = new Vector3(0f, 1.63f, .12f);
            visor.transform.localScale = new Vector3(.24f, .06f, .1f);
            visor.GetComponent<Renderer>().sharedMaterial = Mat("Ink", new Color(.08f, .09f, .1f), .4f);
        }

        static void Staff(HubStation st, string name, string caption, Vector3 pos, float yaw, Color mark)
        {
            var go = new GameObject("Staff_" + st);
            go.transform.SetParent(world, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            PlainFigure(go.transform, mark);
            Blocker(go.transform);
            var n = go.AddComponent<HubNpc>();
            n.station = st; n.displayName = name; n.caption = caption;
            // behind a counter / table / bench: reachable from its far side
            n.radius = st == HubStation.Operations ? 3.4f : st == HubStation.Shop || st == HubStation.Loadout || st == HubStation.Growth ? 3f : 2.4f;
        }

        /// <summary>A capsule the player can't walk through.</summary>
        static void Blocker(Transform t)
        {
            var c = t.gameObject.AddComponent<CapsuleCollider>();
            c.height = PersonH; c.radius = .3f; c.center = new Vector3(0f, PersonH * .5f, 0f);
        }

        static Bounds Bounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            Bounds b = default; bool any = false;
            foreach (var r in rs)
            {
                if (!r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ---- helpers ------------------------------------------------------------------------------------------------------------------
        static GameObject Box(string name, Vector3 pos, Vector3 size, Material m, bool shadows = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(world, false);
            g.transform.position = pos; g.transform.localScale = size;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            if (!shadows) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; Object.DestroyImmediate(g.GetComponent<Collider>()); }
            g.isStatic = true;
            return g;
        }

        static Material Mat(string name, Color c, float smooth, Color? emission = null)
        {
            if (mats.TryGetValue(name, out var have)) return have;
            string path = MatDir + "/Hub_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c); m.color = c;
            m.SetFloat("_Smoothness", smooth);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
