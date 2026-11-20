// The tutorial's SKIP: a slanted chip at the bottom right (above the 戦闘演出 option, which sits above END TURN) saying ESC
// 長押しでスキップ — every skip in the game is a held ESC (EscHoldSkip). Holding ESC for HoldTime fills a yellow bar along its bottom
// edge (the label turns to スキップ中…), so a stray press never throws the lesson away; letting go early drains it. A full hold calls
// OnSkip once. ESC is the chip's while it is up (execution order −200, before the game menu); a short tap still opens the game
// menu (PauseMenu.RequestOpen), and while the menu is open ESC is the menu's.
// ITimedEffect.Evaluate(t) poses the hold `t` seconds in (unscaled clock; no allocation per frame).
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Tutorial
{
    [DefaultExecutionOrder(-200)]
    public class TutorialSkipButton : MonoBehaviour, ITimedEffect
    {
        public const float W = 224f, H = 36f, Right = 20f, Bottom = 124f, HoldTime = .9f, Drain = 2.5f, Slant = 10f, TapMax = .25f;
        public const string Label = "長押しでスキップ", LabelHolding = "スキップ中…";

        public RectTransform fill;
        public TMP_Text label;
        public Image box;
        public System.Action OnSkip;
        float held, downFor; bool down, fired, taken;

        public float Duration => HoldTime;

        /// <summary>Back to untouched (a new tutorial, or after it was hidden).</summary>
        public void ResetHold() { held = 0f; down = false; fired = false; Evaluate(0f); }

        void Update()
        {
            if (fired) return;
            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            if (PauseMenu.IsOpen) down = false;                                   // the menu's ESC
            else if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) { taken = EscHoldSkip.TapTaken != null && EscHoldSkip.TapTaken(); EscapeGate.Consume(); down = true; downFor = 0f; }
                if (down && kb.escapeKey.isPressed) downFor += dt;
                else if (down) { down = false; if (downFor < TapMax && !taken) PauseMenu.RequestOpen(); }   // (a tap that cancelled a mode opens no menu)
            }
            float before = held;
            held = down ? held + dt : Mathf.Max(0f, held - dt * Drain);
            if (held != before) Evaluate(held);
            if (held >= HoldTime) { fired = true; down = false; OnSkip?.Invoke(); }
        }

        public void Evaluate(float t)
        {
            float p = Mathf.Clamp01(t / HoldTime);
            fill.sizeDelta = new Vector2((W - Slant) * p, fill.sizeDelta.y);
            string s = p > 0f ? LabelHolding : Label;
            if (label.text != s) label.text = s;
        }

        // ================================================================
        //  Construction (on the guide's canvas, which has the raycaster)
        // ================================================================
        public static TutorialSkipButton Build(Transform canvas)
        {
            var rt = new GameObject("TutorialSkipButton", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-Right, Bottom); rt.sizeDelta = new Vector2(W, H);
            var b = rt.gameObject.AddComponent<TutorialSkipButton>();

            b.box = rt.gameObject.AddComponent<Image>();
            b.box.color = new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .92f);
            b.box.raycastTarget = false;      // (no clicks: ESC is the skip)
            rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, Slant);

            var band = NewRect("DecoSkipBand", rt);
            band.anchorMin = new Vector2(0f, 0f); band.anchorMax = new Vector2(0f, 1f); band.pivot = new Vector2(0f, .5f);
            band.sizeDelta = new Vector2(4f + Slant, 0f); band.anchoredPosition = Vector2.zero;
            var bandImg = band.gameObject.AddComponent<Image>(); bandImg.color = UIPalette.Blue; bandImg.raycastTarget = false;
            band.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, Slant);

            var tag = Text(rt, "ESC", 12, UIPalette.Blue, UITheme.TextRole.Display, "Tag");
            tag.characterSpacing = 10f;
            Place(tag.rectTransform, 24f, 0f, 60f, H);
            b.label = Text(rt, Label, 14, Color.white, UITheme.TextRole.Body, "Label");
            Place(b.label.rectTransform, 80f, 0f, W - 80f - 14f, H);

            // the hold bar along the bottom edge
            var fill = NewRect("HoldFill", rt);
            fill.anchorMin = fill.anchorMax = new Vector2(0f, 0f); fill.pivot = new Vector2(0f, 0f);
            fill.anchoredPosition = new Vector2(Slant * .5f, 0f); fill.sizeDelta = new Vector2(0f, 3f);
            var fi = fill.gameObject.AddComponent<Image>(); fi.color = UIPalette.Accent; fi.raycastTarget = false;
            b.fill = fill;

            b.Evaluate(0f);
            return b;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        static TMP_Text Text(RectTransform parent, string s, float size, Color c, UITheme.TextRole role, string name)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.color = c; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UIFonts.Apply(t, role);
            return t;
        }
    }
}
