// The defeat (the player's side lost a match): the turn change's banner (KitTurnBanner) held across the middle in the enemy's red —
// "MISSION FAILED" over a glitching DEFEAT — over a darkened screen, and under it only two buttons: [再挑戦] and [タイトルへ] ([エディタへ]
// for a map editor test play). Replaces the old result card (ResultScreen) for a loss; the win paths are unchanged.
//   DefeatScreen_Canvas  its own overlay canvas (sorting 100: over the HUD and every overlay; the 1440×810 menu scale)
//     DecoBackdrop       full screen, ink, fading to BackdropAlpha over BackdropTime — it takes every click on the world behind
//     TurnBanner         KitTurnBanner.PlayHold: band, red lines, MISSION FAILED, DEFEAT (RGB split, slices, a blip now and then)
//     Buttons            two TacticalButtons (ButtonW×ButtonH, ButtonGap apart), centred ButtonsY under the middle; they rise in
//                        (ButtonRise) and fade in from ButtonsAt over ButtonsTime, clickable once in
// ITimedEffect.Evaluate(t) poses it all (the banner too); Update only advances an unscaled clock. Nothing is allocated per frame.
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Defeat Screen")]
    public class KitDefeatScreen : MonoBehaviour, ITimedEffect
    {
        public const float BackdropAlpha = .62f, BackdropTime = .4f, ButtonsAt = 1.35f, ButtonsTime = .45f, ButtonRise = 18f;
        public const float ButtonW = 280f, ButtonH = 52f, ButtonGap = 24f, ButtonsY = 182f;
        public const string Label = "MISSION FAILED", Title = "DEFEAT";

        /// <summary>Previews draw into a canvas of the stage instead of the overlay this component makes.</summary>
        public Canvas externalCanvas;

        Canvas canvas;
        Image backdrop;
        KitTurnBanner banner;
        RectTransform buttons;
        CanvasGroup buttonsG;
        float clock;
        bool running;

        public bool Visible => canvas != null && canvas.gameObject.activeSelf;
        public float Duration => ButtonsAt + ButtonsTime;

        /// <summary>The defeat: the banner, then [再挑戦] and [タイトルへ] (`onEditor` set: [エディタへ] in its place).</summary>
        public void Show(Action onRetry, Action onTitle, Action onEditor = null)
        {
            Hide();
            EnsureCanvas();
            canvas.gameObject.SetActive(true);
            var root = canvas.transform;
            var theme = UITheme.Current;

            var back = HudKit.NewRect("DecoBackdrop", root);
            HudKit.Stretch(back);
            backdrop = HudKit.AddImage(back, new Color(theme.ink.r, theme.ink.g, theme.ink.b, 0f), raycast: true);

            banner = KitTurnBanner.Build(root);
            banner.PlayHold(Label, Title, UIPalette.Foe);

            buttons = HudKit.NewRect("Buttons", root);
            buttons.anchorMin = buttons.anchorMax = buttons.pivot = new Vector2(.5f, .5f);
            buttons.sizeDelta = new Vector2(ButtonW * 2f + ButtonGap, ButtonH);
            buttonsG = buttons.gameObject.AddComponent<CanvasGroup>();
            Button("Retry", "再挑戦", "RETRY", 0f, KitColor.Blue, KitColor.Ink, onRetry);
            if (onEditor != null) Button("Editor", "エディタへ", "EDITOR", ButtonW + ButtonGap, KitColor.Paper2, KitColor.Blue, onEditor);
            else Button("Title", "タイトルへ", "TITLE", ButtonW + ButtonGap, KitColor.Paper2, KitColor.Blue, onTitle);

            clock = 0f;
            running = Application.isPlaying;
            if (running) SoundBank.Play(Snd.UiGlitch, .8f);   // (the banner's glitch)
            Evaluate(0f);
        }

        public void Hide()
        {
            running = false;
            if (canvas == null) return;
            for (int i = canvas.transform.childCount - 1; i >= 0; i--) HudKit.DestroySafe(canvas.transform.GetChild(i).gameObject);
            if (canvas != externalCanvas) canvas.gameObject.SetActive(false);
            banner = null; buttons = null; buttonsG = null; backdrop = null;
        }

        void Button(string name, string label, string sub, float x, KitColor fill, KitColor hoverFill, Action onClick)
        {
            var b = TacticalButton.Create(buttons, "Btn" + name, "➜", label, sub, null, new Vector2(ButtonW, ButtonH));
            UILayout.Place((RectTransform)b.transform, x, 0f, ButtonW, ButtonH);
            b.fill = fill; b.hoverFill = hoverFill;
            b.Go(TacticalButton.Visual.Normal, true);
            b.onClick.AddListener(() => onClick?.Invoke());
        }

        void EnsureCanvas()
        {
            if (externalCanvas != null) { canvas = externalCanvas; return; }
            if (canvas != null) return;
            var go = new GameObject("DefeatScreen_Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;   // (as the result card: over the HUD and every overlay)
            UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
            go.AddComponent<GraphicRaycaster>();
            go.SetActive(false);
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            if (banner == null) return;
            bool reduce = UIMotionSettings.reduceMotion;
            banner.Evaluate(t);

            float b = reduce ? 1f : UIEase.Tactical(Mathf.Clamp01(t / BackdropTime));
            var c = backdrop.color;
            float ba = BackdropAlpha * b;
            if (c.a != ba) { c.a = ba; backdrop.color = c; }

            float p = Mathf.Clamp01((t - ButtonsAt) / ButtonsTime);
            float e = reduce ? (p > 0f ? 1f : 0f) : UIEase.Tactical(p);
            if (buttonsG.alpha != e) buttonsG.alpha = e;
            bool on = e >= 1f;
            if (buttonsG.interactable != on) { buttonsG.interactable = on; buttonsG.blocksRaycasts = on; }
            var pos = new Vector2(0f, -ButtonsY - (reduce ? 0f : ButtonRise * (1f - e)));
            if (buttons.anchoredPosition != pos) buttons.anchoredPosition = pos;
        }
    }
}
