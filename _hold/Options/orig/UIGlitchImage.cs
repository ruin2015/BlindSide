// A picture drawn by the "Blindside/UI/Glitch" shader, with the spec's glitch presets as ITimedEffect.Evaluate(t):
//   CardEnter 0.8s  wipe 0–.26s (from the right, diagonal) · body jitter (brightness / saturation / hue / RGB split / sideways offset) ·
//                   three horizontal strips · light sweep from .3s for .55s (ease-in) · card-wide brightness flash 1.8 → 1.15 → 1 (.5s) · card shake
//   Select    0.5s  the face: brightness 2.2, then offsets, hue +80°, RGB split
//   Hit       0.42s left-right shake (−8, 7, −5, 3) and a red flash (.5s)
// Everything jumps from keyframe to keyframe (no interpolation) except the wipe, the sweep and the brightness flash, which are eased
// as in the mockup (ui_target.html).
// Evaluate returns the card's own offset (the shake) — the caller applies it to a RectTransform (or set shakeTarget and let Update
// do it). With UIMotionSettings.reduceMotion only the brightness flash is left. Update only advances an unscaled clock (so hit-stop
// doesn't freeze the UI) and calls Evaluate. All keyframes are static tables: nothing is allocated per frame.
// The mesh is one quad; its parameters live in the vertices (see the shader header), the material is shared by every glitch image.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Glitch Image")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIGlitchImage : MaskableGraphic, ITimedEffect
    {
        public enum Preset { CardEnter, Select, Hit }

        const AdditionalCanvasShaderChannels NeededChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2
            | AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

        public Preset preset = Preset.CardEnter;
        [SerializeField] Texture texture;
        [SerializeField] Rect uvRect = new Rect(0f, 0f, 1f, 1f);
        [Tooltip("Optional: Update moves this RectTransform by the offset Evaluate returns (the card shake), from where it was at Play().")]
        public RectTransform shakeTarget;

        // ---- parameters the shader gets ---------------------------------------------------------------------------------------
        struct Params
        {
            public float reveal, bright, sat, hue, split, body, s0, s1, s2, sweep, flash;
            public static Params Neutral => new Params { reveal = 1f, bright = 1f, sat = 1f, sweep = -1f };
            public bool Same(in Params o) =>
                reveal == o.reveal && bright == o.bright && sat == o.sat && hue == o.hue && split == o.split && body == o.body
                && s0 == o.s0 && s1 == o.s1 && s2 == o.s2 && sweep == o.sweep && flash == o.flash;
        }

        Params cur = Params.Neutral;
        float overLum = -1f;
        float clock;
        bool running;
        float toneSat = 1f, toneBright = 1f, lastT = float.MaxValue;

        /// <summary>A lasting filter on top of the preset (CSS filter: grayscale / brightness on the card): saturation 0..1 and
        /// brightness multiplier. Re-poses at the last Evaluate time.</summary>
        public void SetTone(float saturation, float brightness)
        {
            if (saturation == toneSat && brightness == toneBright) return;
            toneSat = saturation; toneBright = brightness;
            Evaluate(lastT);
        }
        Vector2 baseAnchored;

        /// <summary>The colour under the picture (sRGB) when it is drawn translucent (color.a &lt; 1, CSS opacity): the shader then picks
        /// the alpha that gives sRGB blending's brightness over it in a Linear project. A colour with alpha 0 switches the match off.</summary>
        public void SetOver(Color under)
        {
            float l = under.a > 0f ? .2126f * under.r + .7152f * under.g + .0722f * under.b : -1f;
            if (l == overLum) return;
            overLum = l;
            SetVerticesDirty();
        }

        // ---- keyframes (time as a fraction of the preset's duration; each holds until the next) ----------------------------------
        struct Body
        {
            public float t, bright, sat, hue, split, x;
            public Body(float t, float bright, float sat, float hue, float split, float x) { this.t = t; this.bright = bright; this.sat = sat; this.hue = hue; this.split = split; this.x = x; }
        }
        struct Key
        {
            public float t, v;
            public Key(float t, float v) { this.t = t; this.v = v; }
        }

        static readonly Body[] CardBody =
        {
            new Body(0f, 2.2f, .2f, 0f, 0f, 0f),        // 0–12% (held until the 14% key, as steps(1) does): brightness 2.2, saturation .2
            new Body(.14f, 1.5f, 1f, 90f, 7f, -14f),    // x−14, brightness 1.5, hue +90°, red −7 / blue +7
            new Body(.24f, 1f, 1f, -50f, -6f, 11f),     // x+11, hue −50°, red +6 / blue −6
            new Body(.32f, 1.3f, 1f, 0f, 4f, -6f),      // x−6, brightness 1.3, ±4
            new Body(.42f, 1f, 1f, 0f, 0f, 5f),         // x+5, normal
            new Body(.52f, 1f, 1f, 0f, -2f, -3f),       // x−3, ±2
            new Body(.62f, 1f, 1f, 0f, 0f, 0f),
        };
        static readonly Key[] Strip1 = { new Key(.16f, 26f), new Key(.22f, -30f), new Key(.30f, 0f), new Key(.38f, 18f), new Key(.46f, 0f) };
        static readonly Key[] Strip2 = { new Key(.19f, -34f), new Key(.26f, 22f), new Key(.34f, 0f), new Key(.42f, -16f), new Key(.50f, 0f) };
        static readonly Key[] Strip3 = { new Key(.21f, 30f), new Key(.28f, 0f), new Key(.36f, -24f), new Key(.44f, 0f) };
        static readonly Key[] CardShake = { new Key(.14f, -7f), new Key(.24f, 6f), new Key(.34f, -3f), new Key(.44f, 0f) };

        static readonly Body[] SelectBody =
        {
            new Body(0f, 2.2f, 1f, 0f, 0f, 0f),
            new Body(.14f, 1f, 1f, 80f, 0f, -5f),
            new Body(.28f, 1f, 1f, 0f, 3f, 4f),
            new Body(.42f, 1f, 1f, 0f, 0f, -2f),
            new Body(.60f, 1f, 1f, 0f, 0f, 0f),
        };

        const float CardDuration = 0.8f, SelectDuration = 0.5f, HitShakeDuration = 0.42f, HitFlashDuration = 0.5f;
        const float WipeTime = 0.26f, SweepStart = 0.3f, SweepTime = 0.55f, CardFlashTime = 0.5f;

        // CSS cubic-bezier(x1, y1, x2, y2) at x (Newton on x, then y): the mockup's wipe (.2,.9,.2,1), flash (ease), sweep (ease-in)
        static float Bezier(float x, float x1, float y1, float x2, float y2)
        {
            x = Mathf.Clamp01(x);
            float u = x;
            for (int i = 0; i < 8; i++)
            {
                float iu = 1f - u;
                float bx = 3f * iu * iu * u * x1 + 3f * iu * u * u * x2 + u * u * u - x;
                float dx = 3f * iu * iu * x1 + 6f * iu * u * (x2 - x1) + 3f * u * u * (1f - x2);
                if (Mathf.Abs(bx) < 1e-5f || Mathf.Abs(dx) < 1e-6f) break;
                u = Mathf.Clamp01(u - bx / dx);
            }
            float w = 1f - u;
            return 3f * w * w * u * y1 + 3f * w * u * u * y2 + u * u * u;
        }

        static int Index(Body[] keys, float p) { for (int i = keys.Length - 1; i >= 0; i--) if (p >= keys[i].t) return i; return -1; }
        static float Value(Key[] keys, float p) { for (int i = keys.Length - 1; i >= 0; i--) if (p >= keys[i].t) return keys[i].v; return 0f; }

        public float Duration
        {
            get
            {
                switch (preset)
                {
                    case Preset.Select: return SelectDuration;
                    case Preset.Hit: return Mathf.Max(HitShakeDuration, HitFlashDuration);
                    default: return Mathf.Max(CardDuration, SweepStart + SweepTime);   // the sweep ends at .85s
                }
            }
        }

        /// <summary>The card offset (the shake) at the last Evaluate.</summary>
        public Vector2 Offset { get; private set; }

        // ---- the effect -------------------------------------------------------------------------------------------------------
        void ITimedEffect.Evaluate(float t) { Evaluate(t); }

        /// <summary>Poses the glitch `t` seconds into the preset (t ≥ Duration = settled) and returns the card's offset.</summary>
        public Vector2 Evaluate(float t)
        {
            var p = Params.Neutral;
            float shake = 0f;
            bool reduce = UIMotionSettings.reduceMotion;
            if (t < Duration)
            {
                switch (preset)
                {
                    case Preset.CardEnter:
                    {
                        float f = t / CardDuration;
                        // the card-wide brightness flash: 1.8 → 1.15 (40%) → 1 over .5s, eased per segment like the mockup's `ease`
                        float fa = CardFlashTime * .4f;
                        p.bright = t < fa ? Mathf.Lerp(1.8f, 1.15f, Bezier(t / fa, .25f, .1f, .25f, 1f))
                            : t < CardFlashTime ? Mathf.Lerp(1.15f, 1f, Bezier((t - fa) / (CardFlashTime - fa), .25f, .1f, .25f, 1f)) : 1f;
                        if (!reduce)
                        {
                            p.reveal = Bezier(t / WipeTime, .2f, .9f, .2f, 1f);
                            int k = Index(CardBody, f);
                            if (k >= 0) { var b = CardBody[k]; p.bright *= b.bright; p.sat = b.sat; p.hue = b.hue; p.split = b.split; p.body = b.x; }
                            p.s0 = Value(Strip1, f); p.s1 = Value(Strip2, f); p.s2 = Value(Strip3, f);
                            float s = (t - SweepStart) / SweepTime;
                            p.sweep = s > 0f && s < 1f ? Bezier(s, .42f, 0f, 1f, 1f) : -1f;
                            shake = Value(CardShake, f);
                        }
                        break;
                    }
                    case Preset.Select:
                    {
                        int k = Index(SelectBody, t / SelectDuration);
                        if (k >= 0)
                        {
                            var b = SelectBody[k];
                            p.bright = b.bright;
                            if (!reduce) { p.sat = b.sat; p.hue = b.hue; p.split = b.split; p.body = b.x; }
                        }
                        break;
                    }
                    case Preset.Hit:
                    {
                        if (!reduce)
                        {
                            float q = t / HitShakeDuration;
                            shake = q < .25f ? -8f : q < .5f ? 7f : q < .75f ? -5f : q < 1f ? 3f : 0f;
                        }
                        // the red flash: five steps from full to nothing over .5s
                        p.flash = Mathf.Max(0f, 1f - Mathf.Floor(t / HitFlashDuration * 5f) * .2f);
                        break;
                    }
                }
            }
            Offset = new Vector2(shake, 0f);
            lastT = t;
            p.sat *= toneSat; p.bright *= toneBright;
            if (!cur.Same(p)) { cur = p; SetVerticesDirty(); }
            return Offset;
        }

        /// <summary>Starts the preset on the unscaled clock.</summary>
        public void Play()
        {
            clock = 0f; running = true;
            if (shakeTarget != null) baseAnchored = shakeTarget.anchoredPosition;
            Apply(Evaluate(0f));
        }

        void Apply(Vector2 offset) { if (shakeTarget != null) shakeTarget.anchoredPosition = baseAnchored + offset; }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Apply(Evaluate(clock));
            if (clock >= Duration) running = false;
        }

        // ---- picture --------------------------------------------------------------------------------------------------------
        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        public void SetTexture(Texture tex, Rect uv = default)
        {
            texture = tex;
            uvRect = (uv.width <= 0f || uv.height <= 0f) ? new Rect(0f, 0f, 1f, 1f) : uv;
            SetAllDirty();
        }

        public void SetSprite(Sprite sprite)
        {
            if (sprite == null) { SetTexture(null); return; }
            Texture2D tex = sprite.texture; Rect r = sprite.textureRect;
            SetTexture(tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height));
        }

        // ---- shared material and mesh ---------------------------------------------------------------------------------------
        static Material shared;
        static Material Shared
        {
            get
            {
                if (shared == null)
                    shared = new Material(Shader.Find("Blindside/UI/Glitch")) { name = "Glitch (shared)", hideFlags = HideFlags.HideAndDontSave };
                return shared;
            }
        }

        public override Material defaultMaterial => Shared;

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureChannels();
        }

        void EnsureChannels()
        {
            var c = canvas;
            if (c == null) return;
            c = c.rootCanvas;
            if ((c.additionalShaderChannels & NeededChannels) != NeededChannels) c.additionalShaderChannels |= NeededChannels;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;
            EnsureChannels();

            float insetU = 0f;
            if (texture != null) insetU = 0.5f / texture.width;          // (v: the shader's _MainTex_TexelSize)

            var v = UIVertex.simpleVert;
            v.color = color;
            v.tangent = new Vector4(cur.flash, cur.sat, cur.body, insetU);
            v.uv1 = new Vector4(cur.reveal, cur.bright, cur.hue, cur.split);
            v.uv2 = new Vector4(cur.s0, cur.s1, cur.s2, cur.sweep);
            v.uv3 = new Vector4(uvRect.xMin, uvRect.yMin, uvRect.xMax, uvRect.yMax);
            for (int i = 0; i < 4; i++)
            {
                bool right = i == 2 || i == 3, top = i == 1 || i == 2;
                v.position = new Vector3(right ? r.xMax : r.xMin, top ? r.yMax : r.yMin, 0f);
                float nx = right ? 1f : 0f, ny = top ? 1f : 0f;
                v.normal = new Vector3(nx, ny, overLum);
                v.uv0 = new Vector4(Mathf.Lerp(uvRect.xMin, uvRect.xMax, nx), Mathf.Lerp(uvRect.yMin, uvRect.yMax, ny), r.width, r.height);
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
