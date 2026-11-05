// The kit's command button (#cmd button): a paper parallelogram (slant 10) with an ink number box, a Japanese label over a small display
// line and an optional value. It is a Button (onClick and every event hookup stay), with its own state look instead of a colour tint:
//   hover     the body moves right 10 and turns blue, texts white (.16s, tactical ease); the light sweep (Deco) runs once in .5s
//   pressed   still blue but darker, moved back to +6
//   disabled  everything greyscale, the whole button at alpha .4 (CanvasGroup), no sweep
// Clicks follow the parallelogram: the body's SlantedGraphic filters raycasts. The look is ITimedEffect.Evaluate(t) — a transition
// from the pose the button had to the new state's pose; Update only advances an unscaled clock. PoseState(state, t) poses it for a
// preview without a pointer. No allocation per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Tactical Button")]
    public class TacticalButton : Button, ITimedEffect
    {
        public enum Visual { Normal, Hover, Pressed, Disabled }

        public const float StateTime = .16f, HoverShift = 10f, PressShift = 6f, DisabledAlpha = .4f;

        [Header("Parts")]
        public RectTransform body;
        public TacticalPanel panel;
        public TMP_Text keyText, labelText, subText, valueText;
        public Image valueBg;                       // the value's chip (.s background); null without a value
        public KitSweep sweep;
        public CanvasGroup group;

        [Header("Colours")]
        public KitColor fill = KitColor.Paper;
        public KitColor hoverFill = KitColor.Blue;

        Visual target = Visual.Normal;
        float fromHover, toHover, fromPress, toPress, hover, press;
        bool sweepActive;
        float sweepAge0, clock;
        bool running;

        public Visual Current => target;

        // ---- colours ----------------------------------------------------------------------------------------------------------
        static Color Ink => UITheme.Current.ink;
        Color Fill => KitPalette.Get(fill);
        Color HoverFill => KitPalette.Get(hoverFill);
        Color PressFill => Color.Lerp(HoverFill, Ink, .22f);
        Color TextNormal => KitPalette.Get(KitPalette.TextOn(fill));
        Color TextHover => KitPalette.Get(KitPalette.TextOn(hoverFill));
        Color SubNormal => UITheme.Current.dim;
        Color SubHover => Color.Lerp(HoverFill, Color.white, .8f);          // rgba(255,255,255,.8) over the hover fill, as sRGB

        // ---- state ------------------------------------------------------------------------------------------------------------
        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
        }

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
            float age = sweepActive ? sweepAge0 + clock : 0f;
            if (v == Visual.Hover && (target == Visual.Normal || target == Visual.Disabled)) { sweepActive = true; sweepAge0 = 0f; }
            else if (sweepActive && (v == Visual.Hover || v == Visual.Pressed)) sweepAge0 = age;
            else sweepActive = false;

            fromHover = hover; fromPress = press;
            target = v;
            toHover = v == Visual.Hover || v == Visual.Pressed ? 1f : 0f;
            toPress = v == Visual.Pressed ? 1f : 0f;
            clock = 0f;
            if (instant || !Application.isPlaying) { sweepActive = sweepActive && !instant && Application.isPlaying; running = false; Evaluate(Duration); }
            else { running = true; Evaluate(0f); }
        }

        /// <summary>For previews: the transition from `from` to `state`, `t` seconds in (the sweep starts when Normal → Hover).</summary>
        public void PoseState(Visual state, float t, Visual from = Visual.Normal)
        {
            running = false;
            fromHover = from == Visual.Hover || from == Visual.Pressed ? 1f : 0f;
            fromPress = from == Visual.Pressed ? 1f : 0f;
            target = state;
            toHover = state == Visual.Hover || state == Visual.Pressed ? 1f : 0f;
            toPress = state == Visual.Pressed ? 1f : 0f;
            sweepActive = state == Visual.Hover && (from == Visual.Normal || from == Visual.Disabled);
            sweepAge0 = 0f;
            Evaluate(t);
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration
        {
            get
            {
                float d = StateTime;
                if (sweepActive && sweep != null) d = Mathf.Max(d, sweep.Duration - sweepAge0);
                return d;
            }
        }

        public void Evaluate(float t)
        {
            float e = UIEase.Tactical(t / StateTime);
            hover = Mathf.LerpUnclamped(fromHover, toHover, e);
            press = Mathf.LerpUnclamped(fromPress, toPress, e);
            bool disabled = target == Visual.Disabled;

            if (body != null) body.anchoredPosition = new Vector2(disabled ? 0f : HoverShift * hover - (HoverShift - PressShift) * press, 0f);

            Color f = Color.Lerp(Color.Lerp(Fill, HoverFill, hover), PressFill, press);
            Color txt = Color.Lerp(TextNormal, TextHover, hover);
            Color sub = Color.Lerp(SubNormal, SubHover, hover);
            Color band = Ink, key = Color.white;
            if (disabled)
            {
                f = HudKit.Grayscale(Fill); txt = HudKit.Grayscale(TextNormal); sub = HudKit.Grayscale(SubNormal);
                band = HudKit.Grayscale(band);
            }
            if (panel != null) { panel.color = f; panel.SetBandColor(band); }
            if (labelText != null) labelText.color = txt;
            if (valueText != null) valueText.color = txt;
            if (subText != null) subText.color = sub;
            if (keyText != null) keyText.color = key;
            if (valueBg != null)
            {
                // rgba(0,0,0,.09) → rgba(255,255,255,.25) on the hover fill, as opaque sRGB (alpha decoration is blended in sRGB)
                Color cn = Color.Lerp(Fill, Color.black, .09f), ch = Color.Lerp(Color.Lerp(HoverFill, PressFill, press), Color.white, .25f);
                valueBg.color = disabled ? HudKit.Grayscale(cn) : Color.Lerp(cn, ch, hover);
            }
            if (group != null) group.alpha = disabled ? DisabledAlpha : 1f;

            if (sweep != null)
            {
                sweep.over = f;
                if (sweepActive && !disabled) sweep.Evaluate(sweepAge0 + t); else sweep.Rest();
            }
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        /// <summary>Fits a one-line name into `avail` without touching the value chip: 15px as is → letter spacing −0.02em → 11px →
        /// (still too wide) an ellipsis, marked for the lint. Runs at build time only.</summary>
        public static void FitLabel(TMP_Text l, float avail)
        {
            l.fontSize = 15f; l.characterSpacing = 0f; l.overflowMode = TextOverflowModes.Overflow;
            avail += .5f;
            if (l.GetPreferredValues(l.text).x <= avail) return;
            l.characterSpacing = -2f;                                   // TMP spacing unit is 0.01em
            if (l.GetPreferredValues(l.text).x <= avail) return;
            l.fontSize = 11f;
            if (l.GetPreferredValues(l.text).x <= avail) return;
            l.overflowMode = TextOverflowModes.Ellipsis;
            UIUtil.GetOrAdd<LintIgnoreClip>(l.gameObject).reason = "a name too long even at 11px / −0.02em ends in an ellipsis rather than run under the value chip";
        }

        // ---- build ------------------------------------------------------------------------------------------------------------
        /// <summary>A command button `name` under `parent` (place it with UILayout.Place): number box 34 wide, label + small line,
        /// optional value on the right. Size defaults to UITheme hud.commandButton (226×40).</summary>
        public static TacticalButton Create(Transform parent, string name, string key, string label, string sub, string value = null, Vector2? size = null, UITheme.TextRole? labelRole = null)
        {
            var theme = UITheme.Current;
            Vector2 sz = size ?? theme.hud.commandButton;
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = sz;
            var group = go.AddComponent<CanvasGroup>();

            var bodyRt = HudKit.NewRect("Body", rt);
            HudKit.Stretch(bodyRt);
            var panel = bodyRt.gameObject.AddComponent<TacticalPanel>();
            panel.SetPreset(KitColor.Paper);
            panel.raycastTarget = true;
            panel.SetBand(TacticalPanel.Band.Clip, 34f, KitColor.Ink);
            var slant = bodyRt.gameObject.AddComponent<SlantedGraphic>();
            slant.Set(SlantedGraphic.Size.SmallMin);

            float h = sz.y;
            var k = HudKit.AddText(bodyRt, key, 15, Color.white, true, TextAlignmentOptions.Center, "Key", UITheme.TextRole.Display);
            UILayout.Place(k.rectTransform, 5, 0, 29, h);

            // value chip (.s: margin-right 16, padding 1px 7px, 12px display, background rgba(0,0,0,.09)): flush right, width = text + 14
            float textX = 34f + 12f, chipW = 0f, chipH = 17f;
            Image chipBg = null;
            TMP_Text v = null;
            if (!string.IsNullOrEmpty(value))
            {
                var chip = HudKit.NewRect("ValueChip", bodyRt);
                chipBg = HudKit.AddImage(chip, Color.Lerp(KitPalette.Get(KitColor.Paper), Color.black, .09f));
                v = HudKit.AddText(chip, value, 12, theme.ink, true, TextAlignmentOptions.Center, "Value", UITheme.TextRole.Display);
                chipW = Mathf.Ceil(v.GetPreferredValues(value).x) + 14f;
                UILayout.Place(chip, sz.x - 16f - chipW, (sz.y - chipH) * .5f, chipW, chipH);
                HudKit.Stretch(v.rectTransform);
            }
            // the name gets the width left of the chip (6 gap): −0.02em letter spacing first, then 11px, then an ellipsis (FitLabel)
            float textW = sz.x - textX - 16f - (chipW > 0f ? chipW + 6f : 0f);
            var l = HudKit.AddText(bodyRt, label, 15, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Label", labelRole);
            FitLabel(l, textW);
            var s = HudKit.AddText(bodyRt, sub, 11, theme.dim, true, TextAlignmentOptions.MidlineLeft, "Sub", UITheme.TextRole.Display);
            s.characterSpacing = 12f;
            float block = 15f + 2f + 14f, top = (h - block) * .5f;
            UILayout.Place(l.rectTransform, textX, top - 2f, textW, 19);
            UILayout.Place(s.rectTransform, textX, top + 17f, textW, 14);

            var sweepRt = HudKit.NewRect("DecoSweep", bodyRt);
            HudKit.Stretch(sweepRt);
            var sw = sweepRt.gameObject.AddComponent<KitSweep>();
            sw.raycastTarget = false;
            sw.MatchShape(slant);
            sw.Rest();

            var b = go.AddComponent<TacticalButton>();
            b.transition = Transition.None;
            b.targetGraphic = panel;
            b.body = bodyRt; b.panel = panel; b.keyText = k; b.labelText = l; b.subText = s; b.valueText = v; b.valueBg = chipBg; b.sweep = sw; b.group = group;
            b.Go(Visual.Normal, true);
            return b;
        }
    }
}
