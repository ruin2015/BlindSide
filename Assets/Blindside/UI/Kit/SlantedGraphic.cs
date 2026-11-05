// Turns the rect of the Image / RawImage / Graphic on the same object into the mockup's parallelogram (clip-path .slant / .slantL):
//   Right (.slant)   top edge [S, W], bottom edge [0, W−S]
//   Left  (.slantL)  top edge [0, W−S], bottom edge [S, W]
// S comes from UITheme (Large 22, Card 14…16, Small 10…12) or is custom. The mesh is sheared in ModifyMesh with
// PopulateUIVertex / SetUIVertex only (no lists, no arrays: nothing is allocated). The same shape answers raycasts, so a slanted
// button's empty corners don't take clicks. As the graphic of a Mask it gives the stencil a parallelogram (see SlantedMask).
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Slanted Graphic")]
    [RequireComponent(typeof(Graphic))]
    public class SlantedGraphic : BaseMeshEffect, ICanvasRaycastFilter
    {
        public enum Size { Large, CardMin, CardMax, SmallMin, SmallMax, Custom }
        /// <summary>Right / Left: both side edges lean. LeftEdge: only the left edge leans (top [S, W], bottom [0, W] — #spot .art, .sq .s).
        /// RightEdge: only the right edge leans (top [0, W], bottom [0, W−S] — #spot .nm em).</summary>
        public enum Direction { Right, Left, LeftEdge, RightEdge }

        public Size size = Size.Large;
        [Tooltip("Used when size is Custom (canvas units).")]
        public float custom = 22f;
        public Direction direction = Direction.Right;

        /// <summary>The slant S in canvas units.</summary>
        public float Amount
        {
            get
            {
                var t = UITheme.Current;
                switch (size)
                {
                    case Size.Large: return t.slantLarge;
                    case Size.CardMin: return t.slantCardMin;
                    case Size.CardMax: return t.slantCardMax;
                    case Size.SmallMin: return t.slantSmallMin;
                    case Size.SmallMax: return t.slantSmallMax;
                    default: return custom;
                }
            }
        }

        public void Set(Size newSize, Direction newDirection = Direction.Right, float customAmount = 0f)
        {
            size = newSize; direction = newDirection; if (newSize == Size.Custom) custom = customAmount;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        // x of the left and right edge at height ny (0 bottom … 1 top), for a rect of width w and slant s
        static void Edges(Direction dir, float w, float s, float ny, out float x0, out float x1)
        {
            switch (dir)
            {
                case Direction.Right: x0 = s * ny; x1 = w - s * (1f - ny); break;
                case Direction.LeftEdge: x0 = s * ny; x1 = w; break;
                case Direction.RightEdge: x0 = 0f; x1 = w - s * (1f - ny); break;
                default: x0 = s * (1f - ny); x1 = w - s * ny; break;
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            Rect r = graphic.rectTransform.rect;
            float w = r.width, h = r.height;
            if (w <= 0.001f || h <= 0.001f) return;
            float s = Mathf.Min(Amount, w * 0.5f);
            UIVertex v = default;
            for (int i = 0, n = vh.currentVertCount; i < n; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float nx = (v.position.x - r.xMin) / w;
                float ny = Mathf.Clamp01((v.position.y - r.yMin) / h);
                Edges(direction, w, s, ny, out float x0, out float x1);
                v.position.x = r.xMin + Mathf.LerpUnclamped(x0, x1, nx);
                vh.SetUIVertex(v, i);
            }
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            var rt = graphic.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, eventCamera, out Vector2 p)) return false;
            Rect r = rt.rect;
            if (!r.Contains(p)) return false;
            float ny = (p.y - r.yMin) / r.height;
            Edges(direction, r.width, Mathf.Min(Amount, r.width * 0.5f), ny, out float x0, out float x1);
            float x = p.x - r.xMin;
            return x >= x0 && x <= x1;
        }
    }
}
