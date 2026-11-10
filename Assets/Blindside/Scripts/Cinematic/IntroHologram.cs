// The intro's tactical hologram: the target area as a blue holographic plan, shown between the wide shot and the low angle.
// Built on its own layer (Hologram) and seen by its own camera, which follows the main camera and renders into a RenderTexture
// that IntroHoloScreen wipes in; the real world keeps rendering underneath until the wipe has covered it.
//   map       every renderer of the battle map (walls, crates, floor) as a glowing wireframe, built up from the ground
//   town      the surrounding buildings and streets, faint
//   ground    a world grid, drawn outward from the map, a pulse ring travelling out
//   frames    three white rectangles stacked over the map, drifting up (the reference's floating planes)
//   hostage   an amber ring + beam + turning diamond and a 人質 / HOSTAGE label, where the hostage is held — or, given targets
//             (the rescue intro), one such marker per target in its own colour and label, each driven in at its own time
// Begin builds it, Tick(t) poses it t seconds after Begin (game time, the intro Timeline's clock), End destroys it.
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public class IntroHologram
    {
        public const string LayerName = "Hologram";
        const string MatDir = "Holo/";   // Assets/Blindside/Resources/Holo

        // reveal timings (s after Begin): the wipe takes ~.95, the build-up follows
        const float GridRevealAt = .35f, GridRevealTime = 1.6f, MapRevealAt = .7f, MapRevealTime = 1.3f, TownRevealAt = .9f, TownRevealTime = 2f;
        const float WallTop = 3.5f, TownTop = 40f;
        // the hostage marker: nothing until the camera has closed in (SlamAt, s after Begin — IntroCameraRig settles its push-in
        // just before), then it is driven down from SlamHeight above like a stake (ease-in), the beam drawn down with it; on impact
        // one flash (FlashTime) and the ring blasts out to ShockRadius (ShockTime) before settling into its pulse
        public const float SlamAt = 4.65f, SlamTime = .45f, SlamHeight = 12f, FlashTime = .45f, ShockTime = .8f, ShockRadius = 9f;
        public static float ImpactAt => SlamAt + SlamTime;

        GameObject root;
        Camera cam;
        RenderTexture rt;
        int layer;
        Material mapMat, floorMat, townMat, streetMat, gridMat, lineMat, markMat;
        readonly List<Transform> frames = new List<Transform>();
        readonly List<float> frameBase = new List<float>();
        /// <summary>A marker to drive in: where, its colour (the default: the material's amber), its label, when (s after Begin).</summary>
        public struct Target
        {
            public Vector3 pos;
            public Color color;
            public string label;
            public float slamAt;
        }

        sealed class Marker
        {
            public Vector3 pos;
            public float slamAt;
            public Transform ring, diamond, label;
            public LineRenderer ringLr, beamLr, diamondLr;
            public TMP_Text labelText;
            public Material mat;
            public Color matBase, labelColor;
        }
        readonly List<Marker> markers = new List<Marker>(2);
        Vector3 hostage;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RevealId = Shader.PropertyToID("_Reveal"), RevealBaseId = Shader.PropertyToID("_RevealBase"),
            CenterId = Shader.PropertyToID("_Center"), AlphaId = Shader.PropertyToID("_Alpha");

        public Texture Texture => rt;
        public Vector3 HostagePoint => hostage;
        /// <summary>Marker `i`'s place (world) and whether it is down yet at `t` (s after Begin).</summary>
        public int MarkerCount => markers.Count;
        public Vector3 MarkerPoint(int i) => markers[i].pos;
        public float MarkerImpact(int i) => markers[i].slamAt + SlamTime;
        public bool Built => root != null;

        public bool Begin(Transform mapRoot, Transform town, Transform streets, Vector3 hostageWorld, Camera main, IList<Target> targets = null)
        {
            End();
            layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0 || main == null) return false;
            hostage = hostageWorld;
            mapMat = Load("Mat_Holo_Map"); floorMat = Load("Mat_Holo_MapFloor"); townMat = Load("Mat_Holo_Town");
            streetMat = Load("Mat_Holo_Street"); gridMat = Load("Mat_Holo_Grid"); lineMat = Load("Mat_Holo_Line"); markMat = Load("Mat_Holo_Marker");
            if (mapMat == null || gridMat == null) return false;

            root = new GameObject("IntroHologram");
            var mapBounds = new Bounds(hostage, Vector3.one);
            bool haveMap = false;
            if (mapRoot != null)
                foreach (var r in mapRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    // only what the current map shows: blocks / floor cells another map (the tutorial's, an edited one) left
                    // behind stay inactive under the same roots and would draw a second map into the hologram
                    if (!r.gameObject.activeInHierarchy) continue;
                    bool floor = r.transform.parent != null && r.transform.parent.name == "Floor";
                    Clone(mf, floor ? floorMat : mapMat, "Map");
                    if (!haveMap) { mapBounds = r.bounds; haveMap = true; } else mapBounds.Encapsulate(r.bounds);
                }
            if (town != null)
            {
                // only each building's most detailed LOD (the others would stack up in the additive blend)
                var lower = new HashSet<Renderer>();
                foreach (var lg in town.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = lg.GetLODs();
                    for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r != null) lower.Add(r);
                }
                foreach (var mf in town.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r = mf.GetComponent<MeshRenderer>();
                    if (mf.sharedMesh != null && r != null && !lower.Contains(r)) Clone(mf, townMat, "Town");
                }
            }
            if (streets != null)
                foreach (var mf in streets.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) Clone(mf, streetMat, "Street");

            // ground grid
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(ground.GetComponent<Collider>());
            ground.name = "HoloGround"; ground.layer = layer;
            ground.transform.SetParent(root.transform, false);
            ground.transform.SetPositionAndRotation(new Vector3(mapBounds.center.x, .02f, mapBounds.center.z), Quaternion.Euler(90f, 0f, 0f));
            ground.transform.localScale = new Vector3(1400f, 1400f, 1f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = gridMat;
            gridMat.SetVector(CenterId, new Vector4(mapBounds.center.x, mapBounds.center.z, 0f, 0f));

            // frames over the map
            for (int i = 0; i < 3; i++)
            {
                float pad = 4f + i * 6f, y = 6f + i * 4.5f;
                var f = Loop("HoloFrame" + i, new[]
                {
                    new Vector3(mapBounds.min.x - pad, 0f, mapBounds.min.z - pad), new Vector3(mapBounds.max.x + pad, 0f, mapBounds.min.z - pad),
                    new Vector3(mapBounds.max.x + pad, 0f, mapBounds.max.z + pad), new Vector3(mapBounds.min.x - pad, 0f, mapBounds.max.z + pad),
                }, .12f, lineMat, new Color(1f, 1f, 1f, .55f - i * .12f));
                f.position = new Vector3(0f, y, 0f);
                frames.Add(f); frameBase.Add(y);
            }

            // the hostage(s)
            if (targets == null || targets.Count == 0)
                AddMarker(new Target { pos = hostage, color = default, label = "人質 <size=60%>HOSTAGE</size>", slamAt = SlamAt });
            else foreach (var tg in targets) AddMarker(tg);

            // camera → RenderTexture, following the main camera
            rt = new RenderTexture(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), 24, RenderTextureFormat.ARGB32) { name = "IntroHologramRT" };
            var cgo = new GameObject("HoloCam");
            cgo.transform.SetParent(main.transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.004f, .02f, .055f, 1f);
            cam.cullingMask = 1 << layer;
            cam.nearClipPlane = .3f; cam.farClipPlane = 3000f;
            cam.fieldOfView = main.fieldOfView;
            cam.targetTexture = rt;
            main.cullingMask &= ~(1 << layer);

            SetReveal(0f);
            return true;
        }

        static readonly Vector3[] Circle = MakeCircle();
        static Vector3[] MakeCircle()
        {
            var c = new Vector3[48];
            for (int i = 0; i < c.Length; i++) { float a = i * Mathf.PI * 2f / c.Length; c[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }
            return c;
        }

        void AddMarker(Target tg)
        {
            var m = new Marker { pos = tg.pos, slamAt = tg.slamAt };
            // its own material (its colour, its flash); the default keeps the material's amber
            m.mat = new Material(markMat);
            if (tg.color.a > 0f) m.mat.SetColor(ColorId, new Color(tg.color.r, tg.color.g, tg.color.b, 1f) * 1.6f);
            m.matBase = m.mat.GetColor(ColorId);
            m.labelColor = tg.color.a > 0f ? tg.color : new Color(1f, .78f, .3f, 1f);
            var p = tg.pos;
            m.ring = Loop("HostageRing", Circle, .08f, m.mat, Color.white);
            m.ring.position = p + Vector3.up * .06f;
            m.ringLr = m.ring.GetComponent<LineRenderer>();
            m.beamLr = Line("HostageBeam", new[] { p + Vector3.up * 14f, p + Vector3.up * 14f }, .06f, m.mat, new Color(1f, 1f, 1f, .8f)).GetComponent<LineRenderer>();
            m.diamond = Loop("HostageDiamond", new[] { new Vector3(0f, .7f, 0f), new Vector3(.45f, 0f, 0f), new Vector3(0f, -.7f, 0f), new Vector3(-.45f, 0f, 0f) }, .07f, m.mat, Color.white);
            m.diamond.position = p + Vector3.up * 3.2f;
            m.diamondLr = m.diamond.GetComponent<LineRenderer>();
            m.label = MakeLabel(p + Vector3.up * 4.6f, tg.label, m.labelColor);
            m.labelText = m.label.GetComponent<TMP_Text>();
            markers.Add(m);
        }

        static Material Load(string name)
        {
            var m = Resources.Load<Material>(MatDir + name);
            return m != null ? new Material(m) : null;   // instances: the reveal values are animated
        }

        void Clone(MeshFilter src, Material mat, string kind)
        {
            var go = new GameObject("Holo" + kind, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = layer;
            var t = go.transform;
            t.SetParent(root.transform, false);
            t.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            t.localScale = src.transform.lossyScale;
            go.GetComponent<MeshFilter>().sharedMesh = src.sharedMesh;
            var mr = go.GetComponent<MeshRenderer>();
            int subs = src.sharedMesh.subMeshCount;
            var mats = new Material[subs];
            for (int i = 0; i < subs; i++) mats[i] = mat;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        Transform Loop(string name, Vector3[] pts, float width, Material mat, Color c) => MakeLine(name, pts, width, mat, c, true);
        Transform Line(string name, Vector3[] pts, float width, Material mat, Color c) => MakeLine(name, pts, width, mat, c, false);

        Transform MakeLine(string name, Vector3[] pts, float width, Material mat, Color c, bool loop)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(root.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = !loop;
            lr.loop = loop;
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
            lr.widthMultiplier = width;
            lr.sharedMaterial = mat;
            lr.startColor = lr.endColor = c;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.View;
            return go.transform;
        }

        Transform MakeLabel(Vector3 at, string text, Color color)
        {
            var go = new GameObject("HostageLabel");
            go.layer = layer;
            go.transform.SetParent(root.transform, false);
            go.transform.position = at;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = 6f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.rectTransform.sizeDelta = new Vector2(12f, 3f);
            UIFonts.Apply(t, UITheme.TextRole.Title);
            return go.transform;
        }

        void SetReveal(float t)
        {
            float g = Mathf.Clamp01((t - GridRevealAt) / GridRevealTime);
            gridMat.SetFloat(RevealId, Mathf.Lerp(0f, 900f, g * g));
            float m = Mathf.Clamp01((t - MapRevealAt) / MapRevealTime);
            Reveal(mapMat, m * WallTop + (m >= 1f ? 1000f : 0f));
            Reveal(floorMat, m > 0f ? 1000f : -1f);
            float w = Mathf.Clamp01((t - TownRevealAt) / TownRevealTime);
            Reveal(townMat, w * TownTop + (w >= 1f ? 1000f : 0f));
            Reveal(streetMat, w > 0f ? 1000f : -1f);
            float a = Mathf.Clamp01((t - MapRevealAt) / .6f);
            lineMat.SetFloat(AlphaId, a); markMat.SetFloat(AlphaId, 1f);   // the marker's own timing is in PoseMarker
            foreach (var mk in markers) mk.mat.SetFloat(AlphaId, 1f);
        }

        static void Reveal(Material m, float h)
        {
            if (m == null) return;
            m.SetFloat(RevealBaseId, 0f);
            m.SetFloat(RevealId, h);
        }

        public void Tick(float t, Camera main)
        {
            if (root == null) return;
            SetReveal(t);
            if (cam != null && main != null) cam.fieldOfView = main.fieldOfView;
            for (int i = 0; i < frames.Count; i++)
                frames[i].position = new Vector3(0f, frameBase[i] + Mathf.Sin(t * .6f + i) * .4f + t * .25f, 0f);
            foreach (var m in markers)
            {
                PoseMarker(m, t);
                if (main != null) m.label.rotation = Quaternion.LookRotation(m.label.position - main.transform.position, Vector3.up);
            }
        }

        static Color Alpha(float a) => new Color(1f, 1f, 1f, a);

        /// <summary>The hostage marker `t` s after Begin: hidden → slammed down → flash + shockwave ring → settled pulse.</summary>
        void PoseMarker(Marker m, float t)
        {
            var hostage = m.pos;
            Transform diamond = m.diamond, label = m.label, ring = m.ring;
            LineRenderer diamondLr = m.diamondLr, beamLr = m.beamLr, ringLr = m.ringLr;
            TMP_Text labelText = m.labelText;
            float p = Mathf.Clamp01((t - m.slamAt) / SlamTime);
            bool shown = t >= m.slamAt;
            float drop = SlamHeight * (1f - p * p);                    // accelerating down: driven in
            float since = t - (m.slamAt + SlamTime);
            float flash = since >= 0f ? Mathf.Exp(-since / (FlashTime * .35f)) : 0f;

            // diamond + label come down together; after impact the diamond bobs and turns
            float bob = since > 0f ? Mathf.Sin(since * 2.4f) * .15f * Mathf.Clamp01(since * 2f) : 0f;
            diamond.position = hostage + Vector3.up * (3.2f + drop + bob);
            diamond.rotation = Quaternion.Euler(0f, since > 0f ? since * 90f : 0f, 0f);
            label.position = hostage + Vector3.up * (4.6f + drop + bob);
            // squash & stretch: narrow and tall while it falls (more as it speeds up), flattened wide on impact, then a damped
            // spring back to its shape
            float sx, sy;
            if (since < 0f) { sx = Mathf.Lerp(1f, .42f, p); sy = Mathf.Lerp(1f, 1.65f, p); }
            else { float e = Mathf.Exp(-since * 6.5f) * Mathf.Cos(since * 21f); sx = 1f + .6f * e; sy = 1f - .45f * e; }
            diamond.localScale = new Vector3(sx, sy, sx);
            label.localScale = new Vector3(sx, sy, 1f);
            var dc = Alpha(shown ? 1f : 0f); diamondLr.startColor = diamondLr.endColor = dc;
            labelText.alpha = shown ? 1f : 0f;

            // the beam is drawn down behind the diamond, then reaches the floor
            float bottom = since >= 0f ? 0f : 3.2f + drop;
            beamLr.SetPosition(1, hostage + Vector3.up * bottom);
            var bc = Alpha(shown ? .8f : 0f); beamLr.startColor = beamLr.endColor = bc;

            // the ring: a shockwave out to ShockRadius, then the slow pulse
            if (since < 0f) { ringLr.startColor = ringLr.endColor = Alpha(0f); }
            else if (since < ShockTime)
            {
                float q = since / ShockTime, e = 1f - (1f - q) * (1f - q) * (1f - q);
                ring.localScale = Vector3.one * Mathf.Lerp(.5f, ShockRadius, e);
                ringLr.widthMultiplier = Mathf.Lerp(.35f, .06f, q);
                ringLr.startColor = ringLr.endColor = Alpha(1f - q);
            }
            else
            {
                float ph = Mathf.Repeat(since - ShockTime, 1.4f) / 1.4f;
                ring.localScale = Vector3.one * Mathf.Lerp(.6f, 2.2f, ph);
                ringLr.widthMultiplier = .08f;
                ringLr.startColor = ringLr.endColor = Alpha(Mathf.Clamp01((since - ShockTime) * 3f) * (1f - ph * .6f));
            }

            // one flash on impact: the whole marker (shared material) burns bright, then back
            m.mat.SetColor(ColorId, m.matBase * (1f + 4f * flash));
            var lc = m.labelColor;
            labelText.color = Color.Lerp(new Color(lc.r, lc.g, lc.b, labelText.color.a), new Color(1f, 1f, 1f, labelText.color.a), flash);
        }

        public void End()
        {
            if (cam != null) { cam.targetTexture = null; Object.Destroy(cam.gameObject); }
            cam = null;
            if (rt != null) { rt.Release(); Object.Destroy(rt); }
            rt = null;
            if (root != null) Object.Destroy(root);
            root = null;
            foreach (var m in new[] { mapMat, floorMat, townMat, streetMat, gridMat, lineMat, markMat }) if (m != null) Object.Destroy(m);
            mapMat = floorMat = townMat = streetMat = gridMat = lineMat = markMat = null;
            foreach (var mk in markers) if (mk.mat != null) Object.Destroy(mk.mat);
            markers.Clear();
            frames.Clear(); frameBase.Clear();
        }
    }
}
