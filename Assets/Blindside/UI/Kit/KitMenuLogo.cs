// The main menu's logo: BLINDSIDE as a glitch, in the title's own look (KitTitleLeft's logo), centred in its rect at `size`.
//   Code      TAC-OS chip (blue, slant 8, 12 display) · TACTICAL BREACH SIMULATION (12 display, #8b939c), centred
//   Logo      BLINDSIDE `size` display (#f1f4f6), one text per letter with a red / blue copy (#ff3e5c / #27b4ff), and two whole-word
//             strips (DecoSlice A / B, RectMask2D) that slide sideways in a glitch
//   LogoBar   10 high, blue 30% · gap 2% · white .75, right edge leaning 12, centred, 0.69 × the word wide
// Two entrances (PlayEnter(heavy)):
//   heavy  (the first time the menu shows in a run) 1.6s: the title's lgIn per letter (.62s steps, 55ms apart) with its jumps ×1.8,
//          and over it a glitch in .05s steps fading out by 1.3s — letters jumping ±14 with an RGB split 6–12 (now and then dimmed to
//          .4), both strips sliding 20–60, the whole word shaking ±8; the code line and the bar come in after .35s / .55s
//   calm   (later shows) everything fades in over .3s
// After either, the calm glitch loops: every 4.2s a .25s burst in .06s steps — a few letters move ±3 with a split 1.5–3.5, now and
// then one thin strip slides 6–12. Every glitch value is a hash of (cycle, step, part): no random stream, a preview at t is repeatable.
// ITimedEffect.Evaluate(t) poses it; Update advances an unscaled clock. With UIMotionSettings.reduceMotion: fades only, no glitch;
// offsets scale with UIMotionSettings.EffectScale. No allocation per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Menu Logo")]
    public class KitMenuLogo : MonoBehaviour, ITimedEffect
    {
        public const string Logo = "BLINDSIDE";
        public const float LetterTime = .62f, LetterStep = .055f, HeavyTime = 1.6f, HeavyGlitchEnd = 1.3f, HeavyStep = .05f, CalmIn = .3f;
        public const float BarDelay = .55f, BarTime = .9f, CodeDelay = .35f, CodeTime = .5f;
        public const float Period = 4.2f, BurstFrom = .90f, BurstTo = .96f, StepTime = .06f;
        public const float CodeH = 21f, Gap = 16f;
        const float Never = -100f;

        static readonly Color LogoC = new Color32(0xf1, 0xf4, 0xf6, 0xff);
        static readonly Color Gray = new Color32(0x8b, 0x93, 0x9c, 0xff);
        static readonly Color BarWhiteC = new Color32(0xC2, 0xC2, 0xC3, 0xff);  // white .75
        static readonly Color GlitchRed = new Color32(0xff, 0x3e, 0x5c, 0xff), GlitchBlue = new Color32(0x27, 0xb4, 0xff, 0xff);
        static readonly Color SliceC = new Color(.945f, .957f, .965f, .9f);

        struct Key
        {
            public float t, x, y, a, split;
            public Key(float t, float x, float y, float a, float split) { this.t = t; this.x = x; this.y = y; this.a = a; this.split = split; }
        }
        static readonly Key[] Keys =
        {
            new Key(0f, 0f, -40f, 0f, 0f),
            new Key(.22f, -8f, 6f, 1f, 6f),
            new Key(.40f, 6f, 0f, 1f, -6f),
            new Key(.58f, -3f, 0f, 1f, 3f),
            new Key(.75f, 0f, 0f, 1f, 0f),
        };

        [System.Serializable]
        public class Letter
        {
            public RectTransform rt;
            public CanvasGroup group;
            public TMP_Text main, red, blue;
            public Vector2 home;
        }

        [Tooltip("The word's size (display font).")]
        public float size = 150f;

        public RectTransform code, logo, bar;
        public CanvasGroup codeGroup, logoGroup;
        public Letter[] letters = new Letter[9];
        public RectMask2D[] slices = new RectMask2D[2];
        public RectTransform[] sliceTexts = new RectTransform[2];
        Vector2 logoHome;

        float clock, enterStart = Never;
        bool heavy;

        public float LogoH => Mathf.Round(size * .947f);
        public float BarY => CodeH + Gap + LogoH + Gap;
        /// <summary>The block's height: code line, gap, word, gap, bar.</summary>
        public float Height => BarY + 10f;
        public float BarW => Mathf.Round(size * 3.47f);
        /// <summary>The entrance's length (heavy: the glitch intro; calm: the fade).</summary>
        public float EnterTime => heavy ? HeavyTime : CalmIn;
        public float Duration => EnterTime;

        // ---- motion ------------------------------------------------------------------------------------------------------------
        /// <summary>The entrance from the start (heavy: the strong glitch intro), then the calm loop.</summary>
        public void PlayEnter(bool heavyIntro)
        {
            Build();
            heavy = heavyIntro;
            clock = 0f; enterStart = 0f;
            Evaluate(0f);
        }

        /// <summary>Previews (edit mode): `t` seconds after an entrance began.</summary>
        public void PoseAt(float t, bool heavyIntro)
        {
            Build();
            heavy = heavyIntro;
            clock = t; enterStart = 0f;
            Evaluate(t);
        }

        void Update()
        {
            if (enterStart <= Never) return;
            clock += Time.unscaledDeltaTime;
            float keep = EnterTime + Period * 200f;
            if (clock > keep) clock -= Period * 200f;   // a whole number of loop periods: the loop looks the same
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            if (logo == null) return;
            bool reduce = UIMotionSettings.reduceMotion;
            bool entering = enterStart > Never;
            float s = entering ? t - enterStart : 1000f;
            float fx = UIMotionSettings.EffectScale;
            bool heavyNow = entering && heavy && s < HeavyTime;

            // fades: the code line, the whole word (calm entrance), the bar
            float ce, we, be;
            if (heavyNow || (entering && heavy && s < HeavyTime + 1f))
            {
                float cp = s - CodeDelay, bp = s - BarDelay;
                ce = cp <= 0f ? 0f : reduce ? 1f : UIEase.Tactical(cp / CodeTime);
                be = bp <= 0f ? 0f : reduce ? 1f : UIEase.Tactical(bp / BarTime);
                we = 1f;
            }
            else
            {
                float k = !entering || heavy ? 1f : Mathf.Clamp01(s / CalmIn);
                ce = k; we = k; be = 1f;
            }
            if (codeGroup.alpha != ce) codeGroup.alpha = ce;
            if (logoGroup.alpha != we) logoGroup.alpha = we;
            float bw = BarW * be;
            if (bar.sizeDelta.x != bw) bar.sizeDelta = new Vector2(bw, 10f);

            // the glitch: heavy intro or the calm loop
            bool burst = false; bool strong = false; float power = 0f; uint cycle = 0, step = 0;
            if (!reduce)
            {
                if (heavyNow)
                {
                    if (s < HeavyGlitchEnd) { burst = true; strong = true; power = 1f - s / HeavyGlitchEnd; step = (uint)Mathf.FloorToInt(s / HeavyStep); cycle = 9999u; }
                }
                else
                {
                    float it = entering ? s - EnterTime : t;
                    if (it < 0f) it = 0f;
                    cycle = (uint)Mathf.FloorToInt(it / Period);
                    float p = it - cycle * Period;
                    float from = Period * BurstFrom, to = Period * BurstTo;
                    if (p >= from && p < to) { burst = true; step = (uint)Mathf.FloorToInt((p - from) / StepTime); }
                }
            }

            // the whole word's shake (heavy only)
            var lp = logoHome;
            if (strong) lp += new Vector2(Mathf.Round((Hash01(cycle, step, 40u, 0u) - .5f) * 16f * power * fx), 0f);
            if (logo.anchoredPosition != lp) logo.anchoredPosition = lp;

            float jump = heavyNow && !reduce ? 1.8f : 1f;
            for (int i = 0; i < letters.Length; i++)
            {
                var l = letters[i];
                float a = 1f, split = 0f; Vector2 off = Vector2.zero;
                // the letter's lgIn (heavy intro only)
                if (heavyNow)
                {
                    float f = (s - i * LetterStep) / LetterTime;
                    if (f < 1f)
                    {
                        int k = 0;
                        for (int j = Keys.Length - 1; j >= 0; j--) if (f >= Keys[j].t) { k = j; break; }
                        var key = Keys[k];
                        a = reduce ? (f >= Keys[1].t ? 1f : 0f) : key.a;
                        if (!reduce) { split = key.split * jump * fx; off = new Vector2(key.x, key.y) * jump * fx; }
                    }
                }
                if (burst && a > 0f)
                {
                    float h0 = Hash01(cycle, step, (uint)i, 0u), h1 = Hash01(cycle, step, (uint)i, 1u), h2 = Hash01(cycle, step, (uint)i, 2u);
                    if (strong)
                    {
                        if (h0 < .7f * power + .1f)
                        {
                            off.x += Mathf.Round((h1 - .5f) * 28f * power * fx);
                            split = (6f + 6f * h2) * (h1 < .5f ? -1f : 1f) * Mathf.Max(.3f, power) * fx;
                            if (h2 < .2f) a = Mathf.Min(a, .4f);
                        }
                    }
                    else if (h0 < .3f)
                    {
                        off.x += Mathf.Round((h1 - .5f) * 6f * fx);
                        split = (1.5f + 2f * h2) * (h1 < .5f ? -1f : 1f) * fx;
                    }
                }

                if (l.group.alpha != a) l.group.alpha = a;
                var pos = l.home + off;
                if (l.rt.anchoredPosition != pos) l.rt.anchoredPosition = pos;
                bool on = split != 0f;
                if (l.red.enabled != on) { l.red.enabled = on; l.blue.enabled = on; }
                if (on)
                {
                    var rp = new Vector2(-split, 0f);
                    if (l.red.rectTransform.anchoredPosition != rp) { l.red.rectTransform.anchoredPosition = rp; l.blue.rectTransform.anchoredPosition = -rp; }
                }
            }

            // the sliding strips
            float lh = LogoH;
            for (int i = 0; i < 2; i++)
            {
                bool show = false; float top = 0f, bottom = 0f, x = 0f;
                if (burst)
                {
                    float h0 = Hash01(cycle, step, 20u + (uint)i, 0u), h1 = Hash01(cycle, step, 20u + (uint)i, 1u), h2 = Hash01(cycle, step, 20u + (uint)i, 2u);
                    float band;
                    if (strong) { show = h0 < .85f; band = lh * (.08f + .22f * h1); x = (20f + 40f * h1) * (h0 < .42f ? -1f : 1f) * Mathf.Max(.35f, power); }
                    else { show = i == 0 && h0 < .45f; band = lh * (.04f + .06f * h1); x = (6f + 6f * h1) * (h0 < .22f ? -1f : 1f); }
                    top = Mathf.Round((lh - band) * h2);
                    bottom = Mathf.Max(0f, lh - top - band);
                    x = Mathf.Round(x * fx);
                }
                var go = slices[i].gameObject;
                if (go.activeSelf != show) go.SetActive(show);
                if (!show) continue;
                var pad = new Vector4(0f, bottom, 0f, top);
                if (slices[i].padding != pad) slices[i].padding = pad;
                var sp = new Vector2(x, 0f);
                if (sliceTexts[i].anchoredPosition != sp) sliceTexts[i].anchoredPosition = sp;
            }
        }

        // a repeatable 0–1 from four integers (no allocation, no shared random stream)
        static float Hash01(uint a, uint b, uint c, uint d)
        {
            uint h = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA77u ^ (c + 0x165667B1u) * 0xC2B2AE3Du ^ (d + 0x27D4EB2Fu) * 0x27D4EB2Du;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        /// <summary>Builds the logo on this object (once), centred in its rect (place the object first: its width centres the rows).</summary>
        public void Build()
        {
            if (logo != null) return;
            var theme = UITheme.Current;
            var root = (RectTransform)transform;
            float w = root.rect.width > 1f ? root.rect.width : 1000f;
            float lh = LogoH;

            // the code line, centred
            code = HudKit.NewRect("Code", root);
            UILayout.Place(code, 0f, 0f, w, CodeH);
            codeGroup = code.gameObject.AddComponent<CanvasGroup>();
            codeGroup.blocksRaycasts = false;
            var tac = Chip.Create(code, "Tac", "TAC-OS", KitColor.Blue, KitColor.White, 12f, 8f);
            tac.padX = 10f; tac.padY = 3f;
            UIFonts.Apply(tac.label, UITheme.TextRole.Display);
            tac.label.characterSpacing = 12f;
            tac.Fit();
            var tr = (RectTransform)tac.transform;
            var mid = HudKit.AddText(code, "TACTICAL BREACH SIMULATION", 12f, Gray, true, TextAlignmentOptions.MidlineLeft, "Mid", UITheme.TextRole.Display);
            mid.characterSpacing = 12f;
            mid.textWrappingMode = TextWrappingModes.NoWrap;
            float mw = Mathf.Ceil(mid.GetPreferredValues(mid.text).x) + 1f;
            float cx = Mathf.Round((w - (tr.sizeDelta.x + 10f + mw)) * .5f);
            UILayout.Place(tr, cx, (CodeH - tr.sizeDelta.y) * .5f, tr.sizeDelta.x, tr.sizeDelta.y);
            UILayout.Place(mid.rectTransform, cx + tr.sizeDelta.x + 10f, 0f, mw, CodeH);

            // the word: one text per letter at its place, centred
            logo = HudKit.NewRect("Logo", root);
            UILayout.Place(logo, 0f, CodeH + Gap, w, lh);
            logoHome = logo.anchoredPosition;
            logoGroup = logo.gameObject.AddComponent<CanvasGroup>();
            logoGroup.blocksRaycasts = false;
            var measure = HudKit.AddText(logo, Logo, size, LogoC, true, TextAlignmentOptions.TopLeft, "Measure", UITheme.TextRole.Display);
            measure.characterSpacing = 2f;
            measure.textWrappingMode = TextWrappingModes.NoWrap;
            float wordW = Mathf.Ceil(measure.GetPreferredValues(Logo).x);
            float x0 = Mathf.Round((w - wordW) * .5f);
            for (int i = 0; i < Logo.Length; i++)
            {
                float x = i == 0 ? 0f : Mathf.Round(measure.GetPreferredValues(Logo.Substring(0, i)).x + size * .02f);
                letters[i] = BuildLetter(logo, i, x0 + x, lh);
            }
            HudKit.DestroySafe(measure.gameObject);

            // the sliding strips: a whole copy of the word each, cut to a band by its RectMask2D
            for (int i = 0; i < 2; i++)
            {
                var sm = HudKit.NewRect(i == 0 ? "DecoSliceA" : "DecoSliceB", logo);
                UILayout.Place(sm, x0 - 80f, 0f, wordW + 160f, lh);
                slices[i] = sm.gameObject.AddComponent<RectMask2D>();
                var st = HudKit.AddText(sm, Logo, size, SliceC, true, TextAlignmentOptions.TopLeft, "DecoSliceText", UITheme.TextRole.Display);
                st.characterSpacing = 2f;
                st.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.LineHeight(st, .95f);
                UILayout.Place(st.rectTransform, 80f, 0f, wordW + 40f, lh);
                sliceTexts[i] = st.rectTransform;
                sm.gameObject.SetActive(false);
            }

            // the bar, centred: blue 0–30%, clear 30–32%, white .75 32–100%
            bar = HudKit.NewRect("LogoBar", root);
            bar.anchorMin = bar.anchorMax = new Vector2(.5f, 1f); bar.pivot = new Vector2(.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -BarY); bar.sizeDelta = new Vector2(BarW, 10f);
            HudKit.AddImage(bar, Color.white);
            bar.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.RightEdge, 12f);
            bar.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            BarPart("Blue", 0f, .3f, theme.blue);
            BarPart("White", .32f, 1f, BarWhiteC);

            enterStart = Never;
            Evaluate(0f);
        }

        void BarPart(string name, float from, float to, Color c)
        {
            var rt = HudKit.NewRect(name, bar);
            rt.anchorMin = new Vector2(from, 0f); rt.anchorMax = new Vector2(to, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            HudKit.AddImage(rt, c);
        }

        Letter BuildLetter(RectTransform parent, int i, float x, float lh)
        {
            var l = new Letter();
            l.rt = HudKit.NewRect("L" + i, parent);
            UILayout.Place(l.rt, x, 0f, size, lh);
            l.home = l.rt.anchoredPosition;
            l.group = l.rt.gameObject.AddComponent<CanvasGroup>();
            l.group.blocksRaycasts = false;
            string ch = Logo.Substring(i, 1);
            l.red = LetterText(l.rt, ch, "Red", GlitchRed);
            l.blue = LetterText(l.rt, ch, "Blue", GlitchBlue);
            l.main = LetterText(l.rt, ch, "Letter", LogoC);
            l.red.enabled = false; l.blue.enabled = false;
            l.red.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the logo letter's red copy (glitch split), offset on purpose";
            l.blue.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the logo letter's blue copy (glitch split), offset on purpose";
            return l;
        }

        TMP_Text LetterText(RectTransform parent, string ch, string name, Color c)
        {
            var t = HudKit.AddText(parent, ch, size, c, true, TextAlignmentOptions.TopLeft, name, UITheme.TextRole.Display);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.LineHeight(t, .95f);
            HudKit.Stretch(t.rectTransform);
            t.gameObject.AddComponent<LintIgnoreClip>().reason = "one letter of the logo in a size-wide slot; the glyph's own advance decides the word's spacing";
            return t;
        }
    }
}
