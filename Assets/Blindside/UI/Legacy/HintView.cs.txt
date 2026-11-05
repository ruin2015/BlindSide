// The hint bar (#hint), split out of PlayerSelectionController so it can be drawn from plain data — by the game (setHint) and by
// the editor previews. Construction, SetHint's layout and the hintIn slide are the controller's former BuildHint / SetHint / HintIn
// bodies, unchanged; the button actions are handed back through a callback.
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public struct HintButton
    {
        public string id, label;
        public bool on;                    // .hb.on (accent background)
        public HintButton(string id, string label, bool on) { this.id = id; this.label = label; this.on = on; }
    }

    public class HintView : MonoBehaviour
    {
        static readonly Color Ink = UIPalette.Ink, Blu = UIPalette.Blue, Acc = UIPalette.Accent;
        public static readonly Vector2 HintPos = new Vector2(-135f, 20f);   // #hint{left:calc(50% - 135px);bottom:20px}

        public RectTransform Root => (RectTransform)transform;
        public bool Visible => gameObject.activeSelf;
        /// <summary>Runs hintIn (the controller, which stays active). Defaults to the view.</summary>
        public MonoBehaviour CoroutineHost;
        MonoBehaviour Host => CoroutineHost != null ? CoroutineHost : this;

        RectTransform hintBtnRow;
        TMP_Text hintMain, hintSub;
        CanvasGroup hintGroup;
        Coroutine hintAnim;

        /// <summary>Builds the (hidden) hint bar under `parent`.</summary>
        public static HintView Build(Transform parent)
        {
            // #hint{left:50%;bottom:18px;transform:translateX(-50%);display:flex;align-items:center;gap:10px;
            //       background:var(--ink);color:#fff;padding:9px 12px 9px 0;font-size:14px;font-weight:700;
            //       min-width:300px;border-bottom:3px solid var(--blu)}
            // the mockup's #hint{left:calc(50% - 135px);bottom:20px} at UITheme.hud.hint (400×48), padding-left 44px
            var hintRoot = HudKit.NewRect("Hint", parent);
            hintRoot.anchorMin = hintRoot.anchorMax = new Vector2(.5f, 0f);
            hintRoot.pivot = new Vector2(0f, 0f);
            hintRoot.anchoredPosition = HintPos;
            hintRoot.sizeDelta = UITheme.Current.hud.hint;
            HudKit.AddImage(hintRoot, Ink, raycast: true);
            var sh = hintRoot.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, .5f); sh.effectDistance = new Vector2(0, -10);
            var v = hintRoot.gameObject.AddComponent<HintView>();
            v.hintGroup = hintRoot.gameObject.AddComponent<CanvasGroup>();

            // #hint::before — 10px repeating-linear-gradient(135deg,var(--acc) 0 4px,var(--ink) 4px 8px)
            var stripe = HudKit.NewRect("Stripe", hintRoot);
            HudKit.LE(stripe).ignoreLayout = true;
            stripe.anchorMin = new Vector2(0, 0); stripe.anchorMax = new Vector2(0, 1); stripe.pivot = new Vector2(0, .5f);
            stripe.anchoredPosition = Vector2.zero; stripe.sizeDelta = new Vector2(30, 0);   // #hint::before{width:30px}
            var raw = stripe.gameObject.AddComponent<RawImage>();
            raw.texture = HudKit.HazardTexture(); raw.raycastTarget = false;
            raw.uvRect = new Rect(0, 0, 30f / 11f, 4f);

            // border-bottom:3px solid var(--blu)
            var bb = HudKit.NewRect("BorderBottom", hintRoot);
            HudKit.LE(bb).ignoreLayout = true;
            bb.anchorMin = new Vector2(0, 0); bb.anchorMax = new Vector2(1, 0); bb.pivot = new Vector2(.5f, 0);
            bb.anchoredPosition = Vector2.zero; bb.sizeDelta = new Vector2(0, 3);
            HudKit.AddImage(bb, Blu);

            // #hint b{font:900 13.5px/1.25} small{12px/1.25}: one line each, placed by Set
            var col = HudKit.NewRect("Text", hintRoot);
            v.hintMain = HudKit.AddText(col, "", 13.5f, Color.white, true, TextAlignmentOptions.TopLeft, "Main", UITheme.TextRole.Title);
            v.hintMain.overflowMode = TextOverflowModes.Ellipsis;
            v.hintSub = HudKit.AddText(col, "", 12, new Color(1, 1, 1, .7f), false, TextAlignmentOptions.TopLeft, "Sub", UITheme.TextRole.Body);
            v.hintSub.overflowMode = TextOverflowModes.Ellipsis;

            // #hint .esc{position:absolute;right:26px;11px;padding:2px 8px}
            v.hintBtnRow = HudKit.NewRect("Buttons", hintRoot);
            UILayout.Row(v.hintBtnRow, 10, null, TextAnchor.MiddleRight);

            hintRoot.gameObject.SetActive(false);
            return v;
        }

        /// <summary>Port of setHint(main,sub,btns) — `on` is the button's `.hb.on` (accent background); a click calls `onAction(id)`.</summary>
        public void Set(string main, string sub, IList<HintButton> btns, System.Action<string> onAction)
        {
            var hintRoot = Root;
            hintRoot.gameObject.SetActive(true);
            hintMain.text = main;
            hintSub.text = sub ?? "";
            hintSub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            for (int i = hintBtnRow.childCount - 1; i >= 0; i--) { var ch = hintBtnRow.GetChild(i); ch.SetParent(null, false); HudKit.DestroySafe(ch.gameObject); }
            float btnW = 0f;
            foreach (var b in btns)
            {
                // #hint .hb{font:700 10px var(--fm);padding:5px 9px;background:var(--pp);color:var(--ink);white-space:nowrap}
                // #hint .hb.on{background:var(--acc)}
                var br = HudKit.NewRect("Hb_" + b.id, hintBtnRow);
                HudKit.AddImage(br, b.on ? Acc : HudKit.PaperGrad1, raycast: true);
                var bt = HudKit.AddText(br, b.label, 11, Ink, true, TextAlignmentOptions.Center);
                float bw = Mathf.Ceil(bt.GetPreferredValues(b.label).x) + 16f;
                UILayout.Fixed(br, bw, 19f);
                HudKit.Stretch(bt.rectTransform);
                var btn = br.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                string id = b.id;
                btn.onClick.AddListener(() => onAction?.Invoke(id));
                btnW += bw + (btnW > 0f ? 10f : 0f);
            }
            hintBtnRow.gameObject.SetActive(btns.Count > 0);

            // fixed box: the text column takes what the buttons leave, one line each (cut with … when longer)
            var size = UITheme.Current.hud.hint;
            const float TextX = 44f, BtnR = 26f;
            UILayout.Place(hintBtnRow, size.x - BtnR - btnW, (size.y - 19f) / 2f, btnW, 19f);
            float colW = size.x - TextX - (btnW > 0f ? BtnR + btnW + 10f : 16f);
            bool two = !string.IsNullOrEmpty(sub);
            UILayout.Place(hintMain.transform.parent as RectTransform, TextX, 0f, colW, size.y);
            UILayout.Place(hintMain.rectTransform, 0f, two ? 7f : 15f, colW, 17f);
            UILayout.Place(hintSub.rectTransform, 0f, 25f, colW, 15f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(hintRoot);

            // @keyframes hintIn{from{transform:translate(-50%,20px);opacity:0}} .3s cubic-bezier(.2,.9,.2,1)
            if (hintAnim != null) Host.StopCoroutine(hintAnim);
            if (HudKit.Animate) hintAnim = Host.StartCoroutine(HintIn());
            else { hintGroup.alpha = 1f; hintRoot.anchoredPosition = HintPos; hintAnim = null; }
        }

        IEnumerator HintIn()
        {
            var hintRoot = Root;
            float t = 0f;
            while (t < .3f)
            {
                t += Time.unscaledDeltaTime;
                float e = UIEase.Tactical(t / .3f);
                hintGroup.alpha = e;
                hintRoot.anchoredPosition = HintPos - new Vector2(0f, 20f * (1f - e));
                yield return null;
            }
            hintGroup.alpha = 1f; hintRoot.anchoredPosition = HintPos;
            hintAnim = null;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
