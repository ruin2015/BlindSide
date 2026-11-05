// End-of-match result screen, ported from finish() (05_ui_input_intro.js ~443-453) and its CSS
// (00_markup_and_styles.html ~207-211 .mbtns, ~283-296 #result/.rs/.rs-h/.rs-b/.rs-st, keyframes panelIn / textSlide).
//   #result  full-screen rgba(4,6,9,.8) backdrop, card centred (width min(580px,92vw))
//   .rs-h    blue (--blu) on success, red (--foe) on failure: small "OPERATION SUCCESS|FAILED",
//            h2 46px "MISSION COMPLETE|FAILED" (textSlide 1s), p 16px "作戦成功|失敗"
//   .rs-b    the reason + three stat boxes (ROUNDS / 敵撃破 / 味方の損失)
//   .mbtns   [再挑戦] (blue) [タイトルへ] (paper3)
// The card is revealed left→right over .5s (panelIn); the title slides in from the left (textSlide).
// The screen is built in code (like PlayerSelectionController's HUD) — the old HUD_ResultScreen.prefab has a single
// button and no reason/stat row, so it can't carry this layout.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    public class ResultScreen : MonoBehaviour
    {
        const float RevealSeconds = .5f, TitleSlideSeconds = 1f;
        static readonly Color Backdrop = new Color32(4, 6, 9, (byte)(.8f * 255));   // rgba(4,6,9,.8)
        static readonly Color Body = UIPalette.Hex("#E3E6E9");                       // --pp
        static readonly Color Paper3 = UIPalette.Hex("#C3C8CD");

        Canvas canvas;
        RectTransform card;
        Coroutine anim;

        /// <summary>Currently on screen.</summary>
        public bool Visible => canvas != null && canvas.gameObject.activeSelf;

        /// <summary>`onEditor` is set for a test play from the map editor: the buttons are then [再挑戦] and [エディタへ] (no way to the title
        /// from there, which would throw the editor's unsaved map away); otherwise [再挑戦] and [タイトルへ].</summary>
        public void Show(bool win, string reason, int rounds, int kills, int losses, Action onRetry, Action onTitle, Action onEditor = null)
        {
            Hide();
            EnsureCanvas();
            canvas.gameObject.SetActive(true);

            var back = NewRect("Backdrop", canvas.transform);
            Stretch(back);
            AddImage(back, Backdrop, true);   // swallows every click on the world behind

            card = NewRect("Card", back);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(580f, 0f);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            var mask = card.gameObject.AddComponent<RectMask2D>();
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- .rs-h ----
            var head = NewRect("Header", card);
            AddImage(head, win ? UIPalette.Blue : UIPalette.Foe);
            Vertical(head, 4, new RectOffset(24, 24, 22, 22));
            var small = Text(head, win ? "OPERATION SUCCESS" : "OPERATION FAILED", 11, new Color(1, 1, 1, .8f), false, TextAlignmentOptions.MidlineLeft);
            small.characterSpacing = 14;   // letter-spacing:.14em
            var titleBox = NewRect("TitleBox", head);
            LE(titleBox, prefH: 50);
            var title = Text(titleBox, win ? "MISSION COMPLETE" : "MISSION FAILED", 43, Color.white, true, TextAlignmentOptions.MidlineLeft);   // 43: "MISSION COMPLETE" fits the card in BSTactical
            Stretch(title.rectTransform);
            title.characterSpacing = 14;
            Text(head, win ? "作戦成功" : "作戦失敗", 16, Color.white, true, TextAlignmentOptions.MidlineLeft);

            // ---- .rs-b ----
            var body = NewRect("Body", card);
            AddImage(body, Body);
            Vertical(body, 10, new RectOffset(24, 24, 16, 16));
            var why = Text(body, reason, 13, UIPalette.Ink2, false, TextAlignmentOptions.TopLeft);
            why.textWrappingMode = TextWrappingModes.Normal;
            var stats = NewRect("Stats", body);
            var hl = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4; hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = false;
            Stat(stats, "ROUNDS", Mathf.Max(1, rounds).ToString());
            Stat(stats, "敵撃破", kills.ToString());
            Stat(stats, "味方の損失", losses.ToString());

            // ---- .mbtns ----
            var row = NewRect("Buttons", card);
            var bl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            bl.childControlWidth = bl.childControlHeight = true;
            bl.childForceExpandWidth = true; bl.childForceExpandHeight = true;
            Button(row, "再挑戦", UIPalette.Blue, Color.white, onRetry);
            if (onEditor != null) Button(row, "エディタへ", Paper3, UIPalette.Ink, onEditor);
            else Button(row, "タイトルへ", Paper3, UIPalette.Ink, onTitle);

            // Reveal: the layout has to be settled first so the mask knows the card's width.
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            anim = StartCoroutine(Play(mask, group, title));
            if (win) SfxNoise.Ui(); else SfxNoise.Boom();   // sfx(win?'ui':'boom')
        }

        public void Hide()
        {
            if (anim != null) { StopCoroutine(anim); anim = null; }
            if (canvas == null) return;
            for (int i = canvas.transform.childCount - 1; i >= 0; i--) Destroy(canvas.transform.GetChild(i).gameObject);
            canvas.gameObject.SetActive(false);
            card = null;
        }

        IEnumerator Play(RectMask2D mask, CanvasGroup group, TMP_Text title)
        {
            // textSlide starts at opacity 0, so the title is parked before the card is revealed.
            var tr = title.rectTransform;
            title.alpha = 0f; tr.offsetMin = tr.offsetMax = new Vector2(-60f, 0f); title.characterSpacing = 50;
            // panelIn .5s and textSlide 1s start together (the card's mask clips the title while it is still open).
            StartCoroutine(UIAnim.ClipReveal(mask, group, RevealSeconds));
            // textSlide: 0% translateX(-60px) spacing .5em opacity 0 → 20% none, .2em, opacity 1 (then holds).
            float t = 0f, dur = TitleSlideSeconds * .2f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = UIEase.Tactical(t / dur);
                tr.offsetMin = tr.offsetMax = new Vector2(Mathf.Lerp(-60f, 0f, p), 0f);
                title.characterSpacing = Mathf.Lerp(50f, 20f, p);
                title.alpha = Mathf.Clamp01(t / dur);
                yield return null;
            }
            tr.offsetMin = tr.offsetMax = Vector2.zero; title.characterSpacing = 20; title.alpha = 1f;
            anim = null;
        }

        // ================================================================
        //  building blocks
        // ================================================================
        void EnsureCanvas()
        {
            if (canvas != null) return;
            var go = new GameObject("ResultScreen_Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;   // #result z-index 90: above the HUD (≤ 60), the dialog and every overlay
            UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
            go.AddComponent<GraphicRaycaster>();
            go.SetActive(false);
        }

        void Stat(RectTransform parent, string label, string value)
        {
            var box = NewRect("Stat", parent);
            AddImage(box, UIPalette.Ink2);
            Vertical(box, 0, new RectOffset(10, 10, 8, 8));
            Text(box, label, 9, new Color(1, 1, 1, .55f), false, TextAlignmentOptions.MidlineLeft);
            Text(box, value, 26, Color.white, true, TextAlignmentOptions.MidlineLeft);
        }

        void Button(RectTransform parent, string label, Color bg, Color fg, Action onClick)
        {
            var rt = NewRect("Btn_" + label, parent);
            var img = AddImage(rt, bg, true);
            var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white; cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);   // :hover{filter:brightness(1.1)}
            cb.pressedColor = new Color(.85f, .85f, .85f, 1f); cb.selectedColor = Color.white;
            btn.colors = cb;
            btn.onClick.AddListener(() => { SfxNoise.Click(); onClick?.Invoke(); });   // sfx('click')
            LE(rt, prefH: 44);
            var t = Text(rt, label, 14, fg, true, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image AddImage(RectTransform rt, Color c, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = raycast;
            return img;
        }

        static void Vertical(RectTransform rt, float spacing, RectOffset pad)
        {
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing; g.padding = pad; g.childAlignment = TextAnchor.UpperLeft;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = true; g.childForceExpandHeight = false;
        }

        static void LE(Component c, float prefH)
        {
            var le = UIUtil.GetOrAdd<LayoutElement>(c.gameObject);
            le.preferredHeight = prefH;
        }

        static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, bool bold, TextAlignmentOptions align)
        {
            var rt = NewRect("Text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            UIFonts.Auto(t, bold);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
