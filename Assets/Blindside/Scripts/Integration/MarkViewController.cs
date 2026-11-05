// The marks half of syncGadgets(t) (blindside_source/03_renderer_threejs.js:203-211) + `.mk` (00_markup_and_styles.html:311):
//   for(const m of G.marks){ if(m.team!==G.side||m.until<=G.phaseCount) continue; …
//     r1=RingGeometry(.3,.38,32) 0xff3b54 at y .07 · pin=ConeGeometry(.12,.3,4) 0xff3b54, rotation.x=PI, y 1.1 ·
//     label .mk = 銃声 | 破壊音 | 敵を捕捉
//     hidden = some living enemy within .5 of the mark is enemyVisible(G.side) → mark + label hidden
//     ph=((t-m.t0)/900)%1: r1 scale 1+ph*1.5, opacity 1-ph · pin y 1.1+sin(t/200)*.08 · label at proj(m.x,1.35,m.y)
// .mk{transform:translate(-50%,-100%);font:700 9px var(--fk);color:#fff;background:var(--foe);padding:1px 6px}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.Rendering;
using Blindside.UI;
using Blindside.Vision;

namespace Blindside.Integration
{
    public class MarkViewController : MonoBehaviour
    {
        static readonly Color MarkRed = new Color32(0xff, 0x3b, 0x54, 0xff);   // 0xff3b54

        MarkBoard board;
        VisibilityController visibility;
        IReadOnlyList<SimUnit> units;

        class View
        {
            public GameObject root;
            public Transform ring, pin;
            public Material ringMat;
            public RectTransform label;
        }
        readonly Dictionary<Mark, View> views = new Dictionary<Mark, View>();
        readonly HashSet<Mark> live = new HashSet<Mark>();
        readonly List<Mark> drop = new List<Mark>();

        Canvas canvas;
        Mesh ringMesh, pinMesh;
        Material pinMat;

        public void Bind(MarkBoard board, VisibilityController visibility, IReadOnlyList<SimUnit> units)
        {
            Unbind();
            this.board = board; this.visibility = visibility; this.units = units;
        }

        public void Unbind()
        {
            foreach (var v in views.Values) DestroyView(v);
            views.Clear();
            board = null; visibility = null; units = null;
        }

        void OnDestroy() => Unbind();

        /// <summary>The `.mk` label canvas — dimmed with the HUD during a battle cinematic.</summary>
        public GameObject LabelCanvas { get { EnsureShared(); return canvas.gameObject; } }

        void LateUpdate()
        {
            if (board == null || visibility == null || !visibility.Active) return;
            var side = visibility.Side;
            float t = Time.unscaledTime * 1000f;
            var cam = Camera.main;

            live.Clear();
            foreach (var m in board.Marks)
            {
                if (m.Team != side || !board.IsLive(m)) continue;
                live.Add(m);
                if (!views.TryGetValue(m, out var v)) { v = BuildView(m); views[m] = v; }

                bool hidden = false;
                foreach (var u in units)
                    if (u.Alive && u.Team != side && Hypot(u.X - m.X, u.Y - m.Y) < .5f && visibility.EnemyVisibleToPlayer(u)) { hidden = true; break; }
                v.root.SetActive(!hidden);
                if (hidden) { v.label.gameObject.SetActive(false); continue; }

                v.root.transform.position = GridSpace.GridToWorld(m.X, m.Y);
                float ph = ((t - m.T0 * 1000f) / 900f) % 1f;
                float s = 1f + ph * 1.5f;
                v.ring.localScale = new Vector3(s, 1f, s);
                SetColor(v.ringMat, new Color(MarkRed.r, MarkRed.g, MarkRed.b, 1f - ph));
                v.pin.localPosition = new Vector3(0f, 1.1f + Mathf.Sin(t / 200f) * .08f, 0f);

                var sp = cam != null ? cam.WorldToScreenPoint(GridSpace.GridToWorld(m.X, m.Y, 1.35f)) : new Vector3(0, 0, -1);
                bool onScreen = sp.z > 0f;
                v.label.gameObject.SetActive(onScreen);
                if (onScreen)
                {
                    float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
                    v.label.anchoredPosition = new Vector2(sp.x / sf, sp.y / sf);
                }
            }

            drop.Clear();
            foreach (var kv in views) if (!live.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var m in drop) { DestroyView(views[m]); views.Remove(m); }
        }

