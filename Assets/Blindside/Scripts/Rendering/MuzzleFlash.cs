// A muzzle flash built from particle systems: a white-hot core and a four-point star at the muzzle, a soft halo, two or
// three flame tongues out along the barrel, a spray of sparks, a puff of smoke that hangs a moment, and a brief light.
// Rigs are pooled and reused round-robin, so a burst allocates nothing after the pool is built; the particles run on
// unscaled time (a hit stop doesn't freeze a flash half-drawn). Textures / materials: Resources/FX (FX_Muzzle*, FX_Smoke).
using Blindside.UI;
using UnityEngine;

namespace Blindside.Rendering
{
    public class MuzzleFlash : MonoBehaviour, ITimedEffect
    {
        const int PoolSize = 10;
        static MuzzleFlash[] pool;
        static int next;
        static Transform poolRoot;

        ParticleSystem core, star, glow, flame, sparks, smoke;
        Light flashLight;
        float t = -1f, lightPeak;

        public float Duration => .07f;

        /// <summary>One shot's flash at `muzzle`, the barrel pointing along `dir`. `scale` 1 = a rifle on a 1.7 m figure.</summary>
        public static void Play(Vector3 muzzle, Vector3 dir, float scale = 1f)
        {
            if (poolRoot == null) Build();
            var fx = pool[next];
            next = (next + 1) % PoolSize;
            fx.Fire(muzzle, dir, scale);
        }

        void Fire(Vector3 muzzle, Vector3 dir, float scale)
        {
            if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
            transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(dir.normalized, Vector3.up));
            transform.localScale = Vector3.one * scale;
            core.Emit(1);
            star.Emit(1);
            glow.Emit(1);
            flame.Emit(5);
            sparks.Emit(8);
            smoke.Emit(4);
            lightPeak = 7f * scale;
            flashLight.range = 4.5f * scale;
            t = 0f;
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

        /// <summary>The light: full at the shot, gone after Duration (the particles run themselves).</summary>
        public void Evaluate(float time)
        {
            float k = Mathf.Clamp01(1f - time / Duration);
            flashLight.intensity = lightPeak * k * k;
            flashLight.enabled = k > 0f;
        }

        // ---- the pool: built once, on the first shot ----
        static void Build()
        {
            var root = new GameObject("MuzzleFlashPool");
            poolRoot = root.transform;
            pool = new MuzzleFlash[PoolSize];
            var starMat = Resources.Load<Material>("FX/FX_MuzzleStar");
            var glowMat = Resources.Load<Material>("FX/FX_MuzzleGlow");
            var flameMat = Resources.Load<Material>("FX/FX_MuzzleFlame");
            var smokeMat = Resources.Load<Material>("FX/FX_Smoke");
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("MuzzleFlash");
                go.transform.SetParent(poolRoot, false);
                var fx = go.AddComponent<MuzzleFlash>();
                // white-hot core
                fx.core = Make(go, "Core", glowMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.035f, .045f), size: new Vector2(.16f, .2f), speed: Vector2.zero,
                    from: new Color(1f, 1f, .95f, 1f), to: new Color(1f, .85f, .5f, 0f), shrink: .4f);
                // four-point star, a random turn each shot
                fx.star = Make(go, "Star", starMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.045f, .06f), size: new Vector2(.28f, .4f), speed: Vector2.zero,
                    from: new Color(1f, .92f, .65f, 1f), to: new Color(1f, .55f, .2f, 0f), shrink: .6f, spin: true);
                // halo
                fx.glow = Make(go, "Glow", glowMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.06f, .075f), size: new Vector2(.6f, .75f), speed: Vector2.zero,
                    from: new Color(1f, .62f, .25f, .55f), to: new Color(1f, .4f, .1f, 0f), shrink: 1.3f);
                // flame tongues along the barrel
                fx.flame = Make(go, "Flame", glowMat, ParticleSystemRenderMode.Stretch,
                    life: new Vector2(.04f, .06f), size: new Vector2(.12f, .2f), speed: new Vector2(3f, 6f),
                    from: new Color(1f, .78f, .42f, 1f), to: new Color(1f, .38f, .1f, 0f), shrink: .7f, cone: 6f);
                // sparks
                fx.sparks = Make(go, "Sparks", glowMat, ParticleSystemRenderMode.Stretch,
                    life: new Vector2(.08f, .2f), size: new Vector2(.012f, .022f), speed: new Vector2(4f, 10f),
                    from: new Color(1f, .85f, .5f, 1f), to: new Color(1f, .5f, .15f, 0f), shrink: 1f, cone: 28f, gravity: .6f);
                // smoke that drifts forward and up, then thins
                fx.smoke = Make(go, "Smoke", smokeMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.55f, 1f), size: new Vector2(.14f, .22f), speed: new Vector2(.3f, .9f),
                    from: new Color(.42f, .41f, .4f, .32f), to: new Color(.5f, .5f, .5f, 0f), shrink: 3.2f, cone: 18f, gravity: -.05f, spin: true);
                fx.flashLight = go.AddComponent<Light>();
                fx.flashLight.type = LightType.Point;
                fx.flashLight.color = new Color(1f, .74f, .42f);
                fx.flashLight.shadows = LightShadows.None;
                fx.flashLight.enabled = false;
                fx.enabled = false;
                pool[i] = fx;
            }
        }

        // One emitter, configured once: emission off (Fire emits), world space, unscaled time; sizes / speeds scale with the
        // rig (scaling mode Hierarchy). `shrink` < 1 shrinks over the life, > 1 grows.
        static ParticleSystem Make(GameObject parent, string name, Material mat, ParticleSystemRenderMode mode,
            Vector2 life, Vector2 size, Vector2 speed, Color from, Color to, float shrink,
            float cone = 0f, float gravity = 0f, bool spin = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.maxParticles = 32;
            if (spin) main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = cone > 0f;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cone;
            shape.radius = .01f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                      new[] { new GradientAlphaKey(from.a, 0f), new GradientAlphaKey(from.a * .6f, .35f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, shrink));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (mode == ParticleSystemRenderMode.Stretch) { r.lengthScale = 2.6f; r.velocityScale = .04f; }
            return ps;
        }
    }
}
