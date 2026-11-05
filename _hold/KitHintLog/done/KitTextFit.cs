// Fits one text into a width the way the kit's HUD lines do (Phase 4-3): first the letter spacing is narrowed step by step down to
// `minSpacing` (TMP unit: 1/100 em), and if the line is still wider than the box it is wrapped; the caller then makes the box taller.
// The font size is never reduced. The text must be on an active object (TMP measures only there).
using TMPro;
using UnityEngine;

namespace Blindside.UI
{
    public static class KitTextFit
    {
        /// <summary>Sets `s` on `t` and returns the number of lines it needs: 0 for an empty text, 1 when it fits (letter spacing
        /// 0 … minSpacing), otherwise the wrapped line count (≥ 2, wrapping on, spacing left at minSpacing).</summary>
        public static int Fit(TMP_Text t, string s, float width, float lineHeight, float minSpacing)
        {
            t.text = s ?? "";
            t.characterSpacing = 0f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            if (string.IsNullOrEmpty(s)) return 0;
            if (t.GetPreferredValues(s).x <= width) return 1;
            for (float sp = -1f; sp >= minSpacing - .001f; sp -= 1f)
            {
                t.characterSpacing = sp;
                if (t.GetPreferredValues(s).x <= width) return 1;
            }
            t.textWrappingMode = TextWrappingModes.Normal;
            float one = t.GetPreferredValues("あ", width, 100000f).y;
            float all = t.GetPreferredValues(s, width, 100000f).y;
            return Mathf.Max(2, Mathf.RoundToInt((all - one) / lineHeight) + 1);
        }
    }
}
