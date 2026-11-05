// The phase banner's band (#bn .b: clip-path polygon(0 16%, 100% 0, 100% 84%, 0 100%)) as a four-point polygon whose corners are set in
// fractions of the rect (x from the left, y from the top), so the reveal / fold keyframes of bnIn are poses of the corners:
//   Set(ax,ay, bx,by, cx,cy, dx,dy)   A top-left, B top-right, C bottom-right, D bottom-left
// A plain Graphic mesh (4 vertices, no allocation); a Deco layer, so name its object Deco….
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Banner Strip")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class KitBannerStrip : MaskableGraphic
    {
        float ax, ay = .16f, bx = 1f, by, cx = 1f, cy = .84f, dx, dy = 1f;

        public void Set(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy)
        {
            if (this.ax == ax && this.ay == ay && this.bx == bx && this.by == by && this.cx == cx && this.cy == cy && this.dx == dx && this.dy == dy) return;
            this.ax = ax; this.ay = ay; this.bx = bx; this.by = by; this.cx = cx; this.cy = cy; this.dx = dx; this.dy = dy;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Color32 c = color;
            vh.AddVert(P(r, ax, ay), c, Vector4.zero);
            vh.AddVert(P(r, bx, by), c, Vector4.zero);
            vh.AddVert(P(r, cx, cy), c, Vector4.zero);
            vh.AddVert(P(r, dx, dy), c, Vector4.zero);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }

        static Vector3 P(Rect r, float fx, float fy) => new Vector3(r.xMin + fx * r.width, r.yMax - fy * r.height, 0f);
    }
}
