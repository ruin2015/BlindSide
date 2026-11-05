// Cuts corners off the rect of the Graphic on the same object (clip-path .cut / #oc: polygon(0 0, 100%−c 0, 100% c, 100% 100%, c 100%,
// 0 100%−c) = top-right and bottom-left cut). Each triangle of the Graphic's mesh is clipped against the cut lines
// (Sutherland–Hodgman), so a sprite, a sliced image or a plain colour all work and colours / UVs are interpolated. Work arrays are
// static and reused: nothing is allocated after the first call.
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Cut Corner Graphic")]
    [RequireComponent(typeof(Graphic))]
    public class CutCornerGraphic : BaseMeshEffect
    {
        [Flags] public enum Corners { None = 0, TopLeft = 1, TopRight = 2, BottomRight = 4, BottomLeft = 8, Mockup = TopRight | BottomLeft, All = 15 }

        public Corners corners = Corners.Mockup;
        [Min(0f)] public float cut = 24f;

        public void Set(float amount, Corners which = Corners.Mockup)
        {
            cut = amount; corners = which;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        // static work space (a polygon of a triangle clipped by 4 half-planes has at most 7 vertices)
        static readonly UIVertex[] polyA = new UIVertex[16], polyB = new UIVertex[16];

        static UIVertex Lerp(in UIVertex a, in UIVertex b, float t)
        {
            UIVertex v = default;
            v.position = Vector3.LerpUnclamped(a.position, b.position, t);
            v.normal = Vector3.LerpUnclamped(a.normal, b.normal, t);
            v.tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t);
            v.color = Color32.LerpUnclamped(a.color, b.color, t);
            v.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
            v.uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t);
            v.uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t);
            v.uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t);
            return v;
        }

        // keep the side where  a*x + b*y + k >= 0
        static int Clip(UIVertex[] input, int count, UIVertex[] output, float a, float b, float k)
        {
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                UIVertex cur = input[i], nxt = input[(i + 1) % count];
                float dc = a * cur.position.x + b * cur.position.y + k;
                float dn = a * nxt.position.x + b * nxt.position.y + k;
                if (dc >= 0f) output[n++] = cur;
                if ((dc >= 0f) != (dn >= 0f)) output[n++] = Lerp(cur, nxt, dc / (dc - dn));
            }
            return n;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || corners == Corners.None || cut <= 0f || vh.currentVertCount == 0) return;
            Rect r = graphic.rectTransform.rect;
            float c = Mathf.Min(cut, Mathf.Min(r.width, r.height));
            if (vh.currentIndexCount < 3) return;

            // read the triangles out as a stream into a reused list, then rebuild the helper from the clipped polygons
            var stream = StreamList;
            vh.GetUIVertexStream(stream);
            vh.Clear();

            for (int t = 0; t + 2 < stream.Count; t += 3)
            {
                polyA[0] = stream[t]; polyA[1] = stream[t + 1]; polyA[2] = stream[t + 2];
                int n = 3;
                UIVertex[] from = polyA, to = polyB;
                if ((corners & Corners.TopRight) != 0) { n = Clip(from, n, to, -1f, -1f, r.xMax + r.yMax - c); Swap(ref from, ref to); }
                if (n >= 3 && (corners & Corners.TopLeft) != 0) { n = Clip(from, n, to, 1f, -1f, -r.xMin + r.yMax - c); Swap(ref from, ref to); }
                if (n >= 3 && (corners & Corners.BottomRight) != 0) { n = Clip(from, n, to, -1f, 1f, r.xMax - r.yMin - c); Swap(ref from, ref to); }
                if (n >= 3 && (corners & Corners.BottomLeft) != 0) { n = Clip(from, n, to, 1f, 1f, -r.xMin - r.yMin - c); Swap(ref from, ref to); }
                if (n < 3) continue;
                int start = vh.currentVertCount;
                for (int i = 0; i < n; i++) vh.AddVert(from[i]);
                for (int i = 1; i < n - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
            }
            stream.Clear();
        }

        static void Swap(ref UIVertex[] a, ref UIVertex[] b) { var t = a; a = b; b = t; }

        static System.Collections.Generic.List<UIVertex> streamList;
        static System.Collections.Generic.List<UIVertex> StreamList => streamList ?? (streamList = new System.Collections.Generic.List<UIVertex>(64));
    }
}
