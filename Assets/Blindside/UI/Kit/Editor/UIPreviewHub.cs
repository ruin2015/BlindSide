// Render Previews / Lint states of the squad HQ's screen (no 3D: a plain backdrop behind the menu canvas), on a sample save
// (¥58,000, CRUSHER Lv 3 with 4 points, SCAR bought, two frag grenades, IGNIS trained a little):
//   hub_overlay      the topbar, a few floating labels and the prompt (作戦室 in reach)
//   hub_shop         ショップ — 武器
//   hub_shop_gadget  ショップ — ガジェット
//   hub_training     訓練所 (IGNIS)
//   hub_operations   作戦室
//   hub_loadout      装備設定 (FLARE: SCAR owned)
//   hub_growth       隊員強化 — the squad list
//   hub_talk         会話 (CRUSHER)
using UnityEngine;
using Blindside.Data;
using Blindside.Hub;
using Blindside.Save;
using Blindside.UITools.EditorTools;

namespace Blindside.UI.EditorTools
{
    public static class UIPreviewHub
    {
        public const string Overlay = "hub_overlay", Shop = "hub_shop", ShopGadget = "hub_shop_gadget", Training = "hub_training",
            Operations = "hub_operations", LoadoutState = "hub_loadout", Growth = "hub_growth", Talk = "hub_talk";

        static SaveData Sample()
        {
            var d = SaveData.Fresh();
            d.tutorialDone = true; d.money = 58000; d.rankPoints = 900;
            var c = d.OperatorOf("CRUSHER"); c.level = 3; c.points = 4; c.hp = 1; c.atk = 1;
            var i = d.OperatorOf("IGNIS"); i.level = 2; i.points = 2; i.trained[2] = 2; i.atk = 2; i.trained[0] = 1; i.hp = 1;
            d.ownedWeapons.Add("SCAR");
            d.GadgetOf("frag").count = 2;
            d.OperatorOf("FLARE").gadget = "frag";
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
            hs.PosePrompt("話す — 作戦オペレーター");
            hs.PoseLabel("作戦オペレーター", "作戦室 · 出撃", new Vector2(720f, 470f));
            hs.PoseLabel("CRUSHER", "会話", new Vector2(420f, 400f));
            hs.PoseLabel("訓練教官", "訓練所", new Vector2(1120f, 430f));
        }

        public static void BuildShop(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Shop, 0);
        public static void BuildShopGadget(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Shop, 1);
        public static void BuildTraining(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Training, 2);
        public static void BuildOperations(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Operations, 0);
        public static void BuildLoadout(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Loadout, 1);
        public static void BuildGrowth(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Growth, 0);

        public static void BuildTalk(UIPreview.Stage s)
        {
            var hs = Screen(s);
            hs.Talk.Open(BuildAssetRegistry.Operator("CRUSHER"), HubCatalog.Lines("CRUSHER"));
        }
    }
}
