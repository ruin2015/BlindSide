// A card shown in perspective (the user's design: the duel card as a flat plate seen at an angle). The HUD canvases are flat (Screen
// Space Overlay: no perspective), so the view is put into the meshes themselves:
//   CardPerspective   on the card's root: where the corners of its rect show (TopLeft … BottomLeft, as fractions of the rect's width and
//                     height, y from the top; the design's own corners), mirrored left-right for a card on the right. Project maps a
//                     point of the rect through that projective map (straight lines stay straight, the far side shrinks)
//   PerspectiveVertex a BaseMeshEffect on every Graphic of the card (added last): its vertices go through Project
//   PerspectiveText   the same for TMP texts, after each rebuild
// Apply(root) adds the effects to everything under the card once it is built. Meshes change only when a graphic or a text is rebuilt;
// moving the card (its slide-in) leaves them as they are. Nothing is allocated per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [DisallowMultipleComponent]
    public class CardPerspective : MonoBehaviour
    {
        [Tooltip("Where the rect's corners show: fractions of its width (x) and height (y, from the top).")]
        public Vector2 topLeft = new Vector2(0f, 0f), topRight = new Vector2(1f, 0f), bottomRight = new Vector2(1f, 1f), bottomLeft = new Vector2(0f, 1f);
        [Tooltip("Mirrored left-right (a card on the right of the screen).")]
        public bool mirror;

        RectTransform rt;
        Rect rect;
        // the unit square → corners map: X = (a u + b v + c) / (g u + h v + 1), Y = (d u + e v + f) / (g u + h v + 1)
        float a, b, c, d, e, f, g, h;
        Vector2 k0, k1, k2, k3;
        bool solved;

        public RectTransform Rt => rt != null ? rt : rt = (RectTransform)transform;

        void Prepare()
        {
            rect = Rt.rect;
            if (solved && k0 == topLeft && k1 == topRight && k2 == bottomRight && k3 == bottomLeft) return;
            k0 = topLeft; k1 = topRight; k2 = bottomRight; k3 = bottomLeft;
            // (Heckbert's square-to-quad)
            float sx = k0.x - k1.x + k2.x - k3.x, sy = k0.y - k1.y + k2.y - k3.y;
            if (Mathf.Abs(sx) < 1e-6f && Mathf.Abs(sy) < 1e-6f) { g = h = 0f; }
            else
            {
                float dx1 = k1.x - k2.x, dx2 = k3.x - k2.x, dy1 = k1.y - k2.y, dy2 = k3.y - k2.y;
                float den = dx1 * dy2 - dx2 * dy1;
                g = (sx * dy2 - dx2 * sy) / den;
                h = (dx1 * sy - sx * dy1) / den;
            }
            a = k1.x - k0.x + g * k1.x; b = k3.x - k0.x + h * k3.x; c = k0.x;
            d = k1.y - k0.y + g * k1.y; e = k3.y - k0.y + h * k3.y; f = k0.y;
            solved = true;
        }

        /// <summary>A point of the card's rect (its local space) where the card in perspective shows it.</summary>
        public Vector3 Project(Vector3 p)
        {
            Prepare();
            if (rect.width <= 0f || rect.height <= 0f) return p;
            float u = (p.x - rect.xMin) / rect.width, v = (rect.yMax - p.y) / rect.height;
            if (mirror) u = 1f - u;
            float w = g * u + h * v + 1f;
            float X = (a * u + b * v + c) / w, Y = (d * u + e * v + f) / w;
            if (mirror) X = 1f - X;
            return new Vector3(rect.xMin + X * rect.width, rect.yMax - Y * rect.height, p.z);
        }

        /// <summary>Adds the perspective to every Graphic and TMP text under `root` (its CardPerspective set up first).</summary>
        public static void Apply(Transform root)
        {
            foreach (var gr in root.GetComponentsInChildren<Graphic>(true))
            {
                if (gr is TMP_Text) continue;
                if (gr.GetComponent<PerspectiveVertex>() == null) gr.gameObject.AddComponent<PerspectiveVertex>();
            }
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                if (t.GetComponent<PerspectiveText>() == null) t.gameObject.AddComponent<PerspectiveText>();
        }
    }
}
