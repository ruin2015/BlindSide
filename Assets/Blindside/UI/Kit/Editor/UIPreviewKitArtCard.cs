// Render Previews > KitArtCard (kit_artcard): the HUD illustration card (KitArtCard) on a HUD canvas — CRUSHER 0.21s into its entrance
// (wipe over, jitter / strips on, brightness flash) and the settled card of CRUSHER, FLARE, IGNIS, SERUM and BASTION, in one row at
// 90%. The data is what PlayerSelectionController.ShowOpArt passes (OperatorArt.Body, key, "kana · role", ability, file tag, colour).
// Every card is set in edit mode and posed with Evaluate(t); nothing runs on a clock.
// "Blindside > UI > Measure Kit Art Card GC" re-poses every card for 300 frames and logs the managed bytes allocated (should be 0).
using TMPro;
using UnityEditor;
using UnityEngine;
using Blindside.Data;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewKitArtCard
    {
        public const string Name = "kit_artcard";
        const string OperatorDir = "Assets/Blindside/Data/Operators/";
        const float Scale = .9f;
        static readonly Color Label = new Color32(0x9a, 0xa2, 0xaa, 0xff);

        struct Column
        {
            public string caption, key; public float t;
            public Column(string caption, string key, float t) { this.caption = caption; this.key = key; this.t = t; }
        }

        static readonly Column[] Columns =
        {
            new Column("CRUSHER  t=0.21", "CRUSHER", .21f),
            new Column("CRUSHER  DONE", "CRUSHER", 1f),
            new Column("FLARE  DONE", "FLARE", 1f),
            new Column("IGNIS  DONE", "IGNIS", 1f),
            new Column("SERUM  DONE", "SERUM", 1f),
            new Column("BASTION  DONE", "BASTION", 1f),
        };

        public static void Build(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("KitArtCard", UITheme.CanvasKind.Hud, 0);
            var root = canvas.transform;
            float colW = KitArtCard.Width * Scale + 24f;
            const float X0 = 30f, Y0 = 60f;

            for (int c = 0; c < Columns.Length; c++)
            {
                var col = Columns[c];
                float x = X0 + c * colW;
                var cap = HudKit.AddText(root, col.caption, 13, Label, false, TextAlignmentOptions.MidlineLeft, "Caption", UITheme.TextRole.Display);
                UILayout.Place(cap.rectTransform, x, Y0, colW - 10f, 20f);

                var slot = HudKit.NewRect("Slot" + c, root);
                UILayout.Place(slot, x, Y0 + 30f, KitArtCard.Width, KitArtCard.Height);
                slot.localScale = new Vector3(Scale, Scale, 1f);
                var card = KitArtCard.Create(slot, "OpArt");
                UILayout.Place((RectTransform)card.transform, 0f, 0f, KitArtCard.Width, KitArtCard.Height);

                var op = AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDir}Operator_{col.key}.asset");
                string kr = op != null ? $"{op.kr} · {op.role}" : col.key;
                card.Show(OperatorArt.Body(col.key), col.key, kr, op != null ? op.abilityName : "", $"OPERATOR FILE // OP-{6 + IndexOf(col.key):000} // ASSAULT",
                    op != null ? op.color : Color.white);
                card.Evaluate(col.t);
            }
        }

        static int IndexOf(string key)
        {
            string[] squad = { "CRUSHER", "FLARE", "IGNIS", "SERUM", "BASTION" };
            for (int i = 0; i < squad.Length; i++) if (squad[i] == key) return i;
            return 0;
        }

        /// <summary>Blindside > UI > Measure Kit Art Card GC: re-poses every card of the KitArtCard stage through its entrance for 300 frames
        /// and logs the managed bytes allocated after 30 warm-up frames. Should be 0.</summary>
        [MenuItem("Blindside/UI/Measure Kit Art Card GC")]
        public static void MeasureGc()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitArtCardGC] edit mode only."); return; }
            var state = new UIPreview.State { name = Name, label = "KitArtCard", build = Build };
            using (var stage = UIPreview.Build(state))
            {
                var cards = stage.canvases[0].GetComponentsInChildren<KitArtCard>();
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = (frame % 60) * (1f / 60f);
                    for (int i = 0; i < cards.Length; i++) { cards[i].Evaluate(t); _ = cards[i].Duration; }
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitArtCardGC] 300 frames, {cards.Length} cards: {bytes} bytes allocated ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
