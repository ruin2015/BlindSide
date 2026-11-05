// A won operation's ending (MatchBootstrap.FinishRoutine → Play(report)), on its own Screen Space Overlay canvas (menu 1440×810, order
// 100 like the old result screen), one unscaled clock (ITimedEffect.Evaluate(t)):
//   (it starts once the finishing exchange's cinematic is over and the camera is back on the map — MatchBootstrap waits for it)
//   0 – 2.6s   the radio alone, the map still in view: the squad leader (attack: CRUSHER) says 「作戦完了。みんな、ご苦労だった。」
//              (TutorialRadio, Play mode only)
//   2.6 – 5.8s 作戦成功 in the middle of the screen: an ink band 150 high opens (.35s), "OPERATION SUCCESS" 14 display (blue) over
//              「作戦成功」 64 Title (white) with a glitch burst; the band closes from 5.2s. (No ATTACKER / DEFENDER VICTORY banner then.)
//              A click skips: the radio to the banner, the banner to the result.
//   5.8s –     the result over a dimmed screen (rgba(4,6,9,.97)):
//     Head     "OPERATION RESULT" 12 display (blue) · 「作戦成功」 30 Title · the stage's name, at x 112 y 34
//     Cards    one card per operator of the player's side (232 wide, 14 apart, centred, y 108): the photo (OperatorArt.Body, cover,
//              top-pinned, greyed with 戦闘不能 when down), name 22 display + kana, Lv + "+N EXP" and its bar, the weapon's mastery and
//              its bar, then the record — 受けたダメージ / 攻撃回数 / 破壊した壁 / 撃破 / アシスト. The MVP's card has a 4px gold frame and
//              the 最優秀隊員 chip. Cards come in 80ms apart (.5s, fade + 24 up); the bars fill from .9s to 2.4s through every level
//              gained (LEVEL UP / MASTERY UP chips light when they cross)
//     Reward   報酬 ¥ counting up 2.4 → 3.2s (bottom left)
//     Rank     貢献度 +N and the rank bar filling from the old points to the new ones 2.6 → 3.8s (bottom centre); a promotion lights
//              「昇進」 and the new rank's name
//     Button   「本部へ戻る」 (TacticalButton 300×52, bottom right) fades in at 4.0s; a click before that skips the counting
// The numbers are the report's (MatchAward, already in the save): the screen only plays them back. No allocation per frame (texts change
// through SetText with numbers, or a char buffer for the yen).
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Progress;

namespace Blindside.UI
{
    public class OperationResultScreen : MonoBehaviour, ITimedEffect, IPointerClickHandler
    {
        public const float ScreenW = 1440f, ScreenH = 810f;
        // the radio alone first (RadioEnd), then 作戦成功 (from BannerFrom), then the result
        public const float RadioEnd = 2.6f, BannerFrom = 2.6f, BannerIn = .35f, BannerOut = BannerFrom + 2.6f, BannerEnd = BannerFrom + 3.0f, ResultStart = BannerFrom + 3.2f;
        public const float CardW = 232f, CardGap = 14f, CardY = 108f, PhotoH = 186f, NameH = 54f;
        public const float CardIn = .5f, CardStep = .08f, FillFrom = .9f, FillTo = 2.4f, MoneyFrom = 2.4f, MoneyTo = 3.2f, RankFrom = 2.6f, RankTo = 3.8f, ButtonAt = 4.0f;
        public const string RadioLine = "作戦完了。みんな、ご苦労だった。";
        static readonly Color DimC = new Color(4f / 255f, 6f / 255f, 9f / 255f, .97f);   // (linear blending: .97 reads as the mockup's deep dim)
        static readonly Color Gold = UIPalette.Hex("#FFC81E");
        static readonly Color StatC = UIPalette.Hex("#9AA3AD");
        static readonly string[] StatLabels = { "受けたダメージ", "攻撃回数", "破壊した壁", "撃破", "アシスト" };

        /// <summary>Previews draw into a canvas of the stage instead of the overlay this component makes.</summary>
        public Canvas externalCanvas;

