// The settings panel and its title-screen opener (the 420-wide three-row panel of Phase 5-2 is kept in UI/Legacy/KitOptionsPanel.cs.txt).
// Built at runtime (no prefab) under a full-screen parent: the title's select screen (SET) and the game menu (PauseMenu → 設定).
//   KitOptionsPanel.Build(parent)    Dim (rgba(3,5,8,.5), takes every click) + Pop (600 × 454, centred)
//     Pop → DecoShadow, Box (TacticalPanel paper, 26 cut at the top right / bottom left)
//       Head     ink 46: blue band 12, 「設定」 18 body, "SETTINGS" 11 display
//       Tabs     four parallelogram tabs 32 high (12 below the head, 28 in): サウンド / 操作 / 画面 / 演出 — the open one ink with a
//                3px blue line under it, the others paper2
//       Page     up to five rows 60 high: label 14 + note 11 on the left, the control on the right (28 in)
//                 サウンド   マスター音量 · 効果音                                       KitSlider 0–100%
//                 操作       カメラ移動速度 · ズーム感度                                  KitSlider 50–200%
//                 画面       画面モード (フルスクリーン / ウィンドウ) · 解像度 (KitStepper) · 垂直同期 (KitSwitch) ·
//                            フレームレート上限 (30 / 60 / 120 / 無制限) · 画質 (標準 / 高)
//                 演出       モーション軽減 · 画面エフェクト (KitSwitch) · エフェクトの強さ (弱 / 中 / 強) ·
//                            戦闘演出 (フル / 短め / オフ) · 画面の揺れ (KitSwitch)
//       Footer   52 high: 「初期設定に戻す」 (paper2, the open tab back to its defaults) + 「閉じる」 (blue, slanted) + "ESC"
//   KitOptionsPanel.CreateOpener     the 48×48 "SET" button at the bottom right of the title screen
// The controls write GameSettings (sound / controls / display / shake), UIMotionSettings (motion options) and BattleDirector.Mode
// (戦闘演出); every value is saved and applied at once. Entrance (.32s, like the confirm dialog): the dim fades in over .2s, the box
// scales .9 → 1 and fades in; ITimedEffect.Evaluate(t), Update only advances an unscaled clock. ESC closes (and is used: EscapeGate).
// With reduceMotion the pop is a fade only. Nothing is allocated per frame (texts change only when a value changes).
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Cinematic;
using Blindside.Settings;

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

    /// <summary>Cells in a row (弱 / 中 / 強 …), one selected: blue with white text; the others paper2.</summary>
    public class KitChoice : MonoBehaviour
    {
        public const float CellW = 46f, H = 30f, Gap = 2f, TotalW = CellW * 3f + Gap * 2f;
        public Image[] cells;
        public TMP_Text[] labels;
        public int Value { get; private set; } = -1;

        /// <summary>The row's width for `n` cells of `cellW`.</summary>
        public static float WidthOf(int n, float cellW = CellW) => cellW * n + Gap * (n - 1);

        public void Set(int v)
        {
            if (v == Value) return;
            Value = v;
            var theme = UITheme.Current;
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].color = i == v ? theme.blue : theme.paper2;
                labels[i].color = i == v ? Color.white : theme.ink;
            }
        }

        public static KitChoice Create(Transform parent, string name, string[] texts, System.Action<int> onPick, float cellW = CellW)
        {
            int n = texts.Length;
            var rt = HudKit.NewRect(name, parent);
            rt.sizeDelta = new Vector2(WidthOf(n, cellW), H);
            var c = rt.gameObject.AddComponent<KitChoice>();
            c.cells = new Image[n];
            c.labels = new TMP_Text[n];
            for (int i = 0; i < n; i++)
            {
                var cell = HudKit.NewRect("Cell" + i, rt);
                UILayout.Place(cell, i * (cellW + Gap), 0f, cellW, H);
                c.cells[i] = HudKit.AddImage(cell, UITheme.Current.paper2, raycast: true);
                cell.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
                var b = cell.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None; b.targetGraphic = c.cells[i];
                int idx = i;
                b.onClick.AddListener(() => onPick?.Invoke(idx));
                c.labels[i] = HudKit.AddText(cell, texts[i], 14f, UITheme.Current.ink, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Body);
                HudKit.Stretch(c.labels[i].rectTransform);
            }
            c.Set(Mathf.Min(1, n - 1));
            return c;
        }
    }

    /// <summary>A parallelogram slider: paper2 track (slant 8), blue fill from the left, an ink tick at its end and the value on the right
    /// (display 14, "80%"). Click or drag on the track; the value snaps to `step`.</summary>
    public class KitSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public const float TrackW = 168f, ValueW = 54f, Gap = 8f, H = 30f, W = TrackW + Gap + ValueW, TickW = 4f;
        public RectTransform track, fill, tick;
        public TMP_Text valueText;
        public float min, max, step;
        public float Value { get; private set; } = float.NaN;
        System.Action<float> onChange;

        /// <summary>Shows `v` (snapped, clamped) without calling back.</summary>
        public void Set(float v)
        {
            v = Snap(v);
            if (v == Value) return;
            Value = v;
            float k = max > min ? (v - min) / (max - min) : 0f;
            float w = Mathf.Round(TrackW * k);
            fill.sizeDelta = new Vector2(w, fill.sizeDelta.y);
            tick.anchoredPosition = new Vector2(Mathf.Clamp(w - TickW * .5f, 0f, TrackW - TickW), 0f);
            valueText.SetText("{0}%", Mathf.RoundToInt(v * 100f));
        }

        float Snap(float v)
        {
            v = Mathf.Clamp(v, min, max);
            if (step > 0f) v = Mathf.Clamp(min + Mathf.Round((v - min) / step) * step, min, max);
            return Mathf.Round(v * 1000f) / 1000f;
        }

        public void OnPointerDown(PointerEventData e) => Pick(e);
        public void OnDrag(PointerEventData e) => Pick(e);

        void Pick(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var local)) return;
            var r = track.rect;
            float k = Mathf.Clamp01((local.x - r.xMin) / r.width);
            float v = Snap(Mathf.Lerp(min, max, k));
            if (v == Value) return;
            Set(v);
            onChange?.Invoke(v);
        }

        public static KitSlider Create(Transform parent, string name, float min, float max, float step, System.Action<float> onChange)
        {
            var theme = UITheme.Current;
            var rt = HudKit.NewRect(name, parent);
            rt.sizeDelta = new Vector2(W, H);
            var s = rt.gameObject.AddComponent<KitSlider>();
            s.min = min; s.max = max; s.step = step; s.onChange = onChange;

            s.track = HudKit.NewRect("Track", rt);
            UILayout.Place(s.track, 0f, 0f, TrackW, H);
            HudKit.AddImage(s.track, theme.paper2, raycast: true);
            s.track.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            s.track.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            s.fill = HudKit.NewRect("Fill", s.track);
            s.fill.anchorMin = new Vector2(0f, 0f); s.fill.anchorMax = new Vector2(0f, 1f); s.fill.pivot = new Vector2(0f, .5f);
            s.fill.anchoredPosition = Vector2.zero; s.fill.sizeDelta = new Vector2(0f, 0f);
            HudKit.AddImage(s.fill, theme.blue);

            s.tick = HudKit.NewRect("DecoTick", s.track);
            s.tick.anchorMin = new Vector2(0f, 0f); s.tick.anchorMax = new Vector2(0f, 1f); s.tick.pivot = new Vector2(0f, .5f);
            s.tick.sizeDelta = new Vector2(TickW, 0f);
            HudKit.AddImage(s.tick, theme.ink);

            s.valueText = HudKit.AddText(rt, "", 14f, theme.ink, true, TextAlignmentOptions.MidlineRight, "Value", UITheme.TextRole.Display);
            s.valueText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(s.valueText.rectTransform, TrackW + Gap, 0f, ValueW, H);
            s.Set(min);
            return s;
        }
    }

    /// <summary>A value with a step button on each side (paper2 parallelograms with "&lt;" / "&gt;"), the value between them (14 body).</summary>
    public class KitStepper : MonoBehaviour
    {
        public const float W = 230f, H = 30f, ButtonW = 34f;
        public TMP_Text valueText;
        string shown;

        public void SetText(string s)
        {
            if (s == shown) return;
            shown = s;
            valueText.SetText(s);
        }

        public static KitStepper Create(Transform parent, string name, System.Action<int> onStep)
        {
            var theme = UITheme.Current;
            var rt = HudKit.NewRect(name, parent);
            rt.sizeDelta = new Vector2(W, H);
            var s = rt.gameObject.AddComponent<KitStepper>();
            StepButton(rt, "Prev", "<", 0f, () => onStep?.Invoke(-1));
            StepButton(rt, "Next", ">", W - ButtonW, () => onStep?.Invoke(1));
            var mid = HudKit.NewRect("Value", rt);
            UILayout.Place(mid, ButtonW + 2f, 0f, W - ButtonW * 2f - 4f, H);
            HudKit.AddImage(mid, Color.Lerp(theme.paper2, theme.paper, .5f));
            mid.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            s.valueText = HudKit.AddText(mid, "", 14f, theme.ink, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Body);
            s.valueText.textWrappingMode = TextWrappingModes.NoWrap;
            HudKit.Stretch(s.valueText.rectTransform);
            return s;
        }

        static void StepButton(RectTransform parent, string name, string glyph, float x, UnityEngine.Events.UnityAction onClick)
        {
            var theme = UITheme.Current;
            var b = HudKit.NewRect(name, parent);
            UILayout.Place(b, x, 0f, ButtonW, H);
            var img = HudKit.AddImage(b, theme.ink, raycast: true);
            b.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            b.gameObject.AddComponent<KitBrighten>().image = img;
            var t = HudKit.AddText(b, glyph, 16f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            HudKit.Stretch(t.rectTransform);
        }
    }

    public class KitOptionsPanel : MonoBehaviour, ITimedEffect
    {
        public const float Width = 600f, HeadH = 46f, TabTop = 12f, TabH = 32f, TabsH = TabTop + TabH + 12f, RowH = 60f, Rows = 5f, FootH = 52f;
        public const float PadX = 28f, Cut = 26f, BandW = 12f, YesSlant = 18f, ResetW = 230f;
        public const float Height = HeadH + TabsH + RowH * Rows + FootH;
        public const float DimTime = .2f, PopTime = .32f;
        static readonly Color DimColor = new Color32(3, 5, 8, 128);                  // rgba(3,5,8,.5)
        public static readonly string[] TabNames = { "サウンド", "操作", "画面", "演出" };
        public const int TabSound = 0, TabControls = 1, TabDisplay = 2, TabEffects = 3;

        RectTransform pop, box;
        CanvasGroup popGroup;
        Image dim;
        readonly RectTransform[] pages = new RectTransform[4];
        readonly Image[] tabBg = new Image[4];
        readonly TMP_Text[] tabText = new TMP_Text[4];
        readonly RectTransform[] tabLine = new RectTransform[4];
        int tab;

        // controls
        KitSlider master, sfx, pan, zoom;
        KitChoice screenMode, fpsCap, quality, level, cinematic;
        KitStepper resolution;
        KitSwitch vsync, reduceSwitch, fxSwitch, shake;
        float clock; bool running;

        public bool IsOpen => gameObject.activeSelf;
        public int Tab => tab;

        /// <summary>Builds the (closed) panel under `parent`, which should be a full-screen rect.</summary>
        public static KitOptionsPanel Build(Transform parent)
        {
            GameSettings.Load();
            var theme = UITheme.Current;
            var root = HudKit.NewRect("OptionsPanel", parent);
            HudKit.Stretch(root);
            var p = root.gameObject.AddComponent<KitOptionsPanel>();

            var dimRt = HudKit.NewRect("DecoDim", root);
            HudKit.Stretch(dimRt);
            p.dim = HudKit.AddImage(dimRt, DimColor, raycast: true);          // blocks every click behind the panel

            p.pop = HudKit.NewRect("Pop", root);
            p.pop.anchorMin = p.pop.anchorMax = p.pop.pivot = new Vector2(.5f, .5f);
            p.pop.sizeDelta = new Vector2(Width, Height);
            p.popGroup = p.pop.gameObject.AddComponent<CanvasGroup>();

            var boxPanel = TacticalPanel.Create(p.pop, "Box", KitColor.Paper, SlantedGraphic.Size.Custom, 0f);
            HudKit.Stretch(boxPanel.rectTransform);
            boxPanel.raycastTarget = true;
            boxPanel.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.Mockup);
            var shadow = KitShadow.Behind(boxPanel.rectTransform, new Color(.03f, .04f, .06f), .6f, 14f, 30f).rectTransform;
            HudKit.Stretch(shadow);
            p.box = boxPanel.rectTransform;

            // head
            var head = HudKit.NewRect("Head", p.box);
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

            p.BuildTabs();
            p.BuildSound(p.Page(TabSound));
            p.BuildControls(p.Page(TabControls));
            p.BuildDisplay(p.Page(TabDisplay));
            p.BuildEffects(p.Page(TabEffects));
            p.BuildFooter();

            p.ShowTab(TabSound);
            root.gameObject.SetActive(false);
            return p;
        }

        // ---- tabs and pages ----------------------------------------------------------------------------------------------------
        void BuildTabs()
        {
            const float gap = 4f;
            float w = (Width - PadX * 2f - gap * 3f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var rt = HudKit.NewRect("Tab" + i, box);
                UILayout.Place(rt, PadX + i * (w + gap), HeadH + TabTop, w, TabH);
                tabBg[i] = HudKit.AddImage(rt, UITheme.Current.paper2, raycast: true);
                rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
                rt.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                var b = rt.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None; b.targetGraphic = tabBg[i];
                int idx = i;
                b.onClick.AddListener(() => ShowTab(idx));
                tabText[i] = HudKit.AddText(rt, TabNames[i], 14f, UITheme.Current.ink, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Body);
                HudKit.Stretch(tabText[i].rectTransform);
                tabLine[i] = HudKit.NewRect("DecoLine", rt);
                tabLine[i].anchorMin = new Vector2(0f, 0f); tabLine[i].anchorMax = new Vector2(1f, 0f); tabLine[i].pivot = new Vector2(.5f, 0f);
                tabLine[i].anchoredPosition = Vector2.zero; tabLine[i].sizeDelta = new Vector2(0f, 3f);
                HudKit.AddImage(tabLine[i], UITheme.Current.blue);
            }
        }

        RectTransform Page(int i)
        {
            var rt = HudKit.NewRect("Page" + i, box);
            UILayout.Place(rt, 0f, HeadH + TabsH, Width, RowH * Rows);
            pages[i] = rt;
            return rt;
        }

        /// <summary>Opens tab `i` (サウンド / 操作 / 画面 / 演出).</summary>
        public void ShowTab(int i)
        {
            tab = Mathf.Clamp(i, 0, 3);
            var theme = UITheme.Current;
            for (int k = 0; k < 4; k++)
            {
                bool on = k == tab;
                if (pages[k].gameObject.activeSelf != on) pages[k].gameObject.SetActive(on);
                tabBg[k].color = on ? theme.ink : theme.paper2;
                tabText[k].color = on ? Color.white : theme.ink;
                tabLine[k].gameObject.SetActive(on);
            }
        }

        // a row: label 14 + a note 11 on the left; the control is placed by the caller at ControlY(index)
        static void Row(RectTransform page, int index, string label, string note, float controlW)
        {
            var theme = UITheme.Current;
            float top = index * RowH;
            float textW = Width - PadX * 2f - controlW - 12f;
            var l = HudKit.AddText(page, label, 14f, theme.ink, true, TextAlignmentOptions.TopLeft, "Label", UITheme.TextRole.Body);
            UILayout.LineHeight(l, 1.25f);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(l.rectTransform, PadX, top + 9f, textW, 18f);
            var n = HudKit.AddText(page, note, 11f, theme.dim, false, TextAlignmentOptions.TopLeft, "Note", UITheme.TextRole.Body);
            n.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(n, 1.25f);
            UILayout.Place(n.rectTransform, PadX, top + 29f, textW, 28f);
            if (index >= (int)Rows - 1) return;
            var line = HudKit.NewRect("DecoRule", page);
            UILayout.Place(line, PadX, top + RowH - 1f, Width - PadX * 2f, 1f);
            HudKit.AddImage(line, Color.Lerp(theme.paper, theme.ink, .12f));
        }

        static void PlaceControl(Component c, int index, float w, float h)
        {
            UILayout.Place((RectTransform)c.transform, Width - PadX - w, index * RowH + (RowH - h) * .5f, w, h);
        }

        T Add<T>(RectTransform page, int index, string label, string note, T control, float w, float h) where T : Component
        {
            Row(page, index, label, note, w);
            PlaceControl(control, index, w, h);
            return control;
        }

        void BuildSound(RectTransform page)
        {
            master = Add(page, 0, "マスター音量", "ゲーム全体の音量", KitSlider.Create(page, "Master", 0f, 1f, .05f, v => GameSettings.SetMasterVolume(v)), KitSlider.W, KitSlider.H);
            sfx = Add(page, 1, "効果音", "銃声・爆発・操作音などの音量", KitSlider.Create(page, "Sfx", 0f, 1f, .05f, v => GameSettings.SetSfxVolume(v)), KitSlider.W, KitSlider.H);
        }

        void BuildControls(RectTransform page)
        {
            pan = Add(page, 0, "カメラ移動速度", "WASD・矢印キーでカメラを動かす速さ", KitSlider.Create(page, "Pan", GameSettings.MinSpeed, GameSettings.MaxSpeed, .1f, v => GameSettings.SetCameraPanSpeed(v)), KitSlider.W, KitSlider.H);
            zoom = Add(page, 1, "ズーム感度", "マウスホイール1回分のズーム量", KitSlider.Create(page, "Zoom", GameSettings.MinSpeed, GameSettings.MaxSpeed, .1f, v => GameSettings.SetZoomSpeed(v)), KitSlider.W, KitSlider.H);
        }

        void BuildDisplay(RectTransform page)
        {
            float modeW = KitChoice.WidthOf(2, 112f), fpsW = KitChoice.WidthOf(4, 56f);
            screenMode = Add(page, 0, "画面モード", "全画面表示とウィンドウ表示を切り替えます",
                KitChoice.Create(page, "ScreenMode", new[] { "フルスクリーン", "ウィンドウ" }, i => GameSettings.SetFullscreen(i == 0), 112f), modeW, KitChoice.H);
            resolution = Add(page, 1, "解像度", "ゲーム画面の大きさ", KitStepper.Create(page, "Resolution", StepResolution), KitStepper.W, KitStepper.H);
            vsync = Add(page, 2, "垂直同期", "画面のちらつき(ティアリング)を抑えます", KitSwitch.Create(page, "VSync", on => GameSettings.SetVSync(on)), KitSwitch.W, KitSwitch.H);
            fpsCap = Add(page, 3, "フレームレート上限", "垂直同期がオフのときに有効です",
                KitChoice.Create(page, "FpsCap", new[] { "30", "60", "120", "無制限" }, i => GameSettings.SetFpsCap(i), 56f), fpsW, KitChoice.H);
            var qn = QualityLabels();
            float qW = KitChoice.WidthOf(qn.Length, 70f);
            quality = Add(page, 4, "画質", "描画の品質(低いほど動作が軽くなります)", KitChoice.Create(page, "Quality", qn, i => GameSettings.SetQuality(i), 70f), qW, KitChoice.H);
        }

        // the project's quality levels (Mobile / PC) as 標準 / 高; any other count by number
        static string[] QualityLabels()
        {
            int n = Mathf.Max(1, QualitySettings.names.Length);
            if (n == 2) return new[] { "標準", "高" };
            if (n == 3) return new[] { "低", "中", "高" };
            var a = new string[n];
            for (int i = 0; i < n; i++) a[i] = (i + 1).ToString();
            return a;
        }

        void StepResolution(int dir)
        {
            var list = GameSettings.Resolutions();
            int i = list.IndexOf(GameSettings.Resolution);
            if (i < 0) i = list.Count - 1;
            i = Mathf.Clamp(i + dir, 0, list.Count - 1);
            GameSettings.SetResolution(list[i]);
        }

        void BuildEffects(RectTransform page)
        {
            reduceSwitch = Add(page, 0, "モーション軽減", "グリッチ・揺れ・流れる縞を止め、フェードだけにします",
                KitSwitch.Create(page, "ReduceSwitch", on => UIMotionSettings.SetReduceMotion(on)), KitSwitch.W, KitSwitch.H);
            fxSwitch = Add(page, 1, "画面エフェクト", "走査線やノイズなど、画面全体の演出",
                KitSwitch.Create(page, "FxSwitch", on => UIMotionSettings.SetScreenFx(on)), KitSwitch.W, KitSwitch.H);
            level = Add(page, 2, "エフェクトの強さ", "画面エフェクトとグリッチの強さ",
                KitChoice.Create(page, "LevelChoice", new[] { "弱", "中", "強" }, i => UIMotionSettings.SetEffectLevel(i)), KitChoice.TotalW, KitChoice.H);
            cinematic = Add(page, 3, "戦闘演出", "交戦時のカメラ演出",
                KitChoice.Create(page, "Cinematic", new[] { BattleDirector.ModeLabel(BattleCinematicMode.Full), BattleDirector.ModeLabel(BattleCinematicMode.Short), BattleDirector.ModeLabel(BattleCinematicMode.Off) },
                    i => { BattleDirector.Mode = (BattleCinematicMode)i; Refresh(false); }), KitChoice.TotalW, KitChoice.H);
            shake = Add(page, 4, "画面の揺れ", "爆発などでカメラが揺れます",
                KitSwitch.Create(page, "Shake", on => GameSettings.SetScreenShake(on)), KitSwitch.W, KitSwitch.H);
        }

        void BuildFooter()
        {
            var theme = UITheme.Current;
            float y = HeadH + TabsH + RowH * Rows;

            // 「初期設定に戻す」: paper2, cut at the bottom left
            var reset = HudKit.NewRect("Reset", box);
            UILayout.Place(reset, 0f, y, ResetW, FootH);
            var resetImg = HudKit.AddImage(reset, theme.paper2, raycast: true);
            reset.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.BottomLeft);
            var rb = reset.gameObject.AddComponent<Button>();
            rb.transition = Selectable.Transition.None; rb.targetGraphic = resetImg;
            rb.onClick.AddListener(ResetTab);
            reset.gameObject.AddComponent<KitBrighten>().image = resetImg;
            var rt = HudKit.AddText(reset, "初期設定に戻す", 14f, theme.ink, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            UILayout.Place(rt.rectTransform, 24f, 0f, ResetW - 36f, FootH);

            // 「閉じる」 (blue, slanted at the right) + ESC
            float cx = ResetW - 12f, cw = Width - cx;
            var foot = HudKit.NewRect("Close", box);
            UILayout.Place(foot, cx, y, cw, FootH);
            var footImg = HudKit.AddImage(foot, theme.blue, raycast: true);
            foot.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, YesSlant);
            var btn = foot.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; btn.targetGraphic = footImg;
            btn.onClick.AddListener(Close);
            foot.gameObject.AddComponent<KitBrighten>().image = footImg;
            var closeText = HudKit.AddText(foot, "閉じる", 16f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            UILayout.Place(closeText.rectTransform, 40f, 0f, cw - 40f - 110f, FootH);
            var esc = HudKit.AddText(foot, "ESC", 11f, Color.Lerp(theme.blue, Color.white, .8f), true, TextAlignmentOptions.MidlineRight, "Esc", UITheme.TextRole.Display);
            esc.characterSpacing = 10f;
            UILayout.Place(esc.rectTransform, cw - 24f - YesSlant - 60f, 0f, 60f, FootH);
        }

        /// <summary>The open tab's values back to their defaults.</summary>
        public void ResetTab()
        {
            switch (tab)
            {
                case TabSound: GameSettings.ResetSound(); break;
                case TabControls: GameSettings.ResetControls(); break;
                case TabDisplay: GameSettings.ResetDisplay(); break;
                case TabEffects:
                    UIMotionSettings.SetReduceMotion(false);
                    UIMotionSettings.SetScreenFx(true);
                    UIMotionSettings.SetEffectLevel(UIMotionSettings.LevelMedium);
                    BattleDirector.Mode = BattleCinematicMode.Full;
                    GameSettings.ResetEffects();
                    break;
            }
            Refresh(false);
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
            master.Set(GameSettings.MasterVolume);
            sfx.Set(GameSettings.SfxVolume);
            pan.Set(GameSettings.CameraPanSpeed);
            zoom.Set(GameSettings.ZoomSpeed);
            screenMode.Set(GameSettings.Fullscreen ? 0 : 1);
            var r = GameSettings.Resolution;
            resolution.SetText(r.x > 0 ? r.x + " × " + r.y : "—");
            vsync.Set(GameSettings.VSync, instant);
            fpsCap.Set(GameSettings.FpsCapIndex);
            quality.Set(Mathf.Clamp(GameSettings.Quality, 0, quality.cells.Length - 1));
            reduceSwitch.Set(UIMotionSettings.reduceMotion, instant);
            fxSwitch.Set(UIMotionSettings.screenFx, instant);
            level.Set(UIMotionSettings.effectLevel);
            cinematic.Set((int)BattleDirector.Mode);
            shake.Set(GameSettings.ScreenShake, instant);
        }

        void OnEnable() { UIMotionSettings.Changed += OnChanged; GameSettings.Changed += OnChanged; }
        void OnDisable() { UIMotionSettings.Changed -= OnChanged; GameSettings.Changed -= OnChanged; }
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
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); Close(); return; }
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
