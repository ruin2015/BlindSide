// Explosions and wall breaks built from particle systems (the MuzzleFlash way: pooled rigs, emission off and Emit(n) per burst,
// world space, unscaled time so a hit stop doesn't freeze one half-drawn; materials from Resources/FX):
//   Explosion(pos, scale)       the frag grenade — a white-hot flash, a fireball that swells and darkens, sparks thrown out
//                               and falling, dark debris chunks tumbling, a ring of dust rolling out along the floor, a column
//                               of smoke that rises and thins over ~3 s, and a short orange light
//   WallBreak(pos, tint, blast) a wall panel coming down — a dust cloud in the wall's colour that rolls out and settles,
//                               chips of the wall thrown out with gravity; `blast` adds a flash, sparks and a little fire
//                               (breaching charges, the launcher, a grenade) — without it, only dust and chips (the hammer, a shotgun)
// Rigs are reused round-robin (PoolSize each), so a burst allocates nothing once the pool is built.
using Blindside.UI;
using UnityEngine;

namespace Blindside.Rendering
{
    public class FxBlast : MonoBehaviour, ITimedEffect
    {
        const int PoolSize = 6;
        static FxBlast[] pool;
        static int next;
        static Transform poolRoot;

        ParticleSystem flash, fire, sparks, debris, dust, smoke;
        Light blastLight;
        float t = -1f, lightPeak, lightTime = .35f;

        public float Duration => lightTime;

        static readonly Color Orange = new Color(1f, .62f, .24f, 1f);

        /// <summary>A grenade's explosion at `pos` (its centre near the floor). `scale` 1 = a frag grenade.</summary>
        public static void Explosion(Vector3 pos, float scale = 1f)
        {
            var fx = Next();
            fx.transform.SetPositionAndRotation(pos, Quaternion.identity);
            fx.transform.localScale = Vector3.one * scale;
            fx.Tint(fx.debris, new Color(.16f, .15f, .14f, 1f), new Color(.1f, .1f, .1f, 0f));
            fx.Tint(fx.dust, new Color(.55f, .5f, .44f, .55f), new Color(.6f, .58f, .54f, 0f));
            fx.flash.Emit(2);
            fx.fire.Emit(14);
            fx.sparks.Emit(40);
            fx.debris.Emit(22);
            fx.dust.Emit(18);
            fx.smoke.Emit(12);
            fx.Light(9f * scale, 7f * scale, .45f);
        }

        /// <summary>A wall panel cell coming down at `pos` (mid-height of the wall): dust and chips in `wall`'s colour; `blast` adds the fire.</summary>
        public static void WallBreak(Vector3 pos, Color wall, bool blast)
        {
            var fx = Next();
            fx.transform.SetPositionAndRotation(pos, Quaternion.identity);
            fx.transform.localScale = Vector3.one * (blast ? .8f : .65f);
            var chip = new Color(wall.r * .8f, wall.g * .8f, wall.b * .8f, 1f);
            fx.Tint(fx.debris, chip, new Color(chip.r, chip.g, chip.b, 0f));
            var cloud = Color.Lerp(wall, new Color(.75f, .72f, .66f), .5f);
            fx.Tint(fx.dust, new Color(cloud.r, cloud.g, cloud.b, .6f), new Color(cloud.r, cloud.g, cloud.b, 0f));
            fx.debris.Emit(blast ? 20 : 14);
            fx.dust.Emit(blast ? 16 : 12);
            fx.smoke.Emit(blast ? 5 : 3);
            if (blast)
            {
                fx.flash.Emit(1);
                fx.fire.Emit(6);
                fx.sparks.Emit(18);
                fx.Light(5f, 5f, .3f);
            }
        }

        static FxBlast Next()
        {
            if (poolRoot == null) Build();
            var fx = pool[next];
            next = (next + 1) % PoolSize;
            return fx;
        }

        void Tint(ParticleSystem ps, Color from, Color to)
        {
            var col = ps.colorOverLifetime;
            col.color = Fade(from, to);
        }