        Canvas canvas;
        RectTransform root, bannerBand, resultRoot;
        CanvasGroup bannerGroup, resultGroup, buttonGroup;
        TMP_Text bannerTitle;
        UIGlitchText bannerGlitch;
        Image dim;
        AwardReport report;
        Action onHq;
        Blindside.Tutorial.TutorialRadio radio;
        float clock; bool running, radioClosed, bannerBurst;

        class Card
        {
            public RectTransform rt; public CanvasGroup group; public Vector2 home;
            public TMP_Text level, mastery; public RectTransform levelFill, masteryFill; public GameObject levelUp, masteryUp;
            public AwardLine line;
            public int shownLevel = -1, shownMastery = -1;
        }
        Card[] cards = new Card[0];

        TMP_Text moneyText, rankName, rankPoints;
        RectTransform rankFill;
        GameObject promoted;
        readonly char[] yen = new char[24];
        int shownMoney = -1, shownRank = -1, shownPoints = -1;
        string[] rankNames;

        public bool Visible => canvas != null && canvas.gameObject.activeSelf;
        public float Duration => ResultStart + ButtonAt + .4f;

        // ---- flow --------------------------------------------------------------------------------------------------------------
        /// <summary>Plays the ending of `report` (a won match of `side`); 本部へ戻る calls `onHq`.</summary>
        public void Play(AwardReport report, OperatorTeam side, Action onHq)
        {
            Build(report, onHq);
            clock = 0f; running = Application.isPlaying; radioClosed = false; bannerBurst = false;
            if (Application.isPlaying)
            {
                if (radio == null) radio = Blindside.Tutorial.TutorialRadio.Build(transform);
                var leader = report.lines.Count > 0 ? BuildAssetRegistry.Operator(report.lines[0].rec.key) : null;
                if (side == OperatorTeam.Attack || leader == null) radio.SetSpeaker(false);
                else radio.SetOperator(leader.key, leader.DisplayName, leader.kr, leader.color);
                radio.Say(RadioLine, false);
            }
            Evaluate(0f);
        }

        /// <summary>Previews (edit mode): `t` seconds into the ending.</summary>
        public void PoseAt(AwardReport report, float t)
        {
            Build(report, null);
            clock = t; running = false;
            Evaluate(t);
        }

        public void Hide()
        {
            running = false;
            if (radio != null && radio.IsOpen) radio.Close();
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            if (!radioClosed && clock >= RadioEnd) { radioClosed = true; if (radio != null && radio.IsOpen) radio.Close(); }
            if (!bannerBurst && clock >= BannerFrom) { bannerBurst = true; if (bannerGlitch != null && Application.isPlaying) bannerGlitch.Burst(.8f); }
            if (clock >= Duration) { clock = Duration; running = false; }
            Evaluate(clock);
        }

        public void OnPointerClick(PointerEventData e)
        {
            // a click skips: the radio to the banner, the banner to the result, the result's counting to its end
            if (clock < BannerFrom) clock = BannerFrom;
            else if (clock < ResultStart) clock = ResultStart;
            else if (clock < ResultStart + ButtonAt) clock = ResultStart + ButtonAt;
            if (!radioClosed && clock >= RadioEnd) { radioClosed = true; if (radio != null && radio.IsOpen) radio.Close(); }
            if (!running && Application.isPlaying) running = true;
            Evaluate(clock);
        }

