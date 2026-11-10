// Hold ESC to skip the helicopter intro (the Timeline one: TitleSelectScreen.Launch starts it with Begin). A hint sits bottom right —
// 「ESC 長押しでスキップ」 with a bar that fills while ESC is held (HoldSeconds); letting go drains it. When it fills, the intro is
// stopped the way its own end stops it (PlayableDirector.Stop → stopped: the match starts, IntroCameraRig cuts to the battlefield
// camera and clears its pieces), and what the Timeline would have left at its end is set by hand: the letterbox open, the black
// fade cleared, the title and the caption gone. ESC is taken from the game menu while the intro runs (EscapeGate; this runs before
// PauseMenu). Unscaled time; nothing is allocated per frame. It removes itself once the intro is over.
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.Cinematic
{
    [DefaultExecutionOrder(-200)]
    public class IntroSkip : MonoBehaviour
    {
        public const float HoldSeconds = 1f, DrainSeconds = .35f, ArmAfter = .5f;

        PlayableDirector director;
        RectTransform fill;
        CanvasGroup group;
        TMP_Text label;
        float hold, age;
        bool done;

        /// <summary>Watches `d` (the intro just started) for a held ESC.</summary>
        public static IntroSkip Begin(PlayableDirector d)
        {
            if (d == null || !Application.isPlaying) return null;
            var old = FindAnyObjectByType<IntroSkip>();
            if (old != null) Destroy(old.gameObject);
            var go = new GameObject("IntroSkip");
            var s = go.AddComponent<IntroSkip>();
            s.director = d;
            s.Build();
            return s;
        }

        void Build()
        {
            var theme = UITheme.Current;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            theme.ApplyTo(gameObject.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;

            var box = HudKit.NewRect("Hint", transform);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(1f, 0f);
            box.anchoredPosition = new Vector2(-32f, 28f); box.sizeDelta = new Vector2(230f, 40f);
            var cap = HudKit.NewRect("Cap", box);
            UILayout.Place(cap, 0f, 4f, 44f, 24f);
            HudKit.AddImage(cap, new Color(1f, 1f, 1f, .9f));
            var ct = HudKit.AddText(cap, "ESC", 12f, theme.ink, true, TextAlignmentOptions.Center, "Key", UITheme.TextRole.Body);
            HudKit.Stretch(ct.rectTransform);
            label = HudKit.AddText(box, "長押しでスキップ", 14f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Body);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(label.rectTransform, 54f, 4f, 176f, 24f);
            var track = HudKit.NewRect("Track", box);
            UILayout.Place(track, 0f, 34f, 230f, 3f);
            HudKit.AddImage(track, new Color(1f, 1f, 1f, .25f));
            fill = HudKit.NewRect("Fill", track);
            fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, .5f);
            fill.anchoredPosition = Vector2.zero; fill.sizeDelta = new Vector2(0f, 0f);
            HudKit.AddImage(fill, theme.blue);
        }

        void Update()
        {
            if (done) return;
            if (director == null || director.state != PlayState.Playing) { Destroy(gameObject); return; }
            float dt = Time.unscaledDeltaTime;
            age += dt;
            var kb = Keyboard.current;
            bool held = kb != null && kb.escapeKey.isPressed && age >= ArmAfter;
            // the intro owns ESC: the game menu doesn't open over it
            if (kb != null && kb.escapeKey.wasPressedThisFrame) EscapeGate.Consume();
            hold = held ? hold + dt : Mathf.Max(0f, hold - dt * HoldSeconds / DrainSeconds);
            float p = Mathf.Clamp01(hold / HoldSeconds);
            fill.sizeDelta = new Vector2(230f * p, 0f);
            // the hint: faint once the intro has begun, bright while held
            float a = Mathf.Clamp01((age - ArmAfter) / .4f) * (held || p > 0f ? 1f : .55f);
            if (group.alpha != a) group.alpha = a;
            if (p >= 1f) Skip();
        }

        void Skip()
        {
            done = true;
            // what the Timeline leaves at its end, set by hand (Stop doesn't play the rest of its tracks)
            var seq = FindAnyObjectByType<IntroSequence>(FindObjectsInactive.Include);
            if (seq != null)
            {
                seq.StopAllCoroutines();
                if (seq.fadeGroup != null) seq.fadeGroup.alpha = 0f;
                if (seq.titleGroup != null) seq.titleGroup.alpha = 0f;
                seq.HideCaption();
                var bt = seq.GetComponent<BlindsideTitle>();
                if (bt != null) bt.Evaluate(bt.Duration);   // (the title's OnGUI draws nothing past its end)
            }
            var bars = FindAnyObjectByType<LetterboxBars>(FindObjectsInactive.Include);
            if (bars != null) { bars.ClearOverride(); bars.openAmount = 0f; }
            // the end of the intro: the match starts (TitleSelectScreen.OnIntroStopped) and the camera cuts (IntroCameraRig)
            if (director != null && director.state == PlayState.Playing) director.Stop();
            Destroy(gameObject);
        }
    }
}