        static float Hypot(float dx, float dy) => Mathf.Sqrt(dx * dx + dy * dy);

        // ================================================================
        //  Construction
        // ================================================================
        View BuildView(Mark m)
        {
            EnsureShared();
            var v = new View();
            v.root = new GameObject("Mark_" + m.Kind);
            v.root.transform.SetParent(transform, false);

            // r1: RingGeometry(.3,.38,32), mkBasic(0xff3b54,{transparent:true,side:DoubleSide}), y .07
            var ring = MakeMeshObject("Ring", v.root.transform, ringMesh, NewUnlit(MarkRed, true, 3005));
            ring.transform.localPosition = new Vector3(0f, .07f, 0f);
            v.ring = ring.transform;
            v.ringMat = ring.GetComponent<MeshRenderer>().sharedMaterial;

            // pin: ConeGeometry(.12,.3,4) mkBasic(0xff3b54), rotation.x=PI (point down), y 1.1
            var pin = MakeMeshObject("Pin", v.root.transform, pinMesh, pinMat);
            v.pin = pin.transform;

            // .mk label
            var rt = new GameObject("Mk", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(.5f, 0f);                    // translate(-50%,-100%)
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = UIPalette.Foe; bg.raycastTarget = false;
            var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(6, 6, 1, 1); hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var txtRt = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            txtRt.SetParent(rt, false);
            var tmp = txtRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = m.Kind == MarkKind.Shot ? "銃声" : m.Kind == MarkKind.Breach ? "破壊音" : m.Kind == MarkKind.Gadget ? "設置物を捕捉" : "敵を捕捉";
            tmp.fontSize = 9; tmp.color = Color.white;
            UIFonts.Auto(tmp, true);
            tmp.alignment = TextAlignmentOptions.Center; tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            v.label = rt;
            return v;
        }

        static void DestroyView(View v)
        {
            if (v.root != null) Destroy(v.root);
            if (v.ringMat != null) Destroy(v.ringMat);
            if (v.label != null) Destroy(v.label.gameObject);
        }

        void EnsureShared()
        {
            if (canvas == null)
            {
                // Labels only (no GraphicRaycaster — `.mk` never takes pointer input). Same reference
                // resolution as the selection HUD's labels so "9px" matches them.
                var go = new GameObject("Marks_Canvas", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1;
                UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            }
            if (ringMesh == null) ringMesh = BuildRing(.3f, .38f, 32);
            if (pinMesh == null) pinMesh = BuildPin(.12f, .3f, 4);
            if (pinMat == null) pinMat = NewUnlit(MarkRed, false, 2000);
        }

        static GameObject MakeMeshObject(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        static Material NewUnlit(Color c, bool transparent, int queue)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            mat.SetFloat("_Cull", 0f);   // side:THREE.DoubleSide
            SetColor(mat, c);
            mat.renderQueue = queue;
            return mat;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        /// <summary>RingGeometry(inner,outer,segments) laid flat (local XZ).</summary>
        static Mesh BuildRing(float inner, float outer, int segments)
        {
            var verts = new Vector3[segments * 2]; var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = d * inner; verts[i * 2 + 1] = d * outer;
            }
            for (int i = 0, k = 0; i < segments; i++)
            {
                int i0 = i * 2, o0 = i0 + 1, i1 = ((i + 1) % segments) * 2, o1 = i1 + 1;
                tris[k++] = i0; tris[k++] = o1; tris[k++] = i1;
                tris[k++] = i0; tris[k++] = o0; tris[k++] = o1;
            }
            var mesh = new Mesh { name = "MarkRing", vertices = verts, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>ConeGeometry(radius,height,radialSegments) turned tip-down (rotation.x=PI): base at
        /// +height/2, tip at -height/2 around the local origin — the pin's y is its centre, like three.js.</summary>
        static Mesh BuildPin(float radius, float height, int segments)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            float top = height / 2f, tip = -height / 2f;
            var rim = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                rim[i] = new Vector3(Mathf.Sin(a) * radius, top, Mathf.Cos(a) * radius);
            }
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = rim[i], b = rim[(i + 1) % segments];
                int v = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(new Vector3(0f, tip, 0f));     // side facet
                tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
                v = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(new Vector3(0f, top, 0f));     // base cap
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
            }
            var mesh = new Mesh { name = "MarkPin" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
