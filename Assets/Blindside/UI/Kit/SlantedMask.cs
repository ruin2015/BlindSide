// A parallelogram face frame (.por: clip-path polygon(S 0, 100% 0, calc(100% − S) 100%, 0 100%)). The frame is an Image + SlantedGraphic +
// Mask(showMaskGraphic off), so the stencil is the slanted shape; its children are NOT slanted. The photo is a RawImage with an
// AspectRatioFitter (Envelope Parent) whose pivot is the top-centre, so a square face is scaled to cover the frame without
// distortion and any overflow is cropped from the bottom (the top of the face stays in view).
//   Frame (Image, SlantedGraphic, Mask, SlantedMask)
//     DecoBackdrop   paper gradient colour behind the photo (decoration only)
//     Photo          RawImage + AspectRatioFitter(Envelope Parent), pivot (.5, 1)
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Slanted Mask")]
    [RequireComponent(typeof(Image), typeof(SlantedGraphic))]
    public class SlantedMask : MonoBehaviour
    {
        public SlantedGraphic slant;
        public RawImage photo;
        public AspectRatioFitter fitter;

        /// <summary>Builds the frame under `parent` at `size`; the caller positions it.</summary>
        public static SlantedMask Create(Transform parent, string name, Vector2 size, SlantedGraphic.Size slantSize,
            Texture texture = null, SlantedGraphic.Direction direction = SlantedGraphic.Direction.Right)
        {
            var rt = HudKit.NewRect(name, parent);
            rt.sizeDelta = size;
            var frame = rt.gameObject.AddComponent<Image>();
            frame.color = Color.white; frame.raycastTarget = false;
            var sg = rt.gameObject.AddComponent<SlantedGraphic>();
            sg.Set(slantSize, direction);
            var mask = rt.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var sm = rt.gameObject.AddComponent<SlantedMask>();
            sm.slant = sg;

            var back = HudKit.NewRect("DecoBackdrop", rt);
            HudKit.Stretch(back);
            HudKit.AddImage(back, HudKit.PaperGrad2);

            var pr = HudKit.NewRect("Photo", rt);
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
            pr.pivot = new Vector2(0.5f, 1f);           // top-aligned: the fitter keeps the pivot on the parent's top edge
            pr.offsetMin = pr.offsetMax = Vector2.zero;
            var raw = pr.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var fit = pr.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1f;
            pr.gameObject.AddComponent<LintIgnoreClip>().reason = "square face scaled to cover the frame, cropped at the bottom";
            sm.photo = raw; sm.fitter = fit;
            if (texture != null) sm.SetPhoto(texture);
            return sm;
        }

        /// <summary>Shows `texture` (or the part `uv` of it, in 0..1) and fits it to the frame at its own aspect ratio.</summary>
        public void SetPhoto(Texture texture, Rect uv = default)
        {
            if (uv.width <= 0f || uv.height <= 0f) uv = new Rect(0f, 0f, 1f, 1f);
            photo.texture = texture; photo.uvRect = uv;
            photo.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            float w = texture != null ? texture.width * uv.width : 1f, h = texture != null ? texture.height * uv.height : 1f;
            fitter.aspectRatio = h > 0f ? w / h : 1f;
        }

        /// <summary>Shows a sprite (an atlas sprite too, by its rect) without creating anything.</summary>
        public void SetPhoto(Sprite sprite)
        {
            if (sprite == null) { SetPhoto((Texture)null); return; }
            Texture2D tex = sprite.texture; Rect r = sprite.textureRect;
            SetPhoto(tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height));
        }
    }
}
