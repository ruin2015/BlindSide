// The HUD's cinematic widgets — phase banner, contact alert, headshot badge, END TURN — built at runtime (AddComponent, never saved
// to a prefab; see GameplayUIBridge's header for why). Split out of GameplayUIBridge so the editor previews build exactly the same
// objects; the bodies are the bridge's former Build* methods, unchanged, with the END TURN click handed in by the caller.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public static class HudWidgets
    {
        static Color32 Ink2 => new Color32(0x23, 0x27, 0x2C, 0xFF);

        static RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
            => HudKit.MakeRect(name, parent, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta);
        static Image AddImg(RectTransform rt, Color color) => HudKit.AddImg(rt, color);
        static TMP_Text AddText(Transform parent, string text, float size, Color color, bool bold, UITheme.TextRole? role = null)
            => HudKit.AddCenteredText(parent, text, size, color, bold, role);

        static Texture2D dashTexture;
        /// <summary>One 34px cell of the banner's dashed lines: 22 on, 12 off. The .55 white over the ink band is given as the opaque sRGB gray it makes.</summary>
        internal static Texture2D DashTexture()
        {
            if (dashTexture != null) return dashTexture;
            const int N = 34;
            var tex = new Texture2D(N, 1, TextureFormat.RGBA32, false) { name = "KitDashCell", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var ink = UITheme.Current.ink;
            Color32 on = Color.Lerp(ink, Color.white, .55f), off = on; off.a = 0;
            var px = new Color32[N];
            for (int i = 0; i < N; i++) px[i] = i < 22 ? on : off;
            tex.SetPixels32(px); tex.Apply();
            return dashTexture = tex;
        }

        public static UIPhaseBanner BuildPhaseBanner(Transform canvas)
        {
            // #bn{left:0;right:0;top:calc(50% - 65px);height:140px} .tx{top:28px} b{60px/1.05} span{16px} small{12px}
            float bh = UITheme.Current.hud.bannerHeight;
            var root = MakeRect("PhaseBanner", canvas, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, 65), new Vector2(0, bh));
            root.gameObject.SetActive(false); // defer Awake() until every field below is wired
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var banner = root.gameObject.AddComponent<UIPhaseBanner>();

            // Phase 4-4 kit skin: ink .95 band cut on a slant (KitBannerStrip), two flowing dashed lines clipped to the visible band, blue
            // parallelograms at ±390, and the text block (small / title / kr) — see UIPhaseBanner for the motion.
            var theme = UITheme.Current;
            var strip = MakeRect("Strip", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var shapeRt = MakeRect("DecoShape", strip, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var shape = shapeRt.gameObject.AddComponent<KitBannerStrip>();
            shape.raycastTarget = false;
            shape.color = new Color(theme.ink.r, theme.ink.g, theme.ink.b, .95f);

            var maskBox = MakeRect("DecoLines", strip, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            maskBox.gameObject.AddComponent<RectMask2D>();
            var maskInner = MakeRect("DecoLinesInner", maskBox, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var lineTex = DashTexture();
            RawImage Line(string name, bool top)
            {
                var r = MakeRect(name, maskInner, new Vector2(0, top ? 1 : 0), new Vector2(1, top ? 1 : 0), new Vector2(0.5f, top ? 1 : 0), new Vector2(0, top ? -32 : 32), new Vector2(0, 3));
                var raw = r.gameObject.AddComponent<RawImage>();
                raw.texture = lineTex; raw.raycastTarget = false;
                return raw;
            }
            var lineTop = Line("DecoLineTop", true);
            var lineBottom = Line("DecoLineBottom", false);

            // .blk{top:42;bottom:42;width:22;skewX(-20deg)} at ±390 from the centre
            const float blkH = 56f;
            float skew = blkH * Mathf.Tan(20f * Mathf.Deg2Rad);
            Image Block(string name, float x)
            {
                var r = MakeRect(name, strip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(22 + skew, blkH));
                var img = AddImg(r, theme.blue);
                img.raycastTarget = false;
                r.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, skew);
                return img;
            }
            var imgL = Block("DecoBlockL", -390f);
            var imgR = Block("DecoBlockR", 390f);

            // .tx{top:28}: small 12 / b 60 / span 16
            var tx = MakeRect("Tx", strip, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var txGroup = tx.gameObject.AddComponent<CanvasGroup>();
            txGroup.blocksRaycasts = false;
            TMP_Text Row(string name, float y, float h, float size, Color color, UITheme.TextRole role)
            {
                var r = MakeRect(name, tx, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -y), new Vector2(700, h));
                var t = HudKit.AddText(r, "", size, color, false, TextAlignmentOptions.Top, "Text", role);
                t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
                t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                t.raycastTarget = false;
                return t;
            }
            var subTmp = Row("Small", 28f, 15f, 12f, Color.Lerp(theme.ink, Color.white, .65f), UITheme.TextRole.Display);
            subTmp.characterSpacing = 20f;                        // letter-spacing:.2em
            var mainTmp = Row("Title", 43f, 63f, 60f, Color.white, UITheme.TextRole.Display);
            var krTmp = Row("Kr", 106f, 20f, 16f, theme.blue, UITheme.TextRole.Body);

            banner.strip = strip; banner.group = group; banner.shape = shape;
            banner.maskBox = maskBox; banner.maskInner = maskInner; banner.stripeTop = lineTop; banner.stripeBottom = lineBottom;
            banner.tx = tx; banner.txGroup = txGroup;
            banner.mainText = mainTmp; banner.krText = krTmp; banner.subText = subTmp;
            banner.colorBlocks = new[] { imgL, imgR };
            banner.duration = 1.9f;
            root.gameObject.SetActive(true);
            return banner;
        }

        // Port of #contact / .ct-in (00_markup_and_styles.html:190-199, markup :470, filled by showContact):
        //   #contact{left:50%;top:18%;transform:translateX(-50%);pointer-events:none}
        //   .ct-in{display:flex;background:var(--ink);color:#fff;border-bottom:4px solid var(--foe)}  (.m1 blu, .m2 acc)
        //   .ct-mark{width:64px;font:700 44px var(--fd);background:var(--foe)}                           (.m2: color ink)
        //   .ct-in>div:last-child{padding:12px 26px 12px 16px}
        //   .ct-en{font:700 30px/1;letter-spacing:.2em} .ct-kr{font-weight:900;font-size:16px;margin-top:3px}
        //   .ct-sub{font-size:12px;opacity:.7;margin-top:2px}
        public static UIContactAlert BuildContactAlert(Transform canvas)
        {
            const float H = 4 + 12 + 30 + 3 + 22 + 2 + 17 + 12;   // border + padding + three text lines
            var root = MakeRect("ContactAlert", canvas, new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(10, H));
            root.gameObject.SetActive(false);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var alert = root.gameObject.AddComponent<UIContactAlert>();

            // Centre pivot so contactIn's scale(1.3→1) grows from the middle, top edge at 18%.
            var box = MakeRect("Box", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -H / 2f), new Vector2(420, H));
            // Phase 4-4 kit skin: the box is a parallelogram (slant 12), the colour mark leans with it, a hazard-stripe line runs along the top
            AddImg(box, UIPalette.Ink).raycastTarget = false;
            box.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            var border = AddImg(MakeRect("BorderBottom", box, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0, 4)), UIPalette.Foe);
            border.raycastTarget = false;
            border.rectTransform.offsetMax = new Vector2(-12f, border.rectTransform.offsetMax.y);   // the bottom edge ends where the slant does
            var hazardBar = MakeRect("DecoHazardBar", box, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 4));
            hazardBar.offsetMin = new Vector2(12f, hazardBar.offsetMin.y);
            var hazard = HazardStripe.Create(hazardBar, "DecoHazard", UIPalette.Foe, UITheme.Current.ink, 4f, 8f);
            hazard.scroll = false;

            var mark = MakeRect("Mark", box, Vector2.zero, new Vector2(0, 1), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            mark.offsetMin = new Vector2(0, 4); mark.offsetMax = new Vector2(64, 0);
            var markImg = AddImg(mark, UIPalette.Foe);
            markImg.raycastTarget = false;
            mark.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.LeftEdge, 11f);
            var markTmp = AddText(mark, "!", 44, Color.white, true);
            markTmp.raycastTarget = false;

            const float left = 64 + 16;
            TMP_Text Line(string name, string text, float y, float h, float size, Color color, bool bold)
            {
                var r = MakeRect(name, box, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, y), new Vector2(700, h));
                var t = AddText(r, text, size, color, bold);
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.raycastTarget = false;
                return t;
            }
            var titleTmp = Line("Title", "CONTACT", -12, 30, 30, Color.white, true);
            titleTmp.characterSpacing = 20;   // letter-spacing:.2em
            var glitch = titleTmp.gameObject.AddComponent<UIGlitchText>();
            glitch.source = titleTmp; glitch.loop = false;
            titleTmp.gameObject.AddComponent<LintIgnoreClip>().reason = "the glitch burst jitters the title by a few px";
            var krTmp = Line("Kr", "敵と遭遇しました！", -12 - 30 - 3, 22, 16, Color.white, true);
            var subTmp = Line("SubText", "", -12 - 30 - 3 - 22 - 2, 17, 12, new Color(1, 1, 1, .7f), false);
            UIFonts.Apply(subTmp, UITheme.TextRole.Body);

            alert.group = group; alert.box = box; alert.mark = mark; alert.subText = subTmp; alert.markText = markTmp;
            alert.borderAndMark = new[] { border, markImg };
            alert.fitTexts = new[] { titleTmp, krTmp, subTmp }; alert.fitLeft = left; alert.fitRight = 26; alert.fitMin = 200;
            alert.glitch = glitch; alert.hazard = hazard; alert.duration = 1.1f;
            root.gameObject.SetActive(true);
            return alert;
        }

        public static UIHeadshotBadge BuildHeadshotBadge(Transform canvas, Canvas canvasComp)
        {
            var root = MakeRect("HeadshotBadge", canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            root.gameObject.SetActive(false);
            var hs = root.gameObject.AddComponent<UIHeadshotBadge>();

            // Lock-on reticle (placed on the head): four corner brackets that close in, four crosshair arms, a centre dot.
            // 260 square: room for the brackets at their widest (110 + 18 from the centre) — it draws nothing itself
            var reticle = MakeRect("Reticle", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260));
            var reticleGroup = reticle.gameObject.AddComponent<CanvasGroup>();
            reticleGroup.blocksRaycasts = false; reticleGroup.interactable = false;
            var reticleGraphics = new System.Collections.Generic.List<Graphic>();
            const float bracketSize = 18f, line = 3f;
            var brackets = new RectTransform[4];
            string[] bracketNames = { "Bracket_TL", "Bracket_TR", "Bracket_BL", "Bracket_BR" };
            for (int i = 0; i < 4; i++)
            {
                bool right = i % 2 == 1, top = i < 2;
                var pivot = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
                var b = MakeRect(bracketNames[i], reticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pivot, Vector2.zero, new Vector2(bracketSize, bracketSize));
                var h = MakeRect("H", b, new Vector2(0, top ? 1 : 0), new Vector2(1, top ? 1 : 0), new Vector2(0.5f, top ? 1 : 0), Vector2.zero, new Vector2(0, line));
                var v = MakeRect("V", b, new Vector2(right ? 1 : 0, 0), new Vector2(right ? 1 : 0, 1), new Vector2(right ? 1 : 0, 0.5f), Vector2.zero, new Vector2(line, 0));
                reticleGraphics.Add(AddImg(h, UIPalette.Accent)); reticleGraphics.Add(AddImg(v, UIPalette.Accent));
                brackets[i] = b;
            }
            var arms = new RectTransform[4];
            const float armGap = 34f, armLen = 46f, armW = 2f;
            arms[0] = MakeRect("Arm_L", reticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-armGap, 0), new Vector2(armLen, armW));
            arms[1] = MakeRect("Arm_R", reticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(armGap, 0), new Vector2(armLen, armW));
            arms[2] = MakeRect("Arm_U", reticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0, armGap), new Vector2(armW, armLen));
            arms[3] = MakeRect("Arm_D", reticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -armGap), new Vector2(armW, armLen));
            foreach (var a in arms) reticleGraphics.Add(AddImg(a, UIPalette.Accent));
            var dot = MakeRect("Dot", reticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 4));
            reticleGraphics.Add(AddImg(dot, UIPalette.Accent));
            foreach (var g in reticleGraphics) g.raycastTarget = false;
            reticleGroup.alpha = 0f;

            var badge = MakeRect("Badge", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(260, 90));
            var group = badge.gameObject.AddComponent<CanvasGroup>();
            // Phase 4-4 kit skin: ink parallelogram (slant 12) behind everything, a hazard-stripe line along its bottom edge
            var bgRt = MakeRect("DecoBg", badge, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImg(bgRt, UITheme.Current.ink).raycastTarget = false;
            bgRt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            var hazardBar = MakeRect("DecoHazardBar", badge, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0, 5));
            hazardBar.offsetMax = new Vector2(-12f, hazardBar.offsetMax.y);
            var hazard = HazardStripe.Create(hazardBar, "DecoHazard", UIPalette.Accent, UITheme.Current.ink, 5f, 10f);
            hazard.scroll = false;
            var iconBg = MakeRect("IconBg", badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(28, 0), new Vector2(48, 48));
            var iconBgImg = AddImg(iconBg, UIPalette.Accent);
            iconBg.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            var icon = MakeRect("Icon", badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(28, 0), new Vector2(32, 32));
            AddText(icon, "+", 20, UIPalette.Ink, true);
            // pivot on the left so the sweep grows left → right; x 30 with that pivot is the same 30..260 line as before
            var underline = MakeRect("TextUnderline", badge, Vector2.zero, new Vector2(1, 0), new Vector2(0f, 0f), new Vector2(30, 26), new Vector2(-30, 3));
            var underlineImg = AddImg(underline, UIPalette.Accent);
            var title = MakeRect("Title", badge, new Vector2(0, 0.5f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(15, 0), new Vector2(-30, 36));
            var titleTmp = AddText(title, "HEADSHOT!", 24, UIPalette.Accent, true);
            var hsGlitch = titleTmp.gameObject.AddComponent<UIGlitchText>();
            hsGlitch.source = titleTmp; hsGlitch.loop = false;
            var detail = MakeRect("Detail", badge, Vector2.zero, new Vector2(1, 0.5f), new Vector2(0.5f, 1f), new Vector2(15, 0), new Vector2(-30, 24));
            var detailTmp = AddText(detail, "ヘッドショット · クリティカル x2 · -40", 12, Color.white, false);

            var flash = MakeRect("Flash", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var flashRaw = flash.gameObject.AddComponent<RawImage>();
            flashRaw.raycastTarget = false; flashRaw.color = new Color(1, 1, 1, 0);

            hs.badge = badge; hs.icon = icon; hs.iconBg = iconBgImg; hs.textUnderline = underlineImg;
            hs.title = titleTmp; hs.detail = detailTmp; hs.group = group; hs.flash = flashRaw;
            hs.canvas = canvasComp; hs.hazard = hazard; hs.glitch = hsGlitch;
            hs.reticle = reticle; hs.reticleGroup = reticleGroup; hs.brackets = brackets; hs.arms = arms; hs.reticleGraphics = reticleGraphics.ToArray();
            root.gameObject.SetActive(true);
            return hs;
        }

        /// <summary>The END TURN / START OP button's parts.</summary>
        public class EndTurn
        {
            public UIPulse pulse;
            public Button button;
            public TMP_Text main, sub;       // #endBtn's b / small (START OP ↔ END TURN)
        }

        // Port of #endBtn (00_markup_and_styles.html:159-167, markup :455):
        //   <button id="endBtn"><span class="l"><b>END TURN</b><small>ターン終了</small></span><span class="arw">▶▶</span></button>
        // 310x72 at right/bottom 14px, --blu background, a 62px --ink arrow block on the right, and the
        // ::before 150px rotated-square outline (18px, white .12) clipped by overflow:hidden.
        // .pulse (box-shadow 0→12px, --blu .7→0, 1.2s) is UIPulse's Glow image, a sibling *behind* the
        // button so growing it never resizes the button itself.
        // Phase 4-5 kit skin: KitEndButton (300×56 at right/bottom 20) under the same "EndTurn" root. The root keeps a UIPulse, now only a
        // switch (SetOn forwards to the button's pulse), so GameplayUIBridge / PlayerSelectionController / MatchBootstrap are untouched.
        public static EndTurn BuildEndTurn(Transform canvas, UnityEngine.Events.UnityAction onClick)
        {
            var w = new EndTurn();
            var root = MakeRect("EndTurn", canvas, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), UITheme.Current.hud.endTurn);
            root.gameObject.SetActive(false);
            var pulse = root.gameObject.AddComponent<UIPulse>();
            var kit = KitEndButton.Create(root);
            pulse.kit = kit;
            w.pulse = pulse; w.button = kit; w.main = kit.main; w.sub = kit.sub;
            if (onClick != null) kit.onClick.AddListener(onClick);
            kit.gameObject.SetActive(false);   // hidden outside the player's phase (GameplayUIBridge)
            root.gameObject.SetActive(true);
            pulse.SetOn(false);                // off until every player unit has acted (PlayerSelectionController.SyncEndButton)
            return w;
        }

        /// <summary>The pre-4-5 END TURN button (blue box + glow pulse), kept for reverting: rename it back to BuildEndTurn.</summary>
        public static EndTurn BuildEndTurnLegacy(Transform canvas, UnityEngine.Events.UnityAction onClick)
        {
            var w = new EndTurn();
            // #end{right:20px;bottom:20px} at UITheme.hud.endTurn (300×56)
            var endSize = UITheme.Current.hud.endTurn;
            var root = MakeRect("EndTurn", canvas, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), endSize);
            root.gameObject.SetActive(false);

            var glowRect = MakeRect("Glow", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, endSize);
            var glowImg = AddImg(glowRect, UIPalette.Blue);
            glowImg.raycastTarget = false; glowImg.enabled = false;
            var pulse = root.gameObject.AddComponent<UIPulse>();
            pulse.glow = glowImg; pulse.period = 1.2f; pulse.growPixels = 12f;
            w.pulse = pulse;

            // #endBtn onclick — closes out the player's Attack/Defense phase. Hidden outside their phase.
            var btnRect = MakeRect("Button", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bg = AddImg(btnRect, UIPalette.Blue);
            btnRect.gameObject.AddComponent<RectMask2D>(); // overflow:hidden
            var endTurnButton = btnRect.gameObject.AddComponent<Button>();
            w.button = endTurnButton;
            endTurnButton.targetGraphic = bg;
            var colors = endTurnButton.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f); // :disabled brightness(.5)
            endTurnButton.colors = colors;
            if (onClick != null) endTurnButton.onClick.AddListener(onClick);

            // ::before — right:-20px; top:-30px; 150x150; border:18px solid rgba(255,255,255,.12); rotate(45deg)
            var deco = MakeRect("Deco", btnRect, Vector2.one, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(20 - 75, 30 - 75), new Vector2(150, 150));
            deco.localRotation = Quaternion.Euler(0, 0, 45);
            deco.gameObject.AddComponent<LintIgnoreOverlap>().bounds = true;   // the clipped outline is meant to run out of the button
            var decoCol = new Color(1, 1, 1, 0.12f);
            foreach (var (aMin, aMax, pivot) in new[] {
                (new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1)),   // top
                (new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0)),   // bottom
                (new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f)),   // left
                (new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f)) }) // right
            {
                bool horiz = aMin.y == aMax.y;
                var edge = MakeRect("Edge", deco, aMin, aMax, pivot, Vector2.zero,
                    horiz ? new Vector2(0, 18) : new Vector2(18, -36)); // side edges skip the corners
                AddImg(edge, decoCol).raycastTarget = false;
            }

            // #end .l{padding:9px 20px 0 36px}; b{22px/1}; small{12px/1.25}; .a{width:60px;20px}
            const float ArrowW = 60f;
            var label = MakeRect("Label", btnRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            label.offsetMin = new Vector2(36, 0); label.offsetMax = new Vector2(-(ArrowW + 20), -9);
            var mainRect = MakeRect("Main", label, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 22));
            var mainTmp = AddText(mainRect, "END TURN", 22, Color.white, true);
            w.main = mainTmp;
            mainTmp.alignment = TextAlignmentOptions.Left; mainTmp.characterSpacing = 10; mainTmp.raycastTarget = false;
            var subRect = MakeRect("Sub", label, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(0, 15));
            var subTmp = AddText(subRect, "ターン終了", 12, new Color(1, 1, 1, 0.85f), true);
            w.sub = subTmp;
            subTmp.alignment = TextAlignmentOptions.Left; subTmp.raycastTarget = false;

            var arw = MakeRect("Arrow", btnRect, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(ArrowW, 0));
            AddImg(arw, UIPalette.Ink).raycastTarget = false;
            var arwTmp = AddText(arw, "▶▶", 20, Color.white, true);
            arwTmp.characterSpacing = -10; arwTmp.raycastTarget = false;

            endTurnButton.gameObject.SetActive(false);
            root.gameObject.SetActive(true);
            pulse.SetOn(false); // off until every player unit has acted (PlayerSelectionController.SyncEndButton)
            return w;
        }
    }
}