        // ---- motion ------------------------------------------------------------------------------------------------------------
        public void Evaluate(float t)
        {
            if (root == null) return;
            bool reduce = UIMotionSettings.reduceMotion;

            // the banner
            float tb = t - BannerFrom;
            float bIn = Mathf.Clamp01(tb / BannerIn), bOut = Mathf.Clamp01((t - BannerOut) / (BannerEnd - BannerOut));
            float ba = tb < 0f ? 0f : t < BannerOut ? (reduce ? 1f : UIEase.Tactical(bIn)) : 1f - bOut;
            if (bannerGroup.alpha != ba) bannerGroup.alpha = ba;
            var bs = new Vector3(1f, reduce ? 1f : Mathf.Max(.02f, UIEase.Tactical(bIn)), 1f);
            if (bannerBand.localScale != bs) bannerBand.localScale = bs;
            bool showBanner = tb >= 0f && t < BannerEnd;
            if (bannerBand.gameObject.activeSelf != showBanner) bannerBand.gameObject.SetActive(showBanner);

            // the dim: light under the banner, deep under the result
            float r = t - ResultStart;
            // the dim: none under the radio (the map stays in view), light under the banner, deep under the result
            var dc = DimC; dc.a = t < ResultStart ? .35f * Mathf.Clamp01(tb / BannerIn) : Mathf.Lerp(.35f, DimC.a, Mathf.Clamp01(r / .4f));
            if (dim.color != dc) dim.color = dc;

            bool showResult = r >= 0f;
            if (resultRoot.gameObject.activeSelf != showResult) resultRoot.gameObject.SetActive(showResult);
            if (!showResult) return;
            float ra = reduce ? 1f : UIEase.Tactical(Mathf.Clamp01(r / .4f));
            if (resultGroup.alpha != ra) resultGroup.alpha = ra;

            // cards
            float fill = Mathf.Clamp01((r - FillFrom) / (FillTo - FillFrom));
            float fe = reduce ? (r >= FillFrom ? 1f : 0f) : UIEase.Tactical(fill);
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                float p = Mathf.Clamp01((r - i * CardStep) / CardIn);
                float e = reduce ? (p > 0f ? 1f : 0f) : UIEase.Tactical(p);
                if (c.group.alpha != e) c.group.alpha = e;
                var pos = c.home + new Vector2(0f, reduce ? 0f : -24f * (1f - e));
                if (c.rt.anchoredPosition != pos) c.rt.anchoredPosition = pos;
                PoseBar(c, fe);
            }

            // the reward
            float me = UIEase.Tactical(Mathf.Clamp01((r - MoneyFrom) / (MoneyTo - MoneyFrom)));
            int money = Mathf.RoundToInt(report.money * (reduce ? (r >= MoneyFrom ? 1f : 0f) : me));
            if (money != shownMoney) { shownMoney = money; int n = FormatYen(money, yen); moneyText.SetText(yen, 0, n); }

            // the rank
            float ke = UIEase.Tactical(Mathf.Clamp01((r - RankFrom) / (RankTo - RankFrom)));
            if (reduce) ke = r >= RankFrom ? 1f : 0f;
            int pts = report.pointsFrom + Mathf.RoundToInt(report.contribution * ke);
            int rank = RankTable.RankOf(pts);
            if (rank != shownRank) { shownRank = rank; rankName.SetText(rankNames[Mathf.Clamp(rank, 0, rankNames.Length - 1)]); }
            if (pts != shownPoints)
            {
                shownPoints = pts;
                if (rank >= RankTable.Top) rankPoints.SetText("{0} pt  MAX", (float)pts);
                else rankPoints.SetText("{0} / {1} pt", (float)pts, (float)RankTable.Threshold(rank + 1));
                float rp = RankTable.Progress(pts, rank);
                rankFill.anchorMax = new Vector2(rp, 1f);
            }
            bool promo = rank > report.rankFrom;
            if (promoted.activeSelf != promo) promoted.SetActive(promo);

            // the button
            float be = reduce ? (r >= ButtonAt ? 1f : 0f) : UIEase.Tactical(Mathf.Clamp01((r - ButtonAt) / .4f));
            if (buttonGroup.alpha != be) buttonGroup.alpha = be;
            bool clickable = be >= 1f;
            if (buttonGroup.blocksRaycasts != clickable) { buttonGroup.blocksRaycasts = clickable; buttonGroup.interactable = clickable; }
        }

