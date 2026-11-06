// The loadout screen's 3D workbench: a table with a blueprint sheet, the selected gun's own model (WeaponData.model) laid on it in
// profile (muzzle left, top up), lit from above and seen by a camera of its own into a RenderTexture (the loadout screen shows it).
// Built at runtime far below the HQ (y −200) so neither camera sees the other's world; its camera renders only while the screen is
// open. The slot points (where each part goes on the gun) come from the model's bounds and the gun's grip / foregrip / sight / butt
// placements (WeaponData) — Anchor(slot) in world space, ViewportOf(slot) for the screen's leader lines. Pose(sheet, gun) is the
// entrance the loadout screen plays: the blueprint unrolls onto the table, then the gun drops onto it.
using UnityEngine;
using Blindside.Data;

namespace Blindside.Hub
{
    public class HubWorkbench : MonoBehaviour
    {
        public const int TexW = 1600, TexH = 900;
        public static readonly Vector3 Origin = new Vector3(0f, -200f, 0f);
        const float GunLength = 1.5f;    // the model is scaled to this many metres (about two thirds of the view's width)

        Camera cam;
        RenderTexture rt;
        Transform gunRoot, sheetT;
        GameObject gun;
        readonly Vector3[] anchors = new Vector3[Attachments.SlotCount];
        static Texture2D blueprint;

        public RenderTexture Texture => rt;
        public Camera View => cam;

        public static HubWorkbench Build(Transform parent = null)
        {
            var go = new GameObject("HubWorkbench");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = Origin;
            var w = go.AddComponent<HubWorkbench>();
            w.BuildScene();
            return w;
        }

        void BuildScene()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            // the table: a dark top, the blueprint sheet on it
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "DecoTable"; Kill(top.GetComponent<Collider>());
            top.transform.SetParent(transform, false);
            top.transform.localPosition = new Vector3(0f, -.04f, 0f); top.transform.localScale = new Vector3(4.4f, .08f, 2.8f);
            var tm = new Material(lit); tm.SetColor("_BaseColor", new Color(.09f, .1f, .12f)); tm.SetFloat("_Smoothness", .35f);
            top.GetComponent<Renderer>().sharedMaterial = tm;

            var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sheet.name = "DecoBlueprint"; Kill(sheet.GetComponent<Collider>());
            sheet.transform.SetParent(transform, false);
            sheet.transform.localPosition = new Vector3(0f, .002f, 0f);
            sheet.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sheet.transform.localScale = new Vector3(3.6f, 2.03f, 1f);
            var sm = new Material(lit);
            sm.SetTexture("_BaseMap", Blueprint()); sm.SetColor("_BaseColor", Color.white); sm.SetFloat("_Smoothness", .15f);
            sheet.GetComponent<Renderer>().sharedMaterial = sm;
            sheetT = sheet.transform;

            // lights: a key from above-front, a cool rim from behind
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Spot; key.range = 6f; key.spotAngle = 70f; key.intensity = 9f; key.color = new Color(1f, .96f, .9f);
            key.shadows = LightShadows.Soft;
            key.transform.localPosition = new Vector3(-.4f, 2.2f, -.9f); key.transform.LookAt(transform.position);
            var rim = new GameObject("Rim").AddComponent<Light>();
            rim.transform.SetParent(transform, false);
            rim.type = LightType.Point; rim.range = 4f; rim.intensity = 3f; rim.color = new Color(.55f, .75f, 1f);
            rim.transform.localPosition = new Vector3(.9f, .8f, 1.1f);

            // the camera: above, tilted a little so the table recedes
            var cgo = new GameObject("WorkbenchCamera");
            cgo.transform.SetParent(transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.fieldOfView = 28f; cam.nearClipPlane = .1f; cam.farClipPlane = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.03f, .035f, .045f);
            cgo.transform.localPosition = new Vector3(0f, 2.55f, -1.05f);
            cgo.transform.localRotation = Quaternion.Euler(68f, 0f, 0f);
            rt = new RenderTexture(TexW, TexH, 24) { name = "WorkbenchRT", antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.enabled = false;

            gunRoot = new GameObject("Gun").transform;
            gunRoot.SetParent(transform, false);
        }

