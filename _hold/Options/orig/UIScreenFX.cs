// The full-screen effect layer (ui_spec.md "화면 전체 효과"): scanlines, a slow blue light band, noise flicker and a rare glitch burst,
// drawn by the "Blindside/UI/ScreenFX" shader as ONE quad on its own top canvas — one draw call, the shared material never changes
// (every parameter is in the vertices, see the shader header).
//   scanlines     3 canvas units apart (rounded to whole screen pixels with the canvas scale), white alpha .03
//   light band    blue, alpha .07, 140 canvas units tall, top → bottom every 8s (mockup #title .scanband: top −140 → 110%)
//   noise         alpha .02; a 64×64 repeating texture made once, only its offset changes, 12 times a second
//   glitch burst  every 8–14s (random), .12s: five white / blue horizontal strips (colour strips instead of sampling the screen),
//                 reshuffled halfway
// Strengths and on/off per layer: UITheme.screenFx; everything off: UIMotionSettings.screenFx = false; with
// UIMotionSettings.reduceMotion only the scanlines stay. Nothing drawn at all = no draw call.
// The layer installs itself after the first scene loads (a DontDestroyOnLoad Screen Space Overlay canvas, sorting order 32767, no
// raycaster), so the title and the HUD both get it. Update only advances an unscaled clock and calls Evaluate; no allocation per frame.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Screen FX")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIScreenFX : MaskableGraphic, ITimedEffect
    {
        public const string CanvasName = "ScreenFXCanvas", LayerName = "DecoScreenFX";
        public const int SortingOrder = 32767;
        const float ClockWrap = 960f;      // a multiple of the 8s band and the 1/12s noise step: the clock stays small without a jump
        const int NoiseSize = 64;

        const AdditionalCanvasShaderChannels NeededChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2
            | AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Tangent;

        public static UIScreenFX Instance { get; private set; }

        // ---- parameters the shader gets ---------------------------------------------------------------------------------------
        struct Params
        {
            public float scanA, scanPeriod, scanLine, noiseA, bandCentre, bandHalf, bandA, burstA, noiseX, noiseY, seed, cell;
            public bool Visible => scanA > 0f || noiseA > 0f || bandA > 0f || burstA > 0f;
            public bool Same(in Params o) =>
                scanA == o.scanA && scanPeriod == o.scanPeriod && scanLine == o.scanLine && noiseA == o.noiseA && bandCentre == o.bandCentre
                && bandHalf == o.bandHalf && bandA == o.bandA && burstA == o.burstA && noiseX == o.noiseX && noiseY == o.noiseY
                && seed == o.seed && cell == o.cell;
        }

        Params cur;
        float clock, burstStart = -1000f, nextBurst = -1f, burstSeed;
        uint rng = 0x9E3779B9u;

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => Fx.bandPeriod > 0f ? Fx.bandPeriod : 8f;

        static UITheme.ScreenFx Fx => UITheme.Current.screenFx;

        void ITimedEffect.Evaluate(float t) { Evaluate(t); }

        /// <summary>Poses the layer `t` seconds into its clock: band position, noise frame, and whether a burst (see PoseBurst) is on.</summary>
        public void Evaluate(float t)
        {
            var fx = Fx;
            var p = new Params();
            bool all = UIMotionSettings.screenFx, reduce = UIMotionSettings.reduceMotion;
            float scale = ScaleFactor();
            Rect r = rectTransform.rect;

            if (all && fx.scanlines && fx.scanlineAlpha > 0f)
            {
                p.scanA = fx.scanlineAlpha;
                p.scanPeriod = Mathf.Max(2f, Mathf.Round(Mathf.Max(fx.scanlineSpacing, 2f) * scale));
                p.scanLine = Mathf.Max(1f, Mathf.Round(scale));
            }
            if (all && !reduce)
            {
                if (fx.band && fx.bandAlpha > 0f)
                {
                    float period = Duration, h = Mathf.Max(fx.bandHeight, 1f);
                    float phase = Mathf.Repeat(t, period) / period;
                    p.bandCentre = Mathf.Lerp(-h, r.height * 1.1f, phase) + h * .5f;
                    p.bandHalf = h * .5f;
                    p.bandA = fx.bandAlpha;
                }
                if (fx.noise && fx.noiseAlpha > 0f)
                {
                    p.noiseA = fx.noiseAlpha;
                    float frame = Mathf.Floor(t * Mathf.Max(fx.noiseFps, 1f));
                    p.noiseX = Mathf.Floor(Hash(frame) * NoiseSize);
                    p.noiseY = Mathf.Floor(Hash(frame + 17.3f) * NoiseSize);
                    p.cell = Mathf.Max(1f, Mathf.Round(scale));
                }
                float since = t - burstStart;
                if (fx.burst && fx.burstAlpha > 0f && since >= 0f && since < fx.burstTime)
                {
                    p.burstA = fx.burstAlpha;
                    p.seed = burstSeed + (since >= fx.burstTime * .5f ? .5f : 0f);   // reshuffled halfway
                }
            }
            if (!cur.Same(p)) { cur = p; SetVerticesDirty(); }
        }

        /// <summary>For previews: a burst starting at clock time `start` with `seed` (Evaluate(t) shows it while t is inside it).</summary>
        public void PoseBurst(float start, float seed) { burstStart = start; burstSeed = seed; }

        static float Hash(float x) { float s = Mathf.Sin(x * 12.9898f + 78.233f) * 43758.5453f; return s - Mathf.Floor(s); }

        float NextRandom()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / 16777216f;
        }

        float ScaleFactor()
        {
            var c = canvas;
            return c != null && c.scaleFactor > 0f ? c.scaleFactor : 1f;
        }

        void Update()
        {
            var fx = Fx;
            clock += Time.unscaledDeltaTime;
            if (nextBurst < 0f) nextBurst = clock + Mathf.Lerp(fx.burstMinInterval, fx.burstMaxInterval, NextRandom());
            if (clock >= nextBurst)
            {
                burstStart = clock; burstSeed = Mathf.Floor(NextRandom() * 1000f);
                nextBurst = clock + Mathf.Lerp(fx.burstMinInterval, fx.burstMaxInterval, NextRandom());
            }
            if (clock >= ClockWrap) { clock -= ClockWrap; burstStart -= ClockWrap; nextBurst -= ClockWrap; }
            Evaluate(clock);
        }

        // ---- install ----------------------------------------------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            if (Instance != null) return;
            var go = new GameObject(CanvasName, typeof(RectTransform));
            DontDestroyOnLoad(go);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = SortingOrder;
            UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
            Create(go.transform);
        }

        /// <summary>Adds the full-screen layer (named Deco…, stretched, no raycast) under `parent` — normally its own top canvas.</summary>
        public static UIScreenFX Create(Transform parent)
        {
            var go = new GameObject(LayerName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var fx = go.AddComponent<UIScreenFX>();
            fx.raycastTarget = false;
            fx.Evaluate(0f);
            return fx;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Instance == null) Instance = this;
            EnsureChannels();
        }

        protected override void OnDisable()
        {
            if (Instance == this) Instance = null;
            base.OnDisable();
        }

        void EnsureChannels()
        {
            var c = canvas;
            if (c == null) return;
            c = c.rootCanvas;
            if ((c.additionalShaderChannels & NeededChannels) != NeededChannels) c.additionalShaderChannels |= NeededChannels;
        }

        // ---- shared material, noise texture, mesh -----------------------------------------------------------------------------
        static Material shared;
        static Texture2D noise;

        static Material Shared
        {
            get
            {
                if (shared == null)
                    shared = new Material(Shader.Find("Blindside/UI/ScreenFX")) { name = "ScreenFX (shared)", hideFlags = HideFlags.HideAndDontSave };
                return shared;
            }
        }

        static Texture2D Noise
        {
            get
            {
                if (noise != null) return noise;
                noise = new Texture2D(NoiseSize, NoiseSize, TextureFormat.RGBA32, false, true)
                    { name = "ScreenFX noise", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                var px = new Color32[NoiseSize * NoiseSize];
                uint s = 0x2545F491u;
                for (int i = 0; i < px.Length; i++)
                {
                    s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                    byte v = (byte)(s & 0xFF);
                    px[i] = new Color32(v, v, v, 255);
                }
                noise.SetPixels32(px);
                noise.Apply(false, true);
                return noise;
            }
        }

        public override Material defaultMaterial => Shared;
        public override Texture mainTexture => Noise;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f || !cur.Visible) return;
            EnsureChannels();

            // the blue goes as sRGB values: the shader mixes the layers in sRGB and converts at the end (see its frag)
            Color blue = UITheme.Current.blue;

            var v = UIVertex.simpleVert;
            v.color = color;
            v.uv1 = new Vector4(cur.scanA, cur.scanPeriod, cur.scanLine, cur.noiseA);
            v.uv2 = new Vector4(cur.bandCentre, cur.bandHalf, cur.bandA, cur.burstA);
            v.uv3 = new Vector4(cur.noiseX, cur.noiseY, cur.seed, cur.cell);
            v.tangent = new Vector4(blue.r, blue.g, blue.b, 0f);
            for (int i = 0; i < 4; i++)
            {
                bool right = i == 2 || i == 3, top = i == 1 || i == 2;
                v.position = new Vector3(right ? r.xMax : r.xMin, top ? r.yMax : r.yMin, 0f);
                v.uv0 = new Vector4(right ? 1f : 0f, top ? 1f : 0f, r.width, r.height);
                vh.AddVert(v);
            }
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }
#endif
    }
}