        // one card's level and mastery at fraction `p` of the experience gained
        void PoseBar(Card c, float p)
        {
            var l = c.line;
            int add = Mathf.RoundToInt(l.exp * p);
            int lv = l.levelFrom, ex = l.expFrom;
            ProgressRules.AddExp(ref lv, ref ex, add, ProgressRules.LevelNeed, ProgressRules.MaxLevel);
            int need = ProgressRules.LevelNeed(lv);
            c.levelFill.anchorMax = new Vector2(need > 0 ? Mathf.Clamp01(ex / (float)need) : 1f, 1f);
            if (lv != c.shownLevel) { c.shownLevel = lv; c.level.SetText("{0}", (float)lv); }
            bool up = lv > l.levelFrom;
            if (c.levelUp.activeSelf != up) c.levelUp.SetActive(up);

            if (c.mastery == null) return;
            int wl = l.wLevelFrom, wx = l.wExpFrom;
            ProgressRules.AddExp(ref wl, ref wx, add, ProgressRules.MasteryNeed, ProgressRules.MaxMastery);
            int wneed = ProgressRules.MasteryNeed(wl);
            c.masteryFill.anchorMax = new Vector2(wneed > 0 ? Mathf.Clamp01(wx / (float)wneed) : 1f, 1f);
            if (wl != c.shownMastery) { c.shownMastery = wl; c.mastery.SetText("熟練度 Lv {0}", (float)wl); }
            bool wup = wl > l.wLevelFrom;
            if (c.masteryUp.activeSelf != wup) c.masteryUp.SetActive(wup);
        }

        static readonly char[] digits = new char[20];

