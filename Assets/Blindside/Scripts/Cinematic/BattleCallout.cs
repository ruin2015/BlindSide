// The battle cinematic's full-screen words (BattleDirector, on its overlay canvas):
//   Callout   a first strike's opening hit — 「CHANCE!」 (ours: the accent) or 「WARNING!」 (theirs: foe red). A band across the middle of
//             the screen (ink .88, slanted ends, an accent rule top and bottom, hazard stripes running along them), the word huge
//             (display 104) with a line under it (先制攻撃 — PREEMPTIVE STRIKE / 敵の先制攻撃 — AMBUSHED), a tint flash over the screen
//   Shutter   the cut into the fight (in place of a plain fade): two slanted ink panels close in from the left and the right, an
//             accent seam where they meet, 「ENGAGE」 and 「交戦開始」 stamped on it while closed; then they open again
// Motion (unscaled; ITimedEffect.Evaluate(t) = the callout): the band snaps open from the middle (.14), the word slams down from 2.2×
// with a jolt (steps), the stripes run, the flash (WARNING: two red pulses); the band closes (.18) at the end. The shutter is posed by
// Cover(k), 0 → 1 → 0, driven by the director's fade. Built once; nothing is allocated per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public class BattleCallout : MonoBehaviour, ITimedEffect
    {
        public const float Seconds = 1.05f, OpenTime = .14f, SlamTime = .16f, CloseTime = .18f, BandH = 170f, Overhang = 140f;
        public float Duration => Seconds;

        RectTransform root, band, word, sub, shutterL, shutterR, seam, engage;
        CanvasGroup bandG, engageG;
        TMP_Text wordText, subText, wordShadow;
        Image flash, bandImg, ruleTop, ruleBottom, seamImg;
        HazardStripe stripeTop, stripeBottom;
        bool warning;
        float clock = 99f;

        public static BattleCallout Build(Transform canvas)
        {
            var theme = UITheme.Current;
            var r = HudKit.NewRect("BattleCallout", canvas);
            HudKit.Stretch(r);
            var c = r.gameObject.AddComponent<BattleCallout>();
            c.root = r;

            // ---- the shutter (under the callout) ----
            c.shutterL = Panel(r, "DecoShutterL", 0f, theme.ink);
            c.shutterR = Panel(r, "DecoShutterR", 1f, theme.ink);
            c.seam = HudKit.NewRect("DecoSeam", r);
            c.seam.anchorMin = new Vector2(.5f, 0f); c.seam.anchorMax = new Vector2(.5f, 1f); c.seam.pivot = new Vector2(.5f, .5f);
            c.seam.sizeDelta = new Vector2(6f, 40f); c.seam.localRotation = Quaternion.Euler(0f, 0f, -12f);
            c.seamImg = HudKit.AddImage(c.seam, UIPalette.Accent);
            c.engage = HudKit.NewRect("Engage", r);
            c.engage.anchorMin = c.engage.anchorMax = c.engage.pivot = new Vector2(.5f, .5f);
            c.engage.sizeDelta = new Vector2(700f, 120f);
            c.engageG = c.engage.gameObject.AddComponent<CanvasGroup>();
            var en = HudKit.AddText(c.engage, "ENGAGE", 64f, Color.white, true, TextAlignmentOptions.Center, "En", UITheme.TextRole.Display);
            en.characterSpacing = 18f;
            UILayout.Place(en.rectTransform, 0f, 0f, 700f, 80f);
            var jp = HudKit.AddText(c.engage, "交戦開始", 18f, UIPalette.Accent, true, TextAlignmentOptions.Center, "Jp", UITheme.TextRole.Title);
            UILayout.Place(jp.rectTransform, 0f, 84f, 700f, 26f);

            // ---- the callout ----
            var fl = HudKit.NewRect("DecoFlash", r);
            HudKit.Stretch(fl);
            c.flash = HudKit.AddImage(fl, new Color(1f, 1f, 1f, 0f));
            c.band = HudKit.NewRect("Band", r);
            c.band.anchorMin = new Vector2(0f, .5f); c.band.anchorMax = new Vector2(1f, .5f); c.band.pivot = new Vector2(.5f, .5f);
            c.band.sizeDelta = new Vector2(0f, BandH);
            c.bandG = c.band.gameObject.AddComponent<CanvasGroup>();
            c.bandImg = HudKit.AddImage(c.band, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .88f));
            c.band.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 60f);
            c.ruleTop = Rule(c.band, "DecoRuleTop", 1f, out c.stripeTop);
            c.ruleBottom = Rule(c.band, "DecoRuleBottom", 0f, out c.stripeBottom);
            c.word = HudKit.NewRect("Word", c.band);
            c.word.anchorMin = c.word.anchorMax = c.word.pivot = new Vector2(.5f, .5f);
            c.word.anchoredPosition = new Vector2(0f, 14f); c.word.sizeDelta = new Vector2(1000f, 120f);
            c.wordShadow = HudKit.AddText(c.word, "CHANCE!", 104f, new Color(0f, 0f, 0f, .7f), true, TextAlignmentOptions.Center, "DecoShadow", UITheme.TextRole.Display);
            HudKit.Stretch(c.wordShadow.rectTransform);
            c.wordShadow.rectTransform.anchoredPosition = new Vector2(5f, -5f);
            c.wordText = HudKit.AddText(c.word, "CHANCE!", 104f, UIPalette.Accent, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            HudKit.Stretch(c.wordText.rectTransform);
            c.wordText.characterSpacing = c.wordShadow.characterSpacing = 6f;
            c.sub = HudKit.NewRect("Sub", c.band);
            c.sub.anchorMin = c.sub.anchorMax = c.sub.pivot = new Vector2(.5f, .5f);
            c.sub.anchoredPosition = new Vector2(0f, -58f); c.sub.sizeDelta = new Vector2(800f, 28f);
            c.subText = HudKit.AddText(c.sub, "", 18f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            HudKit.Stretch(c.subText.rectTransform);

            foreach (var g in r.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            c.Cover(0f);
            c.Evaluate(99f);
            return c;
        }

        static RectTransform Panel(RectTransform parent, string name, float side, Color c)
        {
            var p = HudKit.NewRect(name, parent);
            p.anchorMin = new Vector2(side, 0f); p.anchorMax = new Vector2(side, 1f); p.pivot = new Vector2(side, .5f);
            p.sizeDelta = new Vector2(0f, 0f);
            HudKit.AddImage(p, new Color(c.r, c.g, c.b, 1f));
            p.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, side < .5f ? SlantedGraphic.Direction.Right : SlantedGraphic.Direction.Left, 120f);
            return p;
        }

        static Image Rule(RectTransform band, string name, float y, out HazardStripe stripe)
        {
            var rt = HudKit.NewRect(name, band);
            rt.anchorMin = new Vector2(0f, y); rt.anchorMax = new Vector2(1f, y); rt.pivot = new Vector2(.5f, y);
            rt.sizeDelta = new Vector2(0f, 4f); rt.anchoredPosition = Vector2.zero;
            var img = HudKit.AddImage(rt, UIPalette.Accent);
            var hz = HudKit.NewRect("DecoStripes", band);
            hz.anchorMin = new Vector2(0f, y); hz.anchorMax = new Vector2(1f, y); hz.pivot = new Vector2(.5f, y > .5f ? 0f : 1f);
            hz.sizeDelta = new Vector2(0f, 10f); hz.anchoredPosition = new Vector2(0f, y > .5f ? 6f : -6f);
            stripe = HazardStripe.Create(hz, "DecoHazard", UIPalette.Accent, new Color(0f, 0f, 0f, 0f), 10f, 14f);
            return img;
        }

        // ---- the callout ---------------------------------------------------------------------------------------------------------
        /// <summary>「CHANCE!」 (our first strike landed) or 「WARNING!」 (theirs), from now.</summary>
        public void Play(bool enemy)
        {
            warning = enemy;
            var c = enemy ? UIPalette.Foe : UIPalette.Accent;
            wordText.SetText(enemy ? "WARNING!" : "CHANCE!");
            wordShadow.SetText(enemy ? "WARNING!" : "CHANCE!");
            wordText.color = enemy ? UIPalette.Hex("#FF5A6E") : UIPalette.Accent;
            subText.SetText(enemy ? "敵の先制攻撃  —  AMBUSHED" : "先制攻撃  —  PREEMPTIVE STRIKE");
            ruleTop.color = ruleBottom.color = c;
            if (stripeTop != null) { stripeTop.stripeColor = c; stripeTop.SetVerticesDirty(); }
            if (stripeBottom != null) { stripeBottom.stripeColor = c; stripeBottom.SetVerticesDirty(); }
            clock = UIMotionSettings.reduceMotion ? SlamTime : 0f;
            Evaluate(clock);
        }

        public bool Playing => clock < Seconds;

        void Update()
        {
            if (clock >= Seconds) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            bool on = t < Seconds;
            if (band.gameObject.activeSelf != on) band.gameObject.SetActive(on);
            if (!on) { if (flash.color.a != 0f) flash.color = new Color(1f, 1f, 1f, 0f); return; }
            // the band: snapped open from the middle, closed at the end
            float open = UIEase.Tactical(Mathf.Clamp01(t / OpenTime));
            float close = t > Seconds - CloseTime ? UIEase.Tactical(Mathf.Clamp01((t - (Seconds - CloseTime)) / CloseTime)) : 0f;
            band.localScale = new Vector3(open, 1f - close, 1f);
            bandG.alpha = 1f - close * .5f;
            // the word: slammed down from 2.2×, a jolt as it lands
            float s = UIEase.Tactical(Mathf.Clamp01((t - .04f) / SlamTime));
            float sc = t < .04f ? 0f : Mathf.Lerp(2.2f, 1f, s);
            word.localScale = new Vector3(sc, sc, 1f);
            float jolt = t >= .2f && t < .28f ? ((int)((t - .2f) / .02f) % 2 == 0 ? 8f : -6f) : 0f;   // (steps)
            word.anchoredPosition = new Vector2(jolt, 14f);
            sub.localScale = new Vector3(1f, t < .22f ? 0f : 1f, 1f);
            // the flash: a white tick for ours; two red pulses for theirs
            float a;
            if (!warning) a = t < .2f ? .28f * (1f - t / .2f) : 0f;
            else a = t < .5f ? .26f * Mathf.Abs(Mathf.Sin(t / .5f * Mathf.PI * 2f)) : 0f;
            var fc = warning ? new Color(1f, .15f, .25f, a) : new Color(1f, 1f, 1f, a);
            if (flash.color != fc) flash.color = fc;
        }

        // ---- the shutter -----------------------------------------------------------------------------------------------------------
        /// <summary>The cut into the fight: 0 open … 1 closed (the panels meet; ENGAGE on the seam).</summary>
        public void Cover(float k)
        {
            k = Mathf.Clamp01(k);
            float e = UIEase.Tactical(k);
            float w = root.rect.width > 1f ? root.rect.width : 1920f;
            // each panel starts Overhang outside its screen edge (its outer slant off screen) and reaches past the middle by as much
            float half = w * .5f + Overhang * 2f;
            var size = new Vector2(half * e + (e > 0f ? Overhang : 0f), 0f);
            if (shutterL.sizeDelta != size)
            {
                shutterL.sizeDelta = size; shutterR.sizeDelta = size;
                shutterL.anchoredPosition = new Vector2(-Overhang, 0f); shutterR.anchoredPosition = new Vector2(Overhang, 0f);
            }
            bool show = k > .001f;
            if (shutterL.gameObject.activeSelf != show) { shutterL.gameObject.SetActive(show); shutterR.gameObject.SetActive(show); }
            bool closed = k > .85f;
            if (seam.gameObject.activeSelf != closed) seam.gameObject.SetActive(closed);
            float ea = Mathf.Clamp01((k - .7f) / .3f);
            if (engageG.alpha != ea) engageG.alpha = ea;
            engage.localScale = new Vector3(1f + (1f - ea) * .4f, 1f, 1f);
        }
    }
}
