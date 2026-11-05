// Split out of BlindsideUIAnim.cs: Unity only serializes one MonoScript "main class" per file
// reliably, so every MonoBehaviour that gets AddComponent'd (and, above all, ever saved into a
// prefab) needs its own file, or its component reference silently rots into "Missing (Mono Script)"
// on the next reload. Body copied verbatim from the original file.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    // ------------------------------------------------------------------
    //  Operator illustration card (artIn)
    // ------------------------------------------------------------------
    public class UIOperatorArtCard : MonoBehaviour, ITimedEffect
    {
        public RectMask2D mask; public CanvasGroup group;
        public Image art, colorBar, whiteFlash;   // whiteFlash: full-card white Image on top, raycastTarget off
        public TMP_Text nameText, subText, abilityText, fileTag;
        Vector3 basePos; string current; bool basePosSet;
        void Awake() { basePos = transform.localPosition; basePosSet = true; group.alpha = 0f; }

        /// <summary>The mockup's #oc at UITheme.hud.artCard (300×500, right 20 / top 76): .tag (artCardTag, 11px, padding-left 18) on top,
        /// the .nm block (artCardName) at the bottom with its 5px colour line — b 34px, span 13px, em 12.5px at right 20 / bottom 11 —
        /// and the illustration window between them.</summary>
        public void ApplyLayout()
        {
            var H = UITheme.Current.hud;
            float w = H.artCard.x, h = H.artCard.y, tagH = H.artCardTag, nameH = H.artCardName;
            var rt = (RectTransform)transform;
            UILayout.Anchor(rt, new Vector2(1f, 1f), -20f, -76f, w, h);
            UILayout.Place((RectTransform)fileTag.transform.parent, 0, 0, w, tagH);
            fileTag.fontSize = 11; fileTag.alignment = TextAlignmentOptions.MidlineLeft;
            fileTag.rectTransform.offsetMin = new Vector2(18f, 0f); fileTag.rectTransform.offsetMax = Vector2.zero;
            UILayout.Place(art.rectTransform, 0, tagH, w, h - tagH - nameH);
            UILayout.Place(colorBar.rectTransform, 0, h - nameH, w, 5);
            UILayout.Place((RectTransform)nameText.transform.parent, 18, h - nameH + 5 + 11, w - 36, 34);
            nameText.fontSize = 34; nameText.alignment = TextAlignmentOptions.TopLeft;
            UILayout.LineHeight(nameText, 1f);
            UILayout.Place((RectTransform)subText.transform.parent, 18, h - nameH + 5 + 11 + 34 + 5, w - 36, 17);
            subText.fontSize = 13; subText.alignment = TextAlignmentOptions.TopLeft;
            UILayout.Place((RectTransform)abilityText.transform.parent, 18, h - 11 - 16, w - 18 - 20, 16);
            abilityText.fontSize = 12.5f; abilityText.alignment = TextAlignmentOptions.BottomRight;
            basePos = transform.localPosition; basePosSet = true;     // the shake returns here
        }

        public void Show(Sprite illustration, string key, string krRole, string ability, string tag, Color opColor)
        {
            if (current == key && group.alpha > .99f) return;
            current = key;
            SetContent(illustration, key, krRole, ability, tag, opColor);
            StopAllCoroutines(); StartCoroutine(Run());
        }

        /// <summary>The card's picture, texts and colours, without playing the reveal.</summary>
        public void SetContent(Sprite illustration, string key, string krRole, string ability, string tag, Color opColor)
        {
            SetPhoto(illustration); nameText.text = key; subText.text = krRole; abilityText.text = "✦ " + ability; fileTag.text = tag;
            colorBar.color = opColor; abilityText.color = opColor;
        }
        public void Hide() { StopAllCoroutines(); group.alpha = 0f; current = null; }

        // The illustration fills the Art window edge to edge ("cover") and is pinned to its top, so the head is never cropped —
        // only the bottom of the picture can be. `art` itself keeps its placeholder colour behind the (transparent) picture.
        Image photo;
        void SetPhoto(Sprite s)
        {
            if (s == null) { if (photo != null) photo.gameObject.SetActive(false); return; }
            if (photo == null)
            {
                if (art.GetComponent<RectMask2D>() == null) art.gameObject.AddComponent<RectMask2D>();   // clips the overflow to the window
                var go = new GameObject("Photo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(art.transform, false);
                photo = go.GetComponent<Image>(); photo.raycastTarget = false;
                var rt = photo.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f); rt.pivot = new Vector2(.5f, 1f); rt.anchoredPosition = Vector2.zero;
            }
            photo.gameObject.SetActive(true);
            photo.sprite = s; photo.color = Color.white;
            var window = art.rectTransform.rect;
            float k = Mathf.Max(window.width / s.rect.width, window.height / s.rect.height);
            photo.rectTransform.sizeDelta = new Vector2(s.rect.width * k, s.rect.height * k);
        }

        const float RevealDur = .6f;
        public float Duration => RevealDur;

        IEnumerator Run()
        {
            group.alpha = 1f;
            float w = ((RectTransform)mask.transform).rect.width, t = 0f;
            while (t < RevealDur)
            {
                t += Time.unscaledDeltaTime;
                Pose(t, w);
                yield return null;
            }
            Settle();
        }

        void Pose(float t, float w)
        {
            float p = t / RevealDur;
            float reveal = UIEase.Tactical(Mathf.Clamp01(p / .35f));
            mask.padding = new Vector4(w * (1f - reveal), 0, 0, 0);           // right → left reveal
            if (whiteFlash) { var c = Color.white; c.a = Mathf.Lerp(.75f, 0f, Mathf.Clamp01(p / .6f)); whiteFlash.color = c; }
            float x = p < .48f ? Mathf.Lerp(0, -8, (p - .35f) / .13f) * (p > .35f ? 1 : 0)
                    : p < .60f ? Mathf.Lerp(-8, 4, (p - .48f) / .12f)
                    : p < .72f ? Mathf.Lerp(4, -2, (p - .60f) / .12f)
                    : Mathf.Lerp(-2, 0, (p - .72f) / .28f);
            transform.localPosition = basePos + new Vector3(x, 0, 0);
        }

        void Settle() { mask.padding = Vector4.zero; transform.localPosition = basePos; if (whiteFlash) whiteFlash.color = new Color(1, 1, 1, 0); }

        /// <summary>The card `t` seconds into its reveal (t ≥ .6: fully shown and settled, as after Run). Shows the card (alpha 1).</summary>
        public void Evaluate(float t)
        {
            if (!basePosSet) { basePos = transform.localPosition; basePosSet = true; }
            group.alpha = 1f;
            if (t >= RevealDur) { Settle(); return; }
            Pose(t, ((RectTransform)mask.transform).rect.width);
        }
    }
}