        void Light(float peak, float range, float time)
        {
            lightPeak = peak; lightTime = time;
            blastLight.range = range;
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

        /// <summary>The light: full at the blast, gone after Duration (the particles run themselves).</summary>
        public void Evaluate(float time)
        {
            float k = Mathf.Clamp01(1f - time / Mathf.Max(.01f, Duration));
            blastLight.intensity = lightPeak * k * k;
            blastLight.enabled = k > 0f;
        }

        // ---- the pool: built once, on the first blast ----
        static void Build()
        {
            var root = new GameObject("FxBlastPool");
            poolRoot = root.transform;
            pool = new FxBlast[PoolSize];
            var glowMat = Resources.Load<Material>("FX/FX_MuzzleGlow");
            var flameMat = Resources.Load<Material>("FX/FX_MuzzleFlame");
            var smokeMat = Resources.Load<Material>("FX/FX_Smoke");
            // the chunks: a plain cube with the particle shader, untextured (the colour is the particle's)
            Material chunkMat = null;
            if (smokeMat != null)
            {
                chunkMat = new Material(smokeMat) { name = "FX_Chunk" };
                if (chunkMat.HasProperty("_BaseMap")) chunkMat.SetTexture("_BaseMap", null);
                if (chunkMat.HasProperty("_MainTex")) chunkMat.SetTexture("_MainTex", null);
            }
            var cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cube = cubeGo.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cubeGo);

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("FxBlast");
                go.transform.SetParent(poolRoot, false);
                var fx = go.AddComponent<FxBlast>();
                fx.flash = Make(go, "Flash", glowMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.08f, .12f), size: new Vector2(2.2f, 2.8f), speed: Vector2.zero,
                    from: new Color(1f, .97f, .85f, 1f), to: new Color(1f, .7f, .3f, 0f), grow: 1.4f);
                fx.fire = Make(go, "Fire", flameMat != null ? flameMat : glowMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(.25f, .5f), size: new Vector2(.6f, 1.1f), speed: new Vector2(1.2f, 3.2f),
                    from: new Color(1f, .82f, .45f, 1f), to: new Color(.35f, .12f, .05f, 0f), grow: 2.2f,
                    shape: ParticleSystemShapeType.Hemisphere, radius: .25f, gravity: -.15f, spin: true);
                fx.sparks = Make(go, "Sparks", glowMat, ParticleSystemRenderMode.Stretch,
                    life: new Vector2(.3f, .8f), size: new Vector2(.03f, .06f), speed: new Vector2(5f, 12f),
                    from: new Color(1f, .9f, .55f, 1f), to: Orange * new Color(1f, 1f, 1f, 0f), grow: .6f,
                    shape: ParticleSystemShapeType.Hemisphere, radius: .1f, gravity: 1.4f);
                fx.debris = Make(go, "Debris", chunkMat, ParticleSystemRenderMode.Mesh,
                    life: new Vector2(.8f, 1.5f), size: new Vector2(.05f, .14f), speed: new Vector2(3f, 7f),
                    from: Color.gray, to: new Color(.5f, .5f, .5f, 0f), grow: .9f,
                    shape: ParticleSystemShapeType.Hemisphere, radius: .2f, gravity: 2.2f, spin3D: true);
                fx.debris.GetComponent<ParticleSystemRenderer>().mesh = cube;
                fx.dust = Make(go, "Dust", smokeMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(1.2f, 2.2f), size: new Vector2(.7f, 1.2f), speed: new Vector2(1.5f, 3.2f),
                    from: new Color(.6f, .56f, .5f, .55f), to: new Color(.6f, .58f, .54f, 0f), grow: 2.6f,
                    shape: ParticleSystemShapeType.Circle, radius: .3f, gravity: -.02f, spin: true, drag: 2.2f);
                // the dust ring rolls out along the floor: the circle lies flat
                fx.dust.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                fx.smoke = Make(go, "Smoke", smokeMat, ParticleSystemRenderMode.Billboard,
                    life: new Vector2(2.2f, 3.4f), size: new Vector2(.9f, 1.5f), speed: new Vector2(.6f, 1.6f),
                    from: new Color(.18f, .17f, .16f, .7f), to: new Color(.45f, .44f, .43f, 0f), grow: 3f,
                    shape: ParticleSystemShapeType.Hemisphere, radius: .35f, gravity: -.12f, spin: true, drag: 1.2f);
                fx.blastLight = go.AddComponent<Light>();
                fx.blastLight.type = LightType.Point;
                fx.blastLight.color = new Color(1f, .66f, .32f);
                fx.blastLight.shadows = LightShadows.None;
                fx.blastLight.enabled = false;
                fx.enabled = false;
                pool[i] = fx;
            }
        }

        internal static Gradient Fade(Color from, Color to)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                      new[] { new GradientAlphaKey(from.a, 0f), new GradientAlphaKey(from.a * .7f, .4f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // One emitter, configured once: emission off (the burst emits), world space, unscaled time; sizes / speeds scale with the rig.
        // `grow` > 1 grows over the life, < 1 shrinks. `drag` slows the particles (the dust and the smoke settle).
        internal static ParticleSystem Make(GameObject parent, string name, Material mat, ParticleSystemRenderMode mode,
            Vector2 life, Vector2 size, Vector2 speed, Color from, Color to, float grow,
            ParticleSystemShapeType shape = ParticleSystemShapeType.Sphere, float radius = 0f, float gravity = 0f,
            bool spin = false, bool spin3D = false, float drag = 0f)
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
            main.maxParticles = 64;
            if (spin) main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            if (spin3D)
            {
                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var rot = ps.rotationOverLifetime;
                rot.enabled = true; rot.separateAxes = true;
                rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
                rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
                rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            }
            var emission = ps.emission; emission.enabled = false;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = shape;
            sh.radius = Mathf.Max(.01f, radius);
            if (shape == ParticleSystemShapeType.Hemisphere) sh.rotation = new Vector3(-90f, 0f, 0f);   // (the dome opens upwards)
            if (drag > 0f)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.drag = drag;
                lim.multiplyDragByParticleSize = false;
            }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(from, to);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, grow));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (mode == ParticleSystemRenderMode.Stretch) { r.lengthScale = 2.2f; r.velocityScale = .05f; }
            return ps;
        }
    }
}
