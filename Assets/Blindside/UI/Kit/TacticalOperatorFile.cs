// The battle's operator file (the user's design, replacing KitArtCard — now UI/Legacy): 300×500 at right 20 / top 76 of the HUD
// canvas, shown while one of the squad is selected.
//   Body
//     Tag         ink 26: a blinking dot in the operator colour, "OPERATOR FILE // OP-004 // ASSAULT" (display 11, grey)
//     Art         the illustration window (cover, top-pinned, RectMask2D): the paper gradient, the Photo (UIGlitchImage CardEnter),
//                 a light band running down it (.3s → .85s), scanlines; corner brackets top-left / bottom-right
//     DecoLine    4 in the operator colour
//     Panel       ink 150:
//                   Name     English name, Chakra Petch Medium 34, letter-spaced          Level  "LV. 5", Chakra Petch Bold 22, right
//                   HpLabel  "HP", Chakra Petch Bold 18                                    Hp     "100/140" (the max smaller), right
//                   Bar      7 big slanted segments: the operator colour full, white empty, red ghost on a hit
//                   Gun      the weapon's silhouette (GunIcons), white                    Weapon its short name, Chakra Petch Bold 22, right
// Show plays the .8s entrance (the photo's glitch and its shake, the light band, the card sliding in 24 from the right) unless the same
// operator is already up; SetHp / SetWeapon follow the unit while it is shown. Evaluate(t) poses the entrance; Update only advances an
// unscaled clock. Texts change only in the setters; nothing is allocated per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Tactical Operator File")]
    public class TacticalOperatorFile : MonoBehaviour, ITimedEffect
    {
        public const float Width = 300f, Height = 500f, Right = 20f, Top = 76f, TagH = 26f, PanelH = 150f, LineH = 4f;
        public const float EnterTime = .8f, SlideTime = .35f, SlideIn = 24f, SweepStart = .3f, SweepTime = .55f, FlashTime = .5f;
        public const float BlinkPeriod = 1f, BlinkLow = .25f;
        public const int Segments = 7;
        public const float Scale = .85f;
        const float Pad = 18f, Never = -100f, NameW = 200f - 8f - Pad, NameSize = 34f;   // (the name stops short of LV)
        static readonly Color ArtMid = new Color32(0xE9, 0xEC, 0xEE, 0xFF);
        static readonly Color TagGrey = new Color32(0xB9, 0xBF, 0xC6, 0xFF);

        [Header("Parts")]
        public RectTransform body, art;
        public CanvasGroup group;
        public UIGlitchImage photo;
        public KitTint flash;
        public KitSweep sweep;
        public Image line, tagDot, gun;
        public TMP_Text tagText, nameText, levelText, hpLabel, hpText, weaponText;
        public SegmentedBar bar;

        Color opColor = Color.white;
        string current;
        int hp = -1, maxHp = 1;
        string weaponShown;
        float clock, enterStart = Never, blink;
        bool running, dotLow;

        public bool Visible => gameObject.activeSelf;
        public float Duration => EnterTime;

        // ---- API ----------------------------------------------------------------------------------------------------------------
        /// <summary>Shows the operator and plays the entrance — unless the same operator (`key`) is already on screen.</summary>
        public void Show(string key, Sprite illustration, string nameEn, int level, int currentHp, int maxHpValue,
                         string weaponLabel, Sprite gunSprite, string tag, Color color)
        {
            bool same = current == key && Visible;
            if (!same)
            {
                current = key;
                gameObject.SetActive(true);
                opColor = color;
                tagText.text = tag ?? "";
                nameText.text = nameEn ?? "";
                // one line short of LV: as it is, else tighter, else smaller
                nameText.fontSize = NameSize; nameText.characterSpacing = 14f;
                float nw = nameText.GetPreferredValues(nameText.text).x;
                if (nw > NameW) { nameText.characterSpacing = 4f; nw = nameText.GetPreferredValues(nameText.text).x; }
                if (nw > NameW) nameText.fontSize = Mathf.Max(20f, Mathf.Floor(NameSize * NameW / nw));
                levelText.SetText("LV. {0:0}", Mathf.Max(1, level));
                line.color = color;
                dotLow = false; tagDot.color = color;
                SetPhoto(illustration);
                hp = -1;
            }
            SetHp(currentHp, maxHpValue);
            SetWeapon(weaponLabel, gunSprite);
            if (!same) Play();
        }

        public void Hide()
        {
            running = false; current = null;
            gameObject.SetActive(false);
        }

        /// <summary>The HP number and bar (a drop shows the bar's red ghost). Nothing happens when unchanged.</summary>
        public void SetHp(int currentHp, int maxHpValue)
        {
            int max = Mathf.Max(1, maxHpValue), cur = Mathf.Clamp(currentHp, 0, max);
            if (cur == hp && max == maxHp) return;
            bool drop = hp >= 0 && max == maxHp && cur < hp;
            float from = hp >= 0 ? (float)hp / maxHp : (float)cur / max;
            hp = cur; maxHp = max;
            hpText.SetText("{0:0}<size=72%>/{1:0}</size>", (float)cur, (float)max);
            bar.SetColors(opColor, UITheme.Current.foe, Color.white);
            if (drop && Application.isPlaying) bar.SetTarget((float)cur / max);
            else bar.SetImmediate((float)cur / max);
            if (from < 0f) bar.SetImmediate((float)cur / max);
        }

        public void SetWeapon(string label, Sprite gunSprite)
        {
            label = label ?? "";
            if (label == weaponShown && gun.sprite == gunSprite) return;
            weaponShown = label;
            weaponText.text = label;
            gun.sprite = gunSprite;
            gun.enabled = gunSprite != null;
        }

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
            if (picAspect > winAspect) { w = r.height * winAspect; x = r.x + (r.width - w) * .5f; }
            else { h = r.width / winAspect; yb = r.y + (r.height - h); }
            photo.SetTexture(tex, new Rect(x / tex.width, yb / tex.height, w / tex.width, h / tex.height));
            photo.color = Color.white;
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public void Evaluate(float t)
        {
            float e = t - enterStart;
            if (e < 0f) e = EnterTime;
            bool reduce = UIMotionSettings.reduceMotion;

            photo.preset = UIGlitchImage.Preset.CardEnter;
            Vector2 shake = photo.Evaluate(e);
            float slide = reduce ? 0f : SlideIn * (1f - UIEase.Tactical(Mathf.Clamp01(e / SlideTime)));
            var pos = new Vector2(shake.x + slide, shake.y);
            if (body.anchoredPosition != pos) body.anchoredPosition = pos;
            float a = reduce ? 1f : Mathf.Clamp01(e / (SlideTime * .6f));
            if (group.alpha != a) group.alpha = a;

            float b = e >= FlashTime ? 1f : e < FlashTime * .4f ? Mathf.Lerp(1.8f, 1.15f, e / (FlashTime * .4f)) : Mathf.Lerp(1.15f, 1f, (e - FlashTime * .4f) / (FlashTime * .6f));
            float lift = Mathf.Clamp01((b - 1f) * ArtMid.r / Mathf.Max(.001f, 1f - ArtMid.r));
            flash.Set(lift > 0f ? new Color(1f, 1f, 1f, lift) : Color.clear, ArtMid);

            float s = (e - SweepStart) / SweepTime;
            if (s > 0f && s < 1f && !reduce)
            {
                float p = s * s;
                sweep.Evaluate(sweep.duration * (1f - Mathf.Pow(1f - p, .25f)));
            }
            else sweep.Rest();
        }

        void Update()
        {
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
        static TMP_Text CardText(Transform parent, string name, float size, TextAlignmentOptions align, TMP_FontAsset font, Color c)
        {
            var t = HudKit.AddText(parent, "", size, c, true, align, name, UITheme.TextRole.Display);
            if (font != null) t.font = font;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>Builds the card on this object (anchored at the HUD's top right), hidden.</summary>
        public static TacticalOperatorFile Create(Transform parent, string name)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect(name, parent);
            UILayout.Anchor(root, new Vector2(1f, 1f), -Right, -Top, Width, Height);
            root.localScale = new Vector3(Scale, Scale, 1f);                    // a little smaller than the design, about its top-right
            var f = root.gameObject.AddComponent<TacticalOperatorFile>();
            f.group = root.gameObject.AddComponent<CanvasGroup>();
            f.group.blocksRaycasts = false; f.group.interactable = false;      // never swallows battlefield clicks

            var body = HudKit.NewRect("Body", root);
            HudKit.Stretch(body);
            f.body = body;
            var bg = HudKit.NewRect("DecoBg", body);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, theme.ink);
            KitShadow.Behind(bg, new Color32(0x2b, 0x2f, 0x35, 0xff), .55f, 14f, 34f);

            // ---- the illustration ----
            float artTop = TagH, artH = Height - TagH - PanelH - LineH;
            var art = HudKit.NewRect("Art", body);
            UILayout.Place(art, 0f, artTop, Width, artH);
            art.gameObject.AddComponent<RectMask2D>();
            f.art = art;
            var abg = HudKit.NewRect("DecoArtBg", art);
            HudKit.Stretch(abg);
            HudKit.AddImage(abg, Color.white).sprite = KitArtCard.ArtGradient;
            var fr = HudKit.NewRect("DecoFlash", art);
            HudKit.Stretch(fr);
            f.flash = fr.gameObject.AddComponent<KitTint>();
            f.flash.raycastTarget = false;
            var pr = HudKit.NewRect("Photo", art);
            HudKit.Stretch(pr);
            f.photo = pr.gameObject.AddComponent<UIGlitchImage>();
            f.photo.raycastTarget = false;
            f.photo.preset = UIGlitchImage.Preset.CardEnter;
            f.photo.color = new Color(1f, 1f, 1f, 0f);
            var sr = HudKit.NewRect("DecoSweep", art);
            sr.anchorMin = sr.anchorMax = new Vector2(.5f, .5f); sr.pivot = new Vector2(.5f, .5f);
            sr.sizeDelta = new Vector2(artH, Width); sr.localRotation = Quaternion.Euler(0f, 0f, -90f);
            f.sweep = sr.gameObject.AddComponent<KitSweep>();
            f.sweep.raycastTarget = false;
            f.sweep.lightColor = new Color(1f, 1f, 1f, .5f);
            f.sweep.duration = 1f;
            f.sweep.over = ArtMid;
            f.sweep.Rest();
            var sc = HudKit.NewRect("DecoScan", art);
            HudKit.Stretch(sc);
            var scan = sc.gameObject.AddComponent<RawImage>();
            scan.raycastTarget = false;
            scan.texture = KitArtCard.ScanTexture;
            scan.uvRect = new Rect(0f, 0f, 1f, artH / 4f);
            var brRt = HudKit.NewRect("DecoBrackets", body);
            UILayout.Place(brRt, 12f, artTop + 12f, Width - 24f, artH - 24f);
            CornerBrackets.Create(brRt, "DecoBracketPair", new Color(theme.ink.r, theme.ink.g, theme.ink.b, .75f), 20f, 3f, 0f, ArtMid);

            // ---- the tag ----
            var tg = HudKit.NewRect("Tag", body);
            UILayout.Place(tg, 0f, 0f, Width, TagH);
            HudKit.AddImage(tg, theme.ink);
            var dr = HudKit.NewRect("TagDot", tg);
            UILayout.Place(dr, 14f, (TagH - 6f) * .5f, 6f, 6f);
            f.tagDot = HudKit.AddImage(dr, theme.blue);
            f.tagText = HudKit.AddText(tg, "", 11f, TagGrey, true, TextAlignmentOptions.MidlineLeft, "TagText", UITheme.TextRole.Display);
            f.tagText.characterSpacing = 12f;
            f.tagText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(f.tagText.rectTransform, 27f, 0f, Width - 27f - 8f, TagH);

            // ---- the colour line and the panel ----
            var lr = HudKit.NewRect("DecoLine", body);
            UILayout.Place(lr, 0f, artTop + artH, Width, LineH);
            f.line = HudKit.AddImage(lr, theme.blue);

            float py = Height - PanelH;
            var nm = CardText(body, "Name", 34f, TextAlignmentOptions.MidlineLeft, theme.cardFontMedium, Color.white);
            nm.characterSpacing = 14f;
            UILayout.Place(nm.rectTransform, Pad, py + 8f, NameW, 44f);
            f.nameText = nm;
            f.levelText = CardText(body, "Level", 22f, TextAlignmentOptions.MidlineRight, theme.cardFontBold, Color.white);
            UILayout.Place(f.levelText.rectTransform, 200f, py + 12f, Width - Pad - 200f, 36f);

            f.hpLabel = CardText(body, "HpLabel", 18f, TextAlignmentOptions.BottomLeft, theme.cardFontBold, Color.white);
            f.hpLabel.text = "HP";
            UILayout.Place(f.hpLabel.rectTransform, Pad, py + 54f, 60f, 24f);
            f.hpText = CardText(body, "Hp", 18f, TextAlignmentOptions.BottomRight, theme.cardFontBold, Color.white);
            UILayout.Place(f.hpText.rectTransform, 120f, py + 54f, Width - Pad - 120f, 24f);

            var barRt = HudKit.NewRect("Bar", body);
            float bw = Width - Pad * 2f, gap = 6f;
            UILayout.Place(barRt, Pad, py + 82f, bw, 14f);
            f.bar = barRt.gameObject.AddComponent<SegmentedBar>();
            f.bar.style = SegmentedBar.Style.Custom;
            f.bar.customGap = gap;
            f.bar.customSegment = (bw - gap * (Segments - 1)) / Segments;
            f.bar.raycastTarget = false;
            barRt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 6f);

            var grt = HudKit.NewRect("Gun", body);
            UILayout.Place(grt, Pad, py + 106f, 150f, 34f);
            f.gun = grt.gameObject.AddComponent<Image>();
            f.gun.preserveAspect = true; f.gun.raycastTarget = false; f.gun.color = Color.white; f.gun.enabled = false;
            f.weaponText = CardText(body, "Weapon", 22f, TextAlignmentOptions.MidlineRight, theme.cardFontBold, Color.white);
            UILayout.Place(f.weaponText.rectTransform, 176f, py + 106f, Width - Pad - 176f, 34f);

            root.gameObject.SetActive(false);
            return f;
        }
    }
}
