// Render Previews / Lint states of the squad HQ's screen (no 3D: a plain backdrop behind the menu canvas), on a sample save
// (¥58,000, CRUSHER Lv 3 with 4 points, SCAR bought; parts by count: a suppressor, two 4x scopes, a vertical grip; two frag grenades
// and a 下級医療キット; IGNIS trained a little; FLARE's MX50 carries the suppressor and one scope — one scope and the grip are left):
//   hub_overlay       the topbar, a few floating labels and the prompt (CRUSHER's 作戦室 in reach: F + G 話す)
//   hub_shop          装備購入 — 武器
//   hub_shop_attach   装備購入 — アタッチメント
//   hub_shop_gadget   装備購入 — ガジェット
//   hub_shop_buy      装備購入 — the purchase dialog (4x scope ×3)
//   hub_medical       医務室 (SERUM)
//   hub_training      訓練所 (BASTION; IGNIS's tab)
//   hub_operations    作戦室 (CRUSHER)
//   hub_manage        IGNIS: the choice (ロードアウト / 装備購入)
//   hub_loadout       ロードアウト (FLARE's tab: SCAR owned, suppressor on, a frag carried)
//   hub_menu          the ESC menu
//   hub_squad         隊員情報 over the menu (CRUSHER: Lv 3, 4 points to give)
//   (hub_overlay also shows the minimap and the place name)
//   hub_talk          会話 (CRUSHER)
using UnityEngine;
using Blindside.Data;
using Blindside.Hub;
using Blindside.Save;
using Blindside.UITools.EditorTools;

namespace Blindside.UI.EditorTools
{
    public static class UIPreviewHub
    {
        public const string Overlay = "hub_overlay", Shop = "hub_shop", ShopAttach = "hub_shop_attach", ShopGadget = "hub_shop_gadget",
            Medical = "hub_medical", Training = "hub_training", Operations = "hub_operations", Manage = "hub_manage", Talk = "hub_talk",
            LoadoutState = "hub_loadout", Menu = "hub_menu", Squad = "hub_squad";

        static SaveData Sample()
        {
            var d = SaveData.Fresh();
            d.tutorialDone = true; d.money = 58000; d.rankPoints = 900;
            var c = d.OperatorOf("CRUSHER"); c.level = 3; c.points = 4; c.hp = 1; c.atk = 1;
            var i = d.OperatorOf("IGNIS"); i.level = 2; i.points = 2; i.trained[2] = 2; i.atk = 2; i.trained[0] = 1; i.hp = 1;
            d.ownedWeapons.Add("SCAR");
            d.AttachmentStockOf("suppressor").count = 1; d.AttachmentStockOf("scope4x").count = 2; d.AttachmentStockOf("vgrip").count = 1;
            d.GadgetOf("frag").count = 2;
            d.GadgetOf("kit_low").count = 1;
            var f = d.OperatorOf("FLARE"); f.gadget = "frag";
            var fit = f.FitOf("MX50"); fit.parts[0] = "suppressor"; fit.parts[1] = "scope4x";   // (FLARE's first gun)
            d.RecordOf("").plays = 3; d.RecordOf("").wins = 2;
            return d;
        }

        static HubScreen Screen(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("Hub_Canvas", UITheme.CanvasKind.Menu, 10);
            var bg = HudKit.NewRect("DecoBackdrop", canvas.transform);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, new Color32(0x3a, 0x3f, 0x46, 0xff));
            var scr = new GameObject("HubScreenHost", typeof(RectTransform));
            var rt = (RectTransform)scr.transform;
            rt.SetParent(canvas.transform, false);
            HudKit.Stretch(rt);
            var hs = HubScreen.Build(rt, s.cam);
            hs.previewData = Sample();
            hs.RefreshTopbar();
            return hs;
        }

        public static void BuildOverlay(UIPreview.Stage s)
        {
            var hs = Screen(s);
            hs.PosePrompt("作戦室 · 出撃 — CRUSHER", true);
            hs.PoseLabel("CRUSHER", "作戦室 · 出撃", new Vector2(720f, 470f));
            hs.PoseLabel("補給係", "装備購入", new Vector2(380f, 420f));
            hs.PoseLabel("訓練所", "BASTION", new Vector2(1100f, 420f));
            hs.SetLocation("作戦室");
            PoseMinimap(hs);
        }

