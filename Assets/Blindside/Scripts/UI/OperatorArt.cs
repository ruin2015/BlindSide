// Lookup of the operators' sprites by key (CRUSHER, …) — Body(key) for the illustration card, Face(key) for the small portraits —
// and the shared face-slot look: a light paper gradient (#eef1f3 at the top → #c9d0d6 at the bottom) behind the face, no monogram.
// A key without art returns null; callers then keep their old look (operator colour + two-letter monogram).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public static class OperatorArt
    {
        const string LibraryResource = "OperatorArtLibrary";
        static readonly Color32 PaperTop = new Color32(0xee, 0xf1, 0xf3, 0xff), PaperBottom = new Color32(0xc9, 0xd0, 0xd6, 0xff);

        static Dictionary<string, OperatorArtLibrary.Entry> byKey;
        static Sprite paper;

        static bool TryGet(string key, out OperatorArtLibrary.Entry e)
        {
            if (byKey == null)
            {
                byKey = new Dictionary<string, OperatorArtLibrary.Entry>();
                var lib = Resources.Load<OperatorArtLibrary>(LibraryResource);
                if (lib == null) Debug.LogWarning("[OperatorArt] Resources/" + LibraryResource + " not found — build it from Blindside > UI > Build Operator Art Library.");
                else foreach (var x in lib.entries) if (!string.IsNullOrEmpty(x.key)) byKey[x.key] = x;
            }
            e = default;
            return key != null && byKey.TryGetValue(key, out e);
        }

        /// <summary>The upper-body illustration for the card, or null.</summary>
        public static Sprite Body(string key) => TryGet(key, out var e) ? e.body : null;

        /// <summary>The face for the small portraits, or null.</summary>
        public static Sprite Face(string key) => TryGet(key, out var e) ? e.face : null;

        /// <summary>The paper background of a face slot: a 1×32 vertical gradient, stretched.</summary>
        public static Sprite PaperGradient
        {
            get
            {
                if (paper != null) return paper;
                var tex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < 32; y++) tex.SetPixel(0, y, Color.Lerp(PaperBottom, PaperTop, y / 31f));   // y = 0 is the bottom row
                tex.Apply(false, true);
                paper = Sprite.Create(tex, new Rect(0, 0, 1, 32), new Vector2(.5f, .5f), 100f);
                paper.hideFlags = HideFlags.HideAndDontSave;
                return paper;
            }
        }

        /// <summary>Puts the operator's face on `slot` (an Image that is the portrait's background): the paper gradient as its own sprite and a
        /// "Face" child Image on top of its other children's bottom. The face covers the slot at its own aspect ratio, pinned to the top edge
        /// (object-fit:cover; object-position:50% top) and clipped by the slot — never squashed. Returns the face Image, or null when this
        /// key has no face — `slot` is then left as it was.</summary>
        public static Image ApplyFace(Image slot, string key)
        {
            var face = Face(key);
            if (slot == null || face == null) return null;
            slot.sprite = PaperGradient; slot.type = Image.Type.Simple; slot.color = Color.white;
            if (slot.GetComponent<RectMask2D>() == null) slot.gameObject.AddComponent<RectMask2D>();
            var t = slot.transform.Find("Face") as RectTransform;
            if (t == null)
            {
                var go = new GameObject("Face", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AspectRatioFitter));
                t = (RectTransform)go.transform;
                t.SetParent(slot.transform, false);
                t.SetAsFirstSibling();       // under the slot's other children (shade, K.I.A. overlay)
            }
            // stretched with the pivot on the top edge: EnvelopeParent grows the box past the slot downwards / sideways only
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.pivot = new Vector2(.5f, 1f);
            t.anchoredPosition = Vector2.zero;
            t.gameObject.SetActive(true);
            var img = t.GetComponent<Image>();
            img.sprite = face; img.preserveAspect = false; img.color = Color.white; img.raycastTarget = false;
            var fit = t.GetComponent<AspectRatioFitter>() ?? t.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = face.rect.height > 0 ? face.rect.width / face.rect.height : 1f;
            return img;
        }
    }
}
