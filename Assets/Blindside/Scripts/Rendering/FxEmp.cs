// SILENCE's EMP (ホロスキャン): a blue-white flash on SILENCE, a bubble of light swelling out to the scan radius, a shock ring running
// along the floor with it over a pale wash, electric arcs crackling outward, sparks thrown up, glints left hanging across the area,
// and a flickering blue light that spreads with the wave. Pooled rigs (PoolSize), particles on unscaled time; the ring, the wash and
// the light are posed by Evaluate(t) — nothing is allocated per frame. Materials: Resources/FX (FxKit).
using Blindside.UI;
using UnityEngine;

namespace Blindside.Rendering
{
    public class FxEmp : MonoBehaviour, ITimedEffect
    {
        const int PoolSize = 2;
        static FxEmp[] pool;
        static int next;
        static Transform poolRoot;

        static readonly Color White = new Color(.85f, .95f, 1f, 1f);
        static readonly Color Blue = new Color(.35f, .75f, 1f, 1f);

        ParticleSystem flash, bubble, arcs, sparks, glints;
        Renderer ring, wash;
        Light glow;
        MaterialPropertyBlock mpb;
        float t = -1f, radius = 7f;

        const float Swell = .55f;
        public float Duration => 1.4f;

        /// <summary>The blast at `center` (SILENCE's chest), reaching `radius` metres.</summary>
        public static void Blast(Vector3 center, float radius)
        {
            if (poolRoot == null) Build();
            var fx = pool[next];
            next = (next + 1) % PoolSize;
            fx.Fire(center, radius);
        }

        void Fire(Vector3 center, float r)
        {
            radius = Mathf.Max(1f, r);
            transform.SetPositionAndRotation(center, Quaternion.identity);
            var floor = new Vector3(center.x, .05f, center.z);
            ring.transform.position = floor + Vector3.up * .02f;
            wash.transform.position = floor;
            // the arcs run out about two thirds of the way; the bubble is as wide as the area; the glints hang across it
            var am = arcs.main; am.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.7f, radius * 2.8f);
            var bm = bubble.main; bm.startSize = radius * 2.3f;
            var gs = glints.shape; gs.radius = radius * .9f;
            flash.Emit(2);
            bubble.Emit(1);
            arcs.Emit(54);
            sparks.Emit(34);
            glints.Emit(40);
            t = 0f;
            ring.enabled = wash.enabled = true;
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

        /// <summary>The wave `time` seconds in: the ring and the wash out to the radius by Swell, then fading; the light flickering down.</summary>
        public void Evaluate(float time)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            float p = Mathf.Clamp01(time / Swell);
            float e = 1f - (1f - p) * (1f - p) * (1f - p);
            float rest = Mathf.Clamp01((time - Swell) / Mathf.Max(.01f, Duration - Swell));
            bool live = time < Duration;
            ring.enabled = wash.enabled = live;
            if (live)
            {
                float rs = Mathf.Max(.05f, radius * e);
                ring.transform.localScale = new Vector3(rs, 1f, rs);
                float ra = time < Swell ? .95f - .45f * p : .5f * (1f - rest);
                FxKit.Tint(ring, mpb, new Color(Blue.r, Blue.g, Blue.b, ra));
                wash.transform.localScale = new Vector3(rs * 1.05f, 1f, rs * 1.05f);
                float k = 1f - Mathf.Clamp01(time / Duration);
                FxKit.Tint(wash, mpb, new Color(Blue.r * .7f, Blue.g * .85f, Blue.b, .38f * k * k));
            }
            // the light: bright at the blast, a stutter as it dies (no random numbers: two beating sines)
            float lk = 1f - Mathf.Clamp01(time / Duration);
            float flicker = .65f + .35f * Mathf.Sin(time * 83f) * Mathf.Sin(time * 31f + 1.3f);
            glow.intensity = 26f * lk * lk * flicker;
            glow.range = 2f + radius * 1.4f * e;
            glow.enabled = live;
        }

        // ---- the pool: built once, on the first blast ----
        static void Build()
        {
            var root = new GameObject("FxEmpPool");
            poolRoot = root.transform;
            pool = new FxEmp[PoolSize];
            var glowMat = FxKit.Glow;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("FxEmp");
                go.transform.SetParent(poolRoot, false);
                var fx = go.AddComponent<FxEmp>();
                fx.flash = FxBlast.Make(go, "Flash", glowMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.1f, .16f), size: new Vector2(2.4f, 3.2f), speed: Vector2.zero,
                    from: White, to: new Color(Blue.r, Blue.g, Blue.b, 0f), grow: 1.6f);
                // one huge soft billboard: the bubble of light, grown out from nothing over the swell
                fx.bubble = FxBlast.Make(go, "Bubble", glowMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.7f, .7f), size: new Vector2(14f, 14f), speed: Vector2.zero,
                    from: new Color(Blue.r, Blue.g, Blue.b, .55f), to: new Color(Blue.r, Blue.g, Blue.b, 0f), grow: 1f);
                var sol = fx.bubble.sizeOverLifetime;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .05f, 0f, 4f), new Keyframe(.6f, 1f, 0f, 0f), new Keyframe(1f, 1.05f)));
                fx.arcs = FxBlast.Make(go, "Arcs", glowMat, ParticleSystemRenderMode.Stretch,
                    life: new Vector2(.14f, .32f), size: new Vector2(.05f, .1f), speed: new Vector2(10f, 16f),
                    from: White, to: new Color(Blue.r, Blue.g, Blue.b, 0f), grow: .5f,
                    shape: ParticleSystemShapeType.Circle, radius: .25f);
                fx.arcs.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // (the circle lies flat: arcs run out along the floor)
                var ar = fx.arcs.GetComponent<ParticleSystemRenderer>(); ar.lengthScale = 3.2f; ar.velocityScale = .03f;
                var an = fx.arcs.noise; an.enabled = true; an.strength = 6f; an.frequency = 4f; an.quality = ParticleSystemNoiseQuality.Medium;
                fx.sparks = FxBlast.Make(go, "Sparks", glowMat, ParticleSystemRenderMode.Stretch,
                    life: new Vector2(.3f, .7f), size: new Vector2(.025f, .05f), speed: new Vector2(3f, 8f),
                    from: new Color(.95f, .98f, 1f, 1f), to: new Color(Blue.r, Blue.g, Blue.b, 0f), grow: .6f,
                    shape: ParticleSystemShapeType.Sphere, radius: .2f, gravity: 1.2f);
                fx.glints = FxBlast.Make(go, "Glints", glowMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.5f, 1.1f), size: new Vector2(.08f, .2f), speed: new Vector2(.1f, .5f),
                    from: new Color(.7f, .9f, 1f, 1f), to: new Color(Blue.r, Blue.g, Blue.b, 0f), grow: .3f,
                    shape: ParticleSystemShapeType.Circle, radius: 6f, gravity: -.05f);
                fx.glints.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                // the floor: a pale wash and the shock ring, children of the pool root (placed per blast at floor height)
                fx.wash = FxKit.Flat(go.transform, "Wash", FxKit.SoftDisc);
                fx.ring = FxKit.Flat(go.transform, "Ring", FxKit.Ring);
                fx.wash.enabled = fx.ring.enabled = false;
                fx.glow = FxKit.PointLight(go.transform, new Color(.45f, .78f, 1f));
                fx.enabled = false;
                pool[i] = fx;
            }
        }
    }
}
