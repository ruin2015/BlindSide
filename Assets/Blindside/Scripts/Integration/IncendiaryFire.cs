// IGNIS's co-op rank 8 (焼夷爆薬): the thermite charge leaves the ground around the breached wall burning for two of the enemy's turns.
//   an enemy standing in the fire when its side's phase begins, or walking into it (MovementSystem.OnStep), burns — BurnDamage, once
//   per fire and enemy turn; the kill is IGNIS's (MatchBootstrap.BindCoop's Burn).
//   The fire ticks down when IGNIS's side's phase begins: lit in its phase → burns in the enemy's next two → out.
// Drawn for both sides: a scorched disc, flickering flames and a warm light (ITimedEffect.Evaluate(t), unscaled time, no per-frame GC).
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public class IncendiaryFire : MonoBehaviour, ITimedEffect
    {
        public const int BurnDamage = 15, Turns = 2;
        public const float Radius = 1.6f;
        const int Flames = 14;

        static readonly Color FlameOuter = new Color(1f, .45f, .10f, .7f);
        static readonly Color FlameCore = new Color(1f, .86f, .36f, .95f);
        static readonly Color Ember = new Color(.55f, .14f, .03f, .38f);
        static readonly Color LightColor = new Color(1f, .55f, .2f);

        class Zone
        {
            public float x, y;
            public OperatorTeam owner;
            public IAbilityUnit by;
            public int ttl;
            public readonly List<SimUnit> hit = new List<SimUnit>(4);   // burned in this enemy turn
            public GameObject go;
            public Transform[] flames;
            public float[] phase, height;
            public Light light;
        }

        readonly List<Zone> zones = new List<Zone>();
        IReadOnlyList<SimUnit> units;
        TurnManager turns;
        Transform root;
        Material outerMat, coreMat, emberMat;
        float clock;

        /// <summary>Burns `e` (the fire lit by `by`): damage, popup, log, the kill's credit. Set by MatchBootstrap.BindCoop.</summary>
        public System.Action<SimUnit, IAbilityUnit, int> Burn;

        public int Count => zones.Count;
        public float Duration => 2f;

        public void Bind(IReadOnlyList<SimUnit> units, TurnManager turns)
        {
            Unbind();
            this.units = units;
            this.turns = turns;
            if (turns != null) turns.OnPhaseBegin += OnPhaseBegin;
        }

        public void Unbind()
        {
            if (turns != null) turns.OnPhaseBegin -= OnPhaseBegin;
            turns = null; units = null;
            foreach (var z in zones) if (z.go != null) Destroy(z.go);
            zones.Clear();
        }

        /// <summary>The thermite went off at (x, y): the ground there catches fire.</summary>
        public void Ignite(IAbilityUnit by, float x, float y)
        {
            if (by == null) return;
            var z = new Zone { x = x, y = y, owner = by.Team, by = by, ttl = Turns };
            Build(z);
            zones.Add(z);
            SfxNoise.Boom();
        }

        /// <summary>MovementSystem.OnStep: a unit has just moved a step — an enemy of a fire walking into it burns.</summary>
        public void Step(ITurnUnit tu)
        {
            var u = tu as SimUnit;
            if (u == null || !u.Alive || zones.Count == 0) return;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (u.Team == z.owner || z.hit.Contains(u) || !Inside(z, u)) continue;
                z.hit.Add(u);
                Burn?.Invoke(u, z.by, BurnDamage);
                if (!u.Alive) { turns?.CheckWin(); return; }
            }
        }

        void OnPhaseBegin(MatchPhase phase)
        {
            if (phase != MatchPhase.Attack && phase != MatchPhase.Defense) return;
            var acting = TurnManager.ActingTeam(phase);
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                var z = zones[i];
                if (acting == z.owner)
                {
                    // IGNIS's side again: one burning turn used up
                    if (--z.ttl > 0) continue;
                    if (z.go != null) Destroy(z.go);
                    zones.RemoveAt(i);
                    continue;
                }
                // the enemy's turn: whoever stands in it burns (TurnManager's CheckWin runs right after the phase begins)
                z.hit.Clear();
                if (units == null) continue;
                for (int k = 0; k < units.Count; k++)
                {
                    var u = units[k];
                    if (!u.Alive || u.Captive || u.Team == z.owner || !Inside(z, u)) continue;
                    z.hit.Add(u);
                    Burn?.Invoke(u, z.by, BurnDamage);
                }
            }
        }

        static bool Inside(Zone z, SimUnit u) => (u.X - z.x) * (u.X - z.x) + (u.Y - z.y) * (u.Y - z.y) <= Radius * Radius;

        // ---------------- the view ----------------
        void Update()
        {
            if (zones.Count == 0) return;
            clock += Time.unscaledDeltaTime;
            if (clock >= Duration) clock -= Duration;
            Evaluate(clock);
        }

        /// <summary>Flicker: each flame's height and the light's intensity, on a 2 s loop (every frequency a whole number of periods).</summary>
        public void Evaluate(float t)
        {
            const float w = Mathf.PI;   // 2π / Duration
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z.flames == null) continue;
                float life = z.ttl >= Turns ? 1f : .7f;   // the last turn burns lower
                for (int k = 0; k < z.flames.Length; k++)
                {
                    float p = z.phase[k];
                    float h = .8f + .22f * Mathf.Sin(w * 7f * t + p) + .1f * Mathf.Sin(w * 15f * t + p * 1.7f);
                    float sy = z.height[k] * h * life, sx = .5f * life * (1.05f - .1f * h);
                    var f = z.flames[k];
                    f.localScale = new Vector3(sx, sy, sx);
                    var lp = f.localPosition; lp.y = sy * .5f; f.localPosition = lp;
                }
                if (z.light != null)
                    z.light.intensity = (2.4f + .5f * Mathf.Sin(w * 9f * t) + .3f * Mathf.Sin(w * 21f * t + 1.3f)) * life;
            }
        }

        void Build(Zone z)
        {
            if (root == null) { root = new GameObject("IncendiaryFires").transform; root.SetParent(transform, false); }
            if (outerMat == null) { outerMat = Transparent(FlameOuter, 3010); coreMat = Transparent(FlameCore, 3011); emberMat = Transparent(Ember, 3004); }
            var go = new GameObject("IncendiaryFire");
            go.transform.SetParent(root, false);
            go.transform.position = GridSpace.GridToWorld(z.x, z.y, 0f);
            z.go = go;

            var disc = Prim(PrimitiveType.Cylinder, "DecoEmber", go.transform, emberMat);
            disc.localPosition = new Vector3(0f, .02f, 0f);
            disc.localScale = new Vector3(Radius * 2f, .004f, Radius * 2f);

            z.flames = new Transform[Flames];
            z.phase = new float[Flames];
            z.height = new float[Flames];
            for (int k = 0; k < Flames; k++)
            {
                // spread on a sunflower spiral (no random draws: the combat's random stream is left alone)
                float r = Radius * .8f * Mathf.Sqrt((k + .5f) / Flames), a = k * 2.39996f;
                var f = Prim(PrimitiveType.Sphere, "DecoFlame", go.transform, outerMat);
                f.localPosition = new Vector3(Mathf.Cos(a) * r, .3f, Mathf.Sin(a) * r);
                var core = Prim(PrimitiveType.Sphere, "DecoFlameCore", f, coreMat);
                core.localPosition = new Vector3(0f, -.12f, 0f);
                core.localScale = new Vector3(.55f, .6f, .55f);
                z.flames[k] = f;
                z.phase[k] = k * 1.91f;
                z.height[k] = .75f + .35f * Mathf.Repeat(k * .618f, 1f) * (1f - .5f * r / Radius);
            }

            var lgo = new GameObject("FireLight");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = new Vector3(0f, .9f, 0f);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = LightColor;
            l.range = 4.5f;
            l.intensity = 2.4f;
            l.shadows = LightShadows.None;
            z.light = l;
        }

        static Transform Prim(PrimitiveType type, string name, Transform parent, Material m)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = name;
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(parent, false);
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return p.transform;
        }

        static Material Transparent(Color c, int queue)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = queue;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        void OnDestroy() => Unbind();
    }
}
