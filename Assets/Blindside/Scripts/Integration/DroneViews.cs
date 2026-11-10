// The drone part of syncGadgets(t) in blindside_source/03_renderer_threejs.js (~186-188):
//   body box .26×.08×.26 (0x1b2129), four rotor rings RingGeometry(.06,.1) at (±.17,.05,±.17), eye sphere r.04 at (.14,0,0);
//   ring/eye colour = d.team===G.side ? 0x7cf0c8 : 0xff3b54; the player's own drone also gets a GEO.ring ×2.5 on the floor
//   (0x7cf0c8, opacity .4 — its 2.5m sight circle). o.visible = d.alive && (d.team===G.side || cellVisible(G.side,d.x,d.y));
//   o.position = (d.rx, .34+sin(t/200)*.04, d.ry). A dead drone's group stays hidden.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Drones;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public class DroneViews : MonoBehaviour
    {
        static readonly Color Mine = new Color32(0x7c, 0xf0, 0xc8, 0xff);    // 0x7cf0c8
        static readonly Color Foe = new Color32(0xff, 0x3b, 0x54, 0xff);     // 0xff3b54
        static readonly Color Body = new Color32(0x1b, 0x21, 0x29, 0xff);    // 0x1b2129

        System.Collections.Generic.IReadOnlyList<Drone> drones;
        System.Func<float, float, bool> cellVisible;
        Transform root;
        Material unlitTemplate;

        class View { public GameObject go, ring; }
        readonly Dictionary<Drone, View> views = new Dictionary<Drone, View>();

        public void Bind(IReadOnlyList<Drone> drones, System.Func<float, float, bool> cellVisibleToPlayer)
        {
            Unbind();
            this.drones = drones; cellVisible = cellVisibleToPlayer;
        }

        public void Unbind()
        {
            drones = null; cellVisible = null;
            if (root != null) Destroy(root.gameObject);
            root = null; views.Clear();
        }

        void LateUpdate()
        {
            if (drones == null) return;
            if (root == null) { root = new GameObject("Drones").transform; root.SetParent(transform, false); }
            var side = HudLog.Side;
            float t = Time.unscaledTime * 1000f;
            foreach (var d in drones)
            {
                if (!views.TryGetValue(d, out var v)) { v = Build(d, d.team == side); views[d] = v; }
                bool show = d.alive && (d.team == side || cellVisible == null || cellVisible(d.x, d.y));
                if (v.go.activeSelf != show) v.go.SetActive(show);
                bool ringOn = show && v.ring != null;
                if (v.ring != null && v.ring.activeSelf != ringOn) v.ring.SetActive(ringOn);
                if (!show) continue;
                v.go.transform.position = GridSpace.GridToWorld(d.rx, d.ry, .34f + Mathf.Sin(t / 200f) * .04f);
                if (v.ring != null) v.ring.transform.position = GridSpace.GridToWorld(d.rx, d.ry, .05f);
            }
        }

        View Build(Drone d, bool mine)
        {
            var col = mine ? Mine : Foe;
            var go = new GameObject("Drone_" + d.id);
            go.transform.SetParent(root, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(.26f, .08f, .26f);
            var lit = new Material(Blindside.Rendering.ToonShader.Lit ?? Shader.Find("Standard"));
            if (lit.HasProperty("_BaseColor")) lit.SetColor("_BaseColor", Body);
            if (lit.HasProperty("_Color")) lit.SetColor("_Color", Body);
            body.GetComponent<MeshRenderer>().sharedMaterial = lit;

            var ringMesh = ElectricFieldView.BuildRing(.06f, .1f, 16);
            var rotorMat = Unlit(col, 3001);
            foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            {
                var r = new GameObject("Rotor");
                r.transform.SetParent(go.transform, false);
                r.transform.localPosition = new Vector3(sx * .17f, .05f, sz * .17f);
                r.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                var mr = r.AddComponent<MeshRenderer>();
                mr.sharedMaterial = rotorMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            Destroy(eye.GetComponent<Collider>());
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(.14f, 0f, 0f);
            eye.transform.localScale = Vector3.one * .08f;
            var em = eye.GetComponent<MeshRenderer>();
            em.sharedMaterial = Unlit(col, 3000);
            em.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject ring = null;
            if (mine)
            {
                ring = new GameObject("SightRing");
                ring.transform.SetParent(root, false);
                ring.AddComponent<MeshFilter>().sharedMesh = ElectricFieldView.BuildRing(.975f, 1f, 128);
                var rr = ring.AddComponent<MeshRenderer>();
                rr.sharedMaterial = Unlit(new Color(Mine.r, Mine.g, Mine.b, .4f), 3002);
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ring.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
            }
            return new View { go = go, ring = ring };
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
