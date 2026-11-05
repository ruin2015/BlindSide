// The tutorial's SKIP: a slanted chip at the bottom right (above the 戦闘演出 option, which sits above END TURN). It has to be held
// down for HoldTime — a yellow bar fills along its bottom edge and the label turns to スキップ中… — so a stray click never throws the
// lesson away; letting go early drains the bar again. A full hold calls OnSkip once.
// ITimedEffect.Evaluate(t) poses the hold `t` seconds in (unscaled clock; no allocation per frame).
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialSkipButton : MonoBehaviour, ITimedEffect, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const float W = 224f, H = 36f, Right = 20f, Bottom = 124f, HoldTime = .9f, Drain = 2.5f, Slant = 10f;
        public const string Label = "長押しでスキップ", LabelHolding = "スキップ中…";

        public RectTransform fill;
        public TMP_Text label;
        public Image box;
        public System.Action OnSkip;
        float held; bool down, fired;

        public float Duration => HoldTime;

        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) down = true; }
        public void OnPointerUp(PointerEventData e) => down = false;
        public void OnPointerExit(PointerEventData e) => down = false;

        /// <summary>Back to untouched (a new tutorial, or after it was hidden).</summary>
        public void ResetHold() { held = 0f; down = false; fired = false; Evaluate(0f); }

        void Update()
        {
            if (fired) return;
            float dt = Time.unscaledDeltaTime;
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
            b.box.raycastTarget = true;
            rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, Slant);

            var band = NewRect("DecoSkipBand", rt);
            band.anchorMin = new Vector2(0f, 0f); band.anchorMax = new Vector2(0f, 1f); band.pivot = new Vector2(0f, .5f);
            band.sizeDelta = new Vector2(4f + Slant, 0f); band.anchoredPosition = Vector2.zero;
            var bandImg = band.gameObject.AddComponent<Image>(); bandImg.color = UIPalette.Blue; bandImg.raycastTarget = false;
            band.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, Slant);

            var tag = Text(rt, "SKIP", 12, UIPalette.Blue, UITheme.TextRole.Display, "Tag");
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
