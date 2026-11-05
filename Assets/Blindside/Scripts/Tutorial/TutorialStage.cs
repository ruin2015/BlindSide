// The tutorial's look: an indoor CQB training hall instead of the town. Applied when the tutorial match is set up, taken back when the
// tutorial is disarmed (every change is recorded and restored; the wall materials are also restored by MapWallSync's next Bind).
//   floor        white panels with a faint blue grid on every cell and a glowing blue line every 4 cells (one plane over the hall)
//   map walls    white panels with glowing blue edges, raised to WallHeight (solid walls light grey, HardHeight); reinforced ones
//                are left to ReinforcedWallViews' steel
//   hall         4 tall white walls round the map (Margin cells out) with blue light strips, a dark floor beyond, no sky
//   entrance     a doorway in the west wall (two sliding panels, a glowing frame) and a short dark corridor outside it — where the
//                entrance shot starts; a ceiling with light panels for that first-person shot only (SetCeiling)
// The textures are painted once (procedural), the materials are the URP Lit shader with an emission map.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Map;
using Blindside.Rendering;

namespace Blindside.Tutorial
{
    public class TutorialStage : MonoBehaviour
    {
        public const float WallHeight = 2.4f, HardHeight = 2.6f, HallHeight = 6f, Margin = 3f, DoorWidth = 2.4f, DoorHeight = 3f;
        public const float CorridorLength = 8f, CorridorHeight = 3.2f;
        public static readonly Color Glow = new Color(.18f, .62f, 1f);
        const float GlowHdr = 3.2f;

        /// <summary>The door's centre row (grid y) — FLARE's start row, a little above it.</summary>
        public const float DoorRow = 13.6f;
        /// <summary>The hall's west wall line (grid x).</summary>
        public static float WestWallX => -Margin - .5f;

        Transform root, doorL, doorR, ceiling;
        readonly List<(GameObject go, bool active)> hidden = new List<(GameObject, bool)>();
        readonly List<(Renderer r, bool enabled)> mutedRenderers = new List<(Renderer, bool)>();
        readonly List<(Transform t, Vector3 scale)> raised = new List<(Transform, Vector3)>();
        readonly List<Object> owned = new List<Object>();
        CameraClearFlags camFlags; Color camBg; bool camSaved;
        UnityEngine.Rendering.AmbientMode ambMode; Color ambLight; bool ambSaved;

        static Texture2D floorTex, floorEmi, wallTex, wallEmi, hardTex;

        // ================================================================
        //  Apply / Revert
        // ================================================================
        public static TutorialStage Apply(Transform host, MapGrid map, MapData data)
        {
            var go = new GameObject("TutorialStage");
            go.transform.SetParent(host, false);
            var s = go.AddComponent<TutorialStage>();
            s.root = go.transform;
            s.Build(map, data);
            return s;
        }

        public void Revert()
        {
            foreach (var (t, scale) in raised) if (t != null) t.localScale = scale;
            raised.Clear();
            foreach (var (g, active) in hidden) if (g != null) g.SetActive(active);
            hidden.Clear();
            foreach (var (r, en) in mutedRenderers) if (r != null) r.enabled = en;
            mutedRenderers.Clear();
            var cam = Camera.main;
            if (camSaved && cam != null) { cam.clearFlags = camFlags; cam.backgroundColor = camBg; }
            if (ambSaved) { RenderSettings.ambientMode = ambMode; RenderSettings.ambientLight = ambLight; }
            VisionMeshRenderer.BrightFloor = false;
            foreach (var o in owned) if (o != null) Destroy(o);
            owned.Clear();
            Destroy(gameObject);
        }

        void Build(MapGrid map, MapData data)
        {
            int W = data.width, H = data.height;

            // ---- the town, the sky and the map's own floor planes step aside ----
            Hide(GameObject.Find("SkyDome"));
            Hide(GameObject.Find("IntroSequence/IntroHouse"));
            Hide(GameObject.Find("IntroSequence/House_Exterior"));
            Hide(GameObject.Find("IntroSequence/UnitDrop"));   // the intro's stand-in figure (a white dummy hanging in the air)
            Hide(GameObject.Find("ExfilZones"));               // the tutorial has no extraction
            var floorRoot = GameObject.Find("Blindside_MapRoot/Floor");
            if (floorRoot != null) foreach (var r in floorRoot.GetComponentsInChildren<Renderer>()) Mute(r);
            var cam = Camera.main;
            if (cam != null)
            {
                camFlags = cam.clearFlags; camBg = cam.backgroundColor; camSaved = true;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.06f, .07f, .09f, 1f);
            }
            // indoor light: a bright, even ambient so the white panels stay white on their shaded sides
            VisionMeshRenderer.BrightFloor = true;   // the ally's cone in a deeper, stronger blue on the white floor
            ambMode = RenderSettings.ambientMode; ambLight = RenderSettings.ambientLight; ambSaved = true;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.62f, .65f, .7f);

