// The shock-wire parts of the renderer in blindside_source/03_renderer_threejs.js:
//   syncGadgets' electric fields (~194-198): per G.electrics entry a disc (0x4fb3ff, opacity .07) + ring
//     (GEO.ring .975-1, opacity .8) scaled to e.r, a small node box (.26×.2×.26, 0x1b3550 / emissive 0x0a3a6a)
//     and a 6-point spark line (0x9fd8ff) re-rolled on ~35% of frames; visible when G.side==='def'||e.discovered.
//   cellMesh's electrified band (~108): a 1.06×.07×1.06 box at h*.55 over each electrified wall cell, 0x4fb3ff
//     opacity .85, shown when G.side==='def'||p.elecSeen — and syncGadgets' flicker (~213):
//     opacity=.35+.6*|sin(t/70+x)|.
// p.elecSeen is set by WallDiscovery (an attacker's sight on the panel); e.discovered by VisibilityController / DroneOps.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;
using Blindside.Data;

namespace Blindside.Integration
{
    public class ElectricFieldView : MonoBehaviour
    {
        static readonly Color Zap = new Color32(0x4f, 0xb3, 0xff, 0xff);      // 0x4fb3ff
        static readonly Color Spark = new Color32(0x9f, 0xd8, 0xff, 0xff);    // 0x9fd8ff
        static readonly Color Node = new Color32(0x1b, 0x35, 0x50, 0xff);     // 0x1b3550 (+ emissive 0x0a3a6a)

        List<ElectricField> fields;
        MapGrid map;
        Transform root;
        Material unlitTemplate;

        class FieldGo { public GameObject go; public LineRenderer spark; public Material ring; public FxShortCircuit shorted; public bool off; }
        readonly Dictionary<ElectricField, FieldGo> fieldGos = new Dictionary<ElectricField, FieldGo>();
        readonly List<ElectricField> gone = new List<ElectricField>();
        readonly Dictionary<Vector2Int, Renderer> bands = new Dictionary<Vector2Int, Renderer>();
        MaterialPropertyBlock mpb;

        public void Bind(List<ElectricField> fields, MapGrid map)
        {
            Unbind();
            this.fields = fields; this.map = map;
        }

        public void Unbind()
        {
            fields = null; map = null;
            if (root != null) Destroy(root.gameObject);
            root = null;
            fieldGos.Clear(); bands.Clear();
        }

        static bool ShownToSide => HudLog.Side == OperatorTeam.Defense;   // G.side==='def' (discovery not ported)

        void LateUpdate()
        {
            if (fields == null || map == null) return;
            EnsureRoot();
            bool show = ShownToSide;
            float t = Time.unscaledTime * 1000f;

            foreach (var e in fields)
            {
                if (!fieldGos.TryGetValue(e, out var f)) { f = BuildField(e); fieldGos[e] = f; }
                bool shown = show || e.discovered;   // o.visible = G.side==='def' || e.discovered
                f.go.SetActive(shown);
                // knocked out (SILENCE's EMP): no field — its ring goes dim, the arcs stop, the node shorts out until it comes back on
                bool off = e.disabledLeft > 0;
                if (f.off != off)
                {
                    f.off = off;
                    f.spark.enabled = !off;
                    var rc = new Color(Zap.r, Zap.g, Zap.b, off ? .2f : .8f);
                    if (f.ring.HasProperty("_BaseColor")) f.ring.SetColor("_BaseColor", rc);
                    if (f.ring.HasProperty("_Color")) f.ring.SetColor("_Color", rc);
                }
                f.shorted.SetOn(off);
                if (shown && !off && Random.value < .35f) RollSpark(f.spark, e.r);
            }
            // a field shot out (DroneOps.DestroyGadget took it off the list): its disc and ring go too
            if (fieldGos.Count > fields.Count)
            {
                gone.Clear();
                foreach (var kv in fieldGos) if (!fields.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (var e in gone) { if (fieldGos[e].go != null) Destroy(fieldGos[e].go); fieldGos.Remove(e); }
            }

            // electrified bands: one per wall cell of an electrified, still-standing panel
            var want = new HashSet<Vector2Int>();
            foreach (var p in map.Panels)
            {
                if (!p.electrified || !(show || p.elecSeen)) continue;   // G.side==='def'||p.elecSeen
                foreach (var c in p.cells) if (map.TileAt(c.x, c.y) == TileType.Wall) want.Add(c);
            }
            foreach (var c in want) if (!bands.ContainsKey(c)) bands[c] = BuildBand(c);
            if (mpb == null) mpb = new MaterialPropertyBlock();
            foreach (var kv in bands)
            {
                bool on = want.Contains(kv.Key);
                if (kv.Value.gameObject.activeSelf != on) kv.Value.gameObject.SetActive(on);
                if (!on) continue;
                float a = .35f + .6f * Mathf.Abs(Mathf.Sin(t / 70f + kv.Key.x));
                var col = new Color(Zap.r, Zap.g, Zap.b, a);
                mpb.SetColor("_BaseColor", col); mpb.SetColor("_Color", col);
                kv.Value.SetPropertyBlock(mpb);
            }
        }

        void EnsureRoot()
        {
            if (root != null) return;
            root = new GameObject("ElectricFields").transform;
            root.SetParent(transform, false);
        }

        FieldGo BuildField(ElectricField e)
        {
            var go = new GameObject("ElectricField");
            go.transform.SetParent(root, false);
            go.transform.position = GridSpace.GridToWorld(e.x, e.y, 0f);

            var disc = Flat("Disc", go.transform, BuildDisc(64), new Color(Zap.r, Zap.g, Zap.b, .07f), 3001);
            disc.transform.localPosition = new Vector3(0f, .05f, 0f);
            disc.transform.localScale = new Vector3(e.r, e.r, e.r);
            var ring = Flat("Ring", go.transform, BuildRing(.975f, 1f, 128), new Color(Zap.r, Zap.g, Zap.b, .8f), 3002);
            ring.transform.localPosition = new Vector3(0f, .055f, 0f);
            ring.transform.localScale = new Vector3(e.r, e.r, e.r);

            var nd = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nd.name = "Node";
            Destroy(nd.GetComponent<Collider>());
            nd.transform.SetParent(go.transform, false);
            nd.transform.localPosition = new Vector3(0f, .1f, 0f);
            nd.transform.localScale = new Vector3(.26f, .2f, .26f);
            var nr = nd.GetComponent<MeshRenderer>();
            var nm = new Material(nr.sharedMaterial);
            if (nm.HasProperty("_BaseColor")) nm.SetColor("_BaseColor", Node);
            if (nm.HasProperty("_Color")) nm.SetColor("_Color", Node);
            if (nm.HasProperty("_EmissionColor")) { nm.EnableKeyword("_EMISSION"); nm.SetColor("_EmissionColor", new Color32(0x0a, 0x3a, 0x6a, 0xff)); }
            nr.sharedMaterial = nm;

            var lgo = new GameObject("Spark");
            lgo.transform.SetParent(go.transform, false);
            var lr = lgo.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 6;
            lr.widthMultiplier = .025f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = UnlitMat(Spark, 3003);
            RollSpark(lr, e.r);
            var shorted = FxShortCircuit.Attach(go.transform, new Vector3(0f, .14f, 0f), 1.1f, e.GetHashCode());
            return new FieldGo { go = go, spark = lr, ring = ring.GetComponent<MeshRenderer>().sharedMaterial, shorted = shorted };
        }

        // pts: a=random angle; for i<6: r=i/5*e.r → (cos a·r, .12+rand·.25, sin a·r); a+=(rand-.5)*.8
        static void RollSpark(LineRenderer lr, float r)
        {
            float a = Random.value * 6.28f;
            for (int i = 0; i < 6; i++)
            {
                float rr = i / 5f * r;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * rr, .12f + Random.value * .25f, Mathf.Sin(a) * rr));
                a += (Random.value - .5f) * .8f;
            }
        }

