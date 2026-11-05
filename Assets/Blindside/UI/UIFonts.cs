// Which font a TMP text uses. The mockup has three faces: var(--fd)/var(--fm) — English names, numbers, labels, phase names — is
// the display font (BSTactical); var(--fk) — Japanese sentences and button names — is the body font (Noto Sans JP Medium); and
// the heavy Japanese headings (攻撃, 作戦開始 …) are the title font (Noto Sans JP Black). Each role's font carries its own
// fallbacks (display → Black → Dynamic, body → Dynamic), so any character still renders.
//
// Weight comes from the font, never from TMP's fake Bold / Italic, so applying a role clears those flags; it also turns Auto
// Size off and lifts screen text to UITheme.minTextSize.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Blindside.UI
{
    public static class UIFonts
    {
        /// <summary>True when the text holds a kana, kanji or full-width character.</summary>
        public static bool HasJapanese(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
                if ((c >= 0x3000 && c <= 0x30FF) || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0xFF00 && c <= 0xFFEF))
                    return true;
            return false;
        }

        /// <summary>No Japanese → display; Japanese at 20 px or more, or asked to be bold → title; other Japanese → body.
        /// An empty text can't be told and gets the body font (see Ambiguous).</summary>
        public static UITheme.TextRole RoleFor(string text, float size, bool bold)
        {
            if (string.IsNullOrWhiteSpace(StripTags(text))) return UITheme.TextRole.Body;
            if (!HasJapanese(text)) return UITheme.TextRole.Display;
            return size >= 20f || bold ? UITheme.TextRole.Title : UITheme.TextRole.Body;
        }

        /// <summary>Sets the role's font and clears fake weight, Auto Size and too-small sizes.</summary>
        public static void Apply(TMP_Text t, UITheme.TextRole role)
        {
            if (t == null) return;
            var theme = UITheme.Current;
            var f = theme.Font(role);
            if (f != null && t.font != f) t.font = f;
            t.fontStyle &= ~(FontStyles.Bold | FontStyles.Italic);
            if (t.enableAutoSizing) { t.enableAutoSizing = false; t.fontSize = t.fontSizeMax; }
            if (t is TextMeshProUGUI && t.fontSize < theme.minTextSize) t.fontSize = theme.minTextSize;
        }

        /// <summary>Picks the role from the text as it is now (its content, size and `bold`, or its own Bold flag) and applies it.</summary>
        public static UITheme.TextRole Auto(TMP_Text t, bool bold = false)
        {
            if (t == null) return UITheme.TextRole.Body;
            bool b = bold || (t.fontStyle & FontStyles.Bold) != 0;
            var role = RoleFor(t.text, t.enableAutoSizing ? t.fontSizeMax : t.fontSize, b);
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(StripTags(t.text))) Ambiguous.Add(PathOf(t.transform));
#endif
            Apply(t, role);
            return role;
        }

#if UNITY_EDITOR
        /// <summary>Texts whose role was guessed (empty when created), for the font report. Editor only.</summary>
        public static readonly HashSet<string> Ambiguous = new HashSet<string>();
#endif

        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
            return System.Text.RegularExpressions.Regex.Replace(s, "<[^>]*>", "");
        }
    }
}
