// The confirmation dialog (#md, ui_spec "확인 창"), re-skinned in Phase 4-4 with the same API (Build, Open, Close, Root, Box, Visible,
// CoroutineHost). 420 wide, centred, with the screen dimmed (rgba(3,5,8,.5), takes every click: the modal input block is unchanged):
//   Root      full screen, DecoDim
//     Pop     the box's pose root (scale + CanvasGroup), sized to the box
//       DecoShadow   drop shadow (0 14 30, .6)
//       Box          paper (26 cut at the top right / bottom left, CutCornerGraphic)
//         Head       ink, 46 high, 12 wide band in the colour, title 18 body (not Black), "CONFIRM MOVE" 11 display on the right
//         Body       14 body / line 1.6 (`<b>…</b>` = the figure: 26, blue)
//         Buttons    52 high: Yes (blue / `yesBg`, slanted 18 on the right) over No (paper2, 12 overlap)
// Entrance (modal fade .2s + pop .32s): the dim fades in; the box scales .9 → 1 and fades in. The mockup's skewX(−6°) → 0 cannot be
// done on uGUI layouts (no skew transform), so the pop is scale + fade only. ITimedEffect.Evaluate(t); Update advances an unscaled clock.
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class DialogView : MonoBehaviour, ITimedEffect
    {
        public const float HeadH = 46f, BtnH = 52f, PadX = 28f, PadY = 14f, Cut = 26f, BandW = 12f, Overlap = 12f, YesSlant = 18f;
        public const float DimTime = .2f, PopTime = .32f;
        static readonly Color DimColor = new Color32(3, 5, 8, 128);                   // rgba(3,5,8,.5)
        static readonly Color BodyInk = new Color32(0x2B, 0x30, 0x36, 0xFF);

        public RectTransform Root => (RectTransform)transform;
        public RectTransform Box => box;
        public bool Visible => gameObject.activeSelf;
        /// <summary>Kept for the old call site (the reveal was a coroutine); the dialog now runs its own Update.</summary>
        public MonoBehaviour CoroutineHost;

        RectTransform pop, box, head, bodyBox, buttons, shadow;
        CanvasGroup popGroup;
        Image dim, yesImg;
        TMP_Text title, en, body, yesText, noText;
        RectTransform yes, no;
        float clock; bool running;

        /// <summary>Builds the (hidden) full-screen dialog under `parent`; the buttons call `onYes` / `onNo`.</summary>
        public static DialogView Build(Transform parent, System.Action onYes, System.Action onNo)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("Dialog", parent);
            HudKit.Stretch(root);
            var v = root.gameObject.AddComponent<DialogView>();

            var dimRt = HudKit.NewRect("DecoDim", root);
            HudKit.Stretch(dimRt);
            v.dim = HudKit.AddImage(dimRt, DimColor, raycast: true);        // blocks everything behind the dialog

            v.pop = HudKit.NewRect("Pop", root);
            v.pop.anchorMin = v.pop.anchorMax = v.pop.pivot = new Vector2(.5f, .5f);
            v.popGroup = v.pop.gameObject.AddComponent<CanvasGroup>();

            v.box = HudKit.NewRect("Box", v.pop);
            HudKit.Stretch(v.box);
            HudKit.AddImage(v.box, theme.paper, raycast: true);
            v.box.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.Mockup);
            v.shadow = KitShadow.Behind(v.box, new Color(.03f, .04f, .06f), .6f, 14f, 30f).rectTransform;
            HudKit.Stretch(v.shadow);

            // head: ink, left band, title, CONFIRM MOVE
            v.head = HudKit.NewRect("Head", v.box);
            HudKit.AddImage(v.head, theme.ink);
            v.head.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.TopRight);
            var band = HudKit.NewRect("DecoBand", v.head);
            band.anchorMin = new Vector2(0f, 0f); band.anchorMax = new Vector2(0f, 1f); band.pivot = new Vector2(0f, .5f);
            band.anchoredPosition = Vector2.zero; band.sizeDelta = new Vector2(BandW, 0f);
            HudKit.AddImage(band, theme.blue);
            v.title = HudKit.AddText(v.head, "", 18f, Color.white, false, TextAlignmentOptions.MidlineLeft, "Title", UITheme.TextRole.Body);
            v.en = HudKit.AddText(v.head, "", 11f, Color.Lerp(theme.ink, Color.white, .65f), true, TextAlignmentOptions.MidlineRight, "En", UITheme.TextRole.Display);
            v.en.characterSpacing = 10f;

            v.bodyBox = HudKit.NewRect("Body", v.box);
            v.body = HudKit.AddText(v.bodyBox, "", 14f, BodyInk, false, TextAlignmentOptions.TopLeft, "Text", UITheme.TextRole.Body);
            v.body.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(v.body, 1.6f);

            v.buttons = HudKit.NewRect("Buttons", v.box);
            v.no = BuildButton(v.buttons, "No", theme.paper2, theme.ink, false, () => onNo?.Invoke(), out v.noText, out _);
            v.yes = BuildButton(v.buttons, "Yes", theme.blue, Color.white, true, () => onYes?.Invoke(), out v.yesText, out v.yesImg);

            root.SetAsLastSibling();
            root.gameObject.SetActive(false);
            return v;
        }

        static RectTransform BuildButton(RectTransform row, string name, Color bg, Color fg, bool yes, UnityEngine.Events.UnityAction onClick,
            out TMP_Text label, out Image image)
        {
            var b = HudKit.NewRect(name, row);
            image = HudKit.AddImage(b, bg, raycast: true);
            if (yes)
            {
                b.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.RightEdge, YesSlant);
                b.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.BottomLeft);
            }
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = image;
            btn.onClick.AddListener(onClick);
            b.gameObject.AddComponent<Brighten>().image = image;
            label = HudKit.AddText(b, "", 16f, fg, true, TextAlignmentOptions.Center, "Label", UITheme.TextRole.Title);
            HudKit.Stretch(label.rectTransform);
            return b;
        }

        /// <summary>button:hover{filter:brightness(1.12)} — the colour pulled toward white while the pointer is over it.</summary>
        class Brighten : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Image image;
            Color baseColor; bool over;
            public void OnPointerEnter(PointerEventData e) { if (over) return; over = true; baseColor = image.color; image.color = Color.Lerp(baseColor, Color.white, .12f); }
            public void OnPointerExit(PointerEventData e) { if (!over) return; over = false; image.color = baseColor; }
            void OnDisable() { if (over) { over = false; image.color = baseColor; } }
        }

        void Layout()
        {
            float w = UITheme.Current.hud.dialogWidth;
            float textW = w - PadX * 2f;
            float bodyH = Mathf.Ceil(body.GetPreferredValues(body.text, textW, 0f).y) + PadY * 2f;
            float h = HeadH + bodyH + BtnH;
            pop.sizeDelta = new Vector2(w, h);
            UILayout.Place(head, 0f, 0f, w, HeadH);
            float enW = Mathf.Ceil(en.GetPreferredValues(en.text).x) + 1f;
            UILayout.Place(title.rectTransform, BandW + PadX, 0f, w - BandW - PadX - 52f - enW - 8f, HeadH);
            UILayout.Place(en.rectTransform, w - 52f - enW, 0f, enW, HeadH);
            UILayout.Place(bodyBox, 0f, HeadH, w, bodyH);
            UILayout.Place(body.rectTransform, PadX, PadY, textW, bodyH - PadY * 2f);
            UILayout.Place(buttons, 0f, HeadH + bodyH, w, BtnH);
            float bw = (w + Overlap) * .5f;
            UILayout.Place(yes, 0f, 0f, bw, BtnH);
            UILayout.Place(no, bw - Overlap, 0f, bw, BtnH);
        }

        /// <summary>modal()'s drawing: fills and shows the dialog. `yesBg` is the first button's background (default --blu;
        /// `.mbtns button.red` passes --foe); `<b>…</b>` in the body becomes the 26px blue figure.</summary>
        public void Open(string titleText, string enText, string bodyText, string yesLabel, string noLabel, Color? yesBg = null)
        {
            var theme = UITheme.Current;
            yesImg.color = yesBg ?? theme.blue;
            title.text = titleText; en.text = enText;
            body.text = bodyText.Replace("<b>", $"<b><size=26><color=#{ColorUtility.ToHtmlStringRGB(theme.blue)}>").Replace("</b>", "</color></size></b>");
            yesText.text = yesLabel; noText.text = noLabel;
            Root.gameObject.SetActive(true);        // TMP measures only on an active hierarchy
            Root.SetAsLastSibling();
            Layout();
            clock = 0f;
            running = Application.isPlaying;
            Evaluate(running ? 0f : Duration);
        }

        public void Close() { running = false; Root.gameObject.SetActive(false); }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => Mathf.Max(DimTime, PopTime);

        /// <summary>Poses the entrance `t` seconds in: the dim fades in over .2s, the box pops (scale .9 → 1) and fades in over .32s.</summary>
        public void Evaluate(float t)
        {
            float d = Mathf.Clamp01(t / DimTime);
            var c = DimColor; c.a *= d;
            if (dim.color != c) dim.color = c;
            float p = UIEase.Tactical(t / PopTime);
            float s = UIMotionSettings.reduceMotion ? 1f : Mathf.Lerp(.9f, 1f, p);
            if (pop.localScale.x != s) pop.localScale = new Vector3(s, s, 1f);
            if (popGroup.alpha != p) popGroup.alpha = p;
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }
    }
}
