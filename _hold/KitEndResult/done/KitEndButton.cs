// The HUD's END TURN button (#end, ui_spec "턴 종료"): 300×56, bottom right (x 20, bottom 20), a blue parallelogram whose top-left corner is
// cut by 18 (SlantedGraphic LeftEdge), dark warning stripes flowing over it (HazardStripe, Deco, one cell per 1.2s), "END TURN" 22 display
// (.12em) over "ターン終了" 12 body on the left, and a 60 wide ink block with "▶▶" 20 display on the right.
// It is a Button (onClick, interactable and SetActive keep working for GameplayUIBridge / PlayerSelectionController) with the same state
// grammar as TacticalButton, as ITimedEffect.Evaluate(t) from the pose it had to the new one (Update only advances an unscaled clock):
//   hover     brighter (+shine) and the light sweep runs once (.5s)          pressed   darker
//   disabled  greyscale, the whole button at alpha .4, stripes stopped, no sweep (not the player's turn / a modal is open)
//   pulse     (SetPulse, called through UIPulse.SetOn when every unit has acted) brightness 1.0 ↔ 1.25 over 1.2s, the right block turns acc
// Brightness is a white overlay (DecoShine): alpha decoration is blended in sRGB terms, not by the linear framebuffer. No allocation per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit End Button")]
    public class KitEndButton : Button, ITimedEffect
    {
        public const float W = 300f, H = 56f, Slant = 18f, ArrowW = 60f, StateTime = .16f, PulsePeriod = 1.2f;
        const float ShineHover = .10f, ShinePulse = .19f, PressDark = .18f, DisabledAlpha = .4f, MixTime = .16f;

        public Image body, shine, arrowBg;
        public HazardStripe stripe;
        public KitSweep sweep;
        public CanvasGroup group;
        public TMP_Text main, sub, arrow;

        TacticalButton.Visual target = TacticalButton.Visual.Normal;
        float fromHover, toHover, fromPress, toPress, hover, press;
        float clock, sweepAge0;
        bool running, sweepActive;
        bool pulse; float pulseClock, pulseMix;

        static Color Blue => UITheme.Current.blue;
        static Color Ink => UITheme.Current.ink;

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            TacticalButton.Visual v;
            switch (state)
            {
                case SelectionState.Highlighted: v = TacticalButton.Visual.Hover; break;
                case SelectionState.Pressed: v = TacticalButton.Visual.Pressed; break;
                case SelectionState.Disabled: v = TacticalButton.Visual.Disabled; break;
                default: v = TacticalButton.Visual.Normal; break;
            }
            Go(v, instant);
        }

        /// <summary>Starts the transition to `v` from the current pose (instant: jump to its end).</summary>
        public void Go(TacticalButton.Visual v, bool instant = false)
        {
            float age = sweepActive ? sweepAge0 + clock : 0f;
            if (v == TacticalButton.Visual.Hover && (target == TacticalButton.Visual.Normal || target == TacticalButton.Visual.Disabled)) { sweepActive = true; sweepAge0 = 0f; }
            else if (sweepActive && (v == TacticalButton.Visual.Hover || v == TacticalButton.Visual.Pressed)) sweepAge0 = age;
            else sweepActive = false;
            fromHover = hover; fromPress = press;
            target = v;
            toHover = v == TacticalButton.Visual.Hover || v == TacticalButton.Visual.Pressed ? 1f : 0f;
            toPress = v == TacticalButton.Visual.Pressed ? 1f : 0f;
            clock = 0f;
            if (instant || !Application.isPlaying) { sweepActive = sweepActive && !instant && Application.isPlaying; running = false; Evaluate(Duration); }
            else { running = true; Evaluate(0f); }
        }

        /// <summary>For previews: the transition from `from` to `state`, `t` seconds in (the sweep starts when Normal → Hover).</summary>
        public void PoseState(TacticalButton.Visual state, float t, TacticalButton.Visual from = TacticalButton.Visual.Normal)
        {
            running = false;
            fromHover = from == TacticalButton.Visual.Hover || from == TacticalButton.Visual.Pressed ? 1f : 0f;
            fromPress = from == TacticalButton.Visual.Pressed ? 1f : 0f;
            target = state;
            toHover = state == TacticalButton.Visual.Hover || state == TacticalButton.Visual.Pressed ? 1f : 0f;
            toPress = state == TacticalButton.Visual.Pressed ? 1f : 0f;
            sweepActive = state == TacticalButton.Visual.Hover && (from == TacticalButton.Visual.Normal || from == TacticalButton.Visual.Disabled);
            sweepAge0 = 0f;
            Evaluate(t);
        }

        /// <summary>The pulse on / off. In Play it runs from the start of its wave; out of Play it is posed at its peak (previews).</summary>
        public void SetPulse(bool on)
        {
            pulse = on;
            pulseClock = 0f;
            if (!Application.isPlaying) { pulseMix = on ? 1f : 0f; pulseClock = PulsePeriod * .5f; Apply(); }
            else Apply();
        }

        /// <summary>Previews: poses the pulse at `t` seconds into its wave (peak at 0.6s).</summary>
        public void PosePulse(float t) { pulse = true; pulseMix = 1f; pulseClock = t; Apply(); }

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
            Apply();
            if (sweep != null)
            {
                sweep.over = Blue;
                if (sweepActive && target != TacticalButton.Visual.Disabled) sweep.Evaluate(sweepAge0 + t); else sweep.Rest();
            }
        }

        // the look from the current hover / press / pulse numbers
        void Apply()
        {
            bool dis = target == TacticalButton.Visual.Disabled;
            bool still = dis || UIMotionSettings.reduceMotion;
            if (stripe != null && stripe.scroll == still) stripe.SetScroll(!still);

            float wave = UIMotionSettings.reduceMotion ? .5f : (1f - Mathf.Cos(pulseClock / PulsePeriod * Mathf.PI * 2f)) * .5f;
            float k = dis ? 0f : pulseMix * wave;
            float mix = dis ? 0f : pulseMix;

            Color bodyCol = Blue, dark = Color.Lerp(Blue, Color.black, .1f), ink = Color.Lerp(Ink, KitPalette.Get(KitColor.Acc), mix);
            Color arrowCol = Color.Lerp(Color.white, Ink, mix), txt = Color.white, subCol = Color.Lerp(Blue, Color.white, .9f);
            if (dis)
            {
                bodyCol = HudKit.Grayscale(bodyCol); dark = HudKit.Grayscale(dark); ink = HudKit.Grayscale(ink);
                arrowCol = HudKit.Grayscale(arrowCol); txt = HudKit.Grayscale(txt); subCol = HudKit.Grayscale(subCol);
            }
            body.color = bodyCol;
            if (stripe != null && (stripe.stripeColor != dark || stripe.gapColor != bodyCol)) { stripe.stripeColor = dark; stripe.gapColor = bodyCol; stripe.SetVerticesDirty(); }
            arrowBg.color = ink; arrow.color = arrowCol;
            main.color = txt; sub.color = subCol;

            Color sh = press > 0f ? new Color(0f, 0f, 0f, PressDark * press) : new Color(1f, 1f, 1f, Mathf.Max(ShineHover * hover, ShinePulse * k));
            if (dis) sh.a = 0f;
            shine.color = sh;
            group.alpha = dis ? DisabledAlpha : 1f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (running)
            {
                clock += dt;
                Evaluate(clock);
                if (clock >= Duration) running = false;
            }
            float goal = pulse ? 1f : 0f;
            if (pulse || pulseMix != goal)
            {
                pulseClock += dt;
                pulseMix = Mathf.MoveTowards(pulseMix, goal, dt / MixTime);
                Apply();
            }
        }

        // ---- build ------------------------------------------------------------------------------------------------------------
        /// <summary>The button under `parent` at the end-turn place (a child filling the 300×56 root): returns the KitEndButton.</summary>
        public static KitEndButton Create(RectTransform parent)
        {
            var theme = UITheme.Current;
            var go = new GameObject("Button", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            HudKit.Stretch(rt);
            var group = go.AddComponent<CanvasGroup>();

            var bodyRt = HudKit.NewRect("Body", rt);
            HudKit.Stretch(bodyRt);
            var body = HudKit.AddImage(bodyRt, theme.blue, raycast: true);
            var slant = bodyRt.gameObject.AddComponent<SlantedGraphic>();
            slant.Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.LeftEdge, Slant);

            var stripeRt = HudKit.NewRect("DecoStripe", bodyRt);
            HudKit.Stretch(stripeRt);
            var stripe = stripeRt.gameObject.AddComponent<HazardStripe>();
            stripe.raycastTarget = false;
            stripe.stripeWidth = 10f; stripe.period = 20f; stripe.cycle = 1.2f;
            stripe.stripeColor = Color.Lerp(theme.blue, Color.black, .1f); stripe.gapColor = theme.blue;
            stripe.over = theme.blue;
            stripe.MatchShape(slant);
            stripe.Evaluate(0f);

            var sweepRt = HudKit.NewRect("DecoSweep", bodyRt);
            HudKit.Stretch(sweepRt);
            var sweep = sweepRt.gameObject.AddComponent<KitSweep>();
            sweep.raycastTarget = false;
            sweep.MatchShape(slant);
            sweep.Rest();

            var shineRt = HudKit.NewRect("DecoShine", bodyRt);
            HudKit.Stretch(shineRt);
            var shine = HudKit.AddImage(shineRt, new Color(1f, 1f, 1f, 0f));
            shineRt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.LeftEdge, Slant);

            // #end .l{padding:9px 20px 0 36px}: b 22/1, small 12/1.25
            float colW = W - 36f - ArrowW - 20f;
            var main = HudKit.AddText(rt, "END TURN", 22f, Color.white, true, TextAlignmentOptions.TopLeft, "Main", UITheme.TextRole.Display);
            main.characterSpacing = 12f;
            UILayout.LineHeight(main, 1f);
            UILayout.Place(main.rectTransform, 36f, 9f, colW, 22f);
            var sub = HudKit.AddText(rt, "ターン終了", 12f, Color.Lerp(theme.blue, Color.white, .9f), false, TextAlignmentOptions.TopLeft, "Sub", UITheme.TextRole.Body);
            UILayout.LineHeight(sub, 1.25f);
            UILayout.Place(sub.rectTransform, 36f, 31f, colW, 16f);

            var arrowRt = HudKit.NewRect("Arrow", rt);
            arrowRt.anchorMin = new Vector2(1f, 0f); arrowRt.anchorMax = new Vector2(1f, 1f); arrowRt.pivot = new Vector2(1f, .5f);
            arrowRt.anchoredPosition = Vector2.zero; arrowRt.sizeDelta = new Vector2(ArrowW, 0f);
            var arrowBg = HudKit.AddImage(arrowRt, theme.ink);
            var arrow = HudKit.AddText(arrowRt, "▶▶", 20f, Color.white, true, TextAlignmentOptions.Center, "Glyph", UITheme.TextRole.Display);
            arrow.characterSpacing = -10f;
            HudKit.Stretch(arrow.rectTransform);

            var b = go.AddComponent<KitEndButton>();
            b.transition = Transition.None;
            b.targetGraphic = body;
            b.body = body; b.shine = shine; b.arrowBg = arrowBg; b.stripe = stripe; b.sweep = sweep; b.group = group;
            b.main = main; b.sub = sub; b.arrow = arrow;
            b.Go(TacticalButton.Visual.Normal, true);
            return b;
        }
    }
}
