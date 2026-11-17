// The co-op rank-up card (a co-op event's RankUp, any operator), on its own overlay canvas over the conversation (menu 1440×810, order 85):
//   left    the operator's illustration over a deep blue panel with a slanted right edge (white edge line, faint stripes), the codename
//           (display 34) + kana top left with a bar in the operator's colour
//   top     「RANK UP!」 in yellow (display 150, black outline, a black drop shadow), tilted 8°, slammed down over the art
//   right   a white plate: "RANK" and the rank — the old number pushed up and out, the new one up from below, a white flash
//   centre  the unlocked effect's card (an ink card, a border in the operator's colour, ✚) and its name on a black strip under it
//   bottom  a white slab leaning across (a faint "CO-OP" in it) with "UNLOCK" and what the effect does; the OK hint bottom right
// Comes in, holds a moment, goes (HoldEnd); a click / Space / Enter / F after MinShow sends it off at once.
// One unscaled clock (ITimedEffect.Evaluate(t)); per frame only positions, scales and alphas change — nothing is allocated.
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Progress;

namespace Blindside.UI
{
    public class KitCoopRankUp : MonoBehaviour, ITimedEffect
    {
        public const int SortingOrder = 85;
        public const float ArtW = 760f, HeadSize = 150f, NumberSize = 300f;
        // the timeline (seconds)
        public const float DimIn = .25f, ArtIn = .45f, SlabFrom = .12f, SlabIn = .38f, HeadFrom = .22f, HeadIn = .24f, PlateFrom = .3f,
                           PlateIn = .3f, NumberAt = .85f, NumberIn = .3f, CardFrom = .95f, CardIn = .3f, HintFrom = 1.3f, MinShow = 1.2f,
                           HoldEnd = 3.4f, OutTime = .35f;

        CanvasGroup group;
        RectTransform artGroup, slab, head, headShadow, plate, rankFrom, rankTo, card, cardName, desc, hint;
        Image dim, art;
        Graphic flash;
        TMP_Text codename, kana, fromText, toText, cardIcon, cardNameText, descText;
        Image nameBar, cardBorder;
        TMP_Text headText, headShadowText;
        CanvasGroup plateCg, cardCg, cardNameCg, descCg, hintCg;
        float clock = -1f;

        public float Duration => HoldEnd + OutTime;
        public bool IsOpen => gameObject.activeSelf;
        /// <summary>The card has come and gone.</summary>
        public bool Done { get; private set; }

        // ---- use ---------------------------------------------------------------------------------------------------------------
        /// <summary>`key`'s co-op has gone from rank `from` to `to`: the card plays (wait for Done).</summary>
        public void Show(string key, int from, int to)
        {
            Fill(key, from, to);
            Done = false;
            clock = 0f;
            gameObject.SetActive(true);
            Evaluate(0f);
        }

        public IEnumerator Run(string key, int from, int to)
        {
            Show(key, from, to);
            while (!Done) yield return null;
        }

        /// <summary>Previews: the card `t` seconds in.</summary>
        public void PoseAt(string key, int from, int to, float t)
        {
            Fill(key, from, to);
            clock = -1f;
            gameObject.SetActive(true);
            Evaluate(t);
        }

        void Fill(string key, int from, int to)
        {
            var op = BuildAssetRegistry.Operator(key);
            var col = op != null ? op.color : UITheme.Current.blue;
            codename.text = op != null ? op.DisplayName : key;
            kana.text = op != null ? op.kr : "";
            nameBar.color = col;
            var body = OperatorArt.Body(key);
            art.sprite = body; art.enabled = body != null;
            fromText.text = from.ToString();
            toText.text = to.ToString();
            cardBorder.color = col;
            cardIcon.color = col;
            // what the new rank unlocks (CoopPerks): the names on the strip, what they do on the slab
            var names = new StringBuilder(); var texts = new StringBuilder();
            foreach (var p in CoopPerks.Lines(key))
            {
                if (p.rank != to) continue;
                if (names.Length > 0) { names.Append(" / "); texts.Append('\n'); }
                names.Append(p.name); texts.Append(p.text);
            }
            cardNameText.text = names.Length > 0 ? names.ToString() : "コープランク " + to;
            descText.text = texts.Length > 0 ? texts.ToString() : "絆が深まった。";
        }

