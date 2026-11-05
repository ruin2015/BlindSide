// Render Previews > KitCards (kit_cards): the roster card (SelectableCard) on a HUD canvas — CRUSHER, FLARE, IGNIS, SERUM, BASTION
// stacked in a column per state: normal, hover, selected at t = .1 / .24 / .45, ended, dead, hit at t = .1 / .3 (78/120 → 56/120) and
// the entrance at t = .2 (each card delayed by its index × 60ms). The data comes from the operator assets and OperatorArt here — the
// card itself only gets plain values. Every state is set in edit mode and posed with Evaluate(t); nothing runs on a clock.
// "Blindside > UI > Measure Kit Cards GC" re-poses every card for 300 frames and logs the managed bytes allocated (should be 0).
using System.Globalization;
using TMPro;
using UnityEditor;
using UnityEngine;
using Blindside.Data;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewKitCards
    {
        public const string Name = "kit_cards";
        const string OperatorDir = "Assets/Blindside/Data/Operators/";

        static readonly string[] Squad = { "CRUSHER", "FLARE", "IGNIS", "SERUM", "BASTION" };
        static readonly string[] Icons = { "▣", "◎", "✹", "✚", "⬢" };
        // the defenders (HudController's icons): their katakana names are the longest (オブシディアン) next to the move chip
        static readonly string[] DefenseSquad = { "VOLT", "MIASMA", "WRAITH", "OBSIDIAN", "ARGUS" };
        static readonly string[] DefenseIcons = { "✦", "✹", "⌖", "⬢", "◎" };
        static readonly Color Label = new Color32(0x9a, 0xa2, 0xaa, 0xff);

        enum Pose { Normal, Hover, Select, Ended, Dead, Hit, Enter, Defense }

        struct Column
        {
            public string caption; public Pose pose; public float t;
            public Column(string caption, Pose pose, float t) { this.caption = caption; this.pose = pose; this.t = t; }
        }

        static readonly Column[] Columns =
        {
            new Column("NORMAL", Pose.Normal, 0f),
            new Column("HOVER", Pose.Hover, 1f),
            new Column("SELECT  t=0.10", Pose.Select, .10f),
            new Column("SELECT  t=0.24", Pose.Select, .24f),
            new Column("SELECT  t=0.45", Pose.Select, .45f),
            new Column("ENDED", Pose.Ended, 1f),
            new Column("DEAD  K.I.A", Pose.Dead, 1f),
            new Column("HIT  t=0.10  78 > 56 / 120", Pose.Hit, .10f),
            new Column("HIT  t=0.30  78 > 56 / 120", Pose.Hit, .30f),
            new Column("ENTER  t=0.20  (INDEX x 60MS)", Pose.Enter, .20f),
            new Column("DEFENSE  NORMAL", Pose.Defense, 0f),
        };

        public static void Build(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("KitCards", UITheme.CanvasKind.Hud, 0);
            var root = canvas.transform;
            var theme = UITheme.Current;
            const float ColW = 290f, RowH = 470f, X0 = 40f, Y0 = 40f;
            const int PerRow = 6;

            for (int c = 0; c < Columns.Length; c++)
            {
                var col = Columns[c];
                float x = X0 + (c % PerRow) * ColW, y = Y0 + (c / PerRow) * RowH;
                var cap = HudKit.AddText(root, col.caption, 13, Label, false, TextAlignmentOptions.MidlineLeft, "Caption", UITheme.TextRole.Display);
                UILayout.Place(cap.rectTransform, x, y, ColW - 20f, 20f);
                var keys = col.pose == Pose.Defense ? DefenseSquad : Squad;
                var icons = col.pose == Pose.Defense ? DefenseIcons : Icons;

                for (int i = 0; i < keys.Length; i++)
                {
                    var card = SelectableCard.Create(root, "Card" + keys[i]);
                    UILayout.Place((RectTransform)card.transform, x, y + 30f + i * (SelectableCard.Height + theme.hud.rosterGap), SelectableCard.Width, SelectableCard.Height);
                    var op = AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDir}Operator_{keys[i]}.asset");
                    int max = op != null ? op.hp : 120, cur = max;
                    if (keys[i] == "IGNIS") cur = 78;                                    // the mockup's sample: IGNIS 78/120
                    if (col.pose == Pose.Hit) { cur = 78; max = 120; }
                    float mv = op != null ? op.mv * 3 + 1 : 7;                          // the HUD's move range (UIPreview: mv × 3 + 1)
                    card.SetData(OperatorArt.Face(keys[i]), keys[i], op != null ? op.kr : keys[i], op != null ? op.color : Color.white,
                        cur, max, "移動 " + mv.ToString("0.0", CultureInfo.InvariantCulture) + "m", icons[i]);

                    switch (col.pose)
                    {
                        case Pose.Hover: card.SetHovered(true); break;
                        case Pose.Select: card.SetSelected(true); break;
                        case Pose.Ended: card.SetEnded(true); break;
                        case Pose.Dead: card.SetDead(true); break;
                        case Pose.Hit: card.Hit(56); break;
                        case Pose.Enter: card.PlayEnter(i); break;
                    }
                    card.Evaluate(col.t);
                }
            }
        }

        /// <summary>Blindside > UI > Measure Kit Cards GC: re-poses every card of the KitCards stage (selection, hit, entrance, tone)
        /// for 300 frames and logs the managed bytes allocated after 30 warm-up frames. Should be 0.</summary>
        [MenuItem("Blindside/UI/Measure Kit Cards GC")]
        public static void MeasureGc()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[KitCardsGC] edit mode only."); return; }
            var state = new UIPreview.State { name = Name, label = "KitCards", build = Build };
            using (var stage = UIPreview.Build(state))
            {
                var cards = stage.canvases[0].GetComponentsInChildren<SelectableCard>();
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    float t = (frame % 90) * (1f / 60f);
                    for (int i = 0; i < cards.Length; i++) { cards[i].Evaluate(t); _ = cards[i].Duration; }
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[KitCardsGC] 300 frames, {cards.Length} cards: {bytes} bytes allocated ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
