// A helicopter lit for a night operation (MissionAtmosphere adds it to every HeliRig at night): under the nose a searchlight (a spot
// light) aimed ahead and down, sweeping slowly from side to side, with its beam drawn as a soft cone (seen in the rain); red and green
// position lights on the left and right of the hull, a white strobe on top (a double flash every Period s) and a red beacon at the tail.
// All of it sits under the hull (HeliRig.body, so it pitches and banks with it). Evaluate(t) poses the sweep and the flashes t seconds
// into the cycle (ITimedEffect); Update runs it on the unscaled clock. No allocations per frame.
using UnityEngine;
using Blindside.Cinematic;
using Blindside.UI;

namespace Blindside.Rendering
{
    public class HeliNightLights : MonoBehaviour, ITimedEffect
    {
        public const float Period = 1.6f;            // the strobe / beacon cycle
        const float SweepPeriod = 9.6f, SweepYaw = 14f, BeamTilt = 50f;   // (Period × 6: the whole look repeats every SweepPeriod)
        const float BeamLength = 34f, BeamAngle = 34f;
        static readonly Color SearchColor = new Color(1f, .96f, .86f);
        static readonly Color Red = new Color(1f, .12f, .1f), Green = new Color(.15f, 1f, .35f), White = new Color(1f, 1f, 1f);

        Transform root, searchPivot;
        Light search, strobeLight, beaconLight;
        Renderer strobeBulb, beaconBulb;
        Material beamMat;
        readonly System.Collections.Generic.List<Object> made = new System.Collections.Generic.List<Object>();
        float t0;

        public float Duration => SweepPeriod;

        void OnEnable()
        {
            if (root == null) Build();
            root.gameObject.SetActive(true);
            t0 = Time.unscaledTime;
        }

        void OnDisable() { if (root != null) root.gameObject.SetActive(false); }

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            foreach (var o in made) if (o != null) Destroy(o);
            made.Clear();
        }

        void Update() => Evaluate(Time.unscaledTime - t0);

        public void Evaluate(float t)
        {
            if (root == null) return;
            float yaw = Mathf.Sin(t / SweepPeriod * Mathf.PI * 2f) * SweepYaw;
            searchPivot.localRotation = Quaternion.Euler(BeamTilt, yaw, 0f);
            // strobe: two short flashes at the start of each cycle; beacon: one longer pulse half a cycle later
            float c = Mathf.Repeat(t, Period);
            bool strobe = c < .05f || (c > .14f && c < .19f);
            float beacon = Mathf.Clamp01(1f - Mathf.Abs(c - Period * .5f) / .25f);
            strobeLight.intensity = strobe ? 6f : 0f;
            if (strobeBulb.enabled != strobe) strobeBulb.enabled = strobe;
            beaconLight.intensity = 3f * beacon;
            beaconBulb.enabled = beacon > .05f;
        }

        void Build()
        {
            var rig = GetComponent<HeliRig>();
            var parent = rig != null && rig.body != null ? rig.body : transform;
            root = new GameObject("NightLights").transform;
            root.SetParent(parent, false);

            // the searchlight and its beam
            searchPivot = new GameObject("Searchlight").transform;
            searchPivot.SetParent(root, false);
            searchPivot.localPosition = new Vector3(0f, -.65f, 3.1f);
            search = searchPivot.gameObject.AddComponent<Light>();
            search.type = LightType.Spot;
            search.color = SearchColor;
            search.range = 120f; search.spotAngle = BeamAngle; search.innerSpotAngle = BeamAngle * .45f;
            search.intensity = 320f;
            search.shadows = LightShadows.None;
            var beam = new GameObject("Beam");
            beam.transform.SetParent(searchPivot, false);
            var mesh = ConeMesh(BeamLength, Mathf.Tan(BeamAngle * .5f * Mathf.Deg2Rad) * BeamLength * .8f, 20);
            made.Add(mesh);
            beam.AddComponent<MeshFilter>().sharedMesh = mesh;
            var br = beam.AddComponent<MeshRenderer>();
            beamMat = BeamMaterial();
            made.Add(beamMat);
            br.sharedMaterial = beamMat;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; br.receiveShadows = false;
            // the lens itself, glowing
            Bulb(searchPivot, Vector3.zero, .32f, SearchColor);

            // position lights (steady) and the flashing ones
            Bulb(root, new Vector3(-1.15f, .15f, .4f), .14f, Red);
            Bulb(root, new Vector3(1.15f, .15f, .4f), .14f, Green);
            PointLight(root, new Vector3(-1.2f, .15f, .4f), Red, 2.5f, 1.2f);
            PointLight(root, new Vector3(1.2f, .15f, .4f), Green, 2.5f, 1.2f);
            strobeBulb = Bulb(root, new Vector3(0f, 1.95f, -1.2f), .2f, White);
            strobeLight = PointLight(root, new Vector3(0f, 2.1f, -1.2f), White, 14f, 0f);
            beaconBulb = Bulb(root, new Vector3(0f, .95f, -5.6f), .18f, Red);
            beaconLight = PointLight(root, new Vector3(0f, 1.05f, -5.6f), Red, 6f, 0f);
            Evaluate(0f);
        }

        Renderer Bulb(Transform parent, Vector3 pos, float size, Color color)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(b.GetComponent<Collider>());
            b.name = "Bulb";
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;
            b.transform.localScale = Vector3.one * size;
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color * 2.2f);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color * 2.2f);
            made.Add(m);
            var r = b.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        static Light PointLight(Transform parent, Vector3 pos, Color color, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = color; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>An open cone along +Z from the apex (v = 0) to the far rim (v = 1), its inside drawn too.</summary>
        static Mesh ConeMesh(float length, float radius, int sides)
        {
            var v = new Vector3[(sides + 1) * 2];
            var uv = new Vector2[v.Length];
            var tri = new int[sides * 6];
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                v[i * 2] = new Vector3(Mathf.Cos(a) * .12f, Mathf.Sin(a) * .12f, 0f);
                v[i * 2 + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, length);
                uv[i * 2] = new Vector2(i / (float)sides, 0f);
                uv[i * 2 + 1] = new Vector2(i / (float)sides, 1f);
            }
            for (int i = 0; i < sides; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3, k = i * 6;
                tri[k] = a; tri[k + 1] = c; tri[k + 2] = b;
                tri[k + 3] = b; tri[k + 4] = c; tri[k + 5] = d;
            }
            var m = new Mesh { name = "SearchlightBeam", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Additive, both sides, brightest at the lamp and gone by the far end (the texture's v), soft at the cone's edges.</summary>
        Material BeamMaterial()
        {
            const int w = 4, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BeamFalloff" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                byte a = (byte)(Mathf.Pow(1f - v, 2.6f) * 255);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px); tex.Apply();
            made.Add(tex);
            var src = Resources.Load<Material>("FX/FX_MuzzleGlow");
            var m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.name = "SearchlightBeam";
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            var c = new Color(SearchColor.r, SearchColor.g, SearchColor.b, .065f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            return m;
        }
    }
}