        Renderer BuildBand(Vector2Int c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Electrified_{c.x}_{c.y}";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            var b = WallBounds(c);
            // The wall blocks sink below the floor (bounds ≈ y -.6….6), so h is the part above it.
            float bottom = Mathf.Max(b.min.y, 0f);
            go.transform.position = new Vector3(b.center.x, bottom + (b.max.y - bottom) * .55f, b.center.z);
            go.transform.localScale = new Vector3(b.size.x * 1.06f, .07f, b.size.z * 1.06f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterial = UnlitMat(new Color(Zap.r, Zap.g, Zap.b, .85f), 3004);
            return mr;
        }

        /// <summary>World bounds of Blindside_MapRoot/Walls/Wall_Wood_{x}_{y}, or a unit cube on the cell.</summary>
        static Bounds WallBounds(Vector2Int c)
        {
            var go = GameObject.Find($"Blindside_MapRoot/Walls/Wall_Wood_{c.x}_{c.y}");
            var rs = go != null ? go.GetComponentsInChildren<Renderer>(true) : null;
            if (rs == null || rs.Length == 0) return new Bounds(GridSpace.GridToWorld(c.x, c.y, .5f), Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        GameObject Flat(string name, Transform parent, Mesh mesh, Color color, int queue)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterial = UnlitMat(color, queue);
            return go;
        }

        // Same transparent unlit recipe as PlayerSelectionController.MakeMeshObject (double-sided, no z-write).
        Material UnlitMat(Color color, int queue)
        {
            if (unlitTemplate == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                unlitTemplate = new Material(shader);
                unlitTemplate.SetFloat("_Surface", 1f);
                unlitTemplate.SetFloat("_Blend", 0f);
                unlitTemplate.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                unlitTemplate.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                unlitTemplate.SetFloat("_ZWrite", 0f);
                unlitTemplate.SetFloat("_Cull", 0f);
                unlitTemplate.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            var m = new Material(unlitTemplate);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            m.renderQueue = queue;
            return m;
        }

        /// <summary>CircleGeometry(1,segments) laid flat (local XZ).</summary>
        internal static Mesh BuildDisc(int segments)
        {
            var verts = new Vector3[segments + 1]; var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                tris[i * 3] = 0; tris[i * 3 + 1] = 1 + (i + 1) % segments; tris[i * 3 + 2] = 1 + i;
            }
            var mesh = new Mesh { name = "Disc", vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>RingGeometry(inner,outer,segments) laid flat (local XZ).</summary>
        internal static Mesh BuildRing(float inner, float outer, int segments)
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
            var mesh = new Mesh { name = "Ring", vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
