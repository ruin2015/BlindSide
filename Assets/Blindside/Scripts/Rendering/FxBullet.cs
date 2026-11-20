// The rounds' effects, built from particle systems (the FxBlast way: emitters made once, emission off, Emit per shot, world space,
// materials from Resources/FX). They run on game time, so the battle's hit stop and slow motion hold and slow them with the rest:
//   Tracer(from, to)            the round in flight: a hot streak (a stretched particle) flying from the muzzle to where it goes
//   Hit(at, dir, headshot)      a round in a body: a dark-red mist puffing out along the round's way, droplets thrown on and
//                               falling; a headshot adds a white flash and more of both
//   Shield(at, dir)             a round on a raised shield: a white flash and bright sparks glancing back off the face
//   Miss(at, dir)               a round going wide into the street / a wall behind: a puff of dust, grit and a few sparks
// The spray's spread comes from each emitter's own shape (a cone along the round's direction): no UnityEngine.Random is drawn — its
// stream belongs to the combat rolls. Nothing is allocated per shot once the emitters exist.
using UnityEngine;

namespace Blindside.Rendering
{
    public static class FxBullet
    {
        const float TracerSpeed = 80f, TracerStretch = .022f;   // (slow and long enough to be seen for a few frames)

        static Transform root;
        static ParticleSystem tracer, mist, drops, flash, sparks, dust, grit;

        /// <summary>The round flying from `from` to `to` (a streak; gone when it gets there).</summary>
        public static void Tracer(Vector3 from, Vector3 to)
        {
            if (!Ready()) return;
            var d = to - from;
            float len = d.magnitude;
            if (len < .05f) return;
            var ep = new ParticleSystem.EmitParams
            {
                position = from,
                velocity = d / len * TracerSpeed,
                startLifetime = len / TracerSpeed,
                applyShapeToPosition = false,
            };
            tracer.Emit(ep, 1);
        }

        /// <summary>A round going into a body at `at`, travelling along `dir` (zero: no direction — a burst all round).</summary>
        public static void Hit(Vector3 at, Vector3 dir, bool headshot)
        {
            if (!Ready()) return;
            Burst(mist, at, dir, headshot ? 9 : 6);
            Burst(drops, at, dir, headshot ? 16 : 9);
            if (headshot) { Burst(flash, at, dir, 1); Burst(sparks, at, -dir, 6); }
        }

        /// <summary>A round stopped by a shield at `at` (`dir`: the round's way): a flash and sparks glancing back.</summary>
        public static void Shield(Vector3 at, Vector3 dir)
        {
            if (!Ready()) return;
            Burst(flash, at, -dir, 1);
            Burst(sparks, at, -dir, 18);
        }

        /// <summary>A round going wide, landing at `at` (the street, a wall behind).</summary>
        public static void Miss(Vector3 at, Vector3 dir)
        {
            if (!Ready()) return;
            Burst(dust, at, Vector3.up, 5);
            Burst(grit, at, Vector3.up - dir * .5f, 10);
            Burst(sparks, at, Vector3.up - dir, 4);
        }

        static void Burst(ParticleSystem ps, Vector3 at, Vector3 dir, int n)
        {
            // (no direction — a hit on the map, where the round's way isn't known — sprays up and out)
            var rot = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up);
            ps.transform.SetPositionAndRotation(at, rot);
            ps.Emit(n);
        }

        static bool Ready()
        {
            if (root != null) return true;
            if (!Application.isPlaying) return false;
            Build();
            return root != null;
        }

