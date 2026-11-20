// A flat convex shape in one colour with a soft (anti-aliased) edge: the duel card's plate, colour bars, rule and cells. The HUD canvas
// has no MSAA, so a slanted edge would show steps; here the shape is drawn a little inside its outline at full colour and a thin ring
// out to Feather past it fades to clear. Points are fractions of the rect (x from the left, y from the top). The mesh is made only when
// the graphic is rebuilt (scratch arrays, nothing allocated per frame); CardPerspective's PerspectiveVertex then turns it with the card.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class DuelShape : MaskableGraphic
    {
        public const float DefaultFeather = 1.1f;
        public const int MaxPoints = 8;

        [Tooltip("The outline, fractions of the rect (x from the left, y from the top), clockwise on screen.")]
        public Vector2[] points = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        [Tooltip("The soft edge's width (canvas units).")]
        public float feather = DefaultFeather;

        static readonly Vector2[] scratch = new Vector2[MaxPoints];

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            int n = points == null ? 0 : Mathf.Min(points.Length, MaxPoints);
            if (n < 3 || r.width <= 0f || r.height <= 0f) return;
            for (int i = 0; i < n; i++) scratch[i] = new Vector2(r.xMin + points[i].x * r.width, r.yMax - points[i].y * r.height);
            AddPolygon(vh, scratch, n, color, feather);
        }

        /// <summary>One convex polygon (local points, any winding) in `col`, its edge softened over `feather`.</summary>
        public static void AddPolygon(VertexHelper vh, Vector2[] p, int n, Color32 col, float feather)
        {
            // the centre, to tell outward from inward
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < n; i++) centre += p[i];
            centre /= n;
            Color32 clear = col; clear.a = 0;
            float half = feather * .5f;
            int start = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = p[(i + n - 1) % n], cur = p[i], next = p[(i + 1) % n];
                Vector2 n1 = Outward(prev, cur, centre), n2 = Outward(cur, next, centre);
                Vector2 m = n1 + n2;
                float dot = 1f + Vector2.Dot(n1, n2);
                Vector2 miter = dot > 1e-3f ? m / dot : n1;       // (the offset that moves both edges by 1)
                if (miter.sqrMagnitude > 16f) miter = miter.normalized * 4f;
                v.color = col; v.position = cur - miter * half; vh.AddVert(v);      // inner ring (start + 2i)
                v.color = clear; v.position = cur + miter * half; vh.AddVert(v);    // outer ring (start + 2i + 1)
            }
            // the solid inside: a fan over the inner ring
            for (int i = 1; i < n - 1; i++) vh.AddTriangle(start, start + 2 * i, start + 2 * (i + 1));
            // the soft ring
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int ai = start + 2 * i, ao = ai + 1, bi = start + 2 * j, bo = bi + 1;
                vh.AddTriangle(ai, ao, bo);
                vh.AddTriangle(ai, bo, bi);
            }
        }

        static Vector2 Outward(Vector2 a, Vector2 b, Vector2 centre)
        {
            Vector2 dir = b - a;
            Vector2 nrm = new Vector2(dir.y, -dir.x);
            float len = nrm.magnitude;
            if (len < 1e-5f) return Vector2.zero;
            nrm /= len;
            if (Vector2.Dot(nrm, (a + b) * .5f - centre) < 0f) nrm = -nrm;
            return nrm;
        }

        /// <summary>A DuelShape on a new child of `parent` at (x, y, w, h) (top-left origin), points in rect fractions.</summary>
        public static DuelShape Make(Transform parent, string name, float x, float y, float w, float h, Color c, params Vector2[] pts)
        {
            var rt = HudKit.NewRect(name, parent);
            UILayout.Place(rt, x, y, w, h);
            var s = rt.gameObject.AddComponent<DuelShape>();
            s.raycastTarget = false;
            s.color = c;
            if (pts != null && pts.Length >= 3) s.points = pts;
            return s;
        }
    }
}
