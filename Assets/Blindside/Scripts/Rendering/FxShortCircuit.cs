// A knocked-out gadget shorting (SILENCE's EMP: a camera, an electric field's node): every so often a crackle — a spray of sparks
// that fall, a blue-white pop, an arc jumping across the casing for a few frames and a blink of light — and a thin wisp of smoke
// rising the whole time. One per gadget, switched by SetOn; the bursts come at irregular intervals from its own System.Random (the
// game's random streams are never touched). Unscaled time; nothing allocated per frame.
using Blindside.UI;
using UnityEngine;

namespace Blindside.Rendering
{
    public class FxShortCircuit : MonoBehaviour, ITimedEffect
    {
        static readonly Color ArcCol = new Color(.6f, .85f, 1f, 1f);

        ParticleSystem sparks, pop, wisp;
        LineRenderer arc;
        Light blink;
        System.Random rng;
        readonly Vector3[] pts = new Vector3[7];
        float clock, burstAt = -10f, nextBurst, size = 1f;
        bool on;

        /// <summary>One crackle's flicker (the arc and the light), seconds.</summary>
        public float Duration => .14f;

        /// <summary>A short-circuit rig on `parent` at `local` (the device's body), `size` ≈ the device's width in metres ÷ .25. Off until SetOn.</summary>
        public static FxShortCircuit Attach(Transform parent, Vector3 local, float size, int seed)
        {
            var go = new GameObject("FxShortCircuit");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var fx = go.AddComponent<FxShortCircuit>();
            fx.size = size;
            fx.rng = new System.Random(seed);
            fx.Build();
            fx.enabled = false;
            return fx;
        }

        public bool On => on;

        public void SetOn(bool value)
        {
            if (on == value) return;
            on = value;
            enabled = value;
            var em = wisp.emission; em.enabled = value;
            if (value) { wisp.Play(); nextBurst = clock + .05f; }
            else { wisp.Stop(false, ParticleSystemStopBehavior.StopEmitting); arc.enabled = false; blink.enabled = false; }
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (clock >= nextBurst) Burst();
            Evaluate(clock - burstAt);
        }

        void Burst()
        {
            burstAt = clock;
            nextBurst = clock + .1f + (float)rng.NextDouble() * .55f;
            sparks.Emit(10 + rng.Next(14));
            pop.Emit(1);
            // the arc: a jagged bolt jumping off the casing and out past it (seen from the battle camera's height)
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, s = .28f * size;
            var from = new Vector3(Mathf.Cos(a) * s, ((float)rng.NextDouble() - .3f) * s, Mathf.Sin(a) * s);
            var to = -from * 1.6f + new Vector3(0f, ((float)rng.NextDouble() - .2f) * s, 0f);
            for (int i = 0; i < pts.Length; i++)
            {
                float k = i / (pts.Length - 1f);
                var p = Vector3.Lerp(from, to, k);
                if (i > 0 && i < pts.Length - 1)
                    p += new Vector3((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f) * (.22f * size);
                pts[i] = p;
            }
            arc.SetPositions(pts);
        }

        /// <summary>`time` seconds after a crackle: the arc thins out and the light blinks off by Duration.</summary>
        public void Evaluate(float time)
        {
            float k = Mathf.Clamp01(1f - time / Duration);
            bool live = on && k > 0f;
            arc.enabled = live;
            blink.enabled = live;
            if (!live) return;
            arc.widthMultiplier = .055f * size * (.4f + .6f * k);
            blink.intensity = 7f * k;
        }

        void Build()
        {
            var glowMat = FxKit.Glow;
            sparks = FxBlast.Make(gameObject, "Sparks", glowMat, ParticleSystemRenderMode.Stretch,
                life: new Vector2(.25f, .6f), size: new Vector2(.035f, .07f), speed: new Vector2(1.6f, 4.2f),
                from: new Color(1f, .95f, .75f, 1f), to: new Color(1f, .55f, .2f, 0f), grow: .5f,
                shape: ParticleSystemShapeType.Sphere, radius: .05f * size, gravity: 1.6f);
            pop = FxBlast.Make(gameObject, "Pop", glowMat, ParticleSystemRenderMode.Billboard,
                life: new Vector2(.08f, .14f), size: new Vector2(.8f, 1.2f), speed: Vector2.zero,
                from: new Color(.85f, .95f, 1f, 1f), to: new Color(ArcCol.r, ArcCol.g, ArcCol.b, 0f), grow: 1.4f);
            var pm = pop.main; pm.startSize = new ParticleSystem.MinMaxCurve(.8f * size, 1.2f * size);
            wisp = FxBlast.Make(gameObject, "Wisp", FxKit.Smoke, ParticleSystemRenderMode.Billboard,
                life: new Vector2(1.6f, 2.6f), size: new Vector2(.12f, .2f), speed: new Vector2(.15f, .35f),
                from: new Color(.25f, .25f, .26f, .4f), to: new Color(.5f, .5f, .52f, 0f), grow: 4f,
                shape: ParticleSystemShapeType.Sphere, radius: .04f, gravity: -.03f, spin: true);
            var wm = wisp.main; wm.loop = true; wm.maxParticles = 24;
            var we = wisp.emission; we.enabled = false; we.rateOverTime = 4f;

            var lgo = new GameObject("Arc");
            lgo.transform.SetParent(transform, false);
            arc = lgo.AddComponent<LineRenderer>();
            arc.useWorldSpace = false;
            arc.positionCount = pts.Length;
            arc.sharedMaterial = FxKit.Additive;
            arc.startColor = arc.endColor = ArcCol;
            arc.numCapVertices = 2;
            arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arc.receiveShadows = false;
            arc.enabled = false;

            blink = FxKit.PointLight(transform, new Color(.6f, .82f, 1f));
            blink.range = 3.2f * size;
        }
    }
}
