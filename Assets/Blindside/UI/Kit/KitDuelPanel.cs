// The engagement's information (the user's design) — replacing UICombatPanel (the #combat cut-in) while shots are exchanged, on the map
// and on the battle stage alike.
//   Left    the player's side (or, AI against AI, the shooter): bottom left       Right   the other side: bottom right, mirrored
//   each card (Card) is the design's flat card seen in perspective (CardPerspective: its corners where the design has them — the side
//   toward the screen's middle smaller, the whole card tilted up), in the side's colour (an ally: the operator's own; an enemy: red):
//     DecoPlate    one flat black plate at 68% (the far top corner cut)       DecoBar   a colour bar down the outer edge
//     Tag          "OPERATOR // ALLY" / "HOSTILE // ENEMY" (white, bold italic) over a thin colour rule across the card
//     Weapon, Gun  the weapon's short name top right, its silhouette (GunIcons, white) under it
//     Name         English name, big, white, bold italic
//     Hp           "HP 120/180" in one line (bold italic, the number big) and the slanted segment bar (DuelHpBar) to its right
//     Stats        three white cells with a colour strip: ATK "50×2" · HIT "100%" · CRT "20%" ("—" until that side fires)
//   Middle (top centre)  the volley's tag (ENGAGE · 交戦 / PREEMPTIVE · 先制攻撃 ×1.4, walls) and, after it, the result line
// Every position below is the design's, measured on the flat card as fractions of its width (x) and height (y, from the top); the card's
// width is wide (Aspect) so that Noto Sans JP comes out as narrow as the design's lettering once the far side is drawn in. Shapes have
// soft edges (DuelShape) and texts are SDF, so nothing shows steps. Texts shrink to fit their boxes; nothing leaves the card.
// On the battle stage (Enter, GameplayUIBridge: only once the stage is up) the cards come in with its fade from black: each unfolds
// from its outer edge while sliding in, fades in, and its HP bar fills from empty (.6s); they stay for the whole stage and go at once
// when it closes under black (HideNow). On the map (Show) they slide in from their sides and fade (.35s). One unscaled clock,
// ITimedEffect.Evaluate; HP bars ease on their own clocks.
// Texts and colours change only in the setters; nothing is allocated per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Duel Panel")]
    public class KitDuelPanel : MonoBehaviour, ITimedEffect
    {
        /// <summary>The flat card: CardH tall, Aspect × CardH wide (only its left ~56% shows once drawn in, see Corner*).</summary>
        public const float CardH = 262f, Aspect = 2.3f, CardW = CardH * Aspect, Margin = 40f, Bottom = 100f;   // (Bottom: clear of END TURN)
        // where the flat card's corners show (the design's), x and y in card heights from the top-left corner
        static readonly Vector2 CornerTR = new Vector2(1.5151f, -.0592f), CornerBR = new Vector2(1.5063f, .7947f);
        public const float InTime = .35f, InShift = 60f, OutTime = .2f;
        /// <summary>The battle stage's entrance (Enter): the cards start as the stage fades in from black — each unfolds from its
        /// outer edge (width EnterFrom → 1 over EnterUnfold) while sliding in EnterShift, fades in over EnterFade, and its HP bar
        /// fills from empty (EnterBarFrom, over EnterBarTime).</summary>
        public const float EnterTime = .6f, EnterUnfold = .45f, EnterFrom = .3f, EnterShift = 140f, EnterFade = .22f, EnterBarFrom = .12f, EnterBarTime = .32f;
        public const string Dash = "—";
        const string HpFormat = "<size=47%>HP</size><space=0.3em>{0:0}<space=0.2em><size=44%>/{1:0}</size>";
        static readonly Color Plate = new Color(0f, 0f, 0f, 173f / 255f);
        static readonly Color CellLabel = new Color32(0x1e, 0x1e, 0x1e, 0xff), CellValue = Color.black;

        public class Card
        {
            public RectTransform root;
            public Graphic edgeBar, rule;
            public Graphic[] strips = new Graphic[3];
            public Image gun;
            public TMP_Text tag, name, weapon, hp, atk, hit, crit;
            public DuelHpBar bar;
            public int hpShown = -1, max = 1;
        }

        public Card left, right;
        public RectTransform middle;
        public TMP_Text tagText, resultText;
        public CanvasGroup group;

        float clock, outAt = -1f;
        bool running, shown, staged;
        float barL = 1f, barR = 1f;     // (the HP each bar fills to in the entrance)

        public bool Visible => shown;
        public float Duration => outAt >= 0f ? outAt + OutTime : staged ? EnterTime : InTime;

        // ---- API ----------------------------------------------------------------------------------------------------------------
        /// <summary>One side's card: who (name, the side's colour, ally or not), HP, the weapon, and its numbers for this volley
        /// (null: "—").</summary>
        public void SetSide(bool isLeft, string nameEn, Color color, bool ally, int hp, int maxHp, string weaponLabel, Sprite gunSprite,
                            string atk, string hit, string crit)
        {
            var c = isLeft ? left : right;
            c.name.text = nameEn ?? "";
            c.tag.text = ally ? "OPERATOR // ALLY" : "HOSTILE // ENEMY";
            c.edgeBar.color = color;
            c.rule.color = color;
            for (int i = 0; i < c.strips.Length; i++) c.strips[i].color = color;
            c.weapon.text = weaponLabel ?? "";
            c.gun.sprite = gunSprite; c.gun.enabled = gunSprite != null;
            c.bar.SetColors(color, Color.Lerp(color, Color.white, .55f), Color.white);
            c.max = Mathf.Max(1, maxHp);
            c.hpShown = -1;
            SetHpText(c, hp);
            c.bar.SetImmediate((float)Mathf.Clamp(hp, 0, c.max) / c.max);
            SetStats(isLeft, atk, hit, crit);
        }

        /// <summary>A side's numbers for its volley (null keeps "—").</summary>
        public void SetStats(bool isLeft, string atk, string hit, string crit)
        {
            var c = isLeft ? left : right;
            c.atk.text = string.IsNullOrEmpty(atk) ? Dash : atk;
            c.hit.text = string.IsNullOrEmpty(hit) ? Dash : hit;
            c.crit.text = string.IsNullOrEmpty(crit) ? Dash : crit;
        }

        /// <summary>A side's HP as it is now (a drop leaves the lost segments in the ghost colour for a moment).</summary>
        public void SetHp(bool isLeft, int hp)
        {
            var c = isLeft ? left : right;
            hp = Mathf.Clamp(hp, 0, c.max);
            if (hp == c.hpShown) return;
            bool drop = hp < c.hpShown;
            SetHpText(c, hp);
            if (isLeft) barL = (float)hp / c.max; else barR = (float)hp / c.max;
            if (drop && Application.isPlaying) c.bar.SetTarget((float)hp / c.max); else c.bar.SetImmediate((float)hp / c.max);
            if (drop && Application.isPlaying && !UIMotionSettings.reduceMotion) { if (isLeft) shakeL = 0f; else shakeR = 0f; }   // the side hit jolts
        }

        // ---- the side taking damage jolts (ShakeTime, ShakeAmp px, dying out); the battle stage's entrance flickers in ----
        public const float ShakeTime = .32f, ShakeAmp = 11f, FlickerTime = .2f;
        float shakeL = -1f, shakeR = -1f, shiftNow;

        /// <summary>The card's jolt `s` seconds into it (−1: none): a quick, dying shake (no random numbers).</summary>
        static Vector2 Shake(float s)
        {
            if (s < 0f || s >= ShakeTime) return Vector2.zero;
            float p = s / ShakeTime, d = (1f - p) * (1f - p) * ShakeAmp;
            return new Vector2(Mathf.Sin(p * 58f) * d, Mathf.Cos(p * 43f) * d * .45f);
        }

        /// <summary>The entrance's flicker `t` seconds in: a signal catching — on, a dropout, on, a weaker dropout, then steady.</summary>
        static float Flicker(float t)
        {
            if (t >= FlickerTime) return 1f;
            if (t < .045f) return .85f;
            if (t < .075f) return .1f;
            if (t < .115f) return 1f;
            if (t < .14f) return .35f;
            if (t < .165f) return 1f;
            if (t < .18f) return .6f;
            return 1f;
        }

        /// <summary>Render Previews: one side's card `s` seconds into its jolt.</summary>
        public void PoseShake(bool isLeft, float s)
        {
            if (isLeft) shakeL = s; else shakeR = s;
            PlaceCards();
        }

        void PlaceCards()
        {
            left.root.anchoredPosition = new Vector2(Margin - shiftNow, Bottom) + Shake(shakeL);
            right.root.anchoredPosition = new Vector2(-Margin + shiftNow, Bottom) + Shake(shakeR);
        }

        static void SetHpText(Card c, int hp)
        {
            c.hpShown = Mathf.Clamp(hp, 0, c.max);
            c.hp.SetText(HpFormat, (float)c.hpShown, (float)c.max);   // ("HP", the number and the max on one baseline)
        }

        /// <summary>The volley's tag (top centre).</summary>
        public void SetTag(string tag) { tagText.text = tag ?? ""; resultText.text = ""; }

        /// <summary>The volley's outcome under the tag.</summary>
        public void SetResult(string result) { resultText.text = result ?? ""; }

        /// <summary>Slides the cards in (if not up already) — an engagement on the map.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            if (shown && outAt < 0f) return;
            shown = true; staged = false; outAt = -1f; clock = 0f;
            running = Application.isPlaying;
            Evaluate(running ? 0f : InTime);
        }

        /// <summary>The battle stage's entrance (if not up already): called as the stage starts fading in from black.</summary>
        public void Enter()
        {
            gameObject.SetActive(true);
            if (shown && outAt < 0f) return;
            shown = true; staged = true; outAt = -1f; clock = 0f;
            barL = Ratio(left); barR = Ratio(right);
            running = Application.isPlaying;
            Evaluate(running ? 0f : EnterTime);
        }

        static float Ratio(Card c) => c.hpShown < 0 ? 1f : (float)c.hpShown / c.max;

        /// <summary>Gone at once (the screen is black: the battle stage closing).</summary>
        public void HideNow()
        {
            shown = false; running = false; outAt = -1f;
            shakeL = shakeR = -1f;
            if (staged) { left.bar.ShowRatio(barL); right.bar.ShowRatio(barR); }
            staged = false;
            left.root.localScale = right.root.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        /// <summary>Fades the cards out.</summary>
        public void Hide()
        {
            if (!shown) { gameObject.SetActive(false); return; }
            shown = false;
            if (!Application.isPlaying) { gameObject.SetActive(false); return; }
            outAt = Mathf.Max(clock, InTime);
            clock = outAt; running = true;
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public void Evaluate(float t)
        {
            bool reduce = UIMotionSettings.reduceMotion;
            float o = outAt >= 0f ? Mathf.Clamp01((t - outAt) / OutTime) : 0f;
            float k, a, shift, width = 1f;
            if (staged)
            {
                k = reduce ? 1f : UIEase.Tactical(Mathf.Clamp01(t / EnterUnfold));
                a = (reduce ? 1f : Mathf.Clamp01(t / EnterFade) * Flicker(t)) * (1f - o);   // (flickering in, like a signal catching)
                shift = EnterShift * (1f - k);
                width = Mathf.LerpUnclamped(EnterFrom, 1f, k);
                if (t <= EnterBarFrom + EnterBarTime + .05f)
                {   // the bars fill from empty (nothing lands on the stage this early)
                    float b = reduce ? 1f : UIEase.Tactical(Mathf.Clamp01((t - EnterBarFrom) / EnterBarTime));
                    left.bar.ShowRatio(barL * b); right.bar.ShowRatio(barR * b);
                }
            }
            else
            {
                k = reduce ? 1f : UIEase.Tactical(Mathf.Clamp01(t / InTime));
                a = k * (1f - o);
                shift = InShift * (1f - k);
            }
            if (group.alpha != a) group.alpha = a;
            var sc = new Vector3(width, 1f, 1f);      // (unfolding from the outer edge: the cards' pivots are their outer corners)
            if (left.root.localScale != sc) left.root.localScale = right.root.localScale = sc;
            shiftNow = shift;
            PlaceCards();
            middle.anchoredPosition = new Vector2(0f, -96f + 20f * (1f - k));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool shaking = shakeL >= 0f || shakeR >= 0f;
            if (shakeL >= 0f) { shakeL += dt; if (shakeL >= ShakeTime) shakeL = -1f; }
            if (shakeR >= 0f) { shakeR += dt; if (shakeR >= ShakeTime) shakeR = -1f; }
            if (!running) { if (shaking) PlaceCards(); return; }
            clock += dt;
            Evaluate(clock);
            if (outAt >= 0f && clock >= outAt + OutTime) { running = false; gameObject.SetActive(false); }
            else if (outAt < 0f && clock >= (staged ? EnterTime : InTime)) running = false;
        }

        // ---- build ------------------------------------------------------------------------------------------------------------
        static TMP_Text Txt(Transform p, string name, string s, float size, Color c, TextAlignmentOptions al, TMP_FontAsset font, FontStyles style)
        {
            var t = HudKit.AddText(p, s, size, c, true, al, name, UITheme.TextRole.Display);
            if (font != null) t.font = font;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.richText = true;
            return t;
        }

        /// <summary>Shrinks `t` (down to `min`) when its text would not fit its box. TMP re-fits only when the text changes.</summary>
        static void Fit(TMP_Text t, float min)
        {
            t.fontSizeMax = t.fontSize; t.fontSizeMin = min;
            t.enableAutoSizing = true;
        }

        /// <summary>A text line: its baseline at `baseline` (from the card's top), its box `w` wide from `x` (both from the left card's
        /// left edge, mirrored on the right card), its alignment toward the card's outer edge unless centred.</summary>
        static TMP_Text Line(Transform card, bool isLeft, string name, string s, float x, float baseline, float w, float size, Color c,
                             FontStyles style, TMP_FontAsset font, bool centred = false, bool toFar = false)
        {
            bool alignRight = !centred && (isLeft ? toFar : !toFar);
            var al = centred ? TextAlignmentOptions.Baseline : alignRight ? TextAlignmentOptions.BaselineRight : TextAlignmentOptions.BaselineLeft;
            var t = Txt(card, name, s, size, c, al, font, style);
            float h = size * 1.8f;        // (Baseline: the baseline is the box's middle)
            UILayout.Place(t.rectTransform, Mx(isLeft, x, w), baseline - h * .5f, w, h);
            return t;
        }

        static float Mx(bool isLeft, float x, float w) => isLeft ? x : CardW - x - w;

        static Vector2[] Pts(bool isLeft, params Vector2[] p)
        {
            if (!isLeft) for (int i = 0; i < p.Length; i++) p[i].x = 1f - p[i].x;
            return p;
        }

        static DuelShape Shape(Transform card, bool isLeft, string name, float x, float y, float w, float h, Color c, params Vector2[] pts)
        {
            return DuelShape.Make(card, name, Mx(isLeft, x, w), y, w, h, c, Pts(isLeft, pts));
        }

        static Card BuildCard(Transform parent, bool isLeft)
        {
            const float W = CardW, H = CardH;
            var theme = UITheme.Current;
            var font = theme.duelFont != null ? theme.duelFont : theme.cardFontBold;
            const FontStyles Italic = FontStyles.Italic, Upright = FontStyles.Normal;   // (Noto Sans JP Black: heavy enough as it is)
            var c = new Card();
            var rt = HudKit.NewRect(isLeft ? "Left" : "Right", parent);
            var anchor = new Vector2(isLeft ? 0f : 1f, 0f);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = new Vector2(W, H);
            c.root = rt;

            // the view: the design's corners (x in card heights → fractions of the width)
            var persp = rt.gameObject.AddComponent<CardPerspective>();
            persp.topLeft = Vector2.zero; persp.bottomLeft = new Vector2(0f, 1f);
            persp.topRight = new Vector2(CornerTR.x / Aspect, CornerTR.y);
            persp.bottomRight = new Vector2(CornerBR.x / Aspect, CornerBR.y);
            persp.mirror = !isLeft;

            // the plate (its far top corner cut) and the colour bar down its outer edge
            Shape(rt, isLeft, "DecoPlate", 0f, 0f, W, H, Plate,
                  new Vector2(0f, 0f), new Vector2(.9468f, 0f), new Vector2(1f, .1188f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            c.edgeBar = Shape(rt, isLeft, "DecoBar", 0f, 0f, .0128f * W, H, theme.blue);

            // the tag and the thin colour rule under it
            c.tag = Line(rt, isLeft, "Tag", "", .053f * W, .116f * H, .55f * W, 24.7f, Color.white, Italic, font);
            Fit(c.tag, 16f);
            c.rule = Shape(rt, isLeft, "DecoRule", .050f * W, .170f * H, (.928f - .050f) * W, 1.8f, theme.blue);

            // the weapon: its name top right, its silhouette under it
            c.weapon = Line(rt, isLeft, "Weapon", "", .60f * W, .2865f * H, (.934f - .60f) * W, 26.3f, Color.white, Upright, font, toFar: true);
            Fit(c.weapon, 16f);
            var gr = HudKit.NewRect("Gun", rt);
            float gw = (.822f - .555f) * W, gh = (.465f - .285f) * H;
            UILayout.Place(gr, Mx(isLeft, .555f * W, gw), .285f * H, gw, gh);
            c.gun = gr.gameObject.AddComponent<Image>();
            c.gun.preserveAspect = true; c.gun.raycastTarget = false; c.gun.color = Color.white; c.gun.enabled = false;
            if (!isLeft)
            {   // the gun points to the screen's middle on both cards: flipped about its own centre
                gr.pivot = new Vector2(.5f, .5f);
                gr.anchoredPosition += new Vector2(gw * .5f, -gh * .5f);
                gr.localScale = new Vector3(-1f, 1f, 1f);
            }

            // the name, left of the gun
            c.name = Line(rt, isLeft, "Name", "", .050f * W, .371f * H, (.535f - .050f) * W, 63.6f, Color.white, Italic, font);
            Fit(c.name, 34f);

            // HP: "HP 120/180" in one line, then the segment bar
            c.hp = Line(rt, isLeft, "Hp", "", .056f * W, .595f * H, (.395f - .056f) * W, 56.9f, Color.white, Italic, font);
            Fit(c.hp, 34f);
            c.hp.gameObject.AddComponent<LintIgnoreOverlap>().reason = "line boxes only: the name's caps end 16 units above the HP's (the design's spacing)";
            float bx = .4058f * W, bw = (.9503f - .4058f) * W;
            var barRt = HudKit.NewRect("HpBar", rt);
            UILayout.Place(barRt, Mx(isLeft, bx, bw), .501f * H, bw, (.581f - .501f) * H);
            c.bar = barRt.gameObject.AddComponent<DuelHpBar>();
            c.bar.raycastTarget = false;
            c.bar.segments = 19;
            c.bar.gap = .0045f * W;
            c.bar.slant = .088f * W * (.581f - .501f);      // (the design's segments lean as their sides do: du/dv ≈ −.088)
            c.bar.fromRight = !isLeft;

            // the numbers: three white cells, a colour strip on their outer side
            float[] cx = { .0555f, .3610f, .6673f };
            const float CellW = .2878f, StripW = .0103f, CellTop = .670f, CellBottom = .920f;
            string[] labels = { "ATK", "HIT", "CRT" };
            for (int i = 0; i < 3; i++)
            {
                // (ATK, HIT, CRT read left to right on both cards)
                float x = cx[isLeft ? i : 2 - i] * W, w = CellW * W, y = CellTop * H, h = (CellBottom - CellTop) * H, sw = StripW * W;
                Shape(rt, isLeft, "DecoCell" + i, x, y, w, h, Color.white);
                c.strips[i] = Shape(rt, isLeft, "DecoStrip" + i, x, y, sw, h, theme.blue);
                Line(rt, isLeft, "Label" + i, labels[i], x + sw + .018f * W, .7345f * H, w - sw - .036f * W, 18.3f, CellLabel, Upright, font);
                var val = Line(rt, isLeft, "Value" + i, Dash, x + sw + .01f * W, .875f * H, w - sw - .02f * W, 41f, CellValue, Upright, font, centred: true);
                Fit(val, 24f);
                val.gameObject.AddComponent<LintIgnoreOverlap>().reason = "line boxes only: the label's baseline is 6 units above the value's caps (the design's spacing)";
                if (i == 0) c.atk = val; else if (i == 1) c.hit = val; else c.crit = val;
            }
            CardPerspective.Apply(rt);   // every part of the card drawn in with it
            return c;
        }

        /// <summary>The panel on `canvas` (the HUD canvas), hidden until Show.</summary>
        public static KitDuelPanel Build(Transform canvas)
        {
            var root = HudKit.NewRect("DuelPanel", canvas);
            HudKit.Stretch(root);
            var p = root.gameObject.AddComponent<KitDuelPanel>();
            p.group = root.gameObject.AddComponent<CanvasGroup>();
            p.group.blocksRaycasts = false; p.group.interactable = false;
            p.left = BuildCard(root, true);
            p.right = BuildCard(root, false);

            var mid = HudKit.NewRect("Middle", root);
            mid.anchorMin = mid.anchorMax = new Vector2(.5f, 1f); mid.pivot = new Vector2(.5f, 1f);
            mid.sizeDelta = new Vector2(560f, 64f);
            p.middle = mid;
            DuelShape.Make(mid, "DecoBg", 0f, 0f, 560f, 34f, Plate);
            p.tagText = MidText(mid, "Tag", Color.white);
            UILayout.Place(p.tagText.rectTransform, 12f, 0f, 536f, 34f);
            p.resultText = MidText(mid, "Result", UIPalette.Accent);
            UILayout.Place(p.resultText.rectTransform, 12f, 38f, 536f, 24f);

            p.Evaluate(InTime);
            root.gameObject.SetActive(false);
            return p;
        }

        static TMP_Text MidText(Transform p, string name, Color c)
        {
            var t = HudKit.AddText(p, "", 15f, c, true, TextAlignmentOptions.Center, name, UITheme.TextRole.Body);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }
    }
}
