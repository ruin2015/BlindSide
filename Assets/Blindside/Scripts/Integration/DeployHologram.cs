// The deployment's tactical hologram (the attack side): while Tab is held during the deployment, the map turns into the blue holographic
// plan of the briefing (IntroHologram's materials, Resources/Holo) — the walls, crates and floor as glowing wireframes built up from the
// ground, the world grid spreading out — and the enemy shows as red "contacts": a pulsing ring round a point a cell or so off where each
// one really stands (an estimate, not a fix), a beam and a turning diamond; the hostages as amber markers where they are. Let go of Tab
// and the deployment view comes back. A hint chip (TAB 敵の推定位置) sits under the top bar all through the deployment; over the hologram
// a header says what it is and how many contacts it shows.
// Seen by its own camera (layer Hologram) following the main camera into a RenderTexture, shown under the HUD (its canvas sorts below
// it) and faded in / out. Built the first time Tab is held, gone with the deployment. Per frame nothing is allocated.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public class DeployHologram : MonoBehaviour, ITimedEffect
    {
        public const int SortingOrder = -20;
        public const float FadeTime = .18f, BuildTime = .45f, WallTop = 3.5f, ContactRadius = 1.6f, ContactOffsetMin = .5f, ContactOffsetMax = 1.3f;
        public const float ChipY = 58f, ChipW = 250f, ChipH = 30f, HeaderY = 96f;
        static readonly Color Foe = new Color32(0xe5, 0x36, 0x4d, 0xff), Amber = new Color(1f, .78f, .3f, 1f);
        static readonly int RevealId = Shader.PropertyToID("_Reveal"), RevealBaseId = Shader.PropertyToID("_RevealBase"),
            CenterId = Shader.PropertyToID("_Center"), AlphaId = Shader.PropertyToID("_Alpha"), ColorId = Shader.PropertyToID("_Color");

        // ---- what the deployment hands it ----
        System.Func<bool> active;                 // the deployment is on and the player is the attack side
        IReadOnlyList<SimUnit> units;
        System.Func<IReadOnlyList<Vector3>> hostages;
        OperatorTeam side;

        // ---- the hologram ----
        GameObject root;
        Camera cam;
        RenderTexture rt;
        int layer = -1;
        Material mapMat, floorMat, gridMat, lineMat, foeMat, hostMat;
        sealed class Contact { public Transform ring, diamond; public LineRenderer ringLr, beamLr; public Vector3 pos; public float phase; }
        readonly List<Contact> contacts = new List<Contact>(12);
        readonly List<Contact> hostMarks = new List<Contact>(4);
        // the walls by kind, over the plan (WallKinds order): each its own tinted copy of the plan's material
        readonly Material[] kindMats = new Material[3];
        static readonly int FillId = Shader.PropertyToID("_Fill"), EdgeColorId = Shader.PropertyToID("_EdgeColor");

        // ---- the walls by kind (the hologram's legend, the first operation's lesson) ----
        /// <summary>The kinds shown, in legend order: ソフト壁 (breakable: shots go through), 強化壁 (reinforced), 通常壁 (solid).</summary>
        public static readonly Blindside.Map.MapTerrain[] WallKinds = { Blindside.Map.MapTerrain.Breakable, Blindside.Map.MapTerrain.Reinforced, Blindside.Map.MapTerrain.Solid };
        public static readonly string[] WallKindNames = { "ソフト壁", "強化壁", "通常壁" };
        public static readonly Color[] WallKindColors = { new Color(.36f, 1f, .64f, 1f), new Color(1f, .72f, .18f, 1f), new Color(.86f, .9f, .96f, 1f) };
        public const float KindFill = .55f, KindFillDim = .06f, KindFillFocus = .95f;
        /// <summary>Shown as if Tab were held (the first operation's lesson keeps it up while CRUSHER talks).</summary>
        public static bool Pinned;
        /// <summary>The deployment's hologram owns Tab right now (the log history leaves it alone).</summary>
        public static bool TabTaken { get; private set; }
        /// <summary>The contacts stand exactly where the enemy is (the first operation's lesson: the spots its first turn marks and shoots at).</summary>
        public static bool ExactContacts;
        /// <summary>The kind of wall singled out (an index into WallKinds; -1: none) — the others dim.</summary>
        public static int FocusKind = -1;

        // ---- the screen ----
        Canvas canvas;
        RawImage view;
        CanvasGroup viewGroup;
        RectTransform chip, header;
        TMP_Text headerCount;
        float shown, buildClock;
        bool holding;

        public float Duration => BuildTime;

        /// <summary>Editor checks: shown as if Tab were held.</summary>
        public static bool DebugHold;
        /// <summary>The hologram is fully on screen right now (the first operation's lesson waits for it).</summary>
        public static bool Showing { get; private set; }

        public void Bind(IReadOnlyList<SimUnit> units, OperatorTeam side, System.Func<bool> active, System.Func<IReadOnlyList<Vector3>> hostages)
        {
            this.units = units; this.side = side; this.active = active; this.hostages = hostages;
        }

        void LateUpdate()
        {
            bool on = active != null && active();
            TabTaken = on;
            if (!on) { if (canvas != null || root != null) Teardown(); return; }
            EnsureScreen();
            var kb = Keyboard.current;
            bool hold = (kb != null && kb.tabKey.isPressed) || DebugHold || Pinned;
            if (hold && root == null) Build();
            if (hold && !holding) { buildClock = 0f; Blindside.UI.SoundBank.Play(Blindside.UI.Snd.Hologram, .8f); }   // (the view turning holographic)
            holding = hold;
            float dt = Time.unscaledDeltaTime;
            shown = Mathf.MoveTowards(shown, hold ? 1f : 0f, dt / FadeTime);
            if (hold) buildClock += dt;
            if (cam != null)
            {
                bool render = shown > 0f;
                if (cam.enabled != render) cam.enabled = render;
                var main = Camera.main;
                if (main != null && cam.fieldOfView != main.fieldOfView) cam.fieldOfView = main.fieldOfView;
                if (render) KeepTextureSized();
            }
            if (root != null) Evaluate(buildClock);
            float a = UIMotionSettings.reduceMotion ? (hold ? 1f : 0f) : shown;
            Showing = a >= .99f && root != null;
            if (viewGroup.alpha != a) viewGroup.alpha = a;
            bool showView = a > 0f;
            if (view.gameObject.activeSelf != showView) view.gameObject.SetActive(showView);
            if (header.gameObject.activeSelf != showView) header.gameObject.SetActive(showView);
            if (chip.gameObject.activeSelf == showView) chip.gameObject.SetActive(!showView);

            // the rescue operation: the captives' M.I.A cards over the hologram, each tied to where it is held
            EnsureMiaCards();
            if (miaCanvas != null)
            {
                bool m = showView && hold;
                if (miaCanvas.gameObject.activeSelf != m) miaCanvas.gameObject.SetActive(m);
                if (m) miaCards.Tick(buildClock, Camera.main, miaMarker);
            }
        }

        // ---- the rescue operation's M.I.A cards (IntroMiaCards, as the rescue intro shows them) ----
        public const int MiaSortingOrder = 40;
        const float MiaCardAt = .15f, MiaCardStep = .12f, MiaMarkerZ = 1.2f;
        Canvas miaCanvas;
        Blindside.Cinematic.IntroMiaCards miaCards;
        readonly List<SimUnit> miaUnits = new List<SimUnit>(2);
        System.Func<int, Vector3> miaMarker;

        void EnsureMiaCards()
        {
            if (miaCanvas != null || !MatchBootstrap.RescueMatch || units == null) return;
            miaUnits.Clear();
            foreach (var u in units) if (u.Captive && u.Alive && u.Op != null) miaUnits.Add(u);
            if (miaUnits.Count == 0) return;
            var cgo = new GameObject("DeployHologram_MiaCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            miaCanvas = cgo.AddComponent<Canvas>();
            miaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            miaCanvas.sortingOrder = MiaSortingOrder;   // (over the HUD while Tab is held)
            UITheme.Current.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
            miaCards = Blindside.Cinematic.IntroMiaCards.Build(cgo.transform);
            var d = Blindside.Map.MapLibrary.Current;
            float mid = d != null ? d.width * .5f : 13f;
            for (int i = 0; i < miaUnits.Count; i++) miaCards.Add(miaUnits[i].Op, MiaCardAt + i * MiaCardStep, miaUnits[i].X > mid);
            miaMarker = i => GridSpace.GridToWorld(miaUnits[i].X, miaUnits[i].Y, MiaMarkerZ);
            cgo.SetActive(false);
        }

        /// <summary>The hologram `t` seconds after Tab went down: the walls built up from the ground, the grid spreading, the contacts pulsing.</summary>
        public void Evaluate(float t)
        {
            float k = UIMotionSettings.reduceMotion ? 1f : Mathf.Clamp01(t / BuildTime);
            Reveal(mapMat, k >= 1f ? 1000f : k * WallTop);
            Reveal(floorMat, k > 0f ? 1000f : -1f);
            if (gridMat != null) gridMat.SetFloat(RevealId, Mathf.Lerp(4f, 900f, k * k));
            float now = Time.unscaledTime;
            for (int i = 0; i < contacts.Count; i++) Pulse(contacts[i], now, k);
            for (int i = 0; i < hostMarks.Count; i++) Pulse(hostMarks[i], now, k);
            for (int i = 0; i < kindMats.Length; i++)
            {
                var m = kindMats[i];
                if (m == null) continue;
                Reveal(m, k >= 1f ? 1000f : k * WallTop);
                float fill = FocusKind < 0 ? KindFill : FocusKind == i ? KindFillFocus + .15f * Mathf.Sin(now * 5f) : KindFillDim;
                m.SetFloat(FillId, fill);
                m.SetFloat(AlphaId, FocusKind < 0 || FocusKind == i ? 1f : .35f);
            }
        }

        static void Pulse(Contact c, float now, float k)
        {
            // the ring holds its size (no growing pulse): it only comes in with the build-up
            float s = Mathf.Max(.01f, ContactRadius * k);
            c.ring.localScale = new Vector3(s, 1f, s);
            c.ringLr.startColor = c.ringLr.endColor = new Color(1f, 1f, 1f, .9f * k);
            c.beamLr.startColor = c.beamLr.endColor = new Color(1f, 1f, 1f, .7f * k);
            c.diamond.position = c.pos + Vector3.up * (2.6f + Mathf.Sin(now * 2.2f + c.phase * 6f) * .15f);
            c.diamond.rotation = Quaternion.Euler(0f, now * 90f + c.phase * 360f, 0f);
        }

        static void Reveal(Material m, float h)
        {
            if (m == null) return;
            m.SetFloat(RevealBaseId, 0f);
            m.SetFloat(RevealId, h);
        }

        // ---- building --------------------------------------------------------------------------------------------------------
        void Build()
        {
            layer = LayerMask.NameToLayer(IntroHologramLayer);
            var main = Camera.main;
            mapMat = Load("Mat_Holo_Map"); floorMat = Load("Mat_Holo_MapFloor"); gridMat = Load("Mat_Holo_Grid");
            lineMat = Load("Mat_Holo_Line"); foeMat = Load("Mat_Holo_Marker"); hostMat = Load("Mat_Holo_Marker");
            if (layer < 0 || main == null || mapMat == null) { layer = -1; return; }
            if (foeMat != null) foeMat.SetColor(ColorId, new Color(Foe.r, Foe.g, Foe.b, 1f) * 1.6f);
            if (foeMat != null) foeMat.SetFloat(AlphaId, 1f);
            if (hostMat != null) hostMat.SetFloat(AlphaId, 1f);

            root = new GameObject("DeployHologram");
            var mapRoot = GameObject.Find("Blindside_MapRoot");
            var bounds = new Bounds(Vector3.zero, Vector3.one); bool any = false;
            if (mapRoot != null)
                foreach (var r in mapRoot.GetComponentsInChildren<MeshRenderer>(false))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || !r.enabled) continue;
                    bool floor = r.transform.parent != null && r.transform.parent.name == "Floor";
                    Clone(mf, floor ? floorMat : mapMat);
                    if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                }
            if (gridMat != null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(ground.GetComponent<Collider>());
                ground.name = "HoloGround"; ground.layer = layer;
                ground.transform.SetParent(root.transform, false);
                ground.transform.SetPositionAndRotation(new Vector3(bounds.center.x, .01f, bounds.center.z), Quaternion.Euler(90f, 0f, 0f));
                ground.transform.localScale = new Vector3(600f, 600f, 1f);
                ground.GetComponent<MeshRenderer>().sharedMaterial = gridMat;
                gridMat.SetVector(CenterId, new Vector4(bounds.center.x, bounds.center.z, 0f, 0f));
            }

            BuildWallKinds();

            // the enemy: one contact per fighter, a cell or so off where it stands (its own fixed offset: the estimate holds still)
            contacts.Clear();
            if (units != null)
                foreach (var u in units)
                {
                    if (u == null || !u.Alive || u.Team == side || u.Captive || u.Reserve) continue;
                    var rng = new System.Random(StableHash(u.Id));
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float off = ExactContacts ? 0f : Mathf.Lerp(ContactOffsetMin, ContactOffsetMax, (float)rng.NextDouble());
                    var p = GridSpace.GridToWorld(u.X + Mathf.Cos(ang) * off, u.Y + Mathf.Sin(ang) * off, 0f);
                    contacts.Add(Marker(p, foeMat, (float)rng.NextDouble()));
                }
            // the rescue targets, where they are: the hostages and the captured operators (amber)
            hostMarks.Clear();
            var hs = hostages != null ? hostages() : null;
            if (hs != null) for (int i = 0; i < hs.Count; i++) hostMarks.Add(Marker(hs[i], hostMat, i * .37f));
            if (units != null)
                foreach (var u in units)
                    if (u != null && u.Alive && u.Captive) hostMarks.Add(Marker(GridSpace.GridToWorld(u.X, u.Y, 0f), hostMat, hostMarks.Count * .37f));
            if (headerCount != null)
                headerCount.text = "敵の推定位置 · 反応 " + contacts.Count + (hostMarks.Count > 0 ? "  ·  救出目標 " + hostMarks.Count : "");

            // the camera: follows the main one, renders only the hologram
            var cgo = new GameObject("DeployHoloCam");
            cgo.transform.SetParent(main.transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.004f, .02f, .055f, 1f);
            cam.cullingMask = 1 << layer;
            cam.nearClipPlane = .3f; cam.farClipPlane = 3000f;
            cam.fieldOfView = main.fieldOfView;
            cam.enabled = false;
            main.cullingMask &= ~(1 << layer);
            KeepTextureSized();
        }

        /// <summary>One mesh per kind of wall: a box a little larger than each of its cells (every face UV'd 0..1, so the hologram shader
        /// draws it as a wireframe), in its own tinted copy of the plan's material.</summary>
        void BuildWallKinds()
        {
            var data = Blindside.Map.MapLibrary.Current;
            if (data == null || data.terrain == null) return;
            for (int k = 0; k < WallKinds.Length; k++)
            {
                var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var normals = new List<Vector3>(); var tris = new List<int>();
                for (int y = 0; y < data.height; y++)
                    for (int x = 0; x < data.width; x++)
                    {
                        int i = y * data.width + x;
                        if (i >= data.terrain.Length || data.terrain[i] != WallKinds[k]) continue;
                        AddBox(GridSpace.GridToWorld(x, y, 0f), .54f, WallTop + .06f, verts, uvs, normals, tris);
                    }
                if (verts.Count == 0) continue;
                var mesh = new Mesh { name = "HoloWalls_" + WallKinds[k] };
                if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetNormals(normals); mesh.SetTriangles(tris, 0);
                var mat = new Material(mapMat);
                var c = WallKindColors[k];
                mat.SetColor(ColorId, c);
                mat.SetColor(EdgeColorId, Color.Lerp(c, Color.white, .35f));
                mat.SetFloat(FillId, KindFill);
                kindMats[k] = mat;
                var go = new GameObject("HoloWalls_" + WallKinds[k], typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = layer;
                go.transform.SetParent(root.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        static void AddBox(Vector3 c, float half, float h, List<Vector3> v, List<Vector2> uv, List<Vector3> n, List<int> t)
        {
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++) p[i] = c + new Vector3((i & 1) == 0 ? -half : half, (i & 2) == 0 ? 0f : h, (i & 4) == 0 ? -half : half);
            Face(p[2], p[3], p[7], p[6], Vector3.up, v, uv, n, t);         // top
            Face(p[0], p[1], p[3], p[2], Vector3.back, v, uv, n, t);       // -z
            Face(p[5], p[4], p[6], p[7], Vector3.forward, v, uv, n, t);    // +z
            Face(p[4], p[0], p[2], p[6], Vector3.left, v, uv, n, t);       // -x
            Face(p[1], p[5], p[7], p[3], Vector3.right, v, uv, n, t);      // +x
        }

        static void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, List<Vector3> v, List<Vector2> uv, List<Vector3> n, List<int> t)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
            n.Add(nrm); n.Add(nrm); n.Add(nrm); n.Add(nrm);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        const string IntroHologramLayer = Blindside.Cinematic.IntroHologram.LayerName;

        static int StableHash(string s)
        {
            unchecked { int h = 23; if (s != null) foreach (char c in s) h = h * 31 + c; return h; }
        }

        void KeepTextureSized()
        {
            int w = Mathf.Max(64, Screen.width), h = Mathf.Max(64, Screen.height);
            if (rt != null && rt.width == w && rt.height == h) return;
            if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "DeployHologramRT" };
            cam.targetTexture = rt;
            if (view != null) view.texture = rt;
        }

        static Material Load(string name)
        {
            var m = Resources.Load<Material>("Holo/" + name);
            return m != null ? new Material(m) : null;
        }

        void Clone(MeshFilter src, Material mat)
        {
            if (mat == null) return;
            var go = new GameObject("Holo", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = layer;
            var t = go.transform;
            t.SetParent(root.transform, false);
            t.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            t.localScale = src.transform.lossyScale;
            go.GetComponent<MeshFilter>().sharedMesh = src.sharedMesh;
            var mr = go.GetComponent<MeshRenderer>();
            var mats = new Material[src.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        static readonly Vector3[] Circle = MakeCircle();
        static Vector3[] MakeCircle()
        {
            var c = new Vector3[40];
            for (int i = 0; i < c.Length; i++) { float a = i * Mathf.PI * 2f / c.Length; c[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }
            return c;
        }

        Contact Marker(Vector3 p, Material mat, float phase)
        {
            var c = new Contact { pos = p, phase = phase };
            c.ring = Line("Ring", Circle, .07f, mat, true);
            c.ring.position = p + Vector3.up * .05f;
            c.ringLr = c.ring.GetComponent<LineRenderer>();
            c.beamLr = Line("Beam", new[] { p, p + Vector3.up * 2.2f }, .05f, mat, false).GetComponent<LineRenderer>();
            c.diamond = Line("Diamond", new[] { new Vector3(0f, .5f, 0f), new Vector3(.32f, 0f, 0f), new Vector3(0f, -.5f, 0f), new Vector3(-.32f, 0f, 0f) }, .06f, mat, true);
            return c;
        }

        Transform Line(string name, Vector3[] pts, float width, Material mat, bool loop)
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
            lr.startColor = lr.endColor = Color.white;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.View;
            return go.transform;
        }

        // ---- the screen layer: the hologram's picture, its header, the hint chip ----
        void EnsureScreen()
        {
            if (canvas != null) return;
            var cgo = new GameObject("DeployHologram_Canvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            UITheme.Current.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            BuildScreen((RectTransform)cgo.transform, out view, out viewGroup, out header, out headerCount, out chip);
            view.texture = rt;
            view.gameObject.SetActive(false);
            header.gameObject.SetActive(false);
        }

        /// <summary>The screen layer (also built by Render Previews): the hologram's full-screen picture (faded by its group), the header over
        /// it and the hint chip under the top bar.</summary>
        public static void BuildScreen(RectTransform parent, out RawImage view, out CanvasGroup group, out RectTransform header, out TMP_Text count, out RectTransform chip)
        {
            var theme = UITheme.Current;
            var vr = HudKit.NewRect("HoloView", parent);
            HudKit.Stretch(vr);
            group = vr.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            view = vr.gameObject.AddComponent<RawImage>();
            view.raycastTarget = false;

            // the header, top centre under the top bar: TACTICAL HOLOGRAM + what it shows
            header = HudKit.NewRect("HoloHeader", parent);
            header.anchorMin = header.anchorMax = new Vector2(.5f, 1f); header.pivot = new Vector2(.5f, 1f);
            header.sizeDelta = new Vector2(420f, 86f); header.anchoredPosition = new Vector2(0f, -HeaderY);
            HudKit.AddImage(header, new Color(.02f, .08f, .16f, .78f));
            var line = HudKit.NewRect("DecoHeaderLine", header);
            line.anchorMin = new Vector2(0f, 0f); line.anchorMax = new Vector2(1f, 0f); line.pivot = new Vector2(.5f, 0f);
            line.sizeDelta = new Vector2(0f, 2f); line.anchoredPosition = Vector2.zero;
            HudKit.AddImage(line, new Color(.4f, .85f, 1f, .9f));
            var title = HudKit.AddText(header, "TACTICAL HOLOGRAM", 18f, new Color(.55f, .9f, 1f, 1f), true, TextAlignmentOptions.Top, "Title", UITheme.TextRole.Display);
            Place(title.rectTransform, 0f, 6f, 420f, 22f);
            count = HudKit.AddText(header, "敵の推定位置", 15f, Color.white, false, TextAlignmentOptions.Top, "Count", UITheme.TextRole.Body);
            Place(count.rectTransform, 0f, 30f, 420f, 22f);
            // the legend: the walls by kind, each in its colour
            for (int i = 0; i < WallKindNames.Length; i++)
            {
                float x = 38f + i * 124f;
                var sw = HudKit.NewRect("Swatch" + i, header);
                Place(sw, x, 60f, 14f, 14f);
                HudKit.AddImage(sw, WallKindColors[i]);
                var nm = HudKit.AddText(header, WallKindNames[i], 15f, WallKindColors[i], false, TextAlignmentOptions.MidlineLeft, "Kind" + i, UITheme.TextRole.Body);
                Place(nm.rectTransform, x + 20f, 56f, 100f, 22f);
            }

            // the hint chip: TAB (a key cap) and what it does
            chip = HudKit.NewRect("HoloHint", parent);
            chip.anchorMin = chip.anchorMax = new Vector2(.5f, 1f); chip.pivot = new Vector2(.5f, 1f);
            chip.sizeDelta = new Vector2(ChipW, ChipH); chip.anchoredPosition = new Vector2(0f, -ChipY);
            HudKit.AddImage(chip, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .88f));
            chip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            var key = HudKit.NewRect("Key", chip);
            Place(key, 14f, 5f, 46f, 20f);
            HudKit.AddImage(key, theme.blue);
            var keyText = HudKit.AddText(key, "TAB", 13f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            HudKit.Stretch(keyText.rectTransform);
            var what = HudKit.AddText(chip, "長押しで敵の推定位置", 14f, Color.white, false, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Body);
            Place(what.rectTransform, 68f, 0f, 170f, ChipH);
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        // ---- the deployment is over: everything goes ----
        void Teardown()
        {
            if (cam != null) { cam.targetTexture = null; Destroy(cam.gameObject); }
            cam = null;
            if (rt != null) { rt.Release(); Destroy(rt); }
            rt = null;
            if (root != null) Destroy(root);
            root = null;
            foreach (var m in new[] { mapMat, floorMat, gridMat, lineMat, foeMat, hostMat }) if (m != null) Destroy(m);
            mapMat = floorMat = gridMat = lineMat = foeMat = hostMat = null;
            for (int i = 0; i < kindMats.Length; i++) { if (kindMats[i] != null) Destroy(kindMats[i]); kindMats[i] = null; }
            contacts.Clear(); hostMarks.Clear();
            if (canvas != null) Destroy(canvas.gameObject);
            canvas = null; view = null; viewGroup = null; header = chip = null; headerCount = null;
            if (miaCanvas != null) Destroy(miaCanvas.gameObject);
            miaCanvas = null; miaCards = null; miaUnits.Clear();
            shown = 0f; holding = false; Showing = false; TabTaken = false;
        }

        void OnDestroy() => Teardown();
    }
}
