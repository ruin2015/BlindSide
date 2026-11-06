// Blindside > Scenes > Build Squad Hub — makes (or remakes) Assets/Blindside/Scenes/Hub.unity, the squad's HQ in third person, and
// adds it to the Build Settings. Everything is built from primitives (blockout — replace with art later) with URP Lit materials
// under Art/Hub:
//   the building 32 × 33 m, walls 4 m:  lobby (centre, the player starts here) · 作戦室 (north, the big screen and the table) ·
//   ショップ / 装備設定 / 隊員強化 (the west wing: counter, lockers, consoles) · 訓練所 (east: mats, targets) · south of the lobby a
//   corridor (z −14 … −11) to four rooms, 8 × 8 m each, a sign (HubSign) over every door:
//     医務室 (SERUM, medical items: beds, cabinets, curtain) · 訓練所 (BASTION, training: bench, racks, a heavy bag) · 多目的室 (nobody:
//     table, chairs, a whiteboard, crates) · 隊員・装備管理 (IGNIS, stat points and equipment: workbenches, gun racks, a terminal)
//   CRUSHER runs the 作戦室; the supply clerk the shop (装備購入). The operators open their station with F and talk with G. The west
//   lab / armory and the east range are scenery now
//   figures: sized and grounded on their baked mesh (SkinnedMeshRenderer.bounds is the import-time box, not the mesh), the toe bone's
//   height over the sole kept for HubFigure's foot grounding
//   the player:  FLARE's model (no gun: HubFigure hides gun nodes), CharacterController + HubPlayer, HubCamera behind
//   the stations: HubNpc on each; the shop clerk is a plain figure, the four operators their own models
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
            // the supply clerk keeps the shop (equipment); the operators run the rest (F: the station, G: talk)
            Staff(HubStation.Shop, "補給係", "装備購入", new Vector3(-14.2f, 0f, 1f), 90f, new Color(1f, .78f, .12f));
            Operator("CRUSHER", HubStation.Operations, "作戦室 · 出撃", new Vector3(0f, 0f, 8.6f), 180f, 3.4f);   // behind the table
            foreach (var r in Rooms)
                if (!string.IsNullOrEmpty(r.key)) Operator(r.key, r.station, r.caption, r.spot, r.yaw, 2.4f);
            foreach (var r in Rooms)
            {
                var s = new GameObject("Sign_" + r.name).AddComponent<HubSign>();
                s.transform.SetParent(world, false);
                s.transform.position = new Vector3(r.x, 3.1f, CorridorZ1 + .3f);
                s.displayName = r.name; s.caption = r.key ?? "";
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

        // ---- the operators' rooms ------------------------------------------------------------------------------------------------------
        const float CorridorZ0 = -11f, CorridorZ1 = -14f, RoomsZ1 = -22f, DoorW = 2.2f;

        sealed class Room
        {
            public string key;        // who runs it (null: nobody)
            public string name;       // the door sign
            public string caption;    // the station's caption over the operator
            public HubStation station;
            public float x;           // the room's centre (8 wide: x − 4 … x + 4)
            public Vector3 spot;      // where the operator stands
            public float yaw;
        }

        static readonly Room[] Rooms =
        {
            // each stands a little off the door line, turned toward the door (+z)
            new Room { key = "SERUM",   name = "医務室",         caption = "医務室 · 回復薬",   station = HubStation.Medical,    x = -12f, spot = new Vector3(-11.2f, 0f, -16.8f), yaw = -15f },
            new Room { key = "BASTION", name = "訓練所",         caption = "訓練所",            station = HubStation.Training,   x = -4f,  spot = new Vector3(-3.2f, 0f, -16.6f), yaw = -10f },
            new Room { key = null,      name = "多目的室",       caption = "",                  station = HubStation.Talk,       x = 4f },
            new Room { key = "IGNIS",   name = "隊員・装備管理", caption = "隊員・装備管理",    station = HubStation.Management, x = 12f,  spot = new Vector3(11.2f, 0f, -16.8f), yaw = 15f },
        };

        /// <summary>A squad operator at `pos` running `station` (F) and talkable (G).</summary>
        static void Operator(string key, HubStation station, string caption, Vector3 pos, float yaw, float radius)
        {
            var op = BuildAssetRegistry.Operator(key);
            var go = new GameObject("Op_" + key);
            go.transform.SetParent(world, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Figure(go.transform, op, op != null ? op.color : Color.gray);
            Blocker(go.transform);
            var n = go.AddComponent<HubNpc>();
            n.station = station; n.operatorKey = key;
            n.displayName = op != null ? op.DisplayName : key;
            n.caption = caption;
            n.radius = radius;
        }

        // ---- the building ------------------------------------------------------------------------------------------------------------
        static void Building()
        {
            float zMid = (11f + RoomsZ1) * .5f, zLen = 11f - RoomsZ1 + .6f;
            Box("Floor", new Vector3(0f, -.1f, zMid), new Vector3(32.6f, .2f, zLen), Mat("Floor", new Color(.24f, .26f, .29f), .25f));
            Box("Ceiling", new Vector3(0f, WallH + .1f, zMid), new Vector3(32.6f, .2f, zLen), Mat("Ceiling", new Color(.16f, .17f, .19f), .1f), shadows: false);

            // outer walls
            WallX(-16f, 16f, 11f); WallX(-16f, 16f, RoomsZ1);
            WallZ(-16f, RoomsZ1, 11f); WallZ(16f, RoomsZ1, 11f);
            // the lobby's south wall, its door to the corridor in the middle
            WallX(-16f, -1.6f, CorridorZ0); WallX(1.6f, 16f, CorridorZ0);
            // the rooms' front wall, a door into each, and the walls between them
            float left = -16f;
            foreach (var r in Rooms)
            {
                WallX(left, r.x - DoorW * .5f, CorridorZ1);
                left = r.x + DoorW * .5f;
            }
            WallX(left, 16f, CorridorZ1);
            WallZ(-8f, RoomsZ1, CorridorZ1); WallZ(0f, RoomsZ1, CorridorZ1); WallZ(8f, RoomsZ1, CorridorZ1);
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
            // the corridor and the rooms
            Box("DecoLine_Corridor", new Vector3(0f, .005f, (CorridorZ0 + CorridorZ1) * .5f), new Vector3(31f, .01f, .12f), line, shadows: false);
            for (int i = -12; i <= 12; i += 8) Lamp(new Vector3(i, 3.4f, -12.5f), new Color(1f, .95f, .88f), 6f, 1.1f);
            Lamp(new Vector3(-12f, 3.4f, -18f), new Color(.85f, 1f, .95f), 8f, 1.5f);   // 医務室: clinical white-green
            Lamp(new Vector3(-4f, 3.4f, -18f), new Color(1f, .92f, .8f), 8f, 1.5f);     // トレーニングルーム
            Lamp(new Vector3(4f, 3.4f, -18f), new Color(1f, .9f, .75f), 8f, 1.4f);      // 多目的室
            Lamp(new Vector3(12f, 3.4f, -18f), new Color(1f, .85f, .65f), 8f, 1.4f);    // 武器整備室: workshop amber
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
            // the lobby's south side: a bench by the corridor door
            Box("Sofa", new Vector3(-4.5f, .3f, -10.2f), new Vector3(3f, .6f, .9f), fabric);
            Box("Sofa", new Vector3(4.5f, .3f, -10.2f), new Vector3(3f, .6f, .9f), fabric);

            MedicalRoom(-12f); GymRoom(-4f); MultiRoom(4f); ArmoryRoom(12f);
        }

        // room interiors: x is the room's centre, the room spans x − 4 … x + 4, z −22 … −14 (its door at x, z −14)
        static void MedicalRoom(float x)
        {
            var white = Mat("MedWhite", new Color(.86f, .89f, .9f), .3f);
            var green = Mat("MedGreen", new Color(.45f, .72f, .62f), .2f);
            var metal = Mat("Metal", new Color(.4f, .43f, .47f), .55f);
            var cross = Mat("MedCross", new Color(.9f, .2f, .25f), .3f, new Color(.6f, .08f, .1f));
            Box("DecoFloor_Med", new Vector3(x, .006f, -18f), new Vector3(7.6f, .01f, 7.6f), Mat("MedFloor", new Color(.7f, .76f, .76f), .4f), shadows: false);
            for (int i = 0; i < 2; i++)
            {
                float bz = -20.6f + i * 0f, bx = x - 2.6f + i * 2.4f;
                Box("Bed", new Vector3(bx, .35f, bz), new Vector3(1f, .7f, 2.2f), metal);
                Box("DecoMattress", new Vector3(bx, .74f, bz), new Vector3(.92f, .1f, 2.1f), white);
                Box("DecoPillow", new Vector3(bx, .83f, bz - .8f), new Vector3(.6f, .1f, .35f), white);
                Box("DecoBlanket", new Vector3(bx, .8f, bz + .35f), new Vector3(.95f, .06f, 1.2f), green);
            }
            Box("DecoCurtain", new Vector3(x - 1.4f, 1.2f, -20.4f), new Vector3(.04f, 2.2f, 2.6f), green, shadows: false);
            Box("MedCabinet", new Vector3(x + 3.55f, 1f, -19.5f), new Vector3(.6f, 2f, 2f), white);
            Box("DecoCabinetGlass", new Vector3(x + 3.24f, 1.3f, -19.5f), new Vector3(.02f, 1.1f, 1.8f), Mat("Glass", new Color(.6f, .8f, .9f), .9f, new Color(.05f, .12f, .15f)), shadows: false);
            Box("DeskMed", new Vector3(x + 2.6f, .4f, -16.2f), new Vector3(1.4f, .8f, .7f), white);
            Box("DecoCross", new Vector3(x, 2.6f, -21.82f), new Vector3(.9f, .25f, .02f), cross, shadows: false);
            Box("DecoCross", new Vector3(x, 2.6f, -21.82f), new Vector3(.25f, .9f, .02f), cross, shadows: false);
        }

        static void GymRoom(float x)
        {
            var rubber = Mat("Rubber", new Color(.12f, .13f, .14f), .1f);
            var metal = Mat("Metal", new Color(.4f, .43f, .47f), .55f);
            var red = Mat("Target", new Color(.9f, .21f, .3f), .3f);
            var bag = Mat("Leather", new Color(.35f, .12f, .1f), .35f);
            Box("DecoFloor_Gym", new Vector3(x, .01f, -18f), new Vector3(7.6f, .02f, 7.6f), rubber, shadows: false);
            // bench press
            Box("Bench", new Vector3(x - 1.6f, .25f, -19.6f), new Vector3(.4f, .5f, 1.6f), Mat("Fabric", new Color(.2f, .23f, .28f), .05f));
            Box("BenchUpright", new Vector3(x - 1.6f, .7f, -20.3f), new Vector3(1.4f, 1.4f, .08f), metal);
            Box("Barbell", new Vector3(x - 1.6f, 1.25f, -20.25f), new Vector3(1.9f, .04f, .04f), metal);
            for (int s = -1; s <= 1; s += 2) Box("DecoPlate", new Vector3(x - 1.6f + s * .8f, 1.25f, -20.25f), new Vector3(.06f, .4f, .4f), rubber);
            // dumbbell rack
            Box("DumbbellRack", new Vector3(x + 3.4f, .45f, -18.5f), new Vector3(.6f, .9f, 3f), metal);
            for (int i = 0; i < 5; i++) Box("DecoDumbbell", new Vector3(x + 3.4f, .95f, -19.7f + i * .6f), new Vector3(.4f, .14f, .14f), rubber);
            // heavy bag
            Box("BagChain", new Vector3(x + 1.6f, 3.3f, -20.6f), new Vector3(.03f, 1.2f, .03f), metal, shadows: false);
            var heavy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            heavy.name = "HeavyBag"; heavy.transform.SetParent(world, false);
            heavy.transform.position = new Vector3(x + 1.6f, 1.95f, -20.6f); heavy.transform.localScale = new Vector3(.45f, .65f, .45f);
            heavy.GetComponent<Renderer>().sharedMaterial = bag; heavy.isStatic = true;
            Box("DecoStripe", new Vector3(x, 2.4f, -21.82f), new Vector3(6f, .12f, .02f), red, shadows: false);
        }

        static void MultiRoom(float x)
        {
            var wood = Mat("Wood", new Color(.36f, .27f, .2f), .2f);
            var metal = Mat("Metal", new Color(.4f, .43f, .47f), .55f);
            var fabric = Mat("Fabric", new Color(.2f, .23f, .28f), .05f);
            var board = Mat("Board", new Color(.93f, .94f, .95f), .6f);
            var orange = Mat("Ember", new Color(.95f, .45f, .15f), .3f, new Color(.4f, .12f, .02f));
            Box("Table", new Vector3(x, .38f, -18.6f), new Vector3(2.8f, .76f, 1.4f), wood);
            for (int i = 0; i < 3; i++)
            {
                Box("Chair", new Vector3(x - 1f + i, .23f, -19.7f), new Vector3(.45f, .46f, .45f), fabric);
                Box("Chair", new Vector3(x - 1f + i, .23f, -17.5f), new Vector3(.45f, .46f, .45f), fabric);
            }
            Box("Whiteboard", new Vector3(x, 1.6f, -21.8f), new Vector3(3.2f, 1.4f, .06f), board, shadows: false);
            Box("DecoBoardFrame", new Vector3(x, .88f, -21.76f), new Vector3(3.3f, .06f, .1f), metal, shadows: false);
            for (int i = 0; i < 4; i++) Box("Crate", new Vector3(x - 3.3f, .3f + (i / 2) * .6f, -20.8f + (i % 2) * .7f), new Vector3(.7f, .6f, .65f), i % 2 == 0 ? wood : metal);
            Box("DecoEmber", new Vector3(x + 3.82f, 1.6f, -18f), new Vector3(.02f, .5f, 3f), orange, shadows: false);
        }

        static void ArmoryRoom(float x)
        {
            var metal = Mat("Metal", new Color(.4f, .43f, .47f), .55f);
            var ink = Mat("Ink", new Color(.08f, .09f, .1f), .4f);
            var steel = Mat("Steel", new Color(.55f, .58f, .62f), .7f);
            var mat = Mat("WorkMat", new Color(.18f, .35f, .3f), .1f);
            Box("Workbench", new Vector3(x - 1.4f, .45f, -20.9f), new Vector3(2.4f, .9f, .9f), metal);
            Box("DecoWorkMat", new Vector3(x - 1.4f, .91f, -20.9f), new Vector3(2.2f, .02f, .8f), mat, shadows: false);
            Box("Workbench", new Vector3(x + 3.3f, .45f, -18.6f), new Vector3(.9f, .9f, 2.4f), metal);
            // gun racks on the back wall (silhouettes)
            Box("GunRack", new Vector3(x + 1.8f, 1.5f, -21.78f), new Vector3(2.6f, 1.8f, .1f), ink, shadows: false);
            for (int i = 0; i < 5; i++) Box("DecoRifle", new Vector3(x + .9f + i * .45f, 1.5f, -21.7f), new Vector3(.08f, 1.3f, .06f), steel, shadows: false);
            // BASTION's shield on its stand
            Box("ShieldStand", new Vector3(x - 3.2f, .5f, -17f), new Vector3(.5f, 1f, .5f), ink);
            Box("Shield", new Vector3(x - 3.2f, 1.45f, -17f), new Vector3(.9f, 1.3f, .1f), steel);
            Box("DecoShieldVisor", new Vector3(x - 3.2f, 1.75f, -16.94f), new Vector3(.4f, .1f, .02f), Mat("Screen", new Color(.05f, .2f, .35f), .8f, new Color(.1f, .5f, .9f) * 1.6f), shadows: false);
            for (int i = 0; i < 3; i++) Box("AmmoCase", new Vector3(x - 3.3f, .2f + i * .4f, -20.6f), new Vector3(.8f, .38f, .5f), i % 2 == 0 ? ink : metal);
            // the squad terminal (stats) beside the bench
            var screen = Mat("Screen", new Color(.05f, .2f, .35f), .8f, new Color(.1f, .5f, .9f) * 1.6f);
            Box("Terminal", new Vector3(x + 3.4f, .55f, -16.2f), new Vector3(.7f, 1.1f, .9f), ink);
            Box("DecoTerminalScreen", new Vector3(x + 3.02f, 1.35f, -16.2f), new Vector3(.04f, .6f, .8f), screen, shadows: false);
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
            // size and ground on the real (baked) mesh in the bind pose
            var b = Bounds(m);
            if (b.size.y > 1e-3f) m.transform.localScale *= PersonH / b.size.y;
            b = Bounds(m);
            m.transform.position += Vector3.up * (parent.position.y - b.min.y);
            var f = parent.gameObject.AddComponent<HubFigure>();
            var an = m.GetComponentInChildren<Animator>();
            var toe = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.LeftToes) : null;
            if (toe != null) f.toeHeight = Mathf.Clamp(toe.position.y - parent.position.y, 0f, .2f);
            f.profile = op.animationProfile;
            f.idle = Clip(IdleClipPath);
            f.walk = Clip(WalkClipPath);
            f.run = Clip(RunClipPath);
        }

        // the HQ's own idle / walk / run (the battle keeps the profiles' clips)
        const string IdleClipPath = "Assets/3DModels/AnimationPack/Neutral Idle.fbx";
        const string WalkClipPath = "Assets/3DModels/AnimationPack/Walking.fbx", RunClipPath = "Assets/3DModels/AnimationPack/Running.fbx";

        /// <summary>Blindside > Scenes > Set Up HQ Clips — the HQ clips as Humanoid loops (their own avatar, like the AnimationPack's
        /// RifleRun): root rotation and height baked into the pose, the XZ travel left to the root (dropped: applyRootMotion is off, so
        /// they play in place). Run again after replacing one of the FBX files.</summary>
        [MenuItem("Blindside/Scenes/Set Up HQ Clips")]
        public static void SetUpClips()
        {
            SetUpClip(IdleClipPath, "HubIdle");
            SetUpClip(WalkClipPath, "Walking");
            SetUpClip(RunClipPath, "Running");
        }

        static void SetUpClip(string path, string clipName)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[Hub] missing " + path); return; }
            if (imp.animationType != ModelImporterAnimationType.Human || imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.SaveAndReimport();
                imp = (ModelImporter)AssetImporter.GetAtPath(path);
            }
            var src = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            if (src.Length == 0) { Debug.LogWarning("[Hub] no take in " + path); return; }
            var c = src[0];
            c.name = clipName; c.loopTime = true; c.loopPose = false;
            c.lockRootRotation = true; c.keepOriginalOrientation = true;
            c.lockRootHeightY = true; c.keepOriginalPositionY = true;
            c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = true;
            c.heightFromFeet = false;
            imp.clipAnimations = new[] { c };
            imp.SaveAndReimport();
        }

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
            n.radius = st == HubStation.Shop ? 3f : 2.4f;   // the shop clerk stands behind the counter
        }

        /// <summary>A capsule the player can't walk through.</summary>
        static void Blocker(Transform t)
        {
            var c = t.gameObject.AddComponent<CapsuleCollider>();
            c.height = PersonH; c.radius = .3f; c.center = new Vector3(0f, PersonH * .5f, 0f);
        }

        /// <summary>The world bounds of `g`'s visible geometry as it is posed now: skinned meshes baked (their `bounds` is the import-time
        /// box, ~0.2 m off for these models), other renderers by their bounds.</summary>
        static Bounds Bounds(GameObject g)
        {
            Bounds b = default; bool any = false;
            void Add(Vector3 p) { if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p); }
            var baked = new Mesh();
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    smr.BakeMesh(baked, false);            // renderer space without its scale: TransformPoint adds the full TRS
                    var tr = smr.transform;
                    foreach (var v in baked.vertices) Add(tr.TransformPoint(v));
                }
                else { Add(r.bounds.min); Add(r.bounds.max); }
            }
            Object.DestroyImmediate(baked);
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
