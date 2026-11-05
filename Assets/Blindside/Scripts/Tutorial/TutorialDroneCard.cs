// The tutorial's drone card: a blueprint-style top-down drawing of the recon drone (painted once into a texture: grid, body, arms,
// rotor rings, front camera, dimension marks) with four propellers spinning over it, the name and three spec chips. Right side of the
// HUD, beside the radio. One run of the clock (ITimedEffect.Evaluate(t), unscaled):
//   0 – EnterTime     slides in from the right and fades in
//   ScanFrom – ScanTo a light band sweeps down the drawing (the picture "printing")
//   always            the propellers turn (no allocation: rotations only)
// Hide() fades it out over ExitTime.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialDroneCard : MonoBehaviour, ITimedEffect
    {
        public const float W = 460f, H = 392f, Right = 40f, Up = 60f, PicW = 424f, PicH = 254f;
        public const float EnterTime = .35f, ExitTime = .25f, ScanFrom = .25f, ScanTo = 1.05f, SlideFrom = 46f;
        public const int TexW = 636, TexH = 381;   // 1.5× the picture
        static readonly Color Line = UIPalette.Hex("#2FE4FF");

        public RectTransform box, scan;
        public CanvasGroup group;
        public RectTransform[] props;
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
            for (int i = 0; i < props.Length; i++)
                props[i].localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * t * 900f + i * 37f);
        }

        // ================================================================
        //  Construction
        // ================================================================
        static Texture2D schematic;
        static Sprite propSprite;

        // rotor centres in the picture's own units (centre origin, y up), shared by the painting and the propeller images
        static readonly Vector2[] Rotors = { new Vector2(-112f, 62f), new Vector2(112f, 62f), new Vector2(-112f, -62f), new Vector2(112f, -62f) };
        const float RotorR = 50f;

        public static TutorialDroneCard Build(Transform canvas)
        {
            var root = NewRect("TutorialDroneCard", canvas);
            root.anchorMin = root.anchorMax = new Vector2(1f, .5f); root.pivot = new Vector2(1f, .5f);
            root.sizeDelta = new Vector2(W, H);
            var c = root.gameObject.AddComponent<TutorialDroneCard>();
            c.box = root;
            c.group = root.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = false; c.group.interactable = false;

            var bg = NewRect("DecoCardPanel", root); Place(bg, 0, 0, W, H);
            Img(bg, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .94f));
            var top = NewRect("DecoCardTopLine", root); Place(top, 0, 0, W, 3);
            Img(top, Line);

            var tag = Text(root, "GADGET // DRONE", 12, Line, UITheme.TextRole.Display, "Tag");
            tag.characterSpacing = 8f;
            Place(tag.rectTransform, 18, 12, 300, 16);
            var title = Text(root, "偵察ドローン", 26, Color.white, UITheme.TextRole.Title, "Title");
            Place(title.rectTransform, 18, 28, 300, 36);

            // the drawing
            var pic = NewRect("DronePicture", root); Place(pic, (W - PicW) / 2f, 72, PicW, PicH);
            var raw = pic.gameObject.AddComponent<RawImage>();
            raw.texture = Schematic(); raw.raycastTarget = false;
            pic.gameObject.AddComponent<RectMask2D>();
            float k = PicH / 254f;   // picture units → card units (the drawing is 254 units high)
            c.props = new RectTransform[Rotors.Length];
            for (int i = 0; i < Rotors.Length; i++)
            {
                var p = NewRect("Propeller" + i, pic);
                p.anchorMin = p.anchorMax = new Vector2(.5f, .5f); p.pivot = new Vector2(.5f, .5f);
                p.anchoredPosition = Rotors[i] * k;
                p.sizeDelta = new Vector2(RotorR * 2f * k, RotorR * 2f * k) * .92f;
                var pi = p.gameObject.AddComponent<Image>();
                pi.sprite = PropSprite(); pi.raycastTarget = false; pi.color = new Color(1f, 1f, 1f, .85f);
                c.props[i] = p;
            }
            var scan = NewRect("DecoScanBand", pic);
            scan.anchorMin = new Vector2(0f, 1f); scan.anchorMax = new Vector2(1f, 1f); scan.pivot = new Vector2(.5f, .5f);
            scan.sizeDelta = new Vector2(0f, 10f);
            Img(scan, new Color(Line.r, Line.g, Line.b, .35f));
            c.scan = scan;

            // spec chips
            string[] specs = { "移動 8m", "索敵 半径2.5m", "壁の穴を通過" };
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
                Img(bar, Line);
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
            for (int y = 0; y < TexH; y++)
                for (int xI = 0; xI < TexW; xI++)
                {
                    // picture units, centre origin, y up
                    float u = (xI + .5f - TexW * .5f) / sc, v = (y + .5f - TexH * .5f) / sc;
                    var col = bgA;
                    // grid
                    float gx = Mathf.Abs(Mathf.Repeat(u + 6f, 24f) - 12f), gy = Mathf.Abs(Mathf.Repeat(v + 6f, 24f) - 12f);
                    float grid = Stroke(Mathf.Min(gx, gy), .45f) * .12f;
                    col = Over(col, Line, grid);
                    // arms (body centre → each rotor hub)
                    float arm = 1e9f;
                    foreach (var r in Rotors) arm = Mathf.Min(arm, SegDist(new Vector2(u, v), Vector2.zero, r * .82f));
                    col = Over(col, new Color(.16f, .3f, .38f, 1f), Cover(arm - 7f));
                    col = Over(col, Line, Stroke(arm - 7f, 1.2f) * .9f);
                    // rotor rings + hubs + guard ticks
                    foreach (var r in Rotors)
                    {
                        float d = Vector2.Distance(new Vector2(u, v), r);
                        col = Over(col, Line, Stroke(d - RotorR, 1.4f));
                        col = Over(col, Line, Stroke(d - RotorR + 7f, .6f) * .45f);
                        col = Over(col, Color.white, Cover(d - 7f));
                    }
                    // body: rounded rectangle 96×72, radius 16
                    float body = RoundRect(new Vector2(u, v), new Vector2(48f, 36f), 16f);
                    col = Over(col, new Color(.12f, .22f, .29f, 1f), Cover(body));
                    col = Over(col, Line, Stroke(body, 1.6f));
                    col = Over(col, Line, Stroke(RoundRect(new Vector2(u, v), new Vector2(30f, 18f), 8f), .7f) * .6f);
                    // front camera (top edge of the body)
                    float cam = Vector2.Distance(new Vector2(u, v), new Vector2(0f, 42f));
                    col = Over(col, Color.white, Stroke(cam - 11f, 1.2f));
                    col = Over(col, new Color(1f, .78f, .12f, 1f), Cover(cam - 5f));
                    // dimension mark under the drone
                    float dimY = -121f;
                    col = Over(col, Line, Stroke(SegDist(new Vector2(u, v), new Vector2(-162f, dimY), new Vector2(162f, dimY)), .6f) * .7f);
                    col = Over(col, Line, Stroke(SegDist(new Vector2(u, v), new Vector2(-162f, dimY - 6f), new Vector2(-162f, dimY + 6f)), .6f) * .7f);
                    col = Over(col, Line, Stroke(SegDist(new Vector2(u, v), new Vector2(162f, dimY - 6f), new Vector2(162f, dimY + 6f)), .6f) * .7f);
                    px[y * TexW + xI] = col;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            schematic = tex;
            return tex;
        }

        static Sprite PropSprite()
        {
            if (propSprite != null) return propSprite;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + .5f) / N * 2f - 1f, v = (y + .5f) / N * 2f - 1f;
                    // two blades: thin ellipses along x, pinched at the hub, plus a faint motion disc
                    float blade = Mathf.Max(Ellipse(u, v, .92f, .11f), 0f);
                    float disc = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sqrt(u * u + v * v) - .62f) * 6f) * .12f;
                    float a = Mathf.Clamp01(Mathf.Max(blade, disc));
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            propSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), 100f);
            propSprite.hideFlags = HideFlags.HideAndDontSave;
            return propSprite;
        }

        static float Ellipse(float u, float v, float a, float b)
        {
            // soft-edged: 1 inside, 0 outside over ~2 px
            float k = (u * u) / (a * a) + (v * v) / (b * b * (.35f + .65f * Mathf.Abs(u)));
            return Mathf.Clamp01((1f - k) * 6f);
        }

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