        /// <summary>The minimap on the HQ's floor plan (the walls as the builder lays them; stations as marker objects; the player in the
        /// 作戦室, heading north-east). The marker objects are left in the stage scene, thrown away with it.</summary>
        static void PoseMinimap(HubScreen hs)
        {
            var walls = new System.Collections.Generic.List<Bounds>();
            void X(float x0, float x1, float z) => walls.Add(new Bounds(new Vector3((x0 + x1) * .5f, 2f, z), new Vector3(x1 - x0 + .3f, 4f, .3f)));
            void Z(float x, float z0, float z1) => walls.Add(new Bounds(new Vector3(x, 2f, (z0 + z1) * .5f), new Vector3(.3f, 4f, z1 - z0 + .3f)));
            X(-16f, 16f, 11f); X(-16f, -1.6f, -11f); X(1.6f, 16f, -11f); Z(-16f, -11f, 11f); Z(16f, -11f, 11f);
            Z(-4f, -19f, -11f); Z(4f, -19f, -11f); X(-4f, 4f, -19f);
            Z(-9f, -11f, -8.2f); Z(-9f, -5.8f, -.2f); Z(-9f, 2.2f, 6.8f); Z(-9f, 9.2f, 11f); X(-16f, -9f, -3f); X(-16f, -9f, 5f);
            Z(-6f, 5f, 11f); Z(6f, 5f, 11f); X(-6f, -1.6f, 5f); X(1.6f, 6f, 5f); Z(9f, -11f, -2.5f); Z(9f, 2.5f, 11f);
            var npcs = new System.Collections.Generic.List<HubNpc>();
            HubNpc Npc(string n, HubStation st, Vector3 p) { var go = new GameObject("PreviewNpc_" + n); go.transform.position = p; var c = go.AddComponent<HubNpc>(); c.displayName = n; c.station = st; npcs.Add(c); return c; }
            Npc("CRUSHER", HubStation.Operations, new Vector3(0f, 0f, 8.6f));
            Npc("SERUM", HubStation.Medical, new Vector3(-13.4f, 0f, -7f));
            Npc("補給係", HubStation.Shop, new Vector3(-14.2f, 0f, 1f));
            Npc("IGNIS", HubStation.Management, new Vector3(-13f, 0f, 8.4f));
            Npc("BASTION", HubStation.Training, new Vector3(12.4f, 0f, -1.2f));
            var areas = new System.Collections.Generic.List<HubArea>();
            HubArea Area(string n, float x0, float x1, float z0, float z1) { var go = new GameObject("PreviewArea_" + n); go.transform.position = new Vector3((x0 + x1) * .5f, 0f, (z0 + z1) * .5f); var a = go.AddComponent<HubArea>(); a.displayName = n; a.size = new Vector2(x1 - x0, z1 - z0); areas.Add(a); return a; }
            Area("医務室", -16f, -9f, -11f, -3f); Area("装備購入", -16f, -9f, -3f, 5f); Area("隊員・装備管理", -16f, -9f, 5f, 11f);
            Area("作戦室", -6f, 6f, 5f, 11f); Area("訓練所", 9f, 16f, -11f, 11f); Area("炊事場", -4f, 4f, -19f, -11f); Area("電子戦室", 5f, 13f, -19f, -11f);
            var player = new GameObject("PreviewPlayer").transform;
            player.position = new Vector3(1.5f, 0f, 6.2f); player.rotation = Quaternion.Euler(0f, 40f, 0f);
            hs.BuildMinimap(walls, npcs, areas, player);
        }

        public static void BuildShop(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Shop, 0);
        public static void BuildShopAttach(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Shop, 1);
        public static void BuildShopGadget(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Shop, 2);
        /// <summary>hub_shop_buy: アタッチメント, the 4x scope picked, 「何個購入しますか？」 at 3.</summary>
        public static void BuildShopBuy(UIPreview.Stage s) => Screen(s).Shop.Pose(false, 1, 2, 3);
        public const string ShopBuy = "hub_shop_buy";
        public static void BuildMedical(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Medical, 0);
        public static void BuildTraining(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Training, 2);
        public static void BuildOperations(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Operations, 0);
        public static void BuildManage(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Management, 0);
        public static void BuildLoadout(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Loadout, 1);
        public static void BuildSquad(UIPreview.Stage s) { var hs = Screen(s); hs.Menu.Open(); hs.OpenStation(HubStation.Growth, 0); }
        public static void BuildMenu(UIPreview.Stage s) => Screen(s).Menu.Open();
        /// <summary>hub_bench: the workbench on FLARE's MX50 (suppressor + 4x scope fitted); hub_bench_pick: its 光学機器 picker open.</summary>
        public static void BuildBench(UIPreview.Stage s) => Screen(s).LoadoutScreen.Pose(1, -1);
        public static void BuildBenchPick(UIPreview.Stage s) => Screen(s).LoadoutScreen.Pose(1, 1);
        public const string Bench = "hub_bench", BenchPick = "hub_bench_pick", BenchEnter = "hub_bench_enter";
        /// <summary>hub_bench_enter: the loadout's entrance .95 s in — the gun just landed, the first slots coming out of it.</summary>
        public static void BuildBenchEnter(UIPreview.Stage s) => Screen(s).LoadoutScreen.PoseEntrance(1, .95f);
        /// <summary>hub_armory: ロードアウト — the armoury room with FLARE on the plate, the squad list and the 銃 / ガジェット tiles.</summary>
        public static void BuildArmory(UIPreview.Stage s) => Screen(s).Armory.Pose(1);
        /// <summary>hub_armory_op: FLARE chosen — its card and the 銃 / ガジェット tiles.</summary>
        public static void BuildArmoryOp(UIPreview.Stage s) => Screen(s).Armory.PoseOperator(1);
        public const string ArmoryOp = "hub_armory_op";
        public const string Armory = "hub_armory", ArmoryGun = "hub_armory_gun", ArmoryPart = "hub_armory_part";
        /// <summary>hub_armory_gun: FLARE's 銃 — guns and slots; hub_armory_part: its 光学機器 picker open (the camera on the rail).</summary>
        public static void BuildArmoryGun(UIPreview.Stage s) => Screen(s).Armory.PoseGun(1, -1);
        public static void BuildArmoryPart(UIPreview.Stage s) => Screen(s).Armory.PoseGun(1, 1);
        /// <summary>hub_armory_gadget: FLARE's gadgets, slot 1's picker open (the low shot, FLARE looking back down at it).</summary>
        public static void BuildArmoryGadget(UIPreview.Stage s) => Screen(s).Armory.PoseGadget(1, 0);
        public const string ArmoryGadget = "hub_armory_gadget";

