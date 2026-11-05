// A small label box (.wb .chip / #tb .ob .chip: ink background, white 700 11–12px display text, padding 3px 10px) in a parallelogram
// with a small slant (6). Background and text colours are UITheme presets; the width fits the text (padding on both sides plus the
// slant, so the padding stays visible at every height). The text role follows UIFonts (Latin → display, Japanese → body).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Chip")]
    [RequireComponent(typeof(RectTransform))]
    public class Chip : MonoBehaviour
    {
        public const float DefaultSlant = 6f;

        public KitColor background = KitColor.Ink;
        public KitColor textColor = KitColor.White;
        public float padX = 10f, padY = 3f;
        public Image box;
        public TMP_Text label;

        /// <summary>Colours from presets.</summary>
        public void SetColors(KitColor bg, KitColor text)
        {
            background = bg; textColor = text;
            if (box != null) box.color = KitPalette.Get(bg);
            if (label != null) label.color = KitPalette.Get(text);
        }

        /// <summary>New text; the chip's width follows it.</summary>
        public void SetText(string text)
        {
            if (label == null) return;
            label.text = text;
            Fit();
        }

        /// <summary>Width = text + padding × 2 + slant, height = line + padding × 2 (keeps the left edge in place).</summary>
        public void Fit()
        {
            if (label == null) return;
            var rt = (RectTransform)transform;
            var sg = GetComponent<SlantedGraphic>();
            float s = sg != null ? sg.Amount : 0f;
            Vector2 pref = label.GetPreferredValues(label.text);
            float w = Mathf.Ceil(pref.x) + padX * 2f + s;
            float h = Mathf.Ceil(Mathf.Max(pref.y, label.fontSize * 1.25f)) + padY * 2f;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
        }

        /// <summary>A chip `name` under `parent`: box + slant + text, sized to the text. Place it with UILayout.Place(x, y) afterwards
        /// using its fitted size.</summary>
        public static Chip Create(Transform parent, string name, string text, KitColor bg = KitColor.Ink, KitColor fg = KitColor.White,
            float fontSize = 12f, float slant = DefaultSlant)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            var chip = go.AddComponent<Chip>();
            chip.background = bg; chip.textColor = fg;
            chip.box = HudKit.AddImage(rt, KitPalette.Get(bg));
            go.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, slant);
            chip.label = HudKit.AddText(rt, text, fontSize, KitPalette.Get(fg), true, TextAlignmentOptions.Midline, "Label");
            HudKit.Stretch(chip.label.rectTransform);
            chip.label.rectTransform.offsetMin = new Vector2(slant * .5f, 0f);
            chip.label.rectTransform.offsetMax = new Vector2(-slant * .5f, 0f);
            chip.Fit();
            return chip;
        }
    }
}
