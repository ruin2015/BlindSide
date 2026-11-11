// A conversation scene, visual-novel style (the HQ only: its tutorial; in a match people talk on the radio), on its own overlay canvas (menu
// 1440×810, order 80): the speaker's full illustration on the left standing behind a wide black box, a blue name plate on the box's top
// edge, the line centred in the box, typed in; the choices, when there are any, stacked at the right above the box (black, a blue bar on
// the left; under the pointer one slides left, grows a little, turns deep blue and its bar widens — HubHover). A click / Space / Enter
// finishes the typing, then goes on; a choice is clicked or picked with 1 / 2 …
// One unscaled clock (ITimedEffect.Evaluate(t)): the box fades in and the art slides in; per frame only the typing moves, nothing is allocated.
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class KitDialogue : MonoBehaviour, ITimedEffect
    {
        public const float BoxX = 120f, BoxY = 584f, BoxW = 1200f, BoxH = 190f, ArtX = 70f, ArtY = 196f, ArtS = 440f;
        public const float NameX = 176f, NameW = 280f, NameH = 44f, LineSize = 27f, LinePad = 64f;
        public const float ChoiceW = 316f, ChoiceH = 44f, ChoiceGap = 10f, ChoiceRight = 36f, ChoiceBar = 14f, ChoiceSize = 20f;
        public const float ChoiceHoverShift = 16f, ChoiceBarHover = 24f;
        public const float InTime = .25f, ArtSlide = 40f, CharsPerSecond = 40f, NextBlink = .8f;
        public const int SortingOrder = 80, MaxChoices = 4;

        Canvas canvas;
        RectTransform root, art, next;
        CanvasGroup group;
        Image artImage;
        TMP_Text nameText, lineText;
        readonly RectTransform[] choiceRects = new RectTransform[MaxChoices];
        readonly TMP_Text[] choiceTexts = new TMP_Text[MaxChoices];
        readonly Image[] choiceBacks = new Image[MaxChoices];
        readonly HubHover[] choiceHovers = new HubHover[MaxChoices];
        int choiceCount, chars;
        float clock, typeClock;
        bool running, typing, asking;

        public float Duration => InTime;
        public bool IsOpen => gameObject.activeSelf;
        /// <summary>The line was read and clicked on.</summary>
        public bool Advanced { get; private set; }
        /// <summary>The choice taken (0-based), −1 until one is.</summary>
        public int Choice { get; private set; } = -1;

        // ---- use ---------------------------------------------------------------------------------------------------------------
        /// <summary>`key` (an operator: its illustration and kana) says `line`; wait for Advanced.</summary>
        public void Say(string key, string line)
        {
            Show(key, line, null);
        }

        /// <summary>`key` says `line` and offers `options`; wait for Choice.</summary>
        public void Ask(string key, string line, params string[] options)
        {
            Show(key, line, options);
        }

        /// <summary>Says a line and waits for the click.</summary>
        public IEnumerator Run(string key, string line)
        {
            Say(key, line);
            while (!Advanced) yield return null;
        }

        /// <summary>Asks and waits for the choice (read it from Choice).</summary>
        public IEnumerator RunAsk(string key, string line, params string[] options)
        {
            Ask(key, line, options);
            while (Choice < 0) yield return null;
        }

        public void Close()
        {
            running = false;
            gameObject.SetActive(false);
        }

        /// <summary>Previews: the line typed out, the choices up.</summary>
        public void PoseAt(string key, string line, params string[] options)
        {
            typing = false; running = false;
            Fill(key, line, options);
            gameObject.SetActive(true);
            lineText.maxVisibleCharacters = 99999;
            Evaluate(InTime);
            SetNext(options == null || options.Length == 0);
        }

        void Show(string key, string line, string[] options)
        {
            bool wasOpen = gameObject.activeSelf;
            Fill(key, line, options);
            gameObject.SetActive(true);
            if (!wasOpen) clock = 0f;
            typeClock = 0f; typing = true; running = true;
            lineText.maxVisibleCharacters = 0;
            SetNext(false);
            Evaluate(clock);
        }

        void Fill(string key, string line, string[] options)
        {
            Advanced = false; Choice = -1;
            var op = Blindside.Data.BuildAssetRegistry.Operator(key);
            nameText.text = op != null ? op.kr : key;
            var body = OperatorArt.Body(key);
            artImage.sprite = body;
            artImage.enabled = body != null;
            lineText.text = line;
            chars = line.Length;
            asking = options != null && options.Length > 0;
            choiceCount = asking ? Mathf.Min(options.Length, MaxChoices) : 0;
            for (int i = 0; i < MaxChoices; i++)
            {
                bool on = i < choiceCount;
                choiceRects[i].gameObject.SetActive(false);
                if (!on) continue;
                choiceTexts[i].text = options[i];
                // stacked up from the box, the first on top
                UILayout.Place(choiceRects[i], BoxX + BoxW - ChoiceRight - ChoiceW, BoxY - ChoiceGap - (choiceCount - i) * (ChoiceH + ChoiceGap) + ChoiceGap, ChoiceW, ChoiceH);
                choiceHovers[i].SetBase(choiceRects[i].anchoredPosition);
            }
            if (!typing && !running) ShowChoices();
        }

        void ShowChoices() { for (int i = 0; i < choiceCount; i++) if (!choiceRects[i].gameObject.activeSelf) choiceRects[i].gameObject.SetActive(true); }

        void SetNext(bool on) { if (next.gameObject.activeSelf != on) next.gameObject.SetActive(on); }

        public void Pick(int i) { if (asking && !typing && i < choiceCount) { Choice = i; } }

        // ---- motion ------------------------------------------------------------------------------------------------------------
        public void Evaluate(float t)
        {
            float k = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01(t / InTime));
            if (group.alpha != k) group.alpha = k;
            var p = new Vector2(ArtX - ArtSlide * (1f - k), -ArtY);
            if (art.anchoredPosition != p) art.anchoredPosition = p;
        }

        void Update()
        {
            if (!running) return;
            float dt = Time.unscaledDeltaTime;
            clock += dt;
            if (clock <= InTime + dt) Evaluate(clock);

            var kb = Keyboard.current; var mouse = Mouse.current;
            bool press = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                         || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame));
            if (typing)
            {
                typeClock += dt;
                int shown = UIMotionSettings.reduceMotion ? chars : Mathf.FloorToInt(typeClock * CharsPerSecond);
                if (press) shown = chars;
                if (lineText.maxVisibleCharacters != shown) lineText.maxVisibleCharacters = shown;
                if (shown < chars) return;
                typing = false;
                if (asking) ShowChoices(); else SetNext(true);
                return;
            }
            if (asking)
            {
                if (kb == null) return;
                for (int i = 0; i < choiceCount; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame) { Pick(i); return; }
                return;
            }
            if (!Advanced)
            {
                float b = Mathf.Repeat(clock, NextBlink) / NextBlink;
                var s = Vector3.one * (b < .5f ? 1f : .75f);
                if (next.localScale != s) next.localScale = s;
                if (press) Advanced = true;
            }
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        /// <param name="onCanvas">build onto this canvas instead of a new overlay one (Render Previews' stage)</param>
        public static KitDialogue Build(Transform parent, Canvas onCanvas = null)
        {
            var theme = UITheme.Current;
            var c = onCanvas;
            if (c == null)
            {
                var cgo = new GameObject("Dialogue_Canvas", typeof(RectTransform));
                cgo.transform.SetParent(parent, false);
                c = cgo.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = SortingOrder;
                theme.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
                cgo.AddComponent<GraphicRaycaster>();
            }
            var root = HudKit.NewRect("Dialogue", c.transform);
            HudKit.Stretch(root);
            var d = root.gameObject.AddComponent<KitDialogue>();
            d.canvas = c; d.root = root;
            d.group = root.gameObject.AddComponent<CanvasGroup>();

            d.art = HudKit.NewRect("Art", root);
            UILayout.Place(d.art, ArtX, ArtY, ArtS, ArtS);
            d.artImage = d.art.gameObject.AddComponent<Image>();
            d.artImage.preserveAspect = true; d.artImage.raycastTarget = false;
            d.art.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the speaker stands behind the box on purpose";

            var box = HudKit.NewRect("Box", root);
            UILayout.Place(box, BoxX, BoxY, BoxW, BoxH);
            HudKit.AddImage(box, Color.black);

            var plate = HudKit.NewRect("NamePlate", root);
            UILayout.Place(plate, NameX, BoxY - NameH * .5f, NameW, NameH);
            HudKit.AddImage(plate, theme.blue);
            plate.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            plate.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the name plate sits on the box's top edge on purpose";
            d.nameText = HudKit.AddText(plate, "", 23f, Color.white, true, TextAlignmentOptions.Center, "Name", UITheme.TextRole.Title);
            d.nameText.textWrappingMode = TextWrappingModes.NoWrap;
            HudKit.Stretch(d.nameText.rectTransform);

            d.lineText = HudKit.AddText(box, "", LineSize, Color.white, false, TextAlignmentOptions.Center, "Line", UITheme.TextRole.Body);
            d.lineText.textWrappingMode = TextWrappingModes.Normal;
            var lr = d.lineText.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(LinePad, 20f); lr.offsetMax = new Vector2(-LinePad, -30f);

            d.next = HudKit.NewRect("DecoNext", box);
            d.next.anchorMin = d.next.anchorMax = new Vector2(1f, 0f); d.next.pivot = new Vector2(.5f, .5f);
            d.next.sizeDelta = new Vector2(16f, 16f); d.next.anchoredPosition = new Vector2(-36f, 32f);
            d.next.localRotation = Quaternion.Euler(0f, 0f, 45f);
            HudKit.AddImage(d.next, theme.acc);
            d.next.gameObject.SetActive(false);

            for (int i = 0; i < MaxChoices; i++)
            {
                int idx = i;
                var r = HudKit.NewRect("Choice" + i, root);
                UILayout.Place(r, 0f, 0f, ChoiceW, ChoiceH);
                d.choiceRects[i] = r;
                d.choiceBacks[i] = HudKit.AddImage(r, Color.black, true);
                var btn = r.gameObject.AddComponent<Button>();
                btn.targetGraphic = d.choiceBacks[i];
                btn.transition = Selectable.Transition.None;   // (HubHover tints it)
                btn.onClick.AddListener(() => d.Pick(idx));
                var bar = HudKit.NewRect("DecoBar", r);
                UILayout.Place(bar, 0f, 0f, ChoiceBar, ChoiceH);
                HudKit.AddImage(bar, theme.blue);
                // hover: slides left, grows a little, the black turns to a deep blue and the bar widens
                var hv = r.gameObject.AddComponent<HubHover>();
                hv.target = r; hv.graphic = d.choiceBacks[i];
                hv.baseColor = Color.black; hv.hoverColor = Color.Lerp(Color.black, theme.blue, .45f);
                hv.shift = new Vector2(-ChoiceHoverShift, 0f); hv.scale = 1.04f;
                hv.grow = bar; hv.growFrom = ChoiceBar; hv.growTo = ChoiceBarHover;
                d.choiceHovers[i] = hv;
                d.choiceTexts[i] = HudKit.AddText(r, "", ChoiceSize, Color.white, false, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Body);
                d.choiceTexts[i].textWrappingMode = TextWrappingModes.NoWrap;
                var tr = d.choiceTexts[i].rectTransform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(ChoiceBar + 6f, 0f); tr.offsetMax = new Vector2(-6f, 0f);
                r.gameObject.SetActive(false);
            }
            root.gameObject.SetActive(false);
            return d;
        }
    }
}
