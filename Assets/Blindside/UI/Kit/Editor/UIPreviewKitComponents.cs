// Render Previews > KitComponents (kit_components): the kit's common components on one menu canvas —
//   TacticalPanel   every colour preset (paper, ink, ink2, blue, foe, acc) with border / left band (follow, clip) / drop shadow
//   Chip            colour presets, Latin and Japanese text
//   CornerBrackets  on paper and on ink
//   TacticalButton  normal, hover mid-move with the light sweep, hover settled, pressed, disabled — posed in code with
//                   PoseState(state, t) (no pointer)
//   HazardStripe    yellow / ink (#hint), translucent ink over blue (#end) and slanted acc stripes (.start), each at two scroll positions
// Everything is posed with ITimedEffect.Evaluate(t); nothing runs on a clock. "Blindside > UI > Measure Kit Components GC" re-poses
// every stripe and button for 300 frames and logs the managed bytes allocated (should be 0).
using TMPro;
using UnityEditor;
using UnityEngine;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewKitComponents
    {
        public const string Name = "kit_components";

        static readonly Color Label = new Color32(0x9a, 0xa2, 0xaa, 0xff);

        public static void Build(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("KitComponents", UITheme.CanvasKind.Menu, 0);
            var root = canvas.transform;
            var theme = UITheme.Current;
            Color floor = UIPreview.BackdropColor;

            // ---- TacticalPanel ------------------------------------------------------------------------------------------------
            Caption(root, "TACTICALPANEL   PAPER · INK · INK2 · BLUE · FOE · ACC   (BORDER · BAND FOLLOW / CLIP · SHADOW)", 40, 16, 1000);
            KitColor[] presets = { KitColor.Paper, KitColor.Ink, KitColor.Ink2, KitColor.Blue, KitColor.Foe, KitColor.Acc };
            string[] names = { "PAPER", "INK", "INK2", "BLUE", "FOE", "ACC" };
            for (int i = 0; i < presets.Length; i++)
            {
                var p = TacticalPanel.Create(root, "Panel" + names[i], presets[i], SlantedGraphic.Size.Large);
                UILayout.Place(p.rectTransform, 40 + i * 226, 46, 204, 84);
                switch (i)
                {
                    case 0: p.SetBorder(2f, KitColor.Ink); p.SetBand(TacticalPanel.Band.Follow, 5f, KitColor.Blue); break;
                    case 1: p.SetBand(TacticalPanel.Band.Follow, 5f, KitColor.Blue); break;
                    case 2: p.SetBorder(2f, KitColor.Ink3); break;
                    case 4: p.SetBand(TacticalPanel.Band.Clip, 40f, KitColor.Ink); break;
                    case 5: p.SetBorder(2f, KitColor.Ink); break;
                }
                if (i == 0) KitShadow.Behind(p.rectTransform, floor);
                var t = HudKit.AddText(p.transform, names[i], 16, KitPalette.Get(KitPalette.TextOn(presets[i])), true, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Display);
                UILayout.Place(t.rectTransform, i == 4 ? 52 : 34, 16, 140, 22);
                var sub = HudKit.AddText(p.transform, i == 0 ? "SHADOW" : i == 4 ? "CLIP 40" : (i == 1 ? "BAND 5" : i == 3 ? "PLAIN" : "BORDER 2"), 12,
                    KitPalette.Get(KitPalette.TextOn(presets[i])), true, TextAlignmentOptions.MidlineLeft, "Sub", UITheme.TextRole.Display);
                UILayout.Place(sub.rectTransform, i == 4 ? 52 : 34, 44, 140, 18);
            }

            // ---- Chip ---------------------------------------------------------------------------------------------------------
            Caption(root, "CHIP   INK · PAPER · BLUE · FOE · ACC · INK2 (JA)", 40, 150, 640);
            string[] chipText = { "OBJECTIVE", "SECURE", "ATTACK", "HOSTILE", "WARNING", "行動終了" };
            KitColor[] chipBg = { KitColor.Ink, KitColor.Paper, KitColor.Blue, KitColor.Foe, KitColor.Acc, KitColor.Ink2 };
            float cx = 40;
            for (int i = 0; i < chipText.Length; i++)
            {
                var c = Chip.Create(root, "Chip" + i, chipText[i], chipBg[i], KitPalette.TextOn(chipBg[i]), i == 5 ? 11f : 12f);
                var rt = (RectTransform)c.transform;
                UILayout.Place(rt, cx, 180, rt.sizeDelta.x, rt.sizeDelta.y);
                cx += rt.sizeDelta.x + 12;
            }

            // ---- CornerBrackets -----------------------------------------------------------------------------------------------
            Caption(root, "CORNERBRACKETS   18 / 3 · INK 0.6 ON PAPER   24 / 2 · WHITE 0.6 ON INK", 720, 150, 700);
            var bp = TacticalPanel.Create(root, "BracketPaper", KitColor.Paper, SlantedGraphic.Size.Custom, 0f);
            UILayout.Place(bp.rectTransform, 720, 178, 220, 104);
            CornerBrackets.Create(bp.transform, "Brackets", new Color(theme.ink.r, theme.ink.g, theme.ink.b, .6f), 18f, 3f, 10f, theme.paper);
            var bi = TacticalPanel.Create(root, "BracketInk", KitColor.Ink, SlantedGraphic.Size.Custom, 0f);
            UILayout.Place(bi.rectTransform, 960, 178, 220, 104);
            CornerBrackets.Create(bi.transform, "Brackets", new Color(1f, 1f, 1f, .6f), 24f, 2f, 10f, theme.ink);

            // ---- TacticalButton -----------------------------------------------------------------------------------------------
            Caption(root, "TACTICALBUTTON   226x40 · POSED IN CODE (PoseState)", 40, 300, 640);
            string[] keys = { "1", "2", "3", "4", "5", "6" };
            string[] labels = { "移動", "視界変更", "攻撃", "ドローン", "待機", "移動" };
            string[] subs = { "MOVE", "VIEW", "FIRE", "DRONE", "END", "MOVE" };
            string[] values = { "4.0m", null, null, null, null, "4.0m" };
            string[] captions = { "NORMAL", "HOVER  t=0.04  MOVING", "HOVER  t=0.10  SWEEP", "HOVER  SETTLED", "PRESSED", "DISABLED" };
            for (int i = 0; i < 6; i++)
            {
                int col = i % 3, row = i / 3;
                float x = 40 + col * 280, y = 330 + row * 84;
                var b = TacticalButton.Create(root, "Button" + i, keys[i], labels[i], subs[i], values[i]);
                UILayout.Place((RectTransform)b.transform, x, y, 226, 40);
                switch (i)
                {
                    case 0: b.PoseState(TacticalButton.Visual.Normal, 1f); break;
                    case 1: b.PoseState(TacticalButton.Visual.Hover, .04f); break;
                    case 2: b.PoseState(TacticalButton.Visual.Hover, .10f); break;
                    case 3: b.PoseState(TacticalButton.Visual.Hover, 1f); break;
                    case 4: b.PoseState(TacticalButton.Visual.Pressed, 1f, TacticalButton.Visual.Hover); break;
                    case 5: b.interactable = false; b.PoseState(TacticalButton.Visual.Disabled, 1f); break;
                }
                Caption(root, captions[i], x, y + 46, 236);
            }

            // ---- HazardStripe -------------------------------------------------------------------------------------------------
            Caption(root, "HAZARDSTRIPE   135 DEG · ONE PERIOD PER 1.2-1.6S   t=0 / HALF CYCLE", 900, 300, 520);
            for (int k = 0; k < 2; k++)
            {
                float x = 900 + k * 260, phase = k * .5f;
                // #hint::before: acc / ink 6 / 12
                var hint = HudKit.NewRect("HintStrip" + k, root);
                UILayout.Place(hint, x, 330, 240, 30);
                var h1 = HazardStripe.Create(hint, "DecoHazard", theme.acc, theme.ink, 6f, 12f, 1.4f);
                h1.Evaluate(phase * h1.Duration);

                // #end: blue, rgba(0,0,0,.1) / transparent 10 / 20, 1.2s
                var end = TacticalPanel.Create(root, "EndTurn" + k, KitColor.Blue, SlantedGraphic.Size.Custom, 18f);
                UILayout.Place(end.rectTransform, x, 376, 240, 56);
                var h2 = HazardStripe.Create(end.transform, "DecoHazard", new Color(0f, 0f, 0f, .1f), Color.clear, 10f, 20f, 1.2f);
                h2.over = theme.blue; h2.MatchShape(end.GetComponent<SlantedGraphic>());
                h2.Evaluate(phase * h2.Duration);
                var et = HudKit.AddText(end.transform, "END TURN", 20, Color.white, true, TextAlignmentOptions.MidlineLeft, "Title", UITheme.TextRole.Display);
                UILayout.Place(et.rectTransform, 30, 6, 180, 26);
                var es = HudKit.AddText(end.transform, "ターン終了", 12, Color.white, true, TextAlignmentOptions.MidlineLeft, "Sub");
                UILayout.Place(es.rectTransform, 30, 32, 180, 18);

                // .start::before: acc / transparent 12 / 24 on ink, slanted strip
                var start = TacticalPanel.Create(root, "StartStrip" + k, KitColor.Ink, SlantedGraphic.Size.Custom, 0f);
                UILayout.Place(start.rectTransform, x, 448, 240, 22);
                var h3 = HazardStripe.Create(start.transform, "DecoHazard", theme.acc, Color.clear, 12f, 24f, 1.6f);
                h3.over = theme.ink; h3.slant = 14f;
                h3.Evaluate(phase * h3.Duration);

                Caption(root, k == 0 ? "PHASE 0" : "PHASE 0.5 (HALF PERIOD RIGHT)", x, 476, 240);
            }
        }

        /// <summary>Blindside > UI > Measure Kit Components GC: re-poses every HazardStripe and TacticalButton (hover with the sweep)
        /// for 300 frames in the KitComponents stage and logs the managed bytes allocated (after 30 warm-up frames). Should be 0.</summary>
        [MenuItem("Blindside/UI/Measure Kit Components GC")]
        public static void MeasureGc()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitCompGC] edit mode only."); return; }
            var state = new UIPreview.State { name = Name, label = "KitComponents", build = Build };
            using (var stage = UIPreview.Build(state))
            {
                var canvas = stage.canvases[0];
                var stripes = canvas.GetComponentsInChildren<HazardStripe>();
                var buttons = canvas.GetComponentsInChildren<TacticalButton>();
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = (frame % 60) * (1f / 60f);
                    for (int i = 0; i < stripes.Length; i++) stripes[i].Evaluate(t * 1.6f);
                    for (int i = 0; i < buttons.Length; i++)
                        buttons[i].PoseState(i % 2 == 0 ? TacticalButton.Visual.Hover : TacticalButton.Visual.Normal, t,
                            i % 2 == 0 ? TacticalButton.Visual.Normal : TacticalButton.Visual.Hover);
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitCompGC] 300 frames, {stripes.Length} stripes, {buttons.Length} buttons: {bytes} bytes allocated ({bytes / 300f:0.##} per frame)");
            }
        }

        static void Caption(Transform parent, string text, float x, float y, float w)
        {
            var t = HudKit.AddText(parent, text, 12, Label, false, TextAlignmentOptions.MidlineLeft, "Caption", UITheme.TextRole.Display);
            UILayout.Place(t.rectTransform, x, y, w, 18);
        }
    }
}
