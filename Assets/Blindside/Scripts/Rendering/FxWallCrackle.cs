// A wall carrying current (VOLT's shock wire, ElectricFieldView): every so often it crackles — a jagged blue-white arc runs across
// one face of the wall, sparks spit off it and fall, and a short blue blink lights the floor. The bursts come at irregular intervals
// from the cell's own System.Random (the game's random streams are never touched), so neighbouring walls never fire together.
// One per wall cell, switched by SetOn. The arc's flicker is ITimedEffect.Evaluate(t); unscaled time; nothing allocated per frame.
using Blindside.UI;
using UnityEngine;

namespace Blindside.Rendering
{
    public class FxWallCrackle : MonoBehaviour, ITimedEffect
    {
        static readonly Color ArcCol = new Color(.55f, .82f, 1f, 1f);
        public const float MinGap = .18f, MaxGap = .9f, DoubleChance = .35f;

        ParticleSystem sparks;
        LineRenderer arc;
        Light blink;
        System.Random rng;
        readonly Vector3[] pts = new Vector3[9];
        Vector3 half;                   // the wall block's half size above the floor (local)
        float clock, burstAt = -10f, nextBurst;
        bool on;

        /// <summary>One crackle's flicker (the arc and the light), seconds.</summary>
        public float Duration => .16f;
        public bool On => on;

        /// <summary>A crackle rig for the wall block `b` (world bounds; the part above the floor is used). Off until SetOn.</summary>
        public static FxWallCrackle Attach(Transform parent, Bounds b, int seed)
        {
            float bottom = Mathf.Max(b.min.y, 0f), top = b.max.y;
            var go = new GameObject("FxWallCrackle");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(b.center.x, (bottom + top) * .5f, b.center.z);
            var fx = go.AddComponent<FxWallCrackle>();
            fx.half = new Vector3(b.extents.x, Mathf.Max(.05f, (top - bottom) * .5f), b.extents.z);
            fx.rng = new System.Random(seed);
            fx.Build();
            fx.enabled = false;
            return fx;
        }

        public void SetOn(bool value)
        {
            if (on == value) return;
            on = value;
            enabled = value;
            if (value) nextBurst = clock + (float)rng.NextDouble() * MaxGap;
            else { arc.enabled = false; blink.enabled = false; }
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
            // now and then a second snap right after the first
            nextBurst = clock + (rng.NextDouble() < DoubleChance ? .07f : MinGap + (float)rng.NextDouble() * (MaxGap - MinGap));

            // the arc: across one face (the long side), at a random height, jagged, a little off the surface
            bool alongX = half.x >= half.z;
            float side = rng.NextDouble() < .5 ? -1f : 1f;
            float off = (alongX ? half.z : half.x) + .03f;
            float y0 = Lerp(-.7f, .8f), y1 = Lerp(-.7f, .8f);
            float span = (alongX ? half.x : half.z) * Lerp(.55f, .95f), c = (alongX ? half.x : half.z) * Lerp(-.3f, .3f);
            for (int i = 0; i < pts.Length; i++)
            {
                float k = i / (pts.Length - 1f);
                float along = c - span + 2f * span * k;
                float y = Mathf.Lerp(y0, y1, k) * half.y;
                if (i > 0 && i < pts.Length - 1) { y += Lerp(-.22f, .22f); along += Lerp(-.05f, .05f); }
                float out1 = off + (i > 0 && i < pts.Length - 1 ? Lerp(0f, .06f) : 0f);
                pts[i] = alongX ? new Vector3(along, y, side * out1) : new Vector3(side * out1, y, along);
            }
            arc.SetPositions(pts);

            // sparks spit off the arc's middle, out from the face
            var mid = pts[pts.Length / 2];
            sparks.transform.localPosition = mid;
            sparks.transform.localRotation = Quaternion.LookRotation(alongX ? new Vector3(0f, .35f, side) : new Vector3(side, .35f, 0f));
            sparks.Emit(6 + rng.Next(10));
            blink.transform.localPosition = mid;
        }

        float Lerp(float a, float b) => a + (b - a) * (float)rng.NextDouble();

        /// <summary>`time` seconds after a crackle: the arc thins out and the blink goes off by Duration.</summary>
        public void Evaluate(float time)
        {
            float k = Mathf.Clamp01(1f - time / Duration);
            bool live = on && k > 0f;
            if (arc.enabled != live) arc.enabled = live;
            if (blink.enabled != live) blink.enabled = live;
            if (!live) return;
            // a flicker inside the flash: thick / thin on alternate frames of its life
            float flick = ((int)(time * 60f) & 1) == 0 ? 1f : .55f;
            arc.widthMultiplier = .045f * (.35f + .65f * k) * flick;
            blink.intensity = 3.5f * k * flick;
        }

        void Build()
        {
            sparks = FxBlast.Make(gameObject, "Sparks", FxKit.Glow, ParticleSystemRenderMode.Stretch,
                life: new Vector2(.15f, .4f), size: new Vector2(.02f, .045f), speed: new Vector2(1.2f, 3.2f),
                from: new Color(.9f, .97f, 1f, 1f), to: new Color(.35f, .7f, 1f, 0f), grow: .5f,
                shape: ParticleSystemShapeType.Cone, radius: .02f, gravity: 1.4f);
            var sh = sparks.shape; sh.angle = 38f;
            var pm = sparks.main; pm.maxParticles = 48;

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

            blink = FxKit.PointLight(transform, new Color(.5f, .78f, 1f));
            blink.range = 2.2f;
        }
    }
}
