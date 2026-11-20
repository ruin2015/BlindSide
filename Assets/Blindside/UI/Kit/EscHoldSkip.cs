// The one way to skip anything (a cinematic, an intro, the combat stage, a tutorial): hold ESC for HoldSeconds. A hint sits bottom
// right — an ESC key cap, 「長押しでスキップ」 and a bar that fills while ESC is held; letting go drains it. While it is up it owns ESC
// (EscapeGate, before the game menu: execution order −200). With tapOpensMenu a short tap still opens the game menu (PauseMenu.
// RequestOpen) — for skips offered over a whole playable stretch (the tutorial), where the menu must stay reachable.
// The hold is ITimedEffect.Evaluate(t) (t = seconds held); Update only advances it with unscaled time. No allocation per frame.
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blindside.UI
{
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("Blindside/UI/Esc Hold Skip")]
    public class EscHoldSkip : MonoBehaviour, ITimedEffect
    {
        public const float HoldSeconds = 1f, DrainSeconds = .35f, TapMax = .25f, FadeIn = .4f, RestAlpha = .55f;
        public const float W = 230f, H = 40f, Right = 32f, Bottom = 28f;
        public const int SortingOrder = 900;
        public const string Label = "長押しでスキップ", LabelHolding = "スキップ中…";

        public RectTransform fill;
        public TMP_Text label;
        public CanvasGroup group;

        System.Action onSkip;
        GameObject owner;               // what goes when it is over (its own canvas)
        float hold, age, down, armAfter;
        bool fired, tapOpensMenu, pressedHere, pressTaken;

        public float Duration => HoldSeconds;
        public bool Fired => fired;

        /// <summary>Asked when ESC goes down: true when that press already belongs to something else (the battle's selection cancelling
        /// a mode) — then a short tap opens no menu. PlayerSelectionController sets it; TutorialSkipButton asks it too.</summary>
        public static System.Func<bool> TapTaken;

        /// <summary>Offers the skip: `onSkip` runs once ESC has been held HoldSeconds (counting from `armAfter` seconds on). `bottom`
        /// lifts the hint (over END TURN, say); `label` replaces 長押しでスキップ. Call End when the thing to skip is over by itself.</summary>
        public static EscHoldSkip Begin(System.Action onSkip, float armAfter = .3f, float bottom = Bottom, bool tapOpensMenu = false, string label = null)
        {
            if (!Application.isPlaying) return null;
            var go = new GameObject("EscHoldSkip");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            var s = Build(go.transform, bottom);
            s.owner = go;
            s.onSkip = onSkip; s.armAfter = armAfter; s.tapOpensMenu = tapOpensMenu;
            if (!string.IsNullOrEmpty(label)) { s.idleLabel = label; s.label.text = label; }
            s.group.alpha = 0f;
            return s;
        }

        string idleLabel = Label;

        /// <summary>Takes the hint away without skipping.</summary>
        public void End()
        {
            fired = true;
            if (this != null) Destroy(owner != null ? owner : gameObject);
        }

        /// <summary>The hint under `parent` (a canvas), bottom right, `bottom` up; previews pose it with Evaluate.</summary>
        public static EscHoldSkip Build(Transform parent, float bottom = Bottom)
        {
            var theme = UITheme.Current;
            var box = HudKit.NewRect("EscHoldSkip_Hint", parent);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(1f, 0f);
            box.anchoredPosition = new Vector2(-Right, bottom); box.sizeDelta = new Vector2(W, H);
            var s = box.gameObject.AddComponent<EscHoldSkip>();
            s.group = box.gameObject.AddComponent<CanvasGroup>();
            s.group.blocksRaycasts = false; s.group.interactable = false;

            var cap = HudKit.NewRect("Cap", box);
            UILayout.Place(cap, 0f, 4f, 44f, 24f);
            HudKit.AddImage(cap, new Color(1f, 1f, 1f, .9f));
            var ct = HudKit.AddText(cap, "ESC", 12f, theme.ink, true, TextAlignmentOptions.Center, "Key", UITheme.TextRole.Body);
            HudKit.Stretch(ct.rectTransform);
            s.label = HudKit.AddText(box, Label, 14f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Body);
            UILayout.Place(s.label.rectTransform, 54f, 4f, W - 54f, 24f);
            var track = HudKit.NewRect("DecoTrack", box);
            UILayout.Place(track, 0f, 34f, W, 3f);
            HudKit.AddImage(track, new Color(1f, 1f, 1f, .25f));
            s.fill = HudKit.NewRect("HoldFill", track);
            s.fill.anchorMin = new Vector2(0f, 0f); s.fill.anchorMax = new Vector2(0f, 1f); s.fill.pivot = new Vector2(0f, .5f);
            s.fill.anchoredPosition = Vector2.zero; s.fill.sizeDelta = Vector2.zero;
            HudKit.AddImage(s.fill, theme.blue);
            s.Evaluate(0f);
            return s;
        }

        /// <summary>The hold `t` seconds in: the bar full at HoldSeconds, the label スキップ中… while anything is held.</summary>
        public void Evaluate(float t)
        {
            float p = Mathf.Clamp01(t / HoldSeconds);
            float w = W * p;
            if (fill.sizeDelta.x != w) fill.sizeDelta = new Vector2(w, 0f);
            string s = p > 0f ? LabelHolding : idleLabel;
            if (label.text != s) label.text = s;
        }

        void Update()
        {
            if (fired || onSkip == null) return;
            float dt = Time.unscaledDeltaTime;
            age += dt;
            if (PauseMenu.IsOpen)
            {
                // the menu is up: ESC is its own (to close it), and nothing is held
                pressedHere = false;
                if (hold != 0f) { hold = 0f; Evaluate(0f); }
                if (group.alpha != 0f) group.alpha = 0f;
                return;
            }
            var kb = Keyboard.current;
            bool isDown = kb != null && kb.escapeKey.isPressed;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                pressTaken = TapTaken != null && TapTaken();   // (asked before the selection, order 0, cancels its mode)
                EscapeGate.Consume(); pressedHere = true; down = 0f;
            }
            if (isDown && pressedHere) down += dt;
            if (!isDown && pressedHere)
            {
                // a short tap: the game menu, as a plain ESC would have opened it (unless the press cancelled something)
                if (tapOpensMenu && down < TapMax && !pressTaken) PauseMenu.RequestOpen();
                pressedHere = false;
            }
            bool held = isDown && pressedHere && age >= armAfter;
            float before = hold;
            hold = held ? hold + dt : Mathf.Max(0f, hold - dt * HoldSeconds / DrainSeconds);
            if (hold != before) Evaluate(hold);
            // faint once armed, bright while held
            float a = Mathf.Clamp01((age - armAfter) / FadeIn) * (hold > 0f ? 1f : RestAlpha);
            if (group.alpha != a) group.alpha = a;
            if (hold >= HoldSeconds)
            {
                fired = true;
                var cb = onSkip; onSkip = null;
                cb();
                Destroy(owner != null ? owner : gameObject);
            }
        }
    }
}
