// Render Previews > FontTest: real strings from the mockup in each text role (Display / Body / Title) — English names, numbers, Japanese
// descriptions, mixed labels and kanji-heavy sentences — drawn on one menu canvas. UIPreview.RenderAll writes it at 1440×810 and
// 2560×1440 (font_test_*.png). Not part of UIPreview.States on purpose: UILint measures those, and this page keeps 11px text on purpose.
using UnityEngine;
using TMPro;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewFontTest
    {
        public const string Name = "font_test";

        struct Line { public string text; public float size; public Color color; }

        static Line L(string text, float size, Color? color = null) => new Line { text = text, size = size, color = color ?? Color.white };

        static readonly Color Acc = new Color32(0xff, 0xc8, 0x1e, 0xff);
        static readonly Color Blue = new Color32(0x1f, 0x96, 0xe8, 0xff);
        static readonly Color Dim = new Color32(0x9a, 0xa2, 0xaa, 0xff);

        static readonly Line[] Display =
        {
            L("CRUSHER   VOLT   MIASMA   WRAITH   OBSIDIAN   ARGUS", 34),
            L("ROUND 03 / 15   HP 78/120   12×5   ACC 70%   ▶▶", 26, Acc),
            L("MAP // 基本邸宅   26×20   EXFIL A", 22, Blue),
            L("OPERATOR FILE // OP-006 — 護衛 · 戦闘 · 邸宅 · 強化壁", 18),
            L("✦  ✚  ✛  ✹  ⬢  ▣  ◎  ⌖  ➜  ←  →  ↑  ↓", 30, Acc),
            L("▣ CRUSHER   ◎ FLARE   ✹ IGNIS   ✚ SERUM   ⬢ BASTION", 24),
        };

        static readonly Line[] BodyLines =
        {
            L("人質を救出地点1か所(EXFIL A)まで護送せよ。", 18),
            L("壁貫通射撃は命中率が低下し、遮蔽物の陰に潜む敵には照準が届かない。", 15),
            L("強化壁はハンマーでは壊せない。ランチャー、サーメート、ブラックミラーで破壊する。", 13),
            L("最小サイズ 11px — マップ選択 ▶ 保存先: C:/Users/プレイヤー/Maps", 11, Dim),
        };

        static readonly Line[] TitleLines =
        {
            L("攻撃 · 作戦開始", 40),
            L("護衛、戦闘、邸宅、強化壁", 36, Acc),
            L("命中 78% · 12×5 · ヘッドショット ×2 · ダメージ×2", 24),
            L("ユーザー作成マップ「黎明の砦」を読み込みました", 20, Blue),
        };

        public static void Build(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("FontTest", UITheme.CanvasKind.Menu, 0);
            float y = 22f;
            y = Section(canvas.transform, y, "DISPLAY — BSTactical → NotoJP-Black → NotoJP-Dynamic", Display, UITheme.TextRole.Display);
            y = Section(canvas.transform, y, "BODY — NotoJP-Medium → NotoJP-Dynamic", BodyLines, UITheme.TextRole.Body);
            Section(canvas.transform, y, "TITLE — NotoJP-Black → NotoJP-Dynamic", TitleLines, UITheme.TextRole.Title);
        }

        static float Section(Transform parent, float y, string header, Line[] lines, UITheme.TextRole role)
        {
            var h = HudKit.AddText(parent, header, 12, new Color32(0x5d, 0x64, 0x6c, 0xff), false, TextAlignmentOptions.MidlineLeft, "Header", UITheme.TextRole.Display);
            UILayout.Place(h.rectTransform, 40f, y, 1360f, 20f);
            y += 24f;
            foreach (var l in lines)
            {
                float rowH = Mathf.Ceil(l.size * 1.45f);
                var t = HudKit.AddText(parent, l.text, l.size, l.color, false, TextAlignmentOptions.MidlineLeft, role + " " + l.size, role);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(t.rectTransform, 40f, y, 1360f, rowH);
                y += rowH + 4f;
            }
            return y + 16f;
        }
    }
}
