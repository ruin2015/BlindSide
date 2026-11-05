// The experience an engagement gave (ProgressLive.Flush), shown inside the battle cinematic right after the exchange (MatchBootstrap's
// experience beat, which the director waits for). Its own Screen Space Overlay canvas (menu 1440×810, sorting order 95: over the HUD and
// the cinematic's letterbox, under the result screens), one card per operator of the player's side who took part, centred in the lower
// half (y 500, 16 apart: clear of the HUD's log / hint lines at the bottom):
//   Card   430×120, ink .94, slant 14, a 4px band in the operator colour on the left
//     Face 96×96 (OperatorArt.Face) · Name 20 display · "+N EXP" 16 display (blue)
//     Lv row     "Lv" 11 + the level 22 display, the EXP bar (6, blue) — fills through every level gained
//     Mastery    the weapon's name 12 + 熟練度 Lv N, its bar (4, gold)
//   Badges  one row over the card's top right (gold chips): 撃破ボーナス (it killed in this exchange) · LEVEL UP · MASTERY UP (lit when a
//           level / mastery level is crossed)
// Motion (ITimedEffect.Evaluate, unscaled): in .25s (fade + 20 up), the bars fill .3 → 1.3s (tactical ease), held, out 1.75 → 2.0s.
// With UIMotionSettings.reduceMotion: fades only, the bars jump. No allocation per frame (texts change through SetText with numbers).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Progress;

namespace Blindside.UI
{
    public class ExpGainPopup : MonoBehaviour, ITimedEffect
    {
        public const float CardW = 430f, CardH = 120f, Gap = 16f, Y = 500f, InTime = .25f, FillFrom = .3f, FillTo = 1.3f, OutFrom = 1.75f, OutTo = 2.0f;
        static readonly Color Gold = UIPalette.Hex("#FFC81E");
        static readonly Color Dim = UIPalette.Hex("#9AA3AD");

        public static ExpGainPopup Instance { get; private set; }

        /// <summary>Previews draw into a canvas of the stage.</summary>
        public Canvas externalCanvas;
        Canvas canvas;
        RectTransform root;
        CanvasGroup group;

        class Card
        {
            public ProgressLive.Gain g;
            public TMP_Text level, mastery;
            public RectTransform levelFill, masteryFill;
            public GameObject levelUp, masteryUp;
            public int shownLevel = -1, shownMastery = -1;
        }
        readonly List<Card> cards = new List<Card>(2);
        float clock; bool running;

        public float Duration => OutTo;

        /// <summary>Shows `gains` (from ProgressLive.Flush) on the screen's own overlay.</summary>
        public static ExpGainPopup Show(List<ProgressLive.Gain> gains)
        {
            if (Instance == null)
            {
                var go = new GameObject("ExpGainPopup", typeof(RectTransform));
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<ExpGainPopup>();
            }
            Instance.Play(gains);
            return Instance;
        }

        public void Play(List<ProgressLive.Gain> gains)
        {
            Build(gains);
            clock = 0f; running = Application.isPlaying;
            Evaluate(0f);
        }

        /// <summary>Previews: `gains` posed `t` seconds in.</summary>
        public void PoseAt(List<ProgressLive.Gain> gains, float t)
        {
            Build(gains);
            clock = t; running = false;
            Evaluate(t);
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            if (clock >= Duration) { running = false; if (canvas != null && externalCanvas == null) canvas.gameObject.SetActive(false); return; }
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            if (root == null) return;
            bool reduce = UIMotionSettings.reduceMotion;
            float a = t < OutFrom ? Mathf.Clamp01(t / InTime) : 1f - Mathf.Clamp01((t - OutFrom) / (OutTo - OutFrom));
            if (!reduce && t < OutFrom) a = UIEase.Tactical(a);
            if (group.alpha != a) group.alpha = a;
            var pos = new Vector2(0f, reduce ? 0f : -20f * (1f - Mathf.Clamp01(t / InTime)));
            if (root.anchoredPosition != pos) root.anchoredPosition = pos;

            float p = Mathf.Clamp01((t - FillFrom) / (FillTo - FillFrom));
            p = reduce ? (t >= FillFrom ? 1f : 0f) : UIEase.Tactical(p);
            for (int i = 0; i < cards.Count; i++) PoseCard(cards[i], p);
        }

