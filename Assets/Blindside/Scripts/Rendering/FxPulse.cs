// A pulsing ring on the floor, self-destroying: Heartbeat (GHOST — a lub-dub, twice, then again: two quick rings per beat) and Scan
// (SILENCE — one wide ring sweeping out from the operator, a hologram ping). One ring mesh is shared by every pulse; each pulse owns a
// material copy (its colour / alpha). Unscaled time; nothing is allocated per frame.
using UnityEngine;

namespace Blindside.Rendering
{
    public class FxPulse : MonoBehaviour
    {
        static Mesh ring;
        static Material baseMat;

        Material mat;
        Color color;
        float t, delay, period, life, from, to;
        int rings;
        bool lubDub;

        /// <summary>A heartbeat at `pos` (grid → world already): `beats` lub-dubs, the rings growing to `size`.</summary>
        public static void Heartbeat(Vector3 pos, Color color, int beats = 3, float size = .9f, float delay = 0f)
            => Spawn(pos, color, beats * 2, .6f, .45f, .15f, size, delay, true);

        /// <summary>A scan ring sweeping from the centre out to `radius` (`rings` of them, one after another).</summary>
        public static void Scan(Vector3 pos, Color color, float radius, int rings = 2, float delay = 0f)
            => Spawn(pos, color, rings, .35f, .9f, .2f, radius, delay, false);

        static void Spawn(Vector3 pos, Color color, int rings, float period, float life, float from, float to, float delay, bool lubDub)
        {
            Ensure();
            var go = new GameObject("FxPulse");
            go.transform.position = pos + Vector3.up * .06f;
            var p = go.AddComponent<FxPulse>();
            p.color = color; p.rings = rings; p.period = period; p.life = life; p.from = from; p.to = to; p.delay = delay; p.lubDub = lubDub;
            p.mat = new Material(baseMat);
            p.Build();
        }

        static void Ensure()
        {
            if (ring == null)
            {
                const int N = 64;
                ring = new Mesh { name = "FxPulseRing" };
                var v = new Vector3[N * 2]; var tri = new int[N * 6];
                for (int i = 0; i < N; i++)
                {
                    float a = i * Mathf.PI * 2f / N, c = Mathf.Cos(a), s = Mathf.Sin(a);
                    v[i * 2] = new Vector3(c * .9f, 0f, s * .9f);
                    v[i * 2 + 1] = new Vector3(c, 0f, s);
                    int j = (i + 1) % N, k = i * 6;
                    tri[k] = i * 2; tri[k + 1] = j * 2; tri[k + 2] = i * 2 + 1;
                    tri[k + 3] = j * 2; tri[k + 4] = j * 2 + 1; tri[k + 5] = i * 2 + 1;
                }
                ring.vertices = v; ring.triangles = tri; ring.RecalculateBounds();
            }
            if (baseMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                baseMat = new Material(shader) { name = "FxPulse" };
                baseMat.SetFloat("_Surface", 1f);
                baseMat.SetFloat("_Blend", 0f);
                baseMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                baseMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);   // additive: it glows
                baseMat.SetFloat("_ZWrite", 0f);
                baseMat.SetFloat("_Cull", 0f);
                baseMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                baseMat.renderQueue = 3100;
            }
        }

        Transform[] rts;

        void Build()
        {
            rts = new Transform[rings];
            for (int i = 0; i < rings; i++)
            {
                var r = new GameObject("Ring" + i);
                r.transform.SetParent(transform, false);
                r.AddComponent<MeshFilter>().sharedMesh = ring;
                var mr = r.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                r.SetActive(false);
                rts[i] = r.transform;
            }
            SetColor(0f);
        }

        void SetColor(float a)
        {
            var c = new Color(color.r, color.g, color.b, a);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }

        float RingStart(int i)
        {
            // a heartbeat: the two rings of a beat close together (lub-dub), the beats `period` × 2 apart
            if (lubDub) return delay + (i / 2) * period * 2f + (i % 2) * period * .35f;
            return delay + i * period;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float alpha = 0f;
            for (int i = 0; i < rts.Length; i++)
            {
                float p = (t - RingStart(i)) / life;
                bool on = p >= 0f && p < 1f;
                if (rts[i].gameObject.activeSelf != on) rts[i].gameObject.SetActive(on);
                if (!on) continue;
                float e = 1f - (1f - p) * (1f - p);
                float s = Mathf.Lerp(from, to, e);
                rts[i].localScale = new Vector3(s, 1f, s);
                alpha = Mathf.Max(alpha, 1f - p);
            }
            SetColor(alpha);   // (the rings share the material: the brightest one sets it)
            if (t > RingStart(rts.Length - 1) + life) { Destroy(mat); Destroy(gameObject); }
        }
    }
}
