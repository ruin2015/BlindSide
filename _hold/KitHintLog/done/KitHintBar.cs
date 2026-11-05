// The HUD's hint bar (#hint, ui_spec "안내 바 / 로그"): 400×48 ink box at the bottom centre (left = 50% − 135, bottom 20), a 30 wide
// warning-stripe tab on the left (HazardStripe 6/12, not scrolling), the title (13.5, Body) over the sub text (12, dimmed) in the
// column between the tab and the chips, and the chips ("ESC キャンセル" …: paper, 11, ink) on the right (26 from the edge).
// Long texts: the letter spacing is narrowed first (down to −0.04em); if the text is still wider than the column it wraps to two
// (or more) lines at the same size and the bar grows upwards (Height / HeightChanged, which KitLogView follows).
// Entrance (hintIn): 22 up from below and fade in over .35s — ITimedEffect.Evaluate(t) poses it, Update only advances an unscaled
// clock and stops at the end. Texts and chips change only in Set (a mode change), never per frame; chip objects are pooled.
// The controller's calls are unchanged (Build, Set, Hide, Root, Visible, CoroutineHost) — it only holds this type instead of HintView.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public struct HintButton
    {
        public string id, label;
        public bool on;                    // .hb.on (accent background)
        public HintButton(string id, string label, bool on) { this.id = id; this.label = label; this.on = on; }
    }

    [AddComponentMenu("Blindside/UI/Kit Hint Bar")]
    public class KitHintBar : MonoBehaviour, ITimedEffect
    {
        public const float Width = 400f, MinHeight = 48f, Bottom = 20f, FromCentre = 135f, StripeW = 30f;
        public const float TextX = 44f, RightPad = 26f, ButtonGap = 10f, ButtonH = 19f, ButtonPadX = 16f, NoButtonPad = 16f;
        public const float MainSize = 13.5f, SubSize = 12f, MainLine = 17f, SubLine = 15f, PadY = 8f, MinSpacing = -4f;
        public const float EnterTime = .35f, EnterRise = 22f;
        public static readonly Vector2 Pos = new Vector2(-FromCentre, Bottom);   // #hint{left:calc(50% - 135px);bottom:20px}

        /// <summary>The bar's current height (48, more when a text wraps); MinHeight while hidden.</summary>
        public static float Height { get; private set; } = MinHeight;
        public static event System.Action<float> HeightChanged;

        class Btn { public RectTransform rt; public Image box; public TMP_Text label; public string id; }

        /// <summary>Kept for the old call site (the hint used a coroutine); the bar now runs its own Update.</summary>
        public MonoBehaviour CoroutineHost;
        public RectTransform Root => (RectTransform)transform;
        public bool Visible => gameObject.activeSelf;

        RectTransform column, buttonRow;
        TMP_Text hintMain, hintSub;
        CanvasGroup group;
        readonly List<Btn> buttons = new List<Btn>();
        System.Action<string> onAction;
        float clock;
        bool running;

        /// <summary>Builds the (hidden) hint bar under `parent`.</summary>
        public static KitHintBar Build(Transform parent)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("Hint", parent);
            root.anchorMin = root.anchorMax = new Vector2(.5f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = Pos;
            root.sizeDelta = new Vector2(Width, MinHeight);
            var v = root.gameObject.AddComponent<KitHintBar>();
            v.group = root.gameObject.AddComponent<CanvasGroup>();

            // the box takes clicks (it is part of the HUD, not the battlefield)
            var bg = HudKit.NewRect("DecoBg", root);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, theme.ink, raycast: true);

            // #hint::before — 30 wide, repeating-linear-gradient(135deg, acc 0 6px, ink 6px 12px)
            var tab = HudKit.NewRect("DecoStripeTab", root);
            tab.anchorMin = new Vector2(0f, 0f); tab.anchorMax = new Vector2(0f, 1f); tab.pivot = new Vector2(0f, .5f);
            tab.anchoredPosition = Vector2.zero; tab.sizeDelta = new Vector2(StripeW, 0f);
            var stripe = HazardStripe.Create(tab, "DecoStripe", theme.acc, theme.ink, 6f, 12f);
            stripe.scroll = false;

            // #hint b{13.5px} small{12px, rgba(255,255,255,.7)} — the .7 white over ink is given as the opaque sRGB colour it makes
            v.column = HudKit.NewRect("Text", root);
            v.hintMain = HudKit.AddText(v.column, "", MainSize, Color.white, false, TextAlignmentOptions.TopLeft, "Main", UITheme.TextRole.Body);
            v.hintSub = HudKit.AddText(v.column, "", SubSize, Color.Lerp(theme.ink, Color.white, .7f), false, TextAlignmentOptions.TopLeft, "Sub", UITheme.TextRole.Body);
            UILayout.LineHeight(v.hintMain, 1.25f);
            UILayout.LineHeight(v.hintSub, 1.25f);

            // #hint .esc{position:absolute;right:26px;11px;padding:2px 8px}
            v.buttonRow = HudKit.NewRect("Buttons", root);
            v.buttonRow.anchorMin = v.buttonRow.anchorMax = new Vector2(1f, .5f);
            v.buttonRow.pivot = new Vector2(1f, .5f);
            v.buttonRow.anchoredPosition = new Vector2(-RightPad, 0f);

            root.gameObject.SetActive(false);
            return v;
        }

        Btn GetButton(int i)
        {
            while (buttons.Count <= i)
            {
                var rt = HudKit.NewRect("Hb", buttonRow);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, .5f);
                rt.pivot = new Vector2(0f, .5f);
                var box = HudKit.AddImage(rt, KitPalette.Get(KitColor.Paper), raycast: true);
                var label = HudKit.AddText(rt, "", 11f, KitPalette.Get(KitColor.Ink), true, TextAlignmentOptions.Center, "Label");
                HudKit.Stretch(label.rectTransform);
                var btn = rt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = box;
                int idx = buttons.Count;
                btn.onClick.AddListener(() => Fire(idx));
                buttons.Add(new Btn { rt = rt, box = box, label = label });
            }
            return buttons[i];
        }

        void Fire(int i)
        {
            if (i < buttons.Count && onAction != null) onAction(buttons[i].id);
        }

        /// <summary>Port of setHint(main,sub,btns) — `on` is the chip's `.hb.on` (accent background); a click calls `onAction(id)`.</summary>
        public void Set(string main, string sub, IList<HintButton> btns, System.Action<string> action)
        {
            onAction = action;
            gameObject.SetActive(true);     // TMP measures only on an active hierarchy

            int n = btns != null ? btns.Count : 0;
            float btnW = 0f;
            for (int i = 0; i < n; i++)
            {
                var b = GetButton(i);
                var def = btns[i];
                b.id = def.id;
                b.box.color = KitPalette.Get(def.on ? KitColor.Acc : KitColor.Paper);
                b.label.text = def.label;
                UIFonts.Auto(b.label, true);
                float bw = Mathf.Ceil(b.label.GetPreferredValues(def.label).x) + ButtonPadX;
                float x = i > 0 ? btnW + ButtonGap : 0f;
                b.rt.anchoredPosition = new Vector2(x, 0f);
                b.rt.sizeDelta = new Vector2(bw, ButtonH);
                b.rt.gameObject.SetActive(true);
                btnW = x + bw;
            }
            for (int i = n; i < buttons.Count; i++) buttons[i].rt.gameObject.SetActive(false);
            buttonRow.gameObject.SetActive(n > 0);
            buttonRow.sizeDelta = new Vector2(btnW, ButtonH);

            // the column takes what the tab and the chips leave; the text is fitted to it (spacing first, then wrap) and the bar grows to it
            float colW = Width - TextX - (n > 0 ? RightPad + btnW + ButtonGap : NoButtonPad);
            int mainLines = KitTextFit.Fit(hintMain, main, colW, MainLine, MinSpacing);
            int subLines = KitTextFit.Fit(hintSub, sub, colW, SubLine, MinSpacing);
            hintSub.gameObject.SetActive(subLines > 0);
            float mainBlock = mainLines * MainLine, subBlock = subLines * SubLine;
            float h = Mathf.Max(MinHeight, Mathf.Ceil(mainBlock + subBlock + PadY * 2f));
            float top = (h - mainBlock - subBlock) * .5f;

            Root.sizeDelta = new Vector2(Width, h);
            UILayout.Place(column, TextX, 0f, colW, h);
            UILayout.Place(hintMain.rectTransform, 0f, top, colW, mainBlock);
            UILayout.Place(hintSub.rectTransform, 0f, top + mainBlock, colW, subBlock);
            SetHeight(h);

            // @keyframes hintIn — 22 up from below, fade in .35s
            clock = 0f;
            running = Application.isPlaying;
            Evaluate(running ? 0f : Duration);
        }

        public void Hide()
        {
            running = false;
            gameObject.SetActive(false);
            SetHeight(MinHeight);
        }

        static void SetHeight(float h)
        {
            if (Mathf.Approximately(h, Height)) return;
            Height = h;
            HeightChanged?.Invoke(h);
        }

        void OnDestroy() { if (Height != MinHeight) SetHeight(MinHeight); }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => EnterTime;

        /// <summary>Poses the entrance `t` seconds in: rises 22 and fades in over .35s.</summary>
        public void Evaluate(float t)
        {
            float p = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(t / EnterTime);
            if (group.alpha != p) group.alpha = p;
            var pos = Pos - new Vector2(0f, EnterRise * (1f - p));
            if (Root.anchoredPosition != pos) Root.anchoredPosition = pos;
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Height = MinHeight; HeightChanged = null; }
    }
}