        // "¥30,000" into `buf`, no allocation; returns the length
        static int FormatYen(int v, char[] buf)
        {
            v = Mathf.Max(0, v);
            int n = 0;
            var tmp = digits;
            int d = 0;
            do { if (d > 0 && d % 3 == 0) tmp[n++] = ','; tmp[n++] = (char)('0' + v % 10); v /= 10; d++; } while (v > 0);
            buf[0] = '¥';
            for (int i = 0; i < n; i++) buf[1 + i] = tmp[n - 1 - i];
            return n + 1;
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        void EnsureCanvas()
        {
            if (externalCanvas != null) { canvas = externalCanvas; return; }
            if (canvas != null) return;
            var go = new GameObject("OperationResult_Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
            go.AddComponent<GraphicRaycaster>();
        }

        void Build(AwardReport rep, Action hq)
        {
            report = rep; onHq = hq;
            EnsureCanvas();
            canvas.gameObject.SetActive(true);
            if (root != null) HudKit.DestroySafe(root.gameObject);
            shownMoney = shownRank = shownPoints = -1;
            var theme = UITheme.Current;
            if (rankNames == null) { rankNames = new string[RankTable.Count]; for (int i = 0; i < rankNames.Length; i++) rankNames[i] = RankTable.Name(i); }

            root = HudKit.NewRect("OperationResult", canvas.transform);
            HudKit.Stretch(root);
            var dimRt = HudKit.NewRect("DecoDim", root);
            HudKit.Stretch(dimRt);
            dim = HudKit.AddImage(dimRt, DimC, raycast: true);
            dimRt.gameObject.AddComponent<ClickRelay>().owner = this;

            BuildBanner(theme);
            BuildResult(theme);
        }

        void BuildBanner(UITheme theme)
        {
            bannerBand = HudKit.NewRect("Banner", root);
            bannerBand.anchorMin = new Vector2(0f, .5f); bannerBand.anchorMax = new Vector2(1f, .5f); bannerBand.pivot = new Vector2(.5f, .5f);
            bannerBand.anchoredPosition = new Vector2(0f, 40f); bannerBand.sizeDelta = new Vector2(0f, 150f);
            bannerGroup = bannerBand.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            var band = HudKit.NewRect("DecoBand", bannerBand);
            HudKit.Stretch(band);
            HudKit.AddImage(band, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .92f));
            var top = HudKit.NewRect("DecoLineTop", bannerBand);
            top.anchorMin = new Vector2(0f, 1f); top.anchorMax = Vector2.one; top.pivot = new Vector2(.5f, 1f); top.sizeDelta = new Vector2(0f, 3f);
            HudKit.AddImage(top, theme.blue);
            var bot = HudKit.NewRect("DecoLineBottom", bannerBand);
            bot.anchorMin = Vector2.zero; bot.anchorMax = new Vector2(1f, 0f); bot.pivot = new Vector2(.5f, 0f); bot.sizeDelta = new Vector2(0f, 3f);
            HudKit.AddImage(bot, theme.blue);

            var en = HudKit.AddText(bannerBand, "OPERATION SUCCESS", 14f, theme.blue, true, TextAlignmentOptions.Center, "En", UITheme.TextRole.Display);
            en.characterSpacing = 16f;
            en.rectTransform.anchorMin = new Vector2(0f, .5f); en.rectTransform.anchorMax = new Vector2(1f, .5f);
            en.rectTransform.anchoredPosition = new Vector2(0f, 46f); en.rectTransform.sizeDelta = new Vector2(0f, 20f);
            bannerTitle = HudKit.AddText(bannerBand, "作戦成功", 64f, Color.white, true, TextAlignmentOptions.Center, "Title", UITheme.TextRole.Title);
            bannerTitle.rectTransform.anchorMin = new Vector2(0f, .5f); bannerTitle.rectTransform.anchorMax = new Vector2(1f, .5f);
            bannerTitle.rectTransform.anchoredPosition = new Vector2(0f, -8f); bannerTitle.rectTransform.sizeDelta = new Vector2(0f, 84f);
            bannerGlitch = bannerTitle.gameObject.AddComponent<UIGlitchText>();
            bannerGlitch.source = bannerTitle; bannerGlitch.loop = false; bannerGlitch.maxOffset = 10f;
        }

        void BuildResult(UITheme theme)
        {
            resultRoot = HudKit.NewRect("Result", root);
            HudKit.Stretch(resultRoot);
            resultGroup = resultRoot.gameObject.AddComponent<CanvasGroup>();
            resultGroup.blocksRaycasts = false;          // (the dim under it takes the skipping clicks; the button has its own group)

            // head
            float x0 = Mathf.Round((ScreenW - (report.lines.Count * CardW + Mathf.Max(0, report.lines.Count - 1) * CardGap)) * .5f);
            float hx = Mathf.Min(x0, 112f);
            var en = HudKit.AddText(resultRoot, "OPERATION RESULT", 12f, theme.blue, true, TextAlignmentOptions.TopLeft, "HeadEn", UITheme.TextRole.Display);
            en.characterSpacing = 14f;
            UILayout.Place(en.rectTransform, hx, 34f, 400f, 16f);
            var head = HudKit.AddText(resultRoot, "作戦成功", 30f, Color.white, true, TextAlignmentOptions.TopLeft, "Head", UITheme.TextRole.Title);
            UILayout.Place(head.rectTransform, hx, 50f, 240f, 40f);
            if (!string.IsNullOrEmpty(report.mapLabel))
            {
                var map = HudKit.AddText(resultRoot, report.mapLabel, 14f, StatC, false, TextAlignmentOptions.BottomLeft, "Stage", UITheme.TextRole.Body);
                UILayout.Place(map.rectTransform, hx + 150f, 58f, 400f, 28f);
            }

            // cards
            cards = new Card[report.lines.Count];
            for (int i = 0; i < cards.Length; i++) cards[i] = BuildCard(theme, report.lines[i], x0 + i * (CardW + CardGap));

            BuildBottom(theme, hx);
        }

        Card BuildCard(UITheme theme, AwardLine line, float x)
        {
            var op = BuildAssetRegistry.Operator(line.rec.key);
            Color oc = op != null ? op.color : theme.blue;
            var c = new Card { line = line };
            c.rt = HudKit.NewRect("Card_" + line.rec.key, resultRoot);
            UILayout.Place(c.rt, x, CardY, CardW, 500f);
            c.home = c.rt.anchoredPosition;
            c.group = c.rt.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = false;

            if (line.mvp)
            {
                var frame = HudKit.NewRect("DecoMvpFrame", c.rt);
                UILayout.Place(frame, -4f, -4f, CardW + 8f, PhotoH + NameH + 8f);
                HudKit.AddImage(frame, Gold);
                frame.gameObject.AddComponent<CutCornerGraphic>().Set(20f, CutCornerGraphic.Corners.Mockup);
            }

            // the photo
            var photoBox = HudKit.NewRect("Photo", c.rt);
            UILayout.Place(photoBox, 0f, 0f, CardW, PhotoH);
            HudKit.AddImage(photoBox, Color.Lerp(theme.paper, theme.paper2, .5f));
            photoBox.gameObject.AddComponent<CutCornerGraphic>().Set(18f, CutCornerGraphic.Corners.TopRight);
            photoBox.gameObject.AddComponent<RectMask2D>();
            var wedge = HudKit.NewRect("DecoWedge", photoBox);
            UILayout.Place(wedge, CardW * .45f, 0f, CardW * .7f, PhotoH);
            HudKit.AddImage(wedge, new Color(oc.r, oc.g, oc.b, .22f));
            wedge.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 60f);
            var art = op != null ? OperatorArt.Body(op.key) : null;
            if (art != null)
            {
                var img = HudKit.NewRect("Art", photoBox);
                img.anchorMin = img.anchorMax = new Vector2(.5f, 1f); img.pivot = new Vector2(.5f, 1f);
                float aspect = art.rect.width / Mathf.Max(1f, art.rect.height);
                float w = Mathf.Max(CardW, PhotoH * aspect), h = w / aspect;
                img.sizeDelta = new Vector2(w, h); img.anchoredPosition = Vector2.zero;
                var im = img.gameObject.AddComponent<Image>();
                im.sprite = art; im.raycastTarget = false;
                if (!line.rec.alive) im.color = new Color(.45f, .45f, .48f, 1f);
                img.gameObject.AddComponent<LintIgnoreClip>().reason = "the illustration is cropped to the photo window (cover) on purpose";
            }
            if (line.mvp) Chip(c.rt, "最優秀隊員", "MVP", 8f, 8f, Gold, theme.ink);
            if (!line.rec.alive) Chip(c.rt, "戦闘不能", "KIA", 8f, PhotoH - 34f, theme.foe, Color.white);

            // name
            var nb = HudKit.NewRect("NameBlock", c.rt);
            UILayout.Place(nb, 0f, PhotoH, CardW, NameH);
            HudKit.AddImage(nb, theme.ink);
            nb.gameObject.AddComponent<CutCornerGraphic>().Set(18f, CutCornerGraphic.Corners.BottomLeft);
            var nl = HudKit.NewRect("DecoLine", nb);
            UILayout.Place(nl, 0f, 0f, CardW, 4f);
            HudKit.AddImage(nl, oc);
            var name = HudKit.AddText(nb, op != null ? op.DisplayName : line.rec.key, 22f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(name.rectTransform, 14f, 8f, CardW - 28f, 26f);
            var kana = HudKit.AddText(nb, op != null ? op.kr + " · " + op.role : "", 12f, StatC, false, TextAlignmentOptions.TopLeft, "Kana", UITheme.TextRole.Body);
            kana.textWrappingMode = TextWrappingModes.NoWrap; kana.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(kana.rectTransform, 14f, 33f, CardW - 28f, 16f);

            // level + experience
            float y = PhotoH + NameH + 12f;
            var lvTag = HudKit.AddText(c.rt, "Lv", 12f, StatC, true, TextAlignmentOptions.BottomLeft, "LvTag", UITheme.TextRole.Display);
            UILayout.Place(lvTag.rectTransform, 0f, y, 24f, 24f);
            c.level = HudKit.AddText(c.rt, "", 24f, Color.white, true, TextAlignmentOptions.BottomLeft, "Level", UITheme.TextRole.Display);
            UILayout.Place(c.level.rectTransform, 24f, y - 4f, 60f, 30f);
            var gain = HudKit.AddText(c.rt, "+" + line.exp + " EXP", 13f, theme.blue, true, TextAlignmentOptions.BottomRight, "Gain", UITheme.TextRole.Display);
            UILayout.Place(gain.rectTransform, CardW - 110f, y, 110f, 24f);
            c.levelUp = RightChip(c.rt, "LEVEL UP", PhotoH - 30f, Gold, theme.ink).gameObject;
            c.levelFill = Bar(c.rt, y + 30f, 6f, theme.blue);

            // weapon mastery
            y += 46f;
            string wname = WeaponName(op, line.rec.weapon);
            if (!string.IsNullOrEmpty(line.rec.weapon))
            {
                var wn = HudKit.AddText(c.rt, wname, 12f, StatC, false, TextAlignmentOptions.BottomLeft, "Weapon", UITheme.TextRole.Body);
                wn.textWrappingMode = TextWrappingModes.NoWrap; wn.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(wn.rectTransform, 0f, y, CardW - 92f, 18f);
                c.mastery = HudKit.AddText(c.rt, "", 12f, Color.white, true, TextAlignmentOptions.BottomRight, "Mastery", UITheme.TextRole.Body);
                UILayout.Place(c.mastery.rectTransform, CardW - 92f, y, 92f, 18f);
                c.masteryUp = RightChip(c.rt, "MASTERY UP", PhotoH - 58f, Gold, theme.ink).gameObject;
                c.masteryFill = Bar(c.rt, y + 22f, 4f, Gold);
            }

            // the record
            y += 38f;
            var rule = HudKit.NewRect("DecoRule", c.rt);
            UILayout.Place(rule, 0f, y, CardW, 1f);
            HudKit.AddImage(rule, new Color(1f, 1f, 1f, .12f));
            y += 8f;
            int[] values = { line.rec.damageTaken, line.rec.attacks, line.rec.walls, line.rec.kills, line.rec.assists };
            for (int i = 0; i < StatLabels.Length; i++)
            {
                var lab = HudKit.AddText(c.rt, StatLabels[i], 12f, StatC, false, TextAlignmentOptions.MidlineLeft, "StatLabel" + i, UITheme.TextRole.Body);
                UILayout.Place(lab.rectTransform, 0f, y + i * 24f, CardW - 70f, 24f);
                var val = HudKit.AddText(c.rt, values[i].ToString(), 17f, i == 3 && line.mvp ? Gold : Color.white, true, TextAlignmentOptions.MidlineRight, "StatValue" + i, UITheme.TextRole.Display);
                UILayout.Place(val.rectTransform, CardW - 70f, y + i * 24f, 70f, 24f);
            }
            c.levelUp.SetActive(false);
            if (c.masteryUp != null) c.masteryUp.SetActive(false);
            return c;
        }

        static string WeaponName(OperatorData op, string key)
        {
            if (op != null)
            {
                if (op.weapons != null) foreach (var w in op.weapons) if (w != null && w.key == key) return w.weaponName;
                if (op.weapon != null && op.weapon.key == key) return op.weapon.weaponName;
            }
            return key;
        }

        RectTransform Bar(RectTransform parent, float y, float h, Color c)
        {
            var track = HudKit.NewRect("BarTrack", parent);
            UILayout.Place(track, 0f, y, CardW, h);
            HudKit.AddImage(track, new Color(1f, 1f, 1f, .14f));
            var fill = HudKit.NewRect("BarFill", track);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            HudKit.AddImage(fill, c);
            return fill;
        }

        static RectTransform Chip(RectTransform parent, string text, string en, float x, float y, Color bg, Color fg)
        {
            var rt = HudKit.NewRect("Chip_" + (en ?? text), parent);
            var t = HudKit.AddText(rt, en != null ? text + "  <size=11>" + en + "</size>" : text, 12f, fg, true, TextAlignmentOptions.Center, "Text",
                en != null ? UITheme.TextRole.Body : UITheme.TextRole.Display);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float w = Mathf.Ceil(t.GetPreferredValues(t.text).x) + 20f;
            UILayout.Place(rt, x, y, w, 22f);
            HudKit.AddImage(rt, bg);             // (on the chip itself: drawn under its text child)
            rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 6f);
            HudKit.Stretch(t.rectTransform);
            rt.gameObject.AddComponent<LintIgnoreOverlap>().reason = "a badge laid over the photo / the rank line on purpose";
            return rt;
        }

