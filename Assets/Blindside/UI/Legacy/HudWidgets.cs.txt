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

        public static UIPhaseBanner BuildPhaseBanner(Transform canvas)
        {
            // #bn{left:0;right:0;top:calc(50% - 65px);height:140px} .tx{top:28px} b{60px/1.05} span{16px} small{12px}
            float bh = UITheme.Current.hud.bannerHeight;
            var root = MakeRect("PhaseBanner", canvas, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, 65), new Vector2(0, bh));
            root.gameObject.SetActive(false); // defer Awake() until every field below is wired
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var banner = root.gameObject.AddComponent<UIPhaseBanner>();

            var strip = MakeRect("Strip", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImg(strip, Ink2);
            var blockL = MakeRect("ColorBlockL", strip, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 0));
            var imgL = AddImg(blockL, UIPalette.Blue);
            var blockR = MakeRect("ColorBlockR", strip, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(10, 0));
            var imgR = AddImg(blockR, UIPalette.Blue);
            var mainT = MakeRect("MainText", strip, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(-40, 63));
            var mainTmp = AddText(mainT, "ATTACKER PHASE", 60, Color.white, true);
            var krT = MakeRect("KrText", strip, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -91), new Vector2(-40, 20));
            var krTmp = AddText(krT, "攻撃ターン", 16, UIPalette.Blue, true);
            var subT = MakeRect("SubText", strip, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -111), new Vector2(-40, 15));
            var subTmp = AddText(subT, "部隊を展開してください", 12, UIPalette.Dim, false);

            banner.strip = strip; banner.group = group;
            banner.mainText = mainTmp; banner.krText = krTmp; banner.subText = subTmp;
            banner.colorBlocks = new[] { imgL, imgR };
            banner.duration = 1.7f;
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
            AddImg(box, UIPalette.Ink).raycastTarget = false;
            var border = AddImg(MakeRect("BorderBottom", box, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0, 4)), UIPalette.Foe);
            border.raycastTarget = false;

            var mark = MakeRect("Mark", box, Vector2.zero, new Vector2(0, 1), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            mark.offsetMin = new Vector2(0, 4); mark.offsetMax = new Vector2(64, 0);
            var markImg = AddImg(mark, UIPalette.Foe);
            markImg.raycastTarget = false;
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
            var krTmp = Line("Kr", "敵と遭遇しました！", -12 - 30 - 3, 22, 16, Color.white, true);
            var subTmp = Line("SubText", "", -12 - 30 - 3 - 22 - 2, 17, 12, new Color(1, 1, 1, .7f), false);
            UIFonts.Apply(subTmp, UITheme.TextRole.Body);

            alert.group = group; alert.box = box; alert.mark = mark; alert.subText = subTmp; alert.markText = markTmp;
            alert.borderAndMark = new[] { border, markImg };
            alert.fitTexts = new[] { titleTmp, krTmp, subTmp }; alert.fitLeft = left; alert.fitRight = 26; alert.fitMin = 200;
            alert.glitch = glitch; alert.duration = 1.1f;
            root.gameObject.SetActive(true);
            return alert;
        }

        public static UIHeadshotBadge BuildHeadshotBadge(Transform canvas, Canvas canvasComp)
        {
            var root = MakeRect("HeadshotBadge", canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            root.gameObject.SetActive(false);
            var hs = root.gameObject.AddComponent<UIHeadshotBadge>();

            var badge = MakeRect("Badge", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(260, 90));
            var group = badge.gameObject.AddComponent<CanvasGroup>();
            var iconBg = MakeRect("IconBg", badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(28, 0), new Vector2(48, 48));
            var iconBgImg = AddImg(iconBg, UIPalette.Accent);
            var icon = MakeRect("Icon", badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(28, 0), new Vector2(32, 32));
            AddText(icon, "+", 20, UIPalette.Ink, true);
            var underline = MakeRect("TextUnderline", badge, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(30, 26), new Vector2(-30, 3));
            var underlineImg = AddImg(underline, UIPalette.Accent);
            var title = MakeRect("Title", badge, new Vector2(0, 0.5f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(30, 0), new Vector2(-30, 36));
            var titleTmp = AddText(title, "HEADSHOT!", 24, UIPalette.Accent, true);
            var detail = MakeRect("Detail", badge, Vector2.zero, new Vector2(1, 0.5f), new Vector2(0.5f, 1f), new Vector2(30, 0), new Vector2(-30, 24));
            var detailTmp = AddText(detail, "ヘッドショット · クリティカル x2 · -40", 12, Color.white, false);

            var flash = MakeRect("Flash", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var flashRaw = flash.gameObject.AddComponent<RawImage>();
            flashRaw.raycastTarget = false; flashRaw.color = new Color(1, 1, 1, 0);

            hs.badge = badge; hs.icon = icon; hs.iconBg = iconBgImg; hs.textUnderline = underlineImg;
            hs.title = titleTmp; hs.detail = detailTmp; hs.group = group; hs.flash = flashRaw;
            hs.canvas = canvasComp;
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
        public static EndTurn BuildEndTurn(Transform canvas, UnityEngine.Events.UnityAction onClick)
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