        static void PoseCard(Card c, float p)
        {
            var g = c.g;
            int add = Mathf.RoundToInt(g.exp * p);
            int lv = g.levelFrom, ex = g.expFrom;
            ProgressRules.AddExp(ref lv, ref ex, add, ProgressRules.LevelNeed, ProgressRules.MaxLevel);
            int need = ProgressRules.LevelNeed(lv);
            c.levelFill.anchorMax = new Vector2(need > 0 ? Mathf.Clamp01(ex / (float)need) : 1f, 1f);
            if (lv != c.shownLevel) { c.shownLevel = lv; c.level.SetText("{0}", (float)lv); }
            bool up = lv > g.levelFrom;
            if (c.levelUp.activeSelf != up) c.levelUp.SetActive(up);
            if (c.mastery == null) return;
            int wl = g.wLevelFrom, wx = g.wExpFrom;
            ProgressRules.AddExp(ref wl, ref wx, add, ProgressRules.MasteryNeed, ProgressRules.MaxMastery);
            int wneed = ProgressRules.MasteryNeed(wl);
            c.masteryFill.anchorMax = new Vector2(wneed > 0 ? Mathf.Clamp01(wx / (float)wneed) : 1f, 1f);
            if (wl != c.shownMastery) { c.shownMastery = wl; c.mastery.SetText("熟練度 Lv {0}", (float)wl); }
            bool wup = wl > g.wLevelFrom;
            if (c.masteryUp.activeSelf != wup) c.masteryUp.SetActive(wup);
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        void EnsureCanvas()
        {
            if (externalCanvas != null) { canvas = externalCanvas; return; }
            if (canvas != null) return;
            var go = new GameObject("ExpGainPopup_Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
        }

        void Build(List<ProgressLive.Gain> gains)
        {
            EnsureCanvas();
            canvas.gameObject.SetActive(true);
            if (root != null) HudKit.DestroySafe(root.gameObject);
            cards.Clear();
            var theme = UITheme.Current;
            root = HudKit.NewRect("ExpGain", canvas.transform);
            HudKit.Stretch(root);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            int n = gains != null ? gains.Count : 0;
            float total = n * CardW + Mathf.Max(0, n - 1) * Gap;
            float x0 = Mathf.Round((1440f - total) * .5f);
            for (int i = 0; i < n; i++) cards.Add(BuildCard(theme, gains[i], x0 + i * (CardW + Gap)));
        }

        Card BuildCard(UITheme theme, ProgressLive.Gain g, float x)
        {
            var op = BuildAssetRegistry.Operator(g.rec.key);
            Color oc = op != null ? op.color : theme.blue;
            var c = new Card { g = g };
            var card = HudKit.NewRect("Card_" + g.rec.key, root);
            UILayout.Place(card, x, Y, CardW, CardH);
            HudKit.AddImage(card, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .94f));
            card.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 14f);
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var band = HudKit.NewRect("DecoBand", card);
            UILayout.Place(band, 0f, 0f, 18f, CardH);
            HudKit.AddImage(band, oc);
            band.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 14f);

            var faceBox = HudKit.NewRect("Face", card);
            UILayout.Place(faceBox, 24f, 12f, 96f, 96f);
            HudKit.AddImage(faceBox, Color.Lerp(theme.ink, oc, .25f));
            var face = op != null ? OperatorArt.Face(op.key) : null;
            if (face != null)
            {
                var f = HudKit.NewRect("Photo", faceBox);
                HudKit.Stretch(f);
                var im = f.gameObject.AddComponent<Image>();
                im.sprite = face; im.preserveAspect = true; im.raycastTarget = false;
            }

