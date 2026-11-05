// The confirmation dialog (#dialog .mbox / the mockup's #md), split out of PlayerSelectionController so it can be drawn from plain
// data — by the game (modal()) and by the editor previews. Construction, LayoutDialog and the panelIn reveal are the controller's
// former BuildDialog / LayoutDialog / ModalRoutine drawing, unchanged; waiting for the answer stays with the controller.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class DialogView : MonoBehaviour
    {
        static readonly Color Ink = UIPalette.Ink, Ink2 = UIPalette.Ink2, Blu = UIPalette.Blue;

        public RectTransform Root => (RectTransform)transform;
        public RectTransform Box => dialogBox;
        public bool Visible => gameObject.activeSelf;
        /// <summary>Runs the reveal (the controller, which stays active). Defaults to the view.</summary>
        public MonoBehaviour CoroutineHost;
        MonoBehaviour Host => CoroutineHost != null ? CoroutineHost : this;

        RectTransform dialogBox, dialogHead, dialogBodyBox, dialogButtons;
        TMP_Text dialogTitle, dialogEn, dialogBody, dialogYesText, dialogNoText;
        RectMask2D dialogMask;
        CanvasGroup dialogGroup;
        RawImage hatchRaw;
        Image dialogYesImg;

        /// <summary>Builds the (hidden) full-screen dialog under `parent`; the buttons call `onYes` / `onNo`.</summary>
        public static DialogView Build(Transform parent, System.Action onYes, System.Action onNo)
        {
            // .modal{position:fixed;inset:0;z-index:60;place-items:center;background:rgba(3,6,10,.45)}
            var dialogRoot = HudKit.NewRect("Dialog", parent);
            HudKit.Stretch(dialogRoot);
            HudKit.AddImage(dialogRoot, new Color32(3, 6, 10, (byte)(.45f * 255)), raycast: true);
            var v = dialogRoot.gameObject.AddComponent<DialogView>();

            // .mbox{min-width:340px;max-width:480px;box-shadow:var(--shadow);background:var(--hatch),var(--pp)}
            var dialogBox = HudKit.NewRect("MBox", dialogRoot);
            v.dialogBox = dialogBox;
            dialogBox.anchorMin = dialogBox.anchorMax = dialogBox.pivot = new Vector2(.5f, .5f);
            var boxImg = HudKit.AddImage(dialogBox, HudKit.PaperGrad1, raycast: true);
            var sh = dialogBox.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, .5f); sh.effectDistance = new Vector2(0, -10);
            v.dialogMask = dialogBox.gameObject.AddComponent<RectMask2D>();
            v.dialogGroup = dialogBox.gameObject.AddComponent<CanvasGroup>();
            // #md .bx{width:420px} (UITheme.hud.dialogWidth); the parts are placed by LayoutDialog when it opens

            // --pp: linear-gradient(160deg,#f4f5f6 0%,#e3e6e9 70%,#d6d9dd 100%) + --hatch
            var grad = HudKit.NewRect("Pp", dialogBox);
            HudKit.LE(grad).ignoreLayout = true; HudKit.Stretch(grad);
            var graw = grad.gameObject.AddComponent<RawImage>();
            graw.texture = HudKit.PaperGradientTexture(); graw.raycastTarget = false;
            var hatch = HudKit.NewRect("Hatch", dialogBox);
            HudKit.LE(hatch).ignoreLayout = true; HudKit.Stretch(hatch);
            var hraw = hatch.gameObject.AddComponent<RawImage>();
            hraw.texture = HudKit.HatchTexture(); hraw.raycastTarget = false;
            v.hatchRaw = hraw;
            boxImg.color = Color.clear;
            boxImg.raycastTarget = true;

            // .mh{background:var(--ink);color:#fff;padding:11px 16px;font-weight:900;font-size:17px;
            //     display:flex;justify-content:space-between;align-items:center;gap:16px;border-left:8px solid var(--blu)}
            // #md .hd{padding:12px 52px 12px 28px;font:900 18px/1.25;border-left:12px} small{11px}
            v.dialogHead = HudKit.NewRect("Mh", dialogBox);
            HudKit.AddImage(v.dialogHead, Ink);
            var mhb = HudKit.NewRect("Border", v.dialogHead);
            mhb.anchorMin = new Vector2(0, 0); mhb.anchorMax = new Vector2(0, 1); mhb.pivot = new Vector2(0, .5f);
            mhb.anchoredPosition = Vector2.zero; mhb.sizeDelta = new Vector2(12, 0);
            HudKit.AddImage(mhb, Blu);
            v.dialogTitle = HudKit.AddText(v.dialogHead, "", 18, Color.white, true, TextAlignmentOptions.MidlineLeft, "Title", UITheme.TextRole.Title);
            v.dialogEn = HudKit.AddText(v.dialogHead, "", 11, new Color(1, 1, 1, .6f), true, TextAlignmentOptions.MidlineRight, "En", UITheme.TextRole.Display);

            // .mb{padding:14px 16px;font-size:13px;color:var(--ink2);line-height:1.65}
            // #md .bd{padding:14px 28px;font:500 14px/1.6}
            v.dialogBodyBox = HudKit.NewRect("Mb", dialogBox);
            v.dialogBody = HudKit.AddText(v.dialogBodyBox, "", 14, Ink2, false, TextAlignmentOptions.TopLeft, "Body", UITheme.TextRole.Body);
            v.dialogBody.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(v.dialogBody, 1.6f);

            // .mbtns{display:flex} button{flex:1;padding:13px;font-weight:900;font-size:14px;background:var(--paper3);color:var(--ink)}
            //   button:first-child{background:var(--blu);color:#fff}
            // #md .bt{height:52px} button{flex:1;font:900 16px/52px}
            var row = HudKit.NewRect("Mbtns", dialogBox);
            v.dialogButtons = row;
            v.dialogYesText = BuildDialogButton(row, Blu, Color.white, () => onYes?.Invoke());
            v.dialogYesImg = v.dialogYesText.transform.parent.GetComponent<Image>();
            v.dialogNoText = BuildDialogButton(row, HudKit.Paper3, Ink, () => onNo?.Invoke());

            dialogRoot.SetAsLastSibling();
            dialogRoot.gameObject.SetActive(false);
            return v;
        }

        static TMP_Text BuildDialogButton(RectTransform row, Color bg, Color fg, UnityEngine.Events.UnityAction onClick)
        {
            var b = HudKit.NewRect("Btn", row);
            var img = HudKit.AddImage(b, bg, raycast: true);
            var btn = b.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;   // button:hover{filter:brightness(1.1)}
            cb.normalColor = Color.white; cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            cb.pressedColor = Color.white; cb.selectedColor = Color.white; cb.fadeDuration = .12f; cb.colorMultiplier = 1.1f;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);
            var t = HudKit.AddText(b, "", 16, fg, true, TextAlignmentOptions.Center, role: UITheme.TextRole.Title);
            HudKit.Stretch(t.rectTransform);
            return t;
        }

        /// <summary>#md .bx at UITheme.hud.dialogWidth: header 46, body (its text + 14px above and below), buttons 52 — the box is as tall as
        /// that sum, centred.</summary>
        void LayoutDialog()
        {
            float w = UITheme.Current.hud.dialogWidth;
            const float HeadH = 46f, BtnH = 52f, PadX = 28f, PadY = 14f;
            float textW = w - PadX * 2f;
            float bodyH = Mathf.Ceil(dialogBody.GetPreferredValues(dialogBody.text, textW, 0f).y) + PadY * 2f;
            dialogBox.sizeDelta = new Vector2(w, HeadH + bodyH + BtnH);
            UILayout.Place(dialogHead, 0, 0, w, HeadH);
            float enW = Mathf.Ceil(dialogEn.GetPreferredValues(dialogEn.text).x) + 1f;
            UILayout.Place(dialogTitle.rectTransform, 12f + PadX, 0, w - 12f - PadX - 52f - enW - 8f, HeadH);
            UILayout.Place(dialogEn.rectTransform, w - 52f - enW, 0, enW, HeadH);
            UILayout.Place(dialogBodyBox, 0, HeadH, w, bodyH);
            UILayout.Place(dialogBody.rectTransform, PadX, PadY, textW, bodyH - PadY * 2f);
            UILayout.Place(dialogButtons, 0, HeadH + bodyH, w, BtnH);
            UILayout.Place((RectTransform)dialogYesText.transform.parent, 0, 0, w / 2f, BtnH);
            UILayout.Place((RectTransform)dialogNoText.transform.parent, w / 2f, 0, w / 2f, BtnH);
        }

        /// <summary>modal()'s drawing: fills and shows the dialog. `yesBg` is the first button's background (default --blu;
        /// `.mbtns button.red` passes --foe); `<b>…</b>` in the body becomes the 26px blue figure.</summary>
        public void Open(string title, string en, string body, string yes, string no, Color? yesBg = null)
        {
            if (dialogYesImg != null) dialogYesImg.color = yesBg ?? Blu;
            dialogTitle.text = title;
            dialogEn.text = en;
            // #md .bd b{color:var(--blu);font:700 26px var(--fd)}
            dialogBody.text = body.Replace("<b>", $"<b><size=26><color=#{ColorUtility.ToHtmlStringRGB(Blu)}>").Replace("</b>", "</color></size></b>");
            dialogYesText.text = yes; dialogNoText.text = no;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();

            LayoutDialog();
            if (hatchRaw != null) hatchRaw.uvRect = new Rect(0, 0, dialogBox.rect.width / 7f, dialogBox.rect.height / 7f);

            // .mbox{animation:panelIn .3s cubic-bezier(.2,.9,.2,1)}
            if (HudKit.Animate) Host.StartCoroutine(UIAnim.ClipReveal(dialogMask, dialogGroup, .3f));
        }

        public void Close() => Root.gameObject.SetActive(false);
    }
}
