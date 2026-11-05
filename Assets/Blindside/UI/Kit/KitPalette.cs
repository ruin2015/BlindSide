// The kit's colour presets (UITheme :root colours) by name, shared by TacticalPanel, Chip and TacticalButton, so a preset follows the
// theme asset instead of copying hex values.
using UnityEngine;

namespace Blindside.UI
{
    public enum KitColor { Paper, Ink, Ink2, Blue, Foe, Acc, Paper2, Ink3, Dim, White, Custom }

    public static class KitPalette
    {
        /// <summary>The theme colour of a preset (Custom: `custom`).</summary>
        public static Color Get(KitColor c, Color custom = default)
        {
            var t = UITheme.Current;
            switch (c)
            {
                case KitColor.Paper: return t.paper;
                case KitColor.Ink: return t.ink;
                case KitColor.Ink2: return t.ink2;
                case KitColor.Blue: return t.blue;
                case KitColor.Foe: return t.foe;
                case KitColor.Acc: return t.acc;
                case KitColor.Paper2: return t.paper2;
                case KitColor.Ink3: return t.ink3;
                case KitColor.Dim: return t.dim;
                case KitColor.White: return Color.white;
                default: return custom;
            }
        }

        /// <summary>The text colour that reads on a preset background: ink on paper / acc, white on the dark and saturated ones.</summary>
        public static KitColor TextOn(KitColor background)
        {
            switch (background)
            {
                case KitColor.Paper: case KitColor.Paper2: case KitColor.Acc: case KitColor.White: return KitColor.Ink;
                default: return KitColor.White;
            }
        }
    }
}
