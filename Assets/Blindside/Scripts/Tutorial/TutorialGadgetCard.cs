// The tutorial's gadget card for FLARE (the player): a blueprint-style side view of her breaching launcher (painted once into a
// texture: grid, barrel and muzzle, revolving drum with its chambers, receiver, sight, grip, fore grip, stock, a charge drawn beside it
// and dimension marks), the name and three spec chips. Right side of the screen, like the drone card; shown while CRUSHER tells her
// what her ability does (LBreach5 / LBreach6). One run of the clock (ITimedEffect.Evaluate(t), unscaled):
//   0 – EnterTime     slides in from the right and fades in
//   ScanFrom – ScanTo a light band sweeps down the drawing (the picture "printing")
//   always            the muzzle glow pulses (colour only, no allocation)
// Hide() fades it out over ExitTime.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialGadgetCard : MonoBehaviour, ITimedEffect
    {
        public const float W = 460f, H = 392f, Right = 40f, Up = 60f, PicW = 424f, PicH = 254f;
        public const float EnterTime = .35f, ExitTime = .25f, ScanFrom = .25f, ScanTo = 1.05f, SlideFrom = 46f, GlowPeriod = 1.4f;
        public const int TexW = 636, TexH = 381;   // 1.5× the picture
        static readonly Color Line = UIPalette.Hex("#2FE4FF");
        static readonly Color Flare = UIPalette.Hex("#F8594F");   // FLARE's colour (Operator_FLARE.color)
        static readonly Vector2 Muzzle = new Vector2(196f, 34f);  // picture units (centre origin, y up)

        public RectTransform box, scan;
        public CanvasGroup group;
        public Image glow;
        float clock, exitClock = -1f;
        bool running;

        public float Duration => ScanTo;

        public void Play()
        {
            gameObject.SetActive(true);
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
        }

        public void Evaluate(float t)
        {
            float o = UIEase.Tactical(t / EnterTime);
            group.alpha = o;
            box.anchoredPosition = new Vector2(-Right + SlideFrom * (1f - o), Up);
            float s = Mathf.Clamp01((t - ScanFrom) / (ScanTo - ScanFrom));
            scan.gameObject.SetActive(s > 0f && s < 1f);
            scan.anchoredPosition = new Vector2(0f, -PicH * s);
            float g = .35f + .45f * (.5f + .5f * Mathf.Sin(t * Mathf.PI * 2f / GlowPeriod));
            glow.color = new Color(Flare.r, Flare.g, Flare.b, g * Mathf.Clamp01((t - ScanTo) * 4f));
        }

        // ================================================================
        //  Construction
        // ================================================================
        static Texture2D schematic;
        static Sprite glowSprite;

        /// <summary>`abilityName`: the gadget's name (FLARE's OperatorData.abilityName); `charges`: its uses.</summary>
        public static TutorialGadgetCard Build(Transform canvas, string abilityName, int charges)
        {
            var root = NewRect("TutorialGadgetCard", canvas);
            root.anchorMin = root.anchorMax = new Vector2(1f, .5f); root.pivot = new Vector2(1f, .5f);
            root.sizeDelta = new Vector2(W, H);
            var c = root.gameObject.AddComponent<TutorialGadgetCard>();
            c.box = root;
            c.group = root.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = false; c.group.interactable = false;

            var bg = NewRect("DecoCardPanel", root); Place(bg, 0, 0, W, H);
            Img(bg, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .94f));
            var top = NewRect("DecoCardTopLine", root); Place(top, 0, 0, W, 3);
            Img(top, Flare);

            var tag = Text(root, "GADGET // FLARE", 12, Flare, UITheme.TextRole.Display, "Tag");
            tag.characterSpacing = 8f;
            Place(tag.rectTransform, 18, 12, 300, 16);
            var title = Text(root, string.IsNullOrEmpty(abilityName) ? "ブリーチングランチャー" : abilityName, 26, Color.white, UITheme.TextRole.Title, "Title");
            Place(title.rectTransform, 18, 28, W - 36, 36);

            // the drawing
            var pic = NewRect("LauncherPicture", root); Place(pic, (W - PicW) / 2f, 72, PicW, PicH);
            var raw = pic.gameObject.AddComponent<RawImage>();
            raw.texture = Schematic(); raw.raycastTarget = false;
            pic.gameObject.AddComponent<RectMask2D>();
            float k = PicH / 254f;   // picture units → card units
            var g = NewRect("DecoMuzzleGlow", pic);
            g.anchorMin = g.anchorMax = new Vector2(.5f, .5f); g.pivot = new Vector2(.5f, .5f);
            g.anchoredPosition = Muzzle * k; g.sizeDelta = new Vector2(64f, 64f) * k;
            c.glow = g.gameObject.AddComponent<Image>();
            c.glow.sprite = GlowSprite(); c.glow.raycastTarget = false; c.glow.color = Color.clear;
            var scan = NewRect("DecoScanBand", pic);
            scan.anchorMin = new Vector2(0f, 1f); scan.anchorMax = new Vector2(1f, 1f); scan.pivot = new Vector2(.5f, .5f);
            scan.sizeDelta = new Vector2(0f, 10f);
            Img(scan, new Color(Line.r, Line.g, Line.b, .35f));
            c.scan = scan;

            // spec chips (LauncherAbility: 6m with a clear line; a hole first, the second charge opens the wall)
            string[] specs = { "射程 6m", "強化壁も破壊", "使用回数 " + Mathf.Max(1, charges) };
            float x = 18f;
            foreach (var sTxt in specs)
            {
                var chip = NewRect("SpecChip", root);
                var t = Text(chip, sTxt, 14, Color.white, UITheme.TextRole.Body, "Label");
                float w = Mathf.Ceil(t.GetPreferredValues(sTxt).x) + 22f;
                Place(chip, x, H - 46, w, 28);
                Img(chip, new Color(1f, 1f, 1f, .08f));
                t.alignment = TextAlignmentOptions.Midline;
                var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(12f, 0f); trt.offsetMax = new Vector2(-6f, 0f);
                var bar = NewRect("DecoChipBar", chip);
                bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(0f, 1f); bar.pivot = new Vector2(0f, .5f);
                bar.sizeDelta = new Vector2(3f, 0f); bar.anchoredPosition = Vector2.zero;
                Img(bar, Flare);
                x += w + 8f;
            }

            root.gameObject.SetActive(false);
            return c;
        }

        // ---- the painting (once per session) ----
        static Texture2D Schematic()
        {
            if (schematic != null) return schematic;
            var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[TexW * TexH];
            float sc = TexH / 254f;   // picture units → texture pixels
            var bgA = new Color(.06f, .11f, .16f, 1f);
            var fill = new Color(.12f, .22f, .29f, 1f);
            var fillDark = new Color(.09f, .16f, .22f, 1f);
            for (int y = 0; y < TexH; y++)
                for (int xI = 0; xI < TexW; xI++)
                {
                    var p = new Vector2((xI + .5f - TexW * .5f) / sc, (y + .5f - TexH * .5f) / sc);   // picture units, centre origin, y up
                    var col = bgA;
                    // grid
                    float gx = Mathf.Abs(Mathf.Repeat(p.x + 6f, 24f) - 12f), gy = Mathf.Abs(Mathf.Repeat(p.y + 6f, 24f) - 12f);
                    col = Over(col, Line, Stroke(Mathf.Min(gx, gy), .45f) * .12f);

                    // stock (behind): a tube back from the receiver and a butt plate
                    float stock = Mathf.Min(Capsule(p, new Vector2(-92f, 34f), new Vector2(-170f, 22f), 11f), Capsule(p, new Vector2(-174f, 52f), new Vector2(-174f, -12f), 9f));
                    col = Part(col, stock, fillDark);
                    // grip and fore grip
                    col = Part(col, Capsule(p, new Vector2(-46f, 6f), new Vector2(-66f, -62f), 12f), fillDark);
                    col = Part(col, Capsule(p, new Vector2(70f, 14f), new Vector2(70f, -34f), 10f), fillDark);
                    // trigger guard: the lower half of a ring
                    float guard = Vector2.Distance(p, new Vector2(-18f, -4f)) - 18f;
                    if (p.y < -4f) col = Over(col, Line, Stroke(guard, 1.4f));
                    // barrel and muzzle
                    col = Part(col, RoundRect(p - new Vector2(80f, 34f), new Vector2(108f, 12f), 5f), fill);
                    col = Over(col, Line, Stroke(SegDist(p, new Vector2(-10f, 34f), new Vector2(180f, 34f)), .5f) * .45f);   // bore line
                    col = Part(col, RoundRect(p - new Vector2(188f, 34f), new Vector2(8f, 17f), 3f), fill);
                    // receiver
                    col = Part(col, RoundRect(p - new Vector2(-34f, 30f), new Vector2(60f, 22f), 8f), fill);
                    // revolving drum with six chambers
                    var drum = new Vector2(-6f, 14f);
                    float dd = Vector2.Distance(p, drum);
                    col = Part(col, dd - 40f, fill);
                    for (int ch = 0; ch < 6; ch++)
                    {
                        float a = ch * Mathf.PI / 3f + .3f;
                        var cc = drum + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 24f;
                        col = Over(col, Line, Stroke(Vector2.Distance(p, cc) - 8f, .8f) * .8f);
                    }
                    col = Over(col, Color.white, Cover(dd - 5f));
                    // sight on top
                    col = Part(col, RoundRect(p - new Vector2(-30f, 60f), new Vector2(22f, 8f), 3f), fill);
                    col = Over(col, new Color(1f, .78f, .12f, 1f), Cover(Vector2.Distance(p, new Vector2(-12f, 60f)) - 3.5f));

                    // a charge, drawn apart (lower right) with its tip in FLARE's colour
                    var shellA = new Vector2(112f, -72f); var shellB = new Vector2(168f, -72f);
                    col = Part(col, Capsule(p, shellA, shellB, 12f), fill);
                    col = Over(col, Flare, Cover(Vector2.Distance(p, shellB) - 9f));
                    col = Over(col, Line, Stroke(SegDist(p, new Vector2(128f, -84f), new Vector2(128f, -60f)), .6f) * .7f);
                    // dimension mark under the launcher
                    float dimY = -112f;
                    col = Over(col, Line, Stroke(SegDist(p, new Vector2(-184f, dimY), new Vector2(198f, dimY)), .6f) * .7f);
                    col = Over(col, Line, Stroke(SegDist(p, new Vector2(-184f, dimY - 6f), new Vector2(-184f, dimY + 6f)), .6f) * .7f);
                    col = Over(col, Line, Stroke(SegDist(p, new Vector2(198f, dimY - 6f), new Vector2(198f, dimY + 6f)), .6f) * .7f);
                    px[y * TexW + xI] = col;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            schematic = tex;
            return tex;
        }

        static Sprite GlowSprite()
        {
            if (glowSprite != null) return glowSprite;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + .5f) / N * 2f - 1f, v = (y + .5f) / N * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    px[y * N + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            glowSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), 100f);
            glowSprite.hideFlags = HideFlags.HideAndDontSave;
            return glowSprite;
        }

        // a filled part with its outline
        static Color Part(Color col, float sd, Color fill) => Over(Over(col, fill, Cover(sd)), Line, Stroke(sd, 1.3f));
        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r) => SegDist(p, a, b) - r;
        static float Cover(float sd) => Mathf.Clamp01(.5f - sd * 1.6f);                    // filled shape (sd < 0 inside)
        static float Stroke(float sd, float halfW) => Mathf.Clamp01(.5f - (Mathf.Abs(sd) - halfW) * 1.6f);
        static Color Over(Color under, Color over, float a) => Color.Lerp(under, new Color(over.r, over.g, over.b, 1f), Mathf.Clamp01(a));

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static float RoundRect(Vector2 p, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(r, r);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
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
