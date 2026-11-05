// The tutorial's alarm at the very end: the whole screen flashes red Pulses times (a siren light), red bars close in from the top and
// bottom edges, and a band across the middle reads EMERGENCY // 緊急入電 — the call that sends the squad to the real operation. One run
// of the clock (ITimedEffect.Evaluate(t), unscaled); each pulse's rise calls OnPulse (the director plays the alert sound there).
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialSiren : MonoBehaviour, ITimedEffect
    {
        public const int Pulses = 3;
        public const float Period = .8f, FlashAlpha = .42f, EdgeH = 64f, BandH = 86f, BandIn = .25f, Tail = .5f;

        public Image flash, edgeTop, edgeBottom, band;
        public RectTransform bandRt;
        public CanvasGroup group;
        public System.Action OnPulse;
        float clock; bool running; int pulsed;

        public float Duration => Pulses * Period + Tail;

        public void Play()
        {
            gameObject.SetActive(true);
            clock = 0f; pulsed = 0; running = true;
            Evaluate(0f);
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            int n = Mathf.Min(Pulses, Mathf.FloorToInt(clock / Period) + 1);
            while (pulsed < n) { pulsed++; OnPulse?.Invoke(); }
            Evaluate(clock);
            if (clock >= Duration) { running = false; gameObject.SetActive(false); }
        }

        public void Evaluate(float t)
        {
            float end = Pulses * Period;
            // each pulse: a quick rise and a slower fall, like a rotating beacon
            float k = t < end ? Mathf.Repeat(t, Period) / Period : 1f;
            float beat = t < end ? (k < .18f ? k / .18f : Mathf.Pow(1f - (k - .18f) / .82f, 2f)) : 0f;
            float fade = 1f - Mathf.Clamp01((t - end) / Tail);
            SetA(flash, FlashAlpha * beat);
            float edge = Mathf.Clamp01(t / BandIn) * fade;
            SetA(edgeTop, .85f * edge); SetA(edgeBottom, .85f * edge);
            group.alpha = fade;
            float b = UIEase.Tactical(t / BandIn);
            bandRt.localScale = new Vector3(1f, b, 1f);
        }

        static void SetA(Graphic g, float a) { var c = g.color; if (c.a != a) { c.a = a; g.color = c; } }

        // ================================================================
        //  Construction (full screen, on the radio's canvas)
        // ================================================================
        public static TutorialSiren Build(Transform canvas)
        {
            var root = NewRect("TutorialSiren", canvas);
            root.SetAsFirstSibling();   // under the radio: the HQ call reads on top of the alarm
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var s = root.gameObject.AddComponent<TutorialSiren>();
            s.group = root.gameObject.AddComponent<CanvasGroup>();
            s.group.blocksRaycasts = false; s.group.interactable = false;

            s.flash = Img(Stretch(NewRect("DecoSirenFlash", root)), new Color(UIPalette.Foe.r, UIPalette.Foe.g, UIPalette.Foe.b, 0f));
            var top = NewRect("DecoSirenEdgeTop", root);
            top.anchorMin = new Vector2(0f, 1f); top.anchorMax = Vector2.one; top.pivot = new Vector2(.5f, 1f); top.sizeDelta = new Vector2(0f, EdgeH);
            s.edgeTop = Img(top, new Color(.55f, .05f, .09f, 0f));
            var bot = NewRect("DecoSirenEdgeBottom", root);
            bot.anchorMin = Vector2.zero; bot.anchorMax = new Vector2(1f, 0f); bot.pivot = new Vector2(.5f, 0f); bot.sizeDelta = new Vector2(0f, EdgeH);
            s.edgeBottom = Img(bot, new Color(.55f, .05f, .09f, 0f));

            var band = NewRect("SirenBand", root);
            band.anchorMin = new Vector2(0f, .5f); band.anchorMax = new Vector2(1f, .5f); band.pivot = new Vector2(.5f, .5f);
            band.sizeDelta = new Vector2(0f, BandH); band.anchoredPosition = new Vector2(0f, 120f);
            s.band = Img(band, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .88f));
            s.bandRt = band;
            var lineT = NewRect("DecoBandLineTop", band);
            lineT.anchorMin = new Vector2(0f, 1f); lineT.anchorMax = Vector2.one; lineT.pivot = new Vector2(.5f, 1f); lineT.sizeDelta = new Vector2(0f, 3f);
            Img(lineT, UIPalette.Foe);
            var lineB = NewRect("DecoBandLineBottom", band);
            lineB.anchorMin = Vector2.zero; lineB.anchorMax = new Vector2(1f, 0f); lineB.pivot = new Vector2(.5f, 0f); lineB.sizeDelta = new Vector2(0f, 3f);
            Img(lineB, UIPalette.Foe);
            var en = Text(band, "EMERGENCY", 34, UIPalette.Foe, UITheme.TextRole.Display, "Emergency");
            en.characterSpacing = 18f; en.alignment = TextAlignmentOptions.MidlineRight;
            var ert = en.rectTransform; ert.anchorMin = new Vector2(0f, 0f); ert.anchorMax = new Vector2(.5f, 1f); ert.offsetMin = Vector2.zero; ert.offsetMax = new Vector2(-14f, 0f);
            var ja = Text(band, "緊急入電", 30, Color.white, UITheme.TextRole.Title, "Call");
            ja.alignment = TextAlignmentOptions.MidlineLeft;
            var jrt = ja.rectTransform; jrt.anchorMin = new Vector2(.5f, 0f); jrt.anchorMax = new Vector2(1f, 1f); jrt.offsetMin = new Vector2(14f, 0f); jrt.offsetMax = Vector2.zero;

            root.gameObject.SetActive(false);
            return s;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
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
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UIFonts.Apply(t, role);
            return t;
        }
    }
}
