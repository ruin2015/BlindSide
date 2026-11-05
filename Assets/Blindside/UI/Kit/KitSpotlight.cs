// The title's operator spotlight (#spot .glc, ui_spec "타이틀 크기"): 474×330 at x56 y436 of the 1440×810 title canvas, built from the kit —
//   Block         placed; faded by the entrance (sdIn: from +80 and transparent in .6s after 1s)
//     Body        moved by the entrance and the card shake
//       DecoShadow  KitShadow 0 14 34 .55
//       Bg        ink, the top-right corner cut 28 (CutCornerGraphic), the stencil the rest is clipped to
//         Art     x170 → the right edge, the top-left 64 cut off (SlantedGraphic LeftEdge 64, a Mask):
//                 DecoArtBg (#f3f5f6 → #dfe3e6), DecoStripes (135°, black .035 1px / 6px), DecoWedge (operator colour .18, skewX −14°),
//                 DecoFlash (the .art brightness flash), Photo (UIGlitchImage CardEnter: the upper body — the top 62% of the picture —
//                 covering the window, top-pinned, by its UV rect), DecoSweep (the light band down the window), DecoScan (black .05 1px / 4px)
//         Cbar    the operator colour, 14 wide, full height
//         Label 12 display (SELECTED // ASSAULT | DEFENSE, white .55), Name 42 display (UIGlitchName), Kana 16, Role 13 (white .7),
//         Ability chip (operator colour, only its right edge leaning 10: ✦ 19 display + the ability name 13, ink)
//         Dots    five 8-high bars skewed −20°, 8 apart at left 32 / bottom 24: the shown operator's 48 wide in its colour, the others
//                 26 wide (white .22); width and colour follow in .25s
// Show(op, index, team, glitch) puts an operator in; with glitch on it replays the card glitch (photo CardEnter, the name's glitch, the
// card shake, the .art flash and sweep) — the caller (KitTitleSquad) skips it for the operator already shown. Every motion is a tween
// or a timed effect on one clock (Evaluate(t)); Update advances it with unscaled time while something runs. Translucent mockup colours
// are drawn by the KitDeco material (sRGB alpha match over `over`) or turned into the opaque sRGB colour they give. Nothing is
// allocated per frame (texts and layout change only in Show).
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.Data;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Spotlight")]
    public class KitSpotlight : MonoBehaviour, ITimedEffect
    {
        public const float X = 56f, Y = 436f, W = 474f, H = 330f, Cut = 28f, ArtX = 170f, ArtCut = 64f, BarW = 14f, TextW = 250f;
        public const float EnterShift = 80f, EnterDelay = 1f, EnterTime = .6f, DotTime = .25f;
        public const float GlitchTime = .85f, SweepStart = .3f, SweepTime = .55f, FlashTime = .5f, UpperBody = .62f;
        public const float DotH = 8f, DotOff = 26f, DotOn = 48f, DotGap = 8f, DotLeft = 32f, DotBottom = 24f;
        const float Never = -100f, WedgeSkew = 14f;
        static readonly Color MenuBg = new Color32(0x0d, 0x10, 0x14, 0xff);
        static readonly Color ArtMid = new Color32(0xE9, 0xEC, 0xEE, 0xFF);
        static readonly Color LabelC = new Color32(0x95, 0x96, 0x98, 0xff);   // white .55 over ink
        static readonly Color KanaC = new Color32(0xE7, 0xE8, 0xE8, 0xff);    // white .9 over ink
        static readonly Color RoleC = new Color32(0xB8, 0xB9, 0xBA, 0xff);    // white .7 over ink
        static readonly Color DotOffC = new Color32(0x48, 0x49, 0x4C, 0xff);  // white .22 over ink

        struct Tween
        {
            public float from, to, start, dur;
            public float At(float t) => Mathf.LerpUnclamped(from, to, UIEase.Tactical(dur > 0f ? (t - start) / dur : 1f));
            public float End => start + dur;
            public void Settle(float v) { from = to = v; start = 0f; dur = 0f; }
        }

        [Header("Parts")]
        public CanvasGroup group;
        public RectTransform body, art;
        public Image cbar;
        public UIGlitchImage photo;
        public KitTint wedge, flash;
        public KitSweep sweep;
        public TMP_Text label, nameText, kanaText, roleText, abilityIcon, abilityName;
        public UIGlitchName nameGlitch;
        public Image chip;
        public Image[] dots = new Image[5];

        Tween[] dotT = new Tween[5];
        Color[] dotFrom = new Color[5], dotTo = new Color[5];
        Color opColor = Color.white;
        float clock, enterStart = Never, glitchStart = Never;
        bool running;
        int shownIndex = -1;

        /// <summary>The operator on the card (null before the first Show).</summary>
        public OperatorData Shown { get; private set; }
        public int ShownIndex => shownIndex;

        public float Duration
        {
            get
            {
                float d = Mathf.Max(enterStart + EnterDelay + EnterTime, glitchStart + GlitchTime);
                for (int i = 0; i < dotT.Length; i++) d = Mathf.Max(d, dotT[i].End);
                return d;
            }
        }

        // ---- content ------------------------------------------------------------------------------------------------------------
        /// <summary>Puts `op` (the `index`-th of `team`'s roster) on the card; `glitch` replays the card glitch.</summary>
        public void Show(OperatorData op, int index, OperatorTeam team, bool glitch)
        {
            Build();
            if (op == null) return;
            var theme = UITheme.Current;
            Shown = op; shownIndex = index;
            opColor = op.color;
            cbar.color = op.color;
            label.text = team == OperatorTeam.Defense ? "SELECTED // DEFENSE" : "SELECTED // ASSAULT";
            nameText.text = op.DisplayName;
            kanaText.text = op.kr;
            roleText.text = op.role;
            abilityName.text = op.abilityName;
            chip.color = op.color;
            wedge.Set(new Color(op.color.r, op.color.g, op.color.b, .18f), ArtMid);
            SetPhoto(OperatorArt.Body(op.key));

            // em{padding:4px 16px 4px 14px}: ✦, a space, the name; the box's right edge leans 10
            float iconW = Mathf.Ceil(abilityIcon.GetPreferredValues("✦").x);
            float nameW = Mathf.Ceil(abilityName.GetPreferredValues(op.abilityName).x) + 1f;
            float chipW = 14f + iconW + 4f + nameW + 16f;
            UILayout.Place(chip.rectTransform, 32f, ChipY, chipW, ChipH);
            UILayout.Place(abilityIcon.rectTransform, 14f, -2f, iconW, ChipH + 4f);
            UILayout.Place(abilityName.rectTransform, 14f + iconW + 4f, 4f, nameW, ChipH - 8f);

            Rebase();
            float c = clock;
            for (int i = 0; i < dots.Length; i++)
            {
                bool on = i == index;
                Color cur = dots[i].color;
                dotFrom[i] = cur; dotTo[i] = on ? op.color : DotOffC;
                float w = dots[i].rectTransform.sizeDelta.x, to = on ? DotOn : DotOff;
                if (!Application.isPlaying) { dotT[i].Settle(to); dotFrom[i] = dotTo[i]; }
                else { dotT[i].from = w; dotT[i].to = to; dotT[i].start = c; dotT[i].dur = DotTime; }
            }
            if (glitch && Application.isPlaying) glitchStart = c;
            else if (enterStart > Never && glitchStart <= Never) glitchStart = enterStart + EnterDelay;   // the first operator, mid-entrance
            Run();
        }

        /// <summary>Poses the card `t` seconds into its glitch (previews: edit mode, no clock).</summary>
        public void PoseGlitch(float t)
        {
            running = false;
            clock = t; glitchStart = 0f; enterStart = Never;
            Evaluate(t);
        }

        // object-fit:cover; object-position:50% 0 — of the upper body (the top UpperBody of the picture), by its UV rect
        void SetPhoto(Sprite s)
        {
            if (s == null) { photo.SetTexture(null); photo.color = new Color(1f, 1f, 1f, 0f); return; }
            Texture2D tex = s.texture; Rect r = s.textureRect;
            float winAspect = (W - ArtX) / H;
            float h = r.height * UpperBody, w = h * winAspect;
            if (w > r.width) { w = r.width; h = w / winAspect; }
            float x = r.x + (r.width - w) * .5f, yb = r.y + r.height - h;
            photo.SetTexture(tex, new Rect(x / tex.width, yb / tex.height, w / tex.width, h / tex.height));
            photo.color = Color.white;
        }

        // ---- motion ------------------------------------------------------------------------------------------------------------
        void OnEnable()
        {
            Build();
            if (Application.isPlaying) PlayEnter();
        }

        /// <summary>sdIn after 1s, the card glitch with it.</summary>
        public void PlayEnter()
        {
            Rebase();
            enterStart = clock;
            if (Shown != null) glitchStart = clock + EnterDelay;
            Run();
        }

        void Rebase()
        {
            if (running) return;
            for (int i = 0; i < dotT.Length; i++) { dotT[i].Settle(dotT[i].At(clock)); }
            enterStart = Never; glitchStart = Never;
            clock = 0f;
        }

        void Run()
        {
            running = Application.isPlaying && isActiveAndEnabled;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            if (body == null) return;
            // sdIn
            float p = t - enterStart - EnterDelay;
            float e = enterStart <= Never ? 1f : p <= 0f ? 0f : UIEase.Tactical(p / EnterTime);
            if (UIMotionSettings.reduceMotion && enterStart > Never) e = p <= 0f ? 0f : 1f;
            if (group.alpha != e) group.alpha = e;

            // the card glitch: the photo's CardEnter (its offset is the card shake), the name, the .art flash and sweep
            float g = glitchStart <= Never ? GlitchTime : t - glitchStart;
            if (g < 0f) g = 0f;
            photo.preset = UIGlitchImage.Preset.CardEnter;
            Vector2 shake = photo.Evaluate(g);
            nameGlitch.Evaluate(g);
            float b = g >= FlashTime ? 1f : g < FlashTime * .4f ? Mathf.Lerp(1.8f, 1.15f, g / (FlashTime * .4f)) : Mathf.Lerp(1.15f, 1f, (g - FlashTime * .4f) / (FlashTime * .6f));
            float lift = Mathf.Clamp01((b - 1f) * ArtMid.r / Mathf.Max(.001f, 1f - ArtMid.r));
            flash.Set(lift > 0f ? new Color(1f, 1f, 1f, lift) : Color.clear, ArtMid);
            float s = (g - SweepStart) / SweepTime;
            if (s > 0f && s < 1f && !UIMotionSettings.reduceMotion) sweep.Evaluate(sweep.duration * (1f - Mathf.Pow(1f - s * s, .25f)));
            else sweep.Rest();

            var pos = new Vector2(EnterShift * (1f - e) + shake.x, 0f);
            if (body.anchoredPosition != pos) body.anchoredPosition = pos;

            // dots: widths and colours, laid out from the left
            float x = DotLeft;
            for (int i = 0; i < dots.Length; i++)
            {
                float k = dotT[i].dur > 0f ? Mathf.Clamp01((t - dotT[i].start) / dotT[i].dur) : 1f;
                float w = dotT[i].At(t);
                var rt = dots[i].rectTransform;
                var dp = new Vector2(x, -(H - DotBottom - DotH));
                if (rt.anchoredPosition != dp) rt.anchoredPosition = dp;
                if (rt.sizeDelta.x != w) rt.sizeDelta = new Vector2(w, DotH);
                Color c = Color.Lerp(dotFrom[i], dotTo[i], UIEase.Tactical(k));
                if (dots[i].color != c) dots[i].color = c;
                x += w + DotGap;
            }
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        const float ChipY = 157f, ChipH = 24f;

        /// <summary>Builds the card's parts on this object (once; the prefab holds only the root).</summary>
        public void Build()
        {
            if (body != null) return;
            var theme = UITheme.Current;
            var root = (RectTransform)transform;
            UILayout.Place(root, X, Y, W, H);
            group = UIUtil.GetOrAdd<CanvasGroup>(gameObject);
            group.blocksRaycasts = false; group.interactable = false;

            body = HudKit.NewRect("Body", root);
            HudKit.Stretch(body);

            var bgRt = HudKit.NewRect("Bg", body);
            HudKit.Stretch(bgRt);
            HudKit.AddImage(bgRt, theme.ink);
            bgRt.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.TopRight);
            bgRt.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            KitShadow.Behind(bgRt, MenuBg, .55f, 14f, 34f);

            // ---- .art: clip-path polygon(64px 0, 100% 0, 100% 100%, 0 100%) ----
            float artW = W - ArtX;
            art = HudKit.NewRect("Art", bgRt);
            UILayout.Place(art, ArtX, 0f, artW, H);
            var artImg = HudKit.AddImage(art, Color.white);
            art.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.LeftEdge, ArtCut);
            art.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            artImg.raycastTarget = false;

            var abg = HudKit.NewRect("DecoArtBg", art);
            HudKit.Stretch(abg);
            HudKit.AddImage(abg, Color.white).sprite = KitArtCard.ArtGradient;
            var stripes = HazardStripe.Create(art, "DecoStripes", new Color(0f, 0f, 0f, .035f), new Color(0f, 0f, 0f, 0f), 1f, 6f);
            stripes.over = ArtMid; stripes.SetScroll(false);

            // .art::before{left:-40px;top:-20px;width:190px;bottom:-20px;skewX(-14deg)}: rows 190 wide
            float wh = H + 40f, ws = Mathf.Tan(WedgeSkew * Mathf.Deg2Rad) * wh;
            var wr = HudKit.NewRect("DecoWedge", art);
            UILayout.Place(wr, -40f + 95f - (190f + ws) * .5f, -20f, 190f + ws, wh);
            wedge = wr.gameObject.AddComponent<KitTint>();
            wedge.raycastTarget = false;
            wedge.slant = ws; wedge.slantDirection = SlantedGraphic.Direction.Right;

            var fr = HudKit.NewRect("DecoFlash", art);
            HudKit.Stretch(fr);
            flash = fr.gameObject.AddComponent<KitTint>();
            flash.raycastTarget = false;

            var pr = HudKit.NewRect("Photo", art);
            HudKit.Stretch(pr);
            photo = pr.gameObject.AddComponent<UIGlitchImage>();
            photo.raycastTarget = false;
            photo.preset = UIGlitchImage.Preset.CardEnter;
            photo.color = new Color(1f, 1f, 1f, 0f);

            var sr = HudKit.NewRect("DecoSweep", art);
            sr.anchorMin = sr.anchorMax = new Vector2(.5f, .5f); sr.pivot = new Vector2(.5f, .5f);
            sr.sizeDelta = new Vector2(H, artW); sr.localRotation = Quaternion.Euler(0f, 0f, -90f);
            sweep = sr.gameObject.AddComponent<KitSweep>();
            sweep.raycastTarget = false;
            sweep.lightColor = new Color(1f, 1f, 1f, .5f);
            sweep.duration = 1f;
            sweep.over = ArtMid;
            sweep.Rest();

            var sc = HudKit.NewRect("DecoScan", art);
            HudKit.Stretch(sc);
            var scan = sc.gameObject.AddComponent<RawImage>();
            scan.raycastTarget = false;
            scan.texture = KitArtCard.ScanTexture;
            scan.uvRect = new Rect(0f, 0f, 1f, H / 4f);

            // ---- .cbar ----
            var cb = HudKit.NewRect("Cbar", bgRt);
            UILayout.Place(cb, 0f, 0f, BarW, H);
            cbar = HudKit.AddImage(cb, theme.blue);

            // ---- .nm{padding:24px 0 0 32px}: small 12/1.25 · b 42/1.05 (mt 12) · span 16/1.25 (mt 8) · span.rl 13 (mt 2) · em (mt 16) ----
            const float L = 32f;
            float tw = W - L - BarW;
            label = HudKit.AddText(bgRt, "SELECTED // ASSAULT", 12f, LabelC, true, TextAlignmentOptions.TopLeft, "Label", UITheme.TextRole.Display);
            label.characterSpacing = 14f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(label.rectTransform, L, 24f, TextW, 15f);
            nameText = HudKit.AddText(bgRt, "", 42f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            nameText.characterSpacing = 8f;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.LineHeight(nameText, 1.05f);
            UILayout.Place(nameText.rectTransform, L, 51f, tw, 44f);
            nameGlitch = nameText.gameObject.AddComponent<UIGlitchName>();
            kanaText = HudKit.AddText(bgRt, "", 16f, KanaC, true, TextAlignmentOptions.TopLeft, "Kana", UITheme.TextRole.Body);
            kanaText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(kanaText.rectTransform, L, 103f, TextW - L, 20f);
            roleText = HudKit.AddText(bgRt, "", 13f, RoleC, true, TextAlignmentOptions.TopLeft, "Role", UITheme.TextRole.Body);
            roleText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(roleText.rectTransform, L, 125f, TextW - L, 16f);

            var ch = HudKit.NewRect("Ability", bgRt);
            UILayout.Place(ch, L, ChipY, 120f, ChipH);
            chip = HudKit.AddImage(ch, theme.blue);
            ch.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.RightEdge, 10f);
            abilityIcon = HudKit.AddText(ch, "✦", 19f, theme.ink, true, TextAlignmentOptions.Midline, "AbilityIcon", UITheme.TextRole.Display);
            abilityName = HudKit.AddText(ch, "", 13f, theme.ink, true, TextAlignmentOptions.TopLeft, "AbilityName", UITheme.TextRole.Title);
            abilityName.textWrappingMode = TextWrappingModes.NoWrap;

            // ---- .dots ----
            for (int i = 0; i < dots.Length; i++)
            {
                var d = HudKit.NewRect("Dot" + i, bgRt);
                UILayout.Place(d, DotLeft + i * (DotOff + DotGap), H - DotBottom - DotH, DotOff, DotH);
                dots[i] = HudKit.AddImage(d, DotOffC);
                d.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, Mathf.Tan(20f * Mathf.Deg2Rad) * DotH);
                dotT[i].Settle(DotOff); dotFrom[i] = dotTo[i] = DotOffC;
            }
        }

        /// <summary>A spotlight object named `name` under `parent`, built (the prefab root is the same object without parts).</summary>
        public static KitSpotlight Create(Transform parent, string name)
        {
            var rt = HudKit.NewRect(name, parent);
            var c = rt.gameObject.AddComponent<KitSpotlight>();
            c.Build();
            return c;
        }
    }
}