        // a chip flush right on the card (8 in), at `y`
        static RectTransform RightChip(RectTransform card, string text, float y, Color bg, Color fg)
        {
            var rt = Chip(card, text, null, 0f, y, bg, fg);
            UILayout.Place(rt, CardW - rt.sizeDelta.x - 8f, y, rt.sizeDelta.x, rt.sizeDelta.y);
            return rt;
        }

        void BuildBottom(UITheme theme, float hx)
        {
            float y = 666f;
            var band = HudKit.NewRect("DecoBottomBand", resultRoot);
            UILayout.Place(band, 0f, y - 16f, ScreenW, ScreenH - y + 16f);
            HudKit.AddImage(band, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .9f));
            var line = HudKit.NewRect("DecoBottomLine", resultRoot);
            UILayout.Place(line, 0f, y - 16f, ScreenW, 2f);
            HudKit.AddImage(line, new Color(theme.blue.r, theme.blue.g, theme.blue.b, .8f));

            // reward
            var rl = HudKit.AddText(resultRoot, "報酬  <size=11>REWARD</size>", 14f, StatC, true, TextAlignmentOptions.TopLeft, "RewardLabel", UITheme.TextRole.Body);
            UILayout.Place(rl.rectTransform, hx, y + 4f, 300f, 20f);
            moneyText = HudKit.AddText(resultRoot, "", 40f, Color.white, true, TextAlignmentOptions.TopLeft, "Reward", UITheme.TextRole.Display);
            moneyText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(moneyText.rectTransform, hx, y + 26f, 360f, 50f);