        // ---- motion ------------------------------------------------------------------------------------------------------------
        static float K(float t, float from, float len) => UIEase.Tactical(Mathf.Clamp01((t - from) / len));

        public void Evaluate(float t)
        {
            bool still = UIMotionSettings.reduceMotion;
            float o = 1f - Mathf.Clamp01((t - HoldEnd) / OutTime);   // the way out
            float outK = 1f - o;
            group.alpha = o;
            SetAlpha(dim, .82f * (still ? 1f : K(t, 0f, DimIn)));

            float a = still ? 1f : K(t, 0f, ArtIn);
            artGroup.anchoredPosition = new Vector2(-160f * (1f - a) - 120f * outK, 0f);
            float s = still ? 1f : K(t, SlabFrom, SlabIn);
            slab.anchoredPosition = new Vector2(380f + 420f * (1f - s) + 200f * outK, -470f);

            // RANK UP! slams down: from twice the size, faded in
            float h = still ? 1f : K(t, HeadFrom, HeadIn);
            float hs = Mathf.Lerp(2f, 1f, h);
            head.localScale = headShadow.localScale = new Vector3(hs, hs, 1f);
            SetAlpha(headText, h); SetAlpha(headShadowText, .85f * h);

            float p = still ? 1f : K(t, PlateFrom, PlateIn);
            plate.anchoredPosition = new Vector2(1040f + 30f * outK, -140f + 40f * (1f - p));
            SetAlpha(plateCg, p);

            // the number: the old one pushed up and out, the new one up from below
            float n = still ? (t >= NumberAt ? 1f : 0f) : K(t, NumberAt, NumberIn);
            rankFrom.anchoredPosition = new Vector2(0f, NumberSize * 1.15f * n);
            rankTo.anchoredPosition = new Vector2(0f, -NumberSize * 1.15f * (1f - n));
            // each number is up only while it is in the window (the clip hides the rest of its travel)
            if (rankFrom.gameObject.activeSelf != n < 1f) rankFrom.gameObject.SetActive(n < 1f);
            if (rankTo.gameObject.activeSelf != n > 0f) rankTo.gameObject.SetActive(n > 0f);
            float f = t < NumberAt ? 0f : 1f - Mathf.Clamp01((t - NumberAt) / (NumberIn + .1f));
            SetAlpha(flash, still ? 0f : f);

            float c = still ? 1f : K(t, CardFrom, CardIn);
            float cs = Mathf.Lerp(.6f, 1f, c);
            card.localScale = new Vector3(cs, cs, 1f);
            SetAlpha(cardCg, c); SetAlpha(cardNameCg, c); SetAlpha(descCg, c);
            cardName.anchoredPosition = new Vector2(620f - 30f * (1f - c), -468f);
            SetAlpha(hintCg, still ? 1f : K(t, HintFrom, .2f));
        }

        static void SetAlpha(Graphic g, float a) { var c = g.color; if (c.a != a) { c.a = a; g.color = c; } }
        static void SetAlpha(CanvasGroup g, float a) { if (g.alpha != a) g.alpha = a; }