        static void Build()
        {
            var go = new GameObject("FxBullet");
            Object.DontDestroyOnLoad(go);
            root = go.transform;
            var glowMat = Resources.Load<Material>("FX/FX_MuzzleGlow");
            var smokeMat = Resources.Load<Material>("FX/FX_Smoke");

            // the round: one stretched hot particle a shot (its velocity and life set at the emit)
            tracer = FxBlast.Make(go, "Tracer", glowMat, ParticleSystemRenderMode.Stretch,
                life: new Vector2(.05f, .05f), size: new Vector2(.07f, .085f), speed: Vector2.zero,
                from: new Color(1f, .95f, .72f, 1f), to: new Color(1f, .8f, .45f, .6f), grow: 1f);
            Stretch(tracer, 1f, TracerStretch);
            GameTime(tracer);

            // a body hit: the mist (soft, dark red, swelling and settling) and droplets (small, falling)
            mist = FxBlast.Make(go, "HitMist", smokeMat, ParticleSystemRenderMode.Billboard,
                life: new Vector2(.28f, .5f), size: new Vector2(.12f, .24f), speed: new Vector2(.8f, 2.4f),
                from: new Color(.5f, .03f, .05f, .8f), to: new Color(.28f, .02f, .03f, 0f), grow: 2.6f,
                spin: true, drag: 4f);
            Cone(mist, 32f);
            GameTime(mist);
            drops = FxBlast.Make(go, "HitDrops", smokeMat, ParticleSystemRenderMode.Stretch,
                life: new Vector2(.3f, .6f), size: new Vector2(.018f, .035f), speed: new Vector2(2.2f, 5.2f),
                from: new Color(.55f, .02f, .04f, 1f), to: new Color(.3f, .01f, .02f, 0f), grow: .7f, gravity: 1.6f);
            Cone(drops, 26f);
            Stretch(drops, 1.4f, .03f);
            GameTime(drops);

            // the flash (a headshot, a shield): a short white bloom
            flash = FxBlast.Make(go, "HitFlash", glowMat, ParticleSystemRenderMode.Billboard,
                life: new Vector2(.06f, .09f), size: new Vector2(.45f, .6f), speed: Vector2.zero,
                from: new Color(1f, .97f, .9f, 1f), to: new Color(1f, .8f, .55f, 0f), grow: 1.5f);
            GameTime(flash);

            // sparks: bright streaks glancing off metal / the street
            sparks = FxBlast.Make(go, "HitSparks", glowMat, ParticleSystemRenderMode.Stretch,
                life: new Vector2(.15f, .35f), size: new Vector2(.02f, .04f), speed: new Vector2(4f, 9f),
                from: new Color(1f, .9f, .6f, 1f), to: new Color(1f, .55f, .2f, 0f), grow: .6f, gravity: 1.2f);
            Cone(sparks, 40f);
            Stretch(sparks, 1.6f, .03f);
            GameTime(sparks);

            // a miss: a dust puff and grit kicked up
            dust = FxBlast.Make(go, "MissDust", smokeMat, ParticleSystemRenderMode.Billboard,
                life: new Vector2(.5f, .9f), size: new Vector2(.18f, .32f), speed: new Vector2(.4f, 1.2f),
                from: new Color(.62f, .58f, .52f, .55f), to: new Color(.62f, .6f, .56f, 0f), grow: 2.4f,
                spin: true, drag: 2.5f);
            Cone(dust, 35f);
            GameTime(dust);
            grit = FxBlast.Make(go, "MissGrit", smokeMat, ParticleSystemRenderMode.Stretch,
                life: new Vector2(.25f, .5f), size: new Vector2(.015f, .03f), speed: new Vector2(1.8f, 4.2f),
                from: new Color(.35f, .32f, .28f, 1f), to: new Color(.35f, .32f, .28f, 0f), grow: .8f, gravity: 1.8f);
            Cone(grit, 30f);
            Stretch(grit, 1.2f, .02f);
            GameTime(grit);
        }

        // ---- the emitters' settings past FxBlast.Make ----
        static void Cone(ParticleSystem ps, float angle)
        {
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = angle;
            sh.radius = .02f;
            sh.rotation = Vector3.zero;   // (+Z of the emitter: the burst's direction)
        }

        static void Stretch(ParticleSystem ps, float length, float velocity)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.lengthScale = length; r.velocityScale = velocity;
        }

        /// <summary>On game time (FxBlast's run unscaled): the hit stop holds them, the slow motion slows them.</summary>
        static void GameTime(ParticleSystem ps)
        {
            var main = ps.main;
            main.useUnscaledTime = false;
            main.maxParticles = 256;
            // (the emitters sit wherever their last burst was — the battle stage is far off the map: never paused as "off screen")
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        }
    }
}
