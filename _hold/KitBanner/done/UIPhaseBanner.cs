// The phase banner (#bn, ui_spec "배너"), re-skinned in Phase 4-4 with the same call API (SetContent / Play / PlayRoutine / Evaluate /
// Duration). 1.9s in all, every look a pose of one clock (ITimedEffect.Evaluate(t)); Play only advances that clock with
// Time.unscaledDeltaTime (so it moves during a hit stop):
//   band  (bnIn)   0–14% the ink .95 band is revealed from the left (its corners travel, KitBannerStrip), 14–82% hold, 82–100% it folds
//                  away to the right edge; the white dashed lines inside it are clipped to what is visible (RectMask2D)
//   text  (bnTx)   hidden to 6%, 6–22% in from 140 to the left while the letter spacing closes from .5em to .18em, hold to 82%, then
//                  120 out to the right while fading. With UIMotionSettings.reduceMotion only the fades remain (no move, no spacing).
//   lines          two dashed lines (22 on / 12 off, drawn as an opaque gray: the .55 white over the band) flowing one cell a second
// Texts: small = the `sub` argument (12 display, dim), title 60 display (.18em), `kr` 16 body in the phase colour. No allocation per
// frame (rects, spacing and the uv rect are set only when they change).
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    public class UIPhaseBanner : MonoBehaviour, ITimedEffect
    {
        public const float Cell = 34f, Shift = 140f, ShiftOut = 120f, SpacingFrom = 50f, SpacingTo = 18f;

        public RectTransform strip;              // the full-size content root
        public CanvasGroup group;
        public KitBannerStrip shape;             // DecoShape
        public RectTransform maskBox, maskInner; // the dashed lines' clip (RectMask2D) and the full-width carrier inside it
        public RawImage stripeTop, stripeBottom;
        public RectTransform tx;                 // the text block (moves)
        public CanvasGroup txGroup;
        public TMP_Text mainText, subText, krText;
        public Image[] colorBlocks;              // the blue parallelograms left / right of the text
        public float duration = 1.9f;

        float clock; bool running;
        float lastL = -1f, lastR = -1f, lastSpacing = -999f, lastW = -1f;

        void Awake() { group.alpha = 0f; }

        public float Duration => duration;

        /// <summary>The banner's texts and colour, without playing it.</summary>
        public void SetContent(string main, string kr, string sub, Color color)
        {
            Fit(mainText, main, 60f, 1.05f); Fit(krText, kr, 16f, 1.25f); Fit(subText, sub, 12f, 1.25f);
            krText.color = color;
            foreach (var b in colorBlocks) b.color = color;
            lastSpacing = -999f;
        }

        static void Fit(TMP_Text t, string text, float size, float line)
        {
            t.text = text;
            UIFonts.Apply(t, UIFonts.RoleFor(text, size, false));
            UILayout.LineHeight(t, line);
        }

        public void Play(string main, string kr, string sub, Color color)
        {
            SetContent(main, kr, sub, color);
            clock = 0f; running = true;
            Evaluate(0f);
        }
        public IEnumerator PlayRoutine(string main, string kr, string sub, Color color) { Play(main, kr, sub, color); yield return new WaitForSecondsRealtime(duration); }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= duration) running = false;
        }

        /// <summary>The banner `t` seconds into a play (t ≥ duration: gone).</summary>
        public void Evaluate(float t)
        {
            float p = Mathf.Clamp01(t / duration);
            float a = t <= 0f || t >= duration ? 0f : 1f;
            if (group.alpha != a) group.alpha = a;

            // ---- band: bnIn --------------------------------------------------------------------------------------------------
            float bX = 1f, bY = 0f, cX = 1f, cY = .84f, aX = 0f, dX = 0f, dY = 1f, cYv = cY;
            if (p < .14f) { float q = UIEase.Tactical(p / .14f); bX = q; bY = Mathf.Lerp(.16f, 0f, q); cX = q; cYv = .84f; }
            else if (p > .82f)
            {
                float q = UIEase.Tactical((p - .82f) / .18f);
                aX = q; dX = q; cYv = Mathf.Lerp(.84f, 1f, q); dY = 1f;
                aX = q; dX = q;
            }
            float ay = p > .82f ? Mathf.Lerp(.16f, 0f, UIEase.Tactical((p - .82f) / .18f)) : .16f;
            float dyv = 1f;
            shape.Set(aX, ay, bX, bY, cX, cYv, dX, dyv);

            // the dashed lines show only inside the visible left..right
            float W = strip.rect.width;
            float L = aX, R = Mathf.Max(bX, cX);
            if (L != lastL || R != lastR || W != lastW)
            {
                lastL = L; lastR = R; lastW = W;
                maskBox.offsetMin = new Vector2(L * W, maskBox.offsetMin.y); maskBox.offsetMax = new Vector2(-(1f - R) * W, maskBox.offsetMax.y);
                maskInner.offsetMin = new Vector2(-L * W, maskInner.offsetMin.y); maskInner.offsetMax = new Vector2((1f - R) * W, maskInner.offsetMax.y);
            }
            // lines flow one cell a second
            var uv = new Rect(-Mathf.Repeat(t, 1f), 0f, W / Cell, 1f);
            if (stripeTop.uvRect != uv) { stripeTop.uvRect = uv; stripeBottom.uvRect = uv; }

            // ---- text: bnTx --------------------------------------------------------------------------------------------------
            float x, al, sp = SpacingTo;
            if (p < .06f) { x = -Shift; al = 0f; sp = SpacingFrom; }
            else if (p < .22f) { float q = UIEase.Tactical((p - .06f) / .16f); x = Mathf.Lerp(-Shift, 0f, q); al = q; sp = Mathf.Lerp(SpacingFrom, SpacingTo, q); }
            else if (p < .82f) { x = 0f; al = 1f; }
            else { float q = (p - .82f) / .18f; x = ShiftOut * UIEase.Tactical(q); al = 1f - q; }
            if (UIMotionSettings.reduceMotion) { x = 0f; sp = SpacingTo; }
            var pos = new Vector2(x, 0f);
            if (tx.anchoredPosition != pos) tx.anchoredPosition = pos;
            if (txGroup.alpha != al) txGroup.alpha = al;
            if (sp != lastSpacing) { lastSpacing = sp; mainText.characterSpacing = sp; krText.characterSpacing = sp; }
        }
    }
}
