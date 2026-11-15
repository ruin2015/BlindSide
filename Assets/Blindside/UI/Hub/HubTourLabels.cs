// The HQ tour's overview (HubTutorial), in the way the rescue intro shows its M.I.A profiles (IntroMiaCards): the camera straight down
// on the whole HQ with its ceiling off, a small tag over every facility, and — for the one CRUSHER is talking about — a profile card at
// the right tied to the room by a leader line.
//   Tag    the room's name 13 title on ink .88, a 3 px bar in its colour at the left (lit: the colour itself as the ground), pinned
//          Lift m over the room (its HubArea) as the camera moves
//   Card   DetailW × DetailH at the right (Right in, Top down), ink .94 with cut corners, a 6 px stripe in the room's colour; the
//          illustration of who runs it (OperatorArt.Body; none: the room's English name large and faint on its colour) DetailArtH high,
//          an English chip on it (OPERATIONS …); the name 26 title, 「担当 · …」 13, a rule, 「できること」 12 (blue) and a line 14 per
//          thing that can be done there
//   Line   from the card's left edge to the room, 2 px in the room's colour, a dot 10 on the room
// Motion (unscaled; ITimedEffect.Evaluate(t) = the tags' opening): the tags pop in .32 s each, Stagger apart, rising 10; a new card
// flickers in (a few blinks) sliding 30 from the right .35 s, then its line is drawn out to the room .3 s; Hide fades all .25 s.
// Everything is built when added / highlighted; per frame only positions and alphas change (no allocation).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class HubTourLabels : MonoBehaviour, ITimedEffect
    {
        public const int SortingOrder = 70;   // (under the conversation: KitDialogue 80)
        public const float Lift = 1.2f, PopTime = .32f, Stagger = .07f, Rise = 10f, DimAlpha = .55f, OutTime = .25f;
        public const float TagH = 26f, TagCharW = 13.5f;
        public const float DetailW = 360f, DetailH = 520f, Right = 40f, Top = 30f, DetailArtH = 220f, ActionH = 26f;
        public const float CardIn = .35f, LineDelay = .25f, LineTime = .3f, CardSlide = 30f;

        /// <summary>One facility: its name, colour, who runs it (an operator key: their art; null: none), the English chip, what is done there.</summary>
        public sealed class Room
        {
            public string name, en, host, hostLabel; public string[] actions; public Color accent; public Vector3 world;
        }

        sealed class Tag { public RectTransform rt; public CanvasGroup g; public Image bg; public Room room; public float delay; public Vector2 at, flat; public bool hasFlat, shown; }

        readonly List<Tag> tags = new List<Tag>(8);
        Canvas canvas;
        RectTransform root, card, line, dot;
        CanvasGroup rootG, cardG;
        Image dotImg;
        Camera cam;
        float clock = 99f, outClock = -1f, cardClock = 99f;
        int highlight = -1;
        Vector2 cardPos;

        public float Duration => PopTime + Stagger * Mathf.Max(0, tags.Count - 1);
        public int Count => tags.Count;

        public static HubTourLabels Build(Transform parent, Canvas onCanvas = null)
        {
            var theme = UITheme.Current;
            var c = onCanvas;
            if (c == null)
            {
                var cgo = new GameObject("HubTourLabelsCanvas", typeof(RectTransform));
                cgo.transform.SetParent(parent, false);
                c = cgo.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = SortingOrder;
                theme.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
            }
            var r = HudKit.NewRect("HubTourLabels", c.transform);
            HudKit.Stretch(r);
            var s = r.gameObject.AddComponent<HubTourLabels>();
            s.canvas = c; s.root = r;
            s.rootG = r.gameObject.AddComponent<CanvasGroup>();
            s.rootG.blocksRaycasts = false; s.rootG.interactable = false;
            r.gameObject.SetActive(false);
            return s;
        }

        // ---- the tags ----------------------------------------------------------------------------------------------------------------
        public void Add(Room room)
        {
            var theme = UITheme.Current;
            var rt = HudKit.NewRect("Tag_" + room.name, root);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(.5f, 0f);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            var bg = HudKit.AddImage(rt, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .88f));
            var bar = HudKit.NewRect("DecoBar", rt);
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(0f, 1f); bar.pivot = new Vector2(0f, .5f);
            bar.sizeDelta = new Vector2(3f, 0f); bar.anchoredPosition = Vector2.zero;
            HudKit.AddImage(bar, room.accent);
            var n = HudKit.AddText(rt, room.name, 13f, Color.white, true, TextAlignmentOptions.Center, "Name", UITheme.TextRole.Title);
            n.textWrappingMode = TextWrappingModes.NoWrap;
            // (the names are full-width: 13 px a character at 13 — measured text isn't ready while the canvas is being built)
            float w = room.name.Length * TagCharW + 22f;
            rt.sizeDelta = new Vector2(w, TagH);
            n.rectTransform.anchorMin = Vector2.zero; n.rectTransform.anchorMax = Vector2.one;
            n.rectTransform.offsetMin = new Vector2(5f, 0f); n.rectTransform.offsetMax = Vector2.zero;
            tags.Add(new Tag { rt = rt, g = g, bg = bg, room = room, delay = Stagger * tags.Count });
        }

        /// <summary>The tags pop in, following `view`.</summary>
        public void Show(Camera view)
        {
            cam = view;
            root.gameObject.SetActive(true);
            rootG.alpha = 1f;
            outClock = -1f;
            clock = UIMotionSettings.reduceMotion ? 99f : 0f;
            Highlight(-1);
            Place();
            Evaluate(clock);
        }

        public void Hide()
        {
            if (!root.gameObject.activeSelf) return;
            if (UIMotionSettings.reduceMotion) { root.gameObject.SetActive(false); return; }
            outClock = 0f;
        }

        // ---- the card: the room spoken about ----------------------------------------------------------------------------------------
        /// <summary>Room `i` spoken about: its tag lit and its card in, tied to it (−1: none).</summary>
        public void Highlight(int i)
        {
            var theme = UITheme.Current;
            highlight = i;
            for (int k = 0; k < tags.Count; k++)
                tags[k].bg.color = k == i ? tags[k].room.accent : new Color(theme.ink.r, theme.ink.g, theme.ink.b, .88f);
            BuildCard(i >= 0 && i < tags.Count ? tags[i].room : null);
            cardClock = UIMotionSettings.reduceMotion ? 99f : 0f;
            EvaluateCard(cardClock);
        }

        void BuildCard(Room room)
        {
            if (card != null) { HudKit.DestroySafe(card.gameObject); HudKit.DestroySafe(line.gameObject); HudKit.DestroySafe(dot.gameObject); card = line = dot = null; }
            if (room == null) return;
            var theme = UITheme.Current;

            line = HudKit.NewRect("DecoLine", root);
            line.anchorMin = line.anchorMax = Vector2.zero; line.pivot = new Vector2(0f, .5f);
            line.sizeDelta = new Vector2(0f, 2f);
            HudKit.AddImage(line, new Color(room.accent.r, room.accent.g, room.accent.b, .9f));
            dot = HudKit.NewRect("DecoDot", root);
            dot.anchorMin = dot.anchorMax = Vector2.zero; dot.pivot = new Vector2(.5f, .5f);
            dot.sizeDelta = new Vector2(10f, 10f);
            dotImg = HudKit.AddImage(dot, room.accent);

            card = HudKit.NewRect("Card_" + room.name, root);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(1f, 1f);
            cardPos = new Vector2(-Right, -Top);
            card.anchoredPosition = cardPos; card.sizeDelta = new Vector2(DetailW, DetailH);
            cardG = card.gameObject.AddComponent<CanvasGroup>();
            HudKit.AddImage(card, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .94f));
            card.gameObject.AddComponent<CutCornerGraphic>().Set(14f, CutCornerGraphic.Corners.Mockup);
            var stripe = HudKit.NewRect("DecoStripe", card);
            UILayout.Place(stripe, 0f, 0f, 6f, DetailH);
            HudKit.AddImage(stripe, room.accent);

            // the illustration of who runs it (or the room's English name, faint, on its colour)
            var artBack = HudKit.NewRect("ArtBack", card);
            UILayout.Place(artBack, 18f, 14f, DetailW - 32f, DetailArtH);
            HudKit.AddImage(artBack, Color.Lerp(Color.black, room.accent, .18f));
            artBack.gameObject.AddComponent<RectMask2D>();
            var body = !string.IsNullOrEmpty(room.host) ? OperatorArt.Body(room.host) : null;
            if (body != null)
            {
                var art = HudKit.NewRect("Art", artBack);
                HudKit.Stretch(art);
                var ai = art.gameObject.AddComponent<Image>();
                ai.sprite = body; ai.preserveAspect = true; ai.raycastTarget = false;
            }
            else
            {
                var big = HudKit.AddText(artBack, room.en, 44f, new Color(1f, 1f, 1f, .16f), true, TextAlignmentOptions.Center, "DecoEn", UITheme.TextRole.Display);
                HudKit.Stretch(big.rectTransform);
            }
            var chip = HudKit.NewRect("Chip", card);
            chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(1f, 1f);
            chip.anchoredPosition = new Vector2(-24f, -22f);
            var ct = HudKit.AddText(chip, room.en, 12f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            ct.textWrappingMode = TextWrappingModes.NoWrap;
            chip.sizeDelta = new Vector2(room.en.Length * 9.5f + 24f, 22f);   // (display caps, 12: ~9 px each, spaced)
            HudKit.Stretch(ct.rectTransform);
            HudKit.AddImage(chip, room.accent);   // (the chip's own ground; its text is a child, drawn over it)
            chip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 6f);

            float y = 14f + DetailArtH + 12f;
            var name = HudKit.AddText(card, room.name, 26f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Title);
            name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(name.rectTransform, 22f, y, DetailW - 40f, 34f);
            y += 38f;
            var host = HudKit.AddText(card, "担当 · " + room.hostLabel, 13f, theme.paper2, true, TextAlignmentOptions.TopLeft, "Host", UITheme.TextRole.Body);
            host.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(host.rectTransform, 22f, y, DetailW - 40f, 20f);
            y += 26f;
            var rule = HudKit.NewRect("DecoRule", card);
            UILayout.Place(rule, 22f, y, 40f, 3f);
            HudKit.AddImage(rule, room.accent);
            y += 12f;
            var tag = HudKit.AddText(card, "できること", 12f, theme.blue, true, TextAlignmentOptions.TopLeft, "Tag", UITheme.TextRole.Body);
            UILayout.Place(tag.rectTransform, 22f, y, DetailW - 40f, 18f);
            y += 22f;
            if (room.actions != null)
                foreach (var a in room.actions)
                {
                    var t = HudKit.AddText(card, "・" + a, 14f, Color.white, false, TextAlignmentOptions.TopLeft, "Action", UITheme.TextRole.Body);
                    t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
                    UILayout.Place(t.rectTransform, 22f, y, DetailW - 40f, ActionH - 4f);
                    y += ActionH;
                }
        }

        /// <summary>Previews: tag `i` at canvas point `p` (bottom-left origin) instead of over its room.</summary>
        public void PoseFlat(int i, Vector2 p) { tags[i].flat = p; tags[i].hasFlat = true; }

        /// <summary>Previews: everything in, room `i` spoken about.</summary>
        public void Pose(int highlightIndex)
        {
            root.gameObject.SetActive(true);
            rootG.alpha = 1f;
            clock = 99f; outClock = -1f;
            Highlight(highlightIndex);
            cardClock = 99f;
            Place(); Evaluate(99f); EvaluateCard(99f);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (clock < Duration) clock += dt;
            if (cardClock < CardIn + LineDelay + LineTime) cardClock += dt;
            if (outClock >= 0f)
            {
                outClock += dt;
                float a = 1f - Mathf.Clamp01(outClock / OutTime);
                if (rootG.alpha != a) rootG.alpha = a;
                if (outClock >= OutTime) { outClock = -1f; root.gameObject.SetActive(false); return; }
            }
            Place();
            Evaluate(clock);
            EvaluateCard(cardClock);
        }

        // over each room, on the canvas (bottom-left origin); a room behind the camera: its tag hidden
        void Place()
        {
            float sf = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            for (int i = 0; i < tags.Count; i++)
            {
                var t = tags[i];
                if (t.hasFlat) { t.at = t.flat; t.shown = true; continue; }
                if (cam == null) continue;
                var s = cam.WorldToScreenPoint(t.room.world + Vector3.up * Lift);
                t.shown = s.z > 0f;
                if (t.rt.gameObject.activeSelf != t.shown) t.rt.gameObject.SetActive(t.shown);
                if (t.shown) t.at = new Vector2(s.x / sf, s.y / sf);
            }
        }

        public void Evaluate(float t)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                var c = tags[i];
                float k = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01((t - c.delay) / PopTime));
                float a = k * (highlight < 0 || i == highlight ? 1f : DimAlpha);
                if (c.g.alpha != a) c.g.alpha = a;
                var p = c.at + new Vector2(0f, -Rise * (1f - k));
                if (c.rt.anchoredPosition != p) c.rt.anchoredPosition = p;
            }
        }

        // the card flickers in from the right, then its line is drawn out from its left edge to the room
        void EvaluateCard(float t)
        {
            if (card == null || highlight < 0 || highlight >= tags.Count) return;
            float k = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01(t / CardIn));
            bool blink = !UIMotionSettings.reduceMotion && t < CardIn * .6f && Mathf.Repeat(t, .08f) < .03f;
            float a = blink ? .25f : k;
            if (cardG.alpha != a) cardG.alpha = a;
            var p = cardPos + new Vector2(CardSlide * (1f - k), 0f);
            if (card.anchoredPosition != p) card.anchoredPosition = p;
            // the line: from the card's left edge (its upper third) to the room's tag
            var size = root.rect.size;
            var from = new Vector2(size.x - Right - DetailW, size.y - Top - DetailH * .3f);
            var to = tags[highlight].at + new Vector2(0f, TagH + 6f);   // (just over the tag: the dot never covers its name)
            float d = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01((t - LineDelay) / LineTime));
            var v = to - from;
            line.anchoredPosition = from;
            line.sizeDelta = new Vector2(v.magnitude * d, 2f);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
            dot.anchoredPosition = to;
            float da = d >= 1f ? 1f : 0f;
            if (dotImg.color.a != da) { var col = dotImg.color; col.a = da; dotImg.color = col; }
        }
    }
}