            float tx = 134f, tw = CardW - tx - 24f;
            var name = HudKit.AddText(card, op != null ? op.DisplayName : g.rec.key, 20f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(name.rectTransform, tx, 10f, 160f, 24f);
            var gain = HudKit.AddText(card, "+" + g.exp + " EXP", 16f, theme.blue, true, TextAlignmentOptions.TopRight, "Gain", UITheme.TextRole.Display);
            UILayout.Place(gain.rectTransform, tx + tw - 110f, 12f, 110f, 22f);
            // the badges: one row over the card's top right (outside the card's mask), fixed slots right to left —
            // MASTERY UP, LEVEL UP, 撃破ボーナス (the first two light when they happen)
            var mUp = Chip(root, "MASTERY UP", Gold, theme.ink);
            var lUp = Chip(root, "LEVEL UP", Gold, theme.ink);
            float bx = x + CardW - 14f - mUp.sizeDelta.x;
            UILayout.Place(mUp, bx, Y - 26f, mUp.sizeDelta.x, mUp.sizeDelta.y);
            bx -= 6f + lUp.sizeDelta.x;
            UILayout.Place(lUp, bx, Y - 26f, lUp.sizeDelta.x, lUp.sizeDelta.y);
            if (g.killsNow > 0)
            {
                var k = Chip(root, "撃破ボーナス", Gold, theme.ink);
                bx -= 6f + k.sizeDelta.x;
                UILayout.Place(k, bx, Y - 26f, k.sizeDelta.x, k.sizeDelta.y);
            }
            c.levelUp = lUp.gameObject;
            c.masteryUp = mUp.gameObject;

            var lvTag = HudKit.AddText(card, "Lv", 11f, Dim, true, TextAlignmentOptions.BottomLeft, "LvTag", UITheme.TextRole.Display);
            UILayout.Place(lvTag.rectTransform, tx, 38f, 22f, 22f);
            c.level = HudKit.AddText(card, "", 22f, Color.white, true, TextAlignmentOptions.BottomLeft, "Level", UITheme.TextRole.Display);
            UILayout.Place(c.level.rectTransform, tx + 22f, 34f, 50f, 28f);
            c.levelFill = Bar(card, tx, 66f, tw, 6f, theme.blue);

            if (!string.IsNullOrEmpty(g.rec.weapon))
            {
                var wn = HudKit.AddText(card, WeaponName(op, g.rec.weapon), 12f, Dim, false, TextAlignmentOptions.TopLeft, "Weapon", UITheme.TextRole.Body);
                wn.textWrappingMode = TextWrappingModes.NoWrap; wn.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(wn.rectTransform, tx, 78f, 130f, 18f);
                c.mastery = HudKit.AddText(card, "", 12f, Color.white, true, TextAlignmentOptions.TopLeft, "Mastery", UITheme.TextRole.Body);
                UILayout.Place(c.mastery.rectTransform, tx + 134f, 78f, 90f, 18f);
                c.masteryFill = Bar(card, tx, 100f, tw, 4f, Gold);
            }
            c.levelUp.SetActive(false);
            c.masteryUp.SetActive(false);
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

        static RectTransform Bar(RectTransform parent, float x, float y, float w, float h, Color c)
        {
            var track = HudKit.NewRect("BarTrack", parent);
            UILayout.Place(track, x, y, w, h);
            HudKit.AddImage(track, new Color(1f, 1f, 1f, .14f));
            var fill = HudKit.NewRect("BarFill", track);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            HudKit.AddImage(fill, c);
            return fill;
        }

        static RectTransform Chip(RectTransform parent, string text, Color bg, Color fg)
        {
            var rt = HudKit.NewRect("Chip_" + text, parent);
            bool latin = text[0] < 0x80;
            var t = HudKit.AddText(rt, text, latin ? 11f : 12f, fg, true, TextAlignmentOptions.Center, "Text", latin ? UITheme.TextRole.Display : UITheme.TextRole.Body);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float w = Mathf.Ceil(t.GetPreferredValues(text).x) + 18f;
            rt.sizeDelta = new Vector2(w, 20f);
            HudKit.AddImage(rt, bg);
            rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 6f);
            HudKit.Stretch(t.rectTransform);
            return rt;
        }
    }
}