        /// <summary>The camera draws every frame while true (the screen is open).</summary>
        public void SetLive(bool on) { if (cam != null) cam.enabled = on; }

        /// <summary>Draws one frame now (previews in edit mode).</summary>
        public void RenderNow() { if (cam != null) cam.Render(); }

        /// <summary>Lays `w`'s model on the table (a placeholder bar when it has none) and works out the slot points.</summary>
        public void Show(WeaponData w)
        {
            // the slot points are worked out with the gun at rest (an entrance in progress is posed again by the caller)
            if (gunRoot != null) { gunRoot.localPosition = Vector3.zero; gunRoot.localRotation = Quaternion.identity; gunRoot.gameObject.SetActive(true); }
            Kill(gun);
            gun = null;
            float sign = w != null && w.modelForwardSign < 0f ? -1f : 1f;
            if (w != null && w.model != null)
            {
                gun = Instantiate(w.model, gunRoot, false);
                gun.name = "Model_" + w.key;
                foreach (var c in gun.GetComponentsInChildren<Collider>(true)) Kill(c);
                foreach (var mb in gun.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            }
            else
            {
                // no model: a grey bar the size of a rifle (a child cube, so the root is unscaled like a model's)
                gun = new GameObject("Placeholder");
                gun.transform.SetParent(gunRoot, false);
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Kill(bar.GetComponent<Collider>());
                bar.transform.SetParent(gun.transform, false);
                bar.transform.localScale = new Vector3(1f, .18f, .06f);
            }
            gun.transform.localPosition = Vector3.zero; gun.transform.localRotation = Quaternion.identity; gun.transform.localScale = Vector3.one;
            var mesh = LocalBounds(gun);

            // scale to GunLength, lie on its side: barrel → world −X (muzzle left), top → +Z (up the sheet), its side up to the camera
            float s = mesh.size.x > 1e-4f ? GunLength / mesh.size.x : 1f;
            var t = gun.transform;
            t.localScale = Vector3.one * s;
            t.localRotation = Quaternion.LookRotation(new Vector3(0f, sign, 0f), Vector3.forward);
            // centre it on the sheet, resting on it
            t.localPosition = Vector3.zero;
            var center = t.localRotation * (Vector3.Scale(mesh.center, t.localScale));
            float thick = mesh.size.z * s;
            t.localPosition = new Vector3(-center.x, thick * .5f - center.y + .005f, -center.z);

            // the slot points, in the model's own space (as UnitView reads the gun)
            float rearX = sign > 0f ? mesh.min.x : mesh.max.x, frontX = sign > 0f ? mesh.max.x : mesh.min.x;
            float L = mesh.size.x, y0 = mesh.min.y, H = mesh.size.y, z = mesh.center.z;
            float gr = w != null ? w.gripFromRear : .38f, fg = w != null ? w.foregripFromRear : .65f;
            float sh = w != null ? w.sightHeight : .82f, bh = w != null ? w.buttHeight : .55f;
            Vector3 P(float fromRear, float height) => new Vector3(rearX + sign * fromRear * L, y0 + height * H, z);
            anchors[0] = P(.99f, .78f);                                    // マズル: the muzzle
            anchors[1] = P(Mathf.Clamp01((gr + fg) * .5f), Mathf.Min(1f, sh + .1f));   // 光学機器: the top rail
            anchors[2] = P(fg, .12f);                                     // グリップ: under the handguard
            anchors[3] = P(Mathf.Clamp01(gr + .1f), .1f);                 // マガジン: ahead of the grip
            anchors[4] = P(Mathf.Clamp01(fg + (1f - fg) * .55f), .72f);   // バレル: between the handguard and the muzzle
            anchors[5] = P(.03f, bh);                                     // ストック: the butt
            for (int i = 0; i < anchors.Length; i++) anchors[i] = t.TransformPoint(anchors[i]);
        }

        // ---- the entrance (the loadout screen drives it) ------------------------------------------------------------------------
        const float SheetW = 3.6f, SheetH = 2.03f;

        /// <summary>Poses the entrance: `sheet` 0 → 1 unrolls the blueprint from its left edge (rolled up: hidden); `gun` 0 → 1 drops the
        /// gun onto it — from .5 m up, its muzzle end tipped 10° — and settles it with a small overshoot (0: hidden). 1, 1 = at rest.</summary>
        public void Pose(float sheet, float gun)
        {
            if (sheetT != null)
            {
                float w = Mathf.Max(.0001f, SheetW * sheet);
                sheetT.localScale = new Vector3(w, SheetH * Mathf.Lerp(.96f, 1f, sheet), 1f);
                sheetT.localPosition = new Vector3(-SheetW * .5f + w * .5f, .002f + .03f * (1f - sheet), 0f);
                sheetT.gameObject.SetActive(sheet > 0f);
            }
            if (gunRoot != null)
            {
                gunRoot.gameObject.SetActive(gun > 0f);
                float p = Mathf.Clamp01(gun);
                // ease-out-back: a little past the table, then back to rest
                const float c1 = 1.4f, c3 = c1 + 1f;
                float e = 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
                float lift = .5f * (1f - e);
                gunRoot.localPosition = new Vector3(0f, Mathf.Max(-.01f, lift), 0f);
                gunRoot.localRotation = Quaternion.Euler(0f, 0f, -10f * (1f - Mathf.Clamp01(e)));
            }
        }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        /// <summary>Slot `i`'s point on the gun (world).</summary>
        public Vector3 Anchor(int i) => anchors[i];

        /// <summary>Slot `i`'s point in the render's viewport (0–1, origin bottom left).</summary>
        public Vector2 ViewportOf(int i)
        {
            var v = cam.WorldToViewportPoint(anchors[i]);
            return new Vector2(v.x, v.y);
        }

        static Bounds LocalBounds(GameObject g)
        {
            var b = new Bounds(); bool first = true;
            var root = g.transform.worldToLocalMatrix;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = root * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                    var p = m.MultiplyPoint3x4(c);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            foreach (var sm in g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sm.sharedMesh == null) continue;
                var mb = sm.sharedMesh.bounds;
                var m = root * sm.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                    var p = m.MultiplyPoint3x4(c);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            if (first) b = new Bounds(Vector3.zero, new Vector3(1f, .2f, .06f));
            return b;
        }

        /// <summary>The blueprint sheet: navy with a fine grid, a coarser one every 4 cells, a border and a few construction lines.</summary>
        static Texture2D Blueprint()
        {
            if (blueprint != null) return blueprint;
            const int W = 1024, H = 576;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Blueprint", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var px = new Color32[W * H];
            var bg = new Color32(16, 44, 78, 255);
            var fine = new Color32(34, 72, 112, 255);
            var coarse = new Color32(62, 108, 152, 255);
            var ink = new Color32(150, 190, 226, 255);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = bg;
                    if (x % 16 == 0 || y % 16 == 0) c = fine;
                    if (x % 64 == 0 || y % 64 == 0) c = coarse;
                    bool border = x < 18 || y < 18 || x >= W - 18 || y >= H - 18;
                    bool frame = (x >= 18 && x <= 20) || (y >= 18 && y <= 20) || (x >= W - 21 && x <= W - 19) || (y >= H - 21 && y <= H - 19);
                    if (border) c = bg;
                    if (frame) c = ink;
                    // a title block in the bottom right corner
                    if (x > W - 260 && x < W - 21 && y > 21 && y < 90 && (x == W - 259 || y == 89 || y == 55 || x == W - 140)) c = ink;
                    // centre lines (dashed)
                    if ((y == H / 2 || x == W / 2) && ((x + y) / 8) % 2 == 0 && !border) c = coarse;
                    px[y * W + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            blueprint = tex;
            return tex;
        }

        void OnDestroy()
        {
            if (rt != null) { rt.Release(); if (Application.isPlaying) Destroy(rt); else DestroyImmediate(rt); }
        }
    }
}
