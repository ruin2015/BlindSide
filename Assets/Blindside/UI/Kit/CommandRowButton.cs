// The battle's command button (the user's design, replacing TacticalButton in the command menu): a parallelogram (slant 10), 236×40.
//   normal    white; an ink number box with the key in white; the Japanese name (ink) over the English line (+ the value); the
//             action's icon large at the right, dark grey, tilted
//   hover     ink wipes in from the left (.16s) and the button moves right 10; the number box turns the operator's colour (the key
//             ink); texts white; a line in the operator's colour along the foot; the icon light grey, a little bigger and more tilted
//   pressed   as hover, moved back to +6     disabled  greyscale, the whole button at alpha .4
//   Body
//     Panel        white parallelogram — the click target (the Button's targetGraphic)
//     DecoInk      ink over it, wiping in from the left
//     IconClip     a Mask in the button's parallelogram: the icon never leaves the button
//       Icon       CommandIcons sprite, tinted, tilted
//     Num          the number box (parallelogram): Key
//     Label, Sub   the name, the English line
//     DecoFoot     the operator-colour line along the foot (drawn left → right on hover)
// It is a Button (onClick and every event hookup stay); the look is ITimedEffect.Evaluate(t) — the transition from the pose the
// button had to the new state's — and Update only advances an unscaled clock. PoseState poses it for previews. No allocation per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Command Row Button")]
    public class CommandRowButton : Button, ITimedEffect
    {
        public enum Visual { Normal, Hover, Pressed, Disabled }

        public const float W = 236f, H = 40f, Slant = 10f, NumW = 34f, IconSize = 40f, IconTilt = -16f, IconTiltHover = -22f;
        public const float StateTime = .16f, HoverShift = 10f, PressShift = 6f, DisabledAlpha = .4f, FootH = 3f;
        static readonly Color IconDark = new Color32(0x55, 0x58, 0x5c, 0xff), IconLight = new Color32(0xc8, 0xcb, 0xcf, 0xff);

        [Header("Parts")]
        public RectTransform body, ink, foot, icon;
        public Image panel, inkImg, numBox, footImg, iconImg;
        public TMP_Text keyText, labelText, subText;
        public CanvasGroup group;
        public Color accent = Color.white;

        Visual target = Visual.Normal;
        float fromHover, toHover, fromPress, toPress, hover, press, clock;
        bool running;

        public Visual Current => target;

        protected override void Awake() { base.Awake(); transition = Transition.None; }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            Visual v;
            switch (state)
            {
                case SelectionState.Highlighted: v = Visual.Hover; break;
                case SelectionState.Pressed: v = Visual.Pressed; break;
                case SelectionState.Disabled: v = Visual.Disabled; break;
                default: v = Visual.Normal; break;
            }
            Go(v, instant);
        }

        /// <summary>Starts the transition to `v` from the current pose (instant: jump to its end).</summary>
        public void Go(Visual v, bool instant = false)
        {
            if (body == null) return;                       // (Button.Awake / OnEnable before Create has wired the parts)
            fromHover = hover; fromPress = press;
            target = v;
            toHover = v == Visual.Hover || v == Visual.Pressed ? 1f : 0f;
            toPress = v == Visual.Pressed ? 1f : 0f;
            clock = 0f;
            if (instant || !Application.isPlaying) { running = false; Evaluate(StateTime); }
            else { running = true; Evaluate(0f); }
        }

        /// <summary>For previews: the transition from `from` to `state`, `t` seconds in.</summary>
        public void PoseState(Visual state, float t, Visual from = Visual.Normal)
        {
            running = false;
            fromHover = from == Visual.Hover || from == Visual.Pressed ? 1f : 0f;
            fromPress = from == Visual.Pressed ? 1f : 0f;
            target = state;
            toHover = state == Visual.Hover || state == Visual.Pressed ? 1f : 0f;
            toPress = state == Visual.Pressed ? 1f : 0f;
            Evaluate(t);
        }

        public float Duration => StateTime;

        public void Evaluate(float t)
        {
            if (body == null) return;
            var theme = UITheme.Current;
            float e = UIEase.Tactical(Mathf.Clamp01(t / StateTime));
            hover = Mathf.LerpUnclamped(fromHover, toHover, e);
            press = Mathf.LerpUnclamped(fromPress, toPress, e);
            bool disabled = target == Visual.Disabled;
            float h = disabled ? 0f : hover;

            body.anchoredPosition = new Vector2(HoverShift * h - (HoverShift - PressShift) * press, 0f);
            Color inkC = theme.ink, white = Color.white;
            Color acc = disabled ? HudKit.Grayscale(accent) : accent;
            panel.color = white;
            inkImg.color = Color.Lerp(inkC, Color.black, .25f * press);
            float wipe = Mathf.Clamp01(h);
            if (ink.localScale.x != wipe) ink.localScale = new Vector3(wipe, 1f, 1f);
            if (inkImg.enabled != wipe > .001f) inkImg.enabled = wipe > .001f;
            numBox.color = Color.Lerp(inkC, acc, h);
            keyText.color = Color.Lerp(white, inkC, h);
            labelText.color = Color.Lerp(inkC, white, h);
            subText.color = Color.Lerp(Color.Lerp(inkC, white, .3f), Color.Lerp(white, inkC, .2f), h);
            footImg.color = acc;
            if (foot.localScale.x != h) foot.localScale = new Vector3(h, 1f, 1f);
            iconImg.color = Color.Lerp(IconDark, IconLight, h);
            float z = 1f + .08f * h;
            icon.localScale = new Vector3(z, z, 1f);
            icon.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(IconTilt, IconTiltHover, h));
            float a = disabled ? DisabledAlpha : 1f;
            if (group.alpha != a) group.alpha = a;
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= StateTime) running = false;
        }

        // ---- build ------------------------------------------------------------------------------------------------------------
        static Image Slanted(Transform parent, string name, Color c, float slant, bool raycast = false)
        {
            var rt = HudKit.NewRect(name, parent);
            var img = HudKit.AddImage(rt, c, raycast);
            rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, slant);
            return img;
        }

        /// <summary>A command button `name` under `parent` (place it with UILayout.Place, W×H): key, Japanese name, the English line
        /// (`value` appended, e.g. "MOVE  10.0M"), the icon, and the operator colour for its hover.</summary>
        public static CommandRowButton Create(Transform parent, string name, string key, string label, string en, string value, Sprite iconSprite, Color accent)
        {
            var theme = UITheme.Current;
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(W, H);
            var group = go.AddComponent<CanvasGroup>();

            var body = HudKit.NewRect("Body", rt);
            HudKit.Stretch(body);
            var panel = Slanted(body, "Panel", Color.white, Slant, true);
            HudKit.Stretch(panel.rectTransform);
            var inkImg = Slanted(body, "DecoInk", theme.ink, Slant);
            var ink = inkImg.rectTransform;
            HudKit.Stretch(ink);
            ink.pivot = new Vector2(0f, .5f);

            // the icon behind the texts, at the right — cut by the button's own parallelogram, so it never leaves the button
            var clip = HudKit.NewRect("IconClip", body);
            HudKit.Stretch(clip);
            var clipImg = HudKit.AddImage(clip, Color.white);
            clip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, Slant);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var irt = HudKit.NewRect("Icon", clip);
            irt.anchorMin = irt.anchorMax = new Vector2(0f, .5f); irt.pivot = new Vector2(.5f, .5f);
            irt.sizeDelta = new Vector2(IconSize, IconSize);
            irt.anchoredPosition = new Vector2(W - 40f, 0f);
            var iconImg = irt.gameObject.AddComponent<Image>();
            iconImg.sprite = iconSprite; iconImg.preserveAspect = true; iconImg.raycastTarget = false;
            iconImg.enabled = iconSprite != null;
            irt.gameObject.AddComponent<LintIgnoreClip>().reason = "the tilted icon is cut by the button's own shape (IconClip mask)";

            var numBox = Slanted(body, "Num", theme.ink, Slant);
            UILayout.Place(numBox.rectTransform, 0f, 0f, NumW + Slant, H);
            var keyText = HudKit.AddText(numBox.rectTransform, key, 15f, Color.white, true, TextAlignmentOptions.Center, "Key", UITheme.TextRole.Display);
            if (theme.cardFontBold != null) keyText.font = theme.cardFontBold;
            UILayout.Place(keyText.rectTransform, Slant * .5f, 0f, NumW, H);

            // the name and the English line stop short of the icon (no text over the picture); a long name gets smaller to fit
            float tx = NumW + Slant + 10f, tw = W - tx - IconSize - 16f, lw = tw;
            var labelText = HudKit.AddText(body, label, 15f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Title);
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            TacticalButton.FitLabel(labelText, lw);
            UILayout.Place(labelText.rectTransform, tx, 2f, lw, 20f);
            string sub = string.IsNullOrEmpty(value) ? en : en + "  " + value;
            var subText = HudKit.AddText(body, sub, 11f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Sub", UITheme.TextRole.Display);
            if (theme.cardFontMedium != null) subText.font = theme.cardFontMedium;
            subText.characterSpacing = 6f;
            subText.textWrappingMode = TextWrappingModes.NoWrap;
            subText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(subText.rectTransform, tx, 22f, tw, 15f);

            var footImg = Slanted(body, "DecoFoot", accent, Slant * FootH / H);
            var foot = footImg.rectTransform;
            UILayout.Place(foot, 0f, H - FootH, W - Slant + Slant * FootH / H, FootH);
            foot.pivot = new Vector2(0f, .5f);
            foot.anchoredPosition = new Vector2(0f, -(H - FootH * .5f));

            var b = go.AddComponent<CommandRowButton>();
            b.group = group; b.accent = accent;
            b.body = body; b.ink = ink; b.foot = foot; b.icon = irt;
            b.panel = panel; b.inkImg = inkImg; b.numBox = numBox; b.footImg = footImg; b.iconImg = iconImg;
            b.keyText = keyText; b.labelText = labelText; b.subText = subText;
            b.transition = Transition.None;
            b.targetGraphic = panel;
            b.Go(Visual.Normal, true);
            return b;
        }
    }
}
