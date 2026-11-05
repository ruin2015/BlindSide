// The tutorial's radio: CRUSHER's face on the left of the HUD and his words in a dark window beside it, as if he were talking over the
// headset. One line = one run of the clock (ITimedEffect.Evaluate(t), unscaled):
//   0 – OpenTime     (only when the radio was closed) the box slides in from the left and fades in; the face flickers like a signal
//                    coming in
//   from 0           the line types on at CharsPerSecond (TMP maxVisibleCharacters — nothing re-laid out); the voice bars beside the
//                    name dance while it types and sink GaugeSettle s after
//   waiting          a line that wants a click shows the blinking advance mark; a click / Space / Enter while typing shows it all, a
//                    second one answers it (Advanced). A line that waits for the player to act keeps showing, no mark, clicks pass by.
// Nothing allocates per frame. Built at runtime on its own overlay canvas (HUD scaler, above the selection UI).
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialRadio : MonoBehaviour, ITimedEffect
    {
        public const float OpenTime = .32f, CloseTime = .22f, CharsPerSecond = 34f, SlideFrom = 46f, GaugeSettle = .3f;
        // bottom left (Lift raises it over the unit panel while that is up); bigger box and type, so the lines read at a glance
        // (740 wide: clear of the hint bar, which starts at centre − 135)
        // (the box hugs the portrait: 12 + face + underline 4 + 12, no blank below it; three lines still fit the text column)
        public const float BoxW = 640f, FaceSize = 128f, BoxH = 12f + FaceSize + 4f + 12f, BoxX = 20f, BoxBottom = 20f;
        // (22 px: a 21-character line — 20 and its punctuation — fits the text column)
        public const float NameSize = 22f, LineSize = 22f, TagSize = 12f, LiftSpeed = 1400f;
        public const float TagY = 10f, NameY = 24f, RuleY = 56f, LineY = 63f;
        public const int GaugeBars = 16;
        public const float BarW = 3f, BarGap = 2f, BarMin = 2f, BarMax = 18f;
        /// <summary>How far above BoxBottom the box wants to sit (eased): the director sets it while the unit panel is up.</summary>
        public float Lift;
        float liftNow;
        float BoxYPos => BoxBottom + liftNow;
        public static readonly Color CrusherColor = UIPalette.Hex("#EAAF20");

        public Canvas canvas;
        public RectTransform box, face, scanBand, advanceMark;
        public CanvasGroup group, faceGroup;
        public TMP_Text line;
        public RectTransform[] bars;
        public TMP_Text nameText, tagText, hqMark;
        Image advanceImage;

        public const string CrusherName = "CRUSHER  <size=16><color=#9AA0A8>クラッシャー</color></size>";
        public const string HqName = "HQ  <size=16><color=#9AA0A8>無線機</color></size>";

        /// <summary>Who is talking: CRUSHER (his face) or the HQ radio (an "HQ" mark in the slot, red accents, the emergency channel).</summary>
        public void SetSpeaker(bool hq)
        {
            nameText.text = hq ? HqName : CrusherName;
            nameText.color = hq ? UIPalette.Foe : CrusherColor;
            tagText.text = hq ? "RADIO // EMERGENCY" : "RADIO // CH-07";
            tagText.color = hq ? UIPalette.Foe : UIPalette.Accent;
            var faceImg = face.Find("Face");
            if (faceImg != null) faceImg.gameObject.SetActive(!hq);
            hqMark.gameObject.SetActive(hq);
            foreach (var n in new[] { "DecoRadioTopLine", "DecoFaceUnderline" })
            {
                var line = box.Find(n);
                if (line != null) line.GetComponent<Image>().color = hq ? UIPalette.Foe : CrusherColor;
            }
            var slotImg = face.GetComponent<Image>();
            if (slotImg != null) slotImg.color = hq ? new Color(.16f, .05f, .07f, 1f) : Color.white;   // (white = the paper gradient as is)
        }

        /// <summary>Another squad member on the radio (the match's spot / contact calls): their face, codename and colour.</summary>
        public void SetOperator(string key, string en, string kr, Color color)
        {
            nameText.text = $"{en}  <size=16><color=#9AA0A8>{kr}</color></size>";
            nameText.color = color;
            tagText.text = "RADIO // SQUAD";
            tagText.color = UIPalette.Accent;
            hqMark.gameObject.SetActive(false);
            var slotImg = face.GetComponent<Image>();
            var img = OperatorArt.ApplyFace(slotImg, key);
            var faceT = face.Find("Face");
            if (faceT != null) faceT.gameObject.SetActive(img != null);
            if (img == null && slotImg != null) { slotImg.sprite = null; slotImg.color = color; }
            foreach (var n in new[] { "DecoRadioTopLine", "DecoFaceUnderline" })
            {
                var l = box.Find(n);
                if (l != null) l.GetComponent<Image>().color = color;
            }
            for (int i = 0; i < bars.Length; i++)
                bars[i].GetComponent<Image>().color = new Color(color.r, color.g, color.b, i % 4 == 3 ? .5f : .9f);
        }

        float clock, closeClock = -1f, duration = 1f;
        bool running, opening, wantsClick, open;
        int chars;

        /// <summary>The current line has been clicked away (only for a line that wanted a click).</summary>
        public bool Advanced { get; private set; }
        public bool IsOpen => open;
        public bool Typing => clock * CharsPerSecond < chars;
        public float Duration => duration;

        /// <summary>Shows `text` (opening the radio when it is closed). `waitClick`: the line waits for a click (Advanced) — otherwise it
        /// stays up while the player does what it says.</summary>
        public void Say(string text, bool waitClick = true)
        {
            opening = !open;
            open = true; closeClock = -1f;
            gameObject.SetActive(true);
            line.text = text;
            line.maxVisibleCharacters = 99999;
            line.ForceMeshUpdate();
            chars = line.textInfo.characterCount;
            wantsClick = waitClick;
            Advanced = false;
            duration = (opening ? OpenTime : 0f) + chars / CharsPerSecond + GaugeSettle;
            clock = 0f; running = true;
            Evaluate(0f);
        }

        /// <summary>Slides the radio away.</summary>
        public void Close()
        {
            if (!open) return;
            open = false; closeClock = 0f; running = false;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (closeClock >= 0f)
            {
                closeClock += dt;
                float k = 1f - Mathf.Clamp01(closeClock / CloseTime);
                group.alpha = k;
                box.anchoredPosition = new Vector2(BoxX - SlideFrom * (1f - k), BoxYPos);
                if (closeClock >= CloseTime) { closeClock = -1f; gameObject.SetActive(false); }
                return;
            }
            if (!open) return;
            liftNow = Mathf.MoveTowards(liftNow, Lift, LiftSpeed * dt);
            clock += dt;
            Evaluate(clock);
            if (wantsClick && !Advanced && Pressed())
            {
                float typeStart = opening ? OpenTime : 0f;
                if (clock < typeStart + chars / CharsPerSecond) clock = typeStart + chars / CharsPerSecond;   // show it all first
                else Advanced = true;
            }
        }

        static bool Pressed()
        {
            var m = Mouse.current; var kb = Keyboard.current;
            // a click on a UI control (the SKIP button, END TURN …) is that control's, not "next line"
            bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            return (m != null && m.leftButton.wasPressedThisFrame && !overUi) ||
                   (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame));
        }

        public void Evaluate(float t)
        {
            float typeStart = opening ? OpenTime : 0f;
            // open: slide + fade; the face comes in through a few flickers
            float o = opening ? UIEase.Tactical(t / OpenTime) : 1f;
            group.alpha = o;
            box.anchoredPosition = new Vector2(BoxX - SlideFrom * (1f - o), BoxYPos);
            float flick = opening && t < OpenTime + .12f ? ((int)(t * 30f) % 3 == 0 ? .25f : 1f) : 1f;
            faceGroup.alpha = flick;

            float lt = t - typeStart;
            int shown = Mathf.Clamp(Mathf.FloorToInt(lt * CharsPerSecond), 0, chars);
            if (line.maxVisibleCharacters != shown) line.maxVisibleCharacters = shown;

            // the voice
            float speakEnd = chars / CharsPerSecond;
            float voice = lt < 0f ? 0f : 1f - Mathf.Clamp01((lt - speakEnd) / GaugeSettle);
            float pulse = .55f + .45f * Mathf.Abs(Mathf.Sin(t * 9.5f)) * (.7f + .3f * Mathf.Sin(t * 3.1f));
            float mid = (bars.Length - 1) * .5f;
            for (int i = 0; i < bars.Length; i++)
            {
                float shape = 1f - .55f * Mathf.Abs(i - mid) / mid;
                float n = Mathf.PerlinNoise(t * 11f + i * .53f, i * 1.7f);
                bars[i].sizeDelta = new Vector2(BarW, Mathf.Lerp(BarMin, BarMax, Mathf.Clamp01((n - .2f) * pulse * shape * 2f) * voice));
            }

            // a faint scan band rolling down the face, all the time
            scanBand.anchoredPosition = new Vector2(0f, -Mathf.Repeat(t * 46f, FaceSize + 14f) + 14f);

            // the advance mark blinks once the line is out and wants a click
            bool waiting = wantsClick && shown >= chars;
            advanceImage.enabled = waiting && Mathf.Repeat(t, .8f) < .55f;
        }

        // ================================================================
        //  Construction
        // ================================================================
        /// <param name="onCanvas">build straight onto this (HUD-scaled) canvas instead of a new overlay one (Render Previews' stage)</param>
        public static TutorialRadio Build(Transform parent, Canvas onCanvas = null)
        {
            var canvas = onCanvas;
            if (canvas == null)
            {
                var cgo = new GameObject("TutorialRadio_Canvas", typeof(RectTransform));
                cgo.transform.SetParent(parent, false);
                canvas = cgo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 4;   // under the selection UI (5): its menu, hint bar and confirm dialog always stay on top
                UITheme.Current.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            }

            var root = NewRect("TutorialRadio", canvas.transform);
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f); root.pivot = new Vector2(0f, 0f);   // bottom left
            root.sizeDelta = new Vector2(BoxW, BoxH);
            root.anchoredPosition = new Vector2(BoxX, BoxBottom);
            var r = root.gameObject.AddComponent<TutorialRadio>();
            r.canvas = canvas; r.box = root;
            r.group = root.gameObject.AddComponent<CanvasGroup>();
            r.group.blocksRaycasts = false; r.group.interactable = false;

            // the window
            var bg = NewRect("DecoRadioPanel", root); Place(bg, 0, 0, BoxW, BoxH);
            Img(bg, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .92f));
            var top = NewRect("DecoRadioTopLine", root); Place(top, 0, 0, BoxW, 3);
            Img(top, CrusherColor);
            var side = NewRect("DecoRadioSideLine", root); Place(side, BoxW - 3, 3, 3, BoxH - 3);
            Img(side, new Color(1f, 1f, 1f, .08f));

            // the face
            var slot = NewRect("Portrait", root); Place(slot, 12, 12, FaceSize, FaceSize);   // the paper slot; ApplyFace puts "Face" inside
            var slotImg = Img(slot, CrusherColor);
            OperatorArt.ApplyFace(slotImg, "CRUSHER");
            r.face = slot;
            r.faceGroup = slot.gameObject.AddComponent<CanvasGroup>();
            var band = NewRect("DecoFaceScan", slot);
            band.anchorMin = new Vector2(0f, 1f); band.anchorMax = new Vector2(1f, 1f); band.pivot = new Vector2(.5f, 1f);
            band.sizeDelta = new Vector2(0f, 14f);
            Img(band, new Color(1f, 1f, 1f, .1f));
            r.scanBand = band;
            var under = NewRect("DecoFaceUnderline", root); Place(under, 12, 12 + FaceSize, FaceSize, 4);
            Img(under, CrusherColor);

            // header: RADIO tag, the name, the voice bars
            float tx = 12 + FaceSize + 14;
            var tag = Text(root, "RADIO // CH-07", TagSize, UIPalette.Accent, UITheme.TextRole.Display, "Tag");
            tag.characterSpacing = 8f;
            Place(tag.rectTransform, tx, TagY, 360, 16);
            var name = Text(root, CrusherName, NameSize, CrusherColor, UITheme.TextRole.Display, "Name");
            Place(name.rectTransform, tx, NameY, 420, 29);
            r.nameText = name; r.tagText = tag;
            // the HQ radio has no face: a red "HQ" mark on the dark slot instead (hidden until SetSpeaker(true))
            var hq = Text(slot, "HQ", 56, UIPalette.Foe, UITheme.TextRole.Display, "HqMark");
            hq.alignment = TextAlignmentOptions.Center;
            var hrt = hq.rectTransform; hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.offsetMin = hrt.offsetMax = Vector2.zero;
            r.hqMark = hq;
            hq.gameObject.SetActive(false);
            r.bars = new RectTransform[GaugeBars];
            for (int i = 0; i < GaugeBars; i++)
            {
                var bar = NewRect("DecoVoiceBar" + i, root);
                bar.anchorMin = bar.anchorMax = new Vector2(0f, 1f); bar.pivot = new Vector2(0f, .5f);
                bar.anchoredPosition = new Vector2(BoxW - 24 - GaugeBars * (BarW + BarGap) + i * (BarW + BarGap), -(NameY + 14f));
                bar.sizeDelta = new Vector2(BarW, BarMin);
                Img(bar, new Color(CrusherColor.r, CrusherColor.g, CrusherColor.b, i % 4 == 3 ? .5f : .9f));
                r.bars[i] = bar;
            }
            var rule = NewRect("DecoRadioRule", root); Place(rule, tx, RuleY, BoxW - tx - 18, 1);
            Img(rule, new Color(1f, 1f, 1f, .14f));

            // the words (the lines carry their own breaks, at phrase boundaries; wrapping is the fallback)
            r.line = Text(root, "", LineSize, Color.white, UITheme.TextRole.Body, "Line");
            r.line.alignment = TextAlignmentOptions.TopLeft;
            r.line.textWrappingMode = TextWrappingModes.Normal;
            r.line.lineSpacing = 2f;
            Place(r.line.rectTransform, tx, LineY, BoxW - tx - 16, BoxH - LineY - 4);

            // advance mark: a small diamond bottom right
            var mark = NewRect("AdvanceMark", root);
            mark.anchorMin = mark.anchorMax = new Vector2(0f, 1f); mark.pivot = new Vector2(.5f, .5f);
            mark.anchoredPosition = new Vector2(BoxW - 20f, -(BoxH - 18f));
            mark.sizeDelta = new Vector2(11f, 11f);
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            r.advanceImage = Img(mark, UIPalette.Accent);
            r.advanceMark = mark;

            r.open = false;
            root.gameObject.SetActive(false);
            return r;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Top-left placement in the parent (y down).</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        static Image Img(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static TMP_Text Text(RectTransform parent, string s, float size, Color c, UITheme.TextRole role, string name)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.color = c; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UIFonts.Apply(t, role);
            return t;
        }
    }
}
