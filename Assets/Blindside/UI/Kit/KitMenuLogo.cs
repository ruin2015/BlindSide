// The main menu's centred logo: BLINDSIDE as a glitch, in the title's own look (KitTitleLeft's logo, larger and centred).
//   Code      TAC-OS chip (blue, slant 8, 12 display) · TACTICAL BREACH SIMULATION (12 display, #8b939c), centred; fades in .5s after .35s
//   Logo      BLINDSIDE 150 display (#f1f4f6), one text per letter with a red / blue copy (#ff3e5c / #27b4ff): the title's lgIn
//             (.62s steps, letter i after i × 55ms: hidden to 22%, (−8, up 6) split ±6, (+6) the other way, (−3) ±3, settled from 75%)
//   LogoBar   10 high, blue 30% · gap 2% · white .75, right edge leaning 12, centred; grows 0 → 520 in .9s after .55s
//   glitch    after the entrance, every 3.6s a burst of .43s (82%–94% of the period) in .06s steps: letters jump sideways (±6) with an
//             RGB split (3–8), now and then one dims to .55, and two strips of the word (DecoSlice A / B, RectMask2D) slide 10–26 off;
//             all from a hash of (cycle, step, letter) — no random stream, so a preview at a given t is always the same
// ITimedEffect.Evaluate(t) poses everything at t (the entrance, then the loop); Update advances an unscaled clock. With
// UIMotionSettings.reduceMotion the entrance only fades and the bursts are off; offsets scale with UIMotionSettings.EffectScale.
// No allocation per frame (hash maths only; texts never change).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Menu Logo")]
    public class KitMenuLogo : MonoBehaviour, ITimedEffect
    {
        public const string Logo = "BLINDSIDE";
        public const float LogoSize = 150f, LogoH = 142f, LetterTime = .62f, LetterStep = .055f;
        public const float BarW = 520f, BarDelay = .55f, BarTime = .9f, CodeDelay = .35f, CodeTime = .5f;
        public const float Period = 3.6f, BurstFrom = .82f, BurstTo = .94f, StepTime = .06f;
        // rows: code 21, 16 gap, logo 142, 16 gap, bar 10
        public const float CodeH = 21f, LogoY = 37f, BarY = 195f, Height = 205f;
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

        public RectTransform code, logo, bar;
        public CanvasGroup codeGroup;
        public Letter[] letters = new Letter[9];
        public RectMask2D[] slices = new RectMask2D[2];
        public RectTransform[] sliceTexts = new RectTransform[2];

        float clock, enterStart = Never;

        /// <summary>The entrance's length (the last letter, the bar and the code line all settled).</summary>
        public float EnterTime => Mathf.Max((Logo.Length - 1) * LetterStep + LetterTime, Mathf.Max(BarDelay + BarTime, CodeDelay + CodeTime));
        public float Duration => EnterTime;

        // ---- motion ------------------------------------------------------------------------------------------------------------
        void OnEnable()
        {
            Build();
            if (Application.isPlaying) PlayEnter();
        }

        /// <summary>The entrance from the start (then the glitch loop runs on).</summary>
        public void PlayEnter()
        {
            Build();
            clock = 0f; enterStart = 0f;
            Evaluate(0f);
        }

        /// <summary>Previews (edit mode): the logo `t` seconds after its entrance began.</summary>
        public void PoseAt(float t)
        {
            Build();
            clock = t; enterStart = 0f;
            Evaluate(t);
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            // keep the clock small on a menu left open (a whole number of loop periods after the entrance)
            float keep = EnterTime + Period * 200f;
            if (clock > keep) clock -= Period * 200f;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            if (logo == null) return;
            bool reduce = UIMotionSettings.reduceMotion;
            bool entering = enterStart > Never;
            float s = t - enterStart;
            float fx = UIMotionSettings.EffectScale;

            // the code line (fadeUp)
            float cp = s - CodeDelay;
            float ce = !entering ? 1f : cp <= 0f ? 0f : reduce ? 1f : UIEase.Tactical(cp / CodeTime);
            if (codeGroup.alpha != ce) codeGroup.alpha = ce;

            // the burst window of the loop (none during the entrance or with reduce motion)
            float idle = s - EnterTime;
            bool burst = false; uint cycle = 0, step = 0;
            if (!reduce && (!entering || idle >= 0f))
            {
                float it = entering ? idle : t;
                if (it < 0f) it = 0f;
                cycle = (uint)Mathf.FloorToInt(it / Period);
                float p = it - cycle * Period;
                float from = Period * BurstFrom, to = Period * BurstTo;
                if (p >= from && p < to) { burst = true; step = (uint)Mathf.FloorToInt((p - from) / StepTime); }
            }

            for (int i = 0; i < letters.Length; i++)
            {
                var l = letters[i];
                float a; float split; Vector2 off;
                float f = !entering ? 1f : (s - i * LetterStep) / LetterTime;
                if (f < 1f)
                {
                    int k = 0;
                    for (int j = Keys.Length - 1; j >= 0; j--) if (f >= Keys[j].t) { k = j; break; }
                    var key = Keys[k];
                    a = reduce ? (f >= Keys[1].t ? 1f : 0f) : key.a;
                    split = reduce ? 0f : key.split * fx;
                    off = reduce ? Vector2.zero : new Vector2(key.x, key.y) * fx;
                }
                else if (burst)
                {
                    float h0 = Hash01(cycle, step, (uint)i, 0u), h1 = Hash01(cycle, step, (uint)i, 1u), h2 = Hash01(cycle, step, (uint)i, 2u);
                    bool hit = h0 < .55f;                       // about half the letters move in a step
                    off = hit ? new Vector2(Mathf.Round((h1 - .5f) * 12f) * fx, 0f) : Vector2.zero;
                    split = hit ? (3f + 5f * h2) * (h1 < .5f ? -1f : 1f) * fx : 0f;
                    a = hit && h2 < .15f ? .55f : 1f;
                }
                else { a = 1f; split = 0f; off = Vector2.zero; }

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

            // the two sliding strips of the word
            for (int i = 0; i < 2; i++)
            {
                bool show = false; float top = 0f, bottom = 0f, x = 0f;
                if (burst)
                {
                    float h0 = Hash01(cycle, step, 20u + (uint)i, 0u), h1 = Hash01(cycle, step, 20u + (uint)i, 1u), h2 = Hash01(cycle, step, 20u + (uint)i, 2u);
                    show = h0 < .7f;
                    float band = LogoH * (.08f + .18f * h1);
                    top = Mathf.Round((LogoH - band) * h2);
                    bottom = Mathf.Max(0f, LogoH - top - band);
                    x = Mathf.Round((10f + 16f * h1) * (h0 < .35f ? -1f : 1f) * fx);
                }
                var go = slices[i].gameObject;
                if (go.activeSelf != show) go.SetActive(show);
                if (!show) continue;
                var pad = new Vector4(0f, bottom, 0f, top);
                if (slices[i].padding != pad) slices[i].padding = pad;
                var sp = new Vector2(x, 0f);
                if (sliceTexts[i].anchoredPosition != sp) sliceTexts[i].anchoredPosition = sp;
            }

            // the bar (growX)
            float bp = s - BarDelay;
            float bw = !entering ? BarW : bp <= 0f ? 0f : reduce ? BarW : BarW * UIEase.Tactical(bp / BarTime);
            if (bar.sizeDelta.x != bw) bar.sizeDelta = new Vector2(bw, 10f);
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
            UILayout.Place(logo, 0f, LogoY, w, LogoH);
            var measure = HudKit.AddText(logo, Logo, LogoSize, LogoC, true, TextAlignmentOptions.TopLeft, "Measure", UITheme.TextRole.Display);
            measure.characterSpacing = 2f;
            measure.textWrappingMode = TextWrappingModes.NoWrap;
            float wordW = Mathf.Ceil(measure.GetPreferredValues(Logo).x);
            float x0 = Mathf.Round((w - wordW) * .5f);
            for (int i = 0; i < Logo.Length; i++)
            {
                float x = i == 0 ? 0f : Mathf.Round(measure.GetPreferredValues(Logo.Substring(0, i)).x + LogoSize * .02f);
                letters[i] = BuildLetter(logo, i, x0 + x);
            }
            HudKit.DestroySafe(measure.gameObject);

            // the sliding strips: a whole copy of the word each, cut to a band by its RectMask2D
            for (int i = 0; i < 2; i++)
            {
                var sm = HudKit.NewRect(i == 0 ? "DecoSliceA" : "DecoSliceB", logo);
                UILayout.Place(sm, x0 - 40f, 0f, wordW + 80f, LogoH);
                slices[i] = sm.gameObject.AddComponent<RectMask2D>();
                var st = HudKit.AddText(sm, Logo, LogoSize, SliceC, true, TextAlignmentOptions.TopLeft, "DecoSliceText", UITheme.TextRole.Display);
                st.characterSpacing = 2f;
                st.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.LineHeight(st, .95f);
                UILayout.Place(st.rectTransform, 40f, 0f, wordW + 40f, LogoH);
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

        Letter BuildLetter(RectTransform parent, int i, float x)
        {
            var l = new Letter();
            l.rt = HudKit.NewRect("L" + i, parent);
            UILayout.Place(l.rt, x, 0f, LogoSize, LogoH);
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

        static TMP_Text LetterText(RectTransform parent, string ch, string name, Color c)
        {
            var t = HudKit.AddText(parent, ch, LogoSize, c, true, TextAlignmentOptions.TopLeft, name, UITheme.TextRole.Display);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.LineHeight(t, .95f);
            HudKit.Stretch(t.rectTransform);
            t.gameObject.AddComponent<LintIgnoreClip>().reason = "one letter of the logo in a 150-wide slot; the glyph's own advance decides the word's spacing";
            return t;
        }
    }
}
