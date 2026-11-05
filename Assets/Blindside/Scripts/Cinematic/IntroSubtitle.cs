// The intro's spoken lines, bottom centre (replacing the old bottom-left caption block): a small "HQ //" speaker tag in the
// display font over the line in the body font. One line = one run of the clock (ITimedEffect.Evaluate(t)):
//   0 – FadeIn      the block rises RiseFrom px into place and fades in
//   from 0          the characters type on at CharsPerSecond (TMP maxVisibleCharacters — no allocation)
//   last FadeOut s  it fades out
//   voice gauge     GaugeBars level bars right of the speaker tag dance (noise × a syllable pulse, taller in the middle) while
//                   the line is typing — the voice — and sink to a flat idle GaugeSettle s after it
// Play only sets the text and restarts the unscaled clock; nothing allocates per frame.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public class IntroSubtitle : MonoBehaviour, ITimedEffect
    {
        public const float FadeIn = .28f, FadeOut = .4f, CharsPerSecond = 26f, RiseFrom = 10f, BaseY = 54f;
        public const int GaugeBars = 16;
        public const float BarW = 3f, BarGap = 2f, BarMin = 2f, BarMax = 20f, GaugeY = 42f, GaugeSettle = .3f;

        public RectTransform block;
        public CanvasGroup group;
        public TMP_Text speaker, line;
        public RectTransform[] bars;

        float clock, duration = 4f;
        bool running;
        int chars;

        public float Duration => duration;

        /// <summary>Shows `text` for `seconds` (fading out at the end).</summary>
        public void Play(string text, float seconds)
        {
            line.text = text;
            line.maxVisibleCharacters = 99999;
            line.ForceMeshUpdate();
            chars = line.textInfo.characterCount;
            duration = Mathf.Max(seconds, FadeIn + FadeOut + .2f);
            clock = 0f; running = true;
            Evaluate(0f);
        }

        public void Stop() { running = false; Evaluate(duration); }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= duration) running = false;
        }

        public void Evaluate(float t)
        {
            float a = t >= duration ? 0f : Mathf.Min(UIEase.Tactical(t / FadeIn), Mathf.Clamp01((duration - t) / FadeOut));
            if (group.alpha != a) group.alpha = a;
            block.anchoredPosition = new Vector2(0f, BaseY - RiseFrom * (1f - UIEase.Tactical(t / FadeIn)));
            int shown = Mathf.Clamp(Mathf.FloorToInt(t * CharsPerSecond), 0, chars);
            if (line.maxVisibleCharacters != shown) line.maxVisibleCharacters = shown;

            // the voice: loud while typing, then down to idle
            float speakEnd = chars / CharsPerSecond;
            float voice = 1f - Mathf.Clamp01((t - speakEnd) / GaugeSettle);
            float pulse = .55f + .45f * Mathf.Abs(Mathf.Sin(t * 9.5f)) * (.7f + .3f * Mathf.Sin(t * 3.1f));
            float mid = (bars.Length - 1) * .5f;
            for (int i = 0; i < bars.Length; i++)
            {
                float shape = 1f - .55f * Mathf.Abs(i - mid) / mid;
                float n = Mathf.PerlinNoise(t * 11f + i * .53f, i * 1.7f);
                float h = Mathf.Lerp(BarMin, BarMax, Mathf.Clamp01((n - .2f) * pulse * shape * 2f) * voice);
                bars[i].sizeDelta = new Vector2(BarW, h);
            }
        }

        // ================================================================
        //  Construction (runtime, under the intro's canvas)
        // ================================================================
        public static IntroSubtitle Build(Transform canvas)
        {
            var root = new GameObject("IntroSubtitle", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvas, false);
            root.anchorMin = root.anchorMax = new Vector2(.5f, 0f);
            root.pivot = new Vector2(.5f, 0f);
            root.sizeDelta = new Vector2(1100f, 70f);
            var sub = root.gameObject.AddComponent<IntroSubtitle>();
            sub.block = root;
            sub.group = root.gameObject.AddComponent<CanvasGroup>();
            sub.group.alpha = 0f; sub.group.blocksRaycasts = false; sub.group.interactable = false;

            // a soft dark band behind the words, so they read over the bright sky and over the hologram alike
            var band = new GameObject("DecoSubtitleBand", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            band.SetParent(root, false);
            band.anchorMin = new Vector2(.5f, 0f); band.anchorMax = new Vector2(.5f, 0f); band.pivot = new Vector2(.5f, 0f);
            band.sizeDelta = new Vector2(940f, 74f); band.anchoredPosition = new Vector2(0f, -8f);
            var bi = band.GetComponent<Image>(); bi.color = new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .55f); bi.raycastTarget = false;

            // speaker tag left of the centre line, the voice gauge right of it
            sub.speaker = Text(root, "HQ //", 14f, UIPalette.Accent, 40f, UITheme.TextRole.Display);
            sub.speaker.characterSpacing = 12f;
            var sr = sub.speaker.rectTransform;
            sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(.5f, 0f); sr.offsetMax = new Vector2(-8f, sr.offsetMax.y);
            sub.speaker.alignment = TextAlignmentOptions.BottomRight;
            sub.bars = new RectTransform[GaugeBars];
            for (int i = 0; i < GaugeBars; i++)
            {
                var bar = new GameObject("DecoVoiceBar" + i, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                bar.SetParent(root, false);
                bar.anchorMin = bar.anchorMax = new Vector2(.5f, 0f); bar.pivot = new Vector2(0f, 0f);
                bar.anchoredPosition = new Vector2(4f + i * (BarW + BarGap), GaugeY);
                bar.sizeDelta = new Vector2(BarW, BarMin);
                var img = bar.GetComponent<Image>(); img.raycastTarget = false;
                img.color = new Color(UIPalette.Accent.r, UIPalette.Accent.g, UIPalette.Accent.b, i % 4 == 3 ? .55f : .9f);
                sub.bars[i] = bar;
            }
            sub.line = Text(root, "", 26f, Color.white, 4f, UITheme.TextRole.Body);
            sub.line.textWrappingMode = TextWrappingModes.NoWrap;
            return sub;
        }

        static TMP_Text Text(RectTransform parent, string s, float size, Color c, float y, UITheme.TextRole role)
        {
            var go = new GameObject(role == UITheme.TextRole.Display ? "Speaker" : "Line", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(.5f, 0f);
            rt.sizeDelta = new Vector2(0f, size * 1.5f); rt.anchoredPosition = new Vector2(0f, y);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Bottom; t.raycastTarget = false;
            UIFonts.Apply(t, role);
            return t;
        }
    }
}
