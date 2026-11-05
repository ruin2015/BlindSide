// The gas / camera / mirror parts of the renderer in blindside_source/03_renderer_threejs.js:
//   syncGadgets' cams (~190-193): pole (cyl r.035 h.9, 0x2a3038) + head box (.24×.15×.18 at y.95, 0x3d4652) + red LED
//     (sph .05 at (.13,.97,0), blinking sin(t/200)>0); defense also gets a GEO.ring ×3.2 (0xff6b8a, opacity .35);
//     rotation.y=sin(t/1500+c.x)*.8; visible when c.alive&&(G.side==='def'||c.discovered||cellVisible(G.side,c)).
//   syncGadgets' gas (~200-202): 9 spheres (0x8cff5a, opacity .16) — one centred, eight on a ring of g.r*.55,
//     y .55+rand*.3, scale sc×sc*.6×sc with sc=.75+rand*.35; the group spins rotation.y=t/4000; removed with the cloud.
//   cellMesh's mirror panel (~100, 109): col 0x0f3a4a, transparent, opacity .42 for Defense / .88 for Attack,
//     emissive 0x04303c, metalness .6, roughness .12 — plus a 0x2fe4ff strip (1.03×.04×1.03) on the top.
// c.discovered (attack-side discovery) isn't ported: an attack-side player sees a camera only while its cell is in sight.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;
using Blindside.Vision;

namespace Blindside.Integration
{
    public class GadgetViews : MonoBehaviour
    {
        static readonly Color GasGreen = new Color32(0x8c, 0xff, 0x5a, 0xff);
        static readonly Color Pole = new Color32(0x2a, 0x30, 0x38, 0xff);
        static readonly Color Head = new Color32(0x3d, 0x46, 0x52, 0xff);
        static readonly Color Led = new Color32(0xff, 0x3b, 0x54, 0xff);
        static readonly Color CamRing = new Color32(0xff, 0x6b, 0x8a, 0xff);
        static readonly Color MirrorGlass = new Color32(0x0f, 0x3a, 0x4a, 0xff);
        static readonly Color MirrorEmissive = new Color32(0x04, 0x30, 0x3c, 0xff);
        static readonly Color MirrorStrip = new Color32(0x2f, 0xe4, 0xff, 0xff);

        AbilityContext ctx;
        IReadOnlyList<SimUnit> units;
        Transform root;
        Material unlitTemplate, mirrorMat;

        class CamGo { public GameObject go; public Transform head; public GameObject led; }
        readonly Dictionary<SurveillanceCamera, CamGo> cams = new Dictionary<SurveillanceCamera, CamGo>();
        readonly Dictionary<GasCloud, GameObject> gases = new Dictionary<GasCloud, GameObject>();
        readonly Dictionary<Vector2Int, GameObject> mirrorStrips = new Dictionary<Vector2Int, GameObject>();

        public void Bind(AbilityContext ctx, IReadOnlyList<SimUnit> units)
        {
            Unbind();
            this.ctx = ctx; this.units = units;
        }

        public void Unbind()
        {
            ctx = null; units = null;
            if (root != null) Destroy(root.gameObject);
            root = null;
            cams.Clear(); gases.Clear(); mirrorStrips.Clear();
            mirrorMat = null;   // re-made per match: its opacity depends on the side
        }

        static OperatorTeam Side => HudLog.Side;

        void LateUpdate()
        {
            if (ctx == null) return;
            if (root == null) { root = new GameObject("Gadgets").transform; root.SetParent(transform, false); }
            float t = Time.unscaledTime * 1000f;
            SyncCams(t);
            SyncGas(t);
            SyncMirrors();
        }

        // ---------------- cams ----------------
        void SyncCams(float t)
        {
            bool def = Side == OperatorTeam.Defense;
            foreach (var c in ctx.Cameras)
            {
                if (!cams.TryGetValue(c, out var o)) { o = BuildCam(c, def); cams[c] = o; }
                bool show = c.alive && (def || c.discovered || CellSeenByAttack(c.x, c.y));
                if (o.go.activeSelf != show) o.go.SetActive(show);
                if (!show) continue;
                o.go.transform.rotation = Quaternion.Euler(0f, -Mathf.Sin(t / 1500f + c.x) * .8f * Mathf.Rad2Deg, 0f);
                o.led.SetActive(Mathf.Sin(t / 200f) > 0f);
            }
        }

        // cellVisible('atk', c): some living attacker's fovCells holds the camera's cell (no drones deploy yet).
        bool CellSeenByAttack(float x, float y)
        {
            if (units == null || ctx.Vision == null) return false;
            int cx = Mathf.FloorToInt(x + .5f), cy = Mathf.FloorToInt(y + .5f), k = cy * MapGrid.Width + cx;
            foreach (var u in units)
                if (u.Alive && u.Team == OperatorTeam.Attack && ctx.Vision.SeesPoint(u, cx, cy, exceptCell: k)) return true;
            return false;
        }

