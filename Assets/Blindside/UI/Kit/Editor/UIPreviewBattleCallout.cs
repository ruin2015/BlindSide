// Render Previews / Lint states of the battle cinematic's full-screen words (BattleCallout), over a plain backdrop:
//   battle_chance    「CHANCE!」 — our first strike's opening hit (t = .5 s: the word landed)
//   battle_warning   「WARNING!」 — the enemy's (t = .5 s)
//   battle_engage    the cut into the fight: the shutter closed, 「ENGAGE · 交戦開始」 on the seam
using UnityEngine;
using Blindside.Cinematic;
using Blindside.UITools.EditorTools;

namespace Blindside.UI.EditorTools
{
    public static class UIPreviewBattleCallout
    {
        public const string Chance = "battle_chance", Warning = "battle_warning", Engage = "battle_engage";

        static BattleCallout Make(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("Battle_Canvas", UITheme.CanvasKind.Hud, 6);
            var bg = HudKit.NewRect("DecoBackdrop", canvas.transform);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, new Color32(0x3a, 0x3f, 0x46, 0xff));
            return BattleCallout.Build(canvas.transform);
        }

        public static void BuildChance(UIPreview.Stage s) { var c = Make(s); c.Play(false); c.Evaluate(.5f); }
        public static void BuildWarning(UIPreview.Stage s) { var c = Make(s); c.Play(true); c.Evaluate(.5f); }
        public static void BuildEngage(UIPreview.Stage s) { var c = Make(s); Canvas.ForceUpdateCanvases(); c.Cover(1f); }
    }
}
