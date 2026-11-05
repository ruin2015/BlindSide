// The deployment briefing's operator card (right side of the HUD): the operator's illustration, codename and katakana · role, and
// their ability (name in the operator's colour, the description under it). Show(op) swaps the contents and plays the entrance again.
// One run of the clock (ITimedEffect.Evaluate(t), unscaled):
//   0 – EnterTime    slides in from the right and fades in; the picture comes up from a white flash
// Hide() fades it out over ExitTime.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.Data;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialOperatorCard : MonoBehaviour, ITimedEffect
    {
        public const float W = 420f, H = 520f, Right = 40f, Up = 30f, PicW = 388f, PicH = 280f, Pad = 16f;
        public const float EnterTime = .38f, FlashTime = .3f, ExitTime = .25f, SlideFrom = 50f;

        public RectTransform box;
        public CanvasGroup group;
        public Image band, picture, flash, abilityBar;
        public AspectRatioFitter pictureFit;
        public TMP_Text codename, subline, abilityName, abilityText;
        float clock, exitClock = -1f;
        bool running;

        public float Duration => EnterTime;

        public void Show(OperatorData op)
        {
            if (op == null) return;
            gameObject.SetActive(true);
            band.color = op.color;
            abilityBar.color = op.color;
            codename.text = op.key;
            subline.text = op.kr + " · " + op.role;
            abilityName.text = op.abilityName;
            abilityName.color = op.color;
            abilityText.text = op.description;
            var art = OperatorArt.Body(op.key);
            picture.sprite = art;
            picture.enabled = art != null;
            if (art != null) pictureFit.aspectRatio = art.rect.width / art.rect.height;
            exitClock = -1f; clock = 0f; running = true;
            Evaluate(0f);
        }

        public void Hide() { if (gameObject.activeSelf && exitClock < 0f) exitClock = 0f; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (exitClock >= 0f)
            {
                exitClock += dt;
                group.alpha = 1f - Mathf.Clamp01(exitClock / ExitTime);
                if (exitClock >= ExitTime) { exitClock = -1f; running = false; gameObject.SetActive(false); }
                return;
            }
            if (!running) return;
            clock += dt;
            Evaluate(clock);
            if (clock >= Mathf.Max(EnterTime, FlashTime)) running = false;
        }

        public void Evaluate(float t)
        {
            float o = UIEase.Tactical(t / EnterTime);
            group.alpha = o;
            box.anchoredPosition = new Vector2(-Right + SlideFrom * (1f - o), Up);
            var c = flash.color; c.a = .85f * (1f - Mathf.Clamp01(t / FlashTime)); flash.color = c;
        }

        // ================================================================
        //  Construction
        // ================================================================
        public static TutorialOperatorCard Build(Transform canvas)
        {
            var root = NewRect("TutorialOperatorCard", canvas);
            root.anchorMin = root.anchorMax = new Vector2(1f, .5f); root.pivot = new Vector2(1f, .5f);
            root.sizeDelta = new Vector2(W, H);
            var c = root.gameObject.AddComponent<TutorialOperatorCard>();
            c.box = root;
            c.group = root.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = false; c.group.interactable = false;

            var bg = NewRect("DecoCardPanel", root); Place(bg, 0, 0, W, H);
            Img(bg, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .95f));
            var band = NewRect("DecoCardBand", root); Place(band, 0, 0, W, 4);
            c.band = Img(band, Color.white);

            var tag = Text(root, "OPERATOR // ATTACK", 12, UIPalette.Blue, UITheme.TextRole.Display, "Tag");
            tag.characterSpacing = 8f;
            Place(tag.rectTransform, Pad, 14, 300, 16);

            // the illustration: covering its frame, pinned to the top (faces stay in)
            var frame = NewRect("PictureFrame", root); Place(frame, Pad, 34, PicW, PicH);
            Img(frame, new Color(.86f, .88f, .91f));
            frame.gameObject.AddComponent<RectMask2D>();
            var pic = NewRect("OperatorArt", frame);
            pic.anchorMin = Vector2.zero; pic.anchorMax = Vector2.one; pic.pivot = new Vector2(.5f, 1f);
            pic.offsetMin = pic.offsetMax = Vector2.zero;
            c.picture = Img(pic, Color.white);
            c.picture.preserveAspect = false;
            c.pictureFit = pic.gameObject.AddComponent<AspectRatioFitter>();
            c.pictureFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            pic.gameObject.AddComponent<LintIgnoreClip>().reason = "covers its masked frame on purpose (object-fit: cover, top)";
            var fl = NewRect("DecoFlash", frame);
            fl.anchorMin = Vector2.zero; fl.anchorMax = Vector2.one; fl.offsetMin = fl.offsetMax = Vector2.zero;
            c.flash = Img(fl, new Color(1f, 1f, 1f, 0f));

            // codename + katakana · role
            float y = 34 + PicH + 10;
            c.codename = Text(root, "", 32, Color.white, UITheme.TextRole.Display, "Codename");
            c.codename.characterSpacing = 6f;
            Place(c.codename.rectTransform, Pad, y, W - Pad * 2, 38);
            c.subline = Text(root, "", 14, new Color(.66f, .7f, .75f), UITheme.TextRole.Body, "Subline");
            Place(c.subline.rectTransform, Pad, y + 38, W - Pad * 2, 20);

            // the ability box
            float ay = y + 66;
            var ab = NewRect("AbilityBox", root); Place(ab, Pad, ay, W - Pad * 2, H - ay - Pad);
            Img(ab, new Color(1f, 1f, 1f, .06f));
            var bar = NewRect("DecoAbilityBar", ab);
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(0f, 1f); bar.pivot = new Vector2(0f, .5f);
            bar.sizeDelta = new Vector2(4f, 0f); bar.anchoredPosition = Vector2.zero;
            c.abilityBar = Img(bar, Color.white);
            var alabel = Text(ab, "ABILITY", 11, new Color(.66f, .7f, .75f), UITheme.TextRole.Display, "AbilityTag");
            alabel.characterSpacing = 8f;
            Place(alabel.rectTransform, 14, 8, 200, 14);
            c.abilityName = Text(ab, "", 18, Color.white, UITheme.TextRole.Title, "AbilityName");
            Place(c.abilityName.rectTransform, 14, 22, W - Pad * 2 - 28, 26);
            c.abilityText = Text(ab, "", 14, Color.white, UITheme.TextRole.Body, "AbilityText");
            c.abilityText.textWrappingMode = TextWrappingModes.Normal;
            c.abilityText.lineSpacing = 6f;
            Place(c.abilityText.rectTransform, 14, 52, W - Pad * 2 - 28, H - ay - Pad - 58);

            root.gameObject.SetActive(false);
            return c;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        static Image Img(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static TMP_Text Text(RectTransform parent, string s, float size, Color c, UITheme.TextRole role, string name)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.color = c; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UIFonts.Apply(t, role);
            return t;
        }
    }
}
