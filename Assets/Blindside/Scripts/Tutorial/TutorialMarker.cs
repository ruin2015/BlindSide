// A destination on the tutorial's battlefield: a flat blue ring on the floor with a second ring that keeps spreading out from it and
// fading, and a thin light column rising from the middle. Unscaled clock; the meshes and materials are made once.
using UnityEngine;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialMarker : MonoBehaviour, ITimedEffect
    {
        public const float Radius = .55f, Spread = 1.9f, Period = 1.3f, BeamHeight = 2.6f;
        // a strong orange by default: the training floor is white, so it is painted (alpha blend) with a dark rim, not added as light
        public static readonly Color Destination = new Color(1f, .42f, .08f);
        Color Col = Destination;

        Transform ring, wave;
        Material ringMat, waveMat, beamMat;
        float clock;

        public float Duration => Period;

        /// <param name="color">blue (a destination) by default; the tutorial uses red for a target / a wall that stops bullets</param>
        public static TutorialMarker Create(Vector2 cell, Transform parent = null, Color? color = null)
        {
            var go = new GameObject("TutorialMarker");
            if (parent != null) go.transform.SetParent(parent, false);
            var m = go.AddComponent<TutorialMarker>();
            if (color.HasValue) m.Col = color.Value;
            m.Build();
            m.MoveTo(cell);
            return m;
        }

        public void MoveTo(Vector2 cell) => transform.position = GridSpace.GridToWorld(cell.x, cell.y, .06f);

        void Build()
        {
            // the inner disc, a dark rim round the ring (it reads on the white floor), the ring, the spreading wave, the column
            Part("Disc", RingMesh(0f, Radius - .13f, 48), new Color(Col.r, Col.g, Col.b, .38f), out _);
            Part("Rim", RingMesh(Radius, Radius + .06f, 48), new Color(.08f, .09f, .11f, .85f), out _);
            ring = Part("Ring", RingMesh(Radius - .13f, Radius, 48), Col, out ringMat).transform;
            wave = Part("Wave", RingMesh(Radius - .05f, Radius, 48), Col, out waveMat).transform;
            var beam = Part("Beam", BeamMesh(.07f, BeamHeight), Col, out beamMat);
            beam.transform.localPosition = Vector3.zero;
        }

        GameObject Part(string name, Mesh mesh, Color c, out Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            mat = new Material(shader);
            mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);   // painted (an additive light vanished on the white floor)
            mat.SetFloat("_ZWrite", 0f); mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3010;
            SetColor(mat, c);
            mr.sharedMaterial = mat;
            return go;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            float k = Mathf.Repeat(t, Period) / Period;
            float s = Mathf.Lerp(1f, Spread, 1f - (1f - k) * (1f - k));
            wave.localScale = new Vector3(s, 1f, s);
            SetColor(waveMat, new Color(Col.r, Col.g, Col.b, .85f * (1f - k)));
            float breathe = .5f + .5f * Mathf.Sin(t * 4f);
            SetColor(ringMat, new Color(Col.r, Col.g, Col.b, .85f + .15f * breathe));
            SetColor(beamMat, new Color(Col.r, Col.g, Col.b, .45f + .25f * breathe));
        }

        void OnDestroy()
        {
            foreach (var mf in GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
            foreach (var mr in GetComponentsInChildren<MeshRenderer>()) if (mr.sharedMaterial != null) Destroy(mr.sharedMaterial);
        }

        static Mesh RingMesh(float inner, float outer, int segments)
        {
            var verts = new Vector3[segments * 2]; var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = d * inner; verts[i * 2 + 1] = d * outer;
            }
            for (int i = 0, t = 0; i < segments; i++)
            {
                int i0 = i * 2, o0 = i0 + 1, i1 = ((i + 1) % segments) * 2, o1 = i1 + 1;
                tris[t++] = i0; tris[t++] = o1; tris[t++] = i1;
                tris[t++] = i0; tris[t++] = o0; tris[t++] = o1;
            }
            var mesh = new Mesh { name = "TutorialRing", vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Two crossed vertical quads, `w` wide and `h` tall, fading to nothing at the top (vertex alpha isn't read by
        /// the Unlit shader, so the fade is in the shape: the quads narrow to a point).</summary>
        static Mesh BeamMesh(float w, float h)
        {
            var verts = new Vector3[]
            {
                new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f), new Vector3(0f, h, 0f),
                new Vector3(0f, 0f, -w), new Vector3(0f, 0f, w), new Vector3(0f, h, 0f),
            };
            var mesh = new Mesh { name = "TutorialBeam", vertices = verts, triangles = new[] { 0, 2, 1, 3, 5, 4 } };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
