// Draws a MapEditModel cell by cell: one floor tile per cell (outdoor / indoor material), one block per wall or crate cell taken
// from a pool, so an edit touches only the cells that changed (40×40 stays smooth: no whole-map rebuild). Walls always show their
// real look — wood, concrete grey pillar, steel shutter with its frame and yellow rail — the attack side's "not seen yet" hiding
// (ReinforcedWallViews) does not apply here.
// The look comes from the scene itself: MapCellTemplates.Capture() reads the baked floor materials and one baked wood / hard /
// crate block of the default map (Blindside_MapRoot) before the editor hides that map, and the blocks are clones of those, so an
// edited map looks exactly like the game's. Also drawn here: panel outlines (thin lines), the grid (toggle), the hovered cell, the
// translucent stroke preview and the map's exfil zones.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Blindside.Integration;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.MapEditor
{
    /// <summary>The scene's own floor materials and blocks, read once when the editor opens.</summary>
    public sealed class MapCellTemplates
    {
        public Material outFloor, inFloor, extract;
        public float outY = 0f, inY = .01f, extractY = .02f;
        public GameObject wood, hard, crate;

        public static MapCellTemplates Capture()
        {
            var t = new MapCellTemplates();
            var root = GameObject.Find("Blindside_MapRoot");
            if (root != null)
            {
                var floor = root.transform.Find("Floor");
                if (floor != null)
                {
                    Read(floor.Find("Floor_Outside"), ref t.outFloor, ref t.outY);
                    Read(floor.Find("Floor_Inside"), ref t.inFloor, ref t.inY);
                    // (ExfilZoneView renames the baked plane to Floor_Extract_A once it has run, so look it up by prefix)
                    foreach (Transform c in floor)
                        if (c.name.StartsWith("Floor_Extract")) { Read(c, ref t.extract, ref t.extractY); break; }
                }
                var walls = root.transform.Find("Walls");
                if (walls != null)
                    foreach (Transform c in walls)
                    {
                        if (t.wood == null && c.name.StartsWith("Wall_Wood_")) t.wood = c.gameObject;
                        else if (t.hard == null && c.name.StartsWith("Hard_Block_")) t.hard = c.gameObject;
                        else if (t.crate == null && c.name.StartsWith("Crate_")) t.crate = c.gameObject;
                    }
            }
            return t;
        }

        static void Read(Transform plane, ref Material mat, ref float y)
        {
            if (plane == null) return;
            var r = plane.GetComponent<MeshRenderer>();
            if (r != null) mat = r.sharedMaterial;
            y = plane.position.y;
        }
    }

    public sealed class MapCellView
    {
        const int Wood = 0, Hard = 1, Steel = 2, CrateKind = 3;
        const float PanelLineY = .82f, HoverY = .97f, PreviewY = .95f, GridY = .04f;
        const float DropY = .03f, FovY = .05f, SelectionY = .08f, MarkerY = .15f, InvalidY = 2.2f;

        // object colours (the defenders use the editor's blue, like the attack side's team colour)
        public static readonly Color HostageColor = new Color(1f, .6f, .1f), DefenderColor = new Color32(0x1f, 0x96, 0xe8, 0xff), CameraColor = new Color(.95f, .32f, .6f);
        public static readonly Color TerroristColor = new Color32(0xd6, 0x3b, 0x3b, 0xff), CaptiveColor = new Color32(0x9b, 0x3c, 0xf2, 0xff);
        public static readonly Color ElectricColor = new Color32(0xff, 0xd2, 0x1f, 0xff), MineColor = new Color32(0xff, 0x6a, 0x1a, 0xff),
            SensorColor = new Color32(0x2f, 0xd8, 0x86, 0xff);

        public static Color GadgetColor(string kind) => kind == MapData.GadgetMine ? MineColor : kind == MapData.GadgetSensor ? SensorColor : ElectricColor;
        public static string GadgetLetter(string kind) => kind == MapData.GadgetMine ? "M" : kind == MapData.GadgetSensor ? "S" : "E";
        static readonly Color DropTint = new Color(.35f, .78f, 1f, .42f), CamFovTint = new Color(1f, .4f, .65f, .33f), CamFovLine = new Color(1f, .4f, .65f, .95f);
        static readonly Color MarkerRim = new Color(.06f, .07f, .09f, .95f);

        readonly MapEditModel model;
        readonly MapCellTemplates tpl;
        public GameObject Root { get; }

        readonly Transform floors, blocks, overlays, objectsRoot;
        readonly Mesh quad, discMesh, arrowMesh;
        readonly Transform[] floorTf;
        readonly MeshRenderer[] floorRenderer;
        readonly GameObject[] wallObj, crateObj;
        readonly int[] wallKind;
        readonly Stack<GameObject>[] pool = { new Stack<GameObject>(), new Stack<GameObject>(), new Stack<GameObject>(), new Stack<GameObject>() };
        readonly Material outMat, inMat, steelMat, frameMat, railMat, extractMat;
        readonly Dictionary<Color, Material> overlayMats = new Dictionary<Color, Material>();
        readonly Shader overlayShader;

        readonly GameObject gridObj, panelLinesObj, hoverObj, dropObj, camFovObj, camFovLineObj, selectionObj, invalidX;
        readonly Mesh panelMesh, dropMesh, camFovMesh, camFovLineMesh;
        GameObject defenderPreview;
        bool showDrop, showCamFov;
        readonly List<GameObject> previewPool = new List<GameObject>();
        int previewShown;

        public bool GridVisible { get => gridObj.activeSelf; set => gridObj.SetActive(value); }
        /// <summary>Overlay: the outdoor cells a drop may land on (outdoor floor without a crate), in light blue.</summary>
        public bool ShowDrop { get => showDrop; set { showDrop = value; RebuildOverlays(); } }
        /// <summary>Overlay: every camera's 3.2 m sight, cut by the walls.</summary>
        public bool ShowCameraFov { get => showCamFov; set { showCamFov = value; RebuildOverlays(); } }

        public MapCellView(MapEditModel model, MapCellTemplates templates)
        {
            this.model = model; tpl = templates;
            int w = model.Width, h = model.Height;
            Root = new GameObject("MapEditorWorld");
            floors = Child("Floors"); blocks = Child("Blocks"); overlays = Child("Overlays"); objectsRoot = Child("Objects");
            quad = MakeQuad(); discMesh = MakeDisc(); arrowMesh = MakeArrow();

            overlayShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            outMat = Tiled(tpl.outFloor, new Color(.16f, .19f, .16f)); inMat = Tiled(tpl.inFloor, new Color(.32f, .34f, .38f));
            extractMat = tpl.extract != null ? new Material(tpl.extract) : Flat(new Color(.9f, .7f, .1f));
            steelMat = ReinforcedWallViews.Make("EditorSteel", ReinforcedWallViews.Steel, .35f, .45f, false);
            frameMat = ReinforcedWallViews.Make("EditorFrame", ReinforcedWallViews.Frame, .3f, .4f, false);
            railMat = ReinforcedWallViews.Make("EditorRail", ReinforcedWallViews.Rail, 0f, 0f, true);

            floorTf = new Transform[w * h]; floorRenderer = new MeshRenderer[w * h];
            wallObj = new GameObject[w * h]; crateObj = new GameObject[w * h]; wallKind = new int[w * h];
            for (int i = 0; i < wallKind.Length; i++) wallKind[i] = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var go = new GameObject($"F_{x}_{y}");
                    go.transform.SetParent(floors, false);
                    go.AddComponent<MeshFilter>().sharedMesh = quad;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    floorTf[y * w + x] = go.transform; floorRenderer[y * w + x] = mr;
                }

            gridObj = MakeLines("Grid", BuildGridMesh(w, h), new Color(1f, 1f, 1f, .17f));
            panelMesh = new Mesh { name = "PanelLines" };
            panelLinesObj = MakeLines("PanelLines", panelMesh, new Color(1f, .82f, .12f, .95f));
            hoverObj = MakeOverlayQuad("Hover", new Color(1f, .9f, .25f, .45f), 1.04f, HoverY);
            hoverObj.SetActive(false);

            dropMesh = new Mesh { name = "DropZone" };
            dropObj = MakeLines("DropZoneOverlay", dropMesh, DropTint);
            camFovMesh = new Mesh { name = "CameraFov" };
            camFovObj = MakeLines("CameraFovOverlay", camFovMesh, CamFovTint);
            camFovLineMesh = new Mesh { name = "CameraFovOutline" };
            camFovLineObj = MakeLines("CameraFovOutline", camFovLineMesh, CamFovLine);
            dropObj.SetActive(false); camFovObj.SetActive(false); camFovLineObj.SetActive(false);
            selectionObj = MakeOverlayQuad("Selection", new Color(1f, 1f, 1f, .5f), 1.2f, SelectionY);
            selectionObj.SetActive(false);
            invalidX = BuildInvalidX();
            invalidX.SetActive(false);

            RefreshAll();
            RebuildPanelLines();
            RebuildObjects();
        }

        Transform Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root.transform, false);
            return go.transform;
        }

        // ---- materials & meshes ---------------------------------------------------------------------------------------------

        static Material Flat(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        /// <summary>The scene's floor material at one texture repeat per cell (the baked planes repeat it once per cell over their size).</summary>
        static Material Tiled(Material src, Color fallback)
        {
            if (src == null) return Flat(fallback);
            var m = new Material(src) { name = src.name + " (cell)" };
            m.mainTextureScale = Vector2.one;
            return m;
        }

        Material OverlayMat(Color c)
        {
            if (overlayMats.TryGetValue(c, out var m)) return m;
            m = new Material(overlayShader) { name = "EditorOverlay" };
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.renderQueue = 3100;
            overlayMats[c] = m;
            return m;
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "CellQuad" };
            m.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(.5f, 0, .5f), new Vector3(-.5f, 0, .5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 3, 2, 0, 2, 1 };   // clockwise seen from above
            m.RecalculateBounds();
            return m;
        }

        GameObject MakeOverlayQuad(string name, Color c, float size, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(overlays, false);
            go.transform.localScale = new Vector3(size, 1f, size);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = OverlayMat(c);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.transform.position = new Vector3(0, y, 0);
            return go;
        }

        GameObject MakeLines(string name, Mesh mesh, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(overlays, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = OverlayMat(c);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        static Mesh BuildGridMesh(int w, int h)
        {
            var v = new List<Vector3>(); var idx = new List<int>();
            for (int x = 0; x <= w; x++) { idx.Add(v.Count); v.Add(new Vector3(x - .5f, GridY, .5f)); idx.Add(v.Count); v.Add(new Vector3(x - .5f, GridY, -(h - .5f))); }
            for (int y = 0; y <= h; y++) { idx.Add(v.Count); v.Add(new Vector3(-.5f, GridY, .5f - y)); idx.Add(v.Count); v.Add(new Vector3(w - .5f, GridY, .5f - y)); }
            var m = new Mesh { name = "Grid" };
            m.SetVertices(v);
            m.SetIndices(idx, MeshTopology.Lines, 0);
            m.RecalculateBounds();
            return m;
        }

        // ---- objects --------------------------------------------------------------------------------------------------------

        /// <summary>Redraws every object from the model: the exfil zones (like the game draws them: the scene's extract floor and an
        /// "EXFIL X" label), the hostage (H), the numbered defender starts with their facing arrow, the cameras (C). There are few
        /// enough (≤ 22) to rebuild them all on each change.</summary>
        public void RebuildObjects()
        {
            for (int i = objectsRoot.childCount - 1; i >= 0; i--)
            {
                var child = objectsRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
            var o = model.Objects;
            for (int i = 0; i < o.exfils.Count; i++) AddExfil(i, o.exfils[i]);
            if (o.hostage.HasValue) AddMarker(objectsRoot, "Hostage", o.hostage.Value, HostageColor, "H", null);
            for (int i = 0; i < o.extraHostages.Count; i++) AddMarker(objectsRoot, "Hostage_" + (i + 2), o.extraHostages[i], HostageColor, "H", null);
            for (int s = 0; s < ObjectState.DefenderSlots; s++)
                if (o.defPlaced[s]) AddMarker(objectsRoot, "Defender_" + (s + 1), o.defCell[s], DefenderColor, (s + 1).ToString(), o.defFacing[s]);
            for (int i = 0; i < o.cameras.Count; i++) AddMarker(objectsRoot, "Camera_" + (i + 1), o.cameras[i], CameraColor, "C", null);
            for (int i = 0; i < o.terrorists.Count; i++) AddMarker(objectsRoot, "Terrorist_" + (i + 1), o.terrorists[i].cell, TerroristColor, "T", o.terrorists[i].facing);
            for (int i = 0; i < o.gadgets.Count; i++)
                AddMarker(objectsRoot, "Gadget_" + (i + 1), o.gadgets[i].cell, GadgetColor(o.gadgets[i].kind), GadgetLetter(o.gadgets[i].kind), null);
            for (int i = 0; i < o.captives.Count; i++)
            {
                var op = Blindside.Data.BuildAssetRegistry.Operator(o.captives[i].operatorKey);
                AddMarker(objectsRoot, "Captive_" + (i + 1), o.captives[i].cell, op != null ? op.color : CaptiveColor, "P", null);
            }
            RebuildOverlays();
        }

        void AddExfil(int index, ExfilRect z)
        {
            string letter = MapEditModel.ExfilLabel(index);
            int zw = z.Width, zh = z.Height;
            var go = new GameObject("Exfil_" + letter);
            go.transform.SetParent(objectsRoot, false);
            go.transform.position = GridSpace.GridToWorld(z.Center.x, z.Center.y, tpl.extractY);
            go.transform.localScale = new Vector3(zw, 1f, zh);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(extractMat) { mainTextureScale = new Vector2(zw, zh) };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // the "EXFIL X" label, drawn like ExfilZoneView does (flat on the floor, letters upright from the battle camera).
            var label = new GameObject("ExfilLabel_" + letter);
            label.transform.SetParent(objectsRoot, false);
            label.transform.position = GridSpace.GridToWorld(z.Center.x, z.Center.y, .05f);
            label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var t = label.AddComponent<TextMeshPro>();
            t.text = zw >= 3 ? "EXFIL " + letter : letter;
            UIFonts.Auto(t, true);   // "EXFIL A" / "A": display font, no fake bold
            t.fontSize = zw >= 3 ? 5.5f : 4f;
            t.color = new Color32(0xff, 0xd2, 0x1f, 0xff);
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(Mathf.Max(zw, zw >= 3 ? 4f : 1f), Mathf.Max(zh, 1f));
        }

        /// <summary>A round marker with a rim and a letter / number on a cell, and — for a defender — an arrow pointing where it faces
        /// (`facing`: radians, atan2 over grid x/y, like MapDefenderStart.facing).</summary>
        GameObject AddMarker(Transform parent, string name, Vector2Int cell, Color color, string text, float? facing)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = GridSpace.GridToWorld(cell.x, cell.y, MarkerY);
            Flat(root.transform, "Rim", discMesh, MarkerRim, .94f, 0f);
            if (facing.HasValue)
            {
                var arrow = Flat(root.transform, "Facing", arrowMesh, new Color(1f, 1f, 1f, .95f), 1f, .015f);
                arrow.transform.rotation = Quaternion.Euler(0f, facing.Value * Mathf.Rad2Deg, 0f);   // the mesh points +x: grid (cos f, sin f) = world (cos f, 0, -sin f)
            }
            Flat(root.transform, "Body", discMesh, color, .7f, .03f);
            var label = new GameObject("Label");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, .07f, 0f);
            label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var t = label.AddComponent<TextMeshPro>();
            t.text = text; t.fontSize = 4.2f; t.color = Color.white;
            UIFonts.Auto(t, true);
            t.alignment = TextAlignmentOptions.Center;
            t.sortingOrder = 10;    // the marker discs are transparent overlays (queue 3100) and would otherwise be drawn over the letter
            t.rectTransform.sizeDelta = new Vector2(1f, 1f);
            return root;
        }

        GameObject Flat(Transform parent, string name, Mesh mesh, Color c, float scale, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(scale, 1f, scale);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = OverlayMat(c);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        static Mesh MakeDisc()
        {
            const int n = 28;
            var v = new Vector3[n + 1]; var tri = new int[n * 3];
            v[0] = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                float a = i * 2f * Mathf.PI / n;
                v[i + 1] = new Vector3(Mathf.Cos(a) * .5f, 0f, Mathf.Sin(a) * .5f);
                tri[i * 3] = 0; tri[i * 3 + 1] = (i + 1) % n + 1; tri[i * 3 + 2] = i + 1;
            }
            var m = new Mesh { name = "Disc", vertices = v, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        // A triangle on the x axis (tip at +.68 beyond the marker's rim, base behind the rim's edge): drawn below the marker body,
        // so only the tip shows around it.
        static Mesh MakeArrow()
        {
            var m = new Mesh { name = "Arrow" };
            m.vertices = new[] { new Vector3(.74f, 0f, 0f), new Vector3(.2f, 0f, -.3f), new Vector3(.2f, 0f, .3f) };
            m.triangles = new[] { 0, 2, 1 };
            m.RecalculateBounds();
            return m;
        }

        GameObject BuildInvalidX()
        {
            var root = new GameObject("InvalidX");
            root.transform.SetParent(overlays, false);
            for (int k = 0; k < 2; k++)
            {
                float ang = k == 0 ? 45f : -45f;
                Bar(root.transform, "Outline", new Color(.05f, .05f, .07f, 1f), new Vector3(1.06f, .03f, .3f), ang, -.01f);
                Bar(root.transform, "Bar", new Color(.96f, .13f, .2f, 1f), new Vector3(.96f, .04f, .17f), ang, 0f);
            }
            return root;
        }

        void Bar(Transform parent, string name, Color c, Vector3 scale, float yawDeg, float y)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = OverlayMat(c);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>The red X that says "can't place here" on a cell (null hides it).</summary>
        public void SetInvalid(Vector2Int? cell)
        {
            if (!cell.HasValue) { invalidX.SetActive(false); return; }
            invalidX.SetActive(true);
            invalidX.transform.position = GridSpace.GridToWorld(cell.Value.x, cell.Value.y, InvalidY);
        }
        public bool InvalidShown => invalidX.activeSelf;

        /// <summary>A highlight behind the selected object (None hides it).</summary>
        public void SetSelection(ObjRef r)
        {
            var o = model.Objects;
            Vector2 c; Vector2 size = Vector2.one * 1.2f;
            switch (r.kind)
            {
                case ObjKind.Hostage:
                    if (r.index >= 1 && r.index - 1 < o.extraHostages.Count) { c = new Vector2(o.extraHostages[r.index - 1].x, o.extraHostages[r.index - 1].y); break; }
                    if (r.index >= 1 || !o.hostage.HasValue) { selectionObj.SetActive(false); return; }
                    c = new Vector2(o.hostage.Value.x, o.hostage.Value.y); break;
                case ObjKind.Defender: if (r.index < 0 || r.index >= ObjectState.DefenderSlots || !o.defPlaced[r.index]) { selectionObj.SetActive(false); return; } c = new Vector2(o.defCell[r.index].x, o.defCell[r.index].y); break;
                case ObjKind.Camera: if (r.index < 0 || r.index >= o.cameras.Count) { selectionObj.SetActive(false); return; } c = new Vector2(o.cameras[r.index].x, o.cameras[r.index].y); break;
                case ObjKind.Terrorist: if (r.index < 0 || r.index >= o.terrorists.Count) { selectionObj.SetActive(false); return; } c = new Vector2(o.terrorists[r.index].cell.x, o.terrorists[r.index].cell.y); break;
                case ObjKind.Captive: if (r.index < 0 || r.index >= o.captives.Count) { selectionObj.SetActive(false); return; } c = new Vector2(o.captives[r.index].cell.x, o.captives[r.index].cell.y); break;
                case ObjKind.Gadget: if (r.index < 0 || r.index >= o.gadgets.Count) { selectionObj.SetActive(false); return; } c = new Vector2(o.gadgets[r.index].cell.x, o.gadgets[r.index].cell.y); break;
                case ObjKind.Exfil: if (r.index < 0 || r.index >= o.exfils.Count) { selectionObj.SetActive(false); return; } c = o.exfils[r.index].Center; size = new Vector2(o.exfils[r.index].Width + .2f, o.exfils[r.index].Height + .2f); break;
                default: selectionObj.SetActive(false); return;
            }
            selectionObj.SetActive(true);
            selectionObj.transform.localScale = new Vector3(size.x, 1f, size.y);
            selectionObj.transform.position = GridSpace.GridToWorld(c.x, c.y, SelectionY);
        }

        /// <summary>The marker of the defender being placed, following the cursor's drag (null hides it).</summary>
        public void SetDefenderPreview(Vector2Int? cell, int slot, float facing)
        {
            if (defenderPreview != null) { defenderPreview.SetActive(false); Object.Destroy(defenderPreview); defenderPreview = null; }
            if (!cell.HasValue) return;
            defenderPreview = slot < 0
                ? AddMarker(overlays, "TerroristPreview", cell.Value, TerroristColor, "T", facing)   // (slot −1: a generic terrorist)
                : AddMarker(overlays, "DefenderPreview", cell.Value, DefenderColor, (slot + 1).ToString(), facing);
        }

        // ---- overlays: the drop zone and the cameras' sight ------------------------------------------------------------------

        /// <summary>Rebuilds the two optional overlays from the model (only those that are on).</summary>
        public void RebuildOverlays()
        {
            dropObj.SetActive(showDrop);
            camFovObj.SetActive(showCamFov); camFovLineObj.SetActive(showCamFov);
            var d = model.Data;
            if (showDrop)
            {
                var v = new List<Vector3>(); var tri = new List<int>();
                for (int y = 0; y < d.height; y++)
                    for (int x = 0; x < d.width; x++)
                    {
                        if (!d.IsDropCell(x, y)) continue;
                        int b = v.Count;
                        v.Add(GridSpace.GridToWorld(x - .5f, y - .5f, DropY)); v.Add(GridSpace.GridToWorld(x + .5f, y - .5f, DropY));
                        v.Add(GridSpace.GridToWorld(x + .5f, y + .5f, DropY)); v.Add(GridSpace.GridToWorld(x - .5f, y + .5f, DropY));
                        tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                    }
                dropMesh.Clear();
                dropMesh.SetVertices(v); dropMesh.SetTriangles(tri, 0);
                dropMesh.RecalculateBounds();
            }
            if (showCamFov)
            {
                var v = new List<Vector3>(); var tri = new List<int>(); var li = new List<int>(); var lv = new List<Vector3>();
                foreach (var c in model.Objects.cameras)
                {
                    var poly = MapSight.Polygon(d, c.x, c.y, MapSight.CameraRadius);
                    int b = v.Count;
                    v.Add(GridSpace.GridToWorld(c.x, c.y, FovY));
                    foreach (var p in poly) v.Add(GridSpace.GridToWorld(p.x, p.y, FovY));
                    for (int i = 0; i < poly.Count; i++) { tri.Add(b); tri.Add(b + 1 + i); tri.Add(b + 1 + (i + 1) % poly.Count); }
                    int lb = lv.Count;
                    foreach (var p in poly) lv.Add(GridSpace.GridToWorld(p.x, p.y, FovY + .005f));
                    for (int i = 0; i < poly.Count; i++) { li.Add(lb + i); li.Add(lb + (i + 1) % poly.Count); }
                }
                camFovMesh.Clear(); camFovMesh.SetVertices(v); camFovMesh.SetTriangles(tri, 0); camFovMesh.RecalculateBounds();
                camFovLineMesh.Clear(); camFovLineMesh.SetVertices(lv); camFovLineMesh.SetIndices(li, MeshTopology.Lines, 0); camFovLineMesh.RecalculateBounds();
            }
        }

        // ---- cells ----------------------------------------------------------------------------------------------------------

        public void RefreshAll()
        {
            for (int i = 0; i < floorTf.Length; i++) RefreshCell(i);
        }

        /// <summary>Redraws only these cells.</summary>
        public void Refresh(IReadOnlyList<int> indices)
        {
            for (int i = 0; i < indices.Count; i++) RefreshCell(indices[i]);
        }

        void RefreshCell(int i)
        {
            int w = model.Width, x = i % w, y = i / w;
            var t = model.Data.terrain[i];

            bool inside = t == MapTerrain.In;
            floorRenderer[i].sharedMaterial = inside ? inMat : outMat;
            floorTf[i].position = GridSpace.GridToWorld(x, y, inside ? tpl.inY : tpl.outY);

            int want = t == MapTerrain.Solid ? Hard : t == MapTerrain.Breakable ? Wood : t == MapTerrain.Reinforced ? Steel : -1;
            if (wallKind[i] != want)
            {
                if (wallObj[i] != null) { Release(wallKind[i], wallObj[i]); wallObj[i] = null; }
                wallKind[i] = want;
                if (want >= 0) { wallObj[i] = Acquire(want); Place(wallObj[i], want, x, y); }
            }

            bool crate = model.Data.crates[i];
            if (crate && crateObj[i] == null) { crateObj[i] = Acquire(CrateKind); Place(crateObj[i], CrateKind, x, y); }
            else if (!crate && crateObj[i] != null) { Release(CrateKind, crateObj[i]); crateObj[i] = null; }
        }

        void Place(GameObject go, int kind, int x, int y)
        {
            float lift = kind == Steel ? 0f : (kind == Wood && tpl.wood != null ? tpl.wood.transform.localPosition.y
                : kind == Hard && tpl.hard != null ? tpl.hard.transform.localPosition.y
                : kind == CrateKind && tpl.crate != null ? tpl.crate.transform.localPosition.y : 0f);
            go.transform.position = GridSpace.GridToWorld(x, y, lift);
            go.SetActive(true);
        }

        GameObject Acquire(int kind)
        {
            var stack = pool[kind];
            while (stack.Count > 0)
            {
                var go = stack.Pop();
                if (go != null) return go;
            }
            return Create(kind);
        }

        void Release(int kind, GameObject go)
        {
            go.SetActive(false);
            pool[kind].Push(go);
        }

        GameObject Create(int kind)
        {
            GameObject go;
            switch (kind)
            {
                case Steel: go = CreateSteel(); break;
                case Hard: go = Clone(tpl.hard, new Color(.29f, .32f, .37f), new Vector3(1f, 1.5f, 1f)); break;
                case CrateKind: go = Clone(tpl.crate, new Color(.43f, .41f, .28f), new Vector3(.82f, .62f, .82f)); break;
                default: go = Clone(tpl.wood, new Color(.5f, .34f, .2f), new Vector3(1f, 1.2f, 1f)); break;
            }
            go.transform.SetParent(blocks, false);
            return go;
        }

        // A clone of the scene's baked block (colliders off), or a plain box when the scene has none.
        static GameObject Clone(GameObject template, Color fallback, Vector3 fallbackScale)
        {
            GameObject go;
            if (template != null)
            {
                go = Object.Instantiate(template);
                go.transform.rotation = Quaternion.identity;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = fallbackScale;
                go.GetComponent<MeshRenderer>().sharedMaterial = Flat(fallback);
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            go.SetActive(true);
            return go;
        }

        // A wood block re-skinned as the game's reinforced wall (ReinforcedWallViews): steel body, dark frame on top, yellow rail at the foot.
        GameObject CreateSteel()
        {
            var group = new GameObject("Steel");
            var body = Clone(tpl.wood, new Color(.5f, .34f, .2f), new Vector3(1f, 1.2f, 1f));
            body.transform.SetParent(group.transform, false);
            float yc = tpl.wood != null ? tpl.wood.transform.localPosition.y : 0f;
            body.transform.localPosition = new Vector3(0f, yc, 0f);
            foreach (var r in body.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = steelMat;
            float halfH = body.transform.localScale.y * .5f;
            var frame = Box("Frame", group.transform, frameMat);
            frame.transform.localPosition = new Vector3(0f, yc + halfH, 0f);
            frame.transform.localScale = new Vector3(1.04f, .06f, 1.04f);
            var rail = Box("Rail", group.transform, railMat);
            rail.transform.localPosition = new Vector3(0f, Mathf.Max(yc - halfH, 0f) + .05f, 0f);   // the foot sits at the floor, not below it
            rail.transform.localScale = new Vector3(1.03f, .05f, 1.03f);
            return group;
        }

        static GameObject Box(string name, Transform parent, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ---- overlays -------------------------------------------------------------------------------------------------------

        /// <summary>Outlines every panel of the model's current wall layout with a thin line, just inside the panel's cells.</summary>
        public void RebuildPanelLines()
        {
            var v = new List<Vector3>(); var idx = new List<int>();
            const float inset = .06f;
            foreach (var p in model.Panels)
            {
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                foreach (var c in p.cells) { x0 = Mathf.Min(x0, c.x); x1 = Mathf.Max(x1, c.x); y0 = Mathf.Min(y0, c.y); y1 = Mathf.Max(y1, c.y); }
                float ax = x0 - .5f + inset, bx = x1 + .5f - inset, ay = y0 - .5f + inset, by = y1 + .5f - inset;
                int b = v.Count;
                v.Add(new Vector3(ax, PanelLineY, -ay)); v.Add(new Vector3(bx, PanelLineY, -ay));
                v.Add(new Vector3(bx, PanelLineY, -by)); v.Add(new Vector3(ax, PanelLineY, -by));
                idx.AddRange(new[] { b, b + 1, b + 1, b + 2, b + 2, b + 3, b + 3, b });
            }
            panelMesh.Clear();
            panelMesh.SetVertices(v);
            panelMesh.SetIndices(idx, MeshTopology.Lines, 0);
            panelMesh.RecalculateBounds();
        }

        public void SetHover(Vector2Int? cell)
        {
            if (!cell.HasValue) { hoverObj.SetActive(false); return; }
            hoverObj.SetActive(true);
            hoverObj.transform.position = GridSpace.GridToWorld(cell.Value.x, cell.Value.y, HoverY);
        }

        /// <summary>The translucent preview of a stroke about to be drawn: one tinted tile per cell.</summary>
        public void SetPreview(IReadOnlyList<Vector2Int> cells, Color tint)
        {
            var mat = OverlayMat(tint);
            for (int i = 0; i < cells.Count; i++)
            {
                if (i >= previewPool.Count) previewPool.Add(MakeOverlayQuad("Preview", tint, .92f, PreviewY));
                var go = previewPool[i];
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                go.transform.position = GridSpace.GridToWorld(cells[i].x, cells[i].y, PreviewY);
                go.SetActive(true);
            }
            for (int i = cells.Count; i < previewShown; i++) previewPool[i].SetActive(false);
            previewShown = cells.Count;
        }

        public void ClearPreview()
        {
            for (int i = 0; i < previewShown; i++) previewPool[i].SetActive(false);
            previewShown = 0;
        }

        /// <summary>The preview / palette tint of a tool.</summary>
        public static Color TintFor(EditTool t)
        {
            switch (t)
            {
                case EditTool.OutFloor: return new Color(.35f, .78f, .42f, .55f);
                case EditTool.InFloor: return new Color(.93f, .93f, .82f, .6f);
                case EditTool.SolidWall: return new Color(.6f, .63f, .68f, .7f);
                case EditTool.BreakableWall: return new Color(.78f, .52f, .26f, .7f);
                case EditTool.ReinforcedWall: return new Color(.42f, .62f, .85f, .7f);
                case EditTool.Crate: return new Color(.85f, .76f, .3f, .7f);
                case EditTool.Exfil: return new Color(1f, .82f, .12f, .5f);
                default: return new Color(.9f, .2f, .3f, .6f);
            }
        }

        public void Dispose()
        {
            if (Root != null) Object.Destroy(Root);
        }
    }
}
