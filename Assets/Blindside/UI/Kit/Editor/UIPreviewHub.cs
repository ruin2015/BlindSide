// Render Previews / Lint states of the squad HQ's screen (no 3D: a plain backdrop behind the menu canvas), on a sample save
// (¥58,000, CRUSHER Lv 3 with 4 points, SCAR and a suppressor bought, two frag grenades and a potion, IGNIS trained a little):
//   hub_overlay       the topbar, a few floating labels and the prompt (CRUSHER's 作戦室 in reach: F + G 話す)
//   hub_shop          装備購入 — 武器
//   hub_shop_attach   装備購入 — アタッチメント
//   hub_shop_gadget   装備購入 — ガジェット
//   hub_medical       医務室 (SERUM)
//   hub_training      訓練所 (BASTION; IGNIS's tab)
//   hub_operations    作戦室 (CRUSHER)
//   hub_manage        隊員・装備管理 (IGNIS; FLARE's tab: SCAR owned, suppressor on, a frag carried)
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
            Medical = "hub_medical", Training = "hub_training", Operations = "hub_operations", Manage = "hub_manage", Talk = "hub_talk";

        static SaveData Sample()
        {
            var d = SaveData.Fresh();
            d.tutorialDone = true; d.money = 58000; d.rankPoints = 900;
            var c = d.OperatorOf("CRUSHER"); c.level = 3; c.points = 4; c.hp = 1; c.atk = 1;
            var i = d.OperatorOf("IGNIS"); i.level = 2; i.points = 2; i.trained[2] = 2; i.atk = 2; i.trained[0] = 1; i.hp = 1;
            d.ownedWeapons.Add("SCAR");
            d.ownedAttachments.Add("suppressor");
            d.GadgetOf("frag").count = 2;
            d.GadgetOf("potion").count = 1;
            var f = d.OperatorOf("FLARE"); f.gadget = "frag"; f.attachments[0] = "suppressor";
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
            hs.PoseLabel("訓練所", "BASTION", new Vector2(1100f, 520f));
        }

        public static void BuildShop(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Shop, 0);
        public static void BuildShopAttach(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Shop, 1);
        public static void BuildShopGadget(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Shop, 2);
        public static void BuildMedical(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Medical, 0);
        public static void BuildTraining(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Training, 2);
        public static void BuildOperations(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Operations, 0);
        public static void BuildManage(UIPreview.Stage s) => Screen(s).OpenStation(HubStation.Management, 1);

        public static void BuildTalk(UIPreview.Stage s)
        {
            var hs = Screen(s);
            hs.Talk.Open(BuildAssetRegistry.Operator("CRUSHER"), HubCatalog.Lines("CRUSHER"));
        }
    }
}
