// The rescue intro's M.I.A profiles: one card per captured operator over the hologram, each tied by a leader line to its marker.
//   card    340 × 124, ink (cut corners), the operator's colour as a stripe on the left; the face (96, on the paper gradient), the codename
//           26 display, 「ゴースト · 索敵」 13, a red 「M.I.A」 chip top right and 「敵に拘束中 · 位置特定」 12 (amber) at the foot
//   place   each card on its marker's side of the screen (the caller says which; CardY apart when two share a side), the line from the card's inner edge to the
//           marker's point on screen (followed every frame as the camera orbits), a dot there
//   motion  from its marker's impact: the card flickers in (a few blinks) sliding in from its side, the chip stamps down (scale 1.7 → 1),
//           the line is drawn from the card out to the marker; Hide fades everything out (.25 s)
// ITimedEffect-like: Tick(t) poses all cards at t seconds of the intro's hologram clock (unscaled pacing comes from the Timeline's clock);
// nothing is allocated per frame.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public class IntroMiaCards : MonoBehaviour
    {
        public const float CardW = 340f, CardH = 124f, SideX = 70f, TopY = 250f, CardY = 170f, FaceS = 96f;
        public const float FlickerTime = .3f, SlideTime = .35f, LineTime = .4f;
        static readonly Color MiaRed = new Color32(0xff, 0x3b, 0x54, 0xff);

        sealed class Card
        {
            public RectTransform rt, line, dot, chip;
            public CanvasGroup group;
            public Vector2 pos;
            public float at;          // when it comes in (s, hologram clock)
            public bool right;        // on the right of the screen
            public float w = CardW, h = CardH;
        }

        // the enemy organisation's card (the wipe-out operation): the illustration large, who they are, how many are to be put down
        public const float TargetW = 400f, TargetH = 450f, TargetTop = 130f, TargetArtH = 256f, TargetFace = 44f, TargetFaceGap = 8f;

        RectTransform root;
        CanvasGroup rootGroup;
        readonly List<Card> cards = new List<Card>(2);
        float hideAt = -1f;

        public static IntroMiaCards Build(Transform canvas)
        {
            var r = HudKit.NewRect("IntroMiaCards", canvas);
            HudKit.Stretch(r);
            var c = r.gameObject.AddComponent<IntroMiaCards>();
            c.root = r;
            c.rootGroup = r.gameObject.AddComponent<CanvasGroup>();
            c.rootGroup.blocksRaycasts = false; c.rootGroup.interactable = false;
            r.gameObject.SetActive(false);
            return c;
        }

        /// <summary>Clears the cards (a new intro).</summary>
        public void Clear()
        {
            foreach (var k in cards) { if (k.rt != null) Destroy(k.rt.gameObject); if (k.line != null) Destroy(k.line.gameObject); if (k.dot != null) Destroy(k.dot.gameObject); }
            cards.Clear();
            hideAt = -1f;
            rootGroup.alpha = 1f;
            gameObject.SetActive(false);
        }

        /// <summary>Adds `op`'s card on the left or the `right` of the screen (the side its marker is on, so the lines don't cross),
        /// coming in at `at` (hologram clock).</summary>
        public void Add(OperatorData op, float at, bool right)
        {
            if (op == null) return;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var theme = UITheme.Current;
            int i = cards.Count;
            var k = new Card { at = at, right = right };
            int same = 0; foreach (var o in cards) if (o.right == right) same++;
            float y = TopY + same * CardY + (right ? CardY * .55f : 0f);

            // the leader line and its dot (under the card)
            k.line = HudKit.NewRect("DecoLine" + i, root);
            k.line.anchorMin = k.line.anchorMax = new Vector2(.5f, .5f); k.line.pivot = new Vector2(0f, .5f);
            HudKit.AddImage(k.line, new Color(op.color.r, op.color.g, op.color.b, .85f));
            k.dot = HudKit.NewRect("DecoDot" + i, root);
            k.dot.anchorMin = k.dot.anchorMax = k.dot.pivot = new Vector2(.5f, .5f);
            k.dot.sizeDelta = new Vector2(10f, 10f);
            HudKit.AddImage(k.dot, op.color);

            k.rt = HudKit.NewRect("MiaCard_" + op.key, root);
            k.rt.anchorMin = k.rt.anchorMax = k.rt.pivot = new Vector2(k.right ? 1f : 0f, 1f);
            k.pos = new Vector2(k.right ? -SideX : SideX, -y);
            k.rt.anchoredPosition = k.pos; k.rt.sizeDelta = new Vector2(CardW, CardH);
            k.group = k.rt.gameObject.AddComponent<CanvasGroup>();
            var face = HudKit.AddImage(k.rt, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .92f));
            k.rt.gameObject.AddComponent<CutCornerGraphic>().Set(14f, CutCornerGraphic.Corners.Mockup);
            var stripe = HudKit.NewRect("DecoStripe", k.rt);
            UILayout.Place(stripe, 0f, 0f, 6f, CardH);
            HudKit.AddImage(stripe, op.color);

            var slot = HudKit.NewRect("Face", k.rt);
            UILayout.Place(slot, 16f, (CardH - FaceS) * .5f, FaceS, FaceS);
            var slotImg = HudKit.AddImage(slot, Color.Lerp(theme.paper2, theme.ink, .2f));
            slot.gameObject.AddComponent<RectMask2D>();
            OperatorArt.ApplyFace(slotImg, op.key);
            var under = HudKit.NewRect("DecoFaceLine", k.rt);
            UILayout.Place(under, 16f, (CardH - FaceS) * .5f + FaceS, FaceS, 3f);
            HudKit.AddImage(under, op.color);

            float x = 16f + FaceS + 14f;
            var name = HudKit.AddText(k.rt, op.DisplayName, 26f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(name.rectTransform, x, 14f, CardW - x - 90f, 32f);
            var sub = HudKit.AddText(k.rt, op.kr + " · " + op.role, 13f, theme.paper2, true, TextAlignmentOptions.TopLeft, "Sub", UITheme.TextRole.Body);
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(sub.rectTransform, x, 50f, CardW - x - 12f, 20f);
            var rule = HudKit.NewRect("DecoRule", k.rt);
            UILayout.Place(rule, x, 76f, 40f, 3f);
            HudKit.AddImage(rule, op.color);
            var st = HudKit.AddText(k.rt, "敵に拘束中 · 位置特定", 12f, theme.acc, true, TextAlignmentOptions.TopLeft, "Status", UITheme.TextRole.Body);
            st.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(st.rectTransform, x, 88f, CardW - x - 12f, 18f);

            k.chip = HudKit.NewRect("Chip", k.rt);
            k.chip.anchorMin = k.chip.anchorMax = k.chip.pivot = new Vector2(1f, 1f);
            k.chip.anchoredPosition = new Vector2(-12f, -12f); k.chip.sizeDelta = new Vector2(70f, 24f);
            HudKit.AddImage(k.chip, MiaRed);
            k.chip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 6f);
            var ct = HudKit.AddText(k.chip, "M.I.A", 13f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            HudKit.Stretch(ct.rectTransform);

            cards.Add(k);
            Pose(k, -1f, Vector2.zero, false);
        }

        /// <summary>The operation's targets on one card on the right: `op`'s illustration, the organisation and `count` faces, the 鎮圧対象 chip;
        /// comes in at `at`, its line to the marker as the M.I.A cards'.</summary>
        public void AddTarget(OperatorData op, int count, float at)
        {
            if (op == null) return;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            var theme = UITheme.Current;
            var k = new Card { at = at, right = true, w = TargetW, h = TargetH };

            k.line = HudKit.NewRect("DecoLineTarget", root);
            k.line.anchorMin = k.line.anchorMax = new Vector2(.5f, .5f); k.line.pivot = new Vector2(0f, .5f);
            HudKit.AddImage(k.line, new Color(MiaRed.r, MiaRed.g, MiaRed.b, .85f));
            k.dot = HudKit.NewRect("DecoDotTarget", root);
            k.dot.anchorMin = k.dot.anchorMax = k.dot.pivot = new Vector2(.5f, .5f);
            k.dot.sizeDelta = new Vector2(10f, 10f);
            HudKit.AddImage(k.dot, MiaRed);

            k.rt = HudKit.NewRect("TargetCard_" + op.key, root);
            k.rt.anchorMin = k.rt.anchorMax = k.rt.pivot = new Vector2(1f, 1f);
            k.pos = new Vector2(-SideX, -TargetTop);
            k.rt.anchoredPosition = k.pos; k.rt.sizeDelta = new Vector2(TargetW, TargetH);
            k.group = k.rt.gameObject.AddComponent<CanvasGroup>();
            HudKit.AddImage(k.rt, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .94f));
            k.rt.gameObject.AddComponent<CutCornerGraphic>().Set(14f, CutCornerGraphic.Corners.Mockup);
            var stripe = HudKit.NewRect("DecoStripe", k.rt);
            UILayout.Place(stripe, 0f, 0f, 6f, TargetH);
            HudKit.AddImage(stripe, MiaRed);

            var artBack = HudKit.NewRect("DecoArtBack", k.rt);
            UILayout.Place(artBack, 18f, 14f, TargetW - 32f, TargetArtH);
            HudKit.AddImage(artBack, Color.black);   // (the illustration's own ground)
            artBack.gameObject.AddComponent<RectMask2D>();
            var art = HudKit.NewRect("Art", artBack);
            HudKit.Stretch(art);
            var artImg = art.gameObject.AddComponent<Image>();
            artImg.sprite = OperatorArt.Body(op.key); artImg.preserveAspect = true; artImg.raycastTarget = false;
            artImg.enabled = artImg.sprite != null;

            float y = 14f + TargetArtH + 10f;
            var name = HudKit.AddText(k.rt, op.DisplayName, 28f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(name.rectTransform, 18f, y, TargetW - 36f, 34f);
            var sub = HudKit.AddText(k.rt, op.kr + " · 武装組織の構成員", 14f, theme.paper2, true, TextAlignmentOptions.TopLeft, "Sub", UITheme.TextRole.Body);
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(sub.rectTransform, 18f, y + 36f, TargetW - 36f, 20f);
            var st = HudKit.AddText(k.rt, "鎮圧対象 · " + count + "名", 16f, theme.acc, true, TextAlignmentOptions.TopLeft, "Status", UITheme.TextRole.Body);
            st.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(st.rectTransform, 18f, y + 60f, TargetW - 36f, 24f);
            int shown = Mathf.Min(count, 7);   // (as many faces as fit across the card)
            for (int i = 0; i < shown; i++)
            {
                var slot = HudKit.NewRect("Portrait" + i, k.rt);   // (the paper behind it; ApplyFace puts the face in as a child)
                UILayout.Place(slot, 18f + i * (TargetFace + TargetFaceGap), y + 90f, TargetFace, TargetFace);
                var img = HudKit.AddImage(slot, Color.Lerp(theme.paper2, theme.ink, .2f));
                slot.gameObject.AddComponent<RectMask2D>();
                OperatorArt.ApplyFace(img, op.key);
            }

            k.chip = HudKit.NewRect("Chip", k.rt);
            k.chip.anchorMin = k.chip.anchorMax = k.chip.pivot = new Vector2(1f, 1f);
            k.chip.anchoredPosition = new Vector2(-22f, -22f); k.chip.sizeDelta = new Vector2(92f, 28f);
            HudKit.AddImage(k.chip, MiaRed);
            k.chip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 6f);
            var ct = HudKit.AddText(k.chip, "TARGET", 14f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            HudKit.Stretch(ct.rectTransform);

            cards.Add(k);
            Pose(k, -1f, Vector2.zero, false);
        }

        /// <summary>Previews: every card settled.</summary>
        public void PoseSettled() { for (int i = 0; i < cards.Count; i++) Pose(cards[i], 9f, Vector2.zero, false); }

        /// <summary>Starts the fade-out at `t` (hologram clock).</summary>
        public void Hide(float t) { if (hideAt < 0f) hideAt = t; }

        /// <summary>Poses every card at `t` (hologram clock); `markerWorld(i)` is card i's marker (world), projected through `cam`.</summary>
        public void Tick(float t, Camera cam, System.Func<int, Vector3> markerWorld)
        {
            if (!gameObject.activeSelf) return;
            rootGroup.alpha = hideAt >= 0f ? Mathf.Clamp01(1f - (t - hideAt) / .25f) : 1f;
            for (int i = 0; i < cards.Count; i++)
            {
                var k = cards[i];
                Vector2 local = Vector2.zero; bool on = false;
                if (cam != null && markerWorld != null)
                {
                    var sp = cam.WorldToScreenPoint(markerWorld(i));
                    if (sp.z > 0f) on = RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out local);
                }
                Pose(k, t - k.at, local, on);
            }
        }

        void Pose(Card k, float s, Vector2 marker, bool markerOn)
        {
            bool reduce = UIMotionSettings.reduceMotion;
            // the card: flickers in (on / off in steps), sliding in from its side
            float a;
            if (s < 0f) a = 0f;
            else if (s < FlickerTime && !reduce) a = ((int)(s / .05f)) % 2 == 0 ? 1f : .25f;
            else a = 1f;
            k.group.alpha = a;
            float slide = s < 0f ? 1f : 1f - UIEase.Tactical(Mathf.Clamp01(s / SlideTime));
            k.rt.anchoredPosition = k.pos + new Vector2((k.right ? 60f : -60f) * (reduce ? 0f : slide), 0f);
            // the chip stamps down
            float c = Mathf.Clamp01((s - .15f) / .2f);
            float cs = s < .15f ? 0f : Mathf.Lerp(1.7f, 1f, UIEase.Tactical(c));
            k.chip.localScale = new Vector3(cs, cs, 1f);

            // the leader line: from the card's inner edge (its vertical middle) out to the marker
            var rect = root.rect;
            float edgeX = k.right ? rect.width * .5f + k.rt.anchoredPosition.x - k.w : -rect.width * .5f + k.rt.anchoredPosition.x + k.w;
            var from = new Vector2(edgeX, rect.height * .5f + k.rt.anchoredPosition.y - k.h * .5f);
            float lp = s < .1f ? 0f : UIEase.Tactical(Mathf.Clamp01((s - .1f) / LineTime));
            bool lineOn = markerOn && lp > 0f;
            if (k.line.gameObject.activeSelf != lineOn) k.line.gameObject.SetActive(lineOn);
            if (k.dot.gameObject.activeSelf != (lineOn && lp >= 1f)) k.dot.gameObject.SetActive(lineOn && lp >= 1f);
            if (!lineOn) return;
            var d = marker - from;
            k.line.anchoredPosition = from;
            k.line.sizeDelta = new Vector2(d.magnitude * lp, 2f);
            k.line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            k.dot.anchoredPosition = marker;
        }
    }
}
