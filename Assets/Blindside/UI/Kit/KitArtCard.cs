// The HUD's operator illustration card (#oc .glc, ui_spec "일러스트 카드"): 300×500 at right 20 / top 76 of the HUD canvas, built from
// the kit —
//   Body          the part the card shake moves (the root stays where it was placed)
//     DecoShadowBg  KitShadow 0 14 34 .55
//     Bg          paper, CutCornerGraphic 24 (top-right + bottom-left, clip-path of #oc)
//     Art         the photo window between the tag (30) and the name block (96), RectMask2D:
//                 DecoArtBg (#f3f5f6 → #dfe3e6), DecoStripes (135°, black .035 1px / 6px), DecoWedge (operator colour .18, skewX −14°),
//                 DecoFlash (the .art brightness flash 1.8 → 1.15 → 1 over the paper), Photo (UIGlitchImage CardEnter: wipe, jitter,
//                 strips, its own sweep and flash), DecoSweep (KitSweep turned upright: the light band runs down the window from .3s for
//                 .55s, ease-in), DecoScan (scanlines, black .05 1px / 4px)
//     NameBlock   ink, CutCornerGraphic (bottom-left), DecoLine 5 in the operator colour on top: Name 34 display (UIGlitchName),
//                 Kana 13 body (kana · role), AbilityIcon ✦ (display) + AbilityName 12.5 body in the operator colour at right 20
//     Tag         ink 30, CutCornerGraphic (top-right): TagDot 6×6 in the operator colour blinking (1s, steps(2), .25), TagText 11 display
//     DecoBrackets  CornerBrackets 18 / 3 (ink .6) at the photo's top-left (10, 40) and bottom-right (10 above the name block)
// The photo fills the window ("cover", top-pinned) by cropping its UV rect, so the picture never leaves the card or the screen and is
// never squashed. Show / SetContent / Hide have UIOperatorArtCard's signatures. Show replays the entrance unless the same operator is
// already shown (UIOperatorArtCard's rule); Evaluate(t) poses everything t seconds into the 0.8s entrance; Update only advances unscaled
// clocks. Translucent mockup colours are drawn by the KitDeco material (sRGB alpha match over `over`) or turned into the opaque sRGB colour
// they give. No allocation per frame (texts and layout change only in SetContent).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Art Card")]
    public class KitArtCard : MonoBehaviour, ITimedEffect
    {
        public const float Width = 300f, Height = 500f, Right = 20f, Top = 76f, TagH = 30f, NameH = 96f, Cut = 24f, LineH = 5f;
        public const float EnterTime = .8f, SweepStart = .3f, SweepTime = .55f, FlashTime = .5f, BlinkPeriod = 1f, BlinkLow = .25f;
        const float WedgeSkew = 14f, Never = -100f;
        static readonly Color ArtTop = new Color32(0xF3, 0xF5, 0xF6, 0xFF), ArtBottom = new Color32(0xDF, 0xE3, 0xE6, 0xFF);
        static readonly Color ArtMid = new Color32(0xE9, 0xEC, 0xEE, 0xFF);
        static readonly Color MapFloor = new Color32(0x2b, 0x2f, 0x35, 0xff);

        [Header("Parts")]
        public RectTransform body, art;
        public CanvasGroup group;
        public UIGlitchImage photo;
        public KitTint wedge, flash;
        public KitSweep sweep;
        public Image line, tagDot;
        public TMP_Text tagText, nameText, kanaText, abilityIcon, abilityName;
        public UIGlitchName nameGlitch;

        Color opColor = Color.white;
        string current;
        float clock, enterStart = Never, blink;
        bool running, dotLow;

        public bool Visible => gameObject.activeSelf;
        public float Duration => EnterTime;

        // ---- UIOperatorArtCard's API ----------------------------------------------------------------------------------------------
        /// <summary>Shows the operator and plays the 0.8s entrance — unless the same operator is already on screen.</summary>
        public void Show(Sprite illustration, string key, string krRole, string ability, string tag, Color color)
        {
            if (current == key && Visible) return;
            current = key;
            gameObject.SetActive(true);
            SetContent(illustration, key, krRole, ability, tag, color);
            Play();
        }

        public void Hide()
        {
            running = false; current = null;
            gameObject.SetActive(false);
        }

        /// <summary>The card's picture, texts and colours, without playing the entrance.</summary>
        public void SetContent(Sprite illustration, string key, string krRole, string ability, string tag, Color color)
        {
            var theme = UITheme.Current;
            opColor = color;
            tagText.text = tag;
            nameText.text = key;
            kanaText.text = krRole;
            abilityName.text = ability;
            line.color = color;
            abilityIcon.color = color; abilityName.color = color;
            wedge.Set(new Color(color.r, color.g, color.b, .18f), ArtMid);
            dotLow = false; tagDot.color = color;
            SetPhoto(illustration);

            // em{right:20px;bottom:11px}: the ✦ just left of the right-aligned name
            float nameW = Mathf.Min(Mathf.Ceil(abilityName.GetPreferredValues(ability).x) + 1f, Width - 18f - 20f - 16f);
            float y = NameH - 9f - 16f;                                   // in the name block, under the kana's line box
            UILayout.Place(abilityName.rectTransform, Width - 20f - nameW, y, nameW, 16f);
            UILayout.Place(abilityIcon.rectTransform, Width - 20f - nameW - 14f, y - 2f, 14f, 20f);
            abilityIcon.gameObject.SetActive(!string.IsNullOrEmpty(ability));
        }

        /// <summary>Starts the entrance on the unscaled clock.</summary>
        public void Play()
        {
            clock = 0f; enterStart = 0f;
            running = Application.isPlaying && isActiveAndEnabled;
            Evaluate(0f);
        }

        // object-fit:cover; object-position:50% 0 — by cropping the picture's UV rect to the window's aspect ratio
        void SetPhoto(Sprite s)
        {
            if (s == null) { photo.SetTexture(null); photo.color = new Color(1f, 1f, 1f, 0f); return; }
            Texture2D tex = s.texture; Rect r = s.textureRect;
            var win = art.rect;
            float winAspect = win.width / Mathf.Max(1f, win.height), picAspect = r.width / Mathf.Max(1f, r.height);
            float w = r.width, h = r.height, x = r.x, yb = r.y;
            if (picAspect > winAspect) { w = r.height * winAspect; x = r.x + (r.width - w) * .5f; }       // wider: crop the sides
            else { h = r.width / winAspect; yb = r.y + (r.height - h); }                                       // taller: keep the top
            photo.SetTexture(tex, new Rect(x / tex.width, yb / tex.height, w / tex.width, h / tex.height));
            photo.color = Color.white;
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public void Evaluate(float t)
        {
            float e = t - enterStart;
            if (e < 0f) e = EnterTime;

            // the photo's own glitch; its offset is the card shake (14% −7, 24% +6, 34% −3)
            photo.preset = UIGlitchImage.Preset.CardEnter;
            Vector2 shake = photo.Evaluate(e);
            if (body.anchoredPosition != shake) body.anchoredPosition = shake;
            nameGlitch.Evaluate(e);

            // .art brightness 1.8 → 1.15 (40%) → 1 over .5s on the paper: white over #e9ecee by the share brightness lifts it
            float b = e >= FlashTime ? 1f : e < FlashTime * .4f ? Mathf.Lerp(1.8f, 1.15f, e / (FlashTime * .4f)) : Mathf.Lerp(1.15f, 1f, (e - FlashTime * .4f) / (FlashTime * .6f));
            float lift = Mathf.Clamp01((b - 1f) * ArtMid.r / Mathf.Max(.001f, 1f - ArtMid.r));
            flash.Set(lift > 0f ? new Color(1f, 1f, 1f, lift) : Color.clear, ArtMid);

            // the light band (.sweep): from .3s for .55s, ease-in; KitSweep eases with UIEase.Tactical, so it gets the time that gives
            // the ease-in position
            float s = (e - SweepStart) / SweepTime;
            if (s > 0f && s < 1f && !UIMotionSettings.reduceMotion)
            {
                float p = s * s;
                sweep.Evaluate(sweep.duration * (1f - Mathf.Pow(1f - p, .25f)));
            }
            else sweep.Rest();
        }

        void Update()
        {
            // .tag i: blink 1s steps(2) — full for half a second, .25 for the other half
            blink += Time.unscaledDeltaTime;
            if (blink >= BlinkPeriod) blink -= BlinkPeriod * Mathf.Floor(blink / BlinkPeriod);
            bool low = blink >= BlinkPeriod * .5f;
            if (low != dotLow) { dotLow = low; tagDot.color = low ? Color.Lerp(UITheme.Current.ink, opColor, BlinkLow) : opColor; }

            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= EnterTime) running = false;
        }

        // ---- build ------------------------------------------------------------------------------------------------------------
        static Sprite artGradient;
        internal static Sprite ArtGradient
        {
            get
            {
                if (artGradient != null) return artGradient;
                var tex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < 32; y++) tex.SetPixel(0, y, Color.Lerp(ArtBottom, ArtTop, y / 31f));    // y = 0 is the bottom row
                tex.Apply(false, true);
                artGradient = Sprite.Create(tex, new Rect(0, 0, 1, 32), new Vector2(.5f, .5f), 100f);
                artGradient.hideFlags = HideFlags.HideAndDontSave;
                return artGradient;
            }
        }

        static Texture2D scanTex;
        internal static Texture2D ScanTexture
        {
            get
            {
                // repeating-linear-gradient(0deg, rgba(0,0,0,.05) 0 1px, transparent 1px 4px): one dark row in four. The alpha is the
                // one that darkens a light colour in Linear space as .05 does in sRGB (1 − .95^2.2).
                if (scanTex != null) return scanTex;
                scanTex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                var clear = new Color(0f, 0f, 0f, 0f);
                scanTex.SetPixel(0, 0, clear); scanTex.SetPixel(0, 1, clear); scanTex.SetPixel(0, 2, clear);
                scanTex.SetPixel(0, 3, new Color(0f, 0f, 0f, 1f - Mathf.Pow(.95f, 2.2f)));
                scanTex.Apply(false, true);
                return scanTex;
            }
        }

        /// <summary>Builds the card's parts on this object (once; the prefab holds only the root) and hides it.</summary>
        public void Build()
        {
            if (body != null) return;
            var theme = UITheme.Current;
            var root = (RectTransform)transform;
            UILayout.Anchor(root, new Vector2(1f, 1f), -Right, -Top, Width, Height);
            group = UIUtil.GetOrAdd<CanvasGroup>(gameObject);
            group.blocksRaycasts = false; group.interactable = false;         // #opArt{pointer-events:none}

            body = HudKit.NewRect("Body", root);
            HudKit.Stretch(body);

            var bgRt = HudKit.NewRect("Bg", body);
            HudKit.Stretch(bgRt);
            HudKit.AddImage(bgRt, theme.paper);
            // a hair more than the tag / name block cut, so no paper sliver shows along their cut edges
            bgRt.gameObject.AddComponent<CutCornerGraphic>().Set(Cut + 1.5f, CutCornerGraphic.Corners.Mockup);
            KitShadow.Behind(bgRt, MapFloor, .55f, 14f, 34f);

            // ---- .art ----
            float artH = Height - TagH - NameH;
            art = HudKit.NewRect("Art", body);
            UILayout.Place(art, 0f, TagH, Width, artH);
            art.gameObject.AddComponent<RectMask2D>();
            var abg = HudKit.NewRect("DecoArtBg", art);
            HudKit.Stretch(abg);
            var abgImg = HudKit.AddImage(abg, Color.white);
            abgImg.sprite = ArtGradient;
            var stripes = HazardStripe.Create(art, "DecoStripes", new Color(0f, 0f, 0f, .035f), new Color(0f, 0f, 0f, 0f), 1f, 6f);
            stripes.over = ArtMid; stripes.SetScroll(false);

            // .art::before{left:-40px;top:-20px;width:190px;bottom:-20px;skewX(-14deg)} as a parallelogram whose rows are 190 wide
            float wh = artH + 40f, ws = Mathf.Tan(WedgeSkew * Mathf.Deg2Rad) * wh;
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

            // .sweep: KitSweep runs along its box's width, so the box is the window turned upright (its width = the window height)
            var sr = HudKit.NewRect("DecoSweep", art);
            sr.anchorMin = sr.anchorMax = new Vector2(.5f, .5f); sr.pivot = new Vector2(.5f, .5f);
            sr.sizeDelta = new Vector2(artH, Width); sr.localRotation = Quaternion.Euler(0f, 0f, -90f);
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
            scan.texture = ScanTexture;
            scan.uvRect = new Rect(0f, 0f, 1f, artH / 4f);

            // ---- .nm ----
            var nm = HudKit.NewRect("NameBlock", body);
            UILayout.Place(nm, 0f, Height - NameH, Width, NameH);
            HudKit.AddImage(nm, theme.ink);
            nm.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.BottomLeft);
            var lr = HudKit.NewRect("DecoLine", nm);
            UILayout.Place(lr, 0f, 0f, Width, LineH);
            line = HudKit.AddImage(lr, theme.blue);

            nameText = HudKit.AddText(nm, "", 34f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            nameText.characterSpacing = 12f;
            UILayout.LineHeight(nameText, 1f);
            UILayout.Place(nameText.rectTransform, 18f, LineH + 11f, Width - 36f, 36f);
            nameGlitch = nameText.gameObject.AddComponent<UIGlitchName>();
            kanaText = HudKit.AddText(nm, "", 13f, Color.Lerp(theme.ink, Color.white, .8f), true, TextAlignmentOptions.TopLeft, "Kana", UITheme.TextRole.Body);
            UILayout.Place(kanaText.rectTransform, 18f, LineH + 11f + 34f + 5f, Width - 36f, 16f);
            // the ability sits 2 lower than em{bottom:11px} so its line box doesn't touch the kana's
            // (placed per text in SetContent)
            abilityName = HudKit.AddText(nm, "", 12.5f, theme.blue, true, TextAlignmentOptions.TopRight, "AbilityName", UITheme.TextRole.Body);
            abilityIcon = HudKit.AddText(nm, "✦", 19f, theme.blue, true, TextAlignmentOptions.MidlineLeft, "AbilityIcon", UITheme.TextRole.Display);

            // ---- .tag ----
            var tg = HudKit.NewRect("Tag", body);
            UILayout.Place(tg, 0f, 0f, Width, TagH);
            HudKit.AddImage(tg, theme.ink);
            tg.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.TopRight);
            var dr = HudKit.NewRect("TagDot", tg);
            UILayout.Place(dr, 18f, (TagH - 6f) * .5f, 6f, 6f);
            tagDot = HudKit.AddImage(dr, theme.blue);
            tagText = HudKit.AddText(tg, "", 11f, Color.white, true, TextAlignmentOptions.MidlineLeft, "TagText", UITheme.TextRole.Display);
            tagText.characterSpacing = 12f;
            tagText.overflowMode = TextOverflowModes.Ellipsis;
            // to 6 short of the edge: at the glyphs' height the top-right cut runs at x 287–295, the mockup's text reaches 292 too
            UILayout.Place(tagText.rectTransform, 31f, 0f, Width - 31f - 6f, TagH);

            // ---- .br ----
            var brRt = HudKit.NewRect("DecoBrackets", body);
            UILayout.Place(brRt, 10f, TagH + 10f, Width - 20f, artH - 20f);
            CornerBrackets.Create(brRt, "DecoBracketPair", new Color(theme.ink.r, theme.ink.g, theme.ink.b, .6f), 18f, 3f, 0f, ArtMid);

            enterStart = Never;
            gameObject.SetActive(false);
        }

        /// <summary>A card object named `name` under `parent`, built and hidden (the prefab root is the same object without parts).</summary>
        public static KitArtCard Create(Transform parent, string name)
        {
            var rt = HudKit.NewRect(name, parent);
            var c = rt.gameObject.AddComponent<KitArtCard>();
            c.Build();
            return c;
        }
    }
}