        /// <summary>hub_armory_gadget_held: SERUM's item slot 1 — both frag grenades carried (FLARE and CRUSHER): the row says who has it,
        /// 所持 0; hub_armory_swap: SERUM taking one — 付け替えますか？</summary>
        public const string ArmoryGadgetHeld = "hub_armory_gadget_held", ArmorySwap = "hub_armory_swap";
        static HubScreen HeldScreen(UIPreview.Stage s)
        {
            var hs = Screen(s);
            hs.previewData.OperatorOf("CRUSHER").gadget = "frag";
            return hs;
        }
        public static void BuildArmoryGadgetHeld(UIPreview.Stage s) => HeldScreen(s).Armory.PoseGadget(3, 0);
        public static void BuildArmorySwap(UIPreview.Stage s) { var a = HeldScreen(s).Armory; a.PoseGadget(3, 0); a.PoseTake("frag"); }

        /// <summary>hub_sortie_squad5: 出撃メンバー編成 for the third main quest (5 to go: the wedge, slot 2 chosen, the roster open);
        /// hub_sortie_squad3: the second (3 to go, its captives left out).</summary>
        public const string SortieSquad5 = "hub_sortie_squad5", SortieSquad3 = "hub_sortie_squad3";
        public static void BuildSortieSquad5(UIPreview.Stage s) => Screen(s).SquadPick.Pose(MainQuest(3), 1);
        public static void BuildSortieSquad3(UIPreview.Stage s) => Screen(s).SquadPick.Pose(MainQuest(2), 2);
        static Blindside.Progress.Quest MainQuest(int order)
        {
            foreach (var q in Blindside.Progress.Campaign.Mains()) if (q.order == order) return q;
            return null;
        }

        // the station screens (settled: everything in, the line typed)
        public const string StationSerum = "hub_station_serum", StationIgnis = "hub_station_ignis", StationBastion = "hub_station_bastion",
            StationCrusher = "hub_station_crusher";
        public static void BuildStationSerum(UIPreview.Stage s) => Screen(s).Station.Pose("SERUM");
        public static void BuildStationIgnis(UIPreview.Stage s) => Screen(s).Station.Pose("IGNIS");
        /// <summary>BASTION after 防御力 was picked: who trains with him.</summary>
        public static void BuildStationBastion(UIPreview.Stage s) => Screen(s).Station.Pose("BASTION", 1);
        public static void BuildStationCrusher(UIPreview.Stage s) => Screen(s).Station.Pose("CRUSHER");

        public const string StationPrograms = "hub_station_programs", Dialogue = "dialogue", DialogueChoice = "dialogue_choice";
        public static void BuildStationPrograms(UIPreview.Stage s) => Screen(s).Station.PoseGadgetPrograms();

        /// <summary>The conversation scene: a line, then a line with its two choices.</summary>
        public static void BuildDialogue(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("Dialogue_Canvas", UITheme.CanvasKind.Menu, KitDialogue.SortingOrder);
            KitDialogue.Build(canvas.transform, canvas).PoseAt("CRUSHER", "任務ご苦労だった、新入り。");
        }

        public static void BuildDialogueChoice(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("Dialogue_Canvas", UITheme.CanvasKind.Menu, KitDialogue.SortingOrder);
            KitDialogue.Build(canvas.transform, canvas).PoseAt("CRUSHER", "他に聞きたいことはあるか？", "ありません", "もう一度聞かせてください");
        }

        public static void BuildTalk(UIPreview.Stage s)
        {
            var hs = Screen(s);
            hs.Talk.Open(BuildAssetRegistry.Operator("CRUSHER"), HubCatalog.Lines("CRUSHER"));
        }
    }
}
