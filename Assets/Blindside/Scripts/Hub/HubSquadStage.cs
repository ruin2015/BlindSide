// The sortie's squad screen's 3D lineup (HubSquadScreen): the operators going out standing in a shallow wedge on a hangar floor at dusk —
// the leader (FLARE, slot 0) at the point, the others staggered back to its left and right — seen from a low camera looking up at them
// (the lineup's hero shot). Built at runtime far below the HQ (y −320) with its own camera into a RenderTexture, like the armoury
// (HubArmoryStage); the camera renders only while the screen is open.
// Each figure is the battle's own (the unit prefab + UnitView.ApplyModel, its idle, its carried gun); previews (edit mode) stand the model
// at its idle's first frame. Each slot has a plate with a lit edge (the chosen slot's bright). The camera's opening move eases out
// (fast, then settling — ITimedEffect.Evaluate(t)); nothing is allocated per frame.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Hub
{
    public class HubSquadStage : MonoBehaviour, ITimedEffect
    {
        public static readonly Vector3 Origin = new Vector3(0f, -320f, 0f);
        public const int MaxSlots = 5;
        // the wedge: the leader at the point, the rest staggered back (room space)
        static readonly Vector3[] Spots =
        {
            new Vector3(0f, 0f, 0f), new Vector3(-1.2f, 0f, .6f), new Vector3(1.2f, 0f, .6f), new Vector3(-2.35f, 0f, 1.2f), new Vector3(2.35f, 0f, 1.2f),
        };
        // the low camera: knee height, looking up at the chests (further back for a wider squad)
        const float EyeHeight = .28f, LookHeight = 1.42f, EnterBack = 1.1f, EnterDrop = .12f;
        public const float EnterTime = 1.2f;

        Camera cam;
        RenderTexture rt;
        int slots = 3, chosen = -1;
        readonly UnitView[] figures = new UnitView[MaxSlots];
        readonly GameObject[] statics = new GameObject[MaxSlots];
        readonly string[] keys = new string[MaxSlots];
        readonly Renderer[] edges = new Renderer[MaxSlots];
        Material edgeOn, edgeOff;
        float clock = 99f;
        Vector3 eye, look;

        public RenderTexture Texture => rt;
        public Camera View => cam;
        public float Duration => EnterTime;

        public static HubSquadStage Build()
        {
            var go = new GameObject("HubSquadStage");
            go.transform.position = Origin;
            var s = go.AddComponent<HubSquadStage>();
            s.BuildRoom();
            return s;
        }

        // ---- the room: a hangar at dusk -------------------------------------------------------------------------------------------
        void BuildRoom()
        {
            var concrete = Mat(new Color(.085f, .088f, .095f), .3f);
            var wall = Mat(new Color(.06f, .065f, .075f), .2f);
            var rib = Mat(new Color(.04f, .043f, .05f), .4f);
            var steel = Mat(new Color(.17f, .18f, .2f), .55f);
            var stripe = Mat(new Color(.55f, .42f, .1f), .2f);
            var crate = Mat(new Color(.12f, .11f, .08f), .15f);

            Box("DecoFloor", new Vector3(0f, -.05f, 2f), new Vector3(16f, .1f, 16f), concrete);
            Box("DecoBackWall", new Vector3(0f, 4f, 6.2f), new Vector3(16f, 8f, .2f), wall);
            Box("DecoLeftWall", new Vector3(-7.5f, 4f, 2f), new Vector3(.2f, 8f, 16f), wall);
            Box("DecoRightWall", new Vector3(7.5f, 4f, 2f), new Vector3(.2f, 8f, 16f), wall);
            Box("DecoCeiling", new Vector3(0f, 6.4f, 2f), new Vector3(16f, .1f, 16f), rib);
            // the hangar door: open a crack onto the dusk outside (a warm glow behind the squad)
            Box("DecoDoorL", new Vector3(-3.6f, 2.6f, 6.05f), new Vector3(6.6f, 5.2f, .12f), steel);
            Box("DecoDoorR", new Vector3(3.6f, 2.6f, 6.05f), new Vector3(6.6f, 5.2f, .12f), steel);
            Box("DecoDusk", new Vector3(0f, 2.6f, 6.3f), new Vector3(.9f, 5.2f, .02f), Unlit(new Color(1f, .55f, .28f)));
            for (int i = 0; i < 12; i++)
            {
                float x = -6.6f + i * 1.2f;
                if (Mathf.Abs(x) < .6f) continue;
                Box("DecoDoorRib", new Vector3(x, 2.6f, 5.97f), new Vector3(.07f, 5.2f, .05f), rib);
            }
            // roof trusses and strip lamps (the low camera sees the ceiling)
            for (int i = 0; i < 4; i++)
            {
                float z = -1f + i * 2.1f;
                Box("DecoTruss", new Vector3(0f, 6.1f, z), new Vector3(15f, .18f, .14f), rib);
                Box("DecoStripLamp", new Vector3(-2.2f, 5.9f, z + .5f), new Vector3(1.6f, .05f, .12f), Unlit(new Color(.85f, .92f, 1f)));
                Box("DecoStripLamp", new Vector3(2.2f, 5.9f, z + .5f), new Vector3(1.6f, .05f, .12f), Unlit(new Color(.85f, .92f, 1f)));
            }
            Box("DecoHazard", new Vector3(0f, .005f, -1.6f), new Vector3(9f, .01f, .14f), stripe);
            Box("DecoHazard", new Vector3(0f, .005f, 3.4f), new Vector3(9f, .01f, .14f), stripe);
            // crates and a rack at the sides
            Box("DecoCrate", new Vector3(-5.2f, .45f, 3.3f), new Vector3(.9f, .9f, .9f), crate);
            Box("DecoCrate", new Vector3(-5.1f, 1.2f, 3.35f), new Vector3(.65f, .6f, .65f), crate);
            Box("DecoCrate", new Vector3(5.3f, .4f, 2.8f), new Vector3(1f, .8f, .7f), crate);
            Box("DecoRack", new Vector3(6.2f, 1.5f, 4.4f), new Vector3(1.6f, 3f, .6f), steel);

            edgeOn = Unlit(new Color(.25f, .72f, 1f));
            edgeOff = Unlit(new Color(.1f, .2f, .3f));
            for (int i = 0; i < MaxSlots; i++)
            {
                var p = Spots[i];
                var plate = Prim(PrimitiveType.Cylinder, "DecoPlate", new Vector3(p.x, .025f, p.z), new Vector3(1.15f, .025f, 1.15f), steel);
                plate.receiveShadows = true;
                edges[i] = Prim(PrimitiveType.Cylinder, "DecoPlateEdge", new Vector3(p.x, .002f, p.z), new Vector3(1.22f, .002f, 1.22f), edgeOff);
                plate.transform.SetParent(edges[i].transform, true);   // (shown and hidden with its edge)
            }

            // lights: a hard key from above-front, the dusk's warm rim from the door, a cool fill from the low camera's side
            var key = AddLight("Key", LightType.Spot, new Vector3(.8f, 5.2f, -3.2f), new Color(1f, .95f, .88f), 14f, 12f);
            key.spotAngle = 70f; key.innerSpotAngle = 40f; key.shadows = LightShadows.Soft;
            key.transform.LookAt(transform.TransformPoint(new Vector3(0f, .9f, .6f)));
            var rim = AddLight("Rim", LightType.Spot, new Vector3(0f, 3.2f, 5.4f), new Color(1f, .6f, .32f), 16f, 10f);
            rim.spotAngle = 80f;
            rim.transform.LookAt(transform.TransformPoint(new Vector3(0f, 1.3f, .4f)));
            AddLight("Fill", LightType.Point, new Vector3(-1.5f, 1f, -3.5f), new Color(.35f, .5f, .85f), 2.2f, 8f);
            AddLight("Practical", LightType.Point, new Vector3(-5f, 2.4f, 3f), new Color(1f, .7f, .4f), 1.6f, 4f);

            var cgo = new GameObject("SquadCamera");
            cgo.transform.SetParent(transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.fieldOfView = 40f; cam.nearClipPlane = .05f; cam.farClipPlane = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.02f, .022f, .028f);
            rt = new RenderTexture(1920, 1080, 24) { name = "SquadRT", antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.enabled = false;
        }

        Material Mat(Color c, float smooth)
        {
            var m = new Material(ToonShader.Lit ?? Shader.Find("Standard"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        static Material Unlit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        Renderer Box(string name, Vector3 pos, Vector3 size, Material m) => Prim(PrimitiveType.Cube, name, pos, size, m);

        Renderer Prim(PrimitiveType type, string name, Vector3 pos, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Kill(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = size;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            return r;
        }

        Light AddLight(string name, LightType type, Vector3 pos, Color c, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = pos;
            l.type = type; l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ---- the lineup -----------------------------------------------------------------------------------------------------------
        /// <summary>How many slots stand (3 … 5): the camera frames that many.</summary>
        public void SetSlots(int n)
        {
            slots = Mathf.Clamp(n, 1, MaxSlots);
            for (int i = slots; i < MaxSlots; i++) Show(i, null);
            for (int i = 0; i < MaxSlots; i++) edges[i].gameObject.SetActive(i < slots);
            Frame(out eye, out look);
            Place(eye, look);
        }

        /// <summary>Slot `slot` holds `op` (null: empty) — the same operator again: nothing changes.</summary>
        public void Show(int slot, OperatorData op)
        {
            if (slot < 0 || slot >= MaxSlots) return;
            string k = op != null ? op.key : null;
            if (keys[slot] == k && (figures[slot] != null || statics[slot] != null || k == null)) return;
            if (figures[slot] != null) Kill(figures[slot].gameObject);
            if (statics[slot] != null) Kill(statics[slot]);
            figures[slot] = null; statics[slot] = null; keys[slot] = k;
            if (op == null) return;
            if (!Application.isPlaying) { statics[slot] = Static(op, slot); return; }
            var prefab = UnitPrefab();
            if (prefab == null) return;
            var go = Instantiate(prefab, transform, false);
            go.name = "SquadFigure_" + op.key;
            go.transform.localPosition = Spots[slot];
            var uv = go.GetComponent<UnitView>();
            if (uv == null) { Kill(go); return; }
            uv.ApplyModel(op.modelPrefab, op.animationProfile);
            uv.SetIdentity(true, false);
            uv.SetOverheadVisible(false);
            uv.SetSelected(false);
            uv.SetCombatReady(false);
            uv.SetWeapon(Loadout.Of(op, true));
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "Ring" || t.name == "SelectionRing" || t.name == "FacingIndicator")
                    foreach (var r in t.GetComponents<Renderer>()) r.enabled = false;
            uv.SetFacing(FacingOf(slot) + StanceTurn * Mathf.Deg2Rad);
            figures[slot] = uv;
            groundAt[slot] = 2;   // (stood on its plate once its idle has posed it: Ground)
        }

        // the battle's figure stands a little above its root (the battle floor's height): once its idle has run a couple of frames, its
        // lowest skinned vertex is put on the plate (measured once per figure, into one reused mesh)
        readonly int[] groundAt = new int[MaxSlots];
        // the battle idle stands bladed (the rifle across, the chest to its left of the facing): turned this much more so it squares up
        const float StanceTurn = 40f;
        Mesh bake;
        const float PlateTop = .05f;

        void Ground(int slot)
        {
            var uv = figures[slot];
            if (uv == null || uv.ModelRoot == null) return;
            if (bake == null) bake = new Mesh();
            float min = float.MaxValue;
            foreach (var s in uv.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                s.BakeMesh(bake, true);
                var vs = bake.vertices;
                var m = s.transform.localToWorldMatrix;
                for (int i = 0; i < vs.Length; i++) { float y = m.MultiplyPoint3x4(vs[i]).y; if (y < min) min = y; }
            }
            if (min == float.MaxValue) return;
            var p = uv.transform.localPosition;
            p.y -= (min - transform.position.y) - PlateTop;
            uv.transform.localPosition = p;
        }

        /// <summary>The chosen slot's plate lit (−1: none).</summary>
        public void Choose(int slot)
        {
            chosen = slot;
            for (int i = 0; i < MaxSlots; i++) edges[i].sharedMaterial = i == chosen ? edgeOn : edgeOff;
        }

        // each one turned to the camera, a little outward (the wedge opens toward it)
        float FacingOf(int slot)
        {
            var d = CamPos() - transform.TransformPoint(Spots[slot]);
            d.y = 0f;
            d = Quaternion.Euler(0f, Spots[slot].x * -4f, 0f) * d;
            return Mathf.Atan2(-d.z, d.x);
        }

        Vector3 CamPos() => transform.TransformPoint(eye);

        /// <summary>Where slot `slot` stands (world): the screen hangs its name plate under it.</summary>
        public Vector3 SlotFoot(int slot) => transform.TransformPoint(Spots[Mathf.Clamp(slot, 0, MaxSlots - 1)]);

        /// <summary>Slot `slot`'s head (world): the screen's slot number rides over it.</summary>
        public Vector3 SlotHead(int slot) => transform.TransformPoint(Spots[Mathf.Clamp(slot, 0, MaxSlots - 1)] + Vector3.up * 1.95f);

        GameObject Static(OperatorData op, int slot)
        {
            if (op.modelPrefab == null) return null;
            var go = Instantiate(op.modelPrefab, transform, false);
            go.name = "SquadFigure_" + op.key;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Kill(c);
            var t = go.transform;
            var prof = op.animationProfile;
            if (prof != null && prof.idle != null) prof.idle.SampleAnimation(go, 0f);   // (sampling moves the root: first)
            t.localPosition = Spots[slot];
            var d = CamPos() - t.position; d.y = 0f;
            t.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, Spots[slot].x * -4f, 0f) * d, Vector3.up);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float want = prof != null ? prof.modelHeight : 1.75f;
                if (b.size.y > 1e-3f) t.localScale *= want / b.size.y;
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                t.position += Vector3.up * (transform.position.y - b.min.y);
            }
            return go;
        }

        static GameObject UnitPrefab()
        {
            var r = BuildAssetRegistry.UnitPrefab;
            if (r != null) return r;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blindside/Prefabs/Units/Unit_Simple.prefab");
#else
            return null;
#endif
        }

        // ---- the camera ----------------------------------------------------------------------------------------------------------
        // low in front, far enough back for the whole wedge (three: closer), looking up at the chests; the lineup a little above the
        // middle (the plates and their names under it)
        void Frame(out Vector3 e, out Vector3 l)
        {
            float back = slots >= 5 ? 5f : slots == 4 ? 4.6f : 4.4f;
            float depth = slots >= 4 ? .55f : .3f;
            // the camera slides left so the lineup stands right of the middle (the roster covers the left)
            float pan = slots >= 4 ? -.5f : -.55f;
            e = new Vector3(pan, EyeHeight, depth - back);
            l = new Vector3(pan, LookHeight, depth);
        }

        public bool FitScreen(int w, int h)
        {
            if (rt != null && rt.width == w && rt.height == h) return false;
            if (w < 16 || h < 16) return false;
            var old = rt;
            rt = new RenderTexture(w, h, 24) { name = "SquadRT", antiAliasing = 4 };
            cam.targetTexture = rt;
            if (old != null) { old.Release(); Kill(old); }
            return true;
        }

        public void SetLive(bool on) { if (cam != null) cam.enabled = on; }

        public void RenderNow() { if (cam != null) cam.Render(); }

        /// <summary>The screen opening: the camera pushes in and rises a little to its low shot.</summary>
        public void PlayEntrance()
        {
            clock = UIMotionSettings.reduceMotion ? 99f : 0f;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            float k = HubArmoryStage.EaseOut(Mathf.Clamp01(t / EnterTime));
            Frame(out eye, out look);
            var from = eye + new Vector3(0f, -EnterDrop, -EnterBack);
            Place(Vector3.LerpUnclamped(from, eye, k), look);
        }

        void Place(Vector3 e, Vector3 l)
        {
            if (cam == null) return;
            cam.transform.localPosition = e;
            cam.transform.localRotation = Quaternion.LookRotation(l - e, Vector3.up);
        }

        void Update()
        {
            for (int i = 0; i < MaxSlots; i++)
                if (groundAt[i] > 0 && --groundAt[i] == 0) Ground(i);
            if (clock >= EnterTime) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); Kill(rt); }
            if (bake != null) Kill(bake);
        }
    }
}
