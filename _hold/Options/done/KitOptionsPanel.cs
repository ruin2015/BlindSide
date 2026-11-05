// The settings panel (Phase 5-2) and its title-screen opener. Built at runtime (no prefab) under a full-screen parent.
//   KitOptionsPanel.Build(parent)    Dim (rgba(3,5,8,.5), takes every click) + Pop (420 wide, centred)
//     Pop → DecoShadow, Box (TacticalPanel paper, 26 cut at the top right / bottom left)
//       Head     ink 46: blue band 12, 「設定」 18 body, "SETTINGS" 11 display
//       Rows     モーション軽減 / 画面エフェクト = KitSwitch (parallelogram switch, オン / オフ); エフェクトの強さ = KitChoice (弱 / 中 / 強)
//       Footer   「閉じる」 52 high (blue, slanted) + "ESC"
//   KitOptionsPanel.CreateOpener     the 48×48 "SET" button at the bottom right of the title screen
// Every control writes UIMotionSettings (saved in PlayerPrefs, applied at once). Entrance (.32s, like the confirm dialog): the dim fades in
// over .2s, the box scales .9 → 1 and fades in; ITimedEffect.Evaluate(t), Update only advances an unscaled clock. ESC closes.
// With reduceMotion the pop is a fade only. Nothing is allocated per frame (texts change only when a value changes).
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blindside.UI
{
    /// <summary>Pointer-over brightening (button:hover{filter:brightness(1.12)}) as the colour pulled toward white.</summary>
    public class KitBrighten : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image image;
        Color baseColor; bool over;
        public void OnPointerEnter(PointerEventData e) { if (over || image == null) return; over = true; baseColor = image.color; image.color = Color.Lerp(baseColor, Color.white, .12f); }
        public void OnPointerExit(PointerEventData e) { if (!over || image == null) return; over = false; image.color = baseColor; }
        void OnDisable() { if (over && image != null) { over = false; image.color = baseColor; } }
    }

    /// <summary>A parallelogram on/off switch: the knob slides over .16s (ITimedEffect), the track turns blue, オン / オフ on the free side.</summary>
    public class KitSwitch : MonoBehaviour, ITimedEffect
    {
        public const float W = 84f, H = 30f, KnobW = 32f, Pad = 4f, SlideTime = .16f;
        public Image track;
        public RectTransform knob, labelRt;
        public TMP_Text label;
        public Button button;
        public bool On { get; private set; }
        float from, to, pos, clock; bool running;

        public float Duration => SlideTime;

        /// <summary>Sets the value; instant (or out of Play) jumps to the end pose.</summary>
        public void Set(bool on, bool instant)
        {
            if (on == On && pos == (on ? 1f : 0f) && !running) return;
            On = on;
            from = pos; to = on ? 1f : 0f;
            label.SetText(on ? "オン" : "オフ");
            UILayout.Place(labelRt, on ? 8f : 40f, 0f, 36f, H);
            clock = 0f;
            if (instant || !Application.isPlaying) { running = false; Evaluate(Duration); }
            else { running = true; Evaluate(0f); }
        }

        public void Evaluate(float t)
        {
            float k = UIMotionSettings.reduceMotion ? (t >= SlideTime ? 1f : 0f) : UIEase.Tactical(t / SlideTime);
            pos = Mathf.LerpUnclamped(from, to, k);
            var theme = UITheme.Current;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(Pad, W - KnobW - Pad, pos), 0f);
            track.color = Color.Lerp(theme.paper2, theme.blue, pos);
            label.color = Color.Lerp(theme.ink, Color.white, pos);
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        public static KitSwitch Create(Transform parent, string name, System.Action<bool> onToggle)
        {
            var theme = UITheme.Current;
            var rt = HudKit.NewRect(name, parent);
            rt.sizeDelta = new Vector2(W, H);
            var s = rt.gameObject.AddComponent<KitSwitch>();
            s.track = HudKit.AddImage(rt, theme.paper2, raycast: true);
            rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            s.button = rt.gameObject.AddComponent<Button>();
            s.button.transition = Selectable.Transition.None;
            s.button.targetGraphic = s.track;
            s.button.onClick.AddListener(() => onToggle?.Invoke(!s.On));

            s.labelRt = HudKit.NewRect("Label", rt);
            s.label = HudKit.AddText(s.labelRt, "オフ", 11f, theme.ink, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Body);
            HudKit.Stretch(s.label.rectTransform);

            s.knob = HudKit.NewRect("DecoKnob", rt);
            s.knob.anchorMin = s.knob.anchorMax = new Vector2(0f, .5f); s.knob.pivot = new Vector2(0f, .5f);
            s.knob.sizeDelta = new Vector2(KnobW, H - 8f);
            HudKit.AddImage(s.knob, theme.ink);
            s.knob.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 6f);
            s.Set(false, true);
            return s;
        }
    }

    /// <summary>Three cells in a row (弱 / 中 / 強), one selected: blue with white text; the others paper2.</summary>
    public class KitChoice : MonoBehaviour
    {
        public const float CellW = 46f, H = 30f, Gap = 2f, TotalW = CellW * 3f + Gap * 2f;
        public Image[] cells = new Image[3];
        public TMP_Text[] labels = new TMP_Text[3];
        public int Value { get; private set; } = -1;

        public void Set(int v)
        {
            if (v == Value) return;
            Value = v;
            var theme = UITheme.Current;
            for (int i = 0; i < 3; i++)
            {
                cells[i].color = i == v ? theme.blue : theme.paper2;
                labels[i].color = i == v ? Color.white : theme.ink;
            }
        }

        public static KitChoice Create(Transform parent, string name, string[] texts, System.Action<int> onPick)
        {
            var rt = HudKit.NewRect(name, parent);
            rt.sizeDelta = new Vector2(TotalW, H);
            var c = rt.gameObject.AddComponent<KitChoice>();
            for (int i = 0; i < 3; i++)
            {
                var cell = HudKit.NewRect("Cell" + i, rt);
                UILayout.Place(cell, i * (CellW + Gap), 0f, CellW, H);
                c.cells[i] = HudKit.AddImage(cell, UITheme.Current.paper2, raycast: true);
                cell.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
                var b = cell.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None; b.targetGraphic = c.cells[i];
                int idx = i;
                b.onClick.AddListener(() => onPick?.Invoke(idx));
                c.labels[i] = HudKit.AddText(cell, texts[i], 14f, UITheme.Current.ink, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Body);
                HudKit.Stretch(c.labels[i].rectTransform);
            }
            c.Set(1);
            return c;
        }
    }

    public class KitOptionsPanel : MonoBehaviour, ITimedEffect
    {
        public const float Width = 420f, HeadH = 46f, RowH = 64f, FootH = 52f, PadX = 28f, Cut = 26f, BandW = 12f, YesSlant = 18f;
        public const float DimTime = .2f, PopTime = .32f;
        static readonly Color DimColor = new Color32(3, 5, 8, 128);                  // rgba(3,5,8,.5)

        RectTransform pop;
        CanvasGroup popGroup;
        Image dim;
        KitSwitch reduceSwitch, fxSwitch;
        KitChoice levelChoice;
        float clock; bool running;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Builds the (closed) panel under `parent`, which should be a full-screen rect.</summary>
        public static KitOptionsPanel Build(Transform parent)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("OptionsPanel", parent);
            HudKit.Stretch(root);
            var p = root.gameObject.AddComponent<KitOptionsPanel>();

            var dimRt = HudKit.NewRect("DecoDim", root);
            HudKit.Stretch(dimRt);
            p.dim = HudKit.AddImage(dimRt, DimColor, raycast: true);          // blocks every click behind the panel

            float h = HeadH + RowH * 3f + FootH;
            p.pop = HudKit.NewRect("Pop", root);
            p.pop.anchorMin = p.pop.anchorMax = p.pop.pivot = new Vector2(.5f, .5f);
            p.pop.sizeDelta = new Vector2(Width, h);
            p.popGroup = p.pop.gameObject.AddComponent<CanvasGroup>();

            var box = TacticalPanel.Create(p.pop, "Box", KitColor.Paper, SlantedGraphic.Size.Custom, 0f);
            HudKit.Stretch(box.rectTransform);
            box.raycastTarget = true;
            box.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.Mockup);
            var shadow = KitShadow.Behind(box.rectTransform, new Color(.03f, .04f, .06f), .6f, 14f, 30f).rectTransform;
            HudKit.Stretch(shadow);
            var boxRt = box.rectTransform;

            // head
            var head = HudKit.NewRect("Head", boxRt);
            UILayout.Place(head, 0f, 0f, Width, HeadH);
            HudKit.AddImage(head, theme.ink);
            head.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.TopRight);
            var band = HudKit.NewRect("DecoBand", head);
            band.anchorMin = new Vector2(0f, 0f); band.anchorMax = new Vector2(0f, 1f); band.pivot = new Vector2(0f, .5f);
            band.anchoredPosition = Vector2.zero; band.sizeDelta = new Vector2(BandW, 0f);
            HudKit.AddImage(band, theme.blue);
            var title = HudKit.AddText(head, "設定", 18f, Color.white, false, TextAlignmentOptions.MidlineLeft, "Title", UITheme.TextRole.Body);
            UILayout.Place(title.rectTransform, BandW + PadX, 0f, 160f, HeadH);
            var en = HudKit.AddText(head, "SETTINGS", 11f, Color.Lerp(theme.ink, Color.white, .65f), true, TextAlignmentOptions.MidlineRight, "En", UITheme.TextRole.Display);
            en.characterSpacing = 10f;
            UILayout.Place(en.rectTransform, Width - 52f - 140f, 0f, 140f, HeadH);

            // rows: label 14 + a 2-line note 11 on the left, the control on the right
            float ctlY = (RowH - KitSwitch.H) * .5f;
            Row(boxRt, 0, "モーション軽減", "グリッチ・揺れ・流れる縞を止め、フェードだけにします", true);
            p.reduceSwitch = KitSwitch.Create(boxRt, "ReduceSwitch", on => UIMotionSettings.SetReduceMotion(on));
            UILayout.Place(p.reduceSwitch.GetComponent<RectTransform>(), Width - PadX - KitSwitch.W, HeadH + ctlY, KitSwitch.W, KitSwitch.H);

            Row(boxRt, 1, "画面エフェクト", "走査線やノイズなど、画面全体の演出", true);
            p.fxSwitch = KitSwitch.Create(boxRt, "FxSwitch", on => UIMotionSettings.SetScreenFx(on));
            UILayout.Place(p.fxSwitch.GetComponent<RectTransform>(), Width - PadX - KitSwitch.W, HeadH + RowH + ctlY, KitSwitch.W, KitSwitch.H);

            Row(boxRt, 2, "エフェクトの強さ", "画面エフェクトとグリッチの強さ", false);
            p.levelChoice = KitChoice.Create(boxRt, "LevelChoice", new[] { "弱", "中", "強" }, i => UIMotionSettings.SetEffectLevel(i));
            UILayout.Place(p.levelChoice.GetComponent<RectTransform>(), Width - PadX - KitChoice.TotalW, HeadH + RowH * 2f + ctlY, KitChoice.TotalW, KitChoice.H);

            // footer: 「閉じる」 (blue, slanted at the right, cut at the bottom left) + ESC
            var foot = HudKit.NewRect("Close", boxRt);
            UILayout.Place(foot, 0f, HeadH + RowH * 3f, Width, FootH);
            var footImg = HudKit.AddImage(foot, theme.blue, raycast: true);
            foot.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.RightEdge, YesSlant);
            foot.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.BottomLeft);
            var btn = foot.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; btn.targetGraphic = footImg;
            btn.onClick.AddListener(p.Close);
            foot.gameObject.AddComponent<KitBrighten>().image = footImg;
            var closeText = HudKit.AddText(foot, "閉じる", 16f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            UILayout.Place(closeText.rectTransform, 40f, 0f, Width - 40f - 110f, FootH);
            var esc = HudKit.AddText(foot, "ESC", 11f, Color.Lerp(theme.blue, Color.white, .8f), true, TextAlignmentOptions.MidlineRight, "Esc", UITheme.TextRole.Display);
            esc.characterSpacing = 10f;
            UILayout.Place(esc.rectTransform, Width - 24f - YesSlant - 60f, 0f, 60f, FootH);

            root.gameObject.SetActive(false);
            return p;
        }

        static void Row(RectTransform box, int index, string label, string note, bool rule)
        {
            var theme = UITheme.Current;
            float top = HeadH + index * RowH;
            float textW = Width - PadX * 2f - KitChoice.TotalW - 8f;
            var l = HudKit.AddText(box, label, 14f, theme.ink, true, TextAlignmentOptions.TopLeft, "Label", UITheme.TextRole.Body);
            UILayout.LineHeight(l, 1.25f);
            UILayout.Place(l.rectTransform, PadX, top + 9f, textW, 18f);
            var n = HudKit.AddText(box, note, 11f, theme.dim, false, TextAlignmentOptions.TopLeft, "Note", UITheme.TextRole.Body);
            n.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(n, 1.25f);
            UILayout.Place(n.rectTransform, PadX, top + 29f, textW, 28f);
            if (!rule) return;
            var line = HudKit.NewRect("DecoRule", box);
            UILayout.Place(line, PadX, top + RowH - 1f, Width - PadX * 2f, 1f);
            HudKit.AddImage(line, Color.Lerp(theme.paper, theme.ink, .12f));
        }

        // ---- open / close ------------------------------------------------------------------------------------------------------
        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh(true);
            clock = 0f;
            running = Application.isPlaying;
            Evaluate(running ? 0f : Duration);
        }

        public void Close() { running = false; gameObject.SetActive(false); }

        void Refresh(bool instant)
        {
            reduceSwitch.Set(UIMotionSettings.reduceMotion, instant);
            fxSwitch.Set(UIMotionSettings.screenFx, instant);
            levelChoice.Set(UIMotionSettings.effectLevel);
        }

        void OnEnable() { UIMotionSettings.Changed += OnChanged; }
        void OnDisable() { UIMotionSettings.Changed -= OnChanged; }
        void OnChanged() { Refresh(false); }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => Mathf.Max(DimTime, PopTime);

        /// <summary>Poses the entrance `t` seconds in: the dim fades in over .2s, the box scales .9 → 1 and fades in over .32s.</summary>
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
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        // ---- the opener ------------------------------------------------------------------------------------------------------
        /// <summary>The 48×48 "SET" button at the bottom right of `parent` (x 20 from the edge): opens `panel`.</summary>
        public static Button CreateOpener(Transform parent, KitOptionsPanel panel)
        {
            var theme = UITheme.Current;
            var rt = HudKit.NewRect("SettingsButton", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 4f); rt.sizeDelta = new Vector2(48f, 48f);
            var img = HudKit.AddImage(rt, theme.ink, raycast: true);
            rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; b.targetGraphic = img;
            b.onClick.AddListener(panel.Open);
            rt.gameObject.AddComponent<KitBrighten>().image = img;
            rt.gameObject.AddComponent<LintIgnoreOverlap>().reason = "sits in the title's free bottom-right corner, over the roster's last row margin";
            var tick = HudKit.NewRect("DecoTick", rt);
            UILayout.Place(tick, 12f, 40f, 24f, 3f);
            HudKit.AddImage(tick, theme.blue);
            var t = HudKit.AddText(rt, "SET", 14f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            t.characterSpacing = 6f;
            UILayout.Place(t.rectTransform, 4f, 6f, 40f, 30f);
            return b;
        }
    }
}