            // ---- materials ----
            Paint();
            float hallW = W + 2f * Margin, hallH = H + 2f * Margin;
            var floorMat = Lit(floorTex, floorEmi, Color.white, Glow * GlowHdr, new Vector2(hallW / 4f, hallH / 4f), .45f);
            var wallMat = Lit(wallTex, wallEmi, Color.white, Glow * GlowHdr, Vector2.one, .3f);
            // solid (no bullet gets through): dark charcoal with a faint edge — far from the white, blue-lit ordinary walls
            var hardMat = Lit(hardTex, wallEmi, new Color(.2f, .22f, .26f), new Color(.5f, .55f, .62f) * .35f, Vector2.one, .2f);
            var reinforcedMat = Lit(hardTex, wallEmi, new Color(.42f, .44f, .48f), new Color(1f, .62f, .12f) * 2.6f, Vector2.one, .5f);
            var hallMat = Lit(null, null, new Color(.93f, .94f, .96f), Color.black, Vector2.one, .2f);
            var darkMat = Lit(null, null, new Color(.11f, .12f, .14f), Color.black, Vector2.one, .1f);
            var stripMat = Lit(null, null, Color.black, Glow * 4f, Vector2.one, 0f);
            var lightMat = Lit(null, null, Color.black, new Color(1f, .98f, .94f) * 2.6f, Vector2.one, 0f);

            // ---- the hall: floor over the map and its margin, a dark ground beyond ----
            float x0 = -Margin - .5f, x1 = W - .5f + Margin, y0 = -Margin - .5f, y1 = H - .5f + Margin;
            var centre = GridSpace.GridToWorld((x0 + x1) * .5f, (y0 + y1) * .5f, 0f);
            Box("HallFloor", centre + new Vector3(0f, -.06f, 0f), new Vector3(hallW, .1f, hallH), floorMat, false);
            Box("OuterGround", centre + new Vector3(0f, -.2f, 0f), new Vector3(hallW + 160f, .1f, hallH + 160f), darkMat, false);

            // ---- the map's walls: white panels, raised ----
            var walls = GameObject.Find("Blindside_MapRoot/Walls");
            if (walls != null)
                foreach (Transform c in walls.transform)
                {
                    if (!c.gameObject.activeSelf) continue;
                    bool wood = c.name.StartsWith("Wall_Wood_"), hard = c.name.StartsWith("Hard_Block_");
                    if (!wood && !hard) continue;
                    raised.Add((c, c.localScale));
                    var s = c.localScale; s.y = (hard ? HardHeight : WallHeight) * 2f; c.localScale = s;   // (the block is centred on the floor)
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var t = map.TileAt(x, y);
                    if (t == TileType.Hard) MapWallSync.SetMainMaterial(new Vector2Int(x, y), hardMat);
                    else if (t == TileType.Wall)
                    {
                        var p = map.PanelAt(x, y);
                        // reinforced: a dark steel panel with amber edges (ReinforcedWallViews' own steel takes over once it's spotted)
                        MapWallSync.SetMainMaterial(new Vector2Int(x, y), p.HasValue && p.Value.reinforced ? reinforcedMat : wallMat);
                    }
                }

            // ---- the hall's walls (no shadows: they'd darken the whole floor) ----
            float wx = x0 - .2f, ex = x1 + .2f, ny = y0 - .2f, sy = y1 + .2f;
            HallWall(new Vector2(x0 - .4f, ny), new Vector2(x1 + .4f, ny), hallMat, stripMat);                // north
            HallWall(new Vector2(x0 - .4f, sy), new Vector2(x1 + .4f, sy), hallMat, stripMat);                // south
            HallWall(new Vector2(ex, y0), new Vector2(ex, y1), hallMat, stripMat);                            // east
            float dTop = DoorRow - DoorWidth * .5f, dBot = DoorRow + DoorWidth * .5f;
            HallWall(new Vector2(wx, y0), new Vector2(wx, dTop), hallMat, stripMat);                          // west, north of the door
            HallWall(new Vector2(wx, dBot), new Vector2(wx, y1), hallMat, stripMat);                          // west, south of the door
            Box("DoorLintel", GridSpace.GridToWorld(wx, DoorRow, (DoorHeight + HallHeight) * .5f), new Vector3(.4f, HallHeight - DoorHeight, DoorWidth), hallMat, false);