            // rank
            float rx = 520f, rw = 440f;
            var cl = HudKit.AddText(resultRoot, "貢献度  <size=11>CONTRIBUTION</size>", 14f, StatC, true, TextAlignmentOptions.TopLeft, "RankLabel", UITheme.TextRole.Body);
            UILayout.Place(cl.rectTransform, rx, y + 4f, 260f, 20f);
            var plus = HudKit.AddText(resultRoot, "+" + report.contribution, 16f, theme.blue, true, TextAlignmentOptions.TopRight, "RankGain", UITheme.TextRole.Display);
            UILayout.Place(plus.rectTransform, rx + rw - 120f, y + 4f, 120f, 20f);
            rankName = HudKit.AddText(resultRoot, "", 24f, Color.white, true, TextAlignmentOptions.TopLeft, "RankName", UITheme.TextRole.Title);
            rankName.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(rankName.rectTransform, rx, y + 26f, 220f, 32f);
            promoted = Chip(resultRoot, "昇進", "PROMOTED", rx + 170f, y + 31f, Gold, theme.ink).gameObject;
            rankPoints = HudKit.AddText(resultRoot, "", 12f, StatC, true, TextAlignmentOptions.TopRight, "RankPoints", UITheme.TextRole.Display);
            UILayout.Place(rankPoints.rectTransform, rx + rw - 200f, y + 34f, 200f, 18f);
            var track = HudKit.NewRect("RankTrack", resultRoot);
            UILayout.Place(track, rx, y + 64f, rw, 8f);
            HudKit.AddImage(track, new Color(1f, 1f, 1f, .14f));
            track.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 6f);
            track.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            rankFill = HudKit.NewRect("RankFill", track);
            rankFill.anchorMin = Vector2.zero; rankFill.anchorMax = new Vector2(0f, 1f); rankFill.offsetMin = rankFill.offsetMax = Vector2.zero;
            HudKit.AddImage(rankFill, Gold);
            promoted.SetActive(false);

            // 本部へ戻る
            var bRow = HudKit.NewRect("HqRow", resultRoot);
            UILayout.Place(bRow, ScreenW - 112f - 300f + 12f, y + 18f, 300f, 52f);
            buttonGroup = bRow.gameObject.AddComponent<CanvasGroup>();
            var b = TacticalButton.Create(bRow, "ReturnToHq", "➜", "本部へ戻る", "RETURN TO HQ", null, new Vector2(300f, 52f));
            UILayout.Place((RectTransform)b.transform, 0f, 0f, 300f, 52f);
            b.onClick.AddListener(() => { Hide(); onHq?.Invoke(); });
        }

        /// <summary>Clicks on the dim go to the screen (skipping).</summary>
        class ClickRelay : MonoBehaviour, IPointerClickHandler
        {
            public OperationResultScreen owner;
            public void OnPointerClick(PointerEventData e) { if (owner != null) owner.OnPointerClick(e); }
        }
    }
}
