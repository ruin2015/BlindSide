// A smoke grenade's screen (スモーク弾): the canister on the floor hissing out a jet for its first seconds, an opening burst that
// billows out to the screen's edge and slows to a stop, then a cloud kept up for as long as the screen lasts — big soft puffs that
// roll slowly (noise), turn, swell and thin out, in a few greys so it doesn't read as one flat colour. When the screen ends,
// Dissipate stops it feeding and the last puffs thin away before the object goes. Particles on unscaled time; nothing allocated
// per frame; no UnityEngine.Random (the game's rolls share it) — the particle systems roll their own.
using UnityEngine;

namespace Blindside.Rendering
{
    public class FxSmokeCloud : MonoBehaviour
    {
        ParticleSystem billow, burst, hiss;
        float age;
        bool ending, hissing = true;

        const float HissSeconds = 2.6f, LastPuff = 8f;

        /// <summary>A screen of `radius` metres centred at `floor` (on the floor), under `parent`.</summary>
        public static FxSmokeCloud Create(Transform parent, Vector3 floor, float radius)
        {
            var go = new GameObject("SmokeCloud");
            go.transform.SetParent(parent, false);
            go.transform.position = floor;
            var fx = go.AddComponent<FxSmokeCloud>();
            fx.Build(Mathf.Max(.8f, radius));
            return fx;
        }

        /// <summary>The screen is over: no more smoke; the cloud thins out and the object goes once the last puff has.</summary>
        public void Dissipate()
        {
            if (ending) return;
            ending = true;
            billow.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            hiss.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, LastPuff);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            if (hissing && age >= HissSeconds)
            {
                hissing = false;
                hiss.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void Build(float r)
        {
            var smokeMat = FxKit.Smoke;
            var light = new Color(.8f, .82f, .85f, 1f);
            var dark = new Color(.62f, .64f, .68f, 1f);

            // the cloud kept up: puffs over the whole screen, low, rolling slowly
            billow = FxBlast.Make(gameObject, "Billow", smokeMat, ParticleSystemRenderMode.Billboard,
                life: new Vector2(5.5f, 8f), size: new Vector2(r * .9f, r * 1.35f), speed: new Vector2(.03f, .18f),
                from: light, to: light, grow: 1.35f,
                shape: ParticleSystemShapeType.Circle, radius: r * .78f, gravity: -.004f, spin: true, drag: .5f);
            billow.transform.localPosition = new Vector3(0f, .55f, 0f);
            billow.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var bm = billow.main;
            bm.loop = true;
            bm.maxParticles = 140;
            bm.startColor = new ParticleSystem.MinMaxGradient(dark, light);
            var be = billow.emission; be.enabled = true; be.rateOverTime = 15f;
            var bc = billow.colorOverLifetime; bc.color = Puff(.82f);
            var bn = billow.noise; bn.enabled = true; bn.strength = .35f; bn.frequency = .22f; bn.scrollSpeed = .08f; bn.quality = ParticleSystemNoiseQuality.Medium;
            var br = billow.rotationOverLifetime; br.enabled = true; br.z = new ParticleSystem.MinMaxCurve(-.35f, .35f);
            var bv = billow.velocityOverLifetime; bv.enabled = true; bv.space = ParticleSystemSimulationSpace.World;
            bv.x = new ParticleSystem.MinMaxCurve(0f, 0f); bv.y = new ParticleSystem.MinMaxCurve(.02f, .09f); bv.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            billow.Play();

            // the opening burst: thrown out from the canister, stopped by drag at the screen's edge
            burst = FxBlast.Make(gameObject, "Burst", smokeMat, ParticleSystemRenderMode.Billboard,
                life: new Vector2(5f, 7.5f), size: new Vector2(r * .6f, r * 1.0f), speed: new Vector2(r * 1.1f, r * 2.2f),
                from: light, to: light, grow: 1.9f,
                shape: ParticleSystemShapeType.Hemisphere, radius: .2f, gravity: -.01f, spin: true, drag: 1.6f);
            var um = burst.main; um.maxParticles = 48; um.startColor = new ParticleSystem.MinMaxGradient(dark, light);
            var uc = burst.colorOverLifetime; uc.color = Puff(.85f);
            var un = burst.noise; un.enabled = true; un.strength = .3f; un.frequency = .25f; un.scrollSpeed = .1f;
            var ush = burst.shape; ush.rotation = new Vector3(-90f, 0f, 0f); ush.arc = 360f; ush.radiusThickness = 1f;
            burst.Emit(40);

            // the canister's jet: a thin white stream, fast, widening as it goes, for the first seconds
            hiss = FxBlast.Make(gameObject, "Hiss", smokeMat, ParticleSystemRenderMode.Billboard,
                life: new Vector2(.7f, 1.2f), size: new Vector2(.12f, .22f), speed: new Vector2(1.4f, 2.6f),
                from: new Color(.92f, .93f, .95f, .75f), to: new Color(.85f, .86f, .88f, 0f), grow: 6f,
                shape: ParticleSystemShapeType.Cone, radius: .03f, gravity: -.02f, spin: true, drag: 1.1f);
            hiss.transform.localPosition = new Vector3(0f, .06f, 0f);
            var hsh = hiss.shape; hsh.angle = 22f; hsh.rotation = new Vector3(-90f, 0f, 0f);
            var hm = hiss.main; hm.loop = true; hm.maxParticles = 60;
            var he = hiss.emission; he.enabled = true; he.rateOverTime = 28f;
            hiss.Play();

            // the canister itself, lying on the floor
            var can = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            can.name = "Canister";
            Destroy(can.GetComponent<Collider>());
            can.transform.SetParent(transform, false);
            can.transform.localPosition = new Vector3(0f, .045f, 0f);
            can.transform.localRotation = Quaternion.Euler(0f, 35f, 90f);
            can.transform.localScale = new Vector3(.09f, .11f, .09f);
            var mr = can.GetComponent<MeshRenderer>();
            var shader = ToonShader.Lit != null ? ToonShader.Lit : mr.sharedMaterial.shader;
            var m = new Material(shader) { name = "Canister" };
            var col = new Color(.2f, .23f, .2f, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            mr.sharedMaterial = m;
        }

        // a puff: in quickly, holding most of its life, thinning out at the end
        static Gradient Puff(float alpha)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, .12f), new GradientAlphaKey(alpha * .85f, .7f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        void OnDestroy()
        {
            var mr = transform.Find("Canister");
            if (mr != null) { var r = mr.GetComponent<MeshRenderer>(); if (r != null && r.sharedMaterial != null) Destroy(r.sharedMaterial); }
        }
    }
}
