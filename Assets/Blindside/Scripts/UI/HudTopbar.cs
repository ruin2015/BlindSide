// #topbar (HUD_Topbar.prefab). CSS, 00_markup_and_styles.html:51-68; markup :443-447:
//   #topbar{top:12px;left:14px;right:14px;display:flex;justify-content:space-between;align-items:flex-start;gap:12px}
//   .tb-left{display:flex;align-items:stretch;box-shadow:var(--shadow);animation:dropIn .5s cubic-bezier(.2,.9,.2,1) both}
//   .tb-left .bar-k{width:8px;background:var(--ink)}  .tb-left .lg{padding:7px 14px 7px 12px}
//   .tb-logo{font:700 22px/1 var(--fd);letter-spacing:.14em}
//   .tb-logo small{display:block;font:500 9px var(--fm);letter-spacing:.12em;color:var(--dimp);margin-top:3px}
//   #modeTag{font-size:11px;font-weight:700;margin-top:5px;color:var(--ink2)}
//   .tb-left .bc{width:44px;background:repeating-linear-gradient(90deg,ink 0 2px,transparent 2px 4px,ink 4px 5px,
//                transparent 5px 8px,ink 8px 11px,transparent 11px 13px);opacity:.75;margin:10px 10px 10px 0}
//   .tb-center{display:flex;align-items:stretch;box-shadow:var(--shadow);animation:dropIn .5s .08s ...}
//   #phaseBadge{padding:8px 18px 8px 26px;font:700 16px/1.1 var(--fd);letter-spacing:.12em;color:#fff;background:var(--ink);
//               flex-direction:column;justify-content:center;min-width:210px}
//   #phaseBadge::before{left:0;top:0;bottom:0;width:10px;background:var(--blu);transition:background .3s}
//   #phaseBadge small{font:500 10px var(--fk);letter-spacing:0;opacity:.7;margin-top:2px}
//   #phaseBadge.foe::before{background:var(--foe)} #phaseBadge.prep::before{background:var(--acc)}
//   #roundBox{padding:6px 16px;display:flex;align-items:baseline;gap:4px}  .lbl{9px;margin-right:6px;dimp}  b{30px/1}  span{13px;dimp}
//   .tb-right — HudObjective (nested HUD_Objective.prefab), animation:dropIn .5s .16s
//   @keyframes dropIn{from{transform:translateY(-24px);opacity:0}to{transform:none;opacity:1}}
// The three blocks are positioned by hand (space-between) so the dropIn offset can ride on top of
// the computed position without a LayoutGroup rebuild snapping it back.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    public class HudTopbar : MonoBehaviour
    {
        [Header(".tb-left")]
        public RectTransform left;
        public RectTransform leftLogoCol;    // .lg
        public TMP_Text logoText, logoSmall, modeTag;
        public RectTransform barcode;        // .bc

        [Header(".tb-center")]
        public RectTransform center;
        public RectTransform phaseBadge;
        public Image phaseMarker;            // #phaseBadge::before
        public TMP_Text phaseMain, phaseSub;
        public RectTransform roundBox;
        public TMP_Text roundText;

        [Header(".tb-right")]
        public HudObjective objective;

        public enum BadgeTone { Ally, Foe, Prep }

        const float DropDistance = 24f, DropDur = .5f;

        readonly float[] dropOffset = new float[3];
        readonly float[] dropAlpha = { 1f, 1f, 1f };
        Vector2 leftSize, centerSize;
        Coroutine markerTween, dropTween;
        bool hasTone; BadgeTone tone;

        RectTransform Rt => (RectTransform)transform;
        RectTransform Right => objective != null ? (RectTransform)objective.transform : null;

        void OnEnable() { Relayout(); PlayDropIn(); }
        void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) Position(); }

        /// <summary>#modeTag text.</summary>
        public void SetMode(string text) { if (modeTag.text != text) { modeTag.text = text; Relayout(); } }

        /// <summary>#phaseBadge innerHTML (`MAIN<small>sub</small>`) + its .foe/.prep class.</summary>
        public void SetPhase(string main, string sub, BadgeTone t)
        {
            bool changed = phaseMain.text != main || phaseSub.text != sub;
            phaseMain.text = main; phaseSub.text = sub;
            if (!hasTone || t != tone)
            {
                Color to = t == BadgeTone.Foe ? UIPalette.Foe : t == BadgeTone.Prep ? UIPalette.Accent : UIPalette.Blue;
                if (markerTween != null) StopCoroutine(markerTween);
                if (hasTone && isActiveAndEnabled) markerTween = StartCoroutine(TweenMarker(to));   // transition:background .3s
                else phaseMarker.color = to;
                hasTone = true; tone = t;
            }
            if (changed) Relayout();
        }

        /// <summary>#roundNum (padStart 2) and #roundMax.</summary>
        public void SetRound(int round, int max)
        {
            string s = $"<size=11><b>ROUND</b></size><space=6><size=30><b>{Mathf.Max(0, round):00}</b></size><space=4><size=12><color=#6A7078>/{max}</color></size>";
            if (roundText.text != s) { roundText.text = s; Relayout(); }
        }

        IEnumerator TweenMarker(Color to)
        {
            Color from = phaseMarker.color; float t = 0f;
            while (t < .3f) { t += Time.unscaledDeltaTime; phaseMarker.color = Color.Lerp(from, to, UIEase.InOut(t / .3f)); yield return null; }
            phaseMarker.color = to; markerTween = null;
        }

        /// <summary>Lays the blocks out at the mockup's fixed sizes (UITheme.hud): #tb .lg (logoWidth × topbarHeight), .pb (phaseWidth) +
        /// .rd (roundWidth, overlapping it by 14px), and .ob (HudObjective, UITheme.hud.objective).</summary>
        public void Relayout()
        {
            var H = UITheme.Current.hud;
            float th = H.topbarHeight;
            // the bar spans the canvas width; each block is placed in canvas coordinates (y = 12px from the top)
            Rt.anchorMin = new Vector2(0f, 1f); Rt.anchorMax = new Vector2(1f, 1f); Rt.pivot = new Vector2(.5f, 1f);
            Rt.offsetMin = new Vector2(0f, -(TopY + Mathf.Max(th, H.objective.y))); Rt.offsetMax = Vector2.zero;

            // #tb .lg{width:280px;height:46px;padding:6px 36px 0 30px} b{24px/1} small{11px/1.25;margin-top:2px}
            float lw = H.logoWidth, textW = lw - 30f - 36f;
            Place(leftLogoCol, 0f, 0f, lw, th);
            Place((RectTransform)logoText.transform, 30f, -6f, textW, 24f);
            logoText.fontSize = 24f;
            UILayout.LineHeight(logoText, 1f);
            Place((RectTransform)logoSmall.transform, 30f, -32f, textW, 14f);
            logoSmall.fontSize = 11f;
            modeTag.gameObject.SetActive(false);                  // no slot in .lg — the phase badge's small line carries the side
            Place(barcode, lw - 30f, -10f, 20f, th - 20f);
            leftSize = new Vector2(lw, th);
            left.sizeDelta = leftSize;

            // #tb .pb{width:290px;height:46px;padding:6px 16px 0 42px} ::before{left:12px;width:10px} b{21px/1} small{12px/1.25}
            float pw = H.phaseWidth;
            Place(phaseBadge, 0f, 0f, pw, th);
            Place((RectTransform)phaseMarker.transform, 12f, 0f, 10f, th);
            Place((RectTransform)phaseMain.transform, 42f, -6f, pw - 42f - 16f, 21f);
            phaseMain.fontSize = 21f;
            UILayout.LineHeight(phaseMain, 1f);
            Place((RectTransform)phaseSub.transform, 42f, -28f, pw - 42f - 16f, 15f);
            phaseSub.fontSize = 12f;
            // #tb .rd{width:130px;margin-left:-14px;padding:7px 16px 0 30px}
            float rw = H.roundWidth;
            Place(roundBox, pw - 14f, 0f, rw, th);
            Place((RectTransform)roundText.transform, 30f, -7f, rw - 30f - 16f, 32f);
            centerSize = new Vector2(pw - 14f + rw, th);
            center.sizeDelta = centerSize;

            if (objective != null) objective.Relayout();
            Position();
        }

        const float TopY = 12f, Side = 20f, PhaseFromCentre = 203f;

        /// <summary>#tb .lg{left:20px} · .ph{left:calc(50% - 203px)} · .ob{right:20px}, all top:12px.</summary>
        void Position()
        {
            if (left == null || center == null) return;
            float W = Rt.rect.width;
            float rightW = objective != null ? objective.Width : 0f;
            SetBlock(left, 0, Side);
            SetBlock(center, 1, W / 2f - PhaseFromCentre);
            if (Right != null) SetBlock(Right, 2, W - Side - rightW);
        }

        void SetBlock(RectTransform rt, int i, float x)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -TopY + dropOffset[i]);
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = dropAlpha[i];
        }

        public void PlayDropIn()
        {
            if (dropTween != null) StopCoroutine(dropTween);
            dropTween = StartCoroutine(DropIn());
        }

        IEnumerator DropIn()
        {
            float[] delay = { 0f, .08f, .16f };                   // tb-left / tb-center / tb-right
            float t = 0f;
            while (true)
            {
                bool done = true;
                for (int i = 0; i < 3; i++)
                {
                    float p = Mathf.Clamp01((t - delay[i]) / DropDur);   // animation-fill-mode:both
                    float e = UIEase.Tactical(p);
                    dropOffset[i] = DropDistance * (1f - e);              // translateY(-24px): up is +y here
                    dropAlpha[i] = e;
                    if (p < 1f) done = false;
                }
                Position();
                if (done) break;
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            dropTween = null;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
