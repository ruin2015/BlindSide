// The title's left column and its background (#title .tl, .ghost, .scanband, .diag — ui_spec "타이틀 크기"), built from the kit on a
// full-screen root over the 1440×810 title canvas:
//   DecoGhost     BLINDSIDE 300 display, white .028 (alpha matched to sRGB over the title background), left −30 / bottom −60
//   DecoScanBand  a blue band 140 high (transparent → .07 → transparent) running top → bottom every 8s; hidden while UIScreenFX draws the
//                 same band over the whole screen (its light band is the mockup's .scanband)
//   DecoDiag      hazard stripes 560×12 at the bottom left (acc .85, 14 / 28, scrolling 1.6s), only the right edge leaning 20
//   Column        x64 y92, 660 wide (.tl 600 + the help line's 660):
//     Code        TAC-OS chip (blue, slant 8, 12 display) · TACTICAL BREACH SIMULATION (12 display, #8b939c) · BUILD 0.6 (1px white .2
//                 frame); fadeUp .5s
//     Logo        BLINDSIDE 118 display (#f1f4f6), one text per letter (lgIn .62s steps, letter i after i × 55ms: hidden to 22%, then
//                 in place with jumps — (−8, up 6) red −6 / blue +6, (+6) the split the other way, (−3) ±3, settled from 75%)
//     LogoBar     10 high, blue 30% · gap 2% · white .75, right edge leaning 12; grows 0 → 400 in .9s after .55s
//     Sub         two lines 16 (body, #c7ced6, line 1.75); fadeUp .6s after .5s
//     Meta        SECTOR / HOSTAGE / HOSTILES / TIME LIMIT: label 11 display, value 26 display, white .06 box with a 3px white .6 top line,
//                 right edge leaning 12, 10 apart; fadeUp .6s after .65s. Values from the title: the selected map (SECTOR: SUB-07 for the
//                 built-in house, else its file name when it is short Latin, else CUSTOM; HOSTAGE 01 / 00 by its hostage), the other side's
//                 squad size (attack: the map's defenders, defense: the attack roster), TurnManager.MaxRound.
//     Help        one line 12.5 (body, #9aa3ad, 操作 white) at y676 (768 on the canvas), 3px blue line on its left; fadeUp .6s after .8s
// Bind hooks TitleSelectScreen.Changed (the values follow the side and the map). Every motion is on one clock (Evaluate(t)); Update
// advances it with unscaled time (the stripes and the band always move). Nothing is allocated per frame (texts change only on Changed).
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.MapEditor;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Title Left")]
    public class KitTitleLeft : MonoBehaviour, ITimedEffect
    {
        public const float ScreenW = 1440f, ScreenH = 810f, X = 64f, Y = 92f, W = 660f, H = 700f;
        public const string Logo = "BLINDSIDE";
        public const float LogoSize = 118f, LetterTime = .62f, LetterStep = .055f, BarW = 400f, BarDelay = .55f, BarTime = .9f;
        public const float Rise = 18f, BandH = 140f, BandPeriod = 8f;
        const float Never = -100f;
        // the mockup's blocks: y and height in the column (.tcode 21, .logo mt16 112, .lbar mt16 10, .tsub mt16 56, .meta mt18 65, .help top 676)
        const float CodeY = 0f, CodeH = 21f, LogoY = 37f, LogoH = 112f, BarY = 165f, SubY = 191f, SubH = 56f, MetaY = 265f, MetaH = 65f;
        const float HelpY = 676f, HelpH = 22.75f;
        static readonly float[] FadeDelay = { 0f, .5f, .65f, .8f }, FadeTime = { .5f, .6f, .6f, .6f };   // code, sub, meta, help

        static readonly Color Bg = new Color32(0x0a, 0x0c, 0x0f, 0xff);
        static readonly Color LogoC = new Color32(0xf1, 0xf4, 0xf6, 0xff);
        static readonly Color Gray = new Color32(0x8b, 0x93, 0x9c, 0xff);
        static readonly Color SubC = new Color32(0xc7, 0xce, 0xd6, 0xff);
        static readonly Color HelpC = new Color32(0x9a, 0xa3, 0xad, 0xff);
        static readonly Color FrameC = new Color32(0x3B, 0x3D, 0x3F, 0xff);     // white .2 over the background
        static readonly Color MetaBoxC = new Color32(0x19, 0x1B, 0x1D, 0xff);   // white .06
        static readonly Color MetaLineC = new Color32(0x9D, 0x9E, 0x9F, 0xff);  // white .6
        static readonly Color BarWhiteC = new Color32(0xC2, 0xC2, 0xC3, 0xff);  // white .75
        static readonly Color GlitchRed = new Color32(0xff, 0x3e, 0x5c, 0xff), GlitchBlue = new Color32(0x27, 0xb4, 0xff, 0xff);

        // lgIn as steps: from each key's time (fraction of .62s) to the next — offset (x, y up), alpha, split (red −, blue +)
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

        public TitleSelectScreen screen;
        public RectTransform column, bar, band;
        public RectTransform[] fades = new RectTransform[4];
        public CanvasGroup[] fadeGroups = new CanvasGroup[4];
        public Letter[] letters = new Letter[9];
        public HazardStripe diag;
        public Image bandImage;
        public RectTransform[] metaBoxes = new RectTransform[4];
        public TMP_Text[] metaValues = new TMP_Text[4], metaLabels = new TMP_Text[4];

        float clock, enterStart = Never;
        bool bound;
        string sector, hostage, hostiles, rounds;

        public float Duration => Mathf.Max(enterStart + (letters.Length - 1) * LetterStep + LetterTime,
            Mathf.Max(enterStart + BarDelay + BarTime, enterStart + FadeDelay[3] + FadeTime[3]));

        // ---- the title's state -----------------------------------------------------------------------------------------------
        public void Bind(TitleSelectScreen title)
        {
            Build();
            if (bound && screen == title) { Redraw(); return; }
            if (screen != null) screen.Changed -= Redraw;
            screen = title;
            screen.Changed += Redraw;
            bound = true;
            Redraw();
        }

        void OnDestroy()
        {
            if (screen != null) screen.Changed -= Redraw;
        }

        /// <summary>The meta values from the title's map and side (texts and the boxes' widths change only when a value does).</summary>
        public void Redraw()
        {
            if (screen == null) return;
            var map = screen.SelectedMap;
            string sec = "SUB-07", hos = "01", hst = "05";
            if (map != null && !map.IsDefault) sec = SectorName(map.stem);
            if (map != null && map.file != null && !map.IsDefault) hos = map.file.hasHostage ? "01" : "00";
            int enemies = screen.SelectedTeam == OperatorTeam.Attack
                ? (map != null && map.file != null && map.file.defenders != null ? map.file.defenders.Length : Blindside.Map.MapData.DefenderKeys.Length)
                : Blindside.Integration.MatchBootstrap.AttackerKeys.Length;
            hst = enemies.ToString("00");
            string rnd = Blindside.Turns.TurnManager.MaxRound + "R";
            if (sec == sector && hos == hostage && hst == hostiles && rnd == rounds) return;
            sector = sec; hostage = hos; hostiles = hst; rounds = rnd;
            metaValues[0].text = sec; metaValues[1].text = hos; metaValues[2].text = hst; metaValues[3].text = rnd;
            LayoutMeta();
        }

        // a saved map's file name when it reads as a sector code (short, Latin letters / digits / - _), else CUSTOM
        static string SectorName(string stem)
        {
            if (string.IsNullOrEmpty(stem) || stem.Length > 10) return "CUSTOM";
            foreach (char c in stem)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_')) return "CUSTOM";
            return stem.ToUpperInvariant();
        }

        // .meta div{padding:8px 22px 8px 16px}: each box as wide as its label or value, 10 apart
        void LayoutMeta()
        {
            float x = 0f;
            for (int i = 0; i < 4; i++)
            {
                float w = Mathf.Ceil(Mathf.Max(metaLabels[i].GetPreferredValues(metaLabels[i].text).x, metaValues[i].GetPreferredValues(metaValues[i].text).x)) + 1f;
                float bw = 16f + w + 22f;
                UILayout.Place(metaBoxes[i], x, 0f, bw, MetaH);
                UILayout.Place(metaLabels[i].rectTransform, 16f, 3f + 8f, w, 13.75f);
                UILayout.Place(metaValues[i].rectTransform, 16f, 3f + 8f + 13.75f, w, 32.5f);
                x += bw + 10f;
            }
        }

        // ---- motion ------------------------------------------------------------------------------------------------------------
        void OnEnable()
        {
            Build();
            if (Application.isPlaying) PlayEnter();
        }

        /// <summary>The column's entrance, as when the title opens.</summary>
        public void PlayEnter()
        {
            clock = 0f; enterStart = 0f;
            Evaluate(0f);
        }

        /// <summary>Poses the column `t` seconds into its entrance (previews: edit mode, no clock).</summary>
        public void PoseEnter(float t)
        {
            Build();
            clock = t; enterStart = 0f;
            Evaluate(t);
        }

        public void Evaluate(float t)
        {
            if (column == null) return;
            bool reduce = UIMotionSettings.reduceMotion;
            bool entering = enterStart > Never;

            for (int i = 0; i < 4; i++)
            {
                float p = t - enterStart - FadeDelay[i];
                float e = !entering ? 1f : p <= 0f ? 0f : reduce ? 1f : UIEase.Tactical(p / FadeTime[i]);
                if (fadeGroups[i].alpha != e) fadeGroups[i].alpha = e;
                var pos = new Vector2(0f, -Rise * (1f - e));
                var body = fades[i];
                if (body.anchoredPosition != pos) body.anchoredPosition = pos;
            }

            for (int i = 0; i < letters.Length; i++)
            {
                var l = letters[i];
                float f = !entering ? 1f : (t - enterStart - i * LetterStep) / LetterTime;
                int k = 0;
                if (f >= 1f) k = Keys.Length - 1;
                else for (int j = Keys.Length - 1; j >= 0; j--) if (f >= Keys[j].t) { k = j; break; }
                var key = Keys[k];
                float a = key.a, split = reduce ? 0f : key.split;
                var pos = l.home + (reduce ? Vector2.zero : new Vector2(key.x, key.y));
                if (l.group.alpha != a) l.group.alpha = a;
                if (l.rt.anchoredPosition != pos) l.rt.anchoredPosition = pos;
                bool on = split != 0f;
                if (l.red.enabled != on) { l.red.enabled = on; l.blue.enabled = on; }
                if (on)
                {
                    var rp = new Vector2(-split, 0f);
                    if (l.red.rectTransform.anchoredPosition != rp) { l.red.rectTransform.anchoredPosition = rp; l.blue.rectTransform.anchoredPosition = -rp; }
                }
            }

            // .lbar: growX .9s after .55s
            float bp = t - enterStart - BarDelay;
            float bw = !entering ? BarW : bp <= 0f ? 0f : reduce ? BarW : BarW * UIEase.Tactical(bp / BarTime);
            if (bar.sizeDelta.x != bw) bar.sizeDelta = new Vector2(bw, 10f);

            // the stripes scroll and the band runs whatever the entrance does
            diag.Evaluate(Mathf.Repeat(t, diag.Duration));
            bool showBand = !(Application.isPlaying && UIScreenFX.Instance != null && UIScreenFX.Instance.isActiveAndEnabled
                && UIMotionSettings.screenFx && UITheme.Current.screenFx.band) && !reduce;
            if (bandImage.enabled != showBand) bandImage.enabled = showBand;
            if (showBand)
            {
                float y = Mathf.Lerp(-BandH, ScreenH * 1.1f, Mathf.Repeat(t, BandPeriod) / BandPeriod);
                var bpos = new Vector2(0f, -y);
                if (band.anchoredPosition != bpos) band.anchoredPosition = bpos;
            }
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (clock > 960f) { clock -= 960f; enterStart = Never; }    // a multiple of the 8s band and the 1.6s stripes
            if (enterStart > Never && clock - enterStart > Duration) enterStart = Never;
            Evaluate(clock);
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        /// <summary>Builds the column and its background on this object (once; the prefab holds only the root).</summary>
        public void Build()
        {
            if (column != null) return;
            var theme = UITheme.Current;
            var root = (RectTransform)transform;
            UILayout.Place(root, 0f, 0f, ScreenW, ScreenH);

            // ---- background ----
            var ghost = HudKit.AddText(root, Logo, 300f, new Color(1f, 1f, 1f, SrgbAlpha(.028f, 1f, Lum(Bg))), true, TextAlignmentOptions.TopLeft, "DecoGhost", UITheme.TextRole.Display);
            ghost.characterSpacing = 2f;
            ghost.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.LineHeight(ghost, 1f);
            UILayout.Place(ghost.rectTransform, -30f, ScreenH + 60f - 300f, 2200f, 300f);

            band = HudKit.NewRect("DecoScanBand", root);
            UILayout.Place(band, 0f, -BandH, ScreenW, BandH);
            bandImage = HudKit.AddImage(band, Color.white);
            bandImage.sprite = BandSprite;

            var dm = HudKit.NewRect("DecoDiag", root);
            UILayout.Place(dm, 0f, ScreenH - 12f, 560f, 12f);
            HudKit.AddImage(dm, Color.white);
            dm.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.RightEdge, 20f);
            dm.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var acc = theme.acc;
            diag = HazardStripe.Create(dm, "DecoDiagStripes", new Color(acc.r, acc.g, acc.b, .85f), new Color(0f, 0f, 0f, 0f), 14f, 28f, 1.6f);
            diag.over = Bg;
            diag.SetScroll(false);                                  // scrolled from this column's clock (Evaluate)

            // ---- .tl ----
            column = HudKit.NewRect("Column", root);
            UILayout.Place(column, X, Y, W, H);

            var code = Fade(0, "Code", CodeY, CodeH);
            var tac = Chip.Create(code, "Tac", "TAC-OS", KitColor.Blue, KitColor.White, 12f, 8f);
            tac.padX = 10f; tac.padY = 3f;
            UIFonts.Apply(tac.label, UITheme.TextRole.Display);
            tac.label.characterSpacing = 12f;
            tac.Fit();
            var tr = (RectTransform)tac.transform;
            UILayout.Place(tr, 0f, (CodeH - tr.sizeDelta.y) * .5f, tr.sizeDelta.x, tr.sizeDelta.y);
            var mid = HudKit.AddText(code, "TACTICAL BREACH SIMULATION", 12f, Gray, true, TextAlignmentOptions.MidlineLeft, "Mid", UITheme.TextRole.Display);
            mid.characterSpacing = 12f;
            mid.textWrappingMode = TextWrappingModes.NoWrap;
            float mx = tr.sizeDelta.x + 10f, mw = Mathf.Ceil(mid.GetPreferredValues(mid.text).x) + 1f;
            UILayout.Place(mid.rectTransform, mx, 0f, mw, CodeH);
            var bld = HudKit.NewRect("Build", code);
            var frame = bld.gameObject.AddComponent<TacticalPanel>();
            frame.SetPreset(KitColor.Custom); frame.color = Bg; frame.raycastTarget = false;     // (the border takes the fill's alpha)
            frame.borderWidth = 1f; frame.borderPreset = KitColor.Custom; frame.borderCustom = FrameC;
            var bt = HudKit.AddText(bld, "BUILD 0.6", 12f, Gray, true, TextAlignmentOptions.Center, "Label", UITheme.TextRole.Display);
            bt.characterSpacing = 12f;
            bt.textWrappingMode = TextWrappingModes.NoWrap;
            float bw = Mathf.Ceil(bt.GetPreferredValues(bt.text).x) + 1f + 16f + 2f;
            UILayout.Place(bld, mx + mw + 10f, 0f, bw, CodeH);
            HudKit.Stretch(bt.rectTransform);

            // .logo: one text per letter at its place in the word
            var logo = HudKit.NewRect("Logo", column);
            UILayout.Place(logo, 0f, LogoY, 600f, LogoH);
            var measure = HudKit.AddText(logo, Logo, LogoSize, LogoC, true, TextAlignmentOptions.TopLeft, "Measure", UITheme.TextRole.Display);
            measure.characterSpacing = 2f;
            for (int i = 0; i < Logo.Length; i++)
            {
                float x = i == 0 ? 0f : Mathf.Round(measure.GetPreferredValues(Logo.Substring(0, i)).x + LogoSize * .02f);
                letters[i] = BuildLetter(logo, i, x);
            }
            HudKit.DestroySafe(measure.gameObject);

            // .lbar: blue 0–30%, clear 30–32%, white .75 32–100% of the growing width, the right edge leaning 12
            bar = HudKit.NewRect("LogoBar", column);
            UILayout.Place(bar, 0f, BarY, BarW, 10f);
            HudKit.AddImage(bar, Color.white);
            bar.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.RightEdge, 12f);
            bar.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            BarPart("Blue", 0f, .3f, theme.blue);
            BarPart("White", .32f, 1f, BarWhiteC);

            var sub = Fade(1, "Sub", SubY, SubH);
            var st = HudKit.AddText(sub, "壁の向こうは見えない。視界を開き、先に発見し、先に撃て。\n上から見下ろす3D戦場で繰り広げられる、5対5の人質救出作戦。", 16f, SubC, false,
                TextAlignmentOptions.TopLeft, "Text", UITheme.TextRole.Body);
            st.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.LineHeight(st, 1.75f);
            UILayout.Place(st.rectTransform, 0f, 0f, 600f, SubH);

            var meta = Fade(2, "Meta", MetaY, MetaH);
            string[] labels = { "SECTOR", "HOSTAGE", "HOSTILES", "TIME LIMIT" };
            for (int i = 0; i < 4; i++)
            {
                var box = HudKit.NewRect(labels[i], meta);
                HudKit.AddImage(box, MetaBoxC);
                box.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.RightEdge, 12f);
                box.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                var line = HudKit.NewRect("Line", box);
                line.anchorMin = new Vector2(0f, 1f); line.anchorMax = Vector2.one; line.pivot = new Vector2(.5f, 1f);
                line.anchoredPosition = Vector2.zero; line.sizeDelta = new Vector2(0f, 3f);
                HudKit.AddImage(line, MetaLineC);
                metaLabels[i] = HudKit.AddText(box, labels[i], 11f, Gray, true, TextAlignmentOptions.TopLeft, "Label", UITheme.TextRole.Display);
                metaLabels[i].characterSpacing = 10f;
                metaLabels[i].textWrappingMode = TextWrappingModes.NoWrap;
                metaValues[i] = HudKit.AddText(box, "", 26f, Color.white, true, TextAlignmentOptions.TopLeft, "Value", UITheme.TextRole.Display);
                metaValues[i].textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.LineHeight(metaValues[i], 1.25f);
                metaBoxes[i] = box;
            }
            metaValues[0].text = "SUB-07"; metaValues[1].text = "01"; metaValues[2].text = "05"; metaValues[3].text = "15R";
            LayoutMeta();

            // .help{top:676px;width:660px;border-left:3px solid blue;padding:2px 0 2px 12px;12.5px/1.5;nowrap}
            var help = Fade(3, "Help", HelpY, HelpH);
            var hl = HudKit.NewRect("Line", help);
            UILayout.Place(hl, 0f, 0f, 3f, HelpH);
            HudKit.AddImage(hl, theme.blue);
            var ht = HudKit.AddText(help, "<color=#FFFFFF>操作</color> — 左クリック: 選択・コマンド / ドラッグ: 経路 / ホイール: ズーム / ESC・右クリック: キャンセル / Q: 次の隊員",
                12.5f, HelpC, false, TextAlignmentOptions.TopLeft, "Text", UITheme.TextRole.Body);
            ht.textWrappingMode = TextWrappingModes.NoWrap;
            ht.overflowMode = TextOverflowModes.Overflow;
            ht.characterSpacing = -2.4f;                            // −.024em: the one line fits 645 at 12.5 (no ellipsis)
            UILayout.LineHeight(ht, 1.5f);
            UILayout.Place(ht.rectTransform, 15f, 2f, W - 15f, 18.75f);

            enterStart = Never;
            Evaluate(0f);
        }

        // a fading block (fadeUp): Block (the group) → Body (moved)
        RectTransform Fade(int i, string name, float y, float h)
        {
            var block = HudKit.NewRect(name, column);
            UILayout.Place(block, 0f, y, W, h);
            fadeGroups[i] = block.gameObject.AddComponent<CanvasGroup>();
            fadeGroups[i].blocksRaycasts = false;
            var body = HudKit.NewRect("Body", block);
            HudKit.Stretch(body);
            fades[i] = body;
            return body;
        }

        void BarPart(string name, float from, float to, Color c)
        {
            var rt = HudKit.NewRect(name, bar);
            rt.anchorMin = new Vector2(from, 0f); rt.anchorMax = new Vector2(to, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            HudKit.AddImage(rt, c);
        }

        Letter BuildLetter(RectTransform logo, int i, float x)
        {
            var l = new Letter();
            l.rt = HudKit.NewRect("L" + i, logo);
            UILayout.Place(l.rt, x, 0f, LogoSize, LogoH);
            l.home = l.rt.anchoredPosition;
            l.group = l.rt.gameObject.AddComponent<CanvasGroup>();
            l.group.blocksRaycasts = false;
            string ch = Logo.Substring(i, 1);
            l.red = LetterText(l.rt, ch, "Red", GlitchRed);
            l.blue = LetterText(l.rt, ch, "Blue", GlitchBlue);
            l.main = LetterText(l.rt, ch, "Letter", LogoC);
            l.red.enabled = false; l.blue.enabled = false;
            l.red.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the logo letter's red copy (text-shadow), offset on purpose";
            l.blue.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the logo letter's blue copy (text-shadow), offset on purpose";
            return l;
        }

        static TMP_Text LetterText(RectTransform parent, string ch, string name, Color c)
        {
            var t = HudKit.AddText(parent, ch, LogoSize, c, true, TextAlignmentOptions.TopLeft, name, UITheme.TextRole.Display);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.LineHeight(t, .95f);
            HudKit.Stretch(t.rectTransform);
            t.gameObject.AddComponent<LintIgnoreClip>().reason = "one letter of the logo in a 118-wide slot; the glyph's own advance decides the word's spacing";
            return t;
        }

        // ---- sRGB alpha (a Linear project blends in linear light) -------------------------------------------------------------
        static float Lum(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;

        /// <summary>The alpha that blends a colour of sRGB luminance `fg` over `bg` to the brightness CSS's sRGB blending gives at `a`.</summary>
        static float SrgbAlpha(float a, float fg, float bg)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return a;
            float lb = Mathf.GammaToLinearSpace(bg), lc = Mathf.GammaToLinearSpace(fg), lt = Mathf.GammaToLinearSpace(Mathf.Lerp(bg, fg, a));
            return Mathf.Abs(lc - lb) > .0001f ? Mathf.Clamp01((lt - lb) / (lc - lb)) : a;
        }

        // linear-gradient(transparent, rgba(blue, .07), transparent) top → bottom, each row's alpha matched over the background
        static Sprite bandSprite;
        static Sprite BandSprite
        {
            get
            {
                if (bandSprite != null) return bandSprite;
                const int N = 32;
                var blue = UITheme.Current.blue;
                var tex = new Texture2D(1, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < N; y++)
                {
                    float u = (y + .5f) / N, a = .07f * (1f - Mathf.Abs(u - .5f) * 2f);
                    tex.SetPixel(0, y, new Color(blue.r, blue.g, blue.b, SrgbAlpha(a, Lum(blue), Lum(Bg))));
                }
                tex.Apply(false, true);
                bandSprite = Sprite.Create(tex, new Rect(0, 0, 1, N), new Vector2(.5f, .5f), 100f);
                bandSprite.hideFlags = HideFlags.HideAndDontSave;
                return bandSprite;
            }
        }
    }
}
