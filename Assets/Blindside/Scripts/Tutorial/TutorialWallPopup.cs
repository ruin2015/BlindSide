// The first operation's wall popup: one kind of wall at a time in the middle of the screen, while CRUSHER names it on the radio — its
// name, a large photo of that wall in the game (Resources/Tutorial/WallPhoto_{kind}.png; the drawn wall block of the wall sheet when the
// photo is missing), two large lines on what it does (the point in amber), two ○/× chips (bullets go through / it can be broken), the
// page dots and a 次へ cue (the click itself is the radio's: the page turns with its line).
// One run of the clock per page (ITimedEffect.Evaluate(t), unscaled): the panel in over the confirm dialog's .32, the photo sliding up
// a little after. No per-frame allocation; built at runtime on the radio's canvas.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialWallPopup : MonoBehaviour, ITimedEffect
    {
        public const float W = 760f, H = 640f, Pad = 34f, HeaderH = 92f, PhotoW = 692f, PhotoH = 338f, LinesH = 100f;
        public const float TagSize = 14f, NameSize = 40f, SubSize = 16f, LineSize = 25f, ChipSize = 20f, NextSize = 20f;
        public const float InTime = .32f, PhotoIn = .45f, PhotoDelay = .08f, OutTime = .2f;

        public enum Kind { Soft, Reinforced, Solid }

        static readonly string[] Names = { "ソフト壁", "強化壁", "通常壁" };
        static readonly string[] Subs = { "SOFT WALL", "REINFORCED WALL", "SOLID WALL" };
        static readonly string[] Lines =
        {
            "銃弾が貫通し、壁越しに攻撃できる。\n<color=#FFC81E>能力で破壊することもできる。</color>",
            "銃弾は貫通しない。\nだが、<color=#FFC81E>特定の能力でなら破壊できる。</color>",
            "銃弾は貫通せず、\n<color=#FFC81E>破壊することもできない。</color>",
        };
        // (bullets through, broken) : 0 ×, 1 ○, 2 △ (only with an ability)
        static readonly int[,] Marks = { { 1, 1 }, { 0, 2 }, { 0, 0 } };
        public static readonly Color[] KindColors = { new Color(.36f, 1f, .64f, 1f), new Color(1f, .72f, .18f, 1f), new Color(.86f, .9f, .96f, 1f) };
        public const string PhotoPath = "Tutorial/WallPhoto_";

        public RectTransform panel, photoFrame;
        public CanvasGroup group, photoGroup;
        public TMP_Text title, sub, lines, next;
        public RawImage photo;
        public WallIllustration drawing;
        public Image accent;
        readonly TMP_Text[] chipMarks = new TMP_Text[2];
        readonly Image[] dots = new Image[3];
        float clock, outClock = -1f;
        bool running;

        public float Duration => PhotoDelay + PhotoIn;

        /// <summary>Shows page `page` of `pages` about `kind`.</summary>
        public void Show(Kind kind, int page, int pages)
        {
            int k = (int)kind;
            title.text = Names[k];
            sub.text = Subs[k];
            lines.text = Lines[k];
            accent.color = KindColors[k];
            title.color = KindColors[k];
            var tex = Resources.Load<Texture2D>(PhotoPath + kind);
            photo.texture = tex;
            photo.gameObject.SetActive(tex != null);
            drawing.gameObject.SetActive(tex == null);
            if (tex == null) drawing.SetKind(kind == Kind.Soft ? TutorialWallCard.Kind.Breakable : kind == Kind.Reinforced ? TutorialWallCard.Kind.Reinforced : TutorialWallCard.Kind.Solid);
            for (int c = 0; c < 2; c++)
            {
                int m = Marks[k, c];
                chipMarks[c].text = m == 1 ? "○" : m == 2 ? "△" : "×";
                chipMarks[c].color = m == 1 ? UIPalette.Hex("#5DFFA2") : m == 2 ? UIPalette.Accent : UIPalette.Foe;
            }
            for (int i = 0; i < dots.Length; i++)
            {
                bool on = i < pages;
                dots[i].gameObject.SetActive(on);
                dots[i].color = i == page ? Color.white : new Color(1f, 1f, 1f, .25f);
                dots[i].rectTransform.anchoredPosition = new Vector2((i - (pages - 1) * .5f) * 22f, 0f);
            }
            next.text = page == pages - 1 ? "閉じる" : "次へ";
            gameObject.SetActive(true);
            outClock = -1f; clock = 0f; running = true;
            Evaluate(0f);
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            outClock = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (outClock >= 0f)
            {
                outClock += dt;
                group.alpha = 1f - Mathf.Clamp01(outClock / OutTime);
                if (outClock >= OutTime) { outClock = -1f; running = false; gameObject.SetActive(false); }
                return;
            }
            if (!running) return;
            clock += dt;
            Evaluate(clock);
            if (clock > Duration + .1f) running = false;
        }

        public void Evaluate(float t)
        {
            float k = UIEase.Tactical(Mathf.Clamp01(t / InTime));
            group.alpha = k;
            float s = Mathf.Lerp(.94f, 1f, k);
            panel.localScale = new Vector3(s, s, 1f);
            float p = UIEase.Tactical(Mathf.Clamp01((t - PhotoDelay) / PhotoIn));
            photoGroup.alpha = p;
            photoFrame.anchoredPosition = new Vector2(Pad, -HeaderH - Mathf.Lerp(18f, 0f, p));
        }

        // ================================================================
        //  Construction
        // ================================================================
        public static TutorialWallPopup Build(Transform canvas)
        {
            var root = NewRect("TutorialWallPopup", canvas);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.pivot = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(W, H);
            root.anchoredPosition = new Vector2(0f, 30f);   // (the middle of the screen, a little up: clear of the radio bottom left)
            var c = root.gameObject.AddComponent<TutorialWallPopup>();
            c.panel = root;
            c.group = root.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = false;

            var bg = NewRect("DecoWallPopupPanel", root); Stretch(bg);
            Img(bg, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .96f));
            var line = NewRect("DecoWallPopupLine", root);
            line.anchorMin = new Vector2(0f, 1f); line.anchorMax = new Vector2(1f, 1f); line.pivot = new Vector2(.5f, 1f);
            line.sizeDelta = new Vector2(0f, 4f); line.anchoredPosition = Vector2.zero;
            c.accent = Img(line, UIPalette.Accent);

            var tag = Text(root, "WALL TYPES // FIELD MANUAL", TagSize, UIPalette.Dim, UITheme.TextRole.Display, "Tag");
            tag.characterSpacing = 6f;
            Place(tag.rectTransform, Pad, 18, 420, 18);
            c.title = Text(root, "", NameSize, Color.white, UITheme.TextRole.Title, "Name");
            Place(c.title.rectTransform, Pad, 38, 520, 50);
            c.sub = Text(root, "", SubSize, UIPalette.Dim, UITheme.TextRole.Display, "Sub");
            c.sub.alignment = TextAlignmentOptions.TopRight;
            Place(c.sub.rectTransform, W - Pad - 300f, 56, 300, 22);

            // the photo
            var frame = NewRect("Photo", root); Place(frame, Pad, HeaderH, PhotoW, PhotoH);
            c.photoFrame = frame;
            c.photoGroup = frame.gameObject.AddComponent<CanvasGroup>();
            Img(frame, UIPalette.Ink2);
            var pic = NewRect("Picture", frame); Stretch(pic);
            c.photo = pic.gameObject.AddComponent<RawImage>();
            c.photo.raycastTarget = false;
            var draw = NewRect("Drawing", frame); Stretch(draw);
            c.drawing = draw.gameObject.AddComponent<WallIllustration>();
            c.drawing.raycastTarget = false;

            // the two lines
            c.lines = Text(root, "", LineSize, new Color(.94f, .95f, .97f), UITheme.TextRole.Body, "Lines");
            c.lines.textWrappingMode = TextWrappingModes.Normal;
            c.lines.lineSpacing = 8f;
            Place(c.lines.rectTransform, Pad, HeaderH + PhotoH + 22f, PhotoW - 210f, LinesH);

            // the chips: bullets through / broken
            string[] chipNames = { "銃弾の貫通", "破壊" };
            for (int i = 0; i < 2; i++)
            {
                float y = HeaderH + PhotoH + 22f + i * 46f;
                var chip = NewRect("Chip" + i, root); Place(chip, W - Pad - 196f, y, 196f, 38f);
                Img(chip, UIPalette.Ink2);
                var nm = Text(chip, chipNames[i], ChipSize, new Color(.85f, .88f, .92f), UITheme.TextRole.Body, "Label");
                nm.alignment = TextAlignmentOptions.MidlineLeft;
                Place(nm.rectTransform, 14f, 0f, 130f, 38f);
                var mk = Text(chip, "", ChipSize + 6f, Color.white, UITheme.TextRole.Body, "Mark");
                mk.alignment = TextAlignmentOptions.Center;
                Place(mk.rectTransform, 148f, 0f, 40f, 38f);
                c.chipMarks[i] = mk;
            }

            // the page dots and the cue
            var dotRow = NewRect("Dots", root);
            dotRow.anchorMin = dotRow.anchorMax = new Vector2(.5f, 0f); dotRow.pivot = new Vector2(.5f, .5f);
            dotRow.sizeDelta = new Vector2(100f, 12f); dotRow.anchoredPosition = new Vector2(0f, 34f);
            for (int i = 0; i < 3; i++)
            {
                var d = NewRect("Dot" + i, dotRow);
                d.anchorMin = d.anchorMax = new Vector2(.5f, .5f); d.pivot = new Vector2(.5f, .5f);
                d.sizeDelta = new Vector2(10f, 10f); d.localRotation = Quaternion.Euler(0f, 0f, 45f);
                c.dots[i] = Img(d, Color.white);
            }
            c.next = Text(root, "次へ", NextSize, Color.white, UITheme.TextRole.Body, "Next");
            c.next.alignment = TextAlignmentOptions.MidlineRight;
            c.next.rectTransform.anchorMin = c.next.rectTransform.anchorMax = new Vector2(1f, 0f);
            c.next.rectTransform.pivot = new Vector2(1f, .5f);
            c.next.rectTransform.sizeDelta = new Vector2(200f, 30f);
            c.next.rectTransform.anchoredPosition = new Vector2(-Pad - 34f, 34f);
            var arrow = Text(root, "➜", NextSize, UIPalette.Accent, UITheme.TextRole.Display, "NextArrow");
            arrow.alignment = TextAlignmentOptions.Center;
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(1f, 0f);
            arrow.rectTransform.pivot = new Vector2(1f, .5f);
            arrow.rectTransform.sizeDelta = new Vector2(30f, 30f);
            arrow.rectTransform.anchoredPosition = new Vector2(-Pad, 34f);

            root.gameObject.SetActive(false);
            return c;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

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
