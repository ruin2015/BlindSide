// Shared bits of the particle effects (FxEmp, FxFlash, FxShortCircuit, FxSmokeCloud): the Resources/FX materials, an untextured
// additive material for flat light shapes on the floor, and the flat meshes they use (a ring; a disc whose vertex alpha falls off to
// the rim, so a light wash has a soft edge). Built once, shared; nothing here runs per frame.
using UnityEngine;

namespace Blindside.Rendering
{
    internal static class FxKit
    {
        static Material glow, smoke, additive;
        static Mesh ring, disc;

        public static Material Glow => glow != null ? glow : (glow = Resources.Load<Material>("FX/FX_MuzzleGlow"));
        public static Material Smoke => smoke != null ? smoke : (smoke = Resources.Load<Material>("FX/FX_Smoke"));

        /// <summary>Untextured, additive (SrcAlpha, One), vertex colour × _BaseColor: light shapes laid on the floor.</summary>
        public static Material Additive
        {
            get
            {
                if (additive != null) return additive;
                var src = Smoke;
                additive = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                additive.name = "FX_AdditiveFlat";
                if (additive.HasProperty("_BaseMap")) additive.SetTexture("_BaseMap", null);
                additive.SetFloat("_Surface", 1f);
                additive.SetFloat("_Blend", 2f);
                additive.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                additive.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                additive.SetFloat("_ZWrite", 0f);
                additive.SetFloat("_Cull", 0f);
                additive.renderQueue = 3100;
                return additive;
            }
        }

        /// <summary>A flat ring (radius 1, `inner` the hole), local XZ.</summary>
        public static Mesh Ring
        {
            get
            {
                if (ring != null) return ring;
                const int seg = 96; const float inner = .9f;
                var v = new Vector3[seg * 2]; var c = new Color[seg * 2]; var tr = new int[seg * 6];
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v[i * 2] = d * inner; v[i * 2 + 1] = d;
                    c[i * 2] = new Color(1f, 1f, 1f, 0f); c[i * 2 + 1] = Color.white;   // soft inside, sharp front
                }
                for (int i = 0, t = 0; i < seg; i++)
                {
                    int i0 = i * 2, o0 = i0 + 1, i1 = ((i + 1) % seg) * 2, o1 = i1 + 1;
                    tr[t++] = i0; tr[t++] = o1; tr[t++] = i1;
                    tr[t++] = i0; tr[t++] = o0; tr[t++] = o1;
                }
                ring = new Mesh { name = "FxRing", vertices = v, colors = c, triangles = tr };
                ring.RecalculateBounds();
                return ring;
            }
        }

        /// <summary>A flat disc (radius 1), opaque at the centre and fading to nothing at the rim, local XZ.</summary>
        public static Mesh SoftDisc
        {
            get
            {
                if (disc != null) return disc;
                const int seg = 64, rings = 4;
                var v = new Vector3[1 + seg * rings]; var c = new Color[v.Length];
                var tr = new int[seg * 3 + (rings - 1) * seg * 6];
                v[0] = Vector3.zero; c[0] = Color.white;
                for (int r = 0; r < rings; r++)
                {
                    float rr = (r + 1f) / rings, a = 1f - rr; a *= a;
                    for (int i = 0; i < seg; i++)
                    {
                        float ang = i * Mathf.PI * 2f / seg;
                        v[1 + r * seg + i] = new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr);
                        c[1 + r * seg + i] = new Color(1f, 1f, 1f, a);
                    }
                }
                int t = 0;
                for (int i = 0; i < seg; i++) { tr[t++] = 0; tr[t++] = 1 + (i + 1) % seg; tr[t++] = 1 + i; }
                for (int r = 0; r < rings - 1; r++)
                    for (int i = 0; i < seg; i++)
                    {
                        int a0 = 1 + r * seg + i, a1 = 1 + r * seg + (i + 1) % seg, b0 = a0 + seg, b1 = a1 + seg;
                        tr[t++] = a0; tr[t++] = a1; tr[t++] = b0;
                        tr[t++] = a1; tr[t++] = b1; tr[t++] = b0;
                    }
                disc = new Mesh { name = "FxSoftDisc", vertices = v, colors = c, triangles = tr };
                disc.RecalculateBounds();
                return disc;
            }
        }

        /// <summary>A flat shape under `parent` drawn with the additive material (its colour set per frame through a property block).</summary>
        public static Renderer Flat(Transform parent, string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Additive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Sets a flat shape's colour (no allocation: the caller's block).</summary>
        public static void Tint(Renderer r, MaterialPropertyBlock mpb, Color c)
        {
            mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(mpb);
        }

        /// <summary>A point light, off, under `parent`.</summary>
        public static Light PointLight(Transform parent, Color color)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }
    }
}