            // ---- the doorway: glowing frame, two sliding panels ----
            float fz = .08f;
            Box("DoorFrameTop", GridSpace.GridToWorld(wx + .21f, DoorRow, DoorHeight + fz * .5f), new Vector3(.03f, fz, DoorWidth + fz * 2f), stripMat, false);
            Box("DoorFrameL", GridSpace.GridToWorld(wx + .21f, dTop - fz * .5f, DoorHeight * .5f), new Vector3(.03f, DoorHeight, fz), stripMat, false);
            Box("DoorFrameR", GridSpace.GridToWorld(wx + .21f, dBot + fz * .5f, DoorHeight * .5f), new Vector3(.03f, DoorHeight, fz), stripMat, false);
            doorL = DoorPanel("DoorPanelL", wallMat, stripMat);
            doorR = DoorPanel("DoorPanelR", wallMat, stripMat);
            SetDoorOpen(0f);

            // ---- the corridor outside the door (dark, lit by a ceiling strip) ----
            float cx0 = wx - .2f - CorridorLength, cx1 = wx - .2f, cw = DoorWidth + .4f;
            var cmid = GridSpace.GridToWorld((cx0 + cx1) * .5f, DoorRow, 0f);
            Box("CorridorFloor", cmid + new Vector3(0f, -.04f, 0f), new Vector3(CorridorLength, .08f, cw), darkMat, true);
            Box("CorridorCeiling", cmid + new Vector3(0f, CorridorHeight + .15f, 0f), new Vector3(CorridorLength + .6f, .3f, cw + .6f), darkMat, true);
            Box("CorridorWallN", cmid + new Vector3(0f, CorridorHeight * .5f, cw * .5f + .15f), new Vector3(CorridorLength, CorridorHeight, .3f), darkMat, true);
            Box("CorridorWallS", cmid + new Vector3(0f, CorridorHeight * .5f, -cw * .5f - .15f), new Vector3(CorridorLength, CorridorHeight, .3f), darkMat, true);
            Box("CorridorEnd", GridSpace.GridToWorld(cx0 - .15f, DoorRow, CorridorHeight * .5f), new Vector3(.3f, CorridorHeight, cw + .6f), darkMat, true);
            Box("CorridorLight", cmid + new Vector3(0f, CorridorHeight - .02f, 0f), new Vector3(CorridorLength - 1f, .04f, .18f), lightMat, false);
            Box("CorridorGuideN", cmid + new Vector3(0f, .12f, cw * .5f - .01f), new Vector3(CorridorLength, .04f, .02f), stripMat, false);
            Box("CorridorGuideS", cmid + new Vector3(0f, .12f, -cw * .5f + .01f), new Vector3(CorridorLength, .04f, .02f), stripMat, false);

