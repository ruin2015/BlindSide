// A flat translucent colour over a shape (the card's hit flash, an overlay), drawn by the shared KitDeco material in its own
// parallelogram (slant / slantDirection or MatchShape). The alpha is blended to the mockup's sRGB look over `over` (the colour under
// it); nothing is drawn while the alpha is 0. A decoration: name it Deco….
// `screen` on: composited like CSS mix-blend-mode: screen (alpha = opacity) by the shared "Blindside/UI/KitScreen" material — light
// areas under it barely change, dark ones (text) take the colour, as in the mockup's hit flash.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Tint")]
    public class KitTint : KitShaderGraphic
    {
        public Color tint = new Color(1f, 1f, 1f, 0f);
        [SerializeField] bool screen;

        static Material screenShared;
        static Material ScreenShared
        {
            get
            {
                if (screenShared == null)
                    screenShared = new Material(Shader.Find("Blindside/UI/KitScreen")) { name = "KitScreen (shared)", hideFlags = HideFlags.HideAndDontSave };
                return screenShared;
            }
        }

        public bool Screen
        {
            get => screen;
            set { if (screen == value) return; screen = value; SetMaterialDirty(); }
        }

        public override Material defaultMaterial => screen ? ScreenShared : base.defaultMaterial;

        /// <summary>New tint colour (sRGB values with alpha) and the colour it sits on; re-meshes only when something changed.</summary>
        public void Set(Color c, Color under)
        {
            if (c == tint && under == over) return;
            bool was = Visible;
            tint = c; over = under;
            if (was || Visible) SetVerticesDirty();
        }

        protected override bool Visible => tint.a > 0f;
        protected override float Mode => ModeFlat;
        protected override Color ColorA => tint;

        protected override void Build(VertexHelper vh, Rect r, float s, ref UIVertex v)
        {
            AddQuad(vh, r, s, ref v, 0f, 0f, r.width - s, r.height);
        }
    }
}