        void Update()
        {
            if (clock < 0f) return;
            clock += Time.unscaledDeltaTime;
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool press = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                         || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame));
            if (press && clock >= MinShow && clock < HoldEnd) clock = HoldEnd;
            Evaluate(clock);
            if (clock < Duration) return;
            clock = -1f;
            Done = true;
            gameObject.SetActive(false);
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        /// <param name="onCanvas">build onto this canvas instead of a new overlay one (Render Previews' stage)</param>
        public static KitCoopRankUp Build(Transform parent, Canvas onCanvas = null)
        {
            var theme = UITheme.Current;
            var c = onCanvas;
            if (c == null)
            {
                var cgo = new GameObject("CoopRankUp_Canvas", typeof(RectTransform));
                cgo.transform.SetParent(parent, false);
                c = cgo.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = SortingOrder;
                theme.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
                cgo.AddComponent<GraphicRaycaster>();
            }
            var root = HudKit.NewRect("CoopRankUp", c.transform);
            HudKit.Stretch(root);
            var r = root.gameObject.AddComponent<KitCoopRankUp>();
            r.group = root.gameObject.AddComponent<CanvasGroup>();
            r.dim = HudKit.AddImage(HudKit.NewRect("DecoDim", root), Color.black, raycast: true);
            HudKit.Stretch(r.dim.rectTransform);

            // ---- left: the art panel ----
            r.artGroup = HudKit.NewRect("ArtPanel", root);
            HudKit.Stretch(r.artGroup);
            var back = Strip(r.artGroup, "DecoArtBack", 0f, 0f, ArtW, 810f, Color.Lerp(theme.ink, theme.blue, .32f));
            back.Set(0f, 0f, 1f, 0f, .72f, 1f, 0f, 1f);
            for (int i = 0; i < 3; i++)
            {
                var st = Strip(r.artGroup, "DecoStripe" + i, 0f, 0f, ArtW, 810f, new Color(1f, 1f, 1f, .07f));
                float x0 = .08f + i * .2f;
                st.Set(x0 + .26f, 0f, x0 + .33f, 0f, x0 + .05f, 1f, x0 - .02f, 1f);
            }
            var clip = HudKit.NewRect("ArtClip", r.artGroup);
            UILayout.Place(clip, 0f, 0f, ArtW, 810f);
            clip.gameObject.AddComponent<RectMask2D>();
            var artRt = HudKit.NewRect("Art", clip);
            UILayout.Place(artRt, 0f, 40f, ArtW, 770f);
            r.art = artRt.gameObject.AddComponent<Image>();
            r.art.preserveAspect = true; r.art.raycastTarget = false;
            artRt.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the illustration fills the art panel behind the name and the headline on purpose";
            var edge = Strip(r.artGroup, "DecoEdge", 0f, 0f, ArtW + 20f, 810f, theme.paper);
            edge.Set(.975f, 0f, 1f, 0f, .727f, 1f, .702f, 1f);

            var nameRt = HudKit.NewRect("NameBlock", r.artGroup);
            UILayout.Place(nameRt, 48f, 36f, 420f, 84f);
            nameRt.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the codename sits over the illustration on purpose";
            r.codename = HudKit.AddText(nameRt, "", 34f, Color.white, true, TextAlignmentOptions.TopLeft, "Codename", UITheme.TextRole.Display);
            UILayout.Place(r.codename.rectTransform, 0f, 0f, 420f, 40f);
            r.kana = HudKit.AddText(nameRt, "", 16f, theme.paper2, false, TextAlignmentOptions.TopLeft, "Kana", UITheme.TextRole.Body);
            UILayout.Place(r.kana.rectTransform, 2f, 42f, 420f, 22f);
            r.nameBar = HudKit.AddImage(HudKit.NewRect("DecoNameBar", nameRt), theme.acc);
            UILayout.Place(r.nameBar.rectTransform, 2f, 70f, 120f, 4f);

            // ---- the slab across the bottom right: "CO-OP", UNLOCK and what the effect does ----
            r.slab = HudKit.NewRect("Slab", root);
            UILayout.Place(r.slab, 380f, 470f, 1100f, 250f);
            var slabBack = Strip(r.slab, "DecoSlabBack", 0f, 0f, 1100f, 250f, theme.paper);
            slabBack.Set(.06f, .2f, 1f, 0f, 1f, .78f, 0f, 1f);
            var ghost = HudKit.AddText(r.slab, "CO-OP", 170f, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .1f), true, TextAlignmentOptions.MidlineLeft, "DecoSlabWord", UITheme.TextRole.Display);
            UILayout.Place(ghost.rectTransform, 60f, 30f, 800f, 200f);
            ghost.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 9f);

            r.desc = HudKit.NewRect("Effect", r.slab);
            UILayout.Place(r.desc, 450f, 108f, 580f, 120f);
            r.descCg = r.desc.gameObject.AddComponent<CanvasGroup>();
            var tag = HudKit.NewRect("UnlockTag", r.desc);
            UILayout.Place(tag, 0f, 0f, 110f, 26f);
            HudKit.AddImage(tag, theme.ink);
            tag.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            var tagText = HudKit.AddText(tag, "UNLOCK", 15f, theme.acc, true, TextAlignmentOptions.Center, "Label", UITheme.TextRole.Display);
            HudKit.Stretch(tagText.rectTransform);
            r.descText = HudKit.AddText(r.desc, "", 20f, theme.ink, false, TextAlignmentOptions.TopLeft, "Text", UITheme.TextRole.Body);
            r.descText.textWrappingMode = TextWrappingModes.Normal;
            UILayout.Place(r.descText.rectTransform, 0f, 34f, 580f, 84f);

            // ---- right: the rank plate ----
            r.plate = HudKit.NewRect("RankPlate", root);
            UILayout.Place(r.plate, 1040f, 140f, 360f, 420f);
            r.plateCg = r.plate.gameObject.AddComponent<CanvasGroup>();
            var pb = Strip(r.plate, "DecoPlateBack", 0f, 0f, 360f, 420f, theme.paper);
            pb.Set(.14f, 0f, 1f, 0f, .86f, 1f, 0f, 1f);
            for (int i = 0; i < 3; i++)
            {
                var line = HudKit.AddImage(HudKit.NewRect("DecoPlateLine" + i, r.plate), theme.ink);
                UILayout.Place(line.rectTransform, 300f, 190f + i * 26f, 120f - i * 22f, 8f);
            }
            var rl = HudKit.AddText(r.plate, "RANK", 26f, theme.ink, true, TextAlignmentOptions.TopLeft, "RankLabel", UITheme.TextRole.Display);
            UILayout.Place(rl.rectTransform, 70f, 22f, 200f, 32f);
            var numClip = HudKit.NewRect("NumberClip", r.plate);
            UILayout.Place(numClip, 20f, 60f, 290f, 340f);
            numClip.gameObject.AddComponent<RectMask2D>();
            r.fromText = Number(numClip, "RankFrom", theme.dim);
            r.toText = Number(numClip, "RankTo", theme.ink);
            r.rankFrom = r.fromText.rectTransform; r.rankTo = r.toText.rectTransform;
            var fl = Strip(r.plate, "DecoFlash", 0f, 0f, 360f, 420f, Color.white);
            fl.Set(.14f, 0f, 1f, 0f, .86f, 1f, 0f, 1f);   // (the plate's own shape)
            r.flash = fl;

            // ---- centre: the unlocked effect's card and its name ----
            r.card = HudKit.NewRect("EffectCard", root);
            UILayout.Place(r.card, 640f, 236f, 220f, 210f);
            r.card.pivot = new Vector2(.5f, .5f); r.card.anchoredPosition = new Vector2(750f, -341f);
            r.card.localRotation = Quaternion.Euler(0f, 0f, 6f);
            r.cardCg = r.card.gameObject.AddComponent<CanvasGroup>();
            r.card.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the effect's card leans over the art panel's edge on purpose";
            r.cardBorder = HudKit.AddImage(HudKit.NewRect("DecoCardBorder", r.card), theme.blue);
            HudKit.Stretch(r.cardBorder.rectTransform);
            var inner = HudKit.AddImage(HudKit.NewRect("DecoCardInner", r.card), theme.ink);
            HudKit.Stretch(inner.rectTransform);
            inner.rectTransform.offsetMin = new Vector2(6f, 6f); inner.rectTransform.offsetMax = new Vector2(-6f, -6f);
            r.cardIcon = HudKit.AddText(r.card, "✚", 110f, theme.blue, true, TextAlignmentOptions.Center, "Icon", UITheme.TextRole.Display);
            HudKit.Stretch(r.cardIcon.rectTransform);

            r.cardName = HudKit.NewRect("EffectName", root);
            UILayout.Place(r.cardName, 620f, 468f, 340f, 40f);
            r.cardNameCg = r.cardName.gameObject.AddComponent<CanvasGroup>();
            r.cardName.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the effect's name strip sits on the slab's edge on purpose";
            HudKit.AddImage(r.cardName, Color.black);
            r.cardName.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            r.cardNameText = HudKit.AddText(r.cardName, "", 21f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            r.cardNameText.textWrappingMode = TextWrappingModes.NoWrap;
            HudKit.Stretch(r.cardNameText.rectTransform);

            // ---- the headline: RANK UP! (a black shadow under it) ----
            r.headShadow = Headline(root, "DecoHeadShadow", Color.black, new Vector2(10f, -10f));
            r.head = Headline(root, "Headline", theme.acc, Vector2.zero);
            r.headShadowText = r.headShadow.GetComponent<TMP_Text>();
            r.headText = r.head.GetComponent<TMP_Text>();
            r.headText.outlineColor = new Color32(0, 0, 0, 255); r.headText.outlineWidth = .22f;

            // ---- the OK hint ----
            r.hint = HudKit.NewRect("OkHint", root);
            UILayout.Place(r.hint, 1250f, 744f, 150f, 40f);
            r.hintCg = r.hint.gameObject.AddComponent<CanvasGroup>();
            var key = HudKit.NewRect("Key", r.hint);
            UILayout.Place(key, 0f, 4f, 32f, 32f);
            HudKit.AddImage(key, theme.blue);
            var keyText = HudKit.AddText(key, "F", 18f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            HudKit.Stretch(keyText.rectTransform);
            var ok = HudKit.AddText(r.hint, "OK", 28f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Ok", UITheme.TextRole.Display);
            UILayout.Place(ok.rectTransform, 42f, 0f, 100f, 40f);

            root.gameObject.SetActive(false);
            return r;
        }

        static KitBannerStrip Strip(RectTransform parent, string name, float x, float y, float w, float h, Color c)
        {
            var rt = HudKit.NewRect(name, parent);
            UILayout.Place(rt, x, y, w, h);
            var s = rt.gameObject.AddComponent<KitBannerStrip>();
            s.color = c; s.raycastTarget = false;
            return s;
        }

        static TMP_Text Number(RectTransform parent, string name, Color c)
        {
            var t = HudKit.AddText(parent, "0", NumberSize, c, true, TextAlignmentOptions.Center, name, UITheme.TextRole.Display);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            rt.gameObject.AddComponent<LintIgnoreClip>().reason = "the number slides through its window (RectMask2D): cut at the edges mid-change on purpose";
            return t;
        }

        static RectTransform Headline(RectTransform parent, string name, Color c, Vector2 offset)
        {
            var t = HudKit.AddText(parent, "RANK UP!", HeadSize, c, true, TextAlignmentOptions.Center, name, UITheme.TextRole.Display);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = t.rectTransform;
            UILayout.Place(rt, 300f, 70f, 820f, 190f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(710f + offset.x, -165f + offset.y);
            rt.localRotation = Quaternion.Euler(0f, 0f, 8f);
            rt.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the headline is slammed down over the art and the plate on purpose";
            return rt;
        }
    }
}
