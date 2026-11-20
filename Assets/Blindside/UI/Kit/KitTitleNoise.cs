// The title's first show (MainMenuScreen, once a run): the screen comes up as a signal being tuned in — black and snow at first, then
// the picture breaking through in steps (the black flickering back now and then), torn by bright horizontal strips and a rolling band,
// the snow thinning out, two last short bursts, clear. All layers are decoration (Deco…), none takes a click.
//   DecoNoiseBlack   the title background #0a0c0f over everything, stepping away .45–1.1s (with flicker-backs)
//   DecoNoiseSnow    TV snow: one 480×270 grey noise texture made once (System.Random, fixed seed — never UnityEngine.Random), its
//                    uvRect jumped to another spot every 1/24s; .85 to .55s, thinning with spikes to 1.15s, then the two bursts
//   DecoNoiseRoll    a faint bright band rolling down the screen (to 1.15s)
//   DecoNoiseTear0-3 horizontal tears (white / glitchBlue / glitchRed): each frame step a new height and sideways shift, on or off
// Every value is a function of t (frame steps of 1/24s hashed): ITimedEffect.Evaluate(t) poses it; the host (MainMenuScreen) drives the
// clock on unscaled time. Reduced motion: no snow or tears, the black only fades. Nothing is allocated per frame.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Title Noise")]
    public class KitTitleNoise : MonoBehaviour, ITimedEffect
    {
        public const float RunTime = 1.5f, Fps = 24f;
        public const float BlackFrom = .45f, BlackTo = 1.1f, SnowFull = .55f, SnowThin = 1.15f, SnowFullAlpha = .85f, SnowEndAlpha = .15f;
        public const float RollH = 90f, RollSpeed = 1.7f;
        static readonly float[] BurstFrom = { 1.22f, 1.38f }, BurstTo = { 1.28f, 1.42f };
        static readonly Color Bg = new Color32(0x0a, 0x0c, 0x0f, 0xff);
        const int TexW = 480, TexH = 270;

        static Texture2D snowTex;
        Image black, roll;
        RawImage snow;
        readonly Image[] tears = new Image[4];
        readonly Color[] tearCol = new Color[4];
        RectTransform rollRt;
        readonly RectTransform[] tearRt = new RectTransform[4];

        public float Duration => RunTime;

        /// <summary>The overlay, full screen, under `parent` (hidden until Evaluate poses it inside its run).</summary>
        public static KitTitleNoise Build(RectTransform parent)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("DecoTitleNoise", parent);
            HudKit.Stretch(root);
            var n = root.gameObject.AddComponent<KitTitleNoise>();

            var b = HudKit.NewRect("DecoNoiseBlack", root);
            HudKit.Stretch(b);
            n.black = HudKit.AddImage(b, Bg);

            var s = HudKit.NewRect("DecoNoiseSnow", root);
            HudKit.Stretch(s);
            n.snow = s.gameObject.AddComponent<RawImage>();
            n.snow.raycastTarget = false;
            n.snow.texture = SnowTexture();
            n.snow.color = new Color(.86f, .9f, 1f, 0f);

            n.rollRt = HudKit.NewRect("DecoNoiseRoll", root);
            n.rollRt.anchorMin = new Vector2(0f, 1f); n.rollRt.anchorMax = new Vector2(1f, 1f); n.rollRt.pivot = new Vector2(.5f, 1f);
            n.rollRt.sizeDelta = new Vector2(0f, RollH);
            n.roll = HudKit.AddImage(n.rollRt, new Color(1f, 1f, 1f, 0f));

            n.tearCol[0] = Color.white; n.tearCol[1] = theme.glitchBlue; n.tearCol[2] = theme.glitchRed; n.tearCol[3] = Color.white;
            for (int i = 0; i < n.tears.Length; i++)
            {
                var t = HudKit.NewRect("DecoNoiseTear" + i, root);
                t.anchorMin = new Vector2(0f, 1f); t.anchorMax = new Vector2(1f, 1f); t.pivot = new Vector2(.5f, 1f);
                n.tearRt[i] = t;
                n.tears[i] = HudKit.AddImage(t, new Color(1f, 1f, 1f, 0f));
            }
            n.Evaluate(RunTime);
            return n;
        }

        /// <summary>The snow: grey noise, a little brighter in scattered streaks (made once, kept for the run).</summary>
        static Texture SnowTexture()
        {
            if (snowTex != null) return snowTex;
            snowTex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave, name = "TitleSnow" };
            var px = new Color32[TexW * TexH];
            var rng = new System.Random(4127);
            for (int y = 0; y < TexH; y++)
            {
                float row = .75f + .25f * (float)rng.NextDouble();          // (rows a shade apart: scan lines in the snow)
                for (int x = 0; x < TexW; x++)
                {
                    byte v = (byte)(255f * row * (float)rng.NextDouble());
                    px[y * TexW + x] = new Color32(v, v, v, 255);
                }
            }
            snowTex.SetPixels32(px);
            snowTex.Apply(false, false);
            return snowTex;
        }

        // a value in [0,1) for frame step `step` and channel `k` (the same every time t is posed)
        static float Hash(int step, int k)
        {
            unchecked
            {
                uint h = (uint)step * 73856093u ^ (uint)(k + 1) * 19349663u;
                h ^= h >> 13; h *= 2654435761u; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static bool InBurst(float t)
        {
            for (int i = 0; i < BurstFrom.Length; i++) if (t >= BurstFrom[i] && t < BurstTo[i]) return true;
            return false;
        }

        public void Evaluate(float t)
        {
            bool live = t >= 0f && t < RunTime;
            if (gameObject.activeSelf != live) gameObject.SetActive(live);
            if (!live) return;
            bool reduce = UIMotionSettings.reduceMotion;
            int step = (int)(t * Fps);
            bool burst = InBurst(t);

            // the black: on, then stepping away (12 steps), flickering back now and then; a breath of it in the bursts
            float a;
            if (reduce) a = 1f - Mathf.Clamp01((t - BlackFrom) / (BlackTo - BlackFrom));
            else if (t < BlackFrom) a = 1f;
            else if (t < BlackTo)
            {
                float p = Mathf.Floor((t - BlackFrom) / (BlackTo - BlackFrom) * 12f) / 12f;
                a = 1f - p;
                if (Hash(step, 1) < .22f) a = Mathf.Min(1f, a + .5f);
            }
            else a = burst ? .3f : 0f;
            SetAlpha(black, a);

            // the snow
            float s = 0f;
            if (!reduce)
            {
                if (t < SnowFull) s = SnowFullAlpha + .1f * Hash(step, 2);
                else if (t < SnowThin)
                {
                    s = Mathf.Lerp(SnowFullAlpha, SnowEndAlpha, (t - SnowFull) / (SnowThin - SnowFull));
                    if (Hash(step, 3) < .25f) s = Mathf.Min(1f, s + .4f);
                }
                else if (burst) s = .5f;
            }
            SetAlpha(snow, s);
            if (s > 0f)
            {
                var uv = new Rect(Hash(step, 4), Hash(step, 5), 1f, 1f);
                if (snow.uvRect != uv) snow.uvRect = uv;
            }

            // the rolling band
            float parentH = ((RectTransform)transform).rect.height;
            if (parentH <= 0f) parentH = 810f;
            bool rolling = !reduce && t < SnowThin;
            SetAlpha(roll, rolling ? .1f : 0f);
            if (rolling)
            {
                float y = -((t * RollSpeed) % 1f) * (parentH + RollH) + RollH;
                var pos = new Vector2(0f, y);
                if (rollRt.anchoredPosition != pos) rollRt.anchoredPosition = pos;
            }

            // the tears
            bool tearing = !reduce && (s > .2f || burst);
            for (int i = 0; i < tears.Length; i++)
            {
                bool on = tearing && Hash(step, 10 + i) < .55f;
                SetAlpha(tears[i], on ? .55f : 0f, tearCol[i]);
                if (!on) continue;
                float h = 3f + 22f * Hash(step, 20 + i);
                float y = -(parentH - h) * Hash(step, 30 + i);
                float x = (Hash(step, 40 + i) - .5f) * 80f;
                var size = new Vector2(0f, Mathf.Round(h));
                if (tearRt[i].sizeDelta != size) tearRt[i].sizeDelta = size;
                var pos = new Vector2(Mathf.Round(x), Mathf.Round(y));
                if (tearRt[i].anchoredPosition != pos) tearRt[i].anchoredPosition = pos;
            }
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            if (c.a == a) return;
            c.a = a; g.color = c;
        }

        static void SetAlpha(Graphic g, float a, Color rgb)
        {
            var c = new Color(rgb.r, rgb.g, rgb.b, a);
            if (g.color != c) g.color = c;
        }
    }
}
