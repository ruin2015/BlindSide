// The tutorial's wall sheet: in the middle of the screen, one column per kind of wall — a drawing of the wall block (WallIllustration:
// its look in the training hall, and a round either going through it or stopping on it), its name, and what it does in three lines
// (○ yes / × no, a yellow ○ for "only with …"). Shown while CRUSHER explains the walls (stage 2: shot through or not; stage 4: the
// three kinds). One run of the clock (ITimedEffect.Evaluate(t), unscaled): the columns come in one after another. No per-frame
// allocation; built at runtime on the radio's canvas.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialWallCard : MonoBehaviour, ITimedEffect
    {
        // (enlarged by request so the lines read easily: 3 columns = 1252 wide, clear of the objective card on the right)
        public const float ColW = 380f, ColGap = 24f, Pad = 32f, HeaderH = 74f, ArtH = 200f, NameH = 46f, RowH = 38f, Rows = 3;
        public const float TagSize = 13f, TitleSize = 24f, NameSize = 26f, SubSize = 14f, SubW = 130f, MarkSize = 22f, LineSize = 18f, MarkW = 30f;
        public const float ColIn = .38f, Stagger = .12f, OutTime = .2f;

        public enum Kind { Breakable, Solid, Reinforced }

        // what each kind does (○ / × / yellow ○ = conditional) — the lines CRUSHER's lesson is about
        static readonly string[] Names = { "破壊できる壁", "破壊できない壁", "強化壁" };
        static readonly string[] Subs = { "BREAKABLE", "SOLID", "REINFORCED" };
        static readonly (int mark, string text)[][] Lines =
        {
            new[] { (1, "銃弾が貫通する（命中率は下がる）"), (1, "能力や散弾銃で破壊できる"), (0, "向こう側は見えない") },
            new[] { (0, "銃弾は貫通しない"), (0, "どの能力でも破壊できない"), (0, "向こう側は見えない") },
            new[] { (0, "銃弾は貫通しない"), (2, "強化壁用の能力でのみ破壊できる"), (2, "電気が流れると爆薬を設置できない") },
        };

        public RectTransform card;
        public CanvasGroup group;
        public TMP_Text title;
        RectTransform[] cols = new RectTransform[3];
        CanvasGroup[] colGroups = new CanvasGroup[3];
        WallIllustration[] arts = new WallIllustration[3];
        TMP_Text[] names = new TMP_Text[3], subs = new TMP_Text[3];
        TMP_Text[,] rowText = new TMP_Text[3, 3], rowMark = new TMP_Text[3, 3];
        int count;
        float clock, outClock = -1f;
        bool running;

        public float Duration => ColIn + Stagger * 2f;

        /// <summary>Shows the sheet with these kinds, left to right (`heading`: the line over it).</summary>
        public void Show(string heading, params Kind[] kinds)
        {
            count = Mathf.Min(kinds.Length, 3);
            title.text = heading;
            float w = Pad * 2f + count * ColW + (count - 1) * ColGap;
            card.sizeDelta = new Vector2(w, card.sizeDelta.y);
            for (int i = 0; i < 3; i++)
            {
                bool on = i < count;
                cols[i].gameObject.SetActive(on);
                if (!on) continue;
                int k = (int)kinds[i];
                cols[i].anchoredPosition = new Vector2(Pad + i * (ColW + ColGap), -HeaderH);
                arts[i].SetKind(kinds[i]);
                names[i].text = Names[k];
                subs[i].text = Subs[k];
                for (int r = 0; r < 3; r++)
                {
                    var (mark, text) = Lines[k][r];
                    rowText[i, r].text = text;
                    rowMark[i, r].text = mark == 0 ? "×" : "○";
                    rowMark[i, r].color = mark == 1 ? UIPalette.Hex("#5DFFA2") : mark == 2 ? UIPalette.Accent : UIPalette.Foe;
                }
            }
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
            group.alpha = UIEase.Tactical(Mathf.Clamp01(t / .25f));
            for (int i = 0; i < count; i++)
            {
                float k = UIEase.Tactical(Mathf.Clamp01((t - i * Stagger) / ColIn));
                colGroups[i].alpha = k;
                float s = Mathf.Lerp(.92f, 1f, k);
                cols[i].localScale = new Vector3(s, s, 1f);
            }
        }

        // ================================================================
        //  Construction
        // ================================================================
        public static TutorialWallCard Build(Transform canvas)
        {
            float h = HeaderH + ArtH + NameH + 8f + Rows * RowH + Pad;
            var root = NewRect("TutorialWallCard", canvas);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.pivot = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(Pad * 2f + 3f * ColW + 2f * ColGap, h);
            root.anchoredPosition = new Vector2(0f, 70f);   // (above the radio)
            var c = root.gameObject.AddComponent<TutorialWallCard>();
            c.card = root;
            c.group = root.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = false;

            var bg = NewRect("DecoWallCardPanel", root); Stretch(bg);
            Img(bg, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .95f));
            var line = NewRect("DecoWallCardLine", root);
            line.anchorMin = new Vector2(0f, 1f); line.anchorMax = new Vector2(1f, 1f); line.pivot = new Vector2(.5f, 1f);
            line.sizeDelta = new Vector2(0f, 4f); line.anchoredPosition = Vector2.zero;
            Img(line, UIPalette.Accent);
            var tag = Text(root, "WALL TYPES // FIELD MANUAL", TagSize, UIPalette.Accent, UITheme.TextRole.Display, "Tag");
            tag.characterSpacing = 6f;
            Place(tag.rectTransform, Pad, 14, 460, 18);
            c.title = Text(root, "", TitleSize, Color.white, UITheme.TextRole.Title, "Title");
            Place(c.title.rectTransform, Pad, 34, 900, 32);

            for (int i = 0; i < 3; i++)
            {
                var col = NewRect("Column" + i, root); Place(col, Pad, HeaderH, ColW, h - HeaderH - Pad);
                c.cols[i] = col;
                c.colGroups[i] = col.gameObject.AddComponent<CanvasGroup>();
                var art = NewRect("WallArt", col); Place(art, 0, 0, ColW, ArtH);
                Img(art, UIPalette.Ink2);
                var draw = NewRect("Drawing", art); Stretch(draw);
                c.arts[i] = draw.gameObject.AddComponent<WallIllustration>();
                c.arts[i].raycastTarget = false;
                c.names[i] = Text(col, "", NameSize, Color.white, UITheme.TextRole.Title, "Name");
                Place(c.names[i].rectTransform, 0, ArtH + 8f, ColW - SubW, NameH - 8f);
                c.subs[i] = Text(col, "", SubSize, UIPalette.Dim, UITheme.TextRole.Display, "Sub");
                c.subs[i].alignment = TextAlignmentOptions.TopRight;
                Place(c.subs[i].rectTransform, ColW - SubW, ArtH + 18f, SubW, 20f);
                var rule = NewRect("DecoRule", col); Place(rule, 0, ArtH + NameH + 2f, ColW, 1f);
                Img(rule, new Color(1f, 1f, 1f, .14f));
                for (int r = 0; r < 3; r++)
                {
                    float y = ArtH + NameH + 8f + r * RowH;
                    var mark = Text(col, "", MarkSize, UIPalette.Foe, UITheme.TextRole.Body, "Mark" + r);
                    mark.alignment = TextAlignmentOptions.Center;
                    Place(mark.rectTransform, 0, y, MarkW, RowH);
                    var txt = Text(col, "", LineSize, new Color(.92f, .94f, .96f), UITheme.TextRole.Body, "Line" + r);
                    txt.alignment = TextAlignmentOptions.MidlineLeft;
                    Place(txt.rectTransform, MarkW + 6f, y, ColW - MarkW - 6f, RowH);
                    c.rowMark[i, r] = mark; c.rowText[i, r] = txt;
                }
            }
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

    /// <summary>
    /// A drawing of one wall block for the wall sheet, built from flat polygons (a front face, the top, the side): the ordinary wall
    /// white with the hall's blue glow lines, the solid one dark charcoal with rivets, the reinforced one dark steel with amber hazard
    /// bands. A round comes in from the left: through the ordinary wall (a hole, the round going on behind), stopped with a burst of
    /// sparks on the other two. Rebuilt only when the kind or the size changes.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class WallIllustration : MaskableGraphic
    {
        TutorialWallCard.Kind kind;

        public void SetKind(TutorialWallCard.Kind k) { kind = k; SetVerticesDirty(); }

        static readonly Color Glow = new Color(.18f, .72f, 1f, 1f), Tracer = new Color(1f, .88f, .54f, 1f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float w = r.width, h = r.height;
            Vector2 c = r.center;
            System.Func<float, float, Vector2> P = (x, y) => c + new Vector2(x * w, y * h);

            // the block: front face, top and side (a little to the right and up: depth)
            float fx0 = -.2f, fx1 = .12f, fy0 = -.34f, fy1 = .22f, dx = .1f, dy = .14f;
            Color front, top, sideC, edge;
            switch (kind)
            {
                case TutorialWallCard.Kind.Solid:
                    front = new Color(.23f, .25f, .28f); top = new Color(.33f, .36f, .4f); sideC = new Color(.16f, .18f, .2f); edge = new Color(.42f, .45f, .5f); break;
                case TutorialWallCard.Kind.Reinforced:
                    front = new Color(.27f, .25f, .22f); top = new Color(.38f, .35f, .29f); sideC = new Color(.19f, .17f, .15f); edge = UIPalette.Accent; break;
                default:
                    front = new Color(.91f, .93f, .95f); top = Color.white; sideC = new Color(.76f, .8f, .85f); edge = Glow; break;
            }
            // the shadow on the floor
            Quad(vh, P(fx0 - .02f, fy0 - .05f), P(fx1 + dx + .04f, fy0 - .05f), P(fx1 + dx + .04f, fy0 + dy * .3f), P(fx0 - .02f, fy0 + dy * .3f), new Color(0f, 0f, 0f, .35f));
            Quad(vh, P(fx0, fy1), P(fx1, fy1), P(fx1 + dx, fy1 + dy), P(fx0 + dx, fy1 + dy), top);
            Quad(vh, P(fx1, fy0), P(fx1 + dx, fy0 + dy), P(fx1 + dx, fy1 + dy), P(fx1, fy1), sideC);
            Quad(vh, P(fx0, fy0), P(fx0, fy1), P(fx1, fy1), P(fx1, fy0), front);

            float t = .008f;   // edge line thickness (of the height)
            switch (kind)
            {
                case TutorialWallCard.Kind.Breakable:
                    // the hall's glowing lines: round the face and along the middle
                    Frame(vh, P, fx0, fy0, fx1, fy1, t * 1.6f, edge);
                    Rect(vh, P, fx0, (fy0 + fy1) * .5f - t, fx1, (fy0 + fy1) * .5f + t, new Color(edge.r, edge.g, edge.b, .8f));
                    break;
                case TutorialWallCard.Kind.Solid:
                    Frame(vh, P, fx0, fy0, fx1, fy1, t, edge);
                    for (int i = 0; i < 2; i++)
                        for (int j = 0; j < 3; j++)
                        {
                            float rx = fx0 + .035f + i * (fx1 - fx0 - .07f), ry = fy0 + .06f + j * (fy1 - fy0 - .12f) * .5f;
                            Rect(vh, P, rx - .008f, ry - .012f, rx + .008f, ry + .012f, edge);
                        }
                    break;
                default:
                    // hazard bands top and bottom (amber, black slashes), steel plate seams
                    float bh = .07f;
                    Rect(vh, P, fx0, fy1 - bh, fx1, fy1, UIPalette.Accent);
                    Rect(vh, P, fx0, fy0, fx1, fy0 + bh, UIPalette.Accent);
                    for (float sx = fx0 + .01f; sx < fx1 - .03f; sx += .05f)
                    {
                        Quad(vh, P(sx, fy1 - bh), P(sx + .02f, fy1), P(sx + .035f, fy1), P(sx + .015f, fy1 - bh), UIPalette.Ink);
                        Quad(vh, P(sx, fy0), P(sx + .02f, fy0 + bh), P(sx + .035f, fy0 + bh), P(sx + .015f, fy0), UIPalette.Ink);
                    }
                    Rect(vh, P, (fx0 + fx1) * .5f - .004f, fy0 + bh, (fx0 + fx1) * .5f + .004f, fy1 - bh, new Color(.12f, .11f, .1f));
                    Frame(vh, P, fx0, fy0, fx1, fy1, t, edge);
                    break;
            }

            // the round, from the left, at chest height
            float ty = -.06f, th = .012f;
            if (kind == TutorialWallCard.Kind.Breakable)
            {
                Rect(vh, P, -.48f, ty - th, fx0, ty + th, Tracer);
                // the hole, and the round going on beyond the wall (fainter)
                Rect(vh, P, fx0 + .01f, ty - .03f, fx0 + .04f, ty + .03f, new Color(.2f, .22f, .25f));
                Rect(vh, P, fx1 + dx, ty + dy * .5f - th, .48f, ty + dy * .5f + th, new Color(Tracer.r, Tracer.g, Tracer.b, .55f));
                Arrow(vh, P, .48f, ty + dy * .5f, new Color(Tracer.r, Tracer.g, Tracer.b, .55f));
            }
            else
            {
                Rect(vh, P, -.48f, ty - th, fx0 - .01f, ty + th, Tracer);
                // stopped: a burst of sparks on the face
                var hit = P(fx0 - .005f, ty);
                for (int i = 0; i < 7; i++)
                {
                    float a = (100f + i * 26f) * Mathf.Deg2Rad, len = (i % 2 == 0 ? .1f : .065f) * h;
                    Ray(vh, hit, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), len, .006f * h, Tracer);
                }
            }
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector2.zero); vh.AddVert(b, col, Vector2.zero); vh.AddVert(c, col, Vector2.zero); vh.AddVert(d, col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        static void Rect(VertexHelper vh, System.Func<float, float, Vector2> P, float x0, float y0, float x1, float y1, Color col)
            => Quad(vh, P(x0, y0), P(x0, y1), P(x1, y1), P(x1, y0), col);

        static void Frame(VertexHelper vh, System.Func<float, float, Vector2> P, float x0, float y0, float x1, float y1, float t, Color col)
        {
            Rect(vh, P, x0, y0, x1, y0 + t, col); Rect(vh, P, x0, y1 - t, x1, y1, col);
            Rect(vh, P, x0, y0, x0 + t * .6f, y1, col); Rect(vh, P, x1 - t * .6f, y0, x1, y1, col);
        }

        static void Ray(VertexHelper vh, Vector2 from, Vector2 dir, float len, float half, Color col)
        {
            var n = new Vector2(-dir.y, dir.x) * half;
            var to = from + dir * len;
            Quad(vh, from - n, from + n, to + n * .3f, to - n * .3f, col);
        }

        static void Arrow(VertexHelper vh, System.Func<float, float, Vector2> P, float x, float y, Color col)
        {
            Quad(vh, P(x - .03f, y - .035f), P(x - .03f, y + .035f), P(x + .01f, y), P(x + .01f, y), col);
        }
    }
}
