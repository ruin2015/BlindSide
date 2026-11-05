// A colour that fades out to the right (CSS linear-gradient(90deg, C 0 solidTo, transparent clearAt)), drawn by the shared KitDeco
// material: the alpha is full up to `solidTo`, falls linearly to 0 at `clearAt` (CSS interpolates premultiplied, so only the alpha
// ramps) and is matched per pixel to the mockup's sRGB blending over `over` (the colour under the ramp). A decoration: name it Deco….
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Gradient")]
    public class KitGradient : KitShaderGraphic
    {
        public Color tint = new Color(1f, 1f, 1f, 1f);
        [Range(0f, 1f)] public float solidTo = .56f;
        [Range(0f, 1f)] public float clearAt = .88f;

        /// <summary>New colour (sRGB values with the full alpha) and the colour under the ramp; re-meshes only when something changed.</summary>
        public void Set(Color c, Color under)
        {
            if (c == tint && under == over) return;
            tint = c; over = under;
            SetVerticesDirty();
        }

        protected override bool Visible => tint.a > 0f;
        protected override float Mode => ModeFlat;
        protected override Color ColorA => tint;

        protected override void Build(VertexHelper vh, Rect r, float s, ref UIVertex v)
        {
            float w = r.width - s;
            float x0 = Mathf.Clamp01(solidTo) * w, x1 = Mathf.Max(x0, Mathf.Clamp01(clearAt) * w);
            if (x0 > 0f) AddQuad(vh, r, s, ref v, 0f, 0f, x0, r.height);
            if (x1 > x0)
            {
                int start = vh.currentVertCount;
                AddQuad(vh, r, s, ref v, x0, 0f, x1, r.height);
                // AddQuad's vertex order: 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right — the right pair goes clear
                UIVertex q = default;
                for (int i = 2; i < 4; i++)
                {
                    vh.PopulateUIVertex(ref q, start + i);
                    q.uv2.w = 0f;
                    vh.SetUIVertex(q, start + i);
                }
            }
        }
    }
}
