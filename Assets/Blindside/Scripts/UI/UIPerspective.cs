// A UI panel drawn as if turned away about its left edge (the left side near, the right side far, everything shrinking toward the
// panel's horizontal middle line) — on an overlay canvas, which has no perspective of its own. Every Graphic under the root gets
// its vertices bent: Images (and other uGUI graphics) through UIPerspectiveVertex (a BaseMeshEffect), TextMeshPro texts through
// their OnPreRenderText hook. The bend is in the root's own space (pivot at its left middle), so moving / scaling the root doesn't
// need a rebuild; a child's mesh is re-bent whenever it rebuilds (text / size change). Nothing runs per frame.
// Pointer hits use the unbent rects — keep buttons outside the bent root.
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    public class UIPerspective : MonoBehaviour
    {
        /// <summary>How far the panel is turned (degrees, the right side away) and the eye's distance (root units).</summary>
        public float angle = 16f, eye = 1500f;
        RectTransform root;
        float cos, sin;

        /// <summary>Bends every Graphic under this object (call once after building it).</summary>
        public void Apply()
        {
            root = (RectTransform)transform;
            cos = Mathf.Cos(angle * Mathf.Deg2Rad); sin = Mathf.Sin(angle * Mathf.Deg2Rad);
            foreach (var g in GetComponentsInChildren<Graphic>(true))
            {
                if (g is TMP_Text t)
                {
                    var tt = t;
                    tt.OnPreRenderText += info => BendText(tt, info);
                    tt.havePropertiesChanged = true;
                    continue;
                }
                var v = g.GetComponent<UIPerspectiveVertex>();
                if (v == null) v = g.gameObject.AddComponent<UIPerspectiveVertex>();
                v.warp = this;
                g.SetVerticesDirty();
            }
        }

        /// <summary>A point in the root's space (relative to its pivot, the left middle) → where it is drawn.</summary>
        public Vector3 Bend(Vector3 p)
        {
            float z = p.x * sin;
            float s = eye / Mathf.Max(1f, eye + z);
            return new Vector3(p.x * cos * s, p.y * s, p.z);
        }

        public Matrix4x4 ToRoot(Transform child) => root.worldToLocalMatrix * child.localToWorldMatrix;

        void BendText(TMP_Text t, TMP_TextInfo info)
        {
            if (root == null) return;
            var m = ToRoot(t.transform); var inv = m.inverse;
            for (int i = 0; i < info.meshInfo.Length; i++)
            {
                var verts = info.meshInfo[i].vertices;
                if (verts == null) continue;
                int n = Mathf.Min(info.meshInfo[i].vertexCount, verts.Length);
                for (int k = 0; k < n; k++) verts[k] = inv.MultiplyPoint3x4(Bend(m.MultiplyPoint3x4(verts[k])));
            }
        }
    }

    /// <summary>UIPerspective's bend on one uGUI graphic's mesh.</summary>
    [RequireComponent(typeof(Graphic))]
    public class UIPerspectiveVertex : BaseMeshEffect
    {
        public UIPerspective warp;
        UIVertex v;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || warp == null) return;
            var m = warp.ToRoot(transform); var inv = m.inverse;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position = inv.MultiplyPoint3x4(warp.Bend(m.MultiplyPoint3x4(v.position)));
                vh.SetUIVertex(v, i);
            }
        }
    }
}