        CamGo BuildCam(SurveillanceCamera c, bool def)
        {
            var go = new GameObject(c.placed ? "Camera_Placed" : "Camera_Fixed");
            go.transform.SetParent(root, false);
            go.transform.position = GridSpace.GridToWorld(c.x, c.y, 0f);
            var pole = Prim(PrimitiveType.Cylinder, "Pole", go.transform, Lit(Pole));
            pole.transform.localPosition = new Vector3(0f, .45f, 0f);
            pole.transform.localScale = new Vector3(.07f, .45f, .07f);   // Unity cylinder: 2 tall, 1 wide
            var head = Prim(PrimitiveType.Cube, "Head", go.transform, Lit(Head));
            head.transform.localPosition = new Vector3(0f, .95f, 0f);
            head.transform.localScale = new Vector3(.24f, .15f, .18f);
            var led = Prim(PrimitiveType.Sphere, "Led", go.transform, Unlit(Led, 3000));
            led.transform.localPosition = new Vector3(.13f, .97f, 0f);
            led.transform.localScale = Vector3.one * .1f;                // Unity sphere: diameter 1
            if (def)
            {
                var ring = Flat("Range", go.transform, ElectricFieldView.BuildRing(.975f, 1f, 128), Unlit(new Color(CamRing.r, CamRing.g, CamRing.b, .35f), 3002));
                ring.transform.localPosition = new Vector3(0f, .05f, 0f);
                ring.transform.localScale = new Vector3(3.2f, 3.2f, 3.2f);
            }
            return new CamGo { go = go, head = head.transform, led = led };
        }

        // ---------------- gas ----------------
        void SyncGas(float t)
        {
            foreach (var g in ctx.Gases)
            {
                if (!gases.TryGetValue(g, out var o)) { o = BuildGas(g); gases[g] = o; }
                o.transform.rotation = Quaternion.Euler(0f, -(t / 4000f) * Mathf.Rad2Deg, 0f);
            }
            List<GasCloud> gone = null;
            foreach (var kv in gases) if (!ctx.Gases.Contains(kv.Key)) (gone ??= new List<GasCloud>()).Add(kv.Key);
            if (gone != null) foreach (var g in gone) { Destroy(gases[g]); gases.Remove(g); }
        }

        GameObject BuildGas(GasCloud g)
        {
            var o = new GameObject("GasCloud");
            o.transform.SetParent(root, false);
            o.transform.position = GridSpace.GridToWorld(g.x, g.y, 0f);
            var mat = Unlit(new Color(GasGreen.r, GasGreen.g, GasGreen.b, .16f), 3005);
            for (int i = 0; i < 9; i++)
            {
                var s = Prim(PrimitiveType.Sphere, "Puff", o.transform, mat);
                float a = i / 9f * 6.28f, r = i > 0 ? g.r * .55f : 0f;
                s.transform.localPosition = new Vector3(Mathf.Cos(a) * r, .55f + Random.value * .3f, -Mathf.Sin(a) * r);
                float sc = (.75f + Random.value * .35f) * 2f;          // SphereGeometry(1) → Unity sphere (diameter 1)
                s.transform.localScale = new Vector3(sc, sc * .6f, sc);
                s.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return o;
        }

        // ---------------- mirrors ----------------
        void SyncMirrors()
        {
            var map = ctx.Map;
            foreach (var p in map.Panels)
            {
                if (!p.mirror) continue;
                foreach (var c in p.cells)
                {
                    if (mirrorStrips.ContainsKey(c)) continue;
                    MapWallSync.SetMainMaterial(c, MirrorMat());
                    mirrorStrips[c] = BuildStrip(c);
                }
            }
            // A mirror panel that broke (breakPanel clears p.mirror): the block is hidden by MapWallSync; drop the strip.
            List<Vector2Int> gone = null;
            foreach (var kv in mirrorStrips)
            {
                var pp = map.PanelAt(kv.Key.x, kv.Key.y);
                if (!pp.HasValue || !pp.Value.mirror) (gone ??= new List<Vector2Int>()).Add(kv.Key);
            }
            if (gone != null) foreach (var c in gone) { Destroy(mirrorStrips[c]); mirrorStrips.Remove(c); MapWallSync.SetMainMaterial(c, null); }
        }

        GameObject BuildStrip(Vector2Int c)
        {
            if (!MapWallSync.TryGetBlockBounds(c, out var b)) b = new Bounds(GridSpace.GridToWorld(c.x, c.y, .5f), Vector3.one);
            var s = Prim(PrimitiveType.Cube, $"MirrorStrip_{c.x}_{c.y}", root, Unlit(MirrorStrip, 3001));
            s.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
            s.transform.localScale = new Vector3(b.size.x * 1.03f, .04f, b.size.z * 1.03f);
            s.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return s;
        }

        Material MirrorMat()
        {
            if (mirrorMat != null) return mirrorMat;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = "MirrorPanel" };
            float a = Side == OperatorTeam.Defense ? .42f : .88f;
            var col = new Color(MirrorGlass.r, MirrorGlass.g, MirrorGlass.b, a);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", .6f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .88f);   // roughness .12
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", MirrorEmissive); }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mirrorMat = m;
        }

        // ---------------- helpers ----------------
        static GameObject Prim(PrimitiveType type, string name, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Flat(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Material Lit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        Material Unlit(Color color, int queue)
        {
            if (unlitTemplate == null)
            {
                unlitTemplate = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
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
    }
}