            // ---- the ceiling (entrance shot only): grey slab with rows of light panels ----
            ceiling = new GameObject("HallCeiling").transform;
            ceiling.SetParent(root, false);
            Box("CeilingSlab", centre + new Vector3(0f, HallHeight + .15f, 0f), new Vector3(hallW + .8f, .3f, hallH + .8f), hallMat, false, ceiling);
            for (float gy = y0 + 3f; gy < y1 - 1f; gy += 4f)
                for (float gx = x0 + 3f; gx < x1 - 1f; gx += 4f)
                    Box("CeilingLight", GridSpace.GridToWorld(gx, gy, HallHeight - .02f), new Vector3(2.2f, .04f, .5f), lightMat, false, ceiling);
            SetCeiling(false);
        }

        // ================================================================
        //  The entrance's moving parts
        // ================================================================
        /// <summary>0 closed … 1 open: the two panels slide apart along the wall.</summary>
        public void SetDoorOpen(float k)
        {
            float half = DoorWidth * .25f, slide = DoorWidth * .5f * Mathf.Clamp01(k);
            float x = WestWallX - .38f;
            doorL.position = GridSpace.GridToWorld(x, DoorRow - half - slide, DoorHeight * .5f);
            doorR.position = GridSpace.GridToWorld(x, DoorRow + half + slide, DoorHeight * .5f);
        }

        public void SetCeiling(bool on) { if (ceiling != null) ceiling.gameObject.SetActive(on); }

        /// <summary>Grid point in the corridor `metres` short of the door (where the entrance shot starts).</summary>
        public static Vector2 CorridorPoint(float metres) => new Vector2(WestWallX - .2f - metres, DoorRow);

        Transform DoorPanel(string name, Material panelMat, Material stripMat)
        {
            var t = Box(name, Vector3.zero, new Vector3(.12f, DoorHeight, DoorWidth * .5f), panelMat, true).transform;
            var strip = Box(name + "Strip", Vector3.zero, new Vector3(.02f, .05f, DoorWidth * .5f - .1f), stripMat, false).transform;
            strip.SetParent(t, false);
            strip.localPosition = new Vector3(-.07f / .12f, .15f / DoorHeight, 0f);   // (local units of the scaled panel)
            strip.localScale = new Vector3(.02f / .12f, .05f / DoorHeight, (DoorWidth * .5f - .1f) / (DoorWidth * .5f));
            return t;
        }

        void HallWall(Vector2 a, Vector2 b, Material wallMat, Material stripMat)
        {
            var pa = GridSpace.GridToWorld(a.x, a.y, 0f); var pb = GridSpace.GridToWorld(b.x, b.y, 0f);
            var mid = (pa + pb) * .5f;
            bool alongX = Mathf.Abs(pb.x - pa.x) > Mathf.Abs(pb.z - pa.z);
            float len = alongX ? Mathf.Abs(pb.x - pa.x) : Mathf.Abs(pb.z - pa.z);
            var size = alongX ? new Vector3(len, HallHeight, .4f) : new Vector3(.4f, HallHeight, len);
            Box("HallWall", mid + new Vector3(0f, HallHeight * .5f, 0f), size, wallMat, false);
            // light strips at hip height and under the ceiling, on both faces (the inner one is what counts)
            foreach (float h in new[] { 1f, HallHeight - .5f })
            {
                var ss = alongX ? new Vector3(len, .06f, .44f) : new Vector3(.44f, .06f, len);
                Box("HallStrip", mid + new Vector3(0f, h, 0f), ss, stripMat, false);
            }
        }

        GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, bool shadows, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent != null ? parent : root, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        void Hide(GameObject go)
        {
            if (go == null) return;
            hidden.Add((go, go.activeSelf));
            go.SetActive(false);
        }

        void Mute(Renderer r)
        {
            mutedRenderers.Add((r, r.enabled));
            r.enabled = false;
        }

        Material Lit(Texture2D baseTex, Texture2D emiTex, Color baseCol, Color emiCol, Vector2 tiling, float smooth)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (baseTex != null) { m.SetTexture("_BaseMap", baseTex); m.SetTextureScale("_BaseMap", tiling); }
            m.SetColor("_BaseColor", baseCol);
            m.SetFloat("_Smoothness", smooth);
            if (emiCol.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                if (emiTex != null) m.SetTexture("_EmissionMap", emiTex);
                m.SetColor("_EmissionColor", emiCol);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            owned.Add(m);
            return m;
        }

        // ================================================================
        //  Textures (painted once per session)
        // ================================================================
        static void Paint()
        {
            if (floorTex != null) return;
            // floor: 4×4 cells per tile, 64 px per cell — a fine line round every cell, a thicker one round the tile
            const int C = 64, N = C * 4;
            floorTex = NewTex(N); floorEmi = NewTex(N);
            var fb = new Color32[N * N]; var fe = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int cx = x % C, cy = y % C;
                    bool major = x < 2 || y < 2 || x >= N - 2 || y >= N - 2;
                    bool minor = cx < 1 || cy < 1 || cx >= C - 1 || cy >= C - 1;
                    var b = new Color(.9f, .92f, .95f);
                    float e = 0f;
                    if (minor) { b = new Color(.78f, .84f, .92f); e = .18f; }
                    if (major) { b = new Color(.55f, .78f, 1f); e = 1f; }
                    fb[y * N + x] = b; fe[y * N + x] = new Color(e, e, e);
                }
            floorTex.SetPixels32(fb); floorTex.Apply(true, true);
            floorEmi.SetPixels32(fe); floorEmi.Apply(true, true);

            // wall face: white panel, glowing border, a thin inset line
            const int M = 128;
            wallTex = NewTex(M); wallEmi = NewTex(M); hardTex = NewTex(M);
            var wb = new Color32[M * M]; var we = new Color32[M * M]; var hb = new Color32[M * M];
            for (int y = 0; y < M; y++)
                for (int x = 0; x < M; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(M - 1 - x, M - 1 - y));
                    bool edge = d < 4, inset = d == 11 || d == 12;
                    wb[y * M + x] = edge ? new Color(.62f, .82f, 1f) : inset ? new Color(.86f, .9f, .95f) : new Color(.96f, .97f, .98f);
                    float e = edge ? 1f : inset ? .25f : 0f;
                    we[y * M + x] = new Color(e, e, e);
                    hb[y * M + x] = edge ? new Color(.6f, .66f, .74f) : new Color(.84f, .86f, .89f);
                }
            wallTex.SetPixels32(wb); wallTex.Apply(true, true);
            wallEmi.SetPixels32(we); wallEmi.Apply(true, true);
            hardTex.SetPixels32(hb); hardTex.Apply(true, true);
        }

        static Texture2D NewTex(int n) => new Texture2D(n, n, TextureFormat.RGBA32, true)
        { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, hideFlags = HideFlags.HideAndDontSave };
    }
}
