// Port of mkPoly()/setPoly(o,u,pts) and the styling half of stylePolys() from
// blindside_source/03_renderer_threejs.js (~lines 155-160, 173). The source builds a
// THREE.Mesh (triangle fan, one (apex,a,b) triple per edge) plus a THREE.LineLoop outline;
// this component owns both meshes as a single dynamic-mesh renderer per the request.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Vision;

namespace Blindside.Rendering
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VisionMeshRenderer : MonoBehaviour
    {
        // setPoly: fill at .04, outline at .045 — above the floor (tops out at .02) and the fog/overlay
        // planes (.02/.03). renderOrder 3/4 → Transparent+3/+4, after the reach overlay's +2.
        const float FillY = 0.04f, LineY = 0.045f;
        const int FillQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 3;
        const int LineQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 4;

        [SerializeField] LineRenderer outline; // optional: auto-created if missing

        Mesh mesh;
        MeshRenderer mr;

        static readonly Color32 AllyColor = new Color32(0x2f, 0xe4, 0xff, 0xff);
        static readonly Color32 EnemyColor = new Color32(0xff, 0x3b, 0x54, 0xff);

        void Awake()
        {
            mesh = new Mesh { name = "VisionPolygon" };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            mr = GetComponent<MeshRenderer>();
            if (mr.sharedMaterial == null) mr.material = MakeUnlitTransparent(FillQueue);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            if (outline == null) outline = GetComponentInChildren<LineRenderer>();
            if (outline == null)
            {
                var go = new GameObject("Outline");
                go.transform.SetParent(transform, false);
                outline = go.AddComponent<LineRenderer>();
                outline.loop = true;
                outline.useWorldSpace = false;
                outline.widthMultiplier = 0.03f;
                outline.material = MakeUnlitTransparent(LineQueue);
                outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // mkBasic(..,{transparent:true,depthWrite:false,side:THREE.DoubleSide}): GridToWorld's z=-y mirror
        // flips every triangle's winding, so culling is off rather than relying on the index swap alone.
        static Material MakeUnlitTransparent(int queue)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = queue;
            return mat;
        }

        /// <summary>Convenience wrapper: recompute the polygon straight from VisionSystem and apply it.</summary>
        public void Rebuild(VisionSystem vision, IVisionAgent unit)
        {
            SetPolygon(new Vector2(unit.X, unit.Y), vision.VisionPolygon(unit));
        }

        /// <summary>
        /// Port of setPoly(o,u,pts): one (apex,a,b) triangle per consecutive point pair, apex fixed
        /// at the viewer's own position. `apexXZ` and each point in `pts` are grid (x,y) pairs —
        /// converted to world space via GridSpace at the point of use, same as everything else here.
        /// </summary>
        public void SetPolygon(Vector2 apexXZ, IReadOnlyList<Vector2> pts)
        {
            int n = pts.Count;
            mesh.Clear();
            if (n == 0)
            {
                if (outline) outline.positionCount = 0;
                return;
            }

            var verts = new Vector3[n * 3];
            var tris = new int[n * 3];
            for (int i = 0; i < n; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % n];
                int v = i * 3;
                verts[v] = GridSpace.GridToWorld(apexXZ.x, apexXZ.y, FillY);
                verts[v + 1] = GridSpace.GridToWorld(a.x, a.y, FillY);
                verts[v + 2] = GridSpace.GridToWorld(b.x, b.y, FillY);
                // GridToWorld mirrors Z, which flips every triangle's winding (a reflection always
                // inverts orientation) — swap the last two indices here to keep the fill facing up.
                tris[v] = v; tris[v + 1] = v + 2; tris[v + 2] = v + 1;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (outline)
            {
                outline.positionCount = n;
                for (int i = 0; i < n; i++)
                    outline.SetPosition(i, GridSpace.GridToWorld(pts[i].x, pts[i].y, LineY));
            }
        }

        /// <summary>
        /// Port of stylePolys(): ally polygons are cyan (brighter + more opaque outline when the
        /// unit is selected), enemy polygons are a flat, dimmer red.
        /// </summary>
        /// <summary>The floor under the cones is bright (the tutorial's white training hall): the ally cone is drawn a deeper blue and
        /// stronger, or the pale cyan would vanish into it.</summary>
        public static bool BrightFloor;
        static readonly Color32 AllyOnBright = new Color32(0x14, 0x5c, 0xe6, 0xff);

        public void SetStyle(bool isAlly, bool selected)
        {
            Color32 baseColor = isAlly ? (BrightFloor ? AllyOnBright : AllyColor) : EnemyColor;
            float fillAlpha = isAlly ? (selected ? 0.2f : 0.07f) : 0.14f;
            float lineAlpha = isAlly ? (selected ? 0.85f : 0.3f) : 0.6f;
            if (BrightFloor && isAlly) { fillAlpha = selected ? .3f : .24f; lineAlpha = 1f; }

            var fill = (Color)baseColor; fill.a = fillAlpha;
            SetMatColor(mr.material, fill);

            if (outline)
            {
                var line = (Color)baseColor; line.a = lineAlpha;
                SetMatColor(outline.material, line);
                outline.startColor = line;
                outline.endColor = line;
            }
        }

        static void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
    }
}
