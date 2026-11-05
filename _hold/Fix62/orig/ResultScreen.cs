// End-of-match result screen, ported from finish() (05_ui_input_intro.js ~443-453), re-skinned with the kit in Phase 4-5. The call API
// (Show / Hide / Visible) and the layout are unchanged: a 580 wide card centred on a full-screen backdrop —
//   Header   blue (win) or foe (loss), a parallelogram (slant 12): "OPERATION SUCCESS|FAILED" 11, the title 43 display (UIGlitchText),
//            "作戦成功|失敗" 16
//   Body     the reason (13, wraps inside the card) and three stat boxes (ink2 parallelograms: label 11, value 26 display)
//   Buttons  [再挑戦] [タイトルへ | エディタへ] — TacticalButtons (280×44 in a 290 cell, so the hover shift stays inside the card)
// New: the backdrop is rgba(4,6,9,.7) with the banner's slanted ink band and its dashed lines behind the card.
// Entrance (ITimedEffect.Evaluate(t); Update only advances an unscaled clock): the band is revealed over .3s; then header / body / buttons
// fade in over .5s each, starting at 0 / 200 / 400ms (.9s in all); the title also slides in from the left (60) while its letter spacing
// closes, and glitches for .8s (UIGlitchText burst). With UIMotionSettings.reduceMotion: no slide, no glitch, no flowing lines — fades only.
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    public class ResultScreen : MonoBehaviour, ITimedEffect
    {
        public const float EnterTime = .5f, Step = .2f, BandTime = .3f, GlitchTime = .8f, CardW = 580f;
        static readonly Color Backdrop = new Color32(4, 6, 9, (byte)(.7f * 255));    // rgba(4,6,9,.7)
        static readonly Color Body = UIPalette.Hex("#E3E6E9");                       // --pp

        /// <summary>Previews draw into a canvas of the stage instead of the screen-space overlay this component makes.</summary>
        public Canvas externalCanvas;

        Canvas canvas;
        RectTransform card, titleRt, bandRt;
        CanvasGroup headG, bodyG, btnG, bandLinesG;
        KitBannerStrip band;
        RawImage lineTop, lineBottom;
        TMP_Text title;
        UIGlitchText glitch;
        float clock, lineClock;
        bool running;

        /// <summary>Currently on screen.</summary>
        public bool Visible => canvas != null && canvas.gameObject.activeSelf;

        /// <summary>`onEditor` is set for a test play from the map editor: the buttons are then [再挑戦] and [エディタへ] (no way to the title
        /// from there, which would throw the editor's unsaved map away); otherwise [再挑戦] and [タイトルへ].</summary>
        public void Show(bool win, string reason, int rounds, int kills, int losses, Action onRetry, Action onTitle, Action onEditor = null)
        {
            Hide();
            EnsureCanvas();
            canvas.gameObject.SetActive(true);
            var theme = UITheme.Current;

            var back = NewRect("Backdrop", canvas.transform);
            Stretch(back);
            AddImage(back, Backdrop, true);   // swallows every click on the world behind

            // the band (as the phase banner's) behind the card; its height is set once the card is laid out
            bandRt = NewRect("DecoBand", back);
            bandRt.anchorMin = new Vector2(0f, .5f); bandRt.anchorMax = new Vector2(1f, .5f); bandRt.pivot = new Vector2(.5f, .5f);
            bandRt.anchoredPosition = Vector2.zero; bandRt.sizeDelta = new Vector2(0f, 320f);
            band = bandRt.gameObject.AddComponent<KitBannerStrip>();
            band.raycastTarget = false;
            band.color = new Color(theme.ink.r, theme.ink.g, theme.ink.b, .95f);
            var linesRt = NewRect("DecoBandLines", bandRt);
            Stretch(linesRt);
            bandLinesG = linesRt.gameObject.AddComponent<CanvasGroup>();
            bandLinesG.blocksRaycasts = false;
            lineTop = Line(linesRt, "DecoLineTop", true);
            lineBottom = Line(linesRt, "DecoLineBottom", false);

            card = NewRect("Card", back);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(CardW, 0f);
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- .rs-h ----
            var head = NewRect("Header", card);
            AddImage(head, win ? theme.blue : theme.foe);
            head.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            headG = head.gameObject.AddComponent<CanvasGroup>();
            Vertical(head, 4, new RectOffset(30, 30, 22, 22));
            var small = Text(head, win ? "OPERATION SUCCESS" : "OPERATION FAILED", 11, Color.Lerp(win ? theme.blue : theme.foe, Color.white, .8f), false, TextAlignmentOptions.MidlineLeft);
            small.characterSpacing = 14;   // letter-spacing:.14em
            var titleBox = NewRect("TitleBox", head);
            LE(titleBox, prefH: 50);
            title = Text(titleBox, win ? "MISSION COMPLETE" : "MISSION FAILED", 43, Color.white, true, TextAlignmentOptions.MidlineLeft);   // 43: "MISSION COMPLETE" fits the card in BSTactical
            titleRt = title.rectTransform;
            Stretch(titleRt);
            title.characterSpacing = 14;
            glitch = title.gameObject.AddComponent<UIGlitchText>();
            glitch.source = title; glitch.loop = false;
            title.gameObject.AddComponent<LintIgnoreClip>().reason = "等場スライド中にのみ発生、定着状態は0件 (occurs only during the entrance slide; the settled state is 0)";
            Text(head, win ? "作戦成功" : "作戦失敗", 16, Color.white, true, TextAlignmentOptions.MidlineLeft);

            // ---- .rs-b ----
            var body = NewRect("Body", card);
            AddImage(body, Body);
            bodyG = body.gameObject.AddComponent<CanvasGroup>();
            Vertical(body, 10, new RectOffset(30, 30, 16, 16));
            var why = Text(body, reason, 13, theme.ink2, false, TextAlignmentOptions.TopLeft);
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
            btnG = row.gameObject.AddComponent<CanvasGroup>();
            var bl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            bl.childControlWidth = bl.childControlHeight = true;
            bl.childForceExpandWidth = true; bl.childForceExpandHeight = true;
            Button(row, "再挑戦", KitColor.Blue, KitColor.Ink, onRetry);
            if (onEditor != null) Button(row, "エディタへ", KitColor.Paper2, KitColor.Blue, onEditor);
            else Button(row, "タイトルへ", KitColor.Paper2, KitColor.Blue, onTitle);

            // the layout has to be settled before the band is sized to the card
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            bandRt.sizeDelta = new Vector2(0f, card.rect.height + 90f);

            clock = 0f; lineClock = 0f;
            running = Application.isPlaying;
            if (running)
            {
                glitch.enabled = !UIMotionSettings.reduceMotion;
                if (glitch.enabled) glitch.Burst(GlitchTime);
                if (win) SfxNoise.Ui(); else SfxNoise.Boom();   // sfx(win?'ui':'boom')
            }
            Evaluate(running ? 0f : Duration);
        }

        public void Hide()
        {
            running = false;
            if (canvas == null) return;
            for (int i = canvas.transform.childCount - 1; i >= 0; i--) Destroy(canvas.transform.GetChild(i).gameObject);
            canvas.gameObject.SetActive(false);
            card = null; band = null; lineTop = lineBottom = null;
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => Step * 2f + EnterTime;

        /// <summary>Poses the entrance `t` seconds in: the band, then header / body / buttons fading in at 0 / 200 / 400ms; the title slide.</summary>
        public void Evaluate(float t)
        {
            if (card == null) return;
            bool calm = UIMotionSettings.reduceMotion;
            float qb = UIEase.Tactical(t / BandTime);
            band.Set(0f, .16f, qb, Mathf.Lerp(.16f, 0f, qb), qb, .84f, 0f, 1f);
            bandLinesG.alpha = Mathf.Clamp01((t - .1f) / .2f);

            float ph = UIEase.Tactical(t / EnterTime);
            headG.alpha = ph;
            bodyG.alpha = UIEase.Tactical((t - Step) / EnterTime);
            btnG.alpha = UIEase.Tactical((t - Step * 2f) / EnterTime);

            // textSlide: the title comes in from 60 to the left while the spacing closes
            float slide = calm ? 1f : ph;
            float x = Mathf.Lerp(-60f, 0f, slide);
            titleRt.offsetMin = titleRt.offsetMax = new Vector2(x, 0f);
            title.characterSpacing = Mathf.Lerp(50f, 14f, slide);

            // an out-of-Play preview poses the glitch from a fixed seed (in Play the component runs its own burst)
            if (!Application.isPlaying && !calm && t < GlitchTime)
            {
                glitch.previewSeed = 3;
                glitch.Burst(GlitchTime - t);
                glitch.Evaluate(Time.unscaledTime);
            }
        }

        void Update()
        {
            if (running)
            {
                clock += Time.unscaledDeltaTime;
                Evaluate(clock);
                if (clock >= Duration) running = false;
            }
            if (lineTop != null && !UIMotionSettings.reduceMotion)
            {
                lineClock += Time.unscaledDeltaTime;
                var uv = new Rect(-Mathf.Repeat(lineClock, 1f), 0f, bandRt.rect.width / 34f, 1f);
                lineTop.uvRect = uv; lineBottom.uvRect = uv;
            }
        }

        // ================================================================
        //  building blocks
        // ================================================================
        void EnsureCanvas()
        {
            if (externalCanvas != null) { canvas = externalCanvas; return; }
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

        static RawImage Line(RectTransform parent, string name, bool top)
        {
            var r = NewRect(name, parent);
            r.anchorMin = new Vector2(0f, top ? 1f : 0f); r.anchorMax = new Vector2(1f, top ? 1f : 0f); r.pivot = new Vector2(.5f, top ? 1f : 0f);
            r.anchoredPosition = new Vector2(0f, top ? -16f : 16f); r.sizeDelta = new Vector2(0f, 3f);
            var raw = r.gameObject.AddComponent<RawImage>();
            raw.texture = HudWidgets.DashTexture(); raw.raycastTarget = false;
            return raw;
        }

        void Stat(RectTransform parent, string label, string value)
        {
            var box = NewRect("Stat", parent);
            AddImage(box, UITheme.Current.ink2);
            box.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            Vertical(box, 0, new RectOffset(18, 14, 8, 8));
            Text(box, label, 11, Color.Lerp(UITheme.Current.ink2, Color.white, .55f), false, TextAlignmentOptions.MidlineLeft);
            Text(box, value, 26, Color.white, true, TextAlignmentOptions.MidlineLeft);
        }

        void Button(RectTransform parent, string label, KitColor fill, KitColor hoverFill, Action onClick)
        {
            var cell = NewRect("Cell_" + label, parent);
            LE(cell, prefH: 44);
            var b = TacticalButton.Create(cell, "Btn_" + label, "➜", label, "", null, new Vector2(280f, 44f));
            UILayout.Place((RectTransform)b.transform, 0f, 0f, 280f, 44f);
            b.fill = fill; b.hoverFill = hoverFill;
            b.subText.gameObject.SetActive(false);                                   // one line: the name sits in the middle
            UILayout.Place(b.labelText.rectTransform, 46f, 12.5f, 280f - 46f - 16f, 19f);
            b.Go(TacticalButton.Visual.Normal, true);
            b.onClick.AddListener(() => { SfxNoise.Click(); onClick?.Invoke(); });   // sfx('click')
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
