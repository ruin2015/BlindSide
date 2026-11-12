// The flashbang (閃光弾): a white-hot core that pops, a blinding halo that floods the area, a light that spreads out across the rooms
// in a fraction of a second and dies away slowly, a bright wash running out over the floor, a few sparks and the canister's smoke.
// Pooled rigs (PoolSize), particles on unscaled time; the wash and the light are posed by Evaluate(t) — nothing allocated per frame.
using Blindside.UI;
using UnityEngine;

namespace Blindside.Rendering
{
    public class FxFlash : MonoBehaviour, ITimedEffect
    {
        const int PoolSize = 2;
        static FxFlash[] pool;
        static int next;
        static Transform poolRoot;

        static readonly Color Hot = new Color(1f, .98f, .92f, 1f);
        static readonly Color Warm = new Color(1f, .9f, .72f, 1f);

        ParticleSystem core, halo, sparks, smoke;
        Renderer wash;
        Light flashLight;
        MaterialPropertyBlock mpb;
        float t = -1f, radius = 3.5f;

        const float Spread = .16f, Peak = .05f;
        public float Duration => 1.2f;

        /// <summary>A flashbang going off at `pos` (a little over the floor); its light reaches `radius` metres and beyond.</summary>
        public static void Bang(Vector3 pos, float radius)
        {
            if (poolRoot == null) Build();
            var fx = pool[next];
            next = (next + 1) % PoolSize;
            fx.Fire(pos, radius);
        }

        void Fire(Vector3 pos, float r)
        {
            radius = Mathf.Max(1f, r);
            transform.SetPositionAndRotation(pos, Quaternion.identity);
            wash.transform.position = new Vector3(pos.x, .06f, pos.z);
            var hm = halo.main; hm.startSize = radius * 3.2f;
            core.Emit(2);
            halo.Emit(1);
            sparks.Emit(26);
            smoke.Emit(7);
            t = 0f;
            wash.enabled = true;
            Evaluate(0f);
            enabled = true;
        }

        void Update()
        {
            if (t < 0f) { enabled = false; return; }
            t += Time.unscaledDeltaTime;
            Evaluate(t);
            if (t >= Duration) { t = -1f; enabled = false; }
        }

        /// <summary>`time` seconds in: the light's reach spreads out by Spread, its strength holds a moment then falls away; the wash with it.</summary>
        public void Evaluate(float time)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            bool live = time < Duration;
            float p = Mathf.Clamp01(time / Spread);
            float e = 1f - (1f - p) * (1f - p) * (1f - p);
            float fall = time <= Peak ? 1f : Mathf.Pow(1f - Mathf.Clamp01((time - Peak) / (Duration - Peak)), 2.2f);
            flashLight.enabled = live;
            flashLight.range = .5f + radius * 2.6f * e;
            flashLight.intensity = 55f * fall;
            wash.enabled = live;
            if (!live) return;
            float ws = Mathf.Max(.05f, radius * 1.7f * e);
            wash.transform.localScale = new Vector3(ws, 1f, ws);
            FxKit.Tint(wash, mpb, new Color(Warm.r, Warm.g, Warm.b, .95f * fall));
        }

        // ---- the pool: built once, on the first bang ----
        static void Build()
        {
            var root = new GameObject("FxFlashPool");
            poolRoot = root.transform;
            pool = new FxFlash[PoolSize];
            var glowMat = FxKit.Glow;
            var smokeMat = FxKit.Smoke;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("FxFlash");
                go.transform.SetParent(poolRoot, false);
                var fx = go.AddComponent<FxFlash>();
                fx.core = FxBlast.Make(go, "Core", glowMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.12f, .18f), size: new Vector2(1.6f, 2.2f), speed: Vector2.zero,
                    from: Hot, to: new Color(Warm.r, Warm.g, Warm.b, 0f), grow: 2.2f);
                // the halo: one huge soft billboard, flung open in a few frames and fading slower than the core
                fx.halo = FxBlast.Make(go, "Halo", glowMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.55f, .55f), size: new Vector2(11f, 11f), speed: Vector2.zero,
                    from: new Color(Hot.r, Hot.g, Hot.b, .9f), to: new Color(Warm.r, Warm.g, Warm.b, 0f), grow: 1f);
                var sol = fx.halo.sizeOverLifetime;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .12f, 0f, 9f), new Keyframe(.25f, 1f, 0f, 0f), new Keyframe(1f, 1.1f)));
                fx.sparks = FxBlast.Make(go, "Sparks", glowMat, ParticleSystemRenderMode.Stretch,
                    life: new Vector2(.2f, .45f), size: new Vector2(.02f, .045f), speed: new Vector2(4f, 9f),
                    from: Hot, to: new Color(Warm.r, Warm.g, Warm.b, 0f), grow: .6f,
                    shape: ParticleSystemShapeType.Sphere, radius: .1f, gravity: .8f);
                fx.smoke = FxBlast.Make(go, "Smoke", smokeMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(1.4f, 2.4f), size: new Vector2(.35f, .6f), speed: new Vector2(.3f, .9f),
                    from: new Color(.82f, .82f, .8f, .45f), to: new Color(.86f, .86f, .85f, 0f), grow: 3f,
                    shape: ParticleSystemShapeType.Hemisphere, radius: .15f, gravity: -.04f, spin: true, drag: 1.4f);
                fx.wash = FxKit.Flat(go.transform, "Wash", FxKit.SoftDisc);
                fx.wash.enabled = false;
                fx.flashLight = FxKit.PointLight(go.transform, new Color(1f, .96f, .88f));
                fx.enabled = false;
                pool[i] = fx;
            }
        }
    }
}
