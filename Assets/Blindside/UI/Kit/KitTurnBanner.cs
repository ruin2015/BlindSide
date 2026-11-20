// The turn change (the user's design): a dark band across the middle of the screen between two thick lines in the side's colour (blue:
// the player's phase, red: the enemy's), "Turn N" over a big rough title ("PLAYER PHASE" / "ENEMY PHASE", Rubik Dirt, white).
//   TurnBanner (full width, BandH high at the middle; never takes clicks)
//     DecoBand            ink .74, opening from its middle (scaleY)
//     DecoLineTop/Bottom  LineH in the colour, shooting out from the centre (scaleX)
//     Turn                "Turn" + the number (bigger), in the colour, Chakra Petch Bold Italic
//     DecoGhostR / DecoGhostC   red / cyan copies of the title, split off it while it glitches
//     Title               the title, white
//     DecoSlice0..2       thin light bars cutting across the title during the glitches
// Motion (Duration 1.9s, the old banner's length): the band opens .22s, the lines shoot out .05–.35s, Turn drops in .15–.4s (the
// number from ×1.6); the title glitches in .18–.55s (RGB split, jitter, slices, steps of 1/30s), blips once at 1.0s, and glitches out
// from 1.45s while the lines draw back and the band closes. Every pose is Evaluate(t) on one unscaled clock; the jitter comes from a hash
// of the step (no random stream touched, nothing allocated per frame).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Turn Banner")]
    public class KitTurnBanner : MonoBehaviour, ITimedEffect
    {
        public const float BandH = 210f, LineH = 9f, BandAlpha = .74f, TitleSize = 96f, TurnSize = 34f;
        public const float OpenTime = .22f, LineFrom = .05f, LineTime = .3f, TurnFrom = .15f, TurnTime = .25f;
        public const float GlitchIn = .18f, GlitchInEnd = .55f, BlipAt = 1f, BlipTime = .08f, OutFrom = 1.45f, Total = 1.9f, StepRate = 30f;
        public const string PlayerTitle = "PLAYER  PHASE", EnemyTitle = "ENEMY  PHASE", TurnWord = "Turn";
        static readonly Color GhostR = new Color(1f, .16f, .3f, 1f), GhostC = new Color(.15f, .8f, 1f, 1f);

        [Header("Parts")]
        public RectTransform band, lineTop, lineBottom, turnRt, titleRt, ghostR, ghostC;
        public RectTransform[] slices = new RectTransform[3];
        public Image bandImg, lineTopImg, lineBottomImg;
        public Image[] sliceImg = new Image[3];
        public TMP_Text turnText, title, ghostRText, ghostCText;
        public CanvasGroup group;

        float clock;
        bool running;

        public bool Playing => running;
        public float Duration => Total;

        /// <summary>Shows "Turn `round`" over `titleText` in `color` and plays the 1.9s banner.</summary>
        public void Play(int round, string titleText, Color color)
        {
            turnText.SetText("Turn<size=150%> {0:0}</size>", round);
            Begin(titleText, color);
        }

        /// <summary>The same banner with its own small line over the title (the deployment: "BEFORE BREACH" · DEPLOYMENT).</summary>
        public void Play(string label, string titleText, Color color)
        {
            turnText.text = label ?? "";
            Begin(titleText, color);
        }

        public const string DeployLabel = "BEFORE BREACH", DeployTitle = "DEPLOYMENT", PrepTitle = "PREPARATION";

        void Begin(string titleText, Color color)
        {
            gameObject.SetActive(true);
            title.text = ghostRText.text = ghostCText.text = titleText;
            turnText.color = color;
            lineTopImg.color = lineBottomImg.color = color;
            clock = 0f;
            running = Application.isPlaying;
            Evaluate(0f);
        }

        public void Hide() { running = false; gameObject.SetActive(false); }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        static float Ease(float p) => UIEase.Tactical(Mathf.Clamp01(p));

        /// <summary>A repeatable "random" in −1…1 for glitch step `n`, channel `k`.</summary>
        static float Hash(int n, int k)
        {
            uint h = (uint)(n * 374761393 + k * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xffff) / 32767.5f - 1f;
        }

        public void Evaluate(float t)
        {
            bool reduce = UIMotionSettings.reduceMotion;
            float outK = Ease((t - OutFrom) / (Total - OutFrom));            // 0 → 1 while it leaves

            // the band opens from its middle, and closes again
            float open = Ease(t / OpenTime) * (1f - outK);
            band.localScale = new Vector3(1f, open, 1f);
            var ink = UITheme.Current.ink;
            bandImg.color = new Color(ink.r, ink.g, ink.b, BandAlpha * Mathf.Clamp01(open * 1.4f));

            // the lines shoot out from the centre, and draw back
            float ln = Ease((t - LineFrom) / LineTime) * (1f - outK);
            lineTop.localScale = lineBottom.localScale = new Vector3(ln, 1f, 1f);
            float half = BandH * .5f * open;
            lineTop.anchoredPosition = new Vector2(0f, half);
            lineBottom.anchoredPosition = new Vector2(0f, -half);

            // Turn N drops in (the number from ×1.6)
            float tk = Ease((t - TurnFrom) / TurnTime);
            float ta = tk * (1f - outK);
            var tc = turnText.color; tc.a = ta; turnText.color = tc;
            turnRt.anchoredPosition = new Vector2(0f, 62f + 14f * (1f - tk));
            float ts = Mathf.Lerp(1.6f, 1f, tk);
            turnRt.localScale = reduce ? Vector3.one : new Vector3(ts, ts, 1f);

            // the title: glitching in, a blip, glitching out — steps of 1/30s
            int step = Mathf.FloorToInt(t * StepRate);
            float g = 0f;                                                       // glitch strength 0…1
            if (t >= GlitchIn && t < GlitchInEnd) g = 1f - (t - GlitchIn) / (GlitchInEnd - GlitchIn);
            else if (t >= BlipAt && t < BlipAt + BlipTime) g = .55f;
            else if (t >= OutFrom) g = Mathf.Clamp01((t - OutFrom) / (Total - OutFrom) * 1.6f);
            if (reduce) g = 0f;
            bool shown = t >= GlitchIn && t < Total;
            float alpha = !shown ? 0f : t >= OutFrom ? 1f - outK : (g > .6f && Hash(step, 9) > .3f ? .35f : 1f);
            float jx = g * 26f * Hash(step, 1), jy = g * 5f * Hash(step, 2);
            titleRt.anchoredPosition = new Vector2(jx, -14f + jy);
            title.alpha = alpha;
            float split = g * 14f;
            ghostR.anchoredPosition = new Vector2(jx - split * (1f + Hash(step, 3) * .5f), -14f + jy + g * 3f * Hash(step, 4));
            ghostC.anchoredPosition = new Vector2(jx + split * (1f + Hash(step, 5) * .5f), -14f + jy - g * 3f * Hash(step, 6));
            float ga = shown ? Mathf.Clamp01(g * 1.3f) * .85f : 0f;
            ghostRText.color = new Color(GhostR.r, GhostR.g, GhostR.b, ga);
            ghostCText.color = new Color(GhostC.r, GhostC.g, GhostC.b, ga);

            // light bars cutting across the title while it glitches
            for (int i = 0; i < slices.Length; i++)
            {
                bool on = g > .2f && Hash(step, 10 + i) > .1f;
                var c = sliceImg[i].color; c.a = on ? .35f + .4f * g : 0f; sliceImg[i].color = c;
                if (on) slices[i].anchoredPosition = new Vector2(g * 60f * Hash(step, 20 + i), -14f + 46f * Hash(step, 30 + i));
            }

            float ga2 = t >= Total ? 0f : 1f;
            if (group.alpha != ga2) group.alpha = ga2;
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Total) { running = false; gameObject.SetActive(false); }
        }

        // ---- build ------------------------------------------------------------------------------------------------------------
        static RectTransform Line(Transform parent, string name, out Image img)
        {
            var rt = HudKit.NewRect(name, parent);
            rt.anchorMin = new Vector2(0f, .5f); rt.anchorMax = new Vector2(1f, .5f); rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(0f, LineH);
            img = HudKit.AddImage(rt, Color.white);
            return rt;
        }

        static TMP_Text TitleText(Transform parent, string name, TMP_FontAsset font, Color c)
        {
            var t = HudKit.AddText(parent, EnemyTitle, TitleSize, c, true, TextAlignmentOptions.Center, name, UITheme.TextRole.Display);
            if (font != null) t.font = font;
            t.characterSpacing = 6f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(1400f, 120f);
            rt.anchoredPosition = new Vector2(0f, -14f);
            return t;
        }

        /// <summary>The banner on `canvas` (the HUD canvas), hidden until Play.</summary>
        public static KitTurnBanner Build(Transform canvas)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("TurnBanner", canvas);
            root.anchorMin = new Vector2(0f, .5f); root.anchorMax = new Vector2(1f, .5f); root.pivot = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(0f, BandH + LineH * 2f); root.anchoredPosition = Vector2.zero;
            var b = root.gameObject.AddComponent<KitTurnBanner>();
            b.group = root.gameObject.AddComponent<CanvasGroup>();
            b.group.blocksRaycasts = false; b.group.interactable = false;

            b.band = HudKit.NewRect("DecoBand", root);
            b.band.anchorMin = new Vector2(0f, .5f); b.band.anchorMax = new Vector2(1f, .5f); b.band.pivot = new Vector2(.5f, .5f);
            b.band.sizeDelta = new Vector2(0f, BandH);
            b.bandImg = HudKit.AddImage(b.band, new Color(theme.ink.r, theme.ink.g, theme.ink.b, BandAlpha));
            b.lineTop = Line(root, "DecoLineTop", out b.lineTopImg);
            b.lineBottom = Line(root, "DecoLineBottom", out b.lineBottomImg);

            b.turnText = HudKit.AddText(root, TurnWord, TurnSize, theme.blue, true, TextAlignmentOptions.Center, "Turn", UITheme.TextRole.Display);
            if (theme.cardFontItalic != null) b.turnText.font = theme.cardFontItalic;
            b.turnText.textWrappingMode = TextWrappingModes.NoWrap;
            b.turnRt = b.turnText.rectTransform;
            b.turnRt.anchorMin = b.turnRt.anchorMax = new Vector2(.5f, .5f); b.turnRt.pivot = new Vector2(.5f, .5f);
            b.turnRt.sizeDelta = new Vector2(400f, 72f);
            // its line box reaches into the title's (the glyphs keep clear: Turn sits over the cap height)
            UIUtil.GetOrAdd<LintIgnoreOverlap>(b.turnText.gameObject);

            var font = theme.bannerFont;
            b.ghostRText = TitleText(root, "DecoGhostR", font, GhostR); b.ghostR = b.ghostRText.rectTransform;
            b.ghostCText = TitleText(root, "DecoGhostC", font, GhostC); b.ghostC = b.ghostCText.rectTransform;
            b.title = TitleText(root, "Title", font, Color.white); b.titleRt = b.title.rectTransform;

            for (int i = 0; i < 3; i++)
            {
                var s = HudKit.NewRect("DecoSlice" + i, root);
                s.anchorMin = s.anchorMax = new Vector2(.5f, .5f); s.pivot = new Vector2(.5f, .5f);
                s.sizeDelta = new Vector2(760f + 180f * i, 4f + 3f * i);
                b.slices[i] = s;
                b.sliceImg[i] = HudKit.AddImage(s, new Color(1f, 1f, 1f, 0f));
            }
            b.Evaluate(Total);
            root.gameObject.SetActive(false);
            return b;
        }
    }
}
